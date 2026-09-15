// Validación del desglose de huevos POR ÍTEMS contra la BD: catálogo de la empresa, lista blanca del
// lote (lote_huevo_items) y vigencia de primera postura. La comparten el seguimiento diario de
// PRODUCCIÓN y el de LEVANTE. Nació privada en ProduccionService y se movió acá SIN cambios de
// comportamiento (sep-2026) cuando levante empezó a clasificar por ítems: dos copias podrían aceptar
// conjuntos distintos para el mismo lote.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

internal static class HuevoItemsLoteValidacion
{
    /// <summary>
    /// Valida el desglose de huevos por ítems del request:
    /// (a) reglas puras (cantidad ≥ 0, id &gt; 0, sin repetidos) — <see cref="HuevoItemsCalculos.Validar"/>;
    /// (b) la empresa de la granja del lote debe tener <c>clasificacion_huevo_por_items = true</c>;
    /// (c) todos los <c>catalogItemId</c> deben existir en <c>catalogo_items</c> de esa empresa con
    ///     <c>item_type = 'huevo'</c> y estar <b>activos</b> (una sola query, comparación de conjuntos);
    /// (d) <b>F7.3</b> — todos deben estar entre los tipos que el LOTE declaró producir
    ///     (<c>lote_huevo_items</c>). <b>Fail-closed:</b> un lote sin declarar rechaza todo;
    /// (e) <b>D5</b> — un ítem de primera postura debe estar dentro de la vigencia por semana de
    ///     vida del lote (<c>Company.HuevoPrimeraPosturaHastaSemana</c>).
    /// Lanza <see cref="InvalidOperationException"/> (el controller la traduce a 400) con el detalle.
    /// </summary>
    /// <param name="fechaRegistro">
    /// Fecha del seguimiento, para calcular la semana de vida del lote. Necesaria desde D5: la
    /// vigencia era 100 % UI y la fecha es editable dentro del mismo modal, así que se podía elegir
    /// el ítem en semana 21 y guardarlo con fecha de semana 30.
    /// </param>
    internal static async Task<List<HuevoItemSeguimientoDto>> ValidarAsync(
        ZooSanMarinoContext context, int loteId, List<HuevoItemSeguimientoDto> huevoItems, DateTime fechaRegistro)
    {
        var error = HuevoItemsCalculos.Validar(huevoItems);
        if (error != null) throw new InvalidOperationException(error);

        var companyId = await ResolverCompanyIdDeGranjaDelLoteAsync(context, loteId).ConfigureAwait(false);

        var empresa = await context.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new { c.ClasificacionHuevoPorItems, c.HuevoPrimeraPosturaHastaSemana })
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (empresa?.ClasificacionHuevoPorItems != true)
            throw new InvalidOperationException(
                "La empresa de este lote no tiene habilitada la clasificación de huevos por ítems de inventario; use los campos de clasificación estándar.");

        var ids = huevoItems.Select(i => i.CatalogItemId).Distinct().ToArray();

        // D4 — se exige `Activo`. Antes no se filtraba, así que un ítem dado de baja seguía siendo
        // un id válido para guardar aunque el selector del front (CatalogItemService.GetByTypeAsync,
        // que sí filtra `Activo`) jamás lo ofreciera. Los dos gates ahora coinciden.
        var delCatalogo = await context.CatalogItems.AsNoTracking()
            .Where(ci => ci.CompanyId == companyId && ci.ItemType == "huevo" && ci.Activo && ids.Contains(ci.Id))
            .Select(ci => new { ci.Id, ci.Codigo, ci.Nombre, ci.Metadata })
            .ToListAsync()
            .ConfigureAwait(false);

        var faltantes = ids.Except(delCatalogo.Select(ci => ci.Id)).ToArray();
        if (faltantes.Length > 0)
            throw new InvalidOperationException(
                $"Los siguientes ítems no existen como ítem de huevo ACTIVO del catálogo de la empresa: {string.Join(", ", faltantes)}.");

        var nombrePorItem = delCatalogo.ToDictionary(ci => ci.Id, ci => ci.Nombre);

        // F7.3 — la lista blanca del lote. Va DESPUÉS del catálogo para que el mensaje sea el más
        // específico posible: primero "ese ítem no existe", recién después "existe pero este lote
        // no lo produce".
        var permitidos = await context.LoteHuevoItems.AsNoTracking()
            .Where(lhi => lhi.LoteId == loteId && lhi.Activo)
            .Select(lhi => lhi.CatalogItemId)
            .ToListAsync()
            .ConfigureAwait(false);

        var errorPermitidos = HuevoItemsCalculos.ValidarPermitidos(huevoItems, permitidos, nombrePorItem);
        if (errorPermitidos != null) throw new InvalidOperationException(errorPermitidos);

        // D5 — vigencia de primera postura, ahora también en el backend.
        var errorVigencia = await ValidarVigenciaPrimeraPosturaAsync(
            context, loteId, huevoItems,
            delCatalogo.ToDictionary(
                ci => ci.Id,
                ci => (ci.Nombre, (System.Text.Json.JsonDocument?)ci.Metadata)),
            empresa.HuevoPrimeraPosturaHastaSemana, fechaRegistro).ConfigureAwait(false);
        if (errorVigencia != null) throw new InvalidOperationException(errorVigencia);

        // ENRIQUECIMIENTO desde el catálogo. El desglose se persiste como SNAPSHOT en
        // `metadata.huevoItems`, y `fn_clasificacion_huevo_items_produccion` lee `codigo`, `nombre` y
        // `tipoHuevo` DIRECTO de ahí, sin join al catálogo. Hasta acá el backend confiaba en que el
        // cliente los mandara: el formulario los completa, pero cualquier otro llamador (la API
        // directa, un script, un cliente nuevo) guardaba el desglose con esos campos en NULL y el
        // reporte semanal salía con filas sin nombre. Detectado en el smoke del 22-ago-2026.
        // Se completa solo lo que falta: si el cliente mandó una etiqueta, se respeta.
        var catalogoPorId = delCatalogo.ToDictionary(ci => ci.Id);
        for (var i = 0; i < huevoItems.Count; i++)
        {
            if (!catalogoPorId.TryGetValue(huevoItems[i].CatalogItemId, out var ci)) continue;

            huevoItems[i] = huevoItems[i] with
            {
                Codigo = string.IsNullOrWhiteSpace(huevoItems[i].Codigo) ? ci.Codigo : huevoItems[i].Codigo,
                Nombre = string.IsNullOrWhiteSpace(huevoItems[i].Nombre) ? ci.Nombre : huevoItems[i].Nombre,
                TipoHuevo = string.IsNullOrWhiteSpace(huevoItems[i].TipoHuevo)
                    ? LeerTextoDeMetadata(ci.Metadata, "tipoHuevo", "tipo_huevo")
                    : huevoItems[i].TipoHuevo,
                Um = string.IsNullOrWhiteSpace(huevoItems[i].Um)
                    ? LeerTextoDeMetadata(ci.Metadata, "um", "UM", "unidadMedida")
                    : huevoItems[i].Um
            };
        }

        return huevoItems;
    }

    /// <summary>Lee una clave de texto del metadata del catálogo, tolerando camelCase y snake_case.</summary>
    private static string? LeerTextoDeMetadata(System.Text.Json.JsonDocument? metadata, params string[] claves)
    {
        if (metadata is null || metadata.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            return null;

        foreach (var clave in claves)
        {
            if (!metadata.RootElement.TryGetProperty(clave, out var v)) continue;
            var texto = v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : v.ToString();
            if (!string.IsNullOrWhiteSpace(texto)) return texto.Trim();
        }
        return null;
    }

    /// <summary>
    /// D5 — rechaza un ítem marcado <c>metadata.primeraPostura</c> registrado más allá de la semana
    /// de vida configurada. Sin límite configurado o sin fecha de encaset no hay regla que aplicar
    /// (fail-open, mismo criterio que <see cref="HuevoPrimeraPosturaCalculos.EsVigente"/>).
    /// </summary>
    private static async Task<string?> ValidarVigenciaPrimeraPosturaAsync(
        ZooSanMarinoContext context,
        int loteId,
        List<HuevoItemSeguimientoDto> huevoItems,
        IReadOnlyDictionary<int, (string Nombre, System.Text.Json.JsonDocument? Metadata)> catalogo,
        int? hastaSemana,
        DateTime fechaRegistro)
    {
        if (hastaSemana is null) return null;

        var dePrimeraPostura = huevoItems
            .Where(i => catalogo.TryGetValue(i.CatalogItemId, out var ci) && EsPrimeraPostura(ci.Metadata))
            .ToList();
        if (dePrimeraPostura.Count == 0) return null;

        var fechaEncaset = await context.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId)
            .Select(l => (DateTime?)l.FechaEncaset)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (fechaEncaset is null) return null;

        // Misma fórmula canónica que el resto del repo (HuevosLevanteCalculos.SemanaVida y el espejo
        // del front): el día del encaset es la SEMANA 1.
        var dias = (fechaRegistro.Date - fechaEncaset.Value.Date).Days;
        if (dias < 0) return null;
        var semanaVida = (dias / 7) + 1;

        foreach (var item in dePrimeraPostura)
        {
            var nombre = catalogo.TryGetValue(item.CatalogItemId, out var ci) ? ci.Nombre : $"id {item.CatalogItemId}";
            var msg = HuevoPrimeraPosturaCalculos.MensajeFueraDeVigencia(hastaSemana, semanaVida, nombre);
            if (msg != null) return msg;
        }

        return null;
    }

    /// <summary>¿El ítem del catálogo está marcado como «huevo de primera postura»?</summary>
    private static bool EsPrimeraPostura(System.Text.Json.JsonDocument? metadata)
    {
        if (metadata is null || metadata.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            return false;

        foreach (var clave in new[]
                 {
                     HuevoPrimeraPosturaCalculos.MetadataKeyPrimeraPostura,
                     HuevoPrimeraPosturaCalculos.MetadataKeyPrimeraPosturaSnake
                 })
        {
            if (metadata.RootElement.TryGetProperty(clave, out var v)
                && v.ValueKind == System.Text.Json.JsonValueKind.True)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Empresa efectiva de la clasificación = empresa dueña de la GRANJA del lote (misma regla que
    /// el descuento de inventario, <c>farms.company_id</c>), no la empresa activa del token.
    /// </summary>
    private static async Task<int> ResolverCompanyIdDeGranjaDelLoteAsync(ZooSanMarinoContext context, int loteId)
    {
        var granjaId = await context.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.DeletedAt == null)
            .Select(l => (int?)l.GranjaId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (granjaId is null or <= 0)
            throw new InvalidOperationException($"No se pudo resolver la granja del lote {loteId} para clasificar los huevos por ítems.");

        var companyId = await context.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId.Value)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (companyId is null or <= 0)
            throw new InvalidOperationException($"No se pudo resolver la empresa de la granja {granjaId} para clasificar los huevos por ítems.");

        return companyId.Value;
    }
}

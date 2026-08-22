// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.HuevosPostura.cs
// Hoja "Huevos" del archivo de PRODUCCIÓN: clasificación por ÍTEM del catálogo (empresas con
// companies.clasificacion_huevo_por_items = true, ej. Santa Reyes). Una fila por fecha e ítem.
//
// Va en hoja aparte y no en columnas fijas porque el desglose es VARIABLE por empresa (Santa Reyes
// tiene 21 ítems de huevo) y MigracionEsquemas es un esquema estático que debe seguir siendo la
// fuente única de plantilla y validación.
//
// Reglas (espejo de ProduccionService.ValidarHuevoItemsAsync):
//   - empresa efectiva = dueña de la GRANJA del lote (farms.company_id), no la del token;
//   - el ítem debe existir en catalogo_items con item_type = 'huevo' de esa empresa;
//   - con el flag apagado, traer la hoja es un ERROR explícito (fail-closed, no se ignora en silencio);
//   - huevo_tot = suma de los ítems y las 11 columnas fijas quedan en 0 (regla de HuevoItemsCalculos).
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    /// <summary>
    /// Ítems de HUEVO del catálogo de la empresa (activos), como lista para la hoja Referencias y
    /// para resolver la columna "Ítem" por nombre o código normalizados.
    /// </summary>
    private async Task<List<(int Id, string? Codigo, string Nombre, string? TipoHuevo)>> CargarItemsHuevoEmpresaAsync(
        int companyId, CancellationToken ct)
    {
        var items = await _ctx.CatalogItems.AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.Activo && c.ItemType == "huevo")
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Codigo, c.Nombre, c.Metadata })
            .ToListAsync(ct);

        return items.Select(i => (i.Id, i.Codigo, i.Nombre, LeerTipoHuevo(i.Metadata))).ToList();

        // El tipo comercial (Primera / Pnc) vive en el metadata del catálogo; es informativo.
        static string? LeerTipoHuevo(JsonDocument? metadata)
        {
            if (metadata is null || metadata.RootElement.ValueKind != JsonValueKind.Object) return null;
            return metadata.RootElement.TryGetProperty("tipoHuevo", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
        }
    }

    /// <summary>
    /// Lee y valida la hoja "Huevos". Devuelve los ítems agrupados por FECHA; los problemas se
    /// acumulan en <paramref name="errores"/>. Si el archivo no trae la hoja, devuelve vacío sin error
    /// (es opcional: un archivo anterior se procesa exactamente igual).
    /// </summary>
    private async Task<Dictionary<DateTime, List<HuevoItemSeguimientoDto>>> LeerHojaHuevosPosturaAsync(
        IFormFile file, int companyId, LotePosturaCtx? loteCtx, List<MigracionErrorDto> errores, CancellationToken ct)
    {
        var porFecha = new Dictionary<DateTime, List<HuevoItemSeguimientoDto>>();

        var filas = LeerHojaOpcionalConEsquema(file, MigracionEsquemas.HuevosPostura, errores);
        if (filas.Count == 0) return porFecha;

        // Fail-closed: con el flag apagado NO se ignora la hoja en silencio — el usuario cargó un
        // desglose que no se iba a guardar en ninguna parte.
        if (loteCtx?.ClasificacionHuevoPorItems != true)
        {
            errores.Add(new(0, "Ítem", null,
                "La empresa de este lote no tiene habilitada la clasificación de huevos por ítems: quitá la hoja 'Huevos' y usá las columnas de clasificación de la hoja 'Datos'."));
            return porFecha;
        }

        var catalogo = await CargarItemsHuevoEmpresaAsync(companyId, ct);
        var porClave = new Dictionary<string, List<(int Id, string? Codigo, string Nombre, string? TipoHuevo)>>();
        foreach (var i in catalogo)
        {
            Indexar(i.Nombre, i);
            Indexar(i.Codigo, i);
        }

        if (catalogo.Count == 0)
        {
            errores.Add(new(0, "Ítem", null,
                "La empresa no tiene ítems de huevo en el catálogo (item_type = 'huevo'); no hay contra qué validar la hoja 'Huevos'."));
            return porFecha;
        }

        // (fecha, ítem) repetido: el usuario partió el mismo ítem en dos filas del mismo día. Se suma,
        // pero se avisa: casi siempre es una fila duplicada por error de copiado.
        var vistos = new HashSet<(DateTime, int)>();

        foreach (var fila in filas)
        {
            int e0 = errores.Count;

            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Huevos: fecha inválida o faltante.")); continue; }

            var itemTxt = MigracionCalculos.TextoLimpio(Celda(fila, ClavesHuevos("Ítem")));
            if (itemTxt is null)
            { errores.Add(new(fila.Numero, "Ítem", null, "Huevos: indicá el ítem (nombre o código de la hoja Referencias).")); continue; }

            if (!porClave.TryGetValue(MigracionCalculos.NormalizarClave(itemTxt), out var matches) || matches.Count == 0)
            { errores.Add(new(fila.Numero, "Ítem", itemTxt, $"Huevos: el ítem '{itemTxt}' no existe en el catálogo de huevo de la empresa. Usá el nombre o código de la hoja Referencias.")); continue; }
            if (matches.Count > 1)
            { errores.Add(new(fila.Numero, "Ítem", itemTxt, $"Huevos: '{itemTxt}' coincide con {matches.Count} ítems distintos; usá el código.")); continue; }

            var cantidad = EnteroNoNegNull(fila, errores, "Cantidad", ClavesHuevos("Cantidad"));
            if (errores.Count > e0) continue;
            if (cantidad is null or <= 0)
            { errores.Add(new(fila.Numero, "Cantidad", cantidad?.ToString(), "Huevos: la cantidad es obligatoria y debe ser mayor que cero.")); continue; }

            var item = matches[0];
            if (!vistos.Add((fecha.Date, item.Id)))
                errores.Add(new(fila.Numero, "Ítem", item.Nombre,
                    $"Huevos: '{item.Nombre}' aparece más de una vez el {fecha:yyyy-MM-dd}; las cantidades se suman.", "Advertencia"));

            if (!porFecha.TryGetValue(fecha.Date, out var lista)) porFecha[fecha.Date] = lista = new List<HuevoItemSeguimientoDto>();
            var existente = lista.FindIndex(x => x.CatalogItemId == item.Id);
            if (existente >= 0)
                lista[existente] = lista[existente] with { Cantidad = lista[existente].Cantidad + cantidad.Value };
            else
                lista.Add(new HuevoItemSeguimientoDto(item.Id, item.Codigo, item.Nombre, item.TipoHuevo, cantidad.Value, null));
        }

        // Última red: el mismo validador puro que usa el alta manual.
        foreach (var (fecha, lista) in porFecha)
            if (HuevoItemsCalculos.Validar(lista) is string error)
                errores.Add(new(0, "Ítem", fecha.ToString("yyyy-MM-dd"), $"Huevos ({fecha:yyyy-MM-dd}): {error}"));

        return porFecha;

        void Indexar(string? clave, (int Id, string? Codigo, string Nombre, string? TipoHuevo) item)
        {
            var k = MigracionCalculos.NormalizarClave(clave);
            if (string.IsNullOrEmpty(k)) return;
            if (!porClave.TryGetValue(k, out var lista)) porClave[k] = lista = new List<(int, string?, string, string?)>();
            if (!lista.Any(x => x.Id == item.Id)) lista.Add(item);
        }
    }

    /// <summary>Claves de lectura (título + alias) de una columna de la hoja "Huevos".</summary>
    private static string[] ClavesHuevos(string titulo) =>
        MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.HuevosPostura, titulo);
}

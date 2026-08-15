// src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using LoteDtos = ZooSanMarino.Application.DTOs.Lotes;
using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ProduccionService : IProduccionService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILoteService _loteService;
    private readonly IEspejoHuevoProduccionSyncService _espejoHuevoSync;
    private readonly ILocationScopeResolver _scopeResolver;
    private readonly IFarmInventoryConsumoService? _farmInventoryConsumo;      // Fase 2: modelo A (Colombia) — sin uso tras Fase 3 paso 2
    private readonly IColombiaInventarioConsumoService? _colombiaConsumoB;     // Fase 3 paso 2: modelo B nivel granja (Colombia)
    /// <summary>Doble validación: separa en vez de descontar cuando la empresa la tiene activa.</summary>
    private readonly IValidacionSeguimientoService? _validacion;

    /// <summary>
    /// Fase 3 (paso 2): producción postura Colombia descuenta inventario en el MODELO B unificado a
    /// NIVEL GRANJA (antes Fase 2 usaba modelo A). Ecuador/Panamá no operan producción postura por
    /// esta ruta, por eso no hay flujo modelo B con galpón aquí. El descuento resuelve GranjaId del
    /// lote y descuenta desde los DTOs ItemsHembras/ItemsMachos (no re-parsea JSON), con validación
    /// previa de stock y transacción única (todo-o-nada) idéntica a levante.
    /// </summary>
    public ProduccionService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        ILoteService loteService,
        IEspejoHuevoProduccionSyncService espejoHuevoSync,
        ILocationScopeResolver scopeResolver,
        IFarmInventoryConsumoService? farmInventoryConsumo = null,
        IColombiaInventarioConsumoService? colombiaConsumoB = null,
        IValidacionSeguimientoService? validacion = null)
    {
        _context = context;
        _currentUser = currentUser;
        _loteService = loteService;
        _espejoHuevoSync = espejoHuevoSync;
        _scopeResolver = scopeResolver;
        _farmInventoryConsumo = farmInventoryConsumo;
        _colombiaConsumoB = colombiaConsumoB;
        _validacion = validacion;
    }

    /// <summary>
    /// Resuelve (GranjaId, ModeloInventarioConsumo) del lote de producción para gatear el descuento.
    /// País: lote.PaisId si está poblado; si no, granja→departamento→pais (misma cadena que el inventario).
    /// </summary>
    private async Task<(int? GranjaId, ModeloInventarioConsumo Modelo)> ResolverGranjaYModeloAsync(int loteId)
    {
        var lote = await _context.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.PaisId })
            .FirstOrDefaultAsync();
        if (lote == null) return (null, ModeloInventarioConsumo.Ninguno);

        int? paisId = lote.PaisId;
        if (paisId is not > 0)
            paisId = await _context.Farms.AsNoTracking()
                .Where(f => f.Id == lote.GranjaId)
                .Join(_context.Departamentos.AsNoTracking(),
                    f => f.DepartamentoId, d => d.DepartamentoId, (f, d) => (int?)d.PaisId)
                .FirstOrDefaultAsync();

        return (lote.GranjaId, InventarioConsumoGate.ResolverModelo(paisId));
    }

    /// <summary>
    /// Acumula por CLAVE TIPADA de ítem (conserva el origen del id: item_inventario_ecuador si viene
    /// camino-2, si no catalogItemId) los kg de los ítems del request (ItemsHembras + ItemsMachos),
    /// usando la MISMA prioridad de id y conversión g→kg que ParseMetadataItemsToKgPorOrigen.
    /// TODOS los tipos (alimento + medicamento + insumo), sin re-parsear el JSON del metadata. Id &lt;= 0 se ignora.
    /// </summary>
    private static Dictionary<ItemConsumoKey, decimal> AcumularItemsRequestPorOrigen(
        List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var byItem = new Dictionary<ItemConsumoKey, decimal>();
        void Acumular(List<ItemSeguimientoDto>? items)
        {
            if (items == null) return;
            foreach (var i in items)
            {
                var id = i.ItemInventarioEcuadorId.GetValueOrDefault();
                var esItemInventario = id > 0;
                if (id <= 0) id = i.CatalogItemId;
                if (id <= 0) continue;
                // El silo entra en la clave igual que en ParseMetadataItemsToKgPorOrigen: sin él, dos
                // filas del mismo alimento en silos distintos se sumarían y descontarían del primero.
                var key = new ItemConsumoKey(id, esItemInventario, i.SiloId is > 0 ? i.SiloId : null);
                byItem[key] = byItem.GetValueOrDefault(key) + ToKg(i.Cantidad, i.Unidad);
            }
        }
        Acumular(itemsHembras);
        Acumular(itemsMachos);
        return byItem;
    }

    /// <summary>
    /// Bloquea crear, editar y eliminar seguimiento diario cuando el lote de producción está
    /// cerrado. Mismo criterio que <c>SeguimientoProduccionService.EnsureLoteProduccionAbiertoAsync</c>
    /// (REQ-006), que cubría el OTRO controlador: este servicio —el que atiende
    /// <c>/api/Produccion/seguimiento</c>, el que usa el módulo— no validaba nada, así que un lote
    /// cerrado se podía seguir tocando.
    /// <para>
    /// Se resuelve por el LPP cuando viene informado y, si no, por el lote base. NO se valida el
    /// lote de LEVANTE: queda siempre "Cerrado" al pasar a producción, y mirarlo bloquearía toda la
    /// captura. Sin LPP asociado (flujo legacy) no hay estado de cierre que validar.
    /// </para>
    /// </summary>
    private async Task EnsureLoteProduccionAbiertoAsync(int loteId, int? lotePosturaProduccionId)
    {
        var q = _context.LotePosturaProduccion.AsNoTracking().Where(l => l.DeletedAt == null);

        q = lotePosturaProduccionId.HasValue
            ? q.Where(l => l.LotePosturaProduccionId == lotePosturaProduccionId.Value)
            : q.Where(l => l.LoteId == loteId);

        var estado = await q.Select(l => l.EstadoCierre).FirstOrDefaultAsync().ConfigureAwait(false);

        if (CicloVidaPosturaCalculos.EstaCerrado(estado))
            throw new InvalidOperationException(
                "El lote de producción está cerrado; no se pueden crear, modificar ni eliminar registros de seguimiento diario. " +
                "Reabra el lote desde Seguimiento Diario de Producción si necesita ajustarlo.");
    }

    /// <summary>
    /// Corte de etapa (contraparte de <c>SeguimientoLoteLevanteService.EnsureDiaSinAporteDeProduccionAsync</c>):
    /// bloquea el alta de un día de PRODUCCIÓN cuando levante ya registró ese mismo día del mismo lote
    /// CON consumo o bajas. Solo se mira el aporte, no la existencia de la fila: el arrastre de huevos
    /// del levante crea filas legítimas de solo huevos y esas no chocan. Lo que se impide es el doble
    /// conteo del caso K345 — 14 días de julio-2025 en las dos tablas con el mismo consumo.
    /// </summary>
    private async Task EnsureDiaSinAporteDeLevanteAsync(int loteId, CrearSeguimientoRequest request)
    {
        var (desde, hasta) = FechasPuras.RangoDiaUtc(request.FechaRegistro);
        var loteTexto = loteId.ToString();

        var otra = await _context.SeguimientoDiario.AsNoTracking()
            .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteTexto
                     && s.Fecha >= desde && s.Fecha < hasta)
            .Select(s => new
            {
                Consumo = (s.ConsumoKgHembras ?? 0m) + (s.ConsumoKgMachos ?? 0m),
                Mortalidad = (s.MortalidadHembras ?? 0) + (s.MortalidadMachos ?? 0),
                Seleccion = (s.SelH ?? 0) + (s.SelM ?? 0)
            })
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (otra is null) return;

        // El consumo se toma en crudo (sin convertir la unidad): al guard solo le importa si el día
        // aporta alimento, no cuánto.
        var nuevo = new CorteEtapaPosturaCalculos.AporteDia(
            (decimal)(request.ConsumoH ?? 0) + (decimal)(request.ConsumoM ?? 0),
            request.MortalidadH + request.MortalidadM,
            request.SelH + request.SelM);

        var existente = new CorteEtapaPosturaCalculos.AporteDia(otra.Consumo, otra.Mortalidad, otra.Seleccion);

        if (CorteEtapaPosturaCalculos.HayDobleConteo(nuevo, existente))
            throw new InvalidOperationException(
                CorteEtapaPosturaCalculos.MensajeProduccionChocaConLevante(request.FechaRegistro));
    }


    /// <summary>
    /// Construye el objeto Metadata JSONB con los campos adicionales.
    /// </summary>
    private static System.Text.Json.JsonDocument? BuildMetadata(
        double? consumoHembras, string? unidadHembras, 
        double? consumoMachos, string? unidadMachos,
        string? tipoItemHembras, string? tipoItemMachos,
        int? tipoAlimentoHembras, int? tipoAlimentoMachos)
    {
        var metadata = new Dictionary<string, object?>();
        
        // Consumo original con unidad
        if (consumoHembras.HasValue)
        {
            metadata["consumoOriginalHembras"] = consumoHembras.Value;
            metadata["unidadConsumoOriginalHembras"] = unidadHembras ?? "kg";
        }
        
        if (consumoMachos.HasValue)
        {
            metadata["consumoOriginalMachos"] = consumoMachos.Value;
            metadata["unidadConsumoOriginalMachos"] = unidadMachos ?? "kg";
        }
        
        // Tipo de ítem (alimento, medicamento, etc.)
        if (!string.IsNullOrWhiteSpace(tipoItemHembras))
        {
            metadata["tipoItemHembras"] = tipoItemHembras;
        }
        
        if (!string.IsNullOrWhiteSpace(tipoItemMachos))
        {
            metadata["tipoItemMachos"] = tipoItemMachos;
        }
        
        // IDs de alimentos seleccionados
        if (tipoAlimentoHembras.HasValue)
        {
            metadata["tipoAlimentoHembras"] = tipoAlimentoHembras.Value;
        }
        
        if (tipoAlimentoMachos.HasValue)
        {
            metadata["tipoAlimentoMachos"] = tipoAlimentoMachos.Value;
        }
        
        if (metadata.Count == 0) return null;
        
        return JsonDocument.Parse(JsonSerializer.Serialize(metadata));
    }

    private static (List<ItemSeguimientoDto> alimentos, List<ItemSeguimientoDto> otrosItems) SepararAlimentosYOtrosItems(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0)
            return (new List<ItemSeguimientoDto>(), new List<ItemSeguimientoDto>());
        var alimentos = new List<ItemSeguimientoDto>();
        var otros = new List<ItemSeguimientoDto>();
        foreach (var item in items)
        {
            if (string.Equals(item.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase))
                alimentos.Add(item);
            else
                otros.Add(item);
        }
        return (alimentos, otros);
    }

    private static double CalcularConsumoTotalAlimentos(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0) return 0;
        double total = 0;
        foreach (var item in items)
        {
            if (!string.Equals(item.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase)) continue;
            var unidad = item.Unidad?.ToLower().Trim() ?? "kg";
            var cantidadKg = item.Cantidad;
            if (unidad == "g" || unidad == "gramos" || unidad == "gramo")
                cantidadKg = item.Cantidad / 1000.0;
            total += cantidadKg;
        }
        return total;
    }

    private static JsonDocument? BuildItemsAdicionales(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var dict = new Dictionary<string, object?>();
        if (itemsHembras != null && itemsHembras.Count > 0)
            dict["itemsHembras"] = itemsHembras.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (itemsMachos != null && itemsMachos.Count > 0)
            dict["itemsMachos"] = itemsMachos.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (dict.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    private static string ConstruirTipoAlimentoString(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var parts = new List<string>();
        if (itemsHembras != null)
            foreach (var i in itemsHembras)
                if (string.Equals(i.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase))
                    parts.Add($"H:{i.CatalogItemId}");
        if (itemsMachos != null)
            foreach (var i in itemsMachos)
                if (string.Equals(i.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase))
                    parts.Add($"M:{i.CatalogItemId}");
        return parts.Count > 0 ? string.Join(" / ", parts) : string.Empty;
    }

    /// <summary>
    /// Un ítem tal como se guarda en el metadata jsonb de producción (mismas claves de siempre, en el
    /// mismo orden). <c>siloId</c> se agrega SOLO cuando viene: el metadata es la fuente del diff al
    /// editar, así que sin esto una edición devolvería el alimento a «sin silo» en vez de al silo del
    /// que salió. Con el flag apagado el JSON es el de antes, clave por clave.
    /// </summary>
    private static Dictionary<string, object?> ItemAMetadata(ItemSeguimientoDto i)
    {
        var item = new Dictionary<string, object?>
        {
            ["tipoItem"] = i.TipoItem,
            ["catalogItemId"] = i.CatalogItemId,
            ["itemInventarioEcuadorId"] = i.ItemInventarioEcuadorId,
            ["cantidad"] = i.Cantidad,
            ["unidad"] = i.Unidad
        };
        if (i.SiloId is > 0) item["siloId"] = i.SiloId.Value;
        return item;
    }

    private static JsonDocument? BuildMetadataFromItems(
        List<ItemSeguimientoDto>? itemsHembras,
        List<ItemSeguimientoDto>? itemsMachos,
        double? consumoH, string? unidadH, double? consumoM, string? unidadM,
        string? tipoItemHembras, string? tipoItemMachos,
        int? tipoAlimentoHembras, int? tipoAlimentoMachos)
    {
        var metadata = new Dictionary<string, object?>();
        if (itemsHembras != null && itemsHembras.Count > 0)
            metadata["itemsHembras"] = itemsHembras.Select(ItemAMetadata).ToList();
        if (itemsMachos != null && itemsMachos.Count > 0)
            metadata["itemsMachos"] = itemsMachos.Select(ItemAMetadata).ToList();
        if ((itemsHembras == null || itemsHembras.Count == 0) && consumoH.HasValue) { metadata["consumoOriginalHembras"] = consumoH.Value; metadata["unidadConsumoOriginalHembras"] = unidadH ?? "kg"; }
        if ((itemsMachos == null || itemsMachos.Count == 0) && consumoM.HasValue) { metadata["consumoOriginalMachos"] = consumoM.Value; metadata["unidadConsumoOriginalMachos"] = unidadM ?? "kg"; }
        if (!string.IsNullOrWhiteSpace(tipoItemHembras)) metadata["tipoItemHembras"] = tipoItemHembras;
        if (!string.IsNullOrWhiteSpace(tipoItemMachos)) metadata["tipoItemMachos"] = tipoItemMachos;
        if (tipoAlimentoHembras.HasValue) metadata["tipoAlimentoHembras"] = tipoAlimentoHembras.Value;
        if (tipoAlimentoMachos.HasValue) metadata["tipoAlimentoMachos"] = tipoAlimentoMachos.Value;
        if (metadata.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(metadata));
    }

    private static decimal ToKg(double cantidad, string unidad)
    {
        var u = (unidad ?? "kg").Trim().ToLowerInvariant();
        if (u == "g" || u == "gramos" || u == "gramo") return (decimal)(cantidad / 1000.0);
        return (decimal)cantidad;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Clasificación de huevos POR ÍTEMS del catálogo (Primera/Pnc) — Santa Reyes.
    // Gateada por companies.clasificacion_huevo_por_items de la empresa DUEÑA DE LA GRANJA del
    // lote (misma empresa efectiva que resuelve el inventario, patrón farms.company_id). Cero
    // impacto para el resto de empresas: sin huevoItems en el request no se ejecuta nada de esto.
    // ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Valida el desglose de huevos por ítems del request:
    /// (a) reglas puras (cantidad ≥ 0, id &gt; 0, sin repetidos) — <see cref="HuevoItemsCalculos.Validar"/>;
    /// (b) la empresa de la granja del lote debe tener <c>clasificacion_huevo_por_items = true</c>;
    /// (c) todos los <c>catalogItemId</c> deben existir en <c>catalogo_items</c> de esa empresa con
    ///     <c>item_type = 'huevo'</c> (una sola query, comparación de conjuntos).
    /// Lanza <see cref="InvalidOperationException"/> (el controller la traduce a 400) con el detalle.
    /// </summary>
    private async Task<List<HuevoItemSeguimientoDto>> ValidarHuevoItemsAsync(int loteId, List<HuevoItemSeguimientoDto> huevoItems)
    {
        var error = HuevoItemsCalculos.Validar(huevoItems);
        if (error != null) throw new InvalidOperationException(error);

        var companyId = await ResolverCompanyIdDeGranjaDelLoteAsync(loteId).ConfigureAwait(false);

        var permite = await _context.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => (bool?)c.ClasificacionHuevoPorItems)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (permite != true)
            throw new InvalidOperationException(
                "La empresa de este lote no tiene habilitada la clasificación de huevos por ítems de inventario; use los campos de clasificación estándar.");

        var ids = huevoItems.Select(i => i.CatalogItemId).Distinct().ToArray();
        var existentes = await _context.CatalogItems.AsNoTracking()
            .Where(ci => ci.CompanyId == companyId && ci.ItemType == "huevo" && ids.Contains(ci.Id))
            .Select(ci => ci.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var faltantes = ids.Except(existentes).ToArray();
        if (faltantes.Length > 0)
            throw new InvalidOperationException(
                $"Los siguientes ítems no existen como ítem de huevo del catálogo de la empresa: {string.Join(", ", faltantes)}.");

        return huevoItems;
    }

    /// <summary>
    /// Empresa efectiva de la clasificación = empresa dueña de la GRANJA del lote (misma regla que
    /// el descuento de inventario, <c>farms.company_id</c>), no la empresa activa del token.
    /// </summary>
    private async Task<int> ResolverCompanyIdDeGranjaDelLoteAsync(int loteId)
    {
        var granjaId = await _context.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.DeletedAt == null)
            .Select(l => (int?)l.GranjaId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (granjaId is null or <= 0)
            throw new InvalidOperationException($"No se pudo resolver la granja del lote {loteId} para clasificar los huevos por ítems.");

        var companyId = await _context.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId.Value)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (companyId is null or <= 0)
            throw new InvalidOperationException($"No se pudo resolver la empresa de la granja {granjaId} para clasificar los huevos por ítems.");

        return companyId.Value;
    }

    /// <summary>
    /// Devuelve el <c>LoteId</c> efectivo del lote de producción y SANA la fila si estaba rota.
    /// Los LPP nacen al cerrar un levante; antes del fix de herencia ese cierre no copiaba
    /// <c>LoteId</c>/<c>LotePadreId</c>, dejando filas sin lote base (guardado imposible:
    /// <c>seguimiento_diario_produccion.lote_id</c> es NOT NULL). Si el LPP no tiene lote válido,
    /// se resuelve desde su levante de origen (SIN filtrar <c>DeletedAt</c>: la referencia de un
    /// levante soft-deleted sigue siendo válida) y se persiste la reparación en la fila LPP
    /// (self-heal, <c>ExecuteUpdate</c>). Si no es resoluble, lanza un error claro.
    /// </summary>
    private async Task<int> ResolverYSanarLoteIdAsync(LotePosturaProduccion lpp)
    {
        // Camino existente: el LPP ya tiene lote base → comportamiento idéntico, sin tocar nada.
        if (lpp.LoteId is > 0) return lpp.LoteId.Value;

        int? levLoteId = null;
        int? levPadre = null;
        if (lpp.LotePosturaLevanteId.HasValue)
        {
            // Fail-closed: solo se hereda de un levante de la MISMA empresa; una referencia
            // cruzada (solo posible por datos corruptos) cae al error claro de abajo.
            var lev = await _context.LotePosturaLevante.AsNoTracking()
                .Where(l => l.LotePosturaLevanteId == lpp.LotePosturaLevanteId.Value
                    && l.CompanyId == lpp.CompanyId)
                .Select(l => new { l.LoteId, l.LotePadreId })
                .FirstOrDefaultAsync();
            levLoteId = lev?.LoteId;
            levPadre = lev?.LotePadreId;
        }

        var resuelto = SeguimientoProduccionLoteIdCalculos.ResolverLoteIdEfectivo(lpp.LoteId, levLoteId);
        if (resuelto is not > 0)
            throw new InvalidOperationException(
                $"El lote de producción '{lpp.LoteNombre}' no tiene lote base asociado y su levante de origen tampoco lo tiene. " +
                $"No es posible registrar el seguimiento; repare el lote (lote_postura_produccion #{lpp.LotePosturaProduccionId}).");

        // Self-heal persistente: repara la fila LPP (lote_id y, solo si estaba null, lote_padre_id)
        // para que indicadores, espejo huevo y reportes también la vean sana desde ahora.
        await _context.LotePosturaProduccion
            .Where(l => l.LotePosturaProduccionId == lpp.LotePosturaProduccionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LoteId, resuelto)
                .SetProperty(x => x.LotePadreId, x => x.LotePadreId ?? levPadre)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                .SetProperty(x => x.UpdatedByUserId, _currentUser.UserId));

        return resuelto.Value;
    }

    /// <summary>
    /// Totales del día cuando la clasificación es por ítems: <c>huevo_tot</c> = suma de cantidades
    /// (lo que siguen leyendo espejo, trigger, saldos, indicadores y reportes), <c>huevo_inc</c> = 0
    /// (postura comercial, no incuba) y las 11 columnas fijas en 0 (el desglose vive en el metadata).
    /// </summary>
    private static void AplicarTotalesHuevoPorItems(SeguimientoProduccion entity, IReadOnlyCollection<HuevoItemSeguimientoDto> huevoItems)
    {
        entity.HuevoTot = HuevoItemsCalculos.SumarTotal(huevoItems);
        entity.HuevoInc = 0;
        entity.HuevoLimpio = 0;
        entity.HuevoTratado = 0;
        entity.HuevoSucio = 0;
        entity.HuevoDeforme = 0;
        entity.HuevoBlanco = 0;
        entity.HuevoDobleYema = 0;
        entity.HuevoPiso = 0;
        entity.HuevoPequeno = 0;
        entity.HuevoRoto = 0;
        entity.HuevoDesecho = 0;
        entity.HuevoOtro = 0;
    }
}

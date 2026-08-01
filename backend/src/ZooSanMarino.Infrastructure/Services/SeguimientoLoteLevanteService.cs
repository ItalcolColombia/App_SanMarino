// Partial 'ancla' del Seguimiento Diario Levante: usings, campos, ctor, constantes, helpers
// compartidos (resolución de país, aves vivas/ajuste en lote_postura_levante, derivados nutricionales,
// semana, parseo de metadata) y la interfaz. La implementación vive repartida por responsabilidad en
// 'SeguimientoLoteLevante/Funciones/' (Consultas, Crud, Mapeos). Namespace plano → misma DI, misma
// interfaz, mismo comportamiento.
//
// Seguimiento Diario Levante: persiste en la tabla unificada seguimiento_diario (tipo = 'levante')
// usando ISeguimientoDiarioService. La API y DTOs del módulo Levante se mantienen igual.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoLoteLevanteService : ISeguimientoLoteLevanteService
{
    private const string TipoLevante = "levante";

    /// <summary>Serialización camelCase para metadata sintético (registros viejos sin JSON en BD).</summary>
    private static readonly JsonSerializerOptions SyntheticMetadataJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ZooSanMarinoContext _ctx;
    private readonly ISeguimientoDiarioService _seguimientoDiarioService;
    private readonly IAlimentoNutricionProvider _alimentos;
    private readonly IGramajeProvider _gramaje;
    private readonly ICurrentUser _current;
    private readonly IMovimientoAvesService _movimientoAvesService;
    private readonly ILocationScopeResolver _scopeResolver;
    private readonly IInventarioGestionService? _inventarioGestionService;
    private readonly IFarmInventoryConsumoService? _farmInventoryConsumo;   // Fase 2: modelo A (Colombia) — sin uso tras Fase 3 paso 2
    private readonly IColombiaInventarioConsumoService? _colombiaConsumoB;  // Fase 3 paso 2: modelo B nivel granja (Colombia)
    private readonly ILogger<SeguimientoLoteLevanteService>? _logger;

    public SeguimientoLoteLevanteService(
        ZooSanMarinoContext ctx,
        ISeguimientoDiarioService seguimientoDiarioService,
        IAlimentoNutricionProvider alimentos,
        IGramajeProvider gramaje,
        ICurrentUser current,
        IMovimientoAvesService movimientoAvesService,
        ILocationScopeResolver scopeResolver,
        IInventarioGestionService? inventarioGestionService = null,
        IFarmInventoryConsumoService? farmInventoryConsumo = null,
        IColombiaInventarioConsumoService? colombiaConsumoB = null,
        ILogger<SeguimientoLoteLevanteService>? logger = null)
    {
        _ctx = ctx;
        _seguimientoDiarioService = seguimientoDiarioService;
        _alimentos = alimentos;
        _gramaje = gramaje;
        _current = current;
        _movimientoAvesService = movimientoAvesService;
        _scopeResolver = scopeResolver;
        _inventarioGestionService = inventarioGestionService;
        _farmInventoryConsumo = farmInventoryConsumo;
        _colombiaConsumoB = colombiaConsumoB;
        _logger = logger;
    }

    /// <summary>
    /// ¿La empresa del lote captura la clasificación de huevos en LEVANTE
    /// (<c>companies.captura_huevos_en_levante</c>)?
    /// <para>
    /// Empresa efectiva <b>por datos</b>: <c>farms.company_id</c> de la granja del lote (no
    /// <c>_current.CompanyId</c>), el patrón obligatorio del repo para features por empresa.
    /// <b>Fail-closed</b>: si la granja/empresa no se resuelve devuelve <c>false</c>.
    /// </para>
    /// <para>
    /// También devuelve <c>false</c> cuando la empresa clasifica los huevos POR ÍTEMS del catálogo
    /// (<c>clasificacion_huevo_por_items</c>): ese modo no está soportado todavía en levante y es
    /// preferible no capturar nada que persistir un desglose que los reportes no sabrían leer.
    /// </para>
    /// </summary>
    private async Task<bool> EmpresaCapturaHuevosEnLevanteAsync(int granjaId, CancellationToken ct = default)
    {
        var flags = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId)
            .Join(_ctx.Companies.AsNoTracking(),
                  f => f.CompanyId,
                  c => c.Id,
                  (f, c) => new { c.CapturaHuevosEnLevante, c.ClasificacionHuevoPorItems })
            .FirstOrDefaultAsync(ct);

        if (flags is null) return false;                    // granja o empresa no resoluble
        if (flags.ClasificacionHuevoPorItems) return false;  // modo por ítems: fuera de alcance
        return flags.CapturaHuevosEnLevante;
    }

    /// <summary>
    /// Aplica el gate de captura de huevos en levante sobre el DTO entrante y devuelve el DTO ya
    /// saneado:
    /// <list type="bullet">
    ///   <item>empresa sin el flag (o modo por ítems) ⇒ los huevos se <b>neutralizan a null</b>
    ///   (comportamiento previo byte a byte, sin error: un cliente viejo que mande el campo no
    ///   empieza a fallar);</item>
    ///   <item>empresa con el flag y alguna categoría <b>positiva</b> con fecha de registro
    ///   anterior al encaset ⇒ <b>error explícito</b> (el dato no se puede ubicar en la vida del
    ///   lote; el tab es fijo y ya no hay gate de semana);</item>
    ///   <item>todo en cero ⇒ pasa siempre (un seguimiento normal de semana 3 manda ceros).</item>
    /// </list>
    /// </summary>
    private async Task<SeguimientoLoteLevanteDto> AplicarGateHuevosLevanteAsync(
        SeguimientoLoteLevanteDto dto, Lote lote, CancellationToken ct = default)
    {
        var huevos = HuevosDeDto(dto);
        if (huevos is null) return dto;                      // el cliente no mandó el tab de huevos

        if (!await EmpresaCapturaHuevosEnLevanteAsync(lote.GranjaId, ct))
            return SinHuevos(dto);

        if (huevos.Value.AlgunoPositivo && !HuevosLevanteCalculos.PermiteHuevos(lote.FechaEncaset, dto.FechaRegistro))
            throw new InvalidOperationException(
                "Los huevos no pueden registrarse con una fecha anterior al encasetamiento del lote.");

        return dto;
    }

    /// <summary>Devuelve el DTO con las 11 categorías y el peso del huevo en null (sin tocar el resto).</summary>
    private static SeguimientoLoteLevanteDto SinHuevos(SeguimientoLoteLevanteDto dto) => dto with
    {
        HuevoLimpio = null,
        HuevoTratado = null,
        HuevoSucio = null,
        HuevoDeforme = null,
        HuevoBlanco = null,
        HuevoDobleYema = null,
        HuevoPiso = null,
        HuevoPequeno = null,
        HuevoRoto = null,
        HuevoDesecho = null,
        HuevoOtro = null,
        PesoHuevo = null,
        HuevoTot = null,
        HuevoInc = null
    };

    /// <summary>
    /// País efectivo del lote para gatear el descuento del inventario modelo B.
    /// Fuente robusta: <c>lote.PaisId</c> si está poblado; si no, derivado desde la granja
    /// (farm.DepartamentoId → departamentos.PaisId), la misma cadena que usa el inventario
    /// (InventarioGestionService.GetEffectivePaisIdAsync). Devuelve null si no se puede resolver.
    /// </summary>
    private async Task<int?> ResolverPaisIdLoteAsync(int granjaId, int? paisIdLote)
    {
        if (paisIdLote is > 0) return paisIdLote;
        var paisId = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId)
            .Join(_ctx.Departamentos.AsNoTracking(),
                f => f.DepartamentoId, d => d.DepartamentoId, (f, d) => (int?)d.PaisId)
            .FirstOrDefaultAsync();
        return paisId;
    }

    /// <summary>
    /// Suma mortalidad/selección/error desde la tabla unificada seguimiento_diario (tipo levante).
    /// Base de hembras desde lote_etapa_levante (historial) si existe; si no, desde lote.
    /// REQ-011b fix: ahora también suma traslados de ingreso/salida de hembras (splits H/M por fila
    /// en seguimiento_diario) — antes NO los sumaba, a diferencia de
    /// LoteService.GetMortalidadResumenAsync (LoteService.cs:839), y el auto-consumo por gramaje
    /// calculaba 0 hembras vivas en lotes poblados únicamente por traslado (ej. lotes 116/117
    /// "A374A/B", sin HembrasL propio).
    /// </summary>
    private async Task<int> CalcularHembrasVivasAsync(int loteId)
    {
        var loteIdStr = loteId.ToString();
        int baseH;
        int mortCajaH;
        var etapa = await _ctx.LoteEtapaLevante.AsNoTracking()
            .FirstOrDefaultAsync(el => el.LoteId == loteId);
        if (etapa != null)
        {
            baseH = etapa.AvesInicioHembras;
            var lote = await _ctx.Lotes.AsNoTracking()
                .Where(l => l.LoteId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null)
                .Select(l => new { MortCaja = l.MortCajaH ?? 0 })
                .SingleOrDefaultAsync();
            mortCajaH = lote?.MortCaja ?? 0;
        }
        else
        {
            var loteData = await _ctx.Lotes.AsNoTracking()
                .Where(l => l.LoteId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null)
                .Select(l => new { Base = l.HembrasL ?? 0, MortCaja = l.MortCajaH ?? 0 })
                .SingleAsync();
            baseH = loteData.Base;
            mortCajaH = loteData.MortCaja;
        }

        var sum = await _ctx.SeguimientoDiario.AsNoTracking()
            .Where(x => x.TipoSeguimiento == TipoLevante && x.LoteId == loteIdStr)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                MortH = g.Sum(x => x.MortalidadHembras ?? 0),
                SelH = g.Sum(x => x.SelH ?? 0),
                ErrH = g.Sum(x => x.ErrorSexajeHembras ?? 0),
                InH = g.Sum(x => x.TrasladoIngresoHembras),
                OutH = g.Sum(x => x.TrasladoSalidaHembras)
            })
            .SingleOrDefaultAsync();

        int mort = sum?.MortH ?? 0, sel = sum?.SelH ?? 0, err = sum?.ErrH ?? 0;
        int trasladoInH = sum?.InH ?? 0, trasladoOutH = sum?.OutH ?? 0;
        var vivas = baseH - mortCajaH - mort - sel - err + trasladoInH - trasladoOutH;
        return Math.Max(0, vivas);
    }

    /// <summary>
    /// REQ-011b (soft-check, NO oficial): saldo por sexo del lote a una fecha de corte, replicando la
    /// aritmética de LoteService.GetMortalidadResumenAsync (LoteService.cs:839: base - mortCaja -
    /// mort - sel - err + trasladoIngreso - trasladoSalida) pero acotada a fecha &lt;= fechaCorte,
    /// usando los splits H/M por fila de seguimiento_diario (fechados) — más preciso para un corte
    /// temporal que la suma corrida sin fecha de lote_postura_levante. Solo se usa para advertir
    /// (Crud.cs); NO reemplaza el cálculo oficial de saldo del lote. excludeRegistroId permite excluir
    /// el propio registro en edición, para no auto-justificar su propio consumo/mortalidad.
    /// </summary>
    private async Task<(int saldoH, int saldoM)> CalcularSaldoPorSexoAFechaAsync(int loteId, DateTime fechaCorte, long? excludeRegistroId = null)
    {
        var loteData = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null)
            .Select(l => new { HembrasL = l.HembrasL ?? 0, MachosL = l.MachosL ?? 0, MortCajaH = l.MortCajaH ?? 0, MortCajaM = l.MortCajaM ?? 0 })
            .SingleOrDefaultAsync();
        if (loteData is null) return (0, 0);

        int baseH = loteData.HembrasL, baseM = loteData.MachosL;
        var etapa = await _ctx.LoteEtapaLevante.AsNoTracking().FirstOrDefaultAsync(el => el.LoteId == loteId);
        if (etapa != null) { baseH = etapa.AvesInicioHembras; baseM = etapa.AvesInicioMachos; }

        var loteIdStr = loteId.ToString();
        var q = _ctx.SeguimientoDiario.AsNoTracking()
            .Where(x => x.TipoSeguimiento == TipoLevante && x.LoteId == loteIdStr && x.Fecha.Date <= fechaCorte.Date);
        if (excludeRegistroId.HasValue)
            q = q.Where(x => x.Id != excludeRegistroId.Value);

        var sum = await q.GroupBy(_ => 1).Select(g => new
        {
            MortH = g.Sum(x => x.MortalidadHembras ?? 0),
            MortM = g.Sum(x => x.MortalidadMachos ?? 0),
            SelH = g.Sum(x => x.SelH ?? 0),
            SelM = g.Sum(x => x.SelM ?? 0),
            ErrH = g.Sum(x => x.ErrorSexajeHembras ?? 0),
            ErrM = g.Sum(x => x.ErrorSexajeMachos ?? 0),
            InH = g.Sum(x => x.TrasladoIngresoHembras),
            InM = g.Sum(x => x.TrasladoIngresoMachos),
            OutH = g.Sum(x => x.TrasladoSalidaHembras),
            OutM = g.Sum(x => x.TrasladoSalidaMachos)
        }).SingleOrDefaultAsync();

        int saldoH = Math.Max(0, baseH - loteData.MortCajaH - (sum?.MortH ?? 0) - (sum?.SelH ?? 0) - (sum?.ErrH ?? 0) + (sum?.InH ?? 0) - (sum?.OutH ?? 0));
        int saldoM = Math.Max(0, baseM - loteData.MortCajaM - (sum?.MortM ?? 0) - (sum?.SelM ?? 0) - (sum?.ErrM ?? 0) + (sum?.InM ?? 0) - (sum?.OutM ?? 0));
        return (saldoH, saldoM);
    }

    private static (double? kcalAveH, double? protAveH) CalcularDerivados(double consumoKgHembras, double? kcalAlH, double? protAlH)
    {
        double? kcal = kcalAlH is null ? null : Math.Round(consumoKgHembras * kcalAlH.Value, 3);
        double? prot = protAlH is null ? null : Math.Round(consumoKgHembras * protAlH.Value, 3);
        return (kcal, prot);
    }

    private static int CalcularSemana(DateTime fechaEncaset, DateTime fechaRegistro)
    {
        var dias = (fechaRegistro.Date - fechaEncaset.Date).TotalDays;
        return Math.Max(1, (int)Math.Floor(dias / 7.0) + 1);
    }

    /// <summary>
    /// Descuenta aves de lote_postura_levante (aves_h_actual, aves_m_actual).
    /// Busca por lote_postura_levante_id o por lote_id.
    /// </summary>
    private async Task DescontarAvesEnLotePosturaLevanteAsync(int loteId, int? lotePosturaLevanteId, int hembras, int machos)
    {
        await AjustarAvesEnLotePosturaLevanteAsync(loteId, lotePosturaLevanteId, -hembras, -machos);
    }

    /// <summary>
    /// Ajusta aves en lote_postura_levante. deltaH/deltaM positivos = sumar, negativos = restar.
    /// </summary>
    private async Task AjustarAvesEnLotePosturaLevanteAsync(int loteId, int? lotePosturaLevanteId, int deltaH, int deltaM)
    {
        if (deltaH == 0 && deltaM == 0) return;

        var lev = lotePosturaLevanteId.HasValue
            ? await _ctx.LotePosturaLevante.FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId.Value && l.DeletedAt == null)
            : await _ctx.LotePosturaLevante.FirstOrDefaultAsync(l => l.LoteId == loteId && l.DeletedAt == null);
        if (lev == null) return;

        var avesH = (lev.AvesHActual ?? 0) + deltaH;
        var avesM = (lev.AvesMActual ?? 0) + deltaM;
        lev.AvesHActual = Math.Max(0, avesH);
        lev.AvesMActual = Math.Max(0, avesM);
        lev.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
    }

    /// <summary>
    /// Parseo de items de metadata (itemsHembras/Machos/Generales) → kg por ítem.
    /// Delega en el cálculo puro central compartido (misma lógica que engorde/producción;
    /// un solo lugar → un solo test). Antes había una copia idéntica acá + su propio ToKg.
    /// </summary>
    private static Dictionary<int, decimal> ParseMetadataItemsToKg(JsonElement root)
        => MetadataEngordeCalculos.ParseMetadataItemsToKg(root);

    /// <summary>
    /// Variante TIPADA del parseo (conserva el origen del id — camino 1/2) para las ramas
    /// Colombia (IColombiaInventarioConsumoService), donde catalogItemId e
    /// itemInventarioEcuadorId conviven y sus rangos numéricos colisionan.
    /// </summary>
    private static Dictionary<ItemConsumoKey, decimal> ParseMetadataItemsToKgPorOrigen(JsonElement root)
        => MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(root);
}

public interface IGramajeProviderV2
{
    Task<double?> GetGramajeGrPorAveAsync(string galponId, int semana, string tipoAlimento);
}

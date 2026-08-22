// src/ZooSanMarino.Infrastructure/Services/InventarioGestionService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.Exceptions;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

// `partial` desde A1/A2: las primitivas atómicas de stock viven en
// Services/InventarioGestion/Funciones/InventarioGestionService.StockAtomico.cs.
// Este archivo es el ANCLA (usings, campos, ctor y la interfaz), según la convención de CLAUDE.md.
public partial class InventarioGestionService : IInventarioGestionService
{
    /// <summary>Etiquetas de operación (coinciden con <see cref="MapTipoOperacionLabel"/>).</summary>
    private static readonly IReadOnlyList<string> TiposOperacionFiltroLabels =
    [
        "Ingreso",
        "Consumo",
        "Traslado (salida entre galpones)",
        "Traslado (entrada entre galpones)",
        "Traslado entre granjas (solicitud pendiente)",
        "Traslado entre granjas (en tránsito)",
        "Traslado entre granjas (recepción)",
        "Traslado entre granjas (rechazado)",
        "Ajuste manual de stock",
        "Eliminación de registro de stock"
    ];

    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;
    private readonly ILogger<InventarioGestionService>? _logger;

    public InventarioGestionService(
        ZooSanMarinoContext db,
        ICurrentUser? current,
        ICompanyResolver companyResolver,
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService,
        ILogger<InventarioGestionService>? logger = null)
    {
        _db = db;
        _current = current;
        _companyResolver = companyResolver;
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
        _logger = logger;
    }

    /// <summary>
    /// Refresca <c>seguimiento_diario_aves_engorde.saldo_alimento_kg</c> de los lotes de pollo engorde
    /// del galpón después de un movimiento de alimento.
    /// <para>
    /// Sin esto la columna solo se recalculaba al crear o editar un seguimiento diario, así que un
    /// ingreso o traslado posterior al último día cargado quedaba invisible para la liquidación y para
    /// «Cuadrar Saldos» (la grilla no, porque recalcula en vivo con la fn). Ver
    /// <see cref="SaldoAlimentoEngordeAplicador"/>.
    /// </para>
    /// <para>
    /// Se llama SIEMPRE después del <c>SaveChangesAsync</c>: la fila del histórico —que es la que lee
    /// el saldo— la escribe el trigger <c>trg_inventario_gestion_movimiento_lote_hist</c> en el INSERT.
    /// </para>
    /// </summary>
    /// <param name="movementType">
    /// Tipo del movimiento que motivó el refresco. Solo entradas y salidas de alimento mueven el
    /// saldo: el consumo lo aporta el seguimiento diario y los ajustes manuales entran como
    /// <c>INV_OTRO</c>, que ningún cálculo del saldo mira
    /// (<see cref="TipoEventoInventarioCalculos.AfectaSaldoAlimentoEngorde"/>).
    /// </param>
    private Task RefrescarSaldoAlimentoEngordeAsync(
        int companyId, int farmId, string? nucleoId, string? galponId, string? movementType, CancellationToken ct)
        => !TipoEventoInventarioCalculos.AfectaSaldoAlimentoEngorde(movementType)
        ? Task.CompletedTask
        : SaldoAlimentoEngordeAplicador.RecalcularPorUbicacionAsync(
            _db, companyId, farmId, nucleoId, galponId,
            ex => _logger?.LogError(ex,
                "No se pudo refrescar el saldo de alimento de engorde tras un movimiento de inventario " +
                "(granja {FarmId}, núcleo {NucleoId}, galpón {GalponId}). El movimiento SÍ quedó guardado y " +
                "la tabla diaria lo muestra bien; solo la columna persistida queda vieja hasta el próximo " +
                "recálculo.", farmId, nucleoId, galponId),
            ct);

    /// <summary>
    /// Fecha en histórico: día elegido, anclado a <paramref name="horaAncla"/> UTC; si no hay fecha,
    /// hora actual del servidor (mismo criterio histórico).
    ///
    /// <para>
    /// F2 (22-ago-2026): <paramref name="horaAncla"/> default 12 conserva el comportamiento de
    /// SIEMPRE — todo llamador que no lo pase queda exactamente igual. Los DOS caminos de CONSUMO
    /// (<c>RegistrarConsumoAsync</c> y <c>RegistrarConsumoNivelGranjaAsync</c>) pasan
    /// <see cref="FechaMovimientoSeguimientoCalculos.AnclaConsumoUtc"/> para no empatar con el
    /// ingreso del mismo día — ver el doc-comment de esa constante para el porqué.
    /// </para>
    /// </summary>
    private static DateTimeOffset ResolveMovimientoCreatedAt(
        DateTime? fechaMovimiento,
        int horaAncla = FechaMovimientoSeguimientoCalculos.AnclaIngresoUtc)
    {
        if (!fechaMovimiento.HasValue)
            return DateTimeOffset.UtcNow;
        return FechaMovimientoSeguimientoCalculos.Anclar(fechaMovimiento.Value.Date, horaAncla);
    }

    private async Task<int?> GetEffectiveCompanyIdAsync(CancellationToken ct = default)
    {
        if (_current == null) return null;
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId > 0 ? _current.CompanyId : null;
    }

    private async Task<int> GetEffectivePaisIdAsync(int? fromFarmId, CancellationToken ct = default)
    {
        if (_current?.PaisId > 0) return _current.PaisId.Value;
        if (fromFarmId.HasValue)
        {
            var farm = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fromFarmId.Value, ct);
            if (farm != null)
            {
                var paisId = await _db.Set<Departamento>().Where(d => d.DepartamentoId == farm.DepartamentoId).Select(d => d.PaisId).FirstOrDefaultAsync(ct);
                if (paisId > 0) return paisId;
            }
        }
        return 0;
    }

    /// <summary>Granjas del usuario (user_farms) que pertenecen a la empresa. Stock solo en estas granjas.</summary>
    private async Task<HashSet<int>> GetAssignedFarmIdsInCompanyAsync(int companyId, CancellationToken ct = default)
    {
        if (_current?.UserGuid is not { } uid || uid == Guid.Empty)
            return new HashSet<int>();

        var ids = await _farmService.GetAssignedFarmIdsForUserAsync(uid, ct).ConfigureAwait(false);
        var farms = await _farmService.GetFarmDtosByIdsInCompanyAsync(ids, companyId, ct).ConfigureAwait(false);
        return farms.Select(f => f.Id).ToHashSet();
    }

    /// <summary>Granjas asignadas al usuario en la empresa + núcleos y galpones asociados (misma regla que filtros de stock/histórico).</summary>
    private async Task<(List<FarmDto> FarmsOrigen, List<NucleoDto> NucleosOrigen, List<GalponLiteDto> GalponesOrigen)> LoadOrigenUbicacionUsuarioEnEmpresaAsync(
        int companyId,
        CancellationToken ct = default)
    {
        if (_current?.UserGuid is not { } userGuid || userGuid == Guid.Empty)
            return ([], [], []);

        var idsOrigen = await _farmService.GetAssignedFarmIdsForUserAsync(userGuid, ct).ConfigureAwait(false);
        var farmsOrigen = (await _farmService.GetFarmDtosByIdsInCompanyAsync(idsOrigen, companyId, ct).ConfigureAwait(false)).ToList();
        var allowedOrigenIds = farmsOrigen.Select(f => f.Id).ToHashSet();
        if (allowedOrigenIds.Count == 0)
            return (farmsOrigen, [], []);

        var nucleosAll = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var nucleosOrigen = nucleosAll.Where(n => allowedOrigenIds.Contains(n.GranjaId)).ToList();

        var galponesDetailAll = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesOrigen = galponesDetailAll
            .Where(g => allowedOrigenIds.Contains(g.GranjaId))
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        return (farmsOrigen, nucleosOrigen, galponesOrigen);
    }

    public async Task<InventarioGestionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
        {
            return new InventarioGestionFilterDataDto(
                Array.Empty<FarmDto>(),
                Array.Empty<FarmDto>(),
                Array.Empty<NucleoDto>(),
                Array.Empty<NucleoDto>(),
                Array.Empty<GalponLiteDto>(),
                Array.Empty<GalponLiteDto>(),
                CompanyManejaAlimentoPorGalpon: false,
                Silos: Array.Empty<InventarioGestionSiloDto>());
        }

        var cid = companyId.Value;

        // Defaults GLOBALES de la empresa: manejo de alimento (el front resuelve el efectivo por
        // granja) y si el inventario se ubica en SILOS en vez de galpones.
        var companyFlags = await _db.Set<Company>().AsNoTracking()
            .Where(c => c.Id == cid)
            .Select(c => new { c.ManejaAlimentoPorGalpon, c.ManejaInventarioPorSilo })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var companyManejaPorGalpon = companyFlags?.ManejaAlimentoPorGalpon ?? false;
        var companyManejaPorSilo = companyFlags?.ManejaInventarioPorSilo ?? false;

        // Todas las granjas de la empresa (destino: traslado inter-granja, procedencia en ingreso "otra granja"/bodega, etc.)
        var farmsDestino = (await _farmService.GetAllAsync(userId: null, companyId: cid).ConfigureAwait(false)).ToList();
        var allowedDestinoIds = farmsDestino.Select(f => f.Id).ToHashSet();

        var (farmsOrigen, nucleosOrigen, galponesOrigen) = await LoadOrigenUbicacionUsuarioEnEmpresaAsync(cid, ct).ConfigureAwait(false);

        var nucleosAll = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var nucleosDestino = allowedDestinoIds.Count > 0
            ? nucleosAll.Where(n => allowedDestinoIds.Contains(n.GranjaId)).ToList()
            : new List<NucleoDto>();

        var galponesDetailAll = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetailDestino = allowedDestinoIds.Count > 0
            ? galponesDetailAll.Where(g => allowedDestinoIds.Contains(g.GranjaId)).ToList()
            : new List<GalponDetailDto>();

        var galponesDestino = galponesDetailDestino
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId)).ToList();

        // Silos de las granjas visibles (origen + destino: la recepción de tránsito elige el silo de
        // la granja que recibe). Con el flag apagado no se consulta nada: cero costo para el resto.
        var silos = new List<InventarioGestionSiloDto>();
        if (companyManejaPorSilo)
        {
            var farmIdsConSilos = farmsOrigen.Select(f => f.Id)
                .Concat(allowedDestinoIds)
                .Distinct()
                .ToList();

            if (farmIdsConSilos.Count > 0)
            {
                silos = await _db.FarmSilos.AsNoTracking()
                    .Where(fs => farmIdsConSilos.Contains(fs.GranjaId) && fs.DeletedAt == null && fs.Activo)
                    // Mismo orden que GetSilosElegiblesAsync (bodega al final, silos por número de
                    // catálogo) para que el front los pinte igual en el selector y en la grilla.
                    .OrderBy(fs => fs.GranjaId)
                    .ThenBy(fs => fs.Tipo == Domain.Entities.FarmSilo.TipoBodega ? 1 : 0)
                    .ThenBy(fs => _db.SiloCatalogo
                        .Where(sc => sc.Id == fs.SiloCatalogoId)
                        .Select(sc => (int?)sc.Numero)
                        .FirstOrDefault() ?? int.MaxValue)
                    .ThenBy(fs => fs.Nombre)
                    .Select(fs => new InventarioGestionSiloDto(
                        fs.Id, fs.GranjaId, fs.Nombre, fs.Tipo, fs.CodigoErpUbicacion, fs.CodigoBodega))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }
        }

        return new InventarioGestionFilterDataDto(
            FarmsOrigen: farmsOrigen,
            FarmsDestino: farmsDestino,
            NucleosOrigen: nucleosOrigen,
            NucleosDestino: nucleosDestino,
            GalponesOrigen: galponesOrigen,
            GalponesDestino: galponesDestino,
            CompanyManejaAlimentoPorGalpon: companyManejaPorGalpon,
            Silos: silos,
            CompanyManejaInventarioPorSilo: companyManejaPorSilo);
    }

    public async Task<InventarioGestionHistoricoFiltrosDto> GetHistoricoFiltrosAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
        {
            return new InventarioGestionHistoricoFiltrosDto(
                Array.Empty<InventarioGestionLoteFiltroDto>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                TiposOperacionFiltroLabels,
                Array.Empty<FarmDto>(),
                Array.Empty<NucleoDto>(),
                Array.Empty<GalponLiteDto>());
        }

        var cid = companyId.Value;
        var (farmsOrigenHist, nucleosOrigenHist, galponesOrigenHist) =
            await LoadOrigenUbicacionUsuarioEnEmpresaAsync(cid, ct).ConfigureAwait(false);
        var allowedFarmIds = farmsOrigenHist.Select(f => f.Id).ToHashSet();
        var paisId = await GetEffectivePaisIdAsync(null, ct);

        if (allowedFarmIds.Count == 0)
        {
            return new InventarioGestionHistoricoFiltrosDto(
                Array.Empty<InventarioGestionLoteFiltroDto>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                TiposOperacionFiltroLabels,
                farmsOrigenHist,
                nucleosOrigenHist,
                galponesOrigenHist);
        }

        // Lotes por granja asignada: la empresa se toma de farms.company_id (muchas filas legacy tienen lotes.company_id desalineado).
        var lotesQuery = _db.Lotes.AsNoTracking()
            .Join(_db.Farms.AsNoTracking(),
                l => l.GranjaId,
                f => f.Id,
                (l, f) => new { l, f })
            .Where(x => x.l.DeletedAt == null
                        && allowedFarmIds.Contains(x.l.GranjaId)
                        && x.f.CompanyId == cid
                        && x.f.DeletedAt == null);
        if (paisId > 0)
            lotesQuery = lotesQuery.Where(x => x.l.PaisId == null || x.l.PaisId == paisId);

        var lotes = await lotesQuery
            .OrderByDescending(x => x.l.FechaEncaset)
            .ThenBy(x => x.l.LoteNombre)
            .Select(x => new InventarioGestionLoteFiltroDto(
                x.l.LoteId!.Value,
                x.l.LoteNombre,
                x.l.Fase,
                x.l.GranjaId,
                x.l.NucleoId,
                x.l.GalponId))
            .ToListAsync(ct);

        var movBase = _db.InventarioGestionMovimientos.AsNoTracking()
            .Where(m => m.CompanyId == cid && allowedFarmIds.Contains(m.FarmId));
        if (paisId > 0)
            movBase = movBase.Where(m => m.PaisId == paisId);

        var itemsJoin = _db.ItemInventario.AsNoTracking();

        var conceptos = await (
            from m in movBase
            join i in itemsJoin on m.ItemInventarioEcuadorId equals i.Id
            where i.Concepto != null && i.Concepto != ""
            select i.Concepto!.Trim()
        ).Distinct().ToListAsync(ct);
        conceptos.Sort(StringComparer.OrdinalIgnoreCase);

        var tiposItem = await (
            from m in movBase
            join i in itemsJoin on m.ItemInventarioEcuadorId equals i.Id
            where i.TipoItem != null && i.TipoItem != ""
            select i.TipoItem.Trim()
        ).Distinct().ToListAsync(ct);
        tiposItem.Sort(StringComparer.OrdinalIgnoreCase);

        var estados = await movBase
            .Where(m => m.Estado != null && m.Estado != "")
            .Select(m => m.Estado!.Trim())
            .Distinct()
            .ToListAsync(ct);
        estados.Sort(StringComparer.OrdinalIgnoreCase);

        var movementTypes = await movBase
            .Where(m => m.MovementType != null && m.MovementType != "")
            .Select(m => m.MovementType.Trim())
            .Distinct()
            .ToListAsync(ct);
        movementTypes.Sort(StringComparer.OrdinalIgnoreCase);

        var unidades = await movBase
            .Where(m => m.Unit != null && m.Unit != "")
            .Select(m => m.Unit.Trim())
            .Distinct()
            .ToListAsync(ct);
        unidades.Sort(StringComparer.OrdinalIgnoreCase);

        return new InventarioGestionHistoricoFiltrosDto(
            lotes,
            conceptos,
            tiposItem,
            estados,
            movementTypes,
            unidades,
            TiposOperacionFiltroLabels,
            farmsOrigenHist,
            nucleosOrigenHist,
            galponesOrigenHist);
    }

    public async Task<List<InventarioGestionStockDto>> GetStockAsync(
        int? farmId = null,
        string? nucleoId = null,
        string? galponId = null,
        string? itemType = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var paisId = await GetEffectivePaisIdAsync(farmId, ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionStockDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionStockDto>();

        var query = _db.InventarioGestionStock
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value && allowedFarmIds.Contains(x.FarmId));
        if (paisId > 0) query = query.Where(x => x.PaisId == paisId);
        if (farmId.HasValue) query = query.Where(x => x.FarmId == farmId.Value);
        if (!string.IsNullOrWhiteSpace(nucleoId)) query = query.Where(x => x.NucleoId == nucleoId);
        if (!string.IsNullOrWhiteSpace(galponId)) query = query.Where(x => x.GalponId == galponId);
        if (!string.IsNullOrWhiteSpace(itemType))
        {
            var it = itemType.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Concepto != null &&
                 x.ItemInventario.Concepto.Trim().ToLower() == it) ||
                (x.ItemInventario.TipoItem != null &&
                 x.ItemInventario.TipoItem.Trim().ToLower() == it));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventario.Nombre ?? "").ToLower().Contains(s));
        }

        var list = await query.OrderBy(x => x.Farm.Name).ThenBy(x => x.NucleoId).ThenBy(x => x.GalponId).ThenBy(x => x.ItemInventario.Nombre).ToListAsync(ct);

        var nucleos = await _db.Nucleos.AsNoTracking().Where(n => list.Select(x => x.NucleoId).Contains(n.NucleoId)).ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct);
        var galpones = await _db.Galpones.AsNoTracking().Where(g => list.Select(x => x.GalponId).Contains(g.GalponId)).ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct);
        var silos = await NombresDeSilosAsync(list.Select(x => x.SiloId), ct);

        // Separación pendiente de validar: kilos que un seguimiento diario ya comprometió pero que
        // todavía no se descontaron. Se resuelve acá, en la ÚNICA consulta que responde el saldo, para
        // que ninguna pantalla tenga que acordarse de restarlo por su cuenta.
        var farmsEnVista = list.Select(x => x.FarmId).Distinct().ToList();
        var reservas = farmsEnVista.Count == 0
            ? new List<(int FarmId, int ItemId, string? NucleoId, string? GalponId, int? SiloId, decimal Kg)>()
            : (await _db.SeguimientoReservaAlimento.AsNoTracking()
                .Where(r => farmsEnVista.Contains(r.FarmId) && r.Estado == EstadoReservaSeguimiento.Activa)
                .GroupBy(r => new { r.FarmId, r.ItemInventarioEcuadorId, r.NucleoId, r.GalponId, r.SiloId })
                .Select(g => new
                {
                    g.Key.FarmId, g.Key.ItemInventarioEcuadorId, g.Key.NucleoId, g.Key.GalponId, g.Key.SiloId,
                    Kg = g.Sum(r => r.CantidadKg)
                })
                .ToListAsync(ct))
                .Select(g => (FarmId: g.FarmId, ItemId: g.ItemInventarioEcuadorId, NucleoId: g.NucleoId, GalponId: g.GalponId, SiloId: g.SiloId, Kg: g.Kg))
                .ToList();

        static string NormUbic(string? v) => (v ?? "").Trim();
        var reservadoPorUbicacion = reservas.ToDictionary(
            r => (r.FarmId, r.ItemId, NormUbic(r.NucleoId), NormUbic(r.GalponId), r.SiloId ?? 0),
            r => r.Kg);

        return list.Select(x =>
        {
            var reservado = reservadoPorUbicacion.TryGetValue(
                (x.FarmId, x.ItemInventarioEcuadorId, NormUbic(x.NucleoId), NormUbic(x.GalponId), x.SiloId ?? 0),
                out var kgReservado) ? kgReservado : 0m;
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;
            var itemTypeOut = x.ItemInventario.Concepto ?? x.ItemInventario.TipoItem ?? "alimento";
            return new InventarioGestionStockDto(
                x.Id, x.FarmId, x.NucleoId, x.GalponId, x.ItemInventarioEcuadorId,
                x.ItemInventario.Codigo, x.ItemInventario.Nombre, itemTypeOut,
                // TK-2026-000019 — la unidad la manda el CATÁLOGO, no la columna de la fila. La fila
                // arrastra el default 'kg' de cuando se creó y nadie la sincronizaba, así que un
                // producto creado en litros salía en kilos en esta misma pantalla. El backfill
                // realinea la columna, pero la proyección no depende de que se haya corrido.
                x.Quantity, UnidadInventarioCalculos.Resolver(x.ItemInventario.Unidad, x.Unit),
                x.Farm.Name, nucleoNombre, galponNombre, x.CreatedAt,
                AvisoFechaFueraDeCiclo: null,
                SiloId: x.SiloId,
                SiloNombre: x.SiloId.HasValue && silos.TryGetValue(x.SiloId.Value, out var sn) ? sn : null,
                // DisponibleKg ya no se pasa: es una propiedad DERIVADA del DTO (Quantity − ReservadoKg,
                // la misma cuenta que hacía ReservaSeguimientoCalculos.DisponibleAlimento). Como
                // parámetro, los nueve sitios que arman este DTO a mano para las respuestas de ingreso,
                // traslado y consumo lo dejaban en 0 y el front habría leído «no hay nada».
                ReservadoKg: reservado);
        }).ToList();
    }

    private static bool IsAlimento(ItemInventario item)
    {
        var concept = item.Concepto;
        if (!string.IsNullOrWhiteSpace(concept))
            return string.Equals(concept.Trim(), "alimento", StringComparison.OrdinalIgnoreCase);
        return string.Equals(item.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ¿El alimento de esta granja se maneja a NIVEL GRANJA (sin núcleo/galpón)? Negación del flag
    /// CONFIGURABLE por empresa/granja: <c>manejaPorGalpon = farm.ManejaAlimentoPorGalpon ??
    /// company.ManejaAlimentoPorGalpon</c> (ver <see cref="AlimentoNivelResolver"/>). Reemplaza la
    /// decisión previa por país (Colombia=granja); el seed de la migración preserva ese comportamiento.
    /// Solo aplica a alimento; otros conceptos van siempre a nivel granja.
    /// </summary>
    private async Task<bool> EsInventarioNivelGranjaAsync(int farmId, CancellationToken ct)
    {
        var flags = await _db.Farms.AsNoTracking()
            .Where(f => f.Id == farmId)
            .Join(_db.Set<Company>().AsNoTracking(), f => f.CompanyId, c => c.Id,
                (f, c) => new { Farm = f.ManejaAlimentoPorGalpon, Company = c.ManejaAlimentoPorGalpon })
            .FirstOrDefaultAsync(ct);
        // Sin datos → nivel granja (comportamiento seguro: no exige galpón).
        if (flags == null) return true;
        return !ZooSanMarino.Application.Calculos.AlimentoNivelResolver.ManejaPorGalpon(flags.Farm, flags.Company);
    }

    private async Task<(int CompanyId, int PaisId)> GetFarmCompanyAndPaisAsync(int farmId, CancellationToken ct)
    {
        var farm = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == farmId, ct);
        if (farm == null) throw new InvalidOperationException($"La granja {farmId} no existe.");
        var departamento = await _db.Set<Departamento>().AsNoTracking().FirstOrDefaultAsync(d => d.DepartamentoId == farm.DepartamentoId, ct);
        if (departamento == null) throw new InvalidOperationException($"No se encontró el departamento de la granja {farmId}.");
        return (farm.CompanyId, departamento.PaisId);
    }

    public async Task<InventarioGestionStockDto> RegistrarIngresoAsync(InventarioGestionIngresoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
        var item = await _db.ItemInventario.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);
        if (_current?.CompanyId > 0 && _current.CompanyId != companyId)
            throw new InvalidOperationException("La granja no pertenece a su empresa.");
        var effectivePais = await GetEffectivePaisIdAsync(req.FarmId, ct);
        if (effectivePais > 0 && paisId != effectivePais)
            throw new InvalidOperationException("La granja no pertenece al país activo.");

        var isAlimento = IsAlimento(item);

        // ¿La empresa ubica el inventario por SILO? La decisión sale del flag de la empresa dueña de
        // la granja (InventarioUbicacionSiloCalculos, puro y con tests). Con el flag apagado, todo lo
        // de abajo es exactamente lo de siempre.
        var modoUbicacion = await ResolverModoUbicacionAsync(req.FarmId, ct);
        var errorSilo = InventarioUbicacionSiloCalculos.ValidarUbicacion(modoUbicacion, req.SiloId, req.GalponId, isAlimento);
        if (errorSilo is not null) throw new InvalidOperationException(errorSilo);

        var usaUbicacion = false;
        if (modoUbicacion == ModoUbicacionInventario.PorSilo)
        {
            // El silo ES la ubicación: no se valida núcleo/galpón porque no se persisten. Lo que sí
            // se valida es que el silo sea de ESTA granja —regla 4 del plan: nunca un descuento
            // silencioso en el silo de otra—.
            await ValidarSiloDeGranjaAsync(req.FarmId, req.SiloId!.Value, ct);
        }
        else
        {
            // Alimento por galpón vs nivel granja: CONFIGURABLE por empresa/granja (antes era por país).
            var nivelGranja = await EsInventarioNivelGranjaAsync(req.FarmId, ct);
            usaUbicacion = isAlimento && !nivelGranja;
            if (usaUbicacion && (string.IsNullOrWhiteSpace(req.NucleoId) || string.IsNullOrWhiteSpace(req.GalponId)))
                throw new InvalidOperationException("Para ítem tipo alimento debe indicar Núcleo y Galpón.");
            if (!usaUbicacion && (!string.IsNullOrWhiteSpace(req.NucleoId) || !string.IsNullOrWhiteSpace(req.GalponId)))
                throw new InvalidOperationException(nivelGranja
                    ? "Esta granja maneja el alimento a nivel granja (no use Núcleo/Galpón)."
                    : "Para ítems que no son alimento el inventario es solo a nivel granja (no use Núcleo/Galpón).");
        }

        var origenTipoNorm = req.OrigenTipo?.Trim() ?? "";
        if (string.Equals(origenTipoNorm, "granja", StringComparison.OrdinalIgnoreCase))
        {
            if (!req.OrigenFarmId.HasValue || req.OrigenFarmId.Value <= 0)
                throw new InvalidOperationException("Cuando el origen es otra granja, indique la granja de procedencia (OrigenFarmId).");
            if (req.OrigenFarmId.Value == req.FarmId)
                throw new InvalidOperationException("La granja de origen debe ser distinta a la granja de destino del ingreso.");
            var (origCompanyId, _) = await GetFarmCompanyAndPaisAsync(req.OrigenFarmId.Value, ct);
            if (origCompanyId != companyId)
                throw new InvalidOperationException("La granja de origen debe pertenecer a la misma empresa.");
        }
        if (string.Equals(origenTipoNorm, "bodega", StringComparison.OrdinalIgnoreCase))
        {
            if (!req.OrigenFarmId.HasValue || req.OrigenFarmId.Value <= 0)
                throw new InvalidOperationException("Cuando el origen es bodega, indique la granja a la que pertenece la bodega de procedencia (OrigenFarmId).");
            var (bodegaFarmCompanyId, _) = await GetFarmCompanyAndPaisAsync(req.OrigenFarmId.Value, ct);
            if (bodegaFarmCompanyId != companyId)
                throw new InvalidOperationException("La granja de la bodega de origen debe pertenecer a la misma empresa.");
        }

        var (nucleoId, galponId, siloId) = InventarioUbicacionSiloCalculos.NormalizarUbicacion(
            modoUbicacion,
            usaUbicacion ? req.NucleoId!.Trim() : null,
            usaUbicacion ? req.GalponId!.Trim() : null,
            req.SiloId);

        GuardarMarcaProximoCicloApagada(req.ParaProximoCiclo);

        var estadoIngreso = string.Equals(origenTipoNorm, "planta", StringComparison.OrdinalIgnoreCase)
            ? "Entrada planta"
            : string.Equals(origenTipoNorm, "bodega", StringComparison.OrdinalIgnoreCase)
                ? "Entrada bodega"
                : "Entrada granja";
        var movCreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        // TK-2026-000019 — la unidad la fija el catálogo del ítem. Antes era `req.Unit ?? "kg"`: el
        // front manda la del ítem, pero cualquier otro llamador (o un request sin unidad) grababa
        // kilos sobre un producto que se vende en litros.
        var unidad = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit);

        // A1 — upsert ATÓMICO. Antes esto era buscar-o-insertar: dos ingresos concurrentes sobre
        // una clave sin fila no encontraban nada y ambos insertaban, y como todas las lecturas
        // usan FirstOrDefault, la segunda fila quedaba INVISIBLE para siempre. Con el índice
        // único de la clave natural, el ON CONFLICT convierte esa carrera en una suma.
        InventarioGestionStock existing = null!;
        var mov = new InventarioGestionMovimiento
        {
            CompanyId = companyId,
            PaisId = paisId,
            FarmId = req.FarmId,
            NucleoId = nucleoId,
            GalponId = galponId,
            SiloId = siloId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            Unit = unidad,
            MovementType = "Ingreso",
            Estado = estadoIngreso,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            CreatedAt = movCreatedAt,
            CreatedByUserId = _current?.UserId.ToString(),
            ParaProximoCiclo = req.ParaProximoCiclo,
            // Auditoría: el instante REAL de captura. `CreatedAt` lo pisa la fecha que tipea el
            // usuario, así que no puede servir de auditoría; acá nunca se escribe esa fecha.
            RegistradoAt = DateTimeOffset.UtcNow
        };

        // El stock y el movimiento que lo explica van juntos o no van.
        await EnTransaccionAsync(async () =>
        {
            existing = await SumarStockAtomicoAsync(
                companyId, paisId, req.FarmId, nucleoId, galponId,
                req.ItemInventarioEcuadorId, req.Quantity, unidad, siloId, ct);

            // El movimiento y la fila de stock quedan con la MISMA unidad, la del catálogo: el
            // upsert ya realineó la fila (`unit = EXCLUDED.unit`). Antes acá se heredaba la unidad
            // vieja de la fila, que es cómo el 'kg' original se propagaba a cada movimiento nuevo.
            mov.Unit = existing.Unit;

            _db.InventarioGestionMovimientos.Add(mov);
            await _db.SaveChangesAsync(ct);
        }, ct);

        await RefrescarSaldoAlimentoEngordeAsync(companyId, req.FarmId, nucleoId, galponId, mov.MovementType, ct);

        // Avisa —sin bloquear— si el ingreso quedó fechado fuera del ciclo vigente del galpón.
        // v16a: antes se saltaba cuando venía la marca «para el próximo ciclo» (la atribución era
        // explícita y el aviso, ruido). Con la marca apagada por `GuardarMarcaProximoCicloApagada`
        // ese camino es inalcanzable, así que el aviso vuelve a evaluarse siempre.
        var aviso = await EvaluarAvisoFechaFueraDeCicloAsync(
            companyId, req.FarmId, nucleoId, galponId, movCreatedAt, ct);

        var dto = (await GetStockAsync(req.FarmId, nucleoId, galponId, null, null, ct))
            .FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId
                              && x.NucleoId == nucleoId && x.GalponId == galponId && x.SiloId == siloId)
            ?? new InventarioGestionStockDto(existing.Id, existing.FarmId, existing.NucleoId, existing.GalponId, existing.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", existing.Quantity, existing.Unit, null, null, null, null, null, existing.SiloId);

        return aviso is null ? dto : dto with { AvisoFechaFueraDeCiclo = aviso };
    }

    public async Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoAsync(InventarioGestionTrasladoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
        var item = await _db.ItemInventario.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");

        // Colombia (nivel granja): alimento sin núcleo/galpón → mismo camino que un ítem no-alimento
        // (traslado a nivel granja). EC/PA conservan el galpón-a-galpón para alimento.
        var (_, paisIdOrigen) = await GetFarmCompanyAndPaisAsync(req.FromFarmId, ct);
        var nivelGranja = await EsInventarioNivelGranjaAsync(req.FromFarmId, ct);
        var isAlimento = IsAlimento(item);
        var usaUbicacion = isAlimento && !nivelGranja;

        var mismaGranja = req.FromFarmId == req.ToFarmId;

        // Empresas que ubican por silo: el traslado es entre SILOS (o bodega -> silo) de la misma
        // granja. El nucleo/galpon no participa —un mismo silo puede alimentar a varios galpones—,
        // asi que ni se pide ni se persiste.
        var modoOrigen = await ResolverModoUbicacionAsync(req.FromFarmId, ct);
        if (modoOrigen == ModoUbicacionInventario.PorSilo)
        {
            var errorOrigenSilo = InventarioUbicacionSiloCalculos.ValidarUbicacion(
                modoOrigen, req.FromSiloId, req.FromGalponId, isAlimento);
            if (errorOrigenSilo is not null)
                throw new InvalidOperationException($"Origen: {errorOrigenSilo}");

            await ValidarSiloDeGranjaAsync(req.FromFarmId, req.FromSiloId!.Value, ct);

            if (mismaGranja)
            {
                var errorDestinoSilo = InventarioUbicacionSiloCalculos.ValidarUbicacion(
                    modoOrigen, req.ToSiloId, req.ToGalponId, isAlimento);
                if (errorDestinoSilo is not null)
                    throw new InvalidOperationException($"Destino: {errorDestinoSilo}");
                if (req.FromSiloId!.Value == req.ToSiloId!.Value)
                    throw new InvalidOperationException("El silo de destino debe ser distinto al de origen.");

                await ValidarSiloDeGranjaAsync(req.ToFarmId, req.ToSiloId!.Value, ct);
                return await RegistrarTrasladoMismaGranjaAsync(
                    req, item, null, null, null, null, req.FromSiloId, req.ToSiloId, ct);
            }

            // Inter-granja: el silo destino es solo una sugerencia hasta que el destino reciba el
            // transito, igual que el galpon destino en el camino clasico.
            return await RegistrarTrasladoInterGranjaTransitoAsync(req, item, isAlimento: false, ct);
        }

        if (mismaGranja)
        {
            if (!usaUbicacion)
                throw new InvalidOperationException(nivelGranja
                    ? "En Colombia el inventario es solo a nivel granja: no aplica traslado dentro de la misma granja. Use traslado entre granjas distintas."
                    : "Para ítems que no son alimento no aplica traslado entre galpones en la misma granja (el stock es solo a nivel granja). Use traslado entre granjas distintas si aplica.");
            if (string.IsNullOrWhiteSpace(req.FromNucleoId) || string.IsNullOrWhiteSpace(req.FromGalponId) ||
                string.IsNullOrWhiteSpace(req.ToNucleoId) || string.IsNullOrWhiteSpace(req.ToGalponId))
                throw new InvalidOperationException("Para alimento en la misma granja debe indicar Núcleo y Galpón de origen y destino.");
            var fn = req.FromNucleoId.Trim();
            var fg = req.FromGalponId.Trim();
            var tn = req.ToNucleoId.Trim();
            var tg = req.ToGalponId.Trim();
            if (string.Equals(fg, tg, StringComparison.Ordinal) && string.Equals(fn, tn, StringComparison.Ordinal))
                throw new InvalidOperationException("El galpón de destino debe ser distinto al de origen.");
            return await RegistrarTrasladoMismaGranjaAsync(req, item, fn, fg, tn, tg, null, null, ct);
        }

        return await RegistrarTrasladoInterGranjaTransitoAsync(req, item, usaUbicacion, ct);
    }

    /// <summary>Traslado entre galpones de la misma granja: descuenta origen y suma destino en una sola operación (2 movimientos).</summary>
    private async Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoMismaGranjaAsync(
        InventarioGestionTrasladoRequest req,
        ItemInventario item,
        string? fromNucleoId,
        string? fromGalponId,
        string? toNucleoId,
        string? toGalponId,
        int? fromSiloId,
        int? toSiloId,
        CancellationToken ct)
    {
        // A2 — lectura sin rastreo: la fila se modifica por SQL atómico más abajo, y una copia
        // rastreada con la cantidad vieja haría que el SaveChanges posterior pisara el descuento.
        var stockOrigen = await BuscarStockSinRastreoAsync(req.FromFarmId, req.ItemInventarioEcuadorId, fromNucleoId, fromGalponId, fromSiloId, ct);
        if (stockOrigen == null)
            throw new InvalidOperationException("No hay stock suficiente en el origen para el traslado.");

        var (companyIdTo, paisIdTo) = await GetFarmCompanyAndPaisAsync(req.ToFarmId, ct);
        var transferGroupId = Guid.NewGuid();
        // TK-2026-000019 — el traslado no cambia de unidad por el camino: la del catálogo.
        var unidadTraslado = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit);
        DateTimeOffset movAt = default;

        // Las dos patas del traslado y sus dos movimientos son UNA unidad: descontar el origen sin
        // acreditar el destino (o sin dejar el movimiento que lo explica) crea un descuadre entre
        // granjas que después no se puede reconstruir.
        InventarioGestionStock stockDestino = null!;

        await EnTransaccionAsync(async () =>
        {
            if (!await DescontarStockAtomicoAsync(stockOrigen.Id, req.Quantity, ct))
                throw new InvalidOperationException("No hay stock suficiente en el origen para el traslado.");

            stockDestino = await SumarStockAtomicoAsync(
                companyIdTo, paisIdTo, req.ToFarmId, toNucleoId, toGalponId,
                req.ItemInventarioEcuadorId, req.Quantity, unidadTraslado, toSiloId, ct);

            movAt = await RegistrarMovimientosTrasladoMismaGranjaAsync(
                req, fromNucleoId, fromGalponId, toNucleoId, toGalponId, fromSiloId, toSiloId,
                stockOrigen, unidadTraslado, companyIdTo, paisIdTo, transferGroupId, ct);
        }, ct);

        // Traslado dentro de la misma granja: se movió alimento en DOS galpones.
        await RefrescarSaldoAlimentoEngordeAsync(stockOrigen.CompanyId, req.FromFarmId, fromNucleoId, fromGalponId, "TrasladoSalida", ct);
        await RefrescarSaldoAlimentoEngordeAsync(companyIdTo, req.ToFarmId, toNucleoId, toGalponId, "TrasladoEntrada", ct);

        var listOrigen = (await GetStockAsync(req.FromFarmId, fromNucleoId, fromGalponId, null, null, ct))
            // Con silo, la granja tiene varias filas del mismo item (una por silo): hay que quedarse
            // con la del silo del movimiento o el DTO mostraria el saldo de otro silo.
            .Where(x => x.SiloId == fromSiloId).ToList();
        var listDestino = (await GetStockAsync(req.ToFarmId, toNucleoId, toGalponId, null, null, ct))
            .Where(x => x.SiloId == toSiloId).ToList();
        // `stockOrigen` se leyó ANTES del descuento (AsNoTracking), así que el DTO de respaldo
        // resta a mano; `stockDestino` viene del RETURNING del upsert, o sea ya acumulado.
        var dtoOrigen = listOrigen.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId) ?? new InventarioGestionStockDto(stockOrigen.Id, stockOrigen.FarmId, stockOrigen.NucleoId, stockOrigen.GalponId, stockOrigen.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockOrigen.Quantity - req.Quantity, stockOrigen.Unit, null, null, null, stockOrigen.CreatedAt);
        var dtoDestino = listDestino.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId) ?? new InventarioGestionStockDto(stockDestino.Id, stockDestino.FarmId, stockDestino.NucleoId, stockDestino.GalponId, stockDestino.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockDestino.Quantity, stockDestino.Unit, null, null, null, stockDestino.CreatedAt);

        // Un traslado toca DOS galpones: cada uno tiene su propio ciclo vigente.
        var avisoOrigen  = await EvaluarAvisoFechaFueraDeCicloAsync(stockOrigen.CompanyId, req.FromFarmId, fromNucleoId, fromGalponId, movAt, ct);
        var avisoDestino = await EvaluarAvisoFechaFueraDeCicloAsync(companyIdTo, req.ToFarmId, toNucleoId, toGalponId, movAt, ct);
        if (avisoOrigen  is not null) dtoOrigen  = dtoOrigen  with { AvisoFechaFueraDeCiclo = avisoOrigen };
        if (avisoDestino is not null) dtoDestino = dtoDestino with { AvisoFechaFueraDeCiclo = avisoDestino };

        return (dtoOrigen, dtoDestino);
    }

    /// <summary>
    /// Graba los dos movimientos (salida y entrada) de un traslado dentro de la misma granja y
    /// devuelve la fecha con la que quedaron.
    ///
    /// <para>
    /// Extraído de <c>RegistrarTrasladoMismaGranjaAsync</c> al hacer el traslado atómico: este cuerpo
    /// tiene que ejecutarse DENTRO de la transacción, y así se lee sin anidar cincuenta líneas en una
    /// lambda. No cambia ni un valor respecto de la versión anterior.
    /// </para>
    /// </summary>
    private async Task<DateTimeOffset> RegistrarMovimientosTrasladoMismaGranjaAsync(
        InventarioGestionTrasladoRequest req,
        string? fromNucleoId,
        string? fromGalponId,
        string? toNucleoId,
        string? toGalponId,
        int? fromSiloId,
        int? toSiloId,
        InventarioGestionStock stockOrigen,
        string unidad,
        int companyIdTo,
        int paisIdTo,
        Guid transferGroupId,
        CancellationToken ct)
    {
        var estadoTraslado = string.Equals(req.DestinoTipo?.Trim(), "planta", StringComparison.OrdinalIgnoreCase)
            ? "Transferencia a planta"
            : "Transferencia a granja";
        var movAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
        {
            CompanyId = stockOrigen.CompanyId,
            PaisId = stockOrigen.PaisId,
            FarmId = req.FromFarmId,
            NucleoId = fromNucleoId,
            GalponId = fromGalponId,
            SiloId = fromSiloId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            // Las dos patas del traslado llevan la unidad del CATÁLOGO (TK-2026-000019). Antes cada
            // una copiaba la de su fila de stock, así que una fila torcida seguía escribiendo
            // movimientos torcidos.
            Unit = unidad,
            MovementType = "TrasladoSalida",
            Estado = estadoTraslado,
            FromFarmId = req.ToFarmId,
            // Convencion existente: en la fila de SALIDA los campos From* guardan el OTRO extremo
            // (el destino). El silo sigue el mismo criterio para no inventar una segunda regla.
            FromNucleoId = toNucleoId,
            FromGalponId = toGalponId,
            FromSiloId = toSiloId,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            TransferGroupId = transferGroupId,
            CreatedAt = movAt,
            CreatedByUserId = _current?.UserId.ToString()
        });
        _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
        {
            CompanyId = companyIdTo,
            PaisId = paisIdTo,
            FarmId = req.ToFarmId,
            NucleoId = toNucleoId,
            GalponId = toGalponId,
            SiloId = toSiloId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            Unit = unidad,
            MovementType = "TrasladoEntrada",
            Estado = estadoTraslado,
            FromFarmId = req.FromFarmId,
            FromNucleoId = fromNucleoId,
            FromGalponId = fromGalponId,
            FromSiloId = fromSiloId,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            TransferGroupId = transferGroupId,
            CreatedAt = movAt,
            CreatedByUserId = _current?.UserId.ToString()
        });

        await _db.SaveChangesAsync(ct);
        return movAt;
    }

    /// <summary>
    /// Traslado entre granjas distintas: descuenta origen de inmediato y registra salida en tránsito.
    /// La recepción en destino solo suma stock (no vuelve a descontar origen).
    /// Registros antiguos con movement_type TrasladoInterGranjaPendiente siguen descontando origen al recibir.
    /// </summary>
    private async Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoInterGranjaTransitoAsync(
        InventarioGestionTrasladoRequest req,
        ItemInventario item,
        bool isAlimento,
        CancellationToken ct)
    {
        string? fromNucleoId = null;
        string? fromGalponId = null;
        string? toNucleoHint = null;
        string? toGalponHint = null;

        if (isAlimento)
        {
            if (string.IsNullOrWhiteSpace(req.FromNucleoId) || string.IsNullOrWhiteSpace(req.FromGalponId))
                throw new InvalidOperationException("Para alimento debe indicar Núcleo y Galpón de origen.");
            fromNucleoId = req.FromNucleoId!.Trim();
            fromGalponId = req.FromGalponId!.Trim();
            toNucleoHint = string.IsNullOrWhiteSpace(req.ToNucleoId) ? null : req.ToNucleoId.Trim();
            toGalponHint = string.IsNullOrWhiteSpace(req.ToGalponId) ? null : req.ToGalponId.Trim();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(req.FromNucleoId) || !string.IsNullOrWhiteSpace(req.FromGalponId) ||
                !string.IsNullOrWhiteSpace(req.ToNucleoId) || !string.IsNullOrWhiteSpace(req.ToGalponId))
                throw new InvalidOperationException("Para ítems que no son alimento el traslado entre granjas es solo a nivel granja (sin Núcleo/Galpón).");
        }

        // El silo del origen ya lo valido RegistrarTrasladoAsync; el del destino es una SUGERENCIA
        // hasta que la granja destino reciba el transito (igual que el galpon destino).
        var fromSiloId = req.FromSiloId;
        var toSiloHint = req.ToSiloId;

        // A2 — descuento atómico. Lectura sin rastreo (ver BuscarStockSinRastreoAsync).
        var stockOrigen = await BuscarStockSinRastreoAsync(req.FromFarmId, req.ItemInventarioEcuadorId, fromNucleoId, fromGalponId, fromSiloId, ct);
        if (stockOrigen == null)
            throw new InvalidOperationException("No hay stock suficiente en el origen para registrar el traslado a otra granja.");

        var transferGroupId = Guid.NewGuid();
        var movAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        // TK-2026-000019 — la unidad del catálogo, no la de la fila de origen (que puede arrastrar
        // el 'kg' con el que nació) ni la del request.
        var unidadTransito = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit);

        // El descuento del origen y el movimiento de tránsito que lo explica van juntos: si el
        // movimiento fallara, el alimento saldría de la granja origen sin quedar en tránsito en
        // ningún lado, y el destino nunca podría recibirlo.
        await EnTransaccionAsync(async () =>
        {
            if (!await DescontarStockAtomicoAsync(stockOrigen.Id, req.Quantity, ct))
                throw new InvalidOperationException("No hay stock suficiente en el origen para registrar el traslado a otra granja.");

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = stockOrigen.CompanyId,
                PaisId = stockOrigen.PaisId,
                FarmId = req.FromFarmId,
                NucleoId = fromNucleoId,
                GalponId = fromGalponId,
                SiloId = fromSiloId,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                Quantity = req.Quantity,
                Unit = unidadTransito,
                MovementType = "TrasladoInterGranjaSalida",
                Estado = "Tránsito",
                FromFarmId = req.ToFarmId,
                FromNucleoId = toNucleoHint,
                FromGalponId = toGalponHint,
                FromSiloId = toSiloHint,
                Reference = req.Reference?.Trim(),
                Reason = req.Reason?.Trim(),
                TransferGroupId = transferGroupId,
                CreatedAt = movAt,
                CreatedByUserId = _current?.UserId.ToString()
            });

            await _db.SaveChangesAsync(ct);
        }, ct);
        // Solo el galpón ORIGEN pierde alimento acá; el destino recién suma al recibir el tránsito.
        await RefrescarSaldoAlimentoEngordeAsync(stockOrigen.CompanyId, req.FromFarmId, fromNucleoId, fromGalponId, "TrasladoInterGranjaSalida", ct);

        var listOrigen = (await GetStockAsync(req.FromFarmId, fromNucleoId, fromGalponId, null, null, ct))
            .Where(x => x.SiloId == fromSiloId).ToList();
        var dtoOrigen = listOrigen.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId)
            // `stockOrigen` se leyó ANTES del descuento (AsNoTracking): el respaldo resta a mano.
            ?? new InventarioGestionStockDto(stockOrigen.Id, stockOrigen.FarmId, stockOrigen.NucleoId, stockOrigen.GalponId, stockOrigen.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockOrigen.Quantity - req.Quantity, stockOrigen.Unit, null, null, null, stockOrigen.CreatedAt);
        var itemTypeOut = item.Concepto ?? item.TipoItem ?? "alimento";
        var dtoDestinoPendiente = new InventarioGestionStockDto(
            0,
            req.ToFarmId,
            toNucleoHint,
            toGalponHint,
            req.ItemInventarioEcuadorId,
            item.Codigo,
            item.Nombre,
            itemTypeOut,
            0,
            unidadTransito,
            null,
            null,
            null,
            null);
        return (dtoOrigen, dtoDestinoPendiente);
    }

    public async Task<List<InventarioGestionTransitoPendienteDto>> GetTransitosPendientesAsync(int? farmIdDestino = null, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionTransitoPendienteDto>();

        var candidatos = await _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value && x.TransferGroupId != null &&
                (x.MovementType == "TrasladoInterGranjaPendiente" || x.MovementType == "TrasladoInterGranjaSalida"))
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var gruposConEntrada = (await _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.MovementType == "TrasladoInterGranjaEntrada" && x.TransferGroupId != null)
            .Select(x => x.TransferGroupId!.Value)
            .ToListAsync(ct)).ToHashSet();

        var filtradas = candidatos.Where(s => s.TransferGroupId.HasValue && !gruposConEntrada.Contains(s.TransferGroupId.Value));
        if (farmIdDestino.HasValue)
            filtradas = filtradas.Where(s => s.FromFarmId == farmIdDestino.Value);

        var farmIds = filtradas.SelectMany(s => new[] { s.FarmId, s.FromFarmId ?? 0 }).Where(id => id > 0).Distinct().ToList();
        var farmNames = await _db.Farms.AsNoTracking().Where(f => farmIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        return filtradas.Select(s =>
        {
            farmNames.TryGetValue(s.FarmId, out var fromName);
            var toId = s.FromFarmId ?? 0;
            farmNames.TryGetValue(toId, out var toName);
            var pendienteDespachoOrigen = string.Equals(s.MovementType, "TrasladoInterGranjaPendiente", StringComparison.Ordinal);
            return new InventarioGestionTransitoPendienteDto(
                s.TransferGroupId!.Value,
                s.Id,
                s.FarmId,
                fromName,
                toId,
                toName,
                s.NucleoId,
                s.GalponId,
                s.FromNucleoId,
                s.FromGalponId,
                s.ItemInventarioEcuadorId,
                s.ItemInventario.Codigo,
                s.ItemInventario.Nombre,
                s.Quantity,
                s.Unit,
                s.CreatedAt,
                pendienteDespachoOrigen);
        }).ToList();
    }

    public async Task<InventarioGestionRecepcionTransitoResultDto> RegistrarRecepcionTransitoAsync(InventarioGestionRecepcionTransitoRequest req, CancellationToken ct = default)
    {
        var salida = await _db.InventarioGestionMovimientos
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .FirstOrDefaultAsync(x => x.TransferGroupId == req.TransferGroupId &&
                (x.MovementType == "TrasladoInterGranjaPendiente" || x.MovementType == "TrasladoInterGranjaSalida"), ct);
        if (salida == null)
            throw new InvalidOperationException("No se encontró el movimiento de traslado inter-granja para el grupo indicado.");

        var yaEntrada = await _db.InventarioGestionMovimientos.AnyAsync(
            x => x.TransferGroupId == req.TransferGroupId && x.MovementType == "TrasladoInterGranjaEntrada", ct);
        if (yaEntrada)
            throw new InvalidOperationException("Este traslado ya fue recibido en destino.");

        if (salida.FromFarmId != req.ToFarmId)
            throw new InvalidOperationException("La granja de recepción debe ser la granja destino del traslado.");

        var item = salida.ItemInventario;
        var (companyIdTo, paisIdTo) = await GetFarmCompanyAndPaisAsync(req.ToFarmId, ct);

        // Colombia (nivel granja): recepción de alimento sin núcleo/galpón. EC/PA sin cambios.
        // Con Distribucion (alimento por galpón) lo recibido se reparte entre varios galpones de la granja destino.
        var isAlimento = IsAlimento(item);
        var modoDestino = await ResolverModoUbicacionAsync(req.ToFarmId, ct);
        var porSilo = modoDestino == ModoUbicacionInventario.PorSilo;
        var usaUbicacion = !porSilo && isAlimento && !await EsInventarioNivelGranjaAsync(req.ToFarmId, ct);
        var (destinos, errorDistribucion) = ZooSanMarino.Application.Calculos.InventarioGestionRecepcionDistribucionCalculos.Resolver(
            req.Distribucion, req.ToNucleoId, req.ToGalponId, usaUbicacion, salida.Quantity, porSilo, req.ToSiloId);
        if (errorDistribucion != null)
            throw new InvalidOperationException(errorDistribucion);

        // Solo el camino distribuido valida pertenencia (el de una ubicación conserva su comportamiento previo).
        if (destinos.Count > 1 && usaUbicacion)
            await ValidarGalponesDeGranjaAsync(req.ToFarmId, destinos, ct);

        // Cada silo del reparto tiene que ser de la granja destino. Se valida ANTES de escribir: un
        // silo ajeno acreditaría stock en la granja equivocada y el descuadre aparecería después,
        // sin rastro de su origen.
        if (porSilo)
            foreach (var d in destinos)
                await ValidarSiloDeGranjaAsync(req.ToFarmId, d.SiloId!.Value, ct);

        if (salida.CompanyId != companyIdTo)
            throw new InvalidOperationException("La granja destino no pertenece a la misma empresa que la salida.");

        // Un asiento (stock + movimiento) por ubicación de destino: uno solo en el camino clásico,
        // N cuando la recepción se distribuye entre galpones.
        var ahora = DateTimeOffset.UtcNow;
        var stocksDestino = new List<InventarioGestionStock>(destinos.Count);
        var movimientosEntrada = new List<InventarioGestionMovimiento>(destinos.Count);
        var distribuida = destinos.Count > 1;

        // A1/A2 — toda la recepción es UNA unidad: el descuento del origen, las N acreditaciones de
        // destino y sus N movimientos. Antes, cada pata se resolvía por separado con
        // buscar-o-insertar y read-modify-write; con la recepción distribuida eso significaba que
        // dos destinos que apuntaran al MISMO galpón creaban dos filas de stock (la segunda
        // invisible), porque ninguna de las dos consultas veía la fila que la otra estaba por
        // insertar. El upsert lo resuelve acumulando.
        await EnTransaccionAsync(async () =>
        {
        // Solicitud nueva: aquí se descuenta origen. Registro antiguo (Salida): el descuento ya se hizo al enviar.
        if (string.Equals(salida.MovementType, "TrasladoInterGranjaPendiente", StringComparison.Ordinal))
        {
            var stockOrigen = await BuscarStockSinRastreoAsync(salida.FarmId, salida.ItemInventarioEcuadorId, salida.NucleoId, salida.GalponId, salida.SiloId, ct);
            if (stockOrigen == null)
                throw new InvalidOperationException("No hay stock suficiente en origen para completar la recepción (verifique disponibilidad).");
            if (!await DescontarStockAtomicoAsync(stockOrigen.Id, salida.Quantity, ct))
                throw new InvalidOperationException("No hay stock suficiente en origen para completar la recepción (verifique disponibilidad).");
            salida.MovementType = "TrasladoInterGranjaSalida";
            salida.Estado = "Tránsito";
        }

        for (var i = 0; i < destinos.Count; i++)
        {
            var destino = destinos[i];
            var stockDestino = await SumarStockAtomicoAsync(
                companyIdTo, paisIdTo, req.ToFarmId, destino.NucleoId, destino.GalponId,
                salida.ItemInventarioEcuadorId, destino.Quantity,
                // TK-2026-000019 — la unidad del catálogo, no la que traía el movimiento de salida.
                UnidadInventarioCalculos.Resolver(item.Unidad, salida.Unit), destino.SiloId, ct);
            stocksDestino.Add(stockDestino);

            var movEntrada = new InventarioGestionMovimiento
            {
                CompanyId = companyIdTo,
                PaisId = paisIdTo,
                FarmId = req.ToFarmId,
                NucleoId = destino.NucleoId,
                GalponId = destino.GalponId,
                SiloId = destino.SiloId,
                ItemInventarioEcuadorId = salida.ItemInventarioEcuadorId,
                Quantity = destino.Quantity,
                Unit = stockDestino.Unit,
                MovementType = "TrasladoInterGranjaEntrada",
                Estado = "Recibido desde tránsito",
                FromFarmId = salida.FarmId,
                FromNucleoId = salida.NucleoId,
                FromGalponId = salida.GalponId,
                FromSiloId = salida.SiloId,
                Reference = salida.Reference,
                Reason = distribuida
                    ? $"Recepción traslado inter-granja (distribución {i + 1}/{destinos.Count})"
                    : "Recepción traslado inter-granja",
                TransferGroupId = req.TransferGroupId,
                CreatedAt = ahora,
                CreatedByUserId = _current?.UserId.ToString()
            };
            _db.InventarioGestionMovimientos.Add(movEntrada);
            movimientosEntrada.Add(movEntrada);
        }

        await _db.SaveChangesAsync(ct);
        }, ct);

        // La recepción puede repartirse entre VARIOS galpones del destino: refrescar todos.
        foreach (var ubic in movimientosEntrada
                     .Select(m => (m.NucleoId, m.GalponId, m.MovementType))
                     .Distinct())
            await RefrescarSaldoAlimentoEngordeAsync(companyIdTo, req.ToFarmId, ubic.NucleoId, ubic.GalponId, ubic.MovementType, ct);

        var farmDest = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == req.ToFarmId, ct);

        string? origenNn = null;
        string? origenGn = null;
        if (salida.NucleoId != null)
            origenNn = await _db.Nucleos.AsNoTracking().Where(n => n.NucleoId == salida.NucleoId && n.GranjaId == salida.FarmId).Select(n => n.NucleoNombre).FirstOrDefaultAsync(ct);
        if (salida.GalponId != null)
            origenGn = await _db.Galpones.AsNoTracking().Where(g => g.GalponId == salida.GalponId && g.GranjaId == salida.FarmId).Select(g => g.GalponNombre).FirstOrDefaultAsync(ct);

        var dtosStock = new List<InventarioGestionStockDto>(destinos.Count);
        var dtosMov = new List<InventarioGestionMovimientoDto>(destinos.Count);

        for (var i = 0; i < destinos.Count; i++)
        {
            var destino = destinos[i];
            var stockDestino = stocksDestino[i];
            var movEntrada = movimientosEntrada[i];

            var list = await GetStockAsync(req.ToFarmId, destino.NucleoId, destino.GalponId, null, null, ct);
            dtosStock.Add(list.FirstOrDefault(x => x.ItemInventarioEcuadorId == salida.ItemInventarioEcuadorId)
                ?? new InventarioGestionStockDto(stockDestino.Id, stockDestino.FarmId, stockDestino.NucleoId, stockDestino.GalponId, stockDestino.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.Concepto ?? item.TipoItem ?? "alimento", stockDestino.Quantity, stockDestino.Unit, null, null, null, stockDestino.CreatedAt));

            string? nn = null;
            string? gn = null;
            if (destino.NucleoId != null)
                nn = await _db.Nucleos.AsNoTracking().Where(n => n.NucleoId == destino.NucleoId && n.GranjaId == req.ToFarmId).Select(n => n.NucleoNombre).FirstOrDefaultAsync(ct);
            if (destino.GalponId != null)
                gn = await _db.Galpones.AsNoTracking().Where(g => g.GalponId == destino.GalponId && g.GranjaId == req.ToFarmId).Select(g => g.GalponNombre).FirstOrDefaultAsync(ct);

            dtosMov.Add(new InventarioGestionMovimientoDto(
                movEntrada.Id,
                movEntrada.FarmId,
                movEntrada.NucleoId,
                movEntrada.GalponId,
                movEntrada.ItemInventarioEcuadorId,
                item.Codigo,
                item.Nombre,
                item.Concepto ?? item.TipoItem ?? "alimento",
                movEntrada.Quantity,
                movEntrada.Unit,
                movEntrada.MovementType,
                movEntrada.Estado,
                movEntrada.FromFarmId,
                movEntrada.FromNucleoId,
                movEntrada.FromGalponId,
                movEntrada.Reference,
                movEntrada.Reason,
                movEntrada.CreatedAt,
                farmDest?.Name,
                nn,
                gn,
                movEntrada.TransferGroupId,
                salida.Farm.Name,
                origenNn,
                origenGn,
                "Traslado entre granjas (recepción)",
                item.Concepto,
                item.TipoItem,
                movEntrada.ParaProximoCiclo,
                movEntrada.RegistradoAt));
        }

        return new InventarioGestionRecepcionTransitoResultDto(dtosStock, dtosMov);
    }

    /// <summary>
    /// Valida que cada (núcleo, galpón) de una recepción distribuida exista realmente en la granja destino.
    /// Solo se aplica al camino distribuido: el de una sola ubicación conserva su comportamiento histórico.
    /// </summary>
    private async Task ValidarGalponesDeGranjaAsync(
        int farmId,
        IReadOnlyList<ZooSanMarino.Application.Calculos.InventarioGestionRecepcionDistribucionCalculos.Destino> destinos,
        CancellationToken ct)
    {
        var galponesGranja = await _db.Galpones.AsNoTracking()
            .Where(g => g.GranjaId == farmId)
            .Select(g => new { g.GalponId, g.NucleoId })
            .ToListAsync(ct);

        foreach (var destino in destinos)
        {
            var existe = galponesGranja.Any(g =>
                string.Equals(g.GalponId, destino.GalponId, StringComparison.Ordinal) &&
                string.Equals(g.NucleoId, destino.NucleoId, StringComparison.Ordinal));
            if (!existe)
                throw new InvalidOperationException($"El galpón {destino.GalponId} no pertenece al núcleo {destino.NucleoId} de la granja destino.");
        }
    }

    public async Task RechazarTransitoPendienteAsync(InventarioGestionRechazoTransitoRequest req, CancellationToken ct = default)
    {
        var pendiente = await _db.InventarioGestionMovimientos
            .FirstOrDefaultAsync(x => x.TransferGroupId == req.TransferGroupId && x.MovementType == "TrasladoInterGranjaPendiente", ct);
        if (pendiente == null)
            throw new InvalidOperationException("No hay solicitud pendiente para rechazar (puede estar ya recibida o rechazada).");

        var yaEntrada = await _db.InventarioGestionMovimientos.AnyAsync(
            x => x.TransferGroupId == req.TransferGroupId && x.MovementType == "TrasladoInterGranjaEntrada", ct);
        if (yaEntrada)
            throw new InvalidOperationException("Este traslado ya fue recibido en destino.");

        pendiente.MovementType = "TrasladoInterGranjaRechazado";
        pendiente.Estado = "Rechazado destino";
        var extra = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
        pendiente.Reason = extra != null
            ? $"{pendiente.Reason ?? ""} | Rechazo destino: {extra}".Trim()
            : (pendiente.Reason ?? "Rechazado destino");

        // El rechazo cancela la salida, así que su fila del histórico tiene que quedar ANULADA.
        // Cambiarle el `movement_type` al movimiento NO alcanza: el trigger que llena el histórico es
        // solo AFTER INSERT, así que la fila conserva su `tipo_evento` original —
        // `TrasladoInterGranjaPendiente` mapea a INV_TRASLADO_SALIDA— y el saldo del galpón de origen
        // seguiría descontando un alimento que nunca salió.
        await AnularHistoricoDelMovimientoAsync(pendiente, ct);

        await _db.SaveChangesAsync(ct);
        await RefrescarSaldoAlimentoEngordeAsync(
            pendiente.CompanyId, pendiente.FarmId, pendiente.NucleoId, pendiente.GalponId, "TrasladoSalida", ct);
    }

    /// <summary>Valida empresa, país y granjas asignadas; carga ítem de catálogo.</summary>
    private async Task<InventarioGestionStock> GetStockForMutationAsync(int stockId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        var stock = await _db.InventarioGestionStock
            .Include(x => x.ItemInventario)
            .FirstOrDefaultAsync(x => x.Id == stockId, ct);
        if (stock == null)
            throw new InvalidOperationException("El registro de stock no existe.");
        if (stock.CompanyId != companyId.Value)
            throw new InvalidOperationException("No autorizado.");
        var effectivePais = await GetEffectivePaisIdAsync(stock.FarmId, ct);
        if (effectivePais > 0 && stock.PaisId != effectivePais)
            throw new InvalidOperationException("El registro no corresponde al país activo.");
        if (!allowedFarmIds.Contains(stock.FarmId))
            throw new InvalidOperationException("No tiene acceso a esta granja.");
        return stock;
    }

    public async Task<InventarioGestionStockDto> ActualizarStockAsync(int stockId, InventarioGestionStockUpdateRequest req, CancellationToken ct = default)
    {
        if (req.Quantity < 0)
            throw new InvalidOperationException("La cantidad no puede ser negativa.");

        var stock = await GetStockForMutationAsync(stockId, ct);
        var item = stock.ItemInventario;
        var oldQty = stock.Quantity;
        var oldUnit = stock.Unit;
        // TK-2026-000019 — la unidad DEJA de ser editable acá: la manda el catálogo del ítem. Este
        // campo era texto libre y es el que llenó la base de `LT`, `UND`, `GALONES` y `DOSIS`,
        // porque operación lo usaba para tapar el `kg` que mostraba el stock. `req.Unit` se sigue
        // aceptando en el contrato (no rompe clientes viejos) pero ya no decide nada; si la fila
        // venía torcida, este ajuste la realinea y queda escrito en el motivo.
        var newUnit = UnidadInventarioCalculos.Resolver(item.Unidad, stock.Unit);

        DateTimeOffset? newCreated = null;
        if (req.FechaIngreso.HasValue)
        {
            var d = req.FechaIngreso.Value.Date;
            newCreated = new DateTimeOffset(d.Year, d.Month, d.Day, 12, 0, 0, TimeSpan.Zero);
        }

        var qtyChanged = oldQty != req.Quantity;
        var unitChanged = !string.Equals(oldUnit.Trim(), newUnit.Trim(), StringComparison.OrdinalIgnoreCase);
        var fechaChanged = newCreated.HasValue && stock.CreatedAt.Date != newCreated.Value.Date;

        if (!qtyChanged && !unitChanged && !fechaChanged)
            throw new InvalidOperationException("No hay cambios.");

        if (newCreated.HasValue)
            stock.CreatedAt = newCreated.Value;

        stock.UpdatedAt = DateTimeOffset.UtcNow;

        if (qtyChanged || unitChanged)
        {
            var delta = req.Quantity - oldQty;
            stock.Quantity = req.Quantity;
            stock.Unit = newUnit;

            var extra = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
            var reasonFull = $"Ajuste manual. Anterior: {oldQty} {oldUnit}. Nuevo: {req.Quantity} {newUnit}.";
            if (fechaChanged && newCreated.HasValue)
                reasonFull += $" Fecha ingreso: {newCreated.Value:yyyy-MM-dd}.";
            if (extra != null)
                reasonFull += $" Motivo: {extra}";

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = stock.CompanyId,
                PaisId = stock.PaisId,
                FarmId = stock.FarmId,
                NucleoId = stock.NucleoId,
                GalponId = stock.GalponId,
                ItemInventarioEcuadorId = stock.ItemInventarioEcuadorId,
                Quantity = delta != 0 ? Math.Abs(delta) : 0m,
                Unit = newUnit,
                MovementType = "AjusteStock",
                Estado = "Ajuste manual",
                Reference = null,
                Reason = reasonFull,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = _current?.UserId.ToString()
            });
        }

        await _db.SaveChangesAsync(ct);

        var list = await GetStockAsync(stock.FarmId, stock.NucleoId, stock.GalponId, null, null, ct);
        return list.FirstOrDefault(x => x.Id == stockId)
            ?? new InventarioGestionStockDto(
                stock.Id, stock.FarmId, stock.NucleoId, stock.GalponId, stock.ItemInventarioEcuadorId,
                item.Codigo, item.Nombre, item.Concepto ?? item.TipoItem ?? "alimento",
                stock.Quantity, stock.Unit, null, null, null, stock.CreatedAt);
    }

    public async Task EliminarStockAsync(int stockId, CancellationToken ct = default)
    {
        var stock = await GetStockForMutationAsync(stockId, ct);
        if (stock.Quantity > 0)
        {
            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = stock.CompanyId,
                PaisId = stock.PaisId,
                FarmId = stock.FarmId,
                NucleoId = stock.NucleoId,
                GalponId = stock.GalponId,
                ItemInventarioEcuadorId = stock.ItemInventarioEcuadorId,
                Quantity = stock.Quantity,
                // La unidad del catálogo (TK-2026-000019): `GetStockForMutationAsync` trae el ítem.
                Unit = UnidadInventarioCalculos.Resolver(stock.ItemInventario?.Unidad, stock.Unit),
                MovementType = "EliminacionStock",
                Estado = "Eliminación registro",
                Reference = null,
                Reason = "Eliminación del registro de stock desde gestión de inventario.",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = _current?.UserId.ToString()
            });
        }

        _db.InventarioGestionStock.Remove(stock);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AnularMovimientoHistoricoAsync(int movimientoId, string? motivo, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        var mov = await _db.InventarioGestionMovimientos
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);
        if (mov == null)
            throw new InvalidOperationException("El movimiento no existe o no pertenece a su empresa.");
        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a la granja de este movimiento.");

        var mt = (mov.MovementType ?? "").Trim();
        if (!string.Equals(mt, "Consumo", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mt, "Ingreso", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Solo se pueden anular movimientos de tipo Consumo o Ingreso. Use los flujos de traslado/tránsito para corregir otros casos.");

        // A1/A2 — la reversión del stock, la anulación del histórico y el borrado del movimiento
        // van juntos. Revertir el stock y fallar al anular el histórico dejaría contado un ingreso
        // que ya salió del stock: kilos que la tabla diaria muestra y que no existen.
        await EnTransaccionAsync(async () =>
        {
            if (string.Equals(mt, "Consumo", StringComparison.OrdinalIgnoreCase))
            {
                // Anular un consumo DEVUELVE stock: es una suma, con la misma carrera que un ingreso.
                var (cId, pId) = await GetFarmCompanyAndPaisAsync(mov.FarmId, ct);
                // TK-2026-000019 — al devolver el stock, la unidad la fija el catálogo del ítem;
                // el movimiento anulado puede traer una unidad vieja.
                var unidadCatalogo = await _db.ItemInventario.AsNoTracking()
                    .Where(i => i.Id == mov.ItemInventarioEcuadorId)
                    .Select(i => i.Unidad)
                    .FirstOrDefaultAsync(ct);
                await SumarStockAtomicoAsync(
                    cId, pId, mov.FarmId, mov.NucleoId, mov.GalponId,
                    mov.ItemInventarioEcuadorId, mov.Quantity,
                    UnidadInventarioCalculos.Resolver(unidadCatalogo, mov.Unit), mov.SiloId, ct);
            }
            else
            {
                // Anular un ingreso RESTA stock: si otro movimiento ya lo consumió, no se puede anular.
                var stock = await BuscarStockSinRastreoAsync(mov.FarmId, mov.ItemInventarioEcuadorId, mov.NucleoId, mov.GalponId, mov.SiloId, ct);
                if (stock == null || !await DescontarStockAtomicoAsync(stock.Id, mov.Quantity, ct))
                    throw new InvalidOperationException(
                        "No se puede anular este ingreso: no hay stock suficiente en la ubicación para revertir la cantidad.");
            }

            // El movimiento se borra, así que su fila del histórico tiene que quedar ANULADA o se
            // convierte en huérfana: el saldo de alimento seguiría contando un ingreso que ya salió del
            // stock, y la tabla diaria mostraría kilos que no existen. Misma convención de auditoría que
            // EliminarIngresoAsync y EliminarTrasladoAsync (marcar, no borrar).
            await AnularHistoricoDelMovimientoAsync(mov, ct);

            _db.InventarioGestionMovimientos.Remove(mov);
            await _db.SaveChangesAsync(ct);
        }, ct);
        await RefrescarSaldoAlimentoEngordeAsync(mov.CompanyId, mov.FarmId, mov.NucleoId, mov.GalponId, mov.MovementType, ct);
    }

    /// <summary>
    /// Marca como anulada la fila del histórico unificado que refleja un movimiento de inventario.
    /// <para>
    /// El histórico lo escribe el trigger <c>trg_inventario_gestion_movimiento_lote_hist</c>, que es
    /// <b>solo AFTER INSERT</b>: nada propaga los UPDATE ni los DELETE del movimiento. Cada camino que
    /// deshace un movimiento tiene que anular su fila a mano o el saldo de alimento se separa del stock.
    /// </para>
    /// <para>
    /// Busca por la clave del histórico (<c>origen_tabla</c> + <c>origen_id</c>, única) y cae a un
    /// fallback por ubicación + ítem + cantidad, igual que <c>EliminarIngresoAsync</c>: hay filas
    /// antiguas cargadas antes de que existiera esa clave.
    /// </para>
    /// </summary>
    /// <summary>
    /// Evalúa si el movimiento quedó fechado FUERA del ciclo vigente del galpón y devuelve el aviso a
    /// mostrar, o <c>null</c> si la fecha es normal. <b>Avisa, no bloquea:</b> retrofechar es legítimo
    /// —la operación a veces registra el lunes lo que llegó el viernes— y bloquearlo tendría un costo
    /// real. Lo que no puede pasar es que lo haga sin enterarse.
    /// <para>
    /// Ver <see cref="AvisoFechaFueraDeCicloCalculos"/> para el caso que lo originó.
    /// </para>
    /// </summary>
    private async Task<string?> EvaluarAvisoFechaFueraDeCicloAsync(
        int companyId, int farmId, string? nucleoId, string? galponId, DateTimeOffset fechaMovimiento,
        CancellationToken ct)
    {
        var nucleo = (nucleoId ?? "").Trim();
        var galpon = (galponId ?? "").Trim();
        if (galpon.Length == 0)
            return null;   // nivel granja: no pertenece al ciclo de ningún galpón

        var ciclos = await _db.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId
                     && l.DeletedAt == null
                     && l.GranjaId == farmId
                     && (l.NucleoId == null ? "" : l.NucleoId.Trim()) == nucleo
                     && (l.GalponId == null ? "" : l.GalponId.Trim()) == galpon
                     && l.LoteAveEngordeId != null)
            .Select(l => new
            {
                l.LoteAveEngordeId,
                l.LoteNombre,
                SegMin = _db.SeguimientoDiarioAvesEngorde
                    .Where(s => s.LoteAveEngordeId == l.LoteAveEngordeId).Min(s => (DateTime?)s.Fecha),
                SegMax = _db.SeguimientoDiarioAvesEngorde
                    .Where(s => s.LoteAveEngordeId == l.LoteAveEngordeId).Max(s => (DateTime?)s.Fecha)
            })
            .ToListAsync(ct);

        var diasPrevios = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => (int?)c.DiasAlimentoPrevioEncaset)
            .FirstOrDefaultAsync(ct);

        return AvisoFechaFueraDeCicloCalculos.Evaluar(
            fechaMovimiento.UtcDateTime.Date,
            ciclos.Where(c => c.SegMin.HasValue && c.SegMax.HasValue)
                  .Select(c => new CicloGalpon(
                      c.LoteAveEngordeId!.Value,
                      c.LoteNombre ?? $"#{c.LoteAveEngordeId}",
                      c.SegMin!.Value,
                      c.SegMax!.Value)),
            diasPrevios ?? 10);
    }

    private async Task AnularHistoricoDelMovimientoAsync(InventarioGestionMovimiento mov, CancellationToken ct)
    {
        var hist = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h => h.OrigenTabla == "inventario_gestion_movimiento"
                                   && h.OrigenId == mov.Id, ct);
        hist ??= await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h =>
                h.FarmId == mov.FarmId &&
                h.NucleoId == mov.NucleoId &&
                h.GalponId == mov.GalponId &&
                h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                h.CantidadKg == mov.Quantity &&
                !h.Anulado, ct);

        if (hist != null)
            hist.Anulado = true;
    }

    public async Task<InventarioGestionStockDto> RegistrarConsumoAsync(InventarioGestionConsumoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad de consumo debe ser positiva.");

        // La resolución de la ubicación vive en un solo lugar porque `ValidarStockConsumoAsync` la
        // necesita IDÉNTICA: si la validación previa buscara el stock en otra clave que el descuento,
        // aprobaría un consumo que después falla —o al revés— y volveríamos a tener dos verdades
        // sobre el mismo número.
        var (item, nucleoId, galponId, siloId) = await ResolverUbicacionConsumoAsync(req, ct);
        req = AjustarUbicacionRequest(req, item);

        // A2 — descuento ATÓMICO. Antes esto era read-modify-write:
        //     if (stock.Quantity < req.Quantity) throw;  stock.Quantity -= req.Quantity;
        // Dos consumos de 100 sobre un stock de 150 pasaban LOS DOS la validación y el saldo
        // terminaba en -50: se despachaba alimento que no existía. Ahora la condición viaja
        // DENTRO del UPDATE, así que el segundo consumo ve el saldo ya descontado.
        // La lectura es AsNoTracking() a propósito: una copia rastreada con la cantidad vieja
        // haría que el SaveChanges de abajo pisara el descuento.
        var stock = await BuscarStockSinRastreoAsync(req.FarmId, req.ItemInventarioEcuadorId, nucleoId, galponId, siloId, ct);
        if (stock == null)
            throw new StockInsuficienteException(StockAtomicoCalculos.MensajeStockInsuficiente);

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);
        if (_current?.CompanyId > 0 && _current.CompanyId != companyId)
            throw new InvalidOperationException("La granja no pertenece a su empresa.");

        // El descuento y el movimiento que lo explica van juntos o no van: si el movimiento
        // fallara después del UPDATE, el stock bajaría sin ningún registro que lo justifique.
        await EnTransaccionAsync(async () =>
        {
            if (!await DescontarStockAtomicoAsync(stock.Id, req.Quantity, ct))
                throw new StockInsuficienteException(StockAtomicoCalculos.MensajeStockInsuficiente);

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = companyId,
                PaisId = paisId,
                FarmId = req.FarmId,
                NucleoId = nucleoId,
                GalponId = galponId,
                SiloId = siloId,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                Quantity = req.Quantity,
                // TK-2026-000019 — la del catálogo. Con `req.Unit ?? "kg"`, todo consumo disparado
                // por un seguimiento (que no manda unidad) quedaba en kilos.
                Unit = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit),
                MovementType = "Consumo",
                Estado = "Consumo",
                Reference = req.Reference?.Trim(),
                Reason = req.Reason?.Trim(),
                // Simetría con RegistrarIngresoAsync: sin fecha explícita se usa "ahora" (comportamiento
                // histórico); con fecha, el movimiento queda en el día real del consumo. Ancla a las
                // 18:00 (no a las 12:00 del ingreso) para no empatar el orden intra-día — ver F2.
                CreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento, FechaMovimientoSeguimientoCalculos.AnclaConsumoUtc),
                CreatedByUserId = _current?.UserId.ToString()
            });
            await _db.SaveChangesAsync(ct);
        }, ct);

        var list = (await GetStockAsync(req.FarmId, nucleoId, galponId, null, null, ct))
            .Where(x => x.SiloId == siloId).ToList();
        return list.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId)
            // `stock` se leyó con AsNoTracking ANTES del descuento, así que su Quantity es la
            // anterior: el DTO de respaldo tiene que restar la cantidad consumida a mano. Antes
            // esto salía solo porque la entidad rastreada ya venía decrementada en memoria.
            ?? new InventarioGestionStockDto(stock.Id, stock.FarmId, stock.NucleoId, stock.GalponId, stock.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stock.Quantity - req.Quantity, stock.Unit, null, null, null, stock.CreatedAt, null, stock.SiloId);
    }

    // ─── Fase 3 (paso 2) — consumo/devolución a NIVEL GRANJA (Colombia) ─────────────────────
    // El stock Colombia migrado a modelo B vive a nivel granja (nucleo_id/galpon_id = NULL), a
    // diferencia de Ecuador/Panamá (alimento exige núcleo+galpón). Estos métodos son ADITIVOS: NO
    // cambian el comportamiento de RegistrarConsumoAsync/RegistrarIngresoAsync (que EXIGEN
    // núcleo+galpón para alimento). Descuentan/reponen SIEMPRE contra el stock (farm, item,
    // nucleo=NULL, galpon=NULL), sin exigir galpón, y NO abren transacción propia: participan de la
    // IDbContextTransaction externa que abre el servicio de seguimiento (levante/producción), igual
    // que FarmInventoryConsumoService. Mantienen la validación de stock (si insuficiente → throw)
    // para respetar el bloqueo atómico. Movimientos con MovementType 'Consumo'/'Ingreso' (como hoy
    // Ecuador) y NucleoId/GalponId = NULL, aislados por company+pais de la granja.

    /// <summary>
    /// La fila de stock de nivel granja (nucleo/galpon NULL) de un ítem, discriminando por silo.
    ///
    /// <para>
    /// El silo se filtra SIEMPRE, también cuando es <c>null</c> (<c>silo_id IS NULL</c>): es la misma
    /// clave natural que fija el índice único <c>ux_inventario_gestion_stock_clave_natural</c> con su
    /// <c>COALESCE(silo_id,0)</c>. Para las empresas sin el flag —donde <c>silo_id</c> es NULL en el
    /// 100 % de las filas— la consulta devuelve exactamente lo mismo que antes de la Fase C; para las
    /// que ubican por silo, sin este filtro el consumo descontaría la primera fila que encuentre, que
    /// puede ser la de OTRO silo.
    /// </para>
    /// </summary>
    private IQueryable<InventarioGestionStock> StockNivelGranjaQuery(int farmId, int itemId, int? siloId)
    {
        var q = _db.InventarioGestionStock
            .Where(x => x.FarmId == farmId && x.ItemInventarioEcuadorId == itemId && x.NucleoId == null && x.GalponId == null);

        return siloId.HasValue
            ? q.Where(x => x.SiloId == siloId.Value)
            : q.Where(x => x.SiloId == null);
    }

    /// <summary>
    /// Fase 3 — consumo a nivel granja (Colombia): descuenta <c>inventario_gestion_stock</c> por
    /// (farm, item, nucleo=NULL, galpon=NULL) e inserta un movimiento <c>Consumo</c> sin ubicación
    /// estructurada. Lanza si no hay stock suficiente (bloqueo). No mueve nada de Ecuador/Panamá.
    ///
    /// <para>
    /// F4 (22-ago-2026): antes esto era <c>read-modify-write</c> sobre una fila RASTREADA
    /// (<c>stock.Quantity -= req.Quantity</c>, sin <c>SaveChanges</c> propio — el orquestador externo
    /// commiteaba todo junto). Sin concurrency token en la tabla, dos consumos concurrentes sobre la
    /// misma granja+ítem pasaban <b>los dos</b> la validación y el <c>UPDATE</c> final de EF escribía
    /// el absoluto en memoria: pérdida DETERMINISTA, no una carrera rara. Y el stock a nivel granja es
    /// UNO por (granja, ítem) compartido por TODOS los lotes de la granja — N tablets de la misma
    /// granja recuperando señal a la vez es el peor caso posible.
    /// </para>
    ///
    /// <para>
    /// Ahora adopta la forma que Ecuador/Panamá ya tiene al lado: lectura SIN rastreo
    /// (<see cref="BuscarStockSinRastreoAsync"/>) + descuento en una sola sentencia condicional
    /// (<see cref="DescontarStockAtomicoAsync"/>, <c>UPDATE ... WHERE quantity &gt;= @q</c>) + el
    /// movimiento, TODO dentro de <see cref="EnTransaccionAsync"/> (abre transacción sólo si no hay
    /// una ambiente — el mismo patrón que ya usa <c>RegistrarConsumoAsync</c>). Por eso este método SÍ
    /// llama su propio <c>SaveChangesAsync</c> ahora: los llamadores (levante/engorde/producción)
    /// persisten el seguimiento ANTES de invocar este camino, así que no hay nada ajeno que arrastrar.
    /// </para>
    /// </summary>
    public async Task RegistrarConsumoNivelGranjaAsync(InventarioGestionConsumoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad de consumo debe ser positiva.");
        var item = await _db.ItemInventario.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");

        // Lectura SIN rastreo: es parte del contrato de DescontarStockAtomicoAsync. Una copia
        // rastreada con la cantidad vieja haría que un SaveChanges posterior pisara el descuento.
        var stock = await BuscarStockSinRastreoAsync(req.FarmId, req.ItemInventarioEcuadorId, null, null, req.SiloId, ct);
        if (stock == null || stock.Quantity < req.Quantity)
            throw new StockInsuficienteException(StockAtomicoCalculos.MensajeStockInsuficienteNivelGranja(
                item.Codigo, item.Nombre, req.FarmId, stock?.Quantity ?? 0m, req.Quantity));

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);

        // El descuento y el movimiento que lo explica van juntos o no van: si el movimiento fallara
        // después del UPDATE, el stock bajaría sin ningún registro que lo justifique.
        await EnTransaccionAsync(async () =>
        {
            if (!await DescontarStockAtomicoAsync(stock.Id, req.Quantity, ct))
                // Rama de la carrera: la pre-lectura alcanzaba, pero otra transacción se llevó el
                // saldo antes que ésta. Mismo mensaje con nombre e ítem, no el genérico de EC/PA —
                // así el reporte de la carga masiva sigue diciendo qué faltó y dónde.
                throw new StockInsuficienteException(StockAtomicoCalculos.MensajeStockInsuficienteNivelGranja(
                    item.Codigo, item.Nombre, req.FarmId, stock.Quantity, req.Quantity));

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = companyId,
                PaisId = paisId,
                FarmId = req.FarmId,
                NucleoId = null,
                GalponId = null,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                // El silo del consumo (Fase C). Null en toda empresa sin el flag ⇒ movimiento idéntico
                // al de siempre; con silo, el kardex dice de qué silo salió el alimento.
                SiloId = req.SiloId,
                Quantity = req.Quantity,
                // TK-2026-000019 — la del catálogo (Colombia manda "kg" fijo en el request).
                Unit = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit),
                MovementType = "Consumo",
                Estado = "Consumo",
                Reference = req.Reference?.Trim(),
                Reason = req.Reason?.Trim(),
                // Simetría con RegistrarConsumoAsync: sin fecha explícita se usa "ahora" (lo que hacen
                // todos los llamadores históricos, así que su comportamiento no cambia); con fecha, el
                // movimiento queda en el día real del consumo — lo necesita la carga masiva, cuya
                // idempotencia se apoya en la fecha del movimiento. Ancla a las 18:00, no a las 12:00
                // del ingreso, para no empatar el orden intra-día — ver F2.
                CreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento, FechaMovimientoSeguimientoCalculos.AnclaConsumoUtc),
                CreatedByUserId = _current?.UserId.ToString()
            });
            await _db.SaveChangesAsync(ct);
        }, ct);
    }

    /// <summary>
    /// Fase 3 — devolución a nivel granja (Colombia): repone <c>inventario_gestion_stock</c> por
    /// (farm, item, nucleo=NULL, galpon=NULL) e inserta un movimiento <c>Ingreso</c>. Crea el stock
    /// si no existe.
    ///
    /// <para>
    /// F4 (22-ago-2026), trampa #1 del plan: si esto se quedaba <c>read-modify-write</c> RASTREADO
    /// mientras <see cref="RegistrarConsumoNivelGranjaAsync"/> pasaba a SQL crudo, un
    /// <c>SaveChangesAsync</c> de ESTE método —dentro de la MISMA unidad de trabajo, por ejemplo
    /// <c>AplicarDiffAsync</c> resolviendo dos <c>ItemConsumoKey</c> distintas al mismo
    /// <c>itemBId</c>— escribiría el absoluto de esta fila rastreada y <b>pisaría</b> el descuento
    /// atómico del otro ítem sobre la misma fila. Régimen mixto = footgun documentado en
    /// <c>StockAtomico.cs:44-48</c>. Ahora usa <see cref="SumarStockAtomicoAsync"/> — el mismo
    /// <c>INSERT ... ON CONFLICT ... DO UPDATE</c> que ya usan ingreso y traslados de Ecuador/Panamá—,
    /// así que dos operaciones sobre la misma fila, vengan de donde vengan, se serializan en la base.
    /// </para>
    /// </summary>
    public async Task RegistrarIngresoNivelGranjaAsync(InventarioGestionIngresoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
        var item = await _db.ItemInventario.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);
        var unidad = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit);

        GuardarMarcaProximoCicloApagada(req.ParaProximoCiclo);

        // El ingreso y el movimiento que lo explica van juntos o no van, igual que en el consumo.
        await EnTransaccionAsync(async () =>
        {
            await SumarStockAtomicoAsync(
                companyId, paisId, req.FarmId, null, null,
                req.ItemInventarioEcuadorId, req.Quantity, unidad, req.SiloId, ct);

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = companyId,
                PaisId = paisId,
                FarmId = req.FarmId,
                NucleoId = null,
                GalponId = null,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                SiloId = req.SiloId,
                Quantity = req.Quantity,
                // TK-2026-000019 — la del catálogo, igual que el consumo de nivel granja.
                Unit = unidad,
                MovementType = "Ingreso",
                Estado = "Ingreso",
                Reference = req.Reference?.Trim(),
                Reason = req.Reason?.Trim(),
                // F2.2 (22-ago-2026): antes hardcodeaba UtcNow aunque `req.FechaMovimiento` ya existía en
                // el DTO — una edición devolvía el ajuste positivo al día del seguimiento y su devolución
                // quedaba en HOY: los dos lados del mismo diff en días distintos. Mismo criterio que
                // RegistrarConsumoNivelGranjaAsync, con la ancla de INGRESO (12:00), no la de consumo.
                CreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento),
                CreatedByUserId = _current?.UserId.ToString(),
                // Mismo criterio que RegistrarIngresoAsync: la marca viaja en el request y el instante de
                // captura se guarda aparte. Sin esto, todo lo que entra por Colombia quedaría marcado
                // como «fila anterior a la columna» para siempre.
                ParaProximoCiclo = req.ParaProximoCiclo,
                RegistradoAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }, ct);
    }

    /// <summary>
    /// Guarda de servidor de la marca «para el próximo ciclo» (v16a, 18-ago-2026, FASE A del plan
    /// <c>fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md</c>).
    /// <para>
    /// La feature está APAGADA hasta que entre la atribución persistida (Fase B). Hasta hoy el apagado
    /// era sólo del front (<c>mostrarParaProximoCicloIngreso</c> devuelve <c>false</c> y
    /// <c>puedeMarcarDestinoCiclo</c> exige que la marca ya esté puesta), así que Swagger, la PWA, la
    /// carga masiva o un script podían volver a ponerla. Medido sobre el dump local con la v15 que
    /// corre en producción: marcar los 2.371 movimientos de alimento reales deja 24 filas de la tabla
    /// diaria SIN NINGUNA pantalla, 1.733 filas con saldo distinto (peor caso 193.701,7 kg), lleva las
    /// filas en negativo de 97 a 1.160 y el cuadre de 8 a 58 galpones descuadrados.
    /// </para>
    /// <para>
    /// QUITAR una marca existente sigue permitido a propósito: R3 dice que los kilos nunca pueden
    /// quedar sin poder corregirse. Por eso la guarda mira sólo el valor que se quiere ESCRIBIR.
    /// </para>
    /// </summary>
    private static void GuardarMarcaProximoCicloApagada(bool paraProximoCiclo)
    {
        if (!paraProximoCiclo) return;
        throw new InvalidOperationException(
            "La marca «para el próximo ciclo» está deshabilitada mientras se rediseña la atribución "
            + "del alimento entre ciclos: hoy dejaría kilos reales fuera de toda tabla diaria. "
            + "Registre el ingreso con su fecha real; quitar una marca ya existente sigue permitido.");
    }

    private static void ApplyUbicacionMovimientoFilter(
        ref IQueryable<InventarioGestionMovimiento> query,
        string? nucleoId,
        string? galponId)
    {
        if (string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(x => x.NucleoId == null || x.NucleoId == "");
        else
        {
            var n = nucleoId.Trim();
            query = query.Where(x => x.NucleoId == n);
        }

        if (string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == null || x.GalponId == "");
        else
        {
            var g = galponId.Trim();
            query = query.Where(x => x.GalponId == g);
        }
    }

    public async Task<List<InventarioGestionMovimientoDto>> GetMovimientosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? estado = null,
        string? movementType = null,
        string? nucleoId = null,
        string? galponId = null,
        int? loteId = null,
        string? search = null,
        string? concepto = null,
        string? tipoItem = null,
        string? tipoOperacion = null,
        string? unit = null,
        string? referenceContains = null,
        string? reasonContains = null,
        string? transferGroupId = null,
        int? itemInventarioEcuadorId = null,
        int? fromFarmId = null,
        string? fromNucleoId = null,
        string? fromGalponId = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionMovimientoDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionMovimientoDto>();

        if (fromFarmId.HasValue && !allowedFarmIds.Contains(fromFarmId.Value))
            return new List<InventarioGestionMovimientoDto>();

        if (!fromFarmId.HasValue && (!string.IsNullOrWhiteSpace(fromNucleoId) || !string.IsNullOrWhiteSpace(fromGalponId)))
            return new List<InventarioGestionMovimientoDto>();

        int? farmFilter = farmId;
        string? nucleoFilter = string.IsNullOrWhiteSpace(nucleoId) ? null : nucleoId.Trim();
        string? galponFilter = string.IsNullOrWhiteSpace(galponId) ? null : galponId.Trim();

        if (loteId.HasValue && loteId.Value > 0)
        {
            var lote = await _db.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteId.Value && l.CompanyId == companyId.Value && l.DeletedAt == null, ct);
            if (lote == null || !allowedFarmIds.Contains(lote.GranjaId))
                return new List<InventarioGestionMovimientoDto>();

            farmFilter = lote.GranjaId;
            nucleoFilter = string.IsNullOrWhiteSpace(lote.NucleoId) ? null : lote.NucleoId.Trim();
            galponFilter = string.IsNullOrWhiteSpace(lote.GalponId) ? null : lote.GalponId.Trim();
        }

        if (farmFilter.HasValue && !allowedFarmIds.Contains(farmFilter.Value))
            return new List<InventarioGestionMovimientoDto>();

        var ubicacionPorLote = loteId.HasValue && loteId.Value > 0;
        if (!ubicacionPorLote && !farmFilter.HasValue &&
            (!string.IsNullOrWhiteSpace(nucleoFilter) || !string.IsNullOrWhiteSpace(galponFilter)))
            return new List<InventarioGestionMovimientoDto>();

        var paisId = farmFilter.HasValue ? await GetEffectivePaisIdAsync(farmFilter, ct) : 0;

        var query = _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value && allowedFarmIds.Contains(x.FarmId));
        if (paisId > 0) query = query.Where(x => x.PaisId == paisId);
        if (farmFilter.HasValue) query = query.Where(x => x.FarmId == farmFilter.Value);

        if (loteId.HasValue && loteId.Value > 0)
            ApplyUbicacionMovimientoFilter(ref query, nucleoFilter, galponFilter);
        else
        {
            if (!string.IsNullOrWhiteSpace(nucleoFilter))
                query = query.Where(x => x.NucleoId == nucleoFilter);
            if (!string.IsNullOrWhiteSpace(galponFilter))
                query = query.Where(x => x.GalponId == galponFilter);
        }

        if (fechaDesde.HasValue)
        {
            var start = fechaDesde.Value.Date;
            query = query.Where(x => x.CreatedAt >= start);
        }

        if (fechaHasta.HasValue)
        {
            var end = fechaHasta.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < end);
        }

        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(x => x.Estado != null && x.Estado.Trim() == estado.Trim());
        if (!string.IsNullOrWhiteSpace(movementType)) query = query.Where(x => x.MovementType == movementType.Trim());

        if (!string.IsNullOrWhiteSpace(tipoOperacion))
        {
            var resolved = ResolveMovementTypeFromTipoOperacionLabel(tipoOperacion.Trim());
            if (resolved != null)
                query = query.Where(x => x.MovementType == resolved);
            else
                return new List<InventarioGestionMovimientoDto>();
        }

        if (!string.IsNullOrWhiteSpace(concepto))
        {
            var c = concepto.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.ItemInventario.Concepto != null &&
                x.ItemInventario.Concepto.Trim().ToLower() == c);
        }

        if (!string.IsNullOrWhiteSpace(tipoItem))
        {
            var t = tipoItem.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.ItemInventario.TipoItem != null &&
                x.ItemInventario.TipoItem.Trim().ToLower() == t);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventario.Nombre ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(unit))
        {
            var u = unit.Trim().ToLowerInvariant();
            query = query.Where(x => x.Unit != null && x.Unit.Trim().ToLower() == u);
        }

        if (itemInventarioEcuadorId.HasValue && itemInventarioEcuadorId.Value > 0)
            query = query.Where(x => x.ItemInventarioEcuadorId == itemInventarioEcuadorId.Value);

        if (!string.IsNullOrWhiteSpace(referenceContains))
        {
            var r = referenceContains.Trim().ToLowerInvariant();
            query = query.Where(x => x.Reference != null && x.Reference.ToLower().Contains(r));
        }

        if (!string.IsNullOrWhiteSpace(reasonContains))
        {
            var r = reasonContains.Trim().ToLowerInvariant();
            query = query.Where(x => x.Reason != null && x.Reason.ToLower().Contains(r));
        }

        if (!string.IsNullOrWhiteSpace(transferGroupId))
        {
            var tg = transferGroupId.Trim();
            if (!Guid.TryParse(tg, out var gid))
                return new List<InventarioGestionMovimientoDto>();
            query = query.Where(x => x.TransferGroupId == gid);
        }

        if (fromFarmId.HasValue)
        {
            query = query.Where(x => x.FromFarmId == fromFarmId.Value);
            if (!string.IsNullOrWhiteSpace(fromNucleoId))
            {
                var fn = fromNucleoId.Trim();
                query = query.Where(x => x.FromNucleoId == fn);
            }

            if (!string.IsNullOrWhiteSpace(fromGalponId))
            {
                var fg = fromGalponId.Trim();
                query = query.Where(x => x.FromGalponId == fg);
            }
        }

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(3000)
            .ToListAsync(ct);

        var nucleos = await _db.Nucleos.AsNoTracking().Where(n => list.Select(x => x.NucleoId).Contains(n.NucleoId)).ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct);
        var galpones = await _db.Galpones.AsNoTracking().Where(g => list.Select(x => x.GalponId).Contains(g.GalponId)).ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct);
        var silosMov = await NombresDeSilosAsync(list.SelectMany(x => new[] { x.SiloId, x.FromSiloId }), ct);

        var fromFarmIds = list.Where(x => x.FromFarmId.HasValue).Select(x => x.FromFarmId!.Value).Distinct().ToList();
        var fromFarmNames = fromFarmIds.Count > 0
            ? await _db.Farms.AsNoTracking().Where(f => fromFarmIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct)
            : new Dictionary<int, string>();

        var fromNucleoIds = list.Where(x => x.FromFarmId.HasValue && !string.IsNullOrWhiteSpace(x.FromNucleoId)).Select(x => x.FromNucleoId!).Distinct().ToList();
        var fromNucleoRows = fromFarmIds.Count > 0 && fromNucleoIds.Count > 0
            ? await _db.Nucleos.AsNoTracking().Where(n => fromFarmIds.Contains(n.GranjaId) && fromNucleoIds.Contains(n.NucleoId)).ToListAsync(ct)
            : new List<Nucleo>();
        var fromNucleoDict = fromNucleoRows.ToDictionary(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre);

        var fromGalponIds = list.Where(x => x.FromFarmId.HasValue && !string.IsNullOrWhiteSpace(x.FromGalponId)).Select(x => x.FromGalponId!).Distinct().ToList();
        var fromGalponRows = fromFarmIds.Count > 0 && fromGalponIds.Count > 0
            ? await _db.Galpones.AsNoTracking().Where(g => fromFarmIds.Contains(g.GranjaId) && fromGalponIds.Contains(g.GalponId)).ToListAsync(ct)
            : new List<Galpon>();
        var fromGalponDict = fromGalponRows.ToDictionary(g => (g.GalponId, g.GranjaId), g => g.GalponNombre);

        return list.Select(x =>
        {
            var estadoDisplay = x.Estado;
            if (string.IsNullOrWhiteSpace(estadoDisplay))
            {
                estadoDisplay = x.MovementType switch
                {
                    "Ingreso" => (x.Reason != null && x.Reason.Contains("Llegada a planta", StringComparison.OrdinalIgnoreCase)) ? "Entrada planta" : "Entrada granja",
                    "TrasladoEntrada" or "TrasladoSalida" => "Transferencia a granja",
                    "TrasladoInterGranjaPendiente" => "Pendiente destino",
                    "TrasladoInterGranjaSalida" => "Tránsito",
                    "TrasladoInterGranjaEntrada" => "Recibido desde tránsito",
                    "TrasladoInterGranjaRechazado" => "Rechazado destino",
                    "Consumo" => "Consumo",
                    "AjusteStock" => "Ajuste manual",
                    "EliminacionStock" => "Eliminación registro",
                    _ => x.MovementType
                };
            }
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;
            var itemType = x.ItemInventario.Concepto ?? x.ItemInventario.TipoItem ?? "alimento";

            string? fromGranjaNombre = null;
            string? fromNucleoNombre = null;
            string? fromGalponNombre = null;
            if (x.FromFarmId.HasValue && fromFarmNames.TryGetValue(x.FromFarmId.Value, out var fName))
                fromGranjaNombre = fName;
            if (x.FromFarmId.HasValue && !string.IsNullOrWhiteSpace(x.FromNucleoId) && fromNucleoDict.TryGetValue((x.FromNucleoId, x.FromFarmId.Value), out var fnn))
                fromNucleoNombre = fnn;
            if (x.FromFarmId.HasValue && !string.IsNullOrWhiteSpace(x.FromGalponId) && fromGalponDict.TryGetValue((x.FromGalponId, x.FromFarmId.Value), out var fgn))
                fromGalponNombre = fgn;

            var tipoOp = MapTipoOperacionLabel(x.MovementType);

            return new InventarioGestionMovimientoDto(
                x.Id, x.FarmId, x.NucleoId, x.GalponId, x.ItemInventarioEcuadorId,
                x.ItemInventario.Codigo, x.ItemInventario.Nombre, itemType,
                x.Quantity, x.Unit, x.MovementType, estadoDisplay,
                x.FromFarmId, x.FromNucleoId, x.FromGalponId,
                x.Reference, x.Reason, x.CreatedAt,
                x.Farm.Name, nucleoNombre, galponNombre,
                x.TransferGroupId,
                fromGranjaNombre,
                fromNucleoNombre,
                fromGalponNombre,
                tipoOp,
                x.ItemInventario.Concepto,
                x.ItemInventario.TipoItem,
                x.ParaProximoCiclo,
                x.RegistradoAt,
                x.SiloId,
                x.SiloId.HasValue && silosMov.TryGetValue(x.SiloId.Value, out var sn) ? sn : null,
                x.FromSiloId,
                x.FromSiloId.HasValue && silosMov.TryGetValue(x.FromSiloId.Value, out var fsn) ? fsn : null);
        }).ToList();
    }

    private static string MapTipoOperacionLabel(string movementType) => movementType switch
    {
        "Ingreso" => "Ingreso",
        "Consumo" => "Consumo",
        "TrasladoSalida" => "Traslado (salida entre galpones)",
        "TrasladoEntrada" => "Traslado (entrada entre galpones)",
        "TrasladoInterGranjaPendiente" => "Traslado entre granjas (solicitud pendiente)",
        "TrasladoInterGranjaSalida" => "Traslado entre granjas (en tránsito)",
        "TrasladoInterGranjaEntrada" => "Traslado entre granjas (recepción)",
        "TrasladoInterGranjaRechazado" => "Traslado entre granjas (rechazado)",
        "AjusteStock" => "Ajuste manual de stock",
        "EliminacionStock" => "Eliminación de registro de stock",
        _ => movementType
    };

    /// <summary>Inverso de <see cref="MapTipoOperacionLabel"/> para filtro por etiqueta.</summary>
    private static string? ResolveMovementTypeFromTipoOperacionLabel(string label) => label switch
    {
        "Ingreso" => "Ingreso",
        "Consumo" => "Consumo",
        "Traslado (salida entre galpones)" => "TrasladoSalida",
        "Traslado (entrada entre galpones)" => "TrasladoEntrada",
        "Traslado entre granjas (solicitud pendiente)" => "TrasladoInterGranjaPendiente",
        "Traslado entre granjas (en tránsito)" => "TrasladoInterGranjaSalida",
        "Traslado entre granjas (recepción)" => "TrasladoInterGranjaEntrada",
        "Traslado entre granjas (rechazado)" => "TrasladoInterGranjaRechazado",
        "Ajuste manual de stock" => "AjusteStock",
        "Eliminación de registro de stock" => "EliminacionStock",
        _ => null
    };

    // ─── TRASLADOS: LISTADO Y EDICIÓN ────────────────────────────────────────

    /// <summary>
    /// Tipos de movimiento que representan la "salida" de un traslado (son el registro primario del par/grupo).
    /// Para misma-granja: TrasladoSalida. Para inter-granja: TrasladoInterGranjaSalida | TrasladoInterGranjaPendiente | TrasladoInterGranjaRechazado.
    /// </summary>
    private static readonly HashSet<string> TrasladoSalidaTypes = new(StringComparer.Ordinal)
    {
        "TrasladoSalida",
        "TrasladoInterGranjaSalida",
        "TrasladoInterGranjaPendiente",
        "TrasladoInterGranjaRechazado"
    };

    private static readonly HashSet<string> TrasladoEntradaTypes = new(StringComparer.Ordinal)
    {
        "TrasladoEntrada",
        "TrasladoInterGranjaEntrada"
    };

    private static string MapEstadoTraslado(string movementType) => movementType switch
    {
        "TrasladoSalida" or "TrasladoEntrada" => "Completado",
        "TrasladoInterGranjaSalida" => "En tránsito",
        "TrasladoInterGranjaPendiente" => "Pendiente despacho",
        "TrasladoInterGranjaEntrada" => "Completado",
        "TrasladoInterGranjaRechazado" => "Rechazado",
        _ => movementType
    };

    public async Task<List<InventarioGestionTrasladoListDto>> GetTrasladosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionTrasladoListDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionTrasladoListDto>();

        var salidaTypes = TrasladoSalidaTypes.ToList();

        // Movimientos "salida" (registro primario del traslado)
        var query = _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value
                        && salidaTypes.Contains(x.MovementType)
                        && (allowedFarmIds.Contains(x.FarmId) || (x.FromFarmId.HasValue && allowedFarmIds.Contains(x.FromFarmId.Value))));

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value || x.FromFarmId == farmId.Value);

        if (fechaDesde.HasValue)
        {
            var start = fechaDesde.Value.Date;
            query = query.Where(x => x.CreatedAt >= start);
        }

        if (fechaHasta.HasValue)
        {
            var end = fechaHasta.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < end);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventario.Nombre ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(itemTipoItem))
        {
            var t = itemTipoItem.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Concepto != null && x.ItemInventario.Concepto.Trim().ToLower() == t) ||
                (x.ItemInventario.TipoItem != null && x.ItemInventario.TipoItem.Trim().ToLower() == t));
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(x => x.NucleoId == nucleoId || x.FromNucleoId == nucleoId);

        if (!string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == galponId || x.FromGalponId == galponId);

        var salidas = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        if (salidas.Count == 0)
            return new List<InventarioGestionTrasladoListDto>();

        // Cargar entradas correspondientes por TransferGroupId
        var groupIds = salidas
            .Where(x => x.TransferGroupId.HasValue)
            .Select(x => x.TransferGroupId!.Value)
            .Distinct()
            .ToList();

        var entradaTypes = TrasladoEntradaTypes.ToList();
        // Un grupo puede tener VARIAS entradas (recepción de tránsito distribuida entre galpones):
        // se agrupa y se toma la primera; la fila del traslado muestra el destino guardado en la salida.
        var entradas = groupIds.Count > 0
            ? (await _db.InventarioGestionMovimientos
                    .AsNoTracking()
                    .Where(x => x.TransferGroupId.HasValue && groupIds.Contains(x.TransferGroupId!.Value) && entradaTypes.Contains(x.MovementType))
                    .ToListAsync(ct))
                .GroupBy(x => x.TransferGroupId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First())
            : new Dictionary<Guid, InventarioGestionMovimiento>();

        // Cargar nombres de granjas (origen + destino)
        var allFarmIds = salidas
            .SelectMany(x => new[] { x.FarmId, x.FromFarmId ?? 0 })
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var farmNames = await _db.Farms.AsNoTracking()
            .Where(f => allFarmIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        // Cargar nombres de núcleos y galpones
        var nucleoIds = salidas
            .SelectMany(x => new[] { x.NucleoId, x.FromNucleoId }.Where(n => !string.IsNullOrWhiteSpace(n)))
            .Distinct()
            .ToList();
        var nucleoRows = nucleoIds.Count > 0
            ? await _db.Nucleos.AsNoTracking()
                .Where(n => nucleoIds.Contains(n.NucleoId) && allFarmIds.Contains(n.GranjaId))
                .ToListAsync(ct)
            : new List<Nucleo>();
        var nucleoDict = nucleoRows.ToDictionary(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre);

        var galponIds = salidas
            .SelectMany(x => new[] { x.GalponId, x.FromGalponId }.Where(g => !string.IsNullOrWhiteSpace(g)))
            .Distinct()
            .ToList();
        var galponRows = galponIds.Count > 0
            ? await _db.Galpones.AsNoTracking()
                .Where(g => galponIds.Contains(g.GalponId) && allFarmIds.Contains(g.GranjaId))
                .ToListAsync(ct)
            : new List<Galpon>();
        var galponDict = galponRows.ToDictionary(g => (g.GalponId, g.GranjaId), g => g.GalponNombre);

        // La fila de salida guarda su propio silo (ORIGEN) y el del otro extremo en from_silo_id
        // (DESTINO), igual que hace con núcleo/galpón. Un solo viaje para los dos.
        var siloNombres = await NombresDeSilosAsync(
            salidas.SelectMany(s => new[] { s.SiloId, s.FromSiloId }), ct);

        return salidas.Select(s =>
        {
            farmNames.TryGetValue(s.FarmId, out var fromGranjaName);
            var toFarmId = s.FromFarmId ?? 0;
            farmNames.TryGetValue(toFarmId, out var toGranjaName);

            string? fromNucleoNombre = s.NucleoId != null && nucleoDict.TryGetValue((s.NucleoId, s.FarmId), out var fnn) ? fnn : null;
            string? fromGalponNombre = s.GalponId != null && galponDict.TryGetValue((s.GalponId, s.FarmId), out var fgn) ? fgn : null;
            string? toNucleoNombre = s.FromNucleoId != null && nucleoDict.TryGetValue((s.FromNucleoId, toFarmId), out var tnn) ? tnn : null;
            string? toGalponNombre = s.FromGalponId != null && galponDict.TryGetValue((s.FromGalponId, toFarmId), out var tgn) ? tgn : null;

            int? entradaId = s.TransferGroupId.HasValue && entradas.TryGetValue(s.TransferGroupId.Value, out var entrada) ? entrada.Id : null;
            var estado = MapEstadoTraslado(s.MovementType);

            return new InventarioGestionTrasladoListDto(
                s.TransferGroupId ?? Guid.Empty,
                s.Id,
                entradaId,
                s.FarmId,
                fromGranjaName,
                s.NucleoId,
                fromNucleoNombre,
                s.GalponId,
                fromGalponNombre,
                toFarmId,
                toGranjaName,
                s.FromNucleoId,
                toNucleoNombre,
                s.FromGalponId,
                toGalponNombre,
                s.ItemInventarioEcuadorId,
                s.ItemInventario.Codigo,
                s.ItemInventario.Nombre,
                s.ItemInventario.Concepto ?? s.ItemInventario.TipoItem ?? "alimento",
                s.ItemInventario.TipoItem ?? "alimento",
                s.Quantity,
                s.Unit,
                s.Reference,
                s.Reason,
                estado,
                s.CreatedAt,
                s.CreatedAt,
                s.SiloId,
                s.SiloId.HasValue && siloNombres.TryGetValue(s.SiloId.Value, out var fsn) ? fsn : null,
                s.FromSiloId,
                s.FromSiloId.HasValue && siloNombres.TryGetValue(s.FromSiloId.Value, out var tsn) ? tsn : null);
        }).ToList();
    }

    public async Task<InventarioGestionTrasladoListDto> ActualizarFechaTrasladoAsync(
        Guid transferGroupId,
        InventarioGestionActualizarFechaTrasladoRequest req,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var movimientos = await _db.InventarioGestionMovimientos
            .Where(x => x.TransferGroupId == transferGroupId && x.CompanyId == companyId.Value)
            .ToListAsync(ct);

        if (movimientos.Count == 0)
            throw new InvalidOperationException("No se encontró el traslado indicado.");

        var salida = movimientos.FirstOrDefault(x => TrasladoSalidaTypes.Contains(x.MovementType));
        if (salida == null)
            throw new InvalidOperationException("El TransferGroupId no corresponde a un traslado.");

        if (!allowedFarmIds.Contains(salida.FarmId) && !(salida.FromFarmId.HasValue && allowedFarmIds.Contains(salida.FromFarmId.Value)))
            throw new InvalidOperationException("No tiene acceso a este traslado.");

        var nuevaFecha = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        foreach (var mov in movimientos)
            mov.CreatedAt = nuevaFecha;

        await _db.SaveChangesAsync(ct);

        // Sincronizar fecha_operacion en tabla espejo lote_registro_historico_unificado
        var movIds = movimientos.Select(m => m.Id).ToList();
        var histTraslado = await _db.LoteRegistroHistoricoUnificados
            .Where(h => h.OrigenTabla == "inventario_gestion_movimiento" && movIds.Contains(h.OrigenId))
            .ToListAsync(ct);
        if (histTraslado.Count > 0)
        {
            var fechaDate = nuevaFecha.UtcDateTime.Date;
            foreach (var h in histTraslado)
                h.FechaOperacion = fechaDate;
            await _db.SaveChangesAsync(ct);
        }

        // Correr la fecha de un traslado mueve el alimento de día: refrescar los galpones tocados
        // (salida y entrada pueden ser distintos, y el grupo puede repartirse en varios).
        foreach (var ubic in movimientos
                     .Select(m => (m.CompanyId, m.FarmId, m.NucleoId, m.GalponId, m.MovementType))
                     .Distinct())
            await RefrescarSaldoAlimentoEngordeAsync(ubic.CompanyId, ubic.FarmId, ubic.NucleoId, ubic.GalponId, ubic.MovementType, ct);

        // Recargar y retornar el DTO actualizado
        var result = await GetTrasladosAsync(farmId: salida.FarmId, ct: ct);
        return result.FirstOrDefault(x => x.TransferGroupId == transferGroupId)
            ?? throw new InvalidOperationException("Error al recargar el traslado actualizado.");
    }

    // ─── INGRESOS: LISTADO Y EDICIÓN ─────────────────────────────────────────

    public async Task<List<InventarioGestionIngresoListDto>> GetIngresosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionIngresoListDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionIngresoListDto>();

        var ingresoTypes = new[] { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };

        var query = _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value
                        && ingresoTypes.Contains(x.MovementType)
                        && allowedFarmIds.Contains(x.FarmId));

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value);

        if (fechaDesde.HasValue)
        {
            var start = fechaDesde.Value.Date;
            query = query.Where(x => x.CreatedAt >= start);
        }

        if (fechaHasta.HasValue)
        {
            var end = fechaHasta.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < end);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventario.Nombre ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(itemTipoItem))
        {
            var t = itemTipoItem.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Concepto != null && x.ItemInventario.Concepto.Trim().ToLower() == t) ||
                (x.ItemInventario.TipoItem != null && x.ItemInventario.TipoItem.Trim().ToLower() == t));
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(x => x.NucleoId == nucleoId);

        if (!string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == galponId);

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        // Query orphaned historico records: food ingresos cuyo inventario_gestion_movimiento
        // fue eliminado físicamente pero cuyo registro en lote_registro_historico_unificado
        // quedó con anulado=false (el lookup en EliminarIngresoAsync no lo encontró).
        var ingresoTiposHist = new[] { "INV_INGRESO", "INV_TRASLADO_ENTRADA" };

        IQueryable<LoteRegistroHistoricoUnificado> orphanedQuery = _db.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId.Value
                && h.OrigenTabla == "inventario_gestion_movimiento"
                && ingresoTiposHist.Contains(h.TipoEvento)
                && !h.Anulado
                && allowedFarmIds.Contains(h.FarmId)
                && !_db.InventarioGestionMovimientos.Any(m => m.Id == h.OrigenId));

        if (farmId.HasValue)
            orphanedQuery = orphanedQuery.Where(h => h.FarmId == farmId.Value);

        if (fechaDesde.HasValue)
        {
            var startO = fechaDesde.Value.Date;
            orphanedQuery = orphanedQuery.Where(h => h.FechaOperacion >= startO);
        }

        if (fechaHasta.HasValue)
        {
            var endO = fechaHasta.Value.Date.AddDays(1);
            orphanedQuery = orphanedQuery.Where(h => h.FechaOperacion < endO);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            orphanedQuery = orphanedQuery.Where(h => (h.ItemResumen ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(itemTipoItem))
        {
            var t = itemTipoItem.Trim().ToLowerInvariant();
            orphanedQuery = orphanedQuery.Where(h =>
                h.ItemInventarioEcuadorId != null &&
                _db.ItemInventario.Any(i => i.Id == h.ItemInventarioEcuadorId &&
                    ((i.Concepto != null && i.Concepto.Trim().ToLower() == t) ||
                     (i.TipoItem != null && i.TipoItem.Trim().ToLower() == t))));
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
            orphanedQuery = orphanedQuery.Where(h => h.NucleoId == nucleoId);

        if (!string.IsNullOrWhiteSpace(galponId))
            orphanedQuery = orphanedQuery.Where(h => h.GalponId == galponId);

        var orphaned = await orphanedQuery
            .OrderByDescending(h => h.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        if (list.Count == 0 && orphaned.Count == 0)
            return new List<InventarioGestionIngresoListDto>();

        var farmIds = list.Select(x => x.FarmId)
            .Concat(orphaned.Select(h => h.FarmId))
            .Distinct().ToList();

        var nucleoIds = list.Where(x => !string.IsNullOrWhiteSpace(x.NucleoId)).Select(x => x.NucleoId!)
            .Concat(orphaned.Where(h => !string.IsNullOrWhiteSpace(h.NucleoId)).Select(h => h.NucleoId!))
            .Distinct().ToList();

        var galponIds = list.Where(x => !string.IsNullOrWhiteSpace(x.GalponId)).Select(x => x.GalponId!)
            .Concat(orphaned.Where(h => !string.IsNullOrWhiteSpace(h.GalponId)).Select(h => h.GalponId!))
            .Distinct().ToList();

        var nucleos = nucleoIds.Count > 0
            ? await _db.Nucleos.AsNoTracking()
                .Where(n => nucleoIds.Contains(n.NucleoId) && farmIds.Contains(n.GranjaId))
                .ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct)
            : new Dictionary<(string, int), string>();

        var galpones = galponIds.Count > 0
            ? await _db.Galpones.AsNoTracking()
                .Where(g => galponIds.Contains(g.GalponId) && farmIds.Contains(g.GranjaId))
                .ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct)
            : new Dictionary<(string, int), string>();

        // Farms y items para registros huérfanos (list ya tiene Farm cargado via Include)
        var orphanedFarmIds = orphaned.Select(h => h.FarmId).Distinct().ToList();
        var orphanedFarms = orphanedFarmIds.Count > 0
            ? await _db.Farms.AsNoTracking()
                .Where(f => orphanedFarmIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.Name, ct)
            : new Dictionary<int, string>();

        var orphanedItemIds = orphaned
            .Where(h => h.ItemInventarioEcuadorId.HasValue)
            .Select(h => h.ItemInventarioEcuadorId!.Value)
            .Distinct().ToList();
        var orphanedItems = orphanedItemIds.Count > 0
            ? await _db.ItemInventario.AsNoTracking()
                .Where(i => orphanedItemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, ct)
            : new Dictionary<int, ItemInventario>();

        // Solo los movimientos vivos traen silo: en las filas huérfanas el dato murió con el
        // movimiento (el espejo lo guarda, pero la entidad del histórico no lo mapea).
        var siloNombres = await NombresDeSilosAsync(list.Select(x => x.SiloId), ct);

        var mainDtos = list.Select(x =>
        {
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;

            return new InventarioGestionIngresoListDto(
                x.Id,
                x.FarmId,
                x.Farm.Name,
                x.NucleoId,
                nucleoNombre,
                x.GalponId,
                galponNombre,
                x.ItemInventarioEcuadorId,
                x.ItemInventario.Codigo,
                x.ItemInventario.Nombre,
                x.ItemInventario.Concepto ?? x.ItemInventario.TipoItem ?? "alimento",
                x.ItemInventario.TipoItem ?? "alimento",
                x.Quantity,
                x.Unit,
                x.Reference,
                x.Reason,
                x.Estado,
                x.CreatedAt,
                x.CreatedAt,
                x.ParaProximoCiclo,
                x.RegistradoAt,
                x.SiloId,
                x.SiloId.HasValue && siloNombres.TryGetValue(x.SiloId.Value, out var sn) ? sn : null);
        });

        var orphanedDtos = orphaned.Select(h =>
        {
            orphanedItems.TryGetValue(h.ItemInventarioEcuadorId ?? 0, out var item);

            // ItemResumen viene del trigger como "codigo — nombre"
            string itemCodigo = item?.Codigo ?? "";
            string itemNombre = item?.Nombre ?? "";
            if (string.IsNullOrEmpty(itemCodigo) && !string.IsNullOrEmpty(h.ItemResumen))
            {
                var parts = h.ItemResumen.Split('—', 2);
                itemCodigo = parts[0].Trim();
                itemNombre = parts.Length > 1 ? parts[1].Trim() : h.ItemResumen;
            }

            string? nucleoNombre = h.NucleoId != null && nucleos.TryGetValue((h.NucleoId, h.FarmId), out var nn) ? nn : null;
            string? galponNombre = h.GalponId != null && galpones.TryGetValue((h.GalponId, h.FarmId), out var gn) ? gn : null;
            orphanedFarms.TryGetValue(h.FarmId, out var farmName);

            return new InventarioGestionIngresoListDto(
                h.OrigenId,
                h.FarmId,
                farmName,
                h.NucleoId,
                nucleoNombre,
                h.GalponId,
                galponNombre,
                h.ItemInventarioEcuadorId ?? 0,
                itemCodigo,
                itemNombre,
                item?.Concepto ?? item?.TipoItem ?? "alimento",
                item?.TipoItem ?? "alimento",
                h.CantidadKg ?? 0,
                h.Unidad ?? "kg",
                h.Referencia,
                null,
                null,
                new DateTimeOffset(h.FechaOperacion, TimeSpan.Zero),
                h.CreatedAt,
                // El movimiento ya no existe: la marca sobrevive en el espejo, el instante de captura no
                // (`registrado_at` vive solo en inventario_gestion_movimiento).
                h.ParaProximoCiclo,
                null);
        });

        return mainDtos.Concat(orphanedDtos)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
    }

    public async Task<InventarioGestionIngresoListDto> ActualizarFechaIngresoAsync(
        int movimientoId,
        InventarioGestionActualizarFechaIngresoRequest req,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var mov = await _db.InventarioGestionMovimientos
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);

        if (mov == null)
            throw new InvalidOperationException("No se encontró el ingreso indicado.");

        var tiposEntradaEditables = new HashSet<string>(StringComparer.Ordinal) { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };
        if (!tiposEntradaEditables.Contains(mov.MovementType))
            throw new InvalidOperationException("Solo se puede editar la fecha de movimientos de tipo Ingreso o entrada de traslado.");

        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a este ingreso.");

        mov.CreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        await _db.SaveChangesAsync(ct);

        // Sincronizar fecha_operacion en tabla espejo lote_registro_historico_unificado
        var fechaDateIngreso = mov.CreatedAt.UtcDateTime.Date;
        var histIngreso = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h => h.OrigenTabla == "inventario_gestion_movimiento" && h.OrigenId == movimientoId, ct);
        if (histIngreso != null)
        {
            histIngreso.FechaOperacion = fechaDateIngreso;
        }
        else
        {
            // Fallback: identificar por granja + nucleo + galpon + item + cantidad sin estar anulado
            var histFallback = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.FarmId == mov.FarmId &&
                    h.NucleoId == mov.NucleoId &&
                    h.GalponId == mov.GalponId &&
                    h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    h.CantidadKg == mov.Quantity &&
                    !h.Anulado, ct);
            if (histFallback != null)
                histFallback.FechaOperacion = fechaDateIngreso;
        }
        await _db.SaveChangesAsync(ct);

        // Correr la fecha de un ingreso lo mueve de día dentro del saldo del galpón.
        await RefrescarSaldoAlimentoEngordeAsync(mov.CompanyId, mov.FarmId, mov.NucleoId, mov.GalponId, mov.MovementType, ct);

        return await MapIngresoListDtoAsync(mov, ct);
    }

    /// <summary>
    /// Proyección de un movimiento de ingreso ya cargado (con <c>Farm</c> e <c>ItemInventario</c>) al
    /// DTO del listado. Extraída sin cambiar nada: las dos ediciones de un ingreso —fecha y destino de
    /// ciclo— devuelven exactamente la misma fila.
    /// </summary>
    private async Task<InventarioGestionIngresoListDto> MapIngresoListDtoAsync(
        InventarioGestionMovimiento mov, CancellationToken ct)
    {
        string? nucleoNombre = null;
        string? galponNombre = null;
        if (mov.NucleoId != null)
            nucleoNombre = await _db.Nucleos.AsNoTracking()
                .Where(n => n.NucleoId == mov.NucleoId && n.GranjaId == mov.FarmId)
                .Select(n => n.NucleoNombre)
                .FirstOrDefaultAsync(ct);
        if (mov.GalponId != null)
            galponNombre = await _db.Galpones.AsNoTracking()
                .Where(g => g.GalponId == mov.GalponId && g.GranjaId == mov.FarmId)
                .Select(g => g.GalponNombre)
                .FirstOrDefaultAsync(ct);

        return new InventarioGestionIngresoListDto(
            mov.Id,
            mov.FarmId,
            mov.Farm.Name,
            mov.NucleoId,
            nucleoNombre,
            mov.GalponId,
            galponNombre,
            mov.ItemInventarioEcuadorId,
            mov.ItemInventario.Codigo,
            mov.ItemInventario.Nombre,
            mov.ItemInventario.Concepto ?? mov.ItemInventario.TipoItem ?? "alimento",
            mov.ItemInventario.TipoItem ?? "alimento",
            mov.Quantity,
            mov.Unit,
            mov.Reference,
            mov.Reason,
            mov.Estado,
            mov.CreatedAt,
            mov.CreatedAt,
            mov.ParaProximoCiclo,
            mov.RegistradoAt);
    }

    /// <summary>
    /// Cambia la atribución de ciclo de un ingreso ya registrado y la refleja en
    /// <c>lote_registro_historico_unificado</c>.
    /// <para>
    /// El espejo se busca <b>primero</b> por <c>origen_tabla + origen_id</c>, que es la clave real
    /// (<c>uq_lote_hist_origen</c>). El fallback por granja+núcleo+galpón+ítem+cantidad se conserva tal
    /// cual está en <see cref="ActualizarFechaIngresoAsync"/> para no divergir, pero es <b>frágil</b>:
    /// con dos ingresos idénticos en la misma ubicación puede marcar el otro. Se llega a él solo con
    /// filas viejas sin <c>origen_id</c>; si se cambia, hay que cambiarlo en los dos lugares a la vez.
    /// </para>
    /// </summary>
    public async Task<InventarioGestionIngresoListDto> ActualizarDestinoCicloIngresoAsync(
        int movimientoId,
        InventarioGestionActualizarDestinoCicloRequest req,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var mov = await _db.InventarioGestionMovimientos
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);

        if (mov == null)
            throw new InvalidOperationException("No se encontró el ingreso indicado.");

        var tiposEntradaEditables = new HashSet<string>(StringComparer.Ordinal) { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };
        if (!tiposEntradaEditables.Contains(mov.MovementType))
            throw new InvalidOperationException("Solo se puede marcar el destino de ciclo de movimientos de tipo Ingreso o entrada de traslado.");

        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a este ingreso.");

        if (string.IsNullOrWhiteSpace(mov.GalponId))
            throw new InvalidOperationException("La marca «para el próximo ciclo» solo aplica a movimientos con galpón: sin galpón no hay ciclo al que atribuir el alimento.");

        // v16a: PONER la marca está deshabilitado; QUITARLA no, para que ninguna marca vieja quede
        // sin poder corregirse (R3). Si el movimiento ya está en el valor pedido, no hay escritura.
        if (req.ParaProximoCiclo != mov.ParaProximoCiclo)
            GuardarMarcaProximoCicloApagada(req.ParaProximoCiclo);

        mov.ParaProximoCiclo = req.ParaProximoCiclo;
        await _db.SaveChangesAsync(ct);

        // Espejo: mismo patrón de búsqueda que ActualizarFechaIngresoAsync (clave real primero,
        // fallback frágil después). El histórico se ANULA, nunca se borra, así que la fila vive.
        var hist = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h => h.OrigenTabla == "inventario_gestion_movimiento" && h.OrigenId == movimientoId, ct);
        if (hist == null)
        {
            hist = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.FarmId == mov.FarmId &&
                    h.NucleoId == mov.NucleoId &&
                    h.GalponId == mov.GalponId &&
                    h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    h.CantidadKg == mov.Quantity &&
                    !h.Anulado, ct);
        }
        if (hist != null)
            hist.ParaProximoCiclo = req.ParaProximoCiclo;
        await _db.SaveChangesAsync(ct);

        // Cambiar de ciclo mueve los kg de una apertura a otra: el saldo persistido se recalcula
        // desde la fn, igual que al correr la fecha.
        await RefrescarSaldoAlimentoEngordeAsync(mov.CompanyId, mov.FarmId, mov.NucleoId, mov.GalponId, mov.MovementType, ct);

        return await MapIngresoListDtoAsync(mov, ct);
    }

    // ─── D4: VENTANA DE ALIMENTO PREVIO AL ENCASETAMIENTO ─────────────────────

    /// <inheritdoc />
    public async Task<InventarioGestionVentanaAlimentoPrevioDto> ResolverVentanaAlimentoPrevioEncasetAsync(
        int farmId,
        string? nucleoId,
        string? galponId,
        DateTime fechaMovimiento,
        CancellationToken ct = default)
    {
        var companyId = await _db.Farms.AsNoTracking()
            .Where(f => f.Id == farmId)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync(ct);

        // Empresa efectiva SIEMPRE por datos (farms.company_id) y fail-closed: sin granja no hay
        // ventana que abrir, así que la regla del mes en curso queda como única.
        if (companyId is not { } company || company <= 0)
            return new InventarioGestionVentanaAlimentoPrevioDto(null, 0);

        var dias = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == company)
            .Select(c => (int?)c.DiasAlimentoPrevioEncaset)
            .FirstOrDefaultAsync(ct) ?? 10;

        var galpon = (galponId ?? "").Trim();
        if (galpon.Length == 0)
            return new InventarioGestionVentanaAlimentoPrevioDto(null, dias);

        var nucleo = (nucleoId ?? "").Trim();
        // `fecha_encaset` es timestamptz: el límite tiene que ir anclado en UTC o Npgsql rechaza el
        // parámetro (Kind=Unspecified). Medianoche UTC del día del movimiento incluye el encaset del
        // MISMO día, que el front graba a mediodía UTC (FechasPuras).
        var desde = FechasPuras.RangoDiaUtc(fechaMovimiento).Desde;

        // Encaset más cercano DEL GALPÓN a partir de la fecha del movimiento. "A partir de" y no
        // "futuro": el alimento se digita días después de llegar, así que el encaset que lo justifica
        // ya puede haber ocurrido. Se miran las dos poblaciones porque el pedido cubre engorde y postura.
        var encasetEngorde = await _db.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == company
                     && l.DeletedAt == null
                     && l.GranjaId == farmId
                     && (l.NucleoId == null ? "" : l.NucleoId.Trim()) == nucleo
                     && (l.GalponId == null ? "" : l.GalponId.Trim()) == galpon
                     && l.FechaEncaset != null
                     && l.FechaEncaset >= desde)
            .MinAsync(l => l.FechaEncaset, ct);

        var encasetPostura = await _db.Lotes.AsNoTracking()
            .Where(l => l.CompanyId == company
                     && l.DeletedAt == null
                     && l.GranjaId == farmId
                     && (l.NucleoId == null ? "" : l.NucleoId.Trim()) == nucleo
                     && (l.GalponId == null ? "" : l.GalponId.Trim()) == galpon
                     && l.FechaEncaset != null
                     && l.FechaEncaset >= desde)
            .MinAsync(l => l.FechaEncaset, ct);

        var proximo = (encasetEngorde, encasetPostura) switch
        {
            (null, null) => (DateTime?)null,
            (null, { } p) => p,
            ({ } e, null) => e,
            ({ } e, { } p) => e <= p ? e : p
        };

        return new InventarioGestionVentanaAlimentoPrevioDto(proximo, dias);
    }

    /// <inheritdoc />
    public async Task<InventarioGestionVentanaAlimentoPrevioDto> ResolverVentanaAlimentoPrevioEncasetDeIngresoAsync(
        int movimientoId,
        DateTime fechaMovimiento,
        CancellationToken ct = default)
    {
        var ubicacion = await _db.InventarioGestionMovimientos.AsNoTracking()
            .Where(m => m.Id == movimientoId)
            .Select(m => new { m.FarmId, m.NucleoId, m.GalponId })
            .FirstOrDefaultAsync(ct);

        if (ubicacion == null)
            return new InventarioGestionVentanaAlimentoPrevioDto(null, 0);

        return await ResolverVentanaAlimentoPrevioEncasetAsync(
            ubicacion.FarmId, ubicacion.NucleoId, ubicacion.GalponId, fechaMovimiento, ct);
    }

    // ─── ELIMINAR INGRESO ─────────────────────────────────────────────────────

    /// <summary>
    /// Elimina un movimiento de tipo Ingreso / TrasladoEntrada / TrasladoInterGranjaEntrada.
    /// No modifica stock. Marca anulado=true en lote_registro_historico_unificado (auditoría)
    /// y elimina físicamente el registro de inventario_gestion_movimiento.
    /// </summary>
    public async Task EliminarIngresoAsync(int movimientoId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var mov = await _db.InventarioGestionMovimientos
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);

        // Caso huérfano: el movimiento ya fue eliminado físicamente pero quedó un registro
        // en lote_registro_historico_unificado con anulado=false. Solo marcarlo anulado.
        if (mov == null)
        {
            var ingresoTiposHist = new[] { "INV_INGRESO", "INV_TRASLADO_ENTRADA" };
            var histHuerfano = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.OrigenTabla == "inventario_gestion_movimiento"
                    && h.OrigenId == movimientoId
                    && h.CompanyId == companyId.Value
                    && ingresoTiposHist.Contains(h.TipoEvento)
                    && !h.Anulado
                    && allowedFarmIds.Contains(h.FarmId), ct);

            if (histHuerfano == null)
                throw new InvalidOperationException("No se encontró el ingreso indicado.");

            histHuerfano.Anulado = true;
            await _db.SaveChangesAsync(ct);
            await RefrescarSaldoAlimentoEngordeAsync(
                histHuerfano.CompanyId, histHuerfano.FarmId, histHuerfano.NucleoId, histHuerfano.GalponId, "Ingreso", ct);
            return;
        }

        var tiposIngreso = new HashSet<string>(StringComparer.Ordinal)
            { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };
        if (!tiposIngreso.Contains(mov.MovementType))
            throw new InvalidOperationException("Solo se pueden eliminar movimientos de tipo Ingreso o entrada de traslado.");

        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a este ingreso.");

        // Marcar anulado en tabla espejo (auditoría)
        var histElimIngreso = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h =>
                h.OrigenTabla == "inventario_gestion_movimiento" && h.OrigenId == movimientoId, ct);
        if (histElimIngreso == null)
        {
            // Fallback: buscar por granja + nucleo + galpon + item + cantidad sin estar anulado
            histElimIngreso = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.FarmId == mov.FarmId &&
                    h.NucleoId == mov.NucleoId &&
                    h.GalponId == mov.GalponId &&
                    h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    h.CantidadKg == mov.Quantity &&
                    !h.Anulado, ct);
        }
        if (histElimIngreso != null)
            histElimIngreso.Anulado = true;

        _db.InventarioGestionMovimientos.Remove(mov);
        await _db.SaveChangesAsync(ct);
        // El histórico queda `anulado`, que el saldo sí filtra: el alimento eliminado debe desaparecer.
        await RefrescarSaldoAlimentoEngordeAsync(mov.CompanyId, mov.FarmId, mov.NucleoId, mov.GalponId, mov.MovementType, ct);
    }

    // ─── ELIMINAR TRASLADO ────────────────────────────────────────────────────

    /// <summary>
    /// Elimina todos los movimientos de un TransferGroupId.
    /// No modifica stock. Marca anulado=true en lote_registro_historico_unificado (auditoría)
    /// y elimina físicamente todos los registros de inventario_gestion_movimiento del grupo.
    /// </summary>
    public async Task EliminarTrasladoAsync(Guid transferGroupId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var movimientos = await _db.InventarioGestionMovimientos
            .Where(x => x.TransferGroupId == transferGroupId && x.CompanyId == companyId.Value)
            .ToListAsync(ct);
        if (movimientos.Count == 0)
            throw new InvalidOperationException("No se encontró el traslado indicado.");

        var salida = movimientos.FirstOrDefault(x => TrasladoSalidaTypes.Contains(x.MovementType));
        if (salida == null)
            throw new InvalidOperationException("El TransferGroupId no corresponde a un traslado.");

        if (!allowedFarmIds.Contains(salida.FarmId) &&
            !(salida.FromFarmId.HasValue && allowedFarmIds.Contains(salida.FromFarmId.Value)))
            throw new InvalidOperationException("No tiene acceso a este traslado.");

        // Marcar anulado en tabla espejo para todos los movimientos del grupo
        var movIds = movimientos.Select(m => m.Id).ToList();
        var histElimTraslado = await _db.LoteRegistroHistoricoUnificados
            .Where(h => h.OrigenTabla == "inventario_gestion_movimiento" && movIds.Contains(h.OrigenId))
            .ToListAsync(ct);
        foreach (var h in histElimTraslado)
            h.Anulado = true;

        _db.InventarioGestionMovimientos.RemoveRange(movimientos);
        await _db.SaveChangesAsync(ct);

        // Un grupo de traslado toca salida y entrada, y puede repartirse en varios galpones.
        foreach (var ubic in movimientos
                     .Select(m => (m.CompanyId, m.FarmId, m.NucleoId, m.GalponId, m.MovementType))
                     .Distinct())
            await RefrescarSaldoAlimentoEngordeAsync(ubic.CompanyId, ubic.FarmId, ubic.NucleoId, ubic.GalponId, ubic.MovementType, ct);
    }
}

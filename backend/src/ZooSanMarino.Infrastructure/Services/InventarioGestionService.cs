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
}

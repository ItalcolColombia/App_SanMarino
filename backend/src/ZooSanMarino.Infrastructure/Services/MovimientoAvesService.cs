// src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs
// Partial 'ancla' del servicio de movimientos de aves (reproductoras): usings, campos, ctor y la
// interfaz. La implementación vive repartida por responsabilidad en 'MovimientoAves/Funciones/'
// (Crud, Consultas, Procesamiento, Traslados, Validaciones, Inventario, SeguimientoDiario,
// Postura, Estadisticas, NavegacionCompleta, Mapeo, EjecucionDirecta). Namespace plano →
// misma DI, misma interfaz, mismo comportamiento. La aritmética pura vive en
// Application/Calculos/MovimientoAvesCalculos.cs.
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService : IMovimientoAvesService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IInventarioAvesService _inventarioService;
    private readonly IHistorialInventarioService _historialService;
    private readonly ILocationScopeResolver _scopeResolver;
    private readonly ILogger<MovimientoAvesService> _logger;

    public MovimientoAvesService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        IInventarioAvesService inventarioService,
        IHistorialInventarioService historialService,
        ILocationScopeResolver scopeResolver,
        ILogger<MovimientoAvesService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _inventarioService = inventarioService;
        _historialService = historialService;
        _scopeResolver = scopeResolver;
        _logger = logger;
    }

    /// <summary>
    /// Filtro de alcance granular (user_farms.restrict_locations + user_farm_scopes) sobre
    /// movimiento_aves, componible en SQL. Un movimiento tiene DOS ubicaciones: la fila es visible si
    /// el lado ORIGEN <b>o</b> el lado DESTINO pasa el cierre del usuario (granja no restringida, o
    /// lote permitido, o —cuando el lado no trae lote— galpón/núcleo visible de ESE lado). Lado sin
    /// granja no otorga visibilidad (fail-closed). Sin granjas restringidas la query queda intacta.
    /// </summary>
    private async Task<IQueryable<MovimientoAves>> AplicarScopeUbicacionAsync(IQueryable<MovimientoAves> q)
    {
        var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
        if (restringidos.Count == 0) return q;

        var granjasRestringidas = restringidos.Keys.ToList();
        var lotesPermitidos = restringidos.SelectMany(kv => kv.Value.LotesPermitidos).ToList();
        var galponesVisibles = restringidos.SelectMany(kv => kv.Value.GalponesVisibles).Distinct().ToList();
        var clavesNucleo = restringidos
            .SelectMany(kv => kv.Value.NucleosVisibles.Select(n => kv.Key + "|" + n))
            .ToList();

        return q.Where(m =>
            // ── Lado ORIGEN ──
            (m.GranjaOrigenId != null &&
                (!granjasRestringidas.Contains(m.GranjaOrigenId.Value)
                 || (m.LoteOrigenId != null && lotesPermitidos.Contains(m.LoteOrigenId.Value))
                 || (m.LoteOrigenId == null && m.GalponOrigenId != null && m.GalponOrigenId != "" &&
                     galponesVisibles.Contains(m.GalponOrigenId))
                 || (m.LoteOrigenId == null && (m.GalponOrigenId == null || m.GalponOrigenId == "") &&
                     m.NucleoOrigenId != null &&
                     clavesNucleo.Contains((m.GranjaOrigenId ?? 0).ToString() + "|" + m.NucleoOrigenId))))
            // ── Lado DESTINO ──
            || (m.GranjaDestinoId != null &&
                (!granjasRestringidas.Contains(m.GranjaDestinoId.Value)
                 || (m.LoteDestinoId != null && lotesPermitidos.Contains(m.LoteDestinoId.Value))
                 || (m.LoteDestinoId == null && m.GalponDestinoId != null && m.GalponDestinoId != "" &&
                     galponesVisibles.Contains(m.GalponDestinoId))
                 || (m.LoteDestinoId == null && (m.GalponDestinoId == null || m.GalponDestinoId == "") &&
                     m.NucleoDestinoId != null &&
                     clavesNucleo.Contains((m.GranjaDestinoId ?? 0).ToString() + "|" + m.NucleoDestinoId)))));
    }
}

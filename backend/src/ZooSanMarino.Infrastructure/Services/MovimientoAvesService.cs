// src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs
// Partial 'ancla' del servicio de movimientos de aves (reproductoras): usings, campos, ctor y la
// interfaz. La implementación vive repartida por responsabilidad en 'MovimientoAves/Funciones/'
// (Crud, Consultas, Procesamiento, Traslados, Validaciones, Inventario, SeguimientoDiario,
// Postura, Estadisticas, NavegacionCompleta, Mapeo, EjecucionDirecta). Namespace plano →
// misma DI, misma interfaz, mismo comportamiento. La aritmética pura vive en
// Application/Calculos/MovimientoAvesCalculos.cs.
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService : IMovimientoAvesService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IInventarioAvesService _inventarioService;
    private readonly IHistorialInventarioService _historialService;
    private readonly ILogger<MovimientoAvesService> _logger;

    public MovimientoAvesService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        IInventarioAvesService inventarioService,
        IHistorialInventarioService historialService,
        ILogger<MovimientoAvesService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _inventarioService = inventarioService;
        _historialService = historialService;
        _logger = logger;
    }
}

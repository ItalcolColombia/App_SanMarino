using ZooSanMarino.Application.DTOs.Dashboard;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Datos del dashboard. Cada método resuelve su propio alcance desde la sesión
/// (<see cref="ICurrentUser"/> + <see cref="ILocationScopeResolver"/>) y <b>nunca</b> recibe la
/// empresa por parámetro: mandarla desde el cliente sería confiar en el header crudo, que es lo que
/// <c>ActiveCompanyMiddleware</c> existe para impedir.
///
/// <para><b>Un método por panel</b>, no un mega-método: es lo que hace posible la carga perezosa de
/// verdad en el front — el panel que no se dibuja no se pide.</para>
///
/// <para>Todos los métodos de panel cortan por <b>módulo del menú</b> antes de consultar nada: sin
/// el módulo, devuelven su forma vacía. Ocultar en el front no es proteger.</para>
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Conteos generales del alcance del usuario. Devuelve ceros —no la empresa entera— cuando el
    /// usuario no tiene granjas visibles.
    /// </summary>
    Task<DashboardResumenDto> GetResumenAsync(CancellationToken ct = default);

    /// <summary>
    /// Panel de postura: mortalidad y huevo por día (columnas capturadas del seguimiento diario,
    /// sumadas en la base) y lotes activos por granja.
    /// </summary>
    Task<DashboardPosturaDto> GetPosturaAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct = default);

    /// <summary>
    /// Panel de pollo engorde: mortalidad, consumo de alimento y peso promedio por día, y lotes
    /// activos por granja.
    /// </summary>
    Task<DashboardEngordeDto> GetEngordeAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct = default);

    /// <summary>
    /// Panel de alimento e inventario: existencias de alimento por granja y galpones con descuadre,
    /// con las dos señales del cuadre (kilos y días en rojo) SEPARADAS.
    /// </summary>
    Task<DashboardInventarioDto> GetInventarioAsync(CancellationToken ct = default);

    /// <summary>
    /// Panel de cumplimiento: vacunación vencida y próxima (vía <c>fn_vacunacion_pendientes</c>, que
    /// ya resuelve el alcance granular) y cuadres del push offline sin resolver.
    /// </summary>
    Task<DashboardCumplimientoDto> GetCumplimientoAsync(CancellationToken ct = default);

    /// <summary>
    /// Routes de menú del usuario en la empresa activa (<c>role_menus</c> ∩ <c>company_menus</c>,
    /// vía <c>fn_menu_usuario</c>), aplanadas y normalizadas.
    ///
    /// <para>Es la señal con la que cada endpoint de panel decide si corta. Se resuelve del lado del
    /// servidor a propósito — el menú que el front tiene en la sesión sirve para dibujar, no para
    /// autorizar.</para>
    /// </summary>
    Task<IReadOnlyList<string>> GetRoutesMenuAsync(CancellationToken ct = default);
}

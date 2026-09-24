// Motor de flujos de validación parametrizables por empresa.
// Archivo ANCLA: usings, campos, ctor, la interfaz y los helpers compartidos entre concerns.
// El resto vive en Funciones/ como partial de esta misma clase (namespace PLANO).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class FlujoValidacionService : IFlujoValidacionService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly IEnumerable<IProcesoValidacionAdapter> _adaptadores;

    public FlujoValidacionService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        IEnumerable<IProcesoValidacionAdapter> adaptadores)
    {
        _ctx = ctx;
        _current = current;
        _adaptadores = adaptadores;
    }

    /// <summary>
    /// Empresa efectiva para operar: la activa validada por <c>ActiveCompanyMiddleware</c>. Fail-closed:
    /// nunca se toma de un parámetro sin cruzar contra <see cref="ICurrentUser.CompanyId"/>, salvo el
    /// Super Admin explícito (ver <see cref="EsAdminEmpresas"/> en Configuracion.cs).
    /// </summary>
    private int CompanyIdActiva => _current.CompanyId;

    /// <summary>Usuario real (GUID) de la sesión. Nunca el hash numérico de <c>ICurrentUser.UserId</c>.</summary>
    private Guid UserIdActual => _current.UserGuid
        ?? throw new UnauthorizedAccessException("La sesión no resolvió un usuario válido.");

    private IProcesoValidacionAdapter ResolverAdaptador(string adapterKey) =>
        _adaptadores.FirstOrDefault(a => a.AdapterKey == adapterKey)
        ?? throw new InvalidOperationException($"No hay un adaptador registrado para '{adapterKey}'.");

    private async Task<ValidacionProceso> ObtenerProcesoPorKeyAsync(string procesoKey, CancellationToken ct)
    {
        var proceso = await _ctx.ValidacionProcesos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Key == procesoKey && p.IsActive, ct);
        if (proceso is null)
            throw new InvalidOperationException($"El proceso '{procesoKey}' no existe o no está activo.");
        return proceso;
    }
}

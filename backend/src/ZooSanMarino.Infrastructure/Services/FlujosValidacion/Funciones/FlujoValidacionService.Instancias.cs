// Creación/cancelación de instancias y consulta de estado (plan §8.2, §9.4).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.FlujosValidacion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class FlujoValidacionService
{
    /// <summary>
    /// Crea la instancia al guardar el registro, con el paso 1 PENDIENTE y el resto BLOQUEADA. Debe
    /// llamarse DENTRO de la misma transacción que guarda el seguimiento y crea sus reservas (plan §8.2).
    /// </summary>
    public async Task<Guid> CrearInstanciaAsync(int companyId, string procesoKey, string recursoId, Guid creadorUserId, CancellationToken ct = default)
    {
        var proceso = await ObtenerProcesoPorKeyAsync(procesoKey, ct);
        var flujo = await _ctx.ValidacionFlujos
            .Include(f => f.Pasos)
            .Where(f => f.CompanyId == companyId && f.ProcesoId == proceso.Id && f.Estado == EstadoFlujoValidacion.Publicado)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"No hay un flujo PUBLICADO de '{procesoKey}' para la empresa {companyId}.");

        var instancia = new ValidacionInstancia
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ProcesoId = proceso.Id,
            FlujoId = flujo.Id,
            RecursoTipo = procesoKey,
            RecursoId = recursoId,
            Intento = 1,
            Estado = EstadoValidacionInstancia.PendienteValidacion,
            PasoActualOrden = 1,
            CreatedByUserId = creadorUserId,
        };

        var ahora = DateTime.UtcNow;
        foreach (var paso in flujo.Pasos.OrderBy(p => p.Orden))
        {
            instancia.Pasos.Add(new ValidacionInstanciaPaso
            {
                PasoDefinicionId = paso.Id,
                Orden = paso.Orden,
                Nombre = paso.Nombre,
                AprobacionesRequeridas = paso.AprobacionesRequeridas,
                Estado = paso.Orden == 1 ? EstadoInstanciaPaso.Pendiente : EstadoInstanciaPaso.Bloqueada,
                OpenedAt = paso.Orden == 1 ? ahora : null,
            });
        }

        _ctx.ValidacionInstancias.Add(instancia);
        await _ctx.SaveChangesAsync(ct);
        return instancia.Id;
    }

    /// <summary>Cancela la instancia activa de un recurso (borrado del registro): libera reservas y
    /// resuelve novedades en la misma transacción (plan §6.16).</summary>
    public async Task CancelarInstanciaDelRecursoAsync(string procesoKey, string recursoId, CancellationToken ct = default)
    {
        var proceso = await ObtenerProcesoPorKeyAsync(procesoKey, ct);
        var instancia = await _ctx.ValidacionInstancias
            .Include(i => i.Pasos)
            .Include(i => i.Novedades)
            .Where(i => i.ProcesoId == proceso.Id && i.RecursoId == recursoId
                     && EstadoValidacionInstancia.Activos.Contains(i.Estado))
            .FirstOrDefaultAsync(ct);
        if (instancia is null) return; // sin instancia activa: no-op, idempotente.

        AutorizarLecturaDeEmpresa(instancia.CompanyId); // fail-closed: misma empresa activa

        await using var tx = _ctx.Database.CurrentTransaction is null
            ? await _ctx.Database.BeginTransactionAsync(ct) : null;

        var ahora = DateTime.UtcNow;
        instancia.Estado = EstadoValidacionInstancia.Cancelada;
        instancia.CancelledAt = ahora;
        instancia.MotivoEstado = "Cancelada: se eliminó el registro origen.";
        foreach (var p in instancia.Pasos.Where(p => p.Estado is EstadoInstanciaPaso.Pendiente or EstadoInstanciaPaso.Bloqueada or EstadoInstanciaPaso.EsperandoCorreccion))
            p.Estado = EstadoInstanciaPaso.Cancelada;
        foreach (var n in instancia.Novedades.Where(n => n.Estado == EstadoNovedadValidacion.Activa))
        {
            n.Estado = EstadoNovedadValidacion.Cancelada;
            n.ResolvedAt = ahora;
            n.ResolvedByUserId = UserIdActual;
            n.Resolucion = ResolucionNovedadValidacion.Eliminado;
        }

        var adaptador = ResolverAdaptador(proceso.AdapterKey);
        await adaptador.LiberarAsync(recursoId, ct);

        await _ctx.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }

    public async Task<EstadoInstanciaFlujoDto?> ObtenerEstadoAsync(string procesoKey, string recursoId, CancellationToken ct = default)
    {
        var proceso = await ObtenerProcesoPorKeyAsync(procesoKey, ct);
        var instancia = await _ctx.ValidacionInstancias.AsNoTracking()
            .Include(i => i.Pasos)
            .Include(i => i.Flujo)
            .Where(i => i.ProcesoId == proceso.Id && i.RecursoId == recursoId)
            .OrderByDescending(i => i.Intento)
            .FirstOrDefaultAsync(ct);
        if (instancia is null) return null;
        AutorizarLecturaDeEmpresa(instancia.CompanyId);

        return await ProyectarEstadoAsync(instancia, ct);
    }

    private async Task<EstadoInstanciaFlujoDto> ProyectarEstadoAsync(ValidacionInstancia instancia, CancellationToken ct)
    {
        var pasos = instancia.Pasos.OrderBy(p => p.Orden).ToList();
        var pasoActual = pasos.FirstOrDefault(p => p.Orden == instancia.PasoActualOrden);

        var pasosDefinicion = await _ctx.ValidacionFlujoPasos.AsNoTracking()
            .Include(p => p.Asignados)
            .Where(p => p.FlujoId == instancia.FlujoId)
            .ToDictionaryAsync(p => p.Orden, ct);

        var candidatosActuales = new List<FlujoValidacionAutorizacionCalculos.Candidato>();
        var candidatosLegiblesActuales = new List<string>();
        if (pasoActual is not null && pasosDefinicion.TryGetValue(pasoActual.Orden, out var defActual))
        {
            foreach (var a in defActual.Asignados)
            {
                candidatosActuales.Add(new FlujoValidacionAutorizacionCalculos.Candidato(
                    a.Tipo == TipoAsignadoFlujo.Rol ? FlujoValidacionAutorizacionCalculos.TipoCandidato.Rol : FlujoValidacionAutorizacionCalculos.TipoCandidato.Usuario,
                    a.RoleId, a.UserId));
            }
        }

        var rolesUsuarioEnEmpresa = _current.UserGuid.HasValue
            ? (await _ctx.UserRoles.AsNoTracking()
                .Where(ur => ur.CompanyId == instancia.CompanyId && ur.UserId == _current.UserGuid.Value)
                .Select(ur => ur.RoleId).ToListAsync(ct)).ToHashSet()
            : new HashSet<int>();

        var yaFirmaronEsteIntento = (await _ctx.ValidacionAcciones.AsNoTracking()
            .Where(a => a.InstanciaId == instancia.Id && a.Accion == TipoAccionValidacion.Aprobar)
            .Select(a => a.UserId).ToListAsync(ct)).ToHashSet();

        bool puedeAprobar = false, puedeDevolver = false;
        if (_current.UserGuid.HasValue && instancia.Estado == EstadoValidacionInstancia.PendienteValidacion)
        {
            puedeAprobar = puedeDevolver = FlujoValidacionAutorizacionCalculos.PuedeAprobarOFirmar(
                _current.UserGuid.Value, instancia.CreatedByUserId,
                instancia.Flujo.RequierePersonasDistintas, instancia.Flujo.PermiteAprobacionCreador,
                rolesUsuarioEnEmpresa, candidatosActuales, yaFirmaronEsteIntento);
        }

        bool puedeCorregir = false, puedeEliminar = false;
        if (_current.UserGuid.HasValue && instancia.Estado == EstadoValidacionInstancia.DevueltaCorreccion)
        {
            var novedadActiva = await _ctx.ValidacionNovedadesUsuario.AsNoTracking()
                .Where(n => n.InstanciaId == instancia.Id && n.Estado == EstadoNovedadValidacion.Activa)
                .OrderByDescending(n => n.CreatedAt).FirstOrDefaultAsync(ct);
            if (novedadActiva is not null)
            {
                puedeCorregir = puedeEliminar = FlujoValidacionAutorizacionCalculos.PuedeCorregirODevolverInstancia(
                    _current.UserGuid.Value, novedadActiva.DestinatarioUserId);
            }
        }

        var acciones = await _ctx.ValidacionAcciones.AsNoTracking()
            .Where(a => a.InstanciaId == instancia.Id)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
        var userIdsHistorial = acciones.Select(a => a.UserId).Distinct().ToList();
        var nombresUsuarios = await _ctx.Users.AsNoTracking().Where(u => userIdsHistorial.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.firstName} {u.surName}".Trim(), ct);

        var historial = acciones.Select(a =>
        {
            var pasoDeLaAccion = pasos.FirstOrDefault(p => p.Id == a.InstanciaPasoId);
            return new AccionInstanciaDto(
                a.Accion, pasoDeLaAccion?.Orden ?? 0, pasoDeLaAccion?.Nombre ?? "",
                a.UserId, nombresUsuarios.GetValueOrDefault(a.UserId, "(usuario)"),
                a.Comentario, a.CreatedAt);
        }).ToList();

        return new EstadoInstanciaFlujoDto(
            instancia.Id, instancia.RecursoTipo, instancia.RecursoId, instancia.Estado,
            instancia.PasoActualOrden, pasos.Count, pasoActual?.Nombre ?? "",
            FlujoValidacionCalculos.CalcularVencimiento(instancia.CreatedAt, instancia.Flujo.PlazoTotalHoras),
            puedeAprobar, puedeDevolver, puedeCorregir, puedeEliminar,
            pasos.Select(p => new InstanciaPasoDto(p.Orden, p.Nombre, p.Estado,
                p.Orden == instancia.PasoActualOrden ? candidatosLegiblesActuales : new List<string>())).ToList(),
            historial);
    }

    public async Task<IReadOnlyList<PendienteFlujoDto>> ObtenerMisPendientesAsync(int companyId, string? procesoKey, CancellationToken ct = default)
    {
        if (!_current.UserGuid.HasValue) return Array.Empty<PendienteFlujoDto>();
        AutorizarLecturaDeEmpresa(companyId);

        var query = _ctx.ValidacionInstancias.AsNoTracking()
            .Include(i => i.Pasos)
            .Include(i => i.Proceso)
            .Include(i => i.Flujo)
            .Where(i => i.CompanyId == companyId && i.Estado == EstadoValidacionInstancia.PendienteValidacion);
        if (!string.IsNullOrWhiteSpace(procesoKey))
            query = query.Where(i => i.Proceso.Key == procesoKey);

        var instancias = await query.ToListAsync(ct);
        var rolesUsuarioEnEmpresa = (await _ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.CompanyId == companyId && ur.UserId == _current.UserGuid.Value)
            .Select(ur => ur.RoleId).ToListAsync(ct)).ToHashSet();

        var resultado = new List<PendienteFlujoDto>();
        foreach (var instancia in instancias)
        {
            var pasoActual = instancia.Pasos.FirstOrDefault(p => p.Orden == instancia.PasoActualOrden);
            if (pasoActual is null) continue;

            var candidatos = await _ctx.ValidacionFlujoAsignados.AsNoTracking()
                .Where(a => a.PasoId == pasoActual.PasoDefinicionId).ToListAsync(ct);
            var esCandidato = candidatos.Any(a =>
                (a.Tipo == TipoAsignadoFlujo.Usuario && a.UserId == _current.UserGuid.Value) ||
                (a.Tipo == TipoAsignadoFlujo.Rol && a.RoleId.HasValue && rolesUsuarioEnEmpresa.Contains(a.RoleId.Value)));
            if (!esCandidato) continue;

            var adaptador = ResolverAdaptador(instancia.Proceso.AdapterKey);
            var resumen = await adaptador.ResumirAsync(instancia.RecursoId, ct);
            if (resumen is null) continue;

            resultado.Add(new PendienteFlujoDto(
                instancia.Id, instancia.Proceso.Key, instancia.RecursoId,
                pasoActual.Orden, pasoActual.Nombre, resumen.FarmNombre, resumen.NucleoId, resumen.GalponId,
                resumen.LoteRef, resumen.FechaSeguimiento,
                FlujoValidacionCalculos.CalcularVencimiento(instancia.CreatedAt, instancia.Flujo.PlazoTotalHoras)));
        }
        return resultado;
    }
}

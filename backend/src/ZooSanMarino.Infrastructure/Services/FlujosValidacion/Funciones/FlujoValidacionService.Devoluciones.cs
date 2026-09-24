// Devolución, corrección/reenvío y eliminación durante una devolución (plan §8.4, casos 22-29).
//
// ALCANCE DE ESTA ITERACIÓN: DevolverAsync solo cubre la devolución "primaria" (desde
// PENDIENTE_VALIDACION, la etapa N no aprueba y retrocede a N-1/creador — casos 23-24, 27). La
// devolución EN CASCADA (el destinatario de una novedad activa decide devolver otra etapa más
// atrás, caso 26) queda pendiente de una iteración siguiente: se rechaza explícitamente en vez de
// simularse mal. Ver tracker_estado.md bloque FLUJOS-VALIDACION-EMPRESA, tarea F2 pendiente.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.FlujosValidacion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class FlujoValidacionService
{
    public async Task<ResultadoAccionInstanciaDto> DevolverAsync(DevolverInstanciaCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Motivo))
            throw new InvalidOperationException("La devolución exige una descripción del motivo.");

        var userId = UserIdActual;

        await using var tx = _ctx.Database.CurrentTransaction is null
            ? await _ctx.Database.BeginTransactionAsync(ct) : null;

        var instancia = await _ctx.ValidacionInstancias
            .FromSqlInterpolated($"SELECT * FROM public.validacion_instancias WHERE id = {cmd.InstanciaId} FOR UPDATE")
            .Include(i => i.Pasos)
            .Include(i => i.Flujo)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("La instancia no existe.");
        AutorizarLecturaDeEmpresa(instancia.CompanyId);

        if (instancia.Estado != EstadoValidacionInstancia.PendienteValidacion)
            throw new InvalidOperationException(
                "Solo se puede devolver desde la etapa pendiente actual. La devolución en cascada de un " +
                "registro ya devuelto todavía no está soportada: corrija y reenvíe, o elimine el registro.");

        var pasoQueDevuelve = instancia.Pasos.FirstOrDefault(p => p.Orden == instancia.PasoActualOrden)
            ?? throw new InvalidOperationException("No se encontró la etapa actual de la instancia.");

        var defActual = await _ctx.ValidacionFlujoPasos.AsNoTracking().Include(p => p.Asignados)
            .FirstOrDefaultAsync(p => p.Id == pasoQueDevuelve.PasoDefinicionId, ct)
            ?? throw new InvalidOperationException("No se encontró la definición de la etapa actual.");
        var rolesUsuarioEnEmpresa = (await _ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.CompanyId == instancia.CompanyId && ur.UserId == userId)
            .Select(ur => ur.RoleId).ToListAsync(ct)).ToHashSet();
        var esCandidato = defActual.Asignados.Any(a =>
            (a.Tipo == TipoAsignadoFlujo.Usuario && a.UserId == userId) ||
            (a.Tipo == TipoAsignadoFlujo.Rol && a.RoleId.HasValue && rolesUsuarioEnEmpresa.Contains(a.RoleId.Value)));
        if (!esCandidato)
            throw new UnauthorizedAccessException("No está autorizado para devolver esta etapa.");

        var ahora = DateTime.UtcNow;
        var n = pasoQueDevuelve.Orden;
        var destino = FlujoValidacionCalculos.EtapaDestinoAlDevolver(n);
        var apuntaAlCreador = FlujoValidacionCalculos.DevolucionApuntaAlCreador(n);

        pasoQueDevuelve.Estado = EstadoInstanciaPaso.EsperandoCorreccion;

        Guid destinatario;
        if (apuntaAlCreador)
        {
            destinatario = instancia.CreatedByUserId;
        }
        else
        {
            var pasoAnterior = instancia.Pasos.First(p => p.Orden == destino);
            pasoAnterior.Estado = EstadoInstanciaPaso.Devuelta;
            var firmanteAnterior = await _ctx.ValidacionAcciones.AsNoTracking()
                .Where(a => a.InstanciaPasoId == pasoAnterior.Id && a.Accion == TipoAccionValidacion.Aprobar)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => a.UserId)
                .FirstOrDefaultAsync(ct);
            destinatario = firmanteAnterior == Guid.Empty ? instancia.CreatedByUserId : firmanteAnterior;
        }

        var accion = new ValidacionAccion
        {
            InstanciaId = instancia.Id,
            InstanciaPasoId = pasoQueDevuelve.Id,
            Accion = TipoAccionValidacion.Devolver,
            UserId = userId,
            Comentario = cmd.Motivo,
            CreatedAt = ahora,
        };
        _ctx.ValidacionAcciones.Add(accion);
        await _ctx.SaveChangesAsync(ct); // necesita el Id de la acción para la novedad (FK).

        instancia.Estado = EstadoValidacionInstancia.DevueltaCorreccion;
        instancia.PasoRetornoOrden = n;
        instancia.ReturnedAt = ahora;
        instancia.UpdatedAt = ahora;
        instancia.MotivoEstado = cmd.Motivo;

        var proceso = await _ctx.ValidacionProcesos.AsNoTracking().FirstAsync(p => p.Id == instancia.ProcesoId, ct);
        var adaptador = ResolverAdaptador(proceso.AdapterKey);
        var resumen = await adaptador.ResumirAsync(instancia.RecursoId, ct);
        var contexto = System.Text.Json.JsonSerializer.Serialize(new
        {
            proceso = proceso.Key,
            farm = resumen?.FarmNombre,
            nucleo = resumen?.NucleoId,
            galpon = resumen?.GalponId,
            lote = resumen?.LoteRef,
            fecha = resumen?.FechaSeguimiento,
            etapaOrden = n,
            etapaNombre = pasoQueDevuelve.Nombre,
        });

        _ctx.ValidacionNovedadesUsuario.Add(new ValidacionNovedadUsuario
        {
            InstanciaId = instancia.Id,
            AccionDevolucionId = accion.Id,
            CompanyId = instancia.CompanyId,
            DestinatarioUserId = destinatario,
            GeneradaPorUserId = userId,
            Estado = EstadoNovedadValidacion.Activa,
            Motivo = cmd.Motivo,
            ContextoResumen = contexto,
            CreatedAt = ahora,
        });

        await _ctx.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new ResultadoAccionInstanciaDto(instancia.Id, instancia.Estado, Finalizada: false);
    }

    public async Task<ResultadoAccionInstanciaDto> CorregirYReenviarAsync(CorregirYReenviarInstanciaCommand cmd, CancellationToken ct = default)
    {
        var userId = UserIdActual;

        await using var tx = _ctx.Database.CurrentTransaction is null
            ? await _ctx.Database.BeginTransactionAsync(ct) : null;

        var instancia = await _ctx.ValidacionInstancias
            .FromSqlInterpolated($"SELECT * FROM public.validacion_instancias WHERE id = {cmd.InstanciaId} FOR UPDATE")
            .Include(i => i.Pasos)
            .Include(i => i.Novedades)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("La instancia no existe.");
        AutorizarLecturaDeEmpresa(instancia.CompanyId);

        if (instancia.Estado != EstadoValidacionInstancia.DevueltaCorreccion || instancia.PasoRetornoOrden is null)
            throw new InvalidOperationException("La instancia no está esperando una corrección.");

        var novedadActiva = instancia.Novedades
            .Where(n => n.Estado == EstadoNovedadValidacion.Activa)
            .OrderByDescending(n => n.CreatedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("No hay una novedad activa para esta instancia.");

        if (!FlujoValidacionAutorizacionCalculos.PuedeCorregirODevolverInstancia(userId, novedadActiva.DestinatarioUserId))
            throw new UnauthorizedAccessException("Solo el destinatario de la novedad puede corregir y reenviar.");

        var ahora = DateTime.UtcNow;
        var n = instancia.PasoRetornoOrden.Value;
        var destino = FlujoValidacionCalculos.EtapaDestinoAlDevolver(n);

        // Nota: los datos del registro (alimento/aves/huevos) ya fueron editados por el adaptador/CRUD
        // ANTES de llamar acá — este método solo mueve el estado del flujo (plan §8.4).
        if (destino > 0)
        {
            var pasoAnterior = instancia.Pasos.First(p => p.Orden == destino);
            pasoAnterior.Estado = EstadoInstanciaPaso.Aprobada;
        }
        var pasoQueDevolvio = instancia.Pasos.First(p => p.Orden == n);
        pasoQueDevolvio.Estado = EstadoInstanciaPaso.Pendiente;
        pasoQueDevolvio.OpenedAt = ahora;

        instancia.PasoActualOrden = n;
        instancia.PasoRetornoOrden = null;
        instancia.Estado = EstadoValidacionInstancia.PendienteValidacion;
        instancia.UpdatedAt = ahora;

        _ctx.ValidacionAcciones.Add(new ValidacionAccion
        {
            InstanciaId = instancia.Id,
            InstanciaPasoId = pasoQueDevolvio.Id,
            Accion = TipoAccionValidacion.Reenviar,
            UserId = userId,
            CreatedAt = ahora,
        });

        novedadActiva.Estado = EstadoNovedadValidacion.Resuelta;
        novedadActiva.ResolvedAt = ahora;
        novedadActiva.ResolvedByUserId = userId;
        novedadActiva.Resolucion = ResolucionNovedadValidacion.CorregidoReenviado;

        await _ctx.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new ResultadoAccionInstanciaDto(instancia.Id, instancia.Estado, Finalizada: false);
    }

    public async Task<ResultadoAccionInstanciaDto> EliminarAsync(EliminarInstanciaCommand cmd, CancellationToken ct = default)
    {
        var userId = UserIdActual;

        await using var tx = _ctx.Database.CurrentTransaction is null
            ? await _ctx.Database.BeginTransactionAsync(ct) : null;

        var instancia = await _ctx.ValidacionInstancias
            .FromSqlInterpolated($"SELECT * FROM public.validacion_instancias WHERE id = {cmd.InstanciaId} FOR UPDATE")
            .Include(i => i.Pasos)
            .Include(i => i.Novedades)
            .Include(i => i.Proceso)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("La instancia no existe.");
        AutorizarLecturaDeEmpresa(instancia.CompanyId);

        if (!EstadoValidacionInstancia.Activos.Contains(instancia.Estado))
            throw new InvalidOperationException("La instancia ya no está activa.");

        if (instancia.Estado == EstadoValidacionInstancia.DevueltaCorreccion)
        {
            var novedadActiva = instancia.Novedades
                .Where(n => n.Estado == EstadoNovedadValidacion.Activa)
                .OrderByDescending(n => n.CreatedAt).FirstOrDefault();
            if (novedadActiva is null || !FlujoValidacionAutorizacionCalculos.PuedeCorregirODevolverInstancia(userId, novedadActiva.DestinatarioUserId))
                throw new UnauthorizedAccessException("Solo el destinatario de la novedad puede eliminar este registro.");
        }
        else if (userId != instancia.CreatedByUserId && !_current.EsAdminEmpresas)
        {
            throw new UnauthorizedAccessException("Solo el creador puede eliminar un registro pendiente sin devolución.");
        }

        var ahora = DateTime.UtcNow;
        instancia.Estado = EstadoValidacionInstancia.Cancelada;
        instancia.CancelledAt = ahora;
        instancia.UpdatedAt = ahora;
        foreach (var p in instancia.Pasos.Where(p => p.Estado is EstadoInstanciaPaso.Pendiente or EstadoInstanciaPaso.Bloqueada or EstadoInstanciaPaso.EsperandoCorreccion or EstadoInstanciaPaso.Devuelta))
            p.Estado = EstadoInstanciaPaso.Cancelada;
        foreach (var n in instancia.Novedades.Where(n => n.Estado == EstadoNovedadValidacion.Activa))
        {
            n.Estado = EstadoNovedadValidacion.Resuelta;
            n.ResolvedAt = ahora;
            n.ResolvedByUserId = userId;
            n.Resolucion = ResolucionNovedadValidacion.Eliminado;
        }

        _ctx.ValidacionAcciones.Add(new ValidacionAccion
        {
            InstanciaId = instancia.Id,
            InstanciaPasoId = instancia.Pasos.First(p => p.Orden == instancia.PasoActualOrden).Id,
            Accion = TipoAccionValidacion.Eliminar,
            UserId = userId,
            CreatedAt = ahora,
        });

        var adaptador = ResolverAdaptador(instancia.Proceso.AdapterKey);
        await adaptador.LiberarAsync(instancia.RecursoId, ct);

        await _ctx.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new ResultadoAccionInstanciaDto(instancia.Id, instancia.Estado, Finalizada: false);
    }
}

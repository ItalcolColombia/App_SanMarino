// Aprobación de la etapa actual: intermedia (solo avanza) o final (finaliza vía el adaptador del
// proceso, reutilizando el finalizador real sin duplicar fórmulas). Plan §8.3, casos 18-21.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.FlujosValidacion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class FlujoValidacionService
{
    public async Task<ResultadoAccionInstanciaDto> AprobarAsync(AprobarInstanciaCommand cmd, CancellationToken ct = default)
    {
        var userId = UserIdActual;

        await using var tx = _ctx.Database.CurrentTransaction is null
            ? await _ctx.Database.BeginTransactionAsync(ct) : null;

        // Bloquea la instancia + su etapa actual: SELECT ... FOR UPDATE evita que dos aprobaciones
        // concurrentes de la última etapa apliquen efectos dos veces (caso 20 del plan).
        var instancia = await _ctx.ValidacionInstancias
            .FromSqlInterpolated($"SELECT * FROM public.validacion_instancias WHERE id = {cmd.InstanciaId} FOR UPDATE")
            .Include(i => i.Pasos)
            .Include(i => i.Flujo)
            .Include(i => i.Proceso)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("La instancia no existe.");
        AutorizarLecturaDeEmpresa(instancia.CompanyId);

        if (instancia.Estado != EstadoValidacionInstancia.PendienteValidacion)
            throw new InvalidOperationException($"La instancia no está pendiente de aprobación (estado actual: {instancia.Estado}).");

        var pasoActual = instancia.Pasos.FirstOrDefault(p => p.Orden == instancia.PasoActualOrden)
            ?? throw new InvalidOperationException("No se encontró la etapa actual de la instancia.");

        var defActual = await _ctx.ValidacionFlujoPasos.AsNoTracking().Include(p => p.Asignados)
            .FirstOrDefaultAsync(p => p.Id == pasoActual.PasoDefinicionId, ct)
            ?? throw new InvalidOperationException("No se encontró la definición de la etapa actual.");

        var candidatos = defActual.Asignados.Select(a => new FlujoValidacionAutorizacionCalculos.Candidato(
            a.Tipo == TipoAsignadoFlujo.Rol ? FlujoValidacionAutorizacionCalculos.TipoCandidato.Rol : FlujoValidacionAutorizacionCalculos.TipoCandidato.Usuario,
            a.RoleId, a.UserId)).ToList();
        var asignadoQueHabilita = defActual.Asignados.FirstOrDefault(a =>
            (a.Tipo == TipoAsignadoFlujo.Usuario && a.UserId == userId));

        var rolesUsuarioEnEmpresa = (await _ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.CompanyId == instancia.CompanyId && ur.UserId == userId)
            .Select(ur => ur.RoleId).ToListAsync(ct)).ToHashSet();
        if (asignadoQueHabilita is null)
            asignadoQueHabilita = defActual.Asignados.FirstOrDefault(a => a.Tipo == TipoAsignadoFlujo.Rol && a.RoleId.HasValue && rolesUsuarioEnEmpresa.Contains(a.RoleId.Value));

        var yaFirmaronEsteIntento = (await _ctx.ValidacionAcciones.AsNoTracking()
            .Where(a => a.InstanciaId == instancia.Id && a.Accion == TipoAccionValidacion.Aprobar)
            .Select(a => a.UserId).ToListAsync(ct)).ToHashSet();

        var puedeAprobar = FlujoValidacionAutorizacionCalculos.PuedeAprobarOFirmar(
            userId, instancia.CreatedByUserId, instancia.Flujo.RequierePersonasDistintas,
            instancia.Flujo.PermiteAprobacionCreador, rolesUsuarioEnEmpresa, candidatos, yaFirmaronEsteIntento);
        if (!puedeAprobar)
            throw new UnauthorizedAccessException("No está autorizado para aprobar esta etapa.");

        var ahora = DateTime.UtcNow;
        pasoActual.Estado = EstadoInstanciaPaso.Aprobada;
        pasoActual.CompletedAt = ahora;

        _ctx.ValidacionAcciones.Add(new ValidacionAccion
        {
            InstanciaId = instancia.Id,
            InstanciaPasoId = pasoActual.Id,
            Accion = TipoAccionValidacion.Aprobar,
            UserId = userId,
            AsignadoId = asignadoQueHabilita?.Id,
            CreatedAt = ahora,
        });

        var totalPasos = instancia.Pasos.Count;
        var esUltima = FlujoValidacionCalculos.EsUltimaEtapa(pasoActual.Orden, totalPasos);

        if (!esUltima)
        {
            var siguienteOrden = FlujoValidacionCalculos.SiguienteEtapa(pasoActual.Orden);
            var siguiente = instancia.Pasos.First(p => p.Orden == siguienteOrden);
            siguiente.Estado = EstadoInstanciaPaso.Pendiente;
            siguiente.OpenedAt = ahora;
            instancia.PasoActualOrden = siguienteOrden;
            instancia.UpdatedAt = ahora;

            await _ctx.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return new ResultadoAccionInstanciaDto(instancia.Id, instancia.Estado, Finalizada: false);
        }

        // Última etapa: finaliza vía el adaptador dentro de la MISMA transacción. Si falla, todo se
        // revierte y la instancia queda en ERROR_FINALIZACION (caso 21 del plan).
        try
        {
            var adaptador = ResolverAdaptador(instancia.Proceso.AdapterKey);
            await adaptador.FinalizarAsync(instancia.RecursoId, ct);

            instancia.Estado = EstadoValidacionInstancia.Aprobada;
            instancia.CompletedAt = ahora;
            instancia.UpdatedAt = ahora;
            await _ctx.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return new ResultadoAccionInstanciaDto(instancia.Id, instancia.Estado, Finalizada: true);
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            // La firma y el avance de etapa se deshacen con el rollback (nadie descontó dos veces).
            // Se deja constancia del error en una escritura aparte, fuera de la transacción revertida,
            // para que la instancia sea visible como "necesita reintento" sin duplicar firmas.
            await _ctx.ValidacionInstancias
                .Where(i => i.Id == instancia.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(i => i.Estado, EstadoValidacionInstancia.ErrorFinalizacion)
                    .SetProperty(i => i.MotivoEstado, "La finalización del proceso falló; la firma no se aplicó: " + ex.Message)
                    .SetProperty(i => i.UpdatedAt, DateTime.UtcNow), ct);
            throw;
        }
    }
}

// Implementacion/Funciones/ImplementacionService.ItalJira.cs
// Enlace del plan de implementación con el tablero de ItalJira (I1.2): una historia por plan y una
// tarea por punto del checklist. El reflejo inverso —tarea en LISTO ⇒ punto completado— vive en
// TicketTareaService, que es el único escritor de ticket_tareas.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ImplementacionService
{
    /// <summary>
    /// Enlaza el plan con ItalJira: crea su historia si no la tiene y una tarea del tablero por cada
    /// punto del checklist que todavía no tenga la suya.
    ///
    /// <para>
    /// <b>Es explícito, no automático.</b> Un plan de implementación no siempre es trabajo del área
    /// de desarrollo: hay entregas que se coordinan sólo con el cliente. Crear la historia en cada
    /// alta llenaría el backlog de épicas que nadie va a mover, así que el enlace lo pide quien
    /// gestiona el tablero.
    /// </para>
    ///
    /// <para>
    /// <b>Es idempotente.</b> Se puede llamar de nuevo después de agregar puntos: crea sólo lo que
    /// falta, y devuelve por separado lo que ya estaba de lo que se creó ahora —sin ese desglose no
    /// se distingue «no hacía falta hacer nada» de «no hizo nada»—.
    /// </para>
    ///
    /// <para>
    /// No duplica escritores: la historia la crea <c>IHistoriaService</c> y las tareas
    /// <c>ITicketTareaService.CrearTareaItalJiraAsync</c>, que son los mismos que usa el tablero. Acá
    /// sólo se guarda de qué lado quedó atado cada punto. El permiso también es el de ellos
    /// (<c>tickets.gestionar</c>): si el usuario no lo tiene, esos servicios lanzan.
    /// </para>
    /// </summary>
    /// <returns><c>null</c> si el plan no existe o no es de la empresa activa.</returns>
    public async Task<ImplementacionItalJiraDto?> SincronizarConItalJiraAsync(
        int planId, CancellationToken ct = default)
    {
        if (_historias is null || _ticketTareas is null)
            throw new InvalidOperationException("El módulo ItalJira no está disponible en este entorno.");

        // El permiso se exige ACÁ, no sólo en los servicios de ItalJira que se delegan más abajo.
        // Apoyarse en que ellos lancen al crear deja un agujero: con el plan ya enlazado no hay nada
        // que crear, no se los llama, y el endpoint contestaba 200 a quien no gestiona el tablero
        // —sellándole además el updated_by del plan—. La regla sigue teniendo un solo dueño: se
        // pregunta, no se reescribe.
        if (!_historias.PuedeGestionarItalJira())
            throw new InvalidOperationException("No tenés permisos para gestionar ItalJira.");

        var plan = await GetPlanScopedAsync(planId, ct);
        if (plan is null) return null;

        // ── La historia ────────────────────────────────────────────────────────
        var historiaCreada = false;

        if (plan.HistoriaId is not null)
        {
            // Una historia borrada desde el tablero dejaría el plan apuntando a la nada; en ese caso
            // se vuelve a crear en vez de fallar, que es lo que el usuario espera del botón.
            var vive = await _ctx.Historias.AsNoTracking()
                .AnyAsync(h => h.Id == plan.HistoriaId.Value && h.DeletedAt == null, ct);
            if (!vive) plan.HistoriaId = null;
        }

        if (plan.HistoriaId is null)
        {
            var creada = await _historias.CreateAsync(new CreateHistoriaRequest(
                Titulo: plan.Nombre,
                Descripcion: DescripcionHistoria(plan),
                Estado: null,                       // BACKLOG: el trabajo todavía no arrancó
                Prioridad: null,
                ResponsableUserGuid: plan.ImplementadorUserId,
                HorasEstimadas: null,
                FechaInicioPlan: ADateOnly(plan.FechaInicio),
                FechaFinPlan: ADateOnly(plan.FechaFin),
                Etiquetas: "implementacion"), ct);

            plan.HistoriaId = creada.Id;
            historiaCreada = true;
        }

        // ── Los puntos ─────────────────────────────────────────────────────────
        var puntos = await _ctx.ImplementacionTareas
            .Where(t => t.PlanId == planId && t.DeletedAt == null)
            .OrderBy(t => t.Orden).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var yaEnlazados = 0;
        var enlazadosAhora = 0;

        foreach (var punto in puntos)
        {
            if (punto.TicketTareaId is { } tid)
            {
                var viva = await _ctx.TicketTareas.AsNoTracking()
                    .AnyAsync(t => t.Id == tid && t.DeletedAt == null, ct);
                if (viva) { yaEnlazados++; continue; }
                punto.TicketTareaId = null;   // borrada en el tablero: se rehace
            }

            var tarea = await _ticketTareas.CrearTareaItalJiraAsync(new CreateTicketTareaRequest(
                Titulo: punto.Titulo,
                Descripcion: punto.Descripcion,
                Tipo: null,
                Estado: null,
                Prioridad: null,
                AsignadoUserGuid: punto.AsignadoUserId,
                ParentTareaId: null,
                HorasEstimadas: null,
                FechaInicioPlan: null,
                FechaFinPlan: ADateOnly(punto.FechaProgramada),
                Etiquetas: punto.Categoria,
                HistoriaId: plan.HistoriaId), ct);

            punto.TicketTareaId = tarea.Id;
            punto.UpdatedByUserId = _current.UserId;
            enlazadosAhora++;
        }

        plan.UpdatedByUserId = _current.UserId;
        await _ctx.SaveChangesAsync(ct);

        var codigo = await _ctx.Historias.AsNoTracking()
            .Where(h => h.Id == plan.HistoriaId!.Value)
            .Select(h => h.Codigo)
            .FirstOrDefaultAsync(ct);

        return new ImplementacionItalJiraDto(
            plan.HistoriaId!.Value, codigo ?? "", historiaCreada, yaEnlazados, enlazadosAhora);
    }

    /// <summary>
    /// Descripción de la historia: la del plan más el rastro de dónde vive, para que quien la abra
    /// desde el tablero sepa que su avance real se firma en Implementación.
    /// </summary>
    private static string DescripcionHistoria(ImplementacionPlan plan)
    {
        var propia = string.IsNullOrWhiteSpace(plan.Descripcion) ? "" : plan.Descripcion.Trim() + "\n\n";
        return propia + $"Plan de implementación #{plan.Id} ({plan.Tipo}). " +
               "Cada tarea corresponde a un punto del checklist; al pasarla a LISTO, el punto queda " +
               "completado y habilita las firmas de los participantes.";
    }

    private static DateOnly? ADateOnly(DateTime? f) => f is null ? null : DateOnly.FromDateTime(f.Value);
}

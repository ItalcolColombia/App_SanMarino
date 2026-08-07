// src/app/features/italjira/services/italjira.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type { TicketTarea, UpdateTicketTareaRequest, MoverTareaRequest, CreateTicketTiempoRequest, TicketTiempo }
  from '../../tickets/models/ticket-tarea.models';
import type {
  AsignarAHistoriaRequest, CreateHistoriaRequest, CreateTareaItalJiraRequest,
  Historia, HistoriaDetalle, ItalJiraBacklog, ItalJiraFiltro, ItalJiraRoadmap, ItalJiraTablero,
  MoverHistoriaRequest, UpdateHistoriaRequest,
} from '../models/historia.models';

/**
 * Servicio HTTP de ItalJira. Consume `ItalJiraController` (`/api/italjira`).
 * El JWT, `X-Secret-Up` y los headers de empresa/país los agrega el `authInterceptor`.
 */
@Injectable({ providedIn: 'root' })
export class ItalJiraService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/italjira`;

  /** Filtro común de las tres vistas: una sola forma de armarlo evita que se desincronicen. */
  private params(filtro?: ItalJiraFiltro): HttpParams {
    let p = new HttpParams();
    if (!filtro) return p;
    if (filtro.estado)    p = p.set('estado', filtro.estado);
    if (filtro.prioridad) p = p.set('prioridad', filtro.prioridad);
    if (filtro.responsable) p = p.set('responsable', filtro.responsable);
    if (filtro.texto)     p = p.set('texto', filtro.texto);
    if (filtro.incluirTerminadas === false) p = p.set('incluirTerminadas', 'false');
    return p;
  }

  // ───────────────────────── Historias ─────────────────────────

  historias(filtro?: ItalJiraFiltro): Observable<Historia[]> {
    return this.http.get<Historia[]>(`${this.base}/historias`, { params: this.params(filtro) });
  }

  historia(id: number): Observable<HistoriaDetalle> {
    return this.http.get<HistoriaDetalle>(`${this.base}/historias/${id}`);
  }

  crearHistoria(req: CreateHistoriaRequest): Observable<Historia> {
    return this.http.post<Historia>(`${this.base}/historias`, req);
  }

  editarHistoria(id: number, req: UpdateHistoriaRequest): Observable<Historia> {
    return this.http.put<Historia>(`${this.base}/historias/${id}`, req);
  }

  moverHistoria(id: number, req: MoverHistoriaRequest): Observable<Historia[]> {
    return this.http.post<Historia[]>(`${this.base}/historias/${id}/mover`, req);
  }

  eliminarHistoria(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/historias/${id}`);
  }

  // ───────────────────────── Agrupar trabajo existente ─────────────────────────

  asignarCaso(ticketId: number, req: AsignarAHistoriaRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/casos/${ticketId}/historia`, req);
  }

  asignarTarea(tareaId: number, req: AsignarAHistoriaRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/tareas/${tareaId}/historia`, req);
  }

  // ───────────────────────── Tareas ─────────────────────────

  tareasDeHistoria(historiaId: number): Observable<TicketTarea[]> {
    return this.http.get<TicketTarea[]>(`${this.base}/historias/${historiaId}/tareas`);
  }

  tareasSinHistoria(): Observable<TicketTarea[]> {
    return this.http.get<TicketTarea[]>(`${this.base}/tareas/sin-historia`);
  }

  crearTarea(req: CreateTareaItalJiraRequest): Observable<TicketTarea> {
    return this.http.post<TicketTarea>(`${this.base}/tareas`, req);
  }

  editarTarea(tareaId: number, req: UpdateTicketTareaRequest): Observable<TicketTarea> {
    return this.http.put<TicketTarea>(`${this.base}/tareas/${tareaId}`, req);
  }

  moverTarea(tareaId: number, req: MoverTareaRequest): Observable<TicketTarea[]> {
    return this.http.post<TicketTarea[]>(`${this.base}/tareas/${tareaId}/mover`, req);
  }

  eliminarTarea(tareaId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/tareas/${tareaId}`);
  }

  registrarTiempo(tareaId: number, req: CreateTicketTiempoRequest): Observable<TicketTiempo> {
    return this.http.post<TicketTiempo>(`${this.base}/tareas/${tareaId}/tiempos`, req);
  }

  // ───────────────────────── Vistas agregadas ─────────────────────────

  backlog(filtro?: ItalJiraFiltro): Observable<ItalJiraBacklog> {
    return this.http.get<ItalJiraBacklog>(`${this.base}/backlog`, { params: this.params(filtro) });
  }

  tablero(filtro?: ItalJiraFiltro): Observable<ItalJiraTablero> {
    return this.http.get<ItalJiraTablero>(`${this.base}/tablero`, { params: this.params(filtro) });
  }

  roadmap(filtro?: ItalJiraFiltro): Observable<ItalJiraRoadmap> {
    return this.http.get<ItalJiraRoadmap>(`${this.base}/roadmap`, { params: this.params(filtro) });
  }
}

// src/app/features/tickets/services/ticket-tarea.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CreateTicketTareaRequest,
  CreateTicketTiempoRequest,
  MoverTareaRequest,
  TicketResumenTiempos,
  TicketTarea,
  TicketTiempo,
  UpdateTicketTareaRequest,
} from '../models/ticket-tarea.models';

/**
 * Tareas de un caso y su registro de tiempos (el tablero tipo Jira).
 * Consume TicketTareasController: `/api/tickets/{ticketId}/tareas|tiempos`.
 */
@Injectable({ providedIn: 'root' })
export class TicketTareaService {
  private readonly base = `${environment.apiUrl}/tickets`;
  private readonly http = inject(HttpClient);

  // ── Tareas ───────────────────────────────────────────────────
  listar(ticketId: number): Observable<TicketTarea[]> {
    return this.http.get<TicketTarea[]>(`${this.base}/${ticketId}/tareas`);
  }

  crear(ticketId: number, req: CreateTicketTareaRequest): Observable<TicketTarea> {
    return this.http.post<TicketTarea>(`${this.base}/${ticketId}/tareas`, req);
  }

  editar(ticketId: number, tareaId: number, req: UpdateTicketTareaRequest): Observable<TicketTarea> {
    return this.http.put<TicketTarea>(`${this.base}/${ticketId}/tareas/${tareaId}`, req);
  }

  /** Suelta la tarjeta en una columna/posición. Devuelve el tablero de tareas recalculado. */
  mover(ticketId: number, tareaId: number, req: MoverTareaRequest): Observable<TicketTarea[]> {
    return this.http.post<TicketTarea[]>(`${this.base}/${ticketId}/tareas/${tareaId}/mover`, req);
  }

  eliminar(ticketId: number, tareaId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${ticketId}/tareas/${tareaId}`);
  }

  // ── Registro de tiempo ───────────────────────────────────────
  listarTiempos(ticketId: number): Observable<TicketTiempo[]> {
    return this.http.get<TicketTiempo[]>(`${this.base}/${ticketId}/tiempos`);
  }

  registrarTiempo(ticketId: number, req: CreateTicketTiempoRequest): Observable<TicketTiempo> {
    return this.http.post<TicketTiempo>(`${this.base}/${ticketId}/tiempos`, req);
  }

  eliminarTiempo(ticketId: number, tiempoId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${ticketId}/tiempos/${tiempoId}`);
  }

  resumenTiempos(ticketId: number): Observable<TicketResumenTiempos> {
    return this.http.get<TicketResumenTiempos>(`${this.base}/${ticketId}/tiempos/resumen`);
  }
}

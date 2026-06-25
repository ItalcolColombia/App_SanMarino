// src/app/features/tickets/services/ticket.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CreateTicketRequest,
  AddTicketImagenesRequest,
  CambiarEstadoTicketRequest,
  CreateTicketNotaRequest,
  TransferirTicketRequest,
  TicketListFilter,
  PagedResult,
  TicketListItem,
  TicketDetail,
  TicketImagen,
  TicketImagenMeta,
  TicketNota,
  ConfirmarCierreRequest,
  AddTicketDocumentoRequest,
  AddTicketLinkRequest,
  TicketAdjunto,
  TicketDocumento,
  ResolutorAdminDto,
} from '../models/ticket.models';

/**
 * Servicio HTTP del módulo de tickets. Consume TicketsController.
 * El JWT, X-Secret-Up y los headers de empresa/país los agrega el authInterceptor,
 * por eso aquí se usa HttpClient "plano".
 */
@Injectable({ providedIn: 'root' })
export class TicketService {
  private readonly baseUrl = `${environment.apiUrl}/tickets`;
  private readonly http = inject(HttpClient);

  // ── Solicitante ──────────────────────────────────────────────
  crear(req: CreateTicketRequest): Observable<TicketDetail> {
    return this.http.post<TicketDetail>(this.baseUrl, req);
  }

  misTickets(filter: TicketListFilter = {}): Observable<PagedResult<TicketListItem>> {
    return this.http.get<PagedResult<TicketListItem>>(
      `${this.baseUrl}/mis-tickets`, { params: this.toParams(filter) });
  }

  getById(id: number): Observable<TicketDetail> {
    return this.http.get<TicketDetail>(`${this.baseUrl}/${id}`);
  }

  getImagenesMeta(id: number): Observable<TicketImagenMeta[]> {
    return this.http.get<TicketImagenMeta[]>(`${this.baseUrl}/${id}/imagenes`);
  }

  /** Imagen on-demand (con base64). Se pide solo al abrir el visor. */
  getImagen(id: number, imagenId: number): Observable<TicketImagen> {
    return this.http.get<TicketImagen>(`${this.baseUrl}/${id}/imagenes/${imagenId}`);
  }

  addImagenes(id: number, req: AddTicketImagenesRequest): Observable<{ added: number }> {
    return this.http.post<{ added: number }>(`${this.baseUrl}/${id}/imagenes`, req);
  }

  addNota(id: number, req: CreateTicketNotaRequest): Observable<TicketNota> {
    return this.http.post<TicketNota>(`${this.baseUrl}/${id}/notas`, req);
  }

  // ── Resolutor ────────────────────────────────────────────────
  gestion(filter: TicketListFilter = {}): Observable<PagedResult<TicketListItem>> {
    return this.http.get<PagedResult<TicketListItem>>(
      `${this.baseUrl}/gestion`, { params: this.toParams(filter) });
  }

  tomar(id: number): Observable<TicketDetail> {
    return this.http.post<TicketDetail>(`${this.baseUrl}/${id}/tomar`, {});
  }

  cambiarEstado(id: number, req: CambiarEstadoTicketRequest): Observable<TicketDetail> {
    return this.http.patch<TicketDetail>(`${this.baseUrl}/${id}/estado`, req);
  }

  /** El solicitante confirma el cierre de un ticket SOLUCIONADO. */
  confirmarCierre(id: number, req: ConfirmarCierreRequest = {}): Observable<TicketDetail> {
    return this.http.post<TicketDetail>(`${this.baseUrl}/${id}/confirmar-cierre`, req);
  }

  // ── Adjuntos (documentos + links) ────────────────────────────
  getAdjuntos(id: number): Observable<TicketAdjunto[]> {
    return this.http.get<TicketAdjunto[]>(`${this.baseUrl}/${id}/adjuntos`);
  }

  addDocumento(id: number, req: AddTicketDocumentoRequest): Observable<TicketAdjunto> {
    return this.http.post<TicketAdjunto>(`${this.baseUrl}/${id}/documentos`, req);
  }

  addLink(id: number, req: AddTicketLinkRequest): Observable<TicketAdjunto> {
    return this.http.post<TicketAdjunto>(`${this.baseUrl}/${id}/links`, req);
  }

  /** Documento on-demand (con base64) para descargar. */
  descargarDocumento(id: number, adjuntoId: number): Observable<TicketDocumento> {
    return this.http.get<TicketDocumento>(`${this.baseUrl}/${id}/adjuntos/${adjuntoId}/descargar`);
  }

  deleteAdjunto(id: number, adjuntoId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}/adjuntos/${adjuntoId}`);
  }

  // ── Bandeja asignados ────────────────────────────────────────
  asignados(filter: TicketListFilter = {}): Observable<PagedResult<TicketListItem>> {
    return this.http.get<PagedResult<TicketListItem>>(
      `${this.baseUrl}/asignados`, { params: this.toParams(filter) });
  }

  transferir(id: number, req: TransferirTicketRequest): Observable<TicketDetail> {
    return this.http.post<TicketDetail>(`${this.baseUrl}/${id}/transferir`, req);
  }

  // ── Super Admin ──────────────────────────────────────────────
  admin(filter: TicketListFilter = {}): Observable<PagedResult<TicketListItem>> {
    return this.http.get<PagedResult<TicketListItem>>(
      `${this.baseUrl}/admin`, { params: this.toParams(filter) });
  }

  getResolutoresAdmin(): Observable<ResolutorAdminDto[]> {
    return this.http.get<ResolutorAdminDto[]>(`${this.baseUrl}/admin/resolutores`);
  }

  // ── Común ────────────────────────────────────────────────────
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  catalogos(): Observable<{ tipos: string[]; estados: string[] }> {
    return this.http.get<{ tipos: string[]; estados: string[] }>(`${this.baseUrl}/catalogos`);
  }

  /** Construye HttpParams omitiendo valores vacíos/undefined. */
  private toParams(obj: TicketListFilter): HttpParams {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(obj)) {
      if (v !== undefined && v !== null && v !== '') {
        params = params.set(k, String(v));
      }
    }
    return params;
  }
}

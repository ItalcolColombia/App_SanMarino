// src/app/features/vacunacion/services/vacunacion.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  VacunacionFilterDataDto,
  VacunacionCronogramaItemDto,
  VacunacionCronogramaItemCreateRequest,
  VacunacionCronogramaItemUpdateRequest,
  VacunacionRegistrarAplicadoRequest,
  VacunacionRegistrarNoAplicadoRequest,
  VacunacionCumplimientoFiltroRequest,
  VacunacionCumplimientoLoteDto,
  LineaProductiva,
} from '../models/vacunacion.model';

@Injectable({ providedIn: 'root' })
export class VacunacionService {
  private readonly cronogramaBase = `${environment.apiUrl}/VacunacionCronograma`;
  private readonly registroBase = `${environment.apiUrl}/VacunacionRegistro`;
  private readonly reportesBase = `${environment.apiUrl}/VacunacionReportes`;

  constructor(private http: HttpClient) {}

  getFilterData(): Observable<VacunacionFilterDataDto> {
    return this.http.get<VacunacionFilterDataDto>(`${this.cronogramaBase}/filter-data`);
  }

  getCronogramaLote(lineaProductiva: LineaProductiva, loteId: number): Observable<VacunacionCronogramaItemDto[]> {
    return this.http.get<VacunacionCronogramaItemDto[]>(`${this.cronogramaBase}/por-lote`, {
      params: { lineaProductiva, loteId },
    });
  }

  crearItem(req: VacunacionCronogramaItemCreateRequest): Observable<VacunacionCronogramaItemDto> {
    return this.http.post<VacunacionCronogramaItemDto>(this.cronogramaBase, req);
  }

  actualizarItem(id: number, req: VacunacionCronogramaItemUpdateRequest): Observable<VacunacionCronogramaItemDto> {
    return this.http.put<VacunacionCronogramaItemDto>(`${this.cronogramaBase}/${id}`, req);
  }

  eliminarItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.cronogramaBase}/${id}`);
  }

  registrarAplicado(cronogramaItemId: number, req: VacunacionRegistrarAplicadoRequest): Observable<VacunacionCronogramaItemDto> {
    return this.http.post<VacunacionCronogramaItemDto>(`${this.registroBase}/${cronogramaItemId}/aplicar`, req);
  }

  registrarNoAplicado(cronogramaItemId: number, req: VacunacionRegistrarNoAplicadoRequest): Observable<VacunacionCronogramaItemDto> {
    return this.http.post<VacunacionCronogramaItemDto>(`${this.registroBase}/${cronogramaItemId}/no-aplicar`, req);
  }

  getCumplimiento(req: VacunacionCumplimientoFiltroRequest): Observable<VacunacionCumplimientoLoteDto[]> {
    return this.http.post<VacunacionCumplimientoLoteDto[]>(`${this.reportesBase}/cumplimiento`, req);
  }
}

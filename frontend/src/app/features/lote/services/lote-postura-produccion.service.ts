import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface LotePosturaProduccionDto {
  lotePosturaProduccionId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId?: string | null;
  galponId?: string | null;
  regional?: string | null;
  fechaEncaset?: string | null;
  hembrasL?: number | null;
  machosL?: number | null;
  pesoInicialH?: number | null;
  pesoInicialM?: number | null;
  raza?: string | null;
  linea?: string | null;
  tecnico?: string | null;
  avesEncasetadas?: number | null;
  edadInicial?: number | null;
  loteErp?: string | null;
  paisId?: number | null;
  paisNombre?: string | null;
  empresaNombre?: string | null;
  companyId: number;
  createdAt?: string | null;
  fechaInicioProduccion?: string | null;
  hembrasInicialesProd?: number | null;
  machosInicialesProd?: number | null;
  lotePosturaLevanteId?: number | null;
  avesHInicial?: number | null;
  avesMInicial?: number | null;
  avesHActual?: number | null;
  avesMActual?: number | null;
  estado?: string | null;
  etapa?: string | null;
  edad?: number | null;
  /** Abierta = producción activa. Cerrada = producción finalizada. */
  estadoCierre?: string | null;
  farm?: {
    id: number;
    name: string;
  } | null;
  nucleo?: {
    nucleoId: string;
    nucleoNombre?: string | null;
    granjaId?: number | null;
  } | null;
  galpon?: {
    galponId: string;
    galponNombre?: string | null;
    nucleoId?: string | null;
    granjaId?: number | null;
  } | null;
}

@Injectable({ providedIn: 'root' })
export class LotePosturaProduccionService {
  private readonly baseUrl = `${environment.apiUrl}/LotePosturaProduccion`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<LotePosturaProduccionDto[]> {
    return this.http.get<LotePosturaProduccionDto[]>(this.baseUrl);
  }

  getByLoteId(loteId: number): Observable<LotePosturaProduccionDto[]> {
    return this.http.get<LotePosturaProduccionDto[]>(`${this.baseUrl}/por-lote/${loteId}`);
  }
}

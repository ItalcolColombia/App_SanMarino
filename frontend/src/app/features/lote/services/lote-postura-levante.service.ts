import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface LotePosturaLevanteDto {
  lotePosturaLevanteId: number;
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
  unifH?: number | null;
  unifM?: number | null;
  mortCajaH?: number | null;
  mortCajaM?: number | null;
  raza?: string | null;
  anoTablaGenetica?: number | null;
  linea?: string | null;
  tipoLinea?: string | null;
  codigoGuiaGenetica?: string | null;
  lineaGeneticaId?: number | null;
  tecnico?: string | null;
  mixtas?: number | null;
  pesoMixto?: number | null;
  avesEncasetadas?: number | null;
  edadInicial?: number | null;
  loteErp?: string | null;
  estadoTraslado?: string | null;
  paisId?: number | null;
  paisNombre?: string | null;
  empresaNombre?: string | null;
  companyId: number;
  createdAt?: string | null;
  loteId?: number | null;
  lotePadreId?: number | null;
  lotePosturaLevantePadreId?: number | null;
  avesHInicial?: number | null;
  avesMInicial?: number | null;
  avesHActual?: number | null;
  avesMActual?: number | null;
  empresaId?: number | null;
  usuarioId?: number | null;
  estado?: string | null;
  etapa?: string | null;
  edad?: number | null;
  estadoCierre?: string | null; // Abierto | Cerrado
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
  /** Máxima edad (semanas) con registros en seguimiento (solo en detalle por ID). */
  edadMaximaSeguimiento?: number | null;
}

@Injectable({ providedIn: 'root' })
export class LotePosturaLevanteService {
  private readonly baseUrl = `${environment.apiUrl}/LotePosturaLevante`;
  private readonly http = inject(HttpClient);

  getByLoteId(loteId: number): Observable<LotePosturaLevanteDto[]> {
    return this.http.get<LotePosturaLevanteDto[]>(`${this.baseUrl}/por-lote/${loteId}`);
  }

  getAll(): Observable<LotePosturaLevanteDto[]> {
    return this.http.get<LotePosturaLevanteDto[]>(this.baseUrl);
  }

  /** Detalle por ID (incluye edadMaximaSeguimiento). */
  getById(id: number): Observable<LotePosturaLevanteDto> {
    return this.http.get<LotePosturaLevanteDto>(`${this.baseUrl}/${id}`);
  }
}

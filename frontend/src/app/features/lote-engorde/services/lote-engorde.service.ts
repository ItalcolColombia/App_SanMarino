import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface LoteAveEngordeDto {
  loteAveEngordeId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId?: string | null;
  galponId?: string | null;
  regional?: string;
  fechaEncaset?: string;
  hembrasL?: number;
  machosL?: number;
  mixtas?: number | null;
  pesoInicialH?: number;
  pesoInicialM?: number;
  pesoMixto?: number | null;
  unifH?: number;
  unifM?: number;
  mortCajaH?: number;
  mortCajaM?: number;
  raza?: string;
  anoTablaGenetica?: number | null;
  linea?: string;
  tipoLinea?: string;
  codigoGuiaGenetica?: string;
  lineaGeneticaId?: number | null;
  tecnico?: string;
  avesEncasetadas?: number | null;
  loteErp?: string;
  edadInicial?: number | null;
  estadoTraslado?: string | null;
  farm?: { id: number; name: string; regionalId?: number | null; departamentoId?: number | null; ciudadId?: number | null } | null;
  nucleo?: { nucleoId: string; nucleoNombre?: string | null; granjaId?: number | null } | null;
  galpon?: { galponId: string; galponNombre?: string | null; nucleoId?: string | null; granjaId?: number | null } | null;
  companyId?: number | null;
  createdByUserId?: number | null;
  createdAt?: string | null;
  updatedByUserId?: number | null;
  updatedAt?: string | null;
  paisId?: number | null;
  paisNombre?: string | null;
  empresaNombre?: string | null;
  /** Abierto | Cerrado (liquidado). */
  estadoOperativoLote?: string | null;
  liquidadoAt?: string | null;
  liquidadoPorUserId?: string | null;
  reabiertoAt?: string | null;
  reabiertoPorUserId?: string | null;
  motivoReapertura?: string | null;
}

export interface CreateLoteAveEngordeDto extends Omit<LoteAveEngordeDto, 'loteAveEngordeId'> {
  loteAveEngordeId?: number;
}

export interface UpdateLoteAveEngordeDto extends LoteAveEngordeDto {}

export interface LoteFormDataResponse {
  farms: Array<{ id: number; name: string; companyId?: number }>;
  nucleos: Array<{ nucleoId: string; nucleoNombre?: string | null; granjaId: number }>;
  galpones: Array<{ galponId: string; galponNombre?: string | null; nucleoId: string; granjaId: number }>;
  tecnicos: Array<{ id?: string; firstName?: string; surName?: string }>;
  companies: Array<{ id: number; name: string }>;
  razas: string[];
}

@Injectable({ providedIn: 'root' })
export class LoteEngordeService {
  private readonly baseUrl = `${environment.apiUrl}/LoteAveEngorde`;
  private readonly http = inject(HttpClient);

  getFormData(): Observable<LoteFormDataResponse> {
    return this.http.get<LoteFormDataResponse>(`${this.baseUrl}/form-data`);
  }

  getAll(): Observable<LoteAveEngordeDto[]> {
    return this.http.get<LoteAveEngordeDto[]>(this.baseUrl);
  }

  getById(loteAveEngordeId: number): Observable<LoteAveEngordeDto> {
    return this.http.get<LoteAveEngordeDto>(`${this.baseUrl}/${loteAveEngordeId}`);
  }

  create(dto: CreateLoteAveEngordeDto): Observable<LoteAveEngordeDto> {
    return this.http.post<LoteAveEngordeDto>(this.baseUrl, dto);
  }

  update(dto: UpdateLoteAveEngordeDto): Observable<LoteAveEngordeDto> {
    return this.http.put<LoteAveEngordeDto>(`${this.baseUrl}/${dto.loteAveEngordeId}`, dto);
  }

  delete(loteAveEngordeId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${loteAveEngordeId}`);
  }

  cerrarLote(loteAveEngordeId: number, closedByUserId: string): Observable<LoteAveEngordeDto> {
    return this.http.post<LoteAveEngordeDto>(`${this.baseUrl}/${loteAveEngordeId}/cerrar`, {
      closedByUserId
    });
  }

  abrirLote(loteAveEngordeId: number, body: { motivo: string; openedByUserId: string }): Observable<LoteAveEngordeDto> {
    return this.http.post<LoteAveEngordeDto>(`${this.baseUrl}/${loteAveEngordeId}/abrir`, body);
  }
}

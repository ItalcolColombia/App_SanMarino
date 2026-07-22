// frontend/src/app/features/engorde-comun/services/lote-base-engorde.api.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

/**
 * Catálogo "Lote base" global de pollo engorde (por empresa). Agrupa varios
 * lote_ave_engorde (distintos galpones) bajo un mismo nombre (ej. "95") para
 * reportes por granja. La asignación en el lote es OPCIONAL.
 * Compartido entre el form de lotes engorde y el Reporte Diario Costos.
 */
export interface LoteBaseEngordeDto {
  id: number;
  nombre: string;
  descripcion?: string | null;
  codigoErp?: string | null;
  lineaGenetica?: string | null;
  /** Fecha de activación (ISO). Vigencia por AÑO: aparece en crear-lote solo durante ese año; null = siempre. */
  fechaActivacion?: string | null;
  /** Desactivación manual: inactivo no aparece en el selector de crear-lote. */
  activo: boolean;
  /** Lotes de engorde vivos amarrados a este lote base. */
  lotesAsignados: number;
  createdAt?: string;
}

export interface LoteBaseEngordePayload {
  nombre: string;
  descripcion?: string | null;
  codigoErp?: string | null;
  lineaGenetica?: string | null;
  /** 'yyyy-MM-dd'. */
  fechaActivacion?: string | null;
}

/**
 * Permisos del módulo (seed en AddLoteBaseEngordeCamposYPermisos; gate 100% frontend
 * vía *appHasPermission). "ver" también controla el campo Lote base del form de lote.
 */
export const PERMISOS_LOTE_BASE = {
  ver: 'lote_base_pollo_engorde.ver',
  crear: 'lote_base_pollo_engorde.crear',
  editar: 'lote_base_pollo_engorde.editar',
  eliminar: 'lote_base_pollo_engorde.eliminar'
} as const;

@Injectable({ providedIn: 'root' })
export class LoteBaseEngordeApi {
  private readonly baseUrl = `${environment.apiUrl}/LoteBaseEngorde`;
  private readonly http = inject(HttpClient);

  /** soloVigentes: solo activos y del año en curso (selector de crear-lote en Panamá). */
  getAll(soloVigentes = false): Observable<LoteBaseEngordeDto[]> {
    return this.http.get<LoteBaseEngordeDto[]>(this.baseUrl, {
      params: soloVigentes ? { soloVigentes: true } : {}
    });
  }

  /** Toggle manual de activación. */
  setActivo(id: number, activo: boolean): Observable<LoteBaseEngordeDto> {
    return this.http.put<LoteBaseEngordeDto>(`${this.baseUrl}/${id}/activo`, { activo });
  }

  create(dto: LoteBaseEngordePayload): Observable<LoteBaseEngordeDto> {
    return this.http.post<LoteBaseEngordeDto>(this.baseUrl, dto);
  }

  update(id: number, dto: LoteBaseEngordePayload & { id: number }): Observable<LoteBaseEngordeDto> {
    return this.http.put<LoteBaseEngordeDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

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
  /** Lotes de engorde vivos amarrados a este lote base. */
  lotesAsignados: number;
  createdAt?: string;
}

export interface LoteBaseEngordePayload {
  nombre: string;
  descripcion?: string | null;
  codigoErp?: string | null;
  lineaGenetica?: string | null;
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

  getAll(): Observable<LoteBaseEngordeDto[]> {
    return this.http.get<LoteBaseEngordeDto[]>(this.baseUrl);
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

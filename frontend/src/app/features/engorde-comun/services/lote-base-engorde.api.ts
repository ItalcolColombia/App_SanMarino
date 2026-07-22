// frontend/src/app/features/engorde-comun/services/lote-base-engorde.api.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

/**
 * Catálogo "Lote base" global de pollo engorde (por empresa). Agrupa varios
 * lote_ave_engorde (distintos galpones) bajo un mismo nombre (ej. "95").
 * La visibilidad al crear lote se parametriza por granja: cada lote base trae sus
 * `granjaIds` y solo aparece en el selector de esas granjas.
 * Compartido entre el form de lotes engorde y el Reporte Diario Costos.
 */
export interface LoteBaseEngordeDto {
  id: number;
  nombre: string;
  descripcion?: string | null;
  codigoErp?: string | null;
  lineaGenetica?: string | null;
  /** Fecha de activación (ISO). Se toma automática al crear; ya NO controla vigencia por año. */
  fechaActivacion?: string | null;
  /** Desactivación manual (apagado global): inactivo no aparece en ningún crear-lote. */
  activo: boolean;
  /** Lotes de engorde vivos amarrados a este lote base. */
  lotesAsignados: number;
  /** Granjas donde el lote base es visible al crear lote (filtro de visibilidad). */
  granjaIds: number[];
  /** Nombre del usuario que creó el lote base. */
  createdByNombre?: string | null;
  createdAt?: string;
}

/** Granja asignada a un lote base. */
export interface LoteBaseEngordeGranjaDto {
  farmId: number;
  farmName: string;
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

  /** Lista los lotes base vivos con sus granjas asignadas (granjaIds) y creador. */
  getAll(): Observable<LoteBaseEngordeDto[]> {
    return this.http.get<LoteBaseEngordeDto[]>(this.baseUrl);
  }

  /** Toggle manual de activación (apagado global). */
  setActivo(id: number, activo: boolean): Observable<LoteBaseEngordeDto> {
    return this.http.put<LoteBaseEngordeDto>(`${this.baseUrl}/${id}/activo`, { activo });
  }

  /** Crea un lote base: solo nombre (fecha y usuario se capturan en el backend). */
  create(nombre: string): Observable<LoteBaseEngordeDto> {
    return this.http.post<LoteBaseEngordeDto>(this.baseUrl, { nombre });
  }

  /** Renombra el lote base. */
  update(id: number, nombre: string): Observable<LoteBaseEngordeDto> {
    return this.http.put<LoteBaseEngordeDto>(`${this.baseUrl}/${id}`, { id, nombre });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Asignación de granjas (visibilidad al crear lote) ────────────────────

  /** Granjas asignadas a un lote base. */
  getGranjas(id: number): Observable<LoteBaseEngordeGranjaDto[]> {
    return this.http.get<LoteBaseEngordeGranjaDto[]>(`${this.baseUrl}/${id}/granjas`);
  }

  /** Asigna una granja al lote base (idempotente). */
  assignGranja(id: number, farmId: number): Observable<LoteBaseEngordeGranjaDto> {
    return this.http.post<LoteBaseEngordeGranjaDto>(`${this.baseUrl}/${id}/granjas`, { farmId });
  }

  /** Quita una granja del lote base. */
  unassignGranja(id: number, farmId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}/granjas/${farmId}`);
  }
}

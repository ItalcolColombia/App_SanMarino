// src/app/features/lote/services/lote-huevo-items.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

/**
 * F7.3 — qué tipos de huevo produce un lote.
 *
 * Espejo de `Application/DTOs/LoteHuevoItemDtos.cs`. El seguimiento diario de PRODUCCIÓN muestra
 * una fila fija por cada ítem declarado y **no ofrece ningún otro**: un lote sin declarar no puede
 * clasificar huevos (fail-closed, decisión del cliente).
 */
export interface LoteHuevoItemDto {
  /** Id de la fila de `lote_huevo_items`. `0` en el listado de disponibles (todavía no existe). */
  id: number;
  loteId: number;
  catalogItemId: number;
  /** Código ERP. Puede venir vacío: el código es opcional en el catálogo. */
  codigo?: string | null;
  nombre: string;
  /** `Primera` | `Pnc` | null — categoría comercial del catálogo. */
  tipoHuevo?: string | null;
  /** `UND` | `KIL` | null. Decide si la cantidad admite decimales. */
  um?: string | null;
  /** El ítem representa «huevo de primera postura»: sujeto a la vigencia por semana (F7.4). */
  primeraPostura: boolean;
  /** El ítem sigue activo en el catálogo. Un ítem dado de baja se conserva declarado pero marcado. */
  itemActivo: boolean;
  /** En `disponibles`: si el lote ya lo declaró. En `getByLote`: siempre true. */
  activo: boolean;
}

/** SET completo: lo que no venga se desactiva. Lista vacía = el lote no declara ninguno. */
export interface AsignarHuevoItemsDto {
  catalogItemIds: number[];
}

@Injectable({ providedIn: 'root' })
export class LoteHuevoItemsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/LoteHuevoItem`;

  /** Tipos de huevo que el lote declara producir, ordenados Primera → Pnc → resto. */
  getByLote(loteId: number): Observable<LoteHuevoItemDto[]> {
    return this.http.get<LoteHuevoItemDto[]>(`${this.baseUrl}/${loteId}`);
  }

  /**
   * Catálogo de huevo elegible para el lote, con `activo` marcando los ya declarados.
   * Se resuelve por la empresa dueña de la GRANJA del lote (igual que el gate de guardado), no por
   * la empresa activa del token — si difirieran, se podría declarar un ítem que después el
   * guardado rechaza.
   */
  getDisponibles(loteId: number): Observable<LoteHuevoItemDto[]> {
    return this.http.get<LoteHuevoItemDto[]>(`${this.baseUrl}/${loteId}/disponibles`);
  }

  /**
   * El mismo catálogo elegible, pero para un lote que TODAVÍA NO EXISTE: el formulario de alta
   * necesita ofrecer los tipos antes del POST. La empresa se resuelve por la granja elegida —el
   * mismo dato del que colgará el lote—, y ninguno viene marcado porque no hay declaración previa.
   */
  getDisponiblesPorGranja(granjaId: number): Observable<LoteHuevoItemDto[]> {
    return this.http.get<LoteHuevoItemDto[]>(`${this.baseUrl}/por-granja/${granjaId}/disponibles`);
  }

  /** Reemplaza el conjunto de tipos de huevo del lote. */
  asignar(loteId: number, dto: AsignarHuevoItemsDto): Observable<LoteHuevoItemDto[]> {
    return this.http.put<LoteHuevoItemDto[]>(`${this.baseUrl}/${loteId}`, dto);
  }
}

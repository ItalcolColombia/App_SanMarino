import { InjectionToken } from '@angular/core';
import { Observable } from 'rxjs';
import type {
  SeguimientoLoteLevanteDto,
  CreateSeguimientoLoteLevanteDto,
  UpdateSeguimientoLoteLevanteDto
} from '../../lote-levante/services/seguimiento-lote-levante.service';

/**
 * Contrato multi-país del CRUD de seguimiento diario de engorde.
 * Cada país lo provee en su ruta:
 *   providers: [{ provide: SeguimientoEngordeCrudApi, useExisting: SeguimientoAvesEngordeService }]
 * (Colombia) o `useExisting: SeguimientoAvesEngordePanamaService` (Panamá).
 */
export abstract class SeguimientoEngordeCrudApi {
  abstract create(dto: CreateSeguimientoLoteLevanteDto): Observable<SeguimientoLoteLevanteDto>;
  abstract update(dto: UpdateSeguimientoLoteLevanteDto): Observable<SeguimientoLoteLevanteDto>;
}

/** Opciones de UI por país para el formulario de seguimiento de engorde. */
export interface EngordeFormOpciones {
  /** Mostrar sección de quintales (QQ) — específico Panamá. */
  mostrarQq: boolean;
}

export const ENGORDE_FORM_OPCIONES = new InjectionToken<EngordeFormOpciones>('ENGORDE_FORM_OPCIONES', {
  factory: () => ({ mostrarQq: false })
});

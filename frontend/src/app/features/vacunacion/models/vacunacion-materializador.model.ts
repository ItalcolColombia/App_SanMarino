// src/app/features/vacunacion/models/vacunacion-materializador.model.ts
// Tipos 1:1 con backend/src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionMaterializadorDtos.cs
import { LineaProductiva } from './vacunacion.model';

/**
 * Qué se le va a hacer a cada vacuna del cronograma.
 *
 * `YaAplicado` y `Manual` son las dos formas de «no se toca»: la primera porque el hecho ya ocurrió,
 * la segunda porque alguien lo decidió a mano para ese lote. `Sobrante` es una fila que salió del
 * plan y el plan ya no reclama — se informa, nunca se borra.
 */
export type AccionMaterializacion =
  | 'Crear'
  | 'Actualizar'
  | 'YaAplicado'
  | 'Manual'
  | 'SinCambios'
  | 'Sobrante';

export interface VacunacionMaterializacionConteosDto {
  faltantes: number;
  actualizables: number;
  yaAplicados: number;
  manuales: number;
  sinCambios: number;
  sobrantes: number;
  /** El backend lo calcula: `faltantes > 0 || actualizables > 0`. */
  escribeAlgo: boolean;
}

export interface VacunacionMaterializacionDetalleDto {
  accion: AccionMaterializacion;
  cronogramaItemId: number | null;
  plantillaItemId: number | null;
  itemInventarioId: number;
  vacunaNombre: string;
  unidadObjetivo: string;
  valorObjetivo: number | null;
  detalle: string | null;
}

/**
 * Impacto sobre el cronograma de un lote. El mismo objeto sirve de vista previa (`aplicado: false`)
 * y de informe posterior (`aplicado: true`), porque los dos salen del mismo cálculo del backend.
 */
export interface VacunacionMaterializacionLoteDto {
  lineaProductiva: LineaProductiva;
  loteId: number;
  loteNombre: string | null;
  granjaId: number;
  galponId: string | null;
  plantillaId: number | null;
  plantillaNombre: string | null;
  motivo: string;
  conteos: VacunacionMaterializacionConteosDto;
  detalle: VacunacionMaterializacionDetalleDto[];
  aplicado: boolean;
  error: string | null;
}

export interface VacunacionMaterializacionMasivaDto {
  plantillaId: number;
  plantillaNombre: string;
  lineaProductiva: LineaProductiva;
  /** Lotes abiertos de la línea que se miraron. */
  lotesEvaluados: number;
  /** De ésos, a cuántos les toca esta plantilla. */
  lotesAlcanzados: number;
  /** De ésos, en cuántos hay algo para escribir. */
  lotesQueEscriben: number;
  conteos: VacunacionMaterializacionConteosDto;
  lotes: VacunacionMaterializacionLoteDto[];
  lotesConError: number;
}

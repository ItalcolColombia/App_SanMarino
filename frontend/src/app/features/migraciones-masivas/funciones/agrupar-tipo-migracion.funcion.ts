// features/migraciones-masivas/funciones/agrupar-tipo-migracion.funcion.ts
import { TipoMigracionCodigo } from '../models/migracion.model';

/**
 * Línea Engorde dentro del catálogo de tipos de migración. Todo lo que no está acá es Postura.
 * Fuente única para el gating por permiso de los tiles (`carga_masiva_pollo_engorde` /
 * `carga_masiva_postura`).
 */
const TIPOS_POLLO_ENGORDE: ReadonlySet<TipoMigracionCodigo> = new Set([
  'LotesPolloEngorde',
  'SeguimientoPolloEngorde',
  'SeguimientoReproductoraEngorde',
  'VentaPolloEngorde'
]);

/** True si el tipo de migración pertenece a la línea Pollo Engorde (vs. Postura). */
export function esTipoPolloEngorde(codigo: TipoMigracionCodigo): boolean {
  return TIPOS_POLLO_ENGORDE.has(codigo);
}

/**
 * Tipos de ESTRUCTURA (fase 1) retirados de la pantalla: la creación masiva de granjas, núcleos y
 * galpones sigue viva en el backend (y el historial la muestra por nombre), pero ya no se ofrece
 * como opción al operario.
 */
const TIPOS_ESTRUCTURA: ReadonlySet<TipoMigracionCodigo> = new Set([
  'Granjas',
  'Nucleos',
  'Galpones'
]);

/** True si el tipo de migración es de estructura (Granjas/Núcleos/Galpones) — oculto en la UI. */
export function esTipoEstructura(codigo: TipoMigracionCodigo): boolean {
  return TIPOS_ESTRUCTURA.has(codigo);
}

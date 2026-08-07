// features/migraciones-masivas/funciones/filtrar-tipos-visibles.funcion.ts
import { TipoMigracionInfo, TipoMigracionCodigo } from '../models/migracion.model';
import { esTipoEstructura, esTipoPolloEngorde } from './agrupar-tipo-migracion.funcion';

/** Permisos que habilitan cada línea del módulo. */
export const PERMISO_POLLO_ENGORDE = 'carga_masiva_pollo_engorde';
export const PERMISO_POSTURA = 'carga_masiva_postura';

/** Permiso que habilita la línea a la que pertenece el tipo. */
export function permisoDeLinea(codigo: TipoMigracionCodigo): string {
  return esTipoPolloEngorde(codigo) ? PERMISO_POLLO_ENGORDE : PERMISO_POSTURA;
}

/**
 * Tipos que se le ofrecen al operario en el paso 1.
 *
 * Se descartan, en este orden:
 *   1. los de ESTRUCTURA (Granjas/Núcleos/Galpones) — retirados de la pantalla, siguen vivos en el
 *      backend y el historial los traduce por nombre;
 *   2. los que no están implementados (`disponible = false`);
 *   3. los de una línea cuyo permiso el usuario NO tiene.
 *
 * El criterio del punto 3 cambió (ago-2026): antes el tile se mostraba en gris con "Sin permisos" y
 * ahora directamente NO se muestra — el usuario solo ve los cargadores que puede usar. Es
 * FAIL-CLOSED: con la lista de permisos vacía (sesión sin cargar, error) no se ofrece nada.
 *
 * Función PURA: recibe la lista de permisos del usuario, no consulta servicios.
 */
export function filtrarTiposVisibles(
  tipos: readonly TipoMigracionInfo[],
  permisos: readonly string[]
): TipoMigracionInfo[] {
  return tipos.filter(t =>
    !esTipoEstructura(t.codigo) &&
    t.disponible &&
    permisos.includes(permisoDeLinea(t.codigo))
  );
}

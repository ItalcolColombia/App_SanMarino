/** Módulo mínimo para agrupar (lo que trae `PermissionModule`). */
export interface ModuloParaAgrupar {
  key: string;
  nombre: string;
  orden: number;
}

/** Un grupo de la lista: un módulo con sus permisos. */
export interface GrupoPermisos<T> {
  /** Key del módulo; `null` = «Sin clasificar». */
  moduloKey: string | null;
  nombre: string;
  items: T[];
}

export const NOMBRE_SIN_CLASIFICAR = 'Sin clasificar';

/**
 * Agrupa permisos por módulo para las pantallas (modal de rol, modal de permisos de empresa).
 *
 * - Un permiso COMPARTIDO (varios módulos) aparece UNA sola vez, bajo su primer módulo según `orden`
 *   — así el total de checkboxes coincide con el total de permisos.
 * - Los que no pertenecen a ningún módulo conocido van al final, en «Sin clasificar».
 * - Grupos vacíos no se devuelven. Se respeta el orden de entrada dentro de cada grupo.
 *
 * Función PURA: sin `this`, sin DI. El componente guarda el resultado en un campo (no la llames
 * desde un getter del template: un array nuevo por ciclo rompe el change detection).
 */
export function agruparPermisosPorModulo<T>(
  items: readonly T[],
  modulosDe: (item: T) => readonly string[] | null | undefined,
  modulos: readonly ModuloParaAgrupar[]
): GrupoPermisos<T>[] {
  const ordenados = [...(modulos ?? [])].sort(
    (a, b) => a.orden - b.orden || a.nombre.localeCompare(b.nombre)
  );
  const posicion = new Map(ordenados.map((m, i) => [m.key.toLowerCase(), i]));

  const grupos: GrupoPermisos<T>[] = ordenados.map(m => ({ moduloKey: m.key, nombre: m.nombre, items: [] }));
  const sinClasificar: GrupoPermisos<T> = { moduloKey: null, nombre: NOMBRE_SIN_CLASIFICAR, items: [] };

  for (const item of items ?? []) {
    const indices = (modulosDe(item) ?? [])
      .map(k => posicion.get((k || '').toLowerCase()))
      .filter((i): i is number => i !== undefined);

    if (indices.length === 0) sinClasificar.items.push(item);
    else grupos[Math.min(...indices)].items.push(item);
  }

  return [...grupos, sinClasificar].filter(g => g.items.length > 0);
}

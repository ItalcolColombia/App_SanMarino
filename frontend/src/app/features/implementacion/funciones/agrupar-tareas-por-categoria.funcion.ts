// src/app/features/implementacion/funciones/agrupar-tareas-por-categoria.funcion.ts
// PURA: agrupa las tareas del plan por categoría preservando el orden del cronograma.
import { ImplementacionTareaDto } from '../models/implementacion.models';

export interface GrupoCategoria {
  categoria: string;
  tareas: ImplementacionTareaDto[];
  total: number;
  /** Completadas + confirmadas (ya pasaron por el check). */
  avance: number;
  confirmadas: number;
}

/**
 * Agrupa por categoría en el orden de primera aparición (las tareas vienen ordenadas por `orden`
 * desde el backend). Devuelve referencias nuevas SOLO al invocarse: llamarla al cargar datos y
 * guardar el resultado en un campo estable (nunca desde un getter del template — NG0103).
 */
export function agruparTareasPorCategoria(tareas: ImplementacionTareaDto[]): GrupoCategoria[] {
  const grupos = new Map<string, GrupoCategoria>();
  for (const t of tareas) {
    let g = grupos.get(t.categoria);
    if (!g) {
      g = { categoria: t.categoria, tareas: [], total: 0, avance: 0, confirmadas: 0 };
      grupos.set(t.categoria, g);
    }
    g.tareas.push(t);
    g.total++;
    if (t.estado === 'completada' || t.estado === 'confirmada') g.avance++;
    if (t.estado === 'confirmada') g.confirmadas++;
  }
  return Array.from(grupos.values());
}

export function trackByTarea(_: number, t: ImplementacionTareaDto): number {
  return t.id;
}

export function trackByGrupo(_: number, g: GrupoCategoria): string {
  return g.categoria;
}

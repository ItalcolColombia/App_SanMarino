// src/app/features/implementacion/funciones/filtrar-tareas.funcion.ts
// PURA: filtrado client-side de los ítems del checklist en el detalle del cronograma.
import { ImplementacionTareaDto } from '../models/implementacion.models';

export interface FiltrosTareas {
  /** Texto libre: título, descripción, responsable o participante (case-insensitive). */
  busqueda: string;
  /** '' = todas las categorías. */
  categoria: string;
  /** '' = todos los estados. */
  estado: string;
  soloVencidas: boolean;
}

export const FILTROS_TAREAS_VACIOS: FiltrosTareas = {
  busqueda: '',
  categoria: '',
  estado: '',
  soloVencidas: false,
};

export function hayFiltrosTareas(f: FiltrosTareas): boolean {
  return !!(f.busqueda.trim() || f.categoria || f.estado || f.soloVencidas);
}

export function filtrarTareas(tareas: ImplementacionTareaDto[], f: FiltrosTareas): ImplementacionTareaDto[] {
  const q = f.busqueda.trim().toLowerCase();
  return tareas.filter((t) => {
    if (f.categoria && t.categoria !== f.categoria) return false;
    if (f.estado && t.estado !== f.estado) return false;
    if (f.soloVencidas && !t.vencida) return false;
    if (!q) return true;
    const enFirmas = t.firmas.some((fi) => fi.nombre.toLowerCase().includes(q));
    return enFirmas || [t.titulo, t.descripcion, t.roleNombre, t.asignadoNombre]
      .some((v) => (v ?? '').toLowerCase().includes(q));
  });
}

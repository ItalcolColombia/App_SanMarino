// src/app/features/implementacion/funciones/filtrar-planes.funcion.ts
// PURA: filtrado client-side de la lista de cronogramas (instantáneo, sin HTTP).
import { ImplementacionPlanDto } from '../models/implementacion.models';

export interface FiltrosPlanes {
  /** Texto libre: nombre, descripción, encargado o creador (case-insensitive). */
  busqueda: string;
  /** '' = todos. */
  tipo: string;
  /** '' = todos. */
  estado: string;
}

export const FILTROS_PLANES_VACIOS: FiltrosPlanes = { busqueda: '', tipo: '', estado: '' };

export function hayFiltrosPlanes(f: FiltrosPlanes): boolean {
  return !!(f.busqueda.trim() || f.tipo || f.estado);
}

export function filtrarPlanes(planes: ImplementacionPlanDto[], f: FiltrosPlanes): ImplementacionPlanDto[] {
  const q = f.busqueda.trim().toLowerCase();
  return planes.filter((p) => {
    if (f.tipo && p.tipo !== f.tipo) return false;
    if (f.estado && p.estado !== f.estado) return false;
    if (!q) return true;
    return [p.nombre, p.descripcion, p.implementadorNombre, p.creadoPorNombre]
      .some((v) => (v ?? '').toLowerCase().includes(q));
  });
}

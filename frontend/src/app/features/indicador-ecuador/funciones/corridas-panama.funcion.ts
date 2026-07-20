/**
 * Corridas Panamá — funciones PURAS (sin `this`, sin DI, sin estado).
 *
 * En Panamá el `loteNombre` ES el número de corrida (ej. "94") y se repite en varios
 * galpones de la misma granja (una fila de lote por galpón): la corrida agrupa esos lotes.
 * Ecuador no pasa por aquí (usa el prefijo Año-Corrida YYCC de `cascada-filtros.funcion`).
 */
import { PeLoteAveEngordeItem } from '../models/indicador-filtros.model';

/** Corridas distintas del alcance (nombres trim, sin vacíos), orden numérico descendente (más reciente primero). */
export function corridasDisponiblesPanama(lotes: PeLoteAveEngordeItem[]): string[] {
  const set = new Set<string>();
  for (const l of lotes) {
    const nombre = (l.loteNombre ?? '').trim();
    if (nombre) set.add(nombre);
  }
  return Array.from(set).sort((a, b) =>
    b.localeCompare(a, undefined, { numeric: true, sensitivity: 'base' })
  );
}

/**
 * Lotes de una corrida (match EXACTO del nombre, trim). Corrida null/vacía ⇒ devuelve la
 * MISMA referencia `lotes` (estabilidad para change detection).
 */
export function filtrarLotesPorCorridaPanama(
  lotes: PeLoteAveEngordeItem[],
  corrida: string | null
): PeLoteAveEngordeItem[] {
  const c = (corrida ?? '').trim();
  if (!c) return lotes;
  return lotes.filter(l => (l.loteNombre ?? '').trim() === c);
}

/**
 * Helpers de formato compartidos (número / fecha / nombre de archivo).
 *
 * Centraliza patrones que hoy están duplicados en ~50 archivos de features
 * (formatearNumero / fechaCorta / date-stamp de nombre de archivo). Funciones
 * PURAS, sin estado de Angular. El comportamiento está calcado de las
 * implementaciones canónicas de `movimientos-pollo-engorde/funciones/formato.funcion.ts`
 * para no cambiar salidas al migrar consumidores (refactor ≠ cambio de comportamiento).
 */

/** Número con separadores de miles en español de Colombia (1234 → "1.234"). */
export function formatearNumero(n: number): string {
  return new Intl.NumberFormat('es-CO').format(n);
}

/** Fecha legible corta (`dd/mm/aaaa`); `—` si no hay valor, y el original si no parsea. */
export function fechaCorta(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return isNaN(d.getTime()) ? iso : d.toLocaleDateString('es');
}

/** Fecha y hora legibles (`dd/mm/aaaa, hh:mm`); `—` si no hay valor, y el original si no parsea. */
export function fechaHoraCorta(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return isNaN(d.getTime()) ? iso : d.toLocaleString('es');
}

/**
 * Convierte `YYYY-MM-DD` (input date) a ISO estable a mediodía UTC, evitando el
 * desfase de zona horaria que dejaría la fecha en el día anterior/siguiente.
 */
export function ymdToIsoUtcNoon(ymd: string | null | undefined): string | null {
  const s = (ymd ?? '').trim();
  if (!s) return null;
  const m = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!m) return null;
  return `${m[1]}-${m[2]}-${m[3]}T12:00:00Z`;
}

/**
 * Sello de fecha compacto para nombres de archivo: `YYYYMMDD` (fecha local).
 * Es el patrón repetido en ~44 exports (`new Date()` + `padStart(2,'0')`).
 */
export function dateStampCompact(d: Date = new Date()): string {
  return `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
}

/** Sanitiza un texto para usarlo como nombre de archivo (quita `\ / : * ? " < > |`). */
export function sanitizeFileName(name: string): string {
  return (name || '').replace(/[\\/:*?"<>|]/g, '_');
}

// Re-export de conveniencia del helper decimal ya existente (una sola puerta de entrada).
export { formatDecimalTrim } from './format-decimal';

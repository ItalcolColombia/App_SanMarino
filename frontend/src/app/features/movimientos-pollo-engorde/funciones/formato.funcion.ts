/**
 * Helpers de formato compartidos por el listado y el modal del módulo.
 *
 * Antes estaban duplicados en ambos componentes; se centralizan aquí (clean code: una sola
 * fuente de verdad) y son funciones puras fáciles de testear y reutilizar por país.
 */

/** Formatea un número con separadores de miles en español de Colombia. */
export function formatearNumero(n: number): string {
  return new Intl.NumberFormat('es-CO').format(n);
}

/** Fecha legible corta (`dd/mm/aaaa`); devuelve `—` si no hay valor y el original si no parsea. */
export function fechaCorta(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return isNaN(d.getTime()) ? iso : d.toLocaleDateString('es');
}

/**
 * Convierte `YYYY-MM-DD` (input date) a ISO estable sin desfase de zona horaria.
 * Usamos mediodía UTC para evitar quedar en el día anterior/siguiente por offset.
 */
export function ymdToIsoUtcNoon(ymd: string | null | undefined): string | null {
  const s = (ymd ?? '').trim();
  if (!s) return null;
  // Esperado: 2026-04-07
  const m = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!m) return null;
  return `${m[1]}-${m[2]}-${m[3]}T12:00:00Z`;
}

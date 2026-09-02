/**
 * Helpers de formato del módulo Indicador — funciones PURAS (sin `this`, sin DI, sin estado).
 *
 * NOTA: NO se reexporta el `formatearNumero` central de `shared/utils/format.ts` porque ese usa
 * separadores de miles (Intl es-CO) y aquí la salida es `toFixed(decimales)` — migrarlo cambiaría
 * el resultado en la planilla (refactor ≠ cambio de comportamiento). Se conserva la lógica exacta.
 */

/** Número con `decimales` fijos; `'-'` si es null/undefined. */
export function formatearNumero(valor: number | null | undefined, decimales: number = 2): string {
  if (valor == null) return '-';
  return valor.toFixed(decimales);
}

/** Porcentaje con 2 decimales y símbolo `%`; `'-'` si es null/undefined. */
export function formatearPorcentaje(valor: number | null | undefined): string {
  if (valor == null) return '-';
  return `${valor.toFixed(2)}%`;
}

/**
 * Fecha guardada como timestamptz a medianoche UTC (fecha "pura", sin hora real).
 * OJO: NO usar `new Date(fecha).toLocaleDateString()` — convierte a la zona horaria
 * local del navegador y en Ecuador (UTC-5) corre la fecha un día hacia atrás.
 * Se extrae YYYY-MM-DD directo del ISO, sin pasar por conversión de zona horaria.
 */
export function formatearFechaLote(fecha: string | null | undefined): string {
  if (!fecha) return '—';
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(fecha);
  if (m) return `${m[3]}/${m[2]}/${m[1]}`;
  const d = new Date(fecha);
  return isNaN(d.getTime()) ? fecha : d.toLocaleDateString('es-EC', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

/** Sanitiza un texto para usarlo como nombre de hoja de Excel (máx. 31 chars, sin `\ / ? * [ ] :`). */
export function sanitizarNombreHoja(nombre: string): string {
  return nombre.replace(/[\\\/\?\*\[\]\:]/g, '').substring(0, 31).trim();
}

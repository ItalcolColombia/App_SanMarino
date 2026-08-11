/**
 * Formatea una cantidad de bytes para la pantalla de diagnóstico.
 *
 * Función PURA. Vive acá y no en `shared/utils/format.ts` a propósito: `format.ts` no tiene
 * hoy un equivalente, y CLAUDE.md prohíbe migrar a la fuerza helpers cuya salida cambiaría.
 * Si otra pantalla lo necesita, se promueve a `shared/utils/` con este mismo nombre y firma.
 *
 * Usa unidades binarias (1024) porque es lo que reporta `navigator.storage.estimate()`.
 * Devuelve `'—'` ante `undefined`/`null` (el navegador no siempre expone la cuota) para que
 * el diagnóstico no muestre un `0 B` que se leería como "no hay nada guardado".
 */
export function formatearBytes(bytes: number | null | undefined, decimales = 1): string {
  if (bytes === null || bytes === undefined || Number.isNaN(bytes)) {
    return '—';
  }
  if (bytes < 0) {
    return '—';
  }
  if (bytes === 0) {
    return '0 B';
  }

  const unidades = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), unidades.length - 1);
  const valor = bytes / Math.pow(1024, i);

  // Los bytes crudos no llevan decimales: "512 B", no "512.0 B".
  const dec = i === 0 ? 0 : decimales;

  return `${valor.toFixed(dec)} ${unidades[i]}`;
}

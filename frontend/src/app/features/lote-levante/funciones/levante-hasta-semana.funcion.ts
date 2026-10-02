/**
 * Límite de semanas del seguimiento diario de LEVANTE por empresa (`companies.levante_hasta_semana`).
 *
 * Funciones PURAS (sin `this`, sin DI, sin estado). Espejo de
 * `LevanteSemanaLimiteCalculos` (backend, el autoritativo): con el límite N se registra hasta el
 * último día de la semana N de vida (`encaset + N·7 − 1`; el día del encaset es la semana 1). Desde la
 * semana N+1 hay que cerrar el lote. `null` (o sin fecha de encaset) = sin límite, como siempre.
 */

import { aFechaMediodiaLocal, semanaVidaLevante } from './semana-vida-levante.funcion';

const MS_POR_DIA = 24 * 60 * 60 * 1000;

function aYmd(d: Date): string {
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

/** Último día permitido (`yyyy-MM-dd`), o `null` si no hay límite o no hay fecha de encaset. */
export function fechaLimiteLevante(
  fechaEncaset: string | Date | null | undefined,
  hastaSemana: number | null
): string | null {
  const encaset = aFechaMediodiaLocal(fechaEncaset);
  if (!encaset || hastaSemana == null || hastaSemana <= 0) return null;
  return aYmd(new Date(encaset.getTime() + (hastaSemana * 7 - 1) * MS_POR_DIA));
}

/** ¿Se admite un registro de levante con esta fecha? */
export function permiteRegistroLevante(
  fechaEncaset: string | Date | null | undefined,
  fechaRegistro: string | Date | null | undefined,
  hastaSemana: number | null
): boolean {
  if (hastaSemana == null || hastaSemana <= 0) return true;
  const semana = semanaVidaLevante(fechaEncaset, fechaRegistro);
  return semana == null || semana <= hastaSemana;
}

/** `max` del date picker: el menor entre la ventana vigente y el último día del levante. */
export function maxFechaRegistroLevante(maxVentana: string, fechaLimite: string | null): string {
  if (!fechaLimite) return maxVentana;
  if (!maxVentana) return fechaLimite;
  return fechaLimite < maxVentana ? fechaLimite : maxVentana;
}

/** Mensaje para el operario (mismo contenido que el 400 del backend). */
export function mensajeLevanteFueraDeLimite(
  hastaSemana: number,
  fechaLimite: string,
  semanaRegistro: number | null
): string {
  const [y, m, d] = fechaLimite.split('-');
  const deSemana = semanaRegistro != null ? `; este registro es de la semana ${semanaRegistro}` : '';
  return `El levante de esta empresa se registra hasta la semana ${hastaSemana} de vida del lote ` +
    `(último día: ${d}/${m}/${y})${deSemana}. Cierre el levante del lote para continuar en producción.`;
}

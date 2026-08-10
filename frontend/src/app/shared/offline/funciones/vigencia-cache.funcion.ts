import type { Vigencia } from '../models/offline.model';

/**
 * Ventana de vigencia de una consulta guardada: **16 horas**.
 *
 * Es la jornada offline de la decisión D4 del plan madre. La sesión offline dura una jornada, así
 * que el dato tampoco debería sobrevivirla.
 */
export const TTL_CONSULTA_MS = 16 * 60 * 60 * 1000;

/**
 * ¿Se puede servir esta entrada?
 *
 * ## Por qué el TTL es DURO y no "se sirve igual avisando"
 *
 * La tentación es servir siempre lo último que haya, con un cartel de "datos de hace 3 días". No
 * alcanza: el operario mira el saldo de alimento del galpón, ve un número, y toma una decisión. Un
 * cartel no compite con un número concreto en pantalla. Pasado el plazo se propaga el error de red,
 * que es honesto y no se puede malinterpretar.
 *
 * Un `guardadoEn` en el **futuro** también vence: significa que el reloj del dispositivo cambió, y
 * con el reloj corrido no se puede afirmar nada sobre la antigüedad del dato.
 */
export function vigenciaCache(guardadoEn: number, ahora: number, ttlMs = TTL_CONSULTA_MS): Vigencia {
  if (!Number.isFinite(guardadoEn) || !Number.isFinite(ahora)) {
    return 'vencida';
  }

  const edad = ahora - guardadoEn;

  if (edad < 0) {
    // Guardado "en el futuro": el reloj se movió. No se puede razonar sobre la antigüedad.
    return 'vencida';
  }

  return edad <= ttlMs ? 'vigente' : 'vencida';
}

/**
 * Antigüedad en texto corto para el aviso de la UI ("hace 3 h").
 * Se muestra junto al dato, no en su lugar: acompaña al TTL, no lo reemplaza.
 */
export function antiguedadLegible(guardadoEn: number, ahora: number): string {
  const edad = ahora - guardadoEn;

  if (!Number.isFinite(edad) || edad < 0) {
    return 'fecha desconocida';
  }

  const minutos = Math.floor(edad / 60000);
  if (minutos < 1) return 'hace instantes';
  if (minutos < 60) return `hace ${minutos} min`;

  const horas = Math.floor(minutos / 60);
  return horas === 1 ? 'hace 1 h' : `hace ${horas} h`;
}

/**
 * Semana de vida del lote y gate de captura de huevos en LEVANTE.
 *
 * Funciones PURAS (sin `this`, sin DI, sin estado). Replican la fórmula canónica del backend
 * (`HuevosLevanteCalculos.SemanaVida` / `MovimientoAvesCalculos.SemanaDesdeEncaset`) y del SQL de
 * las fns de indicadores:
 *
 * ```
 * semana = floor((fecha - fechaEncaset) / 7) + 1     // el día del encaset es la SEMANA 1
 * ```
 *
 * El backend es el autoritativo: acá el cálculo sólo decide si mostrar el tab «Huevos» (fijo desde
 * jul-2026; sólo se oculta con la fecha del registro anterior al encaset).
 */

const MS_POR_DIA = 24 * 60 * 60 * 1000;

/**
 * Convierte `yyyy-MM-dd` (o una fecha ISO / Date) a un Date anclado al MEDIODÍA local.
 * El anclaje a mediodía es el patrón del repo para fechas puras: evita que el desplazamiento de
 * zona horaria corra el día (y con él la semana) al parsear.
 */
export function aFechaMediodiaLocal(valor: string | Date | null | undefined): Date | null {
  if (!valor) return null;

  if (valor instanceof Date) {
    if (Number.isNaN(valor.getTime())) return null;
    return new Date(valor.getFullYear(), valor.getMonth(), valor.getDate(), 12, 0, 0, 0);
  }

  const texto = String(valor).trim();
  if (!texto) return null;

  // 'yyyy-MM-dd' o 'yyyy-MM-ddTHH:mm:ss...' → se toma sólo la parte de fecha.
  const ymd = texto.slice(0, 10);
  const d = new Date(`${ymd}T12:00:00`);
  return Number.isNaN(d.getTime()) ? null : d;
}

/**
 * Semana de vida (1-based) del lote en la fecha indicada. `null` si falta cualquiera de las dos
 * fechas o si no son parseables.
 */
export function semanaVidaLevante(
  fechaEncaset: string | Date | null | undefined,
  fechaRegistro: string | Date | null | undefined
): number | null {
  const encaset = aFechaMediodiaLocal(fechaEncaset);
  const registro = aFechaMediodiaLocal(fechaRegistro);
  if (!encaset || !registro) return null;

  const dias = Math.round((registro.getTime() - encaset.getTime()) / MS_POR_DIA);
  return Math.floor(dias / 7) + 1;
}

/**
 * ¿Se puede capturar huevos en levante para este registro?
 *
 * El tab «Huevos» es FIJO (decisión jul-2026: sin gate de semana de vida). La única condición que
 * queda es que la fecha del registro no sea ANTERIOR al encaset; si falta alguna de las dos fechas
 * no hay condición evaluable y se permite (mismo criterio que el backend, que es el que valida).
 */
export function permiteHuevosEnLevante(
  fechaEncaset: string | Date | null | undefined,
  fechaRegistro: string | Date | null | undefined
): boolean {
  const encaset = aFechaMediodiaLocal(fechaEncaset);
  const registro = aFechaMediodiaLocal(fechaRegistro);
  if (!encaset || !registro) return true;
  return registro.getTime() >= encaset.getTime();
}

/**
 * Numeración de DÍA de negocio de un lote de pollo engorde y regla de pesaje obligatorio.
 * Espejo puro de `EncasetamientoCalculos` / `PesajeEngordeCalculos` del backend.
 * Sin `this`, sin DI, sin estado de Angular: reciben datos y devuelven un resultado.
 *
 * Regla: el PRIMER DÍA CON REGISTRO del lote es el **día 1**. Si la empresa tiene activa la regla
 * de la hora de llegada y las aves llegaron a las 13:00 o después, ese primer día es el siguiente
 * al encasetamiento (la fecha de encasetamiento no cambia nunca).
 *
 * Ojo: esto NO reemplaza a la edad. La edad (`fecha − fecha_encaset`, 0 el día del encaset) sigue
 * siendo la que usan la guía genética, los indicadores, el informe semanal y la liquidación; acá
 * solo se resuelve el número que ve y cuenta el usuario.
 */

/** Hora de corte, INCLUSIVE: 13:00 en punto ya cuenta como llegada tardía. */
export const HORA_CORTE_ENCASETAMIENTO = '13:00';

/** Días de la primera semana, en los que el pesaje es diario. */
export const DIAS_PESAJE_DIARIO = 7;

/**
 * Días que se corre el primer día con registro respecto del encasetamiento: 0 o 1.
 * Lo decide la HORA DEL LOTE: desde las 13:00 (inclusive) las aves no consumen el día del encaset.
 * Fail-closed: sin hora informada ⇒ 0 (comportamiento previo).
 *
 * Ya NO se gatea por el flag de empresa (28-ago-2026): el formulario ofrece el campo «Hora de
 * encasetamiento» con su leyenda a todas las empresas y con el gate puesto Ecuador lo llenó 16
 * veces —todas ≥ 13:00— sin efecto alguno. El flag sigue gobernando SOLO el día de pesaje, abajo.
 */
export function desplazamientoPrimerDia(horaEncasetamiento: string | null | undefined): number {
  const h = (horaEncasetamiento ?? '').toString().trim();
  return h.length >= 5 && h.slice(0, 5) >= HORA_CORTE_ENCASETAMIENTO ? 1 : 0;
}

/**
 * Número de día de negocio a partir de la EDAD que ya devuelve el backend (0 el día del encaset).
 * Devuelve 0 o negativo si la fecha es anterior al primer día con registro.
 */
export function diaDeNegocioDesdeEdad(edadDia: number, desplazamiento: number): number {
  return edadDia - desplazamiento + 1;
}

/**
 * Semana de negocio: los días 1..7 son la semana 1, 8..14 la 2, etc.
 * Con desplazamiento 0 coincide exactamente con la semana que devuelve
 * `fn_seguimiento_diario_engorde` (`ceil((edad+1)/7)`) ⇒ no cambia nada en los lotes no tardíos.
 * Días ≤ 0 devuelven 0.
 */
export function semanaDeNegocio(diaDeNegocio: number): number {
  return diaDeNegocio <= 0 ? 0 : Math.ceil(diaDeNegocio / DIAS_PESAJE_DIARIO);
}

/**
 * True si el número de día recibido es día de pesaje obligatorio: 1..7 (diario durante la primera
 * semana) o, a partir del 8, cada múltiplo de 7 (cierre de cada semana).
 */
export function esDiaDePesajeObligatorio(dia: number): boolean {
  return (
    (dia >= 1 && dia <= DIAS_PESAJE_DIARIO) ||
    (dia > DIAS_PESAJE_DIARIO && dia % DIAS_PESAJE_DIARIO === 0)
  );
}

/**
 * Número de día sobre el que se evalúa la regla de pesaje: el día de negocio si la empresa tiene
 * activa la regla de la hora de llegada, o la EDAD cruda si no — que es literalmente el
 * comportamiento histórico, mismo set de días.
 */
export function diaParaReglaDePesaje(edad: number, diaDeNegocio: number, reglaActiva: boolean): number {
  return reglaActiva ? diaDeNegocio : edad;
}

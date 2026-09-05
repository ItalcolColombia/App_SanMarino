// Marca, para una lista de registros diarios YA ORDENADA cronológicamente, cuál es el primero de
// su día y qué lugar ocupa dentro del día.
//
// POR QUÉ EXISTE
// Con `companies.permite_multiples_seguimientos_diarios` (Santa Reyes) un lote puede tener más de
// un registro el mismo día — dos turnos. La grilla de «Registros Diarios» pinta UNA FILA POR
// REGISTRO a propósito (cada fila lleva sus botones Ver / Validar / Editar / Eliminar atados al id;
// agrupar los registros del día dejaría el segundo sin forma de corregirse), así que lo que hay que
// evitar no es la fila: es que la MISMA fecha, semana y edad se repitan como si fueran dos días
// distintos, y que el resumen de inventario del día se cuente dos veces.
//
// Es un cálculo PURO sobre las fechas: con un registro por día devuelve `ordinal = 1` y
// `esPrimeraDelDia = true` para todas las filas, así que para el resto de las empresas —donde el
// alta sigue rechazando el segundo registro del día— el resultado es idéntico al de siempre.

/** Lugar de un registro dentro de su día calendario. */
export interface PosicionEnElDia {
  /** 1 = primer registro de ese día, 2 = segundo, … (en el orden cronológico recibido). */
  ordinal: number;
  /** Cuántos registros tiene ese día en total. 1 en el caso normal. */
  total: number;
  /** `true` sólo para el primer registro del día: es el que rotula fecha/semana/edad. */
  esPrimero: boolean;
}

/**
 * Calcula la posición en el día de cada elemento de una lista **ya ordenada**.
 *
 * @param fechas Fecha de cada registro como `YYYY-MM-DD`, o `null` si no se pudo resolver. Los
 *   `null` no se agrupan entre sí: cada uno queda como su propio día (1 de 1), que es el
 *   comportamiento conservador — no hay evidencia de que dos fechas irresolubles sean el mismo día.
 * @returns Un elemento por cada entrada de `fechas`, en el mismo orden.
 */
export function posicionesEnElDia(fechas: readonly (string | null)[]): PosicionEnElDia[] {
  const totalPorDia = new Map<string, number>();
  for (const ymd of fechas) {
    if (!ymd) continue;
    totalPorDia.set(ymd, (totalPorDia.get(ymd) ?? 0) + 1);
  }

  const vistos = new Map<string, number>();
  return fechas.map(ymd => {
    if (!ymd) return { ordinal: 1, total: 1, esPrimero: true };
    const ordinal = (vistos.get(ymd) ?? 0) + 1;
    vistos.set(ymd, ordinal);
    return { ordinal, total: totalPorDia.get(ymd) ?? 1, esPrimero: ordinal === 1 };
  });
}

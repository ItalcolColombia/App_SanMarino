/**
 * Vista previa del prorrateo de peso de un DESPACHO YA REGISTRADO (líneas que vienen del backend),
 * para el modal de registro de peso de las empresas con báscula diferida.
 *
 * Es el mismo algoritmo que `calcularProrateoPreview` (3 decimales, residuo a la línea con más
 * aves, espejo de `MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea`), pero sobre movimientos
 * ya creados en vez de líneas en edición. Función pura: sin `this`, sin DI, sin estado de Angular.
 */

/** Lo mínimo que se necesita de cada movimiento del despacho. */
export interface LineaDespachoPeso {
  id: number;
  loteNombre: string;
  galponLabel: string;
  aves: number;
}

export interface ProrateoDespachoRow extends LineaDespachoPeso {
  pct: number;
  bruto: number | null;
  tara: number | null;
  neto: number | null;
  promedioPorAve: number | null;
}

const r3 = (n: number): number => Math.round(n * 1000) / 1000;

/**
 * Reparte el peso del camión entre las líneas del despacho, en proporción a sus aves.
 * Devuelve `[]` si no hay líneas. Con peso nulo devuelve las filas sin valores de peso (sirve para
 * mostrar la tabla vacía mientras el usuario todavía no digitó la báscula).
 */
export function calcularProrateoDespacho(
  lineas: LineaDespachoPeso[],
  pesoBruto: number | null,
  pesoTara: number | null
): ProrateoDespachoRow[] {
  if (lineas.length === 0) return [];

  const hayPeso = pesoBruto != null && pesoTara != null;
  const pesoNeto = hayPeso ? pesoBruto! - pesoTara! : null;
  const totalAves = lineas.reduce((s, l) => s + l.aves, 0);

  const rows: ProrateoDespachoRow[] = lineas.map((l) => {
    const factor = totalAves > 0 ? l.aves / totalAves : 0;
    const neto = pesoNeto != null ? r3(pesoNeto * factor) : null;
    return {
      ...l,
      pct: totalAves > 0 ? factor * 100 : 0,
      bruto: hayPeso ? r3(pesoBruto! * factor) : null,
      tara: hayPeso ? r3(pesoTara! * factor) : null,
      neto,
      promedioPorAve: neto != null && l.aves > 0 ? neto / l.aves : null
    };
  });

  // Residuo de redondeo a la línea con más aves (mismo criterio que el backend).
  if (hayPeso && totalAves > 0) {
    const maxIdx = rows.reduce((mi, r, i, a) => (r.aves > a[mi].aves ? i : mi), 0);
    const resBruto = r3(pesoBruto! - rows.reduce((s, r) => s + (r.bruto ?? 0), 0));
    const resTara = r3(pesoTara! - rows.reduce((s, r) => s + (r.tara ?? 0), 0));
    const resNeto = r3(pesoNeto! - rows.reduce((s, r) => s + (r.neto ?? 0), 0));
    const ajustada = rows[maxIdx];
    const netoAjustado = r3((ajustada.neto ?? 0) + resNeto);
    rows[maxIdx] = {
      ...ajustada,
      bruto: r3((ajustada.bruto ?? 0) + resBruto),
      tara: r3((ajustada.tara ?? 0) + resTara),
      neto: netoAjustado,
      promedioPorAve: ajustada.aves > 0 ? netoAjustado / ajustada.aves : null
    };
  }
  return rows;
}

/** Totales de la tabla (el neto total debe coincidir exactamente con bruto − tara del camión). */
export function totalesProrateoDespacho(rows: ProrateoDespachoRow[]): {
  aves: number;
  bruto: number | null;
  tara: number | null;
  neto: number | null;
} {
  if (rows.length === 0) return { aves: 0, bruto: null, tara: null, neto: null };
  const hayPeso = rows.some((r) => r.bruto != null);
  return {
    aves: rows.reduce((s, r) => s + r.aves, 0),
    bruto: hayPeso ? r3(rows.reduce((s, r) => s + (r.bruto ?? 0), 0)) : null,
    tara: hayPeso ? r3(rows.reduce((s, r) => s + (r.tara ?? 0), 0)) : null,
    neto: hayPeso ? r3(rows.reduce((s, r) => s + (r.neto ?? 0), 0)) : null
  };
}

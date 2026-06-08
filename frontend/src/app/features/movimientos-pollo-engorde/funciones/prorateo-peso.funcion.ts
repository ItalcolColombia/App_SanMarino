/**
 * Prorrateo de pesos por lote en una venta por granja (despacho multi-lote).
 *
 * Distribuye el peso bruto / tara / neto del despacho proporcionalmente a las aves de cada línea,
 * ajustando el residuo de redondeo al lote con más aves (espejo del algoritmo del backend). Es la
 * vista previa que ve el usuario antes de guardar. Funciones puras sin estado de Angular.
 */
import { VentaLineaGranja } from '../models/venta-granja.model';

export interface ProrateoRow {
  galponLabel: string;
  loteNombre: string;
  aves: number;
  pct: number;
  bruto: number | null;
  tara: number | null;
  neto: number | null;
}

export interface ProrateoTotales {
  aves: number;
  pct: number;
  bruto: number | null;
  tara: number | null;
  neto: number | null;
}

/**
 * Calcula la distribución proporcional de pesos por línea. Devuelve `[]` cuando hay menos de dos
 * líneas activas (no hay nada que prorratear). Espejo del backend con ajuste de residuo.
 */
export function calcularProrateoPreview(
  lineas: VentaLineaGranja[],
  pesoBruto: number | null,
  pesoTara: number | null
): ProrateoRow[] {
  if (lineas.length < 2) return [];
  const pesoNeto = pesoBruto != null && pesoTara != null ? pesoBruto - pesoTara : null;
  const totalAves = lineas.reduce((s, l) => s + l.h + l.m + l.x, 0);

  const rows: ProrateoRow[] = lineas.map((l) => {
    const aves = l.h + l.m + l.x;
    const factor = totalAves > 0 ? aves / totalAves : 0;
    return {
      galponLabel: l.galponLabel,
      loteNombre: l.loteNombre,
      aves,
      pct: totalAves > 0 ? factor * 100 : 0,
      bruto: pesoBruto != null ? Math.round(pesoBruto * factor * 1000) / 1000 : null,
      tara: pesoTara != null ? Math.round(pesoTara * factor * 1000) / 1000 : null,
      neto: pesoNeto != null ? Math.round(pesoNeto * factor * 1000) / 1000 : null
    };
  });

  // Ajuste de residuo al lote con mayor cantidad de aves (espejo del backend).
  if (pesoBruto != null) {
    const maxIdx = rows.reduce((mi, r, i, a) => (r.aves > a[mi].aves ? i : mi), 0);
    const resBruto = Math.round((pesoBruto - rows.reduce((s, r) => s + (r.bruto ?? 0), 0)) * 1000) / 1000;
    const resTara = pesoTara != null ? Math.round((pesoTara - rows.reduce((s, r) => s + (r.tara ?? 0), 0)) * 1000) / 1000 : 0;
    const resNeto = pesoNeto != null ? Math.round((pesoNeto - rows.reduce((s, r) => s + (r.neto ?? 0), 0)) * 1000) / 1000 : 0;
    rows[maxIdx] = {
      ...rows[maxIdx],
      bruto: rows[maxIdx].bruto != null ? Math.round((rows[maxIdx].bruto! + resBruto) * 1000) / 1000 : null,
      tara: rows[maxIdx].tara != null ? Math.round((rows[maxIdx].tara! + resTara) * 1000) / 1000 : null,
      neto: rows[maxIdx].neto != null ? Math.round((rows[maxIdx].neto! + resNeto) * 1000) / 1000 : null
    };
  }
  return rows;
}

/** Fila de totales para la tabla de prorrateo. */
export function calcularProrateoTotales(rows: ProrateoRow[]): ProrateoTotales {
  if (rows.length === 0) return { aves: 0, pct: 0, bruto: null, tara: null, neto: null };
  const hayPeso = rows.some((r) => r.bruto != null);
  return {
    aves: rows.reduce((s, r) => s + r.aves, 0),
    pct: rows.reduce((s, r) => s + r.pct, 0),
    bruto: hayPeso ? Math.round(rows.reduce((s, r) => s + (r.bruto ?? 0), 0) * 1000) / 1000 : null,
    tara: hayPeso ? Math.round(rows.reduce((s, r) => s + (r.tara ?? 0), 0) * 1000) / 1000 : null,
    neto: hayPeso ? Math.round(rows.reduce((s, r) => s + (r.neto ?? 0), 0) * 1000) / 1000 : null
  };
}

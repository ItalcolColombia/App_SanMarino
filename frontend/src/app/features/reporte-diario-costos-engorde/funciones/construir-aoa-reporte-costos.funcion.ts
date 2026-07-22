// frontend/src/app/features/reporte-diario-costos-engorde/funciones/construir-aoa-reporte-costos.funcion.ts
// Función PURA: arma la matriz AOA (array-of-arrays) del Excel del Reporte Diario
// Costos engorde. Sin `this`, sin DI, sin side-effects: el componente la llama y
// pasa el resultado a `exportarAoaExcel` (helper compartido).

import {
  ReporteDiarioCostosGalponHeader,
  ReporteDiarioCostosReporte
} from '../models/reporte-diario-costos.model';

export interface AoaReporteCostos {
  aoa: (string | number)[][];
  colWidths: number[];
}

function fechaCortaExcel(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return isNaN(d.getTime()) ? String(iso) : d.toLocaleDateString('es');
}

function kgExcel(v: number | null | undefined): number | string {
  if (v == null) return '—';
  return Math.round(v * 100) / 100;
}

/**
 * Layout (espejo del mockup):
 *  título · contexto (granja/lote base/rango) · lotes involucrados
 *  header 2 niveles: FECHA | ALIMENTO(3) | TOTAL DÍA | MORT+SEL×galpón | AVES VIVAS×galpón | TOTAL AVES
 *  una fila por fecha×alimento (los valores de día solo en la primera fila de la fecha)
 *  footer: SUMA TOTAL + suma por galpón + aves vivas actuales.
 */
export function construirAoaReporteCostos(reporte: ReporteDiarioCostosReporte): AoaReporteCostos {
  const galpones: ReporteDiarioCostosGalponHeader[] = reporte.galpones;
  const nGal = galpones.length;
  const aoa: (string | number)[][] = [];

  aoa.push(['REPORTE DIARIO COSTOS — POLLO ENGORDE']);
  aoa.push([
    `Granja: ${reporte.granjaNombre}`,
    `Lote base: ${reporte.loteBaseNombre ?? 'Todos los lotes'}`,
    `Del ${fechaCortaExcel(reporte.fechaInicioEfectiva)} al ${fechaCortaExcel(reporte.fechaFinEfectiva)}`
  ]);
  aoa.push([
    'Lotes:',
    ...reporte.lotes.map(l => `${l.loteNombre} (${l.galponNombre})`)
  ]);
  aoa.push([]);

  // Header nivel 1
  aoa.push([
    'FECHA', 'ALIMENTO', '', '', 'TOTAL DÍA (kg)',
    ...galpones.map(() => 'MORTALIDAD + SELECCIÓN'),
    ...galpones.map(() => 'AVES VIVAS'),
    'TOTAL AVES'
  ]);
  // Header nivel 2
  aoa.push([
    '', 'Tipo alimento', 'Stock (kg)', 'Consumo (kg)', '',
    ...galpones.map(g => g.galponNombre),
    ...galpones.map(g => g.galponNombre),
    ''
  ]);

  // Filas: una por fecha×alimento
  for (const f of reporte.filas) {
    const alimentos = f.alimentos.length > 0
      ? f.alimentos
      : [{ nombreAlimento: '—', stockKg: null, consumoKg: null as number | null }];
    const porGalpon = new Map(f.galpones.map(g => [g.galponId, g]));

    alimentos.forEach((a, idx) => {
      const primera = idx === 0;
      aoa.push([
        primera ? fechaCortaExcel(f.fecha) : '',
        a.nombreAlimento,
        kgExcel(a.stockKg),
        kgExcel(a.consumoKg),
        primera ? kgExcel(f.consumoTotalKg) : '',
        ...galpones.map(g => (primera ? (porGalpon.get(g.galponId)?.mortSel ?? 0) : '')),
        ...galpones.map(g => (primera ? (porGalpon.get(g.galponId)?.avesVivas ?? 0) : '')),
        primera ? f.avesVivasTotal : ''
      ]);
    });
  }

  // Footer
  const totPorGalpon = new Map(reporte.totales.porGalpon.map(g => [g.galponId, g]));
  const avesActuales = new Map(reporte.avesVivasActuales.map(a => [a.galponId, a]));
  aoa.push([
    'SUMA TOTAL', '', '', '',
    kgExcel(reporte.totales.consumoTotalKg),
    ...galpones.map(g => totPorGalpon.get(g.galponId)?.mortSel ?? 0),
    ...galpones.map(g => avesActuales.get(g.galponId)?.avesVivas ?? 0),
    reporte.avesVivasActualesTotal
  ]);
  aoa.push([]);
  aoa.push(['CONSUMO TOTAL POR ALIMENTO (kg)']);
  for (const a of reporte.totales.alimentos) {
    aoa.push(['', a.nombreAlimento, '', kgExcel(a.consumoKg)]);
  }

  const colWidths = [12, 24, 12, 13, 14, ...galpones.map(() => 14), ...galpones.map(() => 12), 12];
  return { aoa, colWidths };
}

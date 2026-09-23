// frontend/src/app/features/reporte-diario-costos-engorde/funciones/construir-aoa-reporte-costos.funcion.ts
// Función PURA: arma la matriz AOA (array-of-arrays) del Excel del Reporte Diario
// Costos engorde. Sin `this`, sin DI, sin side-effects: el componente la llama y
// pasa el resultado a `exportarAoaExcel` (helper compartido).

import {
  ReporteDiarioCostosDesgloseSexo,
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

/** Celdas de mortalidad + selección de UN galpón: [H, M, Total] con desglose por sexo, [Total] sin él. */
function celdasMortGalpon(
  g: (ReporteDiarioCostosDesgloseSexo & { mortSel: number }) | undefined,
  porSexo: boolean
): number[] {
  const total = g?.mortSel ?? 0;
  return porSexo ? [g?.mortSelHembras ?? 0, g?.mortSelMachos ?? 0, total] : [total];
}

/**
 * Layout (espejo del mockup):
 *  título · contexto (granja/lote base/rango) · lotes involucrados
 *  header 2 niveles: FECHA | ALIMENTO(3) | TOTAL DÍA | MORT+SEL×galpón | AVES VIVAS×galpón | TOTAL AVES
 *  una fila por fecha×alimento (los valores de día solo en la primera fila de la fecha)
 *  footer: SUMA TOTAL + suma por galpón + aves vivas actuales.
 *
 * Con `reporte.mortalidadPorSexo` cada galpón de MORT+SEL ocupa 3 columnas (H | M | Total) y se
 * agrega un 3.er nivel de encabezado; sin él, el archivo es el de siempre.
 */
export function construirAoaReporteCostos(reporte: ReporteDiarioCostosReporte): AoaReporteCostos {
  const galpones: ReporteDiarioCostosGalponHeader[] = reporte.galpones;
  const porSexo = reporte.mortalidadPorSexo === true;
  const colsMort = porSexo ? 3 : 1;
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
    ...galpones.flatMap(() => Array<string>(colsMort).fill('MORTALIDAD + SELECCIÓN')),
    ...galpones.map(() => 'AVES VIVAS'),
    'TOTAL AVES'
  ]);
  // Header nivel 2
  aoa.push([
    '', 'Tipo alimento', 'Stock (kg)', 'Consumo (kg)', '',
    ...galpones.flatMap(g => Array<string>(colsMort).fill(g.galponNombre)),
    ...galpones.map(g => g.galponNombre),
    ''
  ]);
  // Header nivel 3 (solo con desglose por sexo)
  if (porSexo) {
    aoa.push([
      '', '', '', '', '',
      ...galpones.flatMap(() => ['H', 'M', 'Total']),
      ...galpones.map(() => ''),
      ''
    ]);
  }

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
        ...galpones.flatMap<string | number>(g => (primera
          ? celdasMortGalpon(porGalpon.get(g.galponId), porSexo)
          : Array<string>(colsMort).fill(''))),
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
    ...galpones.flatMap(g => celdasMortGalpon(totPorGalpon.get(g.galponId), porSexo)),
    ...galpones.map(g => avesActuales.get(g.galponId)?.avesVivas ?? 0),
    reporte.avesVivasActualesTotal
  ]);
  aoa.push([]);
  aoa.push(['CONSUMO TOTAL POR ALIMENTO (kg)']);
  for (const a of reporte.totales.alimentos) {
    aoa.push(['', a.nombreAlimento, '', kgExcel(a.consumoKg)]);
  }

  const colWidths = [
    12, 24, 12, 13, 14,
    ...galpones.flatMap(() => (porSexo ? [8, 8, 10] : [14])),
    ...galpones.map(() => 12),
    12
  ];
  return { aoa, colWidths };
}

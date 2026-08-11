// frontend/src/app/features/reporte-diario-costos-postura/funciones/construir-aoa-costos-postura.funcion.ts
import type { ExcelCell, HojaAoaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { fechaCortaSinTz } from '../../../shared/utils/format';
import { expandirFilasAlimento } from './expandir-filas-alimento.funcion';
import { ReporteDiarioCostosPosturaReporte } from '../models/reporte-diario-costos-postura.model';

/**
 * Arma las TRES hojas del Excel — una por pestaña de la pantalla (Aves · Alimento · Huevos),
 * con la misma información y el mismo orden que se ve en pantalla.
 *
 * La hoja Huevos solo se agrega si el reporte trae filas de producción (en levante no aplica).
 *
 * Función PURA: sin `this`, sin DI, sin estado.
 */
export function construirHojasCostosPostura(rep: ReporteDiarioCostosPosturaReporte): HojaAoaExcel[] {
  const hojas: HojaAoaExcel[] = [
    hojaAves(rep),
    hojaAlimento(rep)
  ];

  const filasProduccion = rep.filas.filter(f => f.fase === 'Produccion');
  if (filasProduccion.length > 0) hojas.push(hojaHuevos(rep));

  return hojas;
}

/** Líneas de contexto que encabezan cada hoja (qué filtros produjeron estos números). */
function encabezado(rep: ReporteDiarioCostosPosturaReporte, titulo: string): ExcelCell[][] {
  const f = rep.filtrosAplicados;
  const lotesBase = Array.from(new Set(rep.lotes.map(l => l.loteBaseNombre).filter(Boolean)));
  const granjas = Array.from(new Set(rep.lotes.map(l => l.granjaNombre).filter(Boolean)));

  const aoa: ExcelCell[][] = [
    [titulo],
    [
      `Rango: ${fechaCortaSinTz(rep.fechaDesdeEfectiva)} a ${fechaCortaSinTz(rep.fechaHastaEfectiva)}`,
      `Fase: ${rep.fases.length ? rep.fases.join(' + ') : '—'}`,
      `Granja: ${granjas.length ? granjas.join(', ') : 'Todas'}`,
      `Lote base: ${lotesBase.length ? lotesBase.join(', ') : 'Todos'}`,
      `Regional: ${f.regional || 'Todas'}`
    ]
  ];

  // Dónde se hizo cada fase: sin esto, un ciclo repartido entre dos granjas es ilegible en el Excel.
  for (const u of rep.ubicaciones ?? []) {
    aoa.push([
      `${u.fase}: ${u.granjaNombre}`,
      `Lote base: ${u.loteBaseNombre || '—'}`,
      `Lotes: ${u.lotes}`,
      `Del ${fechaCortaSinTz(u.desde)} al ${fechaCortaSinTz(u.hasta)}`,
      `Días: ${u.dias}`
    ]);
  }

  if ((rep.diasDuplicados ?? 0) > 0) {
    aoa.push([
      `${rep.diasDuplicados} día(s) registrados en levante Y en producción: la fila de levante se ` +
      'lista con la marca "NO SUMA" y queda fuera de los totales para no contar dos veces el mismo ' +
      'alimento y las mismas aves.'
    ]);
  }

  aoa.push([]);
  return aoa;
}

/** Marca de la columna final: qué filas se listan pero no suman. */
function marca(excluida: boolean): string {
  return excluida ? 'NO SUMA (duplicado con producción)' : '';
}

function hojaAves(rep: ReporteDiarioCostosPosturaReporte): HojaAoaExcel {
  const aoa: ExcelCell[][] = encabezado(rep, 'Reporte Diario Costos Postura — Aves');

  // Cabecera de dos pisos, igual que el diseño (grupo arriba, sexo abajo).
  aoa.push(['Fecha', 'Granja', 'Lote : Galpón', 'Fase', 'Edad (días)', 'Semana',
    'Mortalidad', '', 'Selección', '', 'Error de Sexaje', '', 'Ventas', '', 'Observación']);
  aoa.push(['', '', '', '', '', '',
    'Hembras', 'Machos', 'Hembras', 'Machos', 'Hembras', 'Machos', 'Hembras', 'Machos', '']);

  for (const f of rep.filas) {
    aoa.push([
      fechaCortaSinTz(f.fecha), f.granjaNombre, f.loteGalpon, f.fase, f.edadDias, f.semana,
      f.mortalidadH, f.mortalidadM,
      f.seleccionH, f.seleccionM,
      f.errorSexajeH, f.errorSexajeM,
      f.ventaAvesH, f.ventaAvesM,
      marca(f.excluidoDelTotal)
    ]);
  }

  const t = rep.totales.aves;
  aoa.push([]);
  aoa.push(['TOTAL', '', '', '', '', '',
    t.mortalidadH, t.mortalidadM,
    t.seleccionH, t.seleccionM,
    t.errorSexajeH, t.errorSexajeM,
    t.ventaAvesH, t.ventaAvesM]);
  aoa.push(['Total hembras', t.totalH, 'Total machos', t.totalM, 'Total general', t.total]);

  return {
    sheetName: 'Aves',
    aoa,
    colWidths: [12, 22, 28, 12, 11, 9, 11, 10, 11, 10, 11, 10, 11, 10, 30]
  };
}

function hojaAlimento(rep: ReporteDiarioCostosPosturaReporte): HojaAoaExcel {
  const aoa: ExcelCell[][] = encabezado(rep, 'Reporte Diario Costos Postura — Alimento');

  aoa.push(['Fecha', 'Granja', 'Lote : Galpón', 'Fase',
    'Hembras', '', 'Machos', '', 'Observación']);
  aoa.push(['', '', '', '',
    'Tipo alimento', 'Cantidad (kg)', 'Tipo alimento', 'Cantidad (kg)', '']);

  for (const f of expandirFilasAlimento(rep.filas)) {
    aoa.push([
      f.fechaFmt, f.granjaNombre, f.loteGalpon, f.fase,
      f.hembraNombre, f.hembraKg,
      f.machoNombre, f.machoKg,
      marca(f.excluidoDelTotal)
    ]);
  }

  const t = rep.totales;
  aoa.push([]);
  aoa.push(['TOTAL CONSUMO (kg)', '', '', '', '', t.consumoKgH, '', t.consumoKgM]);
  aoa.push([]);
  aoa.push(['Consumo por referencia de alimento']);
  aoa.push(['Sexo', 'Alimento', 'Cantidad (kg)']);
  for (const a of t.alimentos) {
    aoa.push([a.sexo === 'H' ? 'Hembras' : 'Machos', a.nombre, a.cantidadKg]);
  }

  return {
    sheetName: 'Alimento',
    aoa,
    colWidths: [12, 22, 28, 12, 42, 14, 42, 14, 30]
  };
}

function hojaHuevos(rep: ReporteDiarioCostosPosturaReporte): HojaAoaExcel {
  const aoa: ExcelCell[][] = encabezado(rep, 'Reporte Diario Costos Postura — Huevos (solo producción)');

  aoa.push(['Fecha', 'Granja', 'Lote : Galpón', 'Huevo fértil', 'Huevo comercial', 'Huevo inservible',
    'Ventas de huevo', 'Traslado a planta', 'Huevo Total']);

  for (const f of rep.filas.filter(x => x.fase === 'Produccion')) {
    aoa.push([
      fechaCortaSinTz(f.fecha), f.granjaNombre, f.loteGalpon,
      f.huevo.fertil, f.huevo.comercial, f.huevo.inservible,
      f.huevo.venta, f.huevo.trasladoPlanta, f.huevo.total
    ]);
  }

  const h = rep.totales.huevo;
  aoa.push([]);
  aoa.push(['TOTAL', '', '', h.fertil, h.comercial, h.inservible, h.venta, h.trasladoPlanta, h.total]);

  return {
    sheetName: 'Huevos',
    aoa,
    colWidths: [12, 22, 28, 14, 16, 16, 16, 18, 14]
  };
}

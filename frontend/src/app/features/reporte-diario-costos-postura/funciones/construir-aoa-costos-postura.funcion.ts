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
/**
 * @param ocultaMachos empresas sin machos en postura (SR-DEF-1): las columnas de machos —y las de
 *   error de sexaje, que desaparece como concepto— no se emiten en el Excel.
 * @param clasificacionPorItems empresas que clasifican huevo por ítem del catálogo: fértil/comercial/
 *   inservible salen de las 11 columnas fijas y quedan siempre en 0 (el desglose real vive en
 *   `metadata.huevoItems`, que este reporte no lee) — no se emiten en el Excel.
 */
export function construirHojasCostosPostura(
  rep: ReporteDiarioCostosPosturaReporte,
  ocultaMachos = false,
  clasificacionPorItems = false
): HojaAoaExcel[] {
  const hojas: HojaAoaExcel[] = [
    hojaAves(rep, ocultaMachos),
    hojaAlimento(rep, ocultaMachos)
  ];

  const filasProduccion = rep.filas.filter(f => f.fase === 'Produccion');
  if (filasProduccion.length > 0) hojas.push(hojaHuevos(rep, clasificacionPorItems));

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

function hojaAves(rep: ReporteDiarioCostosPosturaReporte, ocultaMachos: boolean): HojaAoaExcel {
  const aoa: ExcelCell[][] = encabezado(rep, 'Reporte Diario Costos Postura — Aves');

  // Cabecera de dos pisos, igual que el diseño (grupo arriba, sexo abajo).
  // `soloH` = la pareja Hembras/Machos se reduce a una sola columna; el grupo "Error de Sexaje"
  // desaparece entero porque el concepto no existe para estas empresas.
  const par = (grupo: string) => ocultaMachos ? [grupo] : [grupo, ''];
  const sexos = () => ocultaMachos ? ['Hembras'] : ['Hembras', 'Machos'];
  aoa.push(['Fecha', 'Granja', 'Lote : Galpón', 'Fase', 'Edad (días)', 'Semana',
    ...par('Mortalidad'), ...par('Selección'),
    ...(ocultaMachos ? [] : ['Error de Sexaje', '']),
    ...par('Ventas'), 'Observación']);
  aoa.push(['', '', '', '', '', '',
    ...sexos(), ...sexos(),
    ...(ocultaMachos ? [] : ['Hembras', 'Machos']),
    ...sexos(), '']);

  for (const f of rep.filas) {
    aoa.push([
      fechaCortaSinTz(f.fecha), f.granjaNombre, f.loteGalpon, f.fase, f.edadDias, f.semana,
      ...(ocultaMachos ? [f.mortalidadH] : [f.mortalidadH, f.mortalidadM]),
      ...(ocultaMachos ? [f.seleccionH] : [f.seleccionH, f.seleccionM]),
      ...(ocultaMachos ? [] : [f.errorSexajeH, f.errorSexajeM]),
      ...(ocultaMachos ? [f.ventaAvesH] : [f.ventaAvesH, f.ventaAvesM]),
      marca(f.excluidoDelTotal)
    ]);
  }

  const t = rep.totales.aves;
  aoa.push([]);
  aoa.push(['TOTAL', '', '', '', '', '',
    ...(ocultaMachos ? [t.mortalidadH] : [t.mortalidadH, t.mortalidadM]),
    ...(ocultaMachos ? [t.seleccionH] : [t.seleccionH, t.seleccionM]),
    ...(ocultaMachos ? [] : [t.errorSexajeH, t.errorSexajeM]),
    ...(ocultaMachos ? [t.ventaAvesH] : [t.ventaAvesH, t.ventaAvesM])]);
  // El "Total general" se conserva: es el total de aves del lote, no un dato de machos.
  aoa.push(ocultaMachos
    ? ['Total hembras', t.totalH, 'Total general', t.total]
    : ['Total hembras', t.totalH, 'Total machos', t.totalM, 'Total general', t.total]);

  return {
    sheetName: 'Aves',
    aoa,
    colWidths: [12, 22, 28, 12, 11, 9, 11, 10, 11, 10, 11, 10, 11, 10, 30]
  };
}

function hojaAlimento(rep: ReporteDiarioCostosPosturaReporte, ocultaMachos: boolean): HojaAoaExcel {
  const aoa: ExcelCell[][] = encabezado(rep, 'Reporte Diario Costos Postura — Alimento');

  aoa.push(['Fecha', 'Granja', 'Lote : Galpón', 'Fase',
    'Hembras', '', ...(ocultaMachos ? [] : ['Machos', '']), 'Observación']);
  aoa.push(['', '', '', '',
    'Tipo alimento', 'Cantidad (kg)',
    ...(ocultaMachos ? [] : ['Tipo alimento', 'Cantidad (kg)']), '']);

  for (const f of expandirFilasAlimento(rep.filas)) {
    aoa.push([
      f.fechaFmt, f.granjaNombre, f.loteGalpon, f.fase,
      f.hembraNombre, f.hembraKg,
      ...(ocultaMachos ? [] : [f.machoNombre, f.machoKg]),
      marca(f.excluidoDelTotal)
    ]);
  }

  const t = rep.totales;
  aoa.push([]);
  aoa.push(ocultaMachos
    ? ['TOTAL CONSUMO (kg)', '', '', '', '', t.consumoKgH]
    : ['TOTAL CONSUMO (kg)', '', '', '', '', t.consumoKgH, '', t.consumoKgM]);
  aoa.push([]);
  aoa.push(['Consumo por referencia de alimento']);
  aoa.push(['Sexo', 'Alimento', 'Cantidad (kg)']);
  for (const a of t.alimentos.filter(x => !ocultaMachos || x.sexo === 'H')) {
    aoa.push([a.sexo === 'H' ? 'Hembras' : 'Machos', a.nombre, a.cantidadKg]);
  }

  return {
    sheetName: 'Alimento',
    aoa,
    colWidths: [12, 22, 28, 12, 42, 14, 42, 14, 30]
  };
}

function hojaHuevos(rep: ReporteDiarioCostosPosturaReporte, clasificacionPorItems = false): HojaAoaExcel {
  const aoa: ExcelCell[][] = encabezado(rep, 'Reporte Diario Costos Postura — Huevos (solo producción)');

  // Misma posicion en cabecera y datos: [] o los 3 valores, nunca desalineados.
  const encabezadoParticion: ExcelCell[] = clasificacionPorItems ? [] : ['Huevo fértil', 'Huevo comercial', 'Huevo inservible'];
  aoa.push(['Fecha', 'Granja', 'Lote : Galpón', ...encabezadoParticion,
    'Ventas de huevo', 'Traslado a planta', 'Huevo Total']);

  for (const f of rep.filas.filter(x => x.fase === 'Produccion')) {
    const particion: ExcelCell[] = clasificacionPorItems ? [] : [f.huevo.fertil, f.huevo.comercial, f.huevo.inservible];
    aoa.push([
      fechaCortaSinTz(f.fecha), f.granjaNombre, f.loteGalpon,
      ...particion,
      f.huevo.venta, f.huevo.trasladoPlanta, f.huevo.total
    ]);
  }

  const h = rep.totales.huevo;
  const totalParticion: ExcelCell[] = clasificacionPorItems ? [] : [h.fertil, h.comercial, h.inservible];
  aoa.push([]);
  aoa.push(['TOTAL', '', '', ...totalParticion, h.venta, h.trasladoPlanta, h.total]);

  return {
    sheetName: 'Huevos',
    aoa,
    colWidths: clasificacionPorItems
      ? [12, 22, 28, 16, 18, 14]
      : [12, 22, 28, 14, 16, 16, 16, 18, 14]
  };
}

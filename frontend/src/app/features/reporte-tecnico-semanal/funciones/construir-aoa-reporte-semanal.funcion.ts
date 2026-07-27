// Función PURA: arma las hojas AOA del export a Excel del Reporte Técnico
// Semanal (una hoja por galpón + consolidado), a partir de la especificación
// única de columnas (columnas-reporte-semanal.funcion.ts). Sin this/DI/toast.
import { ExcelCell, HojaAoaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import {
  ReporteSemanalTabHeader,
  ReporteTecnicoSemanalLevanteResponse,
  ReporteTecnicoSemanalProduccionResponse
} from '../models/reporte-tecnico-semanal.model';
import {
  agruparColumnas,
  ColumnaReporte,
  COLUMNAS_LEVANTE,
  COLUMNAS_PRODUCCION
} from './columnas-reporte-semanal.funcion';

const fechaYmd = (iso: string | null): string => (iso ? String(iso).slice(0, 10) : '');

function redondear(v: number, dec: number): number {
  const factor = Math.pow(10, dec);
  return Math.round(v * factor) / factor;
}

function filaCabeceraInfo(header: ReporteSemanalTabHeader, titulo: string): ExcelCell[][] {
  return [
    [titulo],
    [
      'Lote:', header.loteNombre,
      'Granja:', header.granjaNombre ?? '',
      'Municipio:', header.municipio ?? '',
      'Núcleo:', header.nucleoNombre ?? header.nucleoId ?? '',
      'Galpón:', header.galponNombre ?? header.galponId ?? ''
    ],
    [
      'Raza:', header.raza ?? '',
      'Año guía:', header.anioGuia ?? '',
      'Fecha encaset:', fechaYmd(header.fechaEncaset),
      'Aves H:', header.baseHembras,
      'Aves M:', header.baseMachos,
      'Técnico:', header.tecnico ?? ''
    ],
    []
  ];
}

function construirHoja<T>(
  sheetName: string,
  titulo: string,
  header: ReporteSemanalTabHeader,
  semanas: T[],
  columnas: ColumnaReporte<T>[]
): HojaAoaExcel {
  const grupos = agruparColumnas(columnas);
  const filaGrupos: ExcelCell[] = [];
  for (const g of grupos) {
    filaGrupos.push(g.titulo);
    for (let i = 1; i < g.span; i++) filaGrupos.push('');
  }
  const filaTitulos: ExcelCell[] = columnas.map(c => c.titulo);

  const cuerpo: ExcelCell[][] = semanas.map(s =>
    columnas.map(c => {
      const v = c.valor(s);
      if (v == null) return '';
      return typeof v === 'number' ? redondear(v, c.dec) : v;
    })
  );

  return {
    sheetName,
    aoa: [...filaCabeceraInfo(header, titulo), filaGrupos, filaTitulos, ...cuerpo],
    colWidths: columnas.map(c => Math.max(9, c.titulo.length + 2))
  };
}

/** Nombre de hoja Excel: máx 31 chars, sin caracteres inválidos, único. */
function nombreHoja(base: string, usados: Set<string>): string {
  let limpio = base.replace(/[\\/:*?\[\]]/g, ' ').trim().slice(0, 31) || 'Hoja';
  let candidato = limpio;
  let n = 2;
  while (usados.has(candidato)) {
    const sufijo = ` (${n++})`;
    candidato = limpio.slice(0, 31 - sufijo.length) + sufijo;
  }
  usados.add(candidato);
  return candidato;
}

export function construirHojasLevante(
  respuesta: ReporteTecnicoSemanalLevanteResponse
): HojaAoaExcel[] {
  const hojas: HojaAoaExcel[] = [];
  const usados = new Set<string>();
  const titulo = `Resumen Semanal de Levante — ${respuesta.loteBaseNombre}`;

  if (respuesta.consolidado) {
    hojas.push(construirHoja(
      nombreHoja(`Gral ${respuesta.loteBaseNombre}`, usados),
      titulo, respuesta.consolidado.header, respuesta.consolidado.semanas, COLUMNAS_LEVANTE));
  }
  for (const tab of respuesta.tabs) {
    hojas.push(construirHoja(
      nombreHoja(tab.header.loteNombre, usados),
      titulo, tab.header, tab.semanas, COLUMNAS_LEVANTE));
  }
  return hojas;
}

export function construirHojasProduccion(
  respuesta: ReporteTecnicoSemanalProduccionResponse
): HojaAoaExcel[] {
  const hojas: HojaAoaExcel[] = [];
  const usados = new Set<string>();
  const titulo = `Resumen Semanal de Producción — ${respuesta.loteBaseNombre}`;

  if (respuesta.consolidado) {
    hojas.push(construirHoja(
      nombreHoja(`Gral ${respuesta.loteBaseNombre}`, usados),
      titulo, respuesta.consolidado.header, respuesta.consolidado.semanas, COLUMNAS_PRODUCCION));
  }
  for (const tab of respuesta.tabs) {
    hojas.push(construirHoja(
      nombreHoja(tab.header.loteNombre, usados),
      titulo, tab.header, tab.semanas, COLUMNAS_PRODUCCION));
  }
  return hojas;
}

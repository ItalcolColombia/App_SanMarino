// Función PURA: arma la hoja AOA del export a Excel de la hoja «RESUMEN
// SEMANAL» del Informe RA Pesadas, a partir de la MISMA spec de columnas que
// pinta la tabla en pantalla. Sin this / DI / toast.
import { ExcelCell, HojaAoaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import {
  agruparColumnasResumen,
  ColumnaResumen,
  COLUMNAS_RESUMEN_LEVANTE,
  COLUMNAS_RESUMEN_PRODUCCION
} from './columnas-resumen-ra-pesadas.funcion';
import {
  ResumenSemanalRaPesadasLevanteResponse,
  ResumenSemanalRaPesadasProduccionResponse,
  ResumenSemanalTotales
} from '../models/resumen-semanal-ra-pesadas.model';

const fechaYmd = (iso: string | null): string => (iso ? String(iso).slice(0, 10) : '');

function redondear(v: number, dec: number): number {
  const factor = Math.pow(10, dec);
  return Math.round(v * factor) / factor;
}

function celda<T>(col: ColumnaResumen<T>, fila: T): ExcelCell {
  const v = col.valor(fila);
  if (v === null || v === undefined || v === '') return '';
  return typeof v === 'number' ? redondear(v, col.dec) : v;
}

/**
 * Fila de totales: los saldos SUMAN y los indicadores vienen del ponderado que
 * ya calculó el backend (por saldo de hembras). Las columnas sin `totalKey`
 * quedan en blanco a propósito: promediar un `Dif` o una `Edad` no significa nada.
 */
function filaTotales<T>(columnas: ColumnaResumen<T>[], totales: ResumenSemanalTotales): ExcelCell[] {
  return columnas.map((col, i) => {
    if (i === 0) return `TOTAL (${totales.lotes} lote${totales.lotes === 1 ? '' : 's'})`;
    if (!col.totalKey) return '';
    if (col.totalKey === '__saldoHembras') return redondear(totales.saldoHembras, col.dec);
    if (col.totalKey === '__saldoMachos') return redondear(totales.saldoMachos, col.dec);
    const v = totales.ponderados?.[col.totalKey];
    return v === null || v === undefined ? '' : redondear(v, col.dec);
  });
}

function construirHoja<T>(
  columnas: ColumnaResumen<T>[],
  filas: T[],
  totales: ResumenSemanalTotales,
  titulo: string,
  anio: number,
  semanaAnio: number,
  desde: string | null,
  hasta: string | null,
  sheetName: string
): HojaAoaExcel {
  const grupos = agruparColumnasResumen(columnas);

  // Fila de grupos: el título va en la primera celda del grupo y el resto vacío
  // (el helper no combina celdas; así queda legible igual que en el archivo).
  const filaGrupos: ExcelCell[] = [];
  for (const g of grupos) {
    filaGrupos.push(g.titulo);
    for (let i = 1; i < g.span; i++) filaGrupos.push('');
  }

  const aoa: ExcelCell[][] = [
    [titulo],
    ['Año:', anio, 'Semana:', semanaAnio, 'Del:', fechaYmd(desde), 'Al:', fechaYmd(hasta)],
    [],
    filaGrupos,
    columnas.map(c => c.titulo),
    ...filas.map(f => columnas.map(c => celda(c, f))),
    [],
    filaTotales(columnas, totales)
  ];

  return {
    sheetName,
    aoa,
    colWidths: columnas.map(c => (c.texto ? 18 : 12))
  };
}

export function construirHojaResumenLevante(
  resp: ResumenSemanalRaPesadasLevanteResponse
): HojaAoaExcel[] {
  return [
    construirHoja(
      COLUMNAS_RESUMEN_LEVANTE,
      resp.filas,
      resp.totales,
      'RESUMEN SEMANAL LOTES REPRODUCTORAS PESADAS — LEVANTE',
      resp.anio,
      resp.semanaAnio,
      resp.fechaInicioSemana,
      resp.fechaFinSemana,
      'Resumen Levante'
    )
  ];
}

export function construirHojaResumenProduccion(
  resp: ResumenSemanalRaPesadasProduccionResponse
): HojaAoaExcel[] {
  return [
    construirHoja(
      COLUMNAS_RESUMEN_PRODUCCION,
      resp.filas,
      resp.totales,
      'RESUMEN SEMANAL LOTES REPRODUCTORAS PESADAS — PRODUCCIÓN',
      resp.anio,
      resp.semanaAnio,
      resp.fechaInicioSemana,
      resp.fechaFinSemana,
      'Resumen Produccion'
    )
  ];
}

/**
 * Helper único de exportación a Excel (`.xlsx`) para todo el front.
 *
 * Centraliza el armado de libro/hoja/descarga que hoy está copiado idéntico en 13–15
 * módulos (`XLSX.utils.aoa_to_sheet → book_new → book_append_sheet → writeFile` con
 * título, filas de filtros, headers y nombre de archivo con sello `YYYYMMDD`).
 * El comportamiento es calcado del canónico
 * `movimientos-pollo-engorde/funciones/exportar-ventas-excel.funcion.ts`, por lo que
 * migrar un consumidor NO cambia el archivo generado.
 *
 * El módulo consumidor sigue siendo dueño de SUS headers y del mapeo de filas (lógica de
 * dominio); este helper solo hace el ensamblado mecánico. Funciones PURAS (salvo la
 * descarga, que es el efecto esperado).
 */
import * as XLSX from 'xlsx';
import { dateStampCompact, sanitizeFileName } from '../format';

/** Celda admitida en una hoja aoa. */
export type ExcelCell = string | number | boolean | null | undefined;

/** Opciones de descarga del `.xlsx`. */
export interface ExportarExcelOpts {
  /** Nombre base del archivo (sin extensión ni sello). Se sanitiza. */
  filenameBase: string;
  /** Nombre de la hoja. Default `Hoja1`. */
  sheetName?: string;
  /** Fila de título arriba de las cabeceras (opcional). */
  title?: string;
  /** Filas de subtítulo (p. ej. filtros aplicados) debajo del título (opcional). */
  subtitles?: string[];
  /** Agregar el sello `_YYYYMMDD` al nombre. Default `true`. */
  stamp?: boolean;
}

/** Definición de una hoja para el export multi-hoja. */
export interface HojaExcel {
  sheetName: string;
  headers: ExcelCell[];
  rows: ExcelCell[][];
  title?: string;
  subtitles?: string[];
}

/**
 * Arma el `aoa` (array-of-arrays) canónico:
 * `[title]`, `[subtitle]...`, `[]` (blanco), `headers`, `...rows`.
 * Idéntico al patrón que hoy se repite a mano. Función pura (testeable sin descargar).
 */
export function construirAoaExcel(
  headers: ExcelCell[],
  rows: ExcelCell[][],
  title?: string,
  subtitles?: string[],
): ExcelCell[][] {
  const aoa: ExcelCell[][] = [];
  if (title) aoa.push([title]);
  if (subtitles?.length) subtitles.forEach((s) => aoa.push([s]));
  if (title || subtitles?.length) aoa.push([]); // fila en blanco separadora
  aoa.push(headers);
  rows.forEach((r) => aoa.push(r));
  return aoa;
}

/** Nombre final del archivo: `{base sanitizado}{_YYYYMMDD}.xlsx`. */
export function nombreArchivoXlsx(opts: ExportarExcelOpts): string {
  const base = sanitizeFileName(opts.filenameBase || 'export');
  const stamp = opts.stamp === false ? '' : `_${dateStampCompact()}`;
  return `${base}${stamp}.xlsx`;
}

/** Construye y descarga un `.xlsx` de una sola hoja (patrón canónico). */
export function exportarTablaExcel(headers: ExcelCell[], rows: ExcelCell[][], opts: ExportarExcelOpts): void {
  const aoa = construirAoaExcel(headers, rows, opts.title, opts.subtitles);
  const ws = XLSX.utils.aoa_to_sheet(aoa);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, opts.sheetName ?? 'Hoja1');
  XLSX.writeFile(wb, nombreArchivoXlsx(opts));
}

/** Construye y descarga un `.xlsx` con varias hojas. */
export function exportarMultiHojaExcel(hojas: HojaExcel[], opts: ExportarExcelOpts): void {
  const wb = XLSX.utils.book_new();
  hojas.forEach((h) => {
    const aoa = construirAoaExcel(h.headers, h.rows, h.title, h.subtitles);
    const ws = XLSX.utils.aoa_to_sheet(aoa);
    XLSX.utils.book_append_sheet(wb, ws, h.sheetName);
  });
  XLSX.writeFile(wb, nombreArchivoXlsx(opts));
}

/** Opciones del export desde filas-objeto: nombre completo custom O base + sello `_YYYYMMDD`. */
export interface ExportarObjetosExcelOpts {
  sheetName?: string;
  /** Si se da, es el nombre de archivo tal cual (ignora filenameBase/stamp). */
  filenameFull?: string;
  filenameBase?: string;
  stamp?: boolean;
  /** Fila a usar cuando no hay datos (p. ej. `{ Mensaje: 'Sin registros' }`). */
  emptyRow?: Record<string, ExcelCell>;
}

/**
 * Descarga un `.xlsx` desde filas-objeto (usa `json_to_sheet`), para exports que ya arman un objeto
 * por fila. Centraliza el ensamblado libro/hoja/descarga que hoy se repite con `json_to_sheet`.
 */
export function exportarObjetosExcel(rows: Record<string, ExcelCell>[], opts: ExportarObjetosExcelOpts): void {
  const data = rows.length ? rows : (opts.emptyRow ? [opts.emptyRow] : []);
  const ws = XLSX.utils.json_to_sheet(data);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, opts.sheetName ?? 'Hoja1');
  const filename = opts.filenameFull
    ?? nombreArchivoXlsx({ filenameBase: opts.filenameBase ?? 'export', sheetName: opts.sheetName, stamp: opts.stamp });
  XLSX.writeFile(wb, filename);
}

/** Hoja de filas-objeto para el export multi-hoja json. */
export interface HojaObjetosExcel {
  sheetName: string;
  rows: Record<string, ExcelCell>[];
  /** Fila cuando no hay datos. Si se omite y `rows` está vacío, la hoja igual se agrega vacía. */
  emptyRow?: Record<string, ExcelCell>;
}

/**
 * Descarga un `.xlsx` con varias hojas de filas-objeto (json_to_sheet por hoja). El caller decide
 * qué hojas incluir (para respetar el "solo si hay datos"). Si no hay ninguna hoja, no descarga.
 */
export function exportarObjetosMultiHojaExcel(
  hojas: HojaObjetosExcel[],
  opts: { filenameFull?: string; filenameBase?: string; stamp?: boolean },
): void {
  if (!hojas.length) return;
  const wb = XLSX.utils.book_new();
  hojas.forEach((h) => {
    const data = h.rows.length ? h.rows : (h.emptyRow ? [h.emptyRow] : []);
    XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(data), h.sheetName);
  });
  const filename = opts.filenameFull
    ?? nombreArchivoXlsx({ filenameBase: opts.filenameBase ?? 'export', stamp: opts.stamp });
  XLSX.writeFile(wb, filename);
}

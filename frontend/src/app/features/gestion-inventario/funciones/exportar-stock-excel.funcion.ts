/**
 * Exportación a Excel del stock de inventario de TODAS las granjas asignadas.
 *
 * El archivo trae **dos hojas**, que son la partición natural del módulo:
 * - `Alimento`: se maneja por ubicación ⇒ cada fila lleva su núcleo y galpón.
 * - `Otros conceptos`: se manejan a nivel granja ⇒ sin columnas de ubicación.
 *
 * Así entra todo el stock (todos los conceptos, todas las bodegas) en un solo archivo, que es lo
 * que operación necesita para comparar bodegas.
 *
 * El **nivel de manejo lo decide el backend**, no esta función: las filas de alimento vienen con
 * núcleo/galpón y las de otros conceptos vienen sin ubicación (`null`). Acá solo se reparte por
 * concepto y se formatea.
 *
 * Funciones puras salvo la descarga, que es el efecto esperado.
 */
import { InventarioGestionStockDto } from '../services/gestion-inventario.service';
import { fechaCortaSinTz } from '../../../shared/utils/format';
import {
  ExcelCell,
  HojaExcel,
  exportarMultiHojaExcel
} from '../../../shared/utils/excel/exportar-tabla-excel.funcion';

/** Contexto de exportación: filtros legibles aplicados y si la ubicación aplica. */
export interface ExportarStockExcelMeta {
  /** Líneas de contexto comunes a las dos hojas (granjas incluidas, búsqueda aplicada…). */
  filtros: string[];
  /**
   * `true` = la hoja de alimento incluye columnas Núcleo y Galpón. `false` en Colombia
   * (inventario a nivel granja: irían vacías en el 100 % de las filas). Espejo de
   * `stockShowNucleoGalpon`.
   */
  incluirUbicacion: boolean;
}

export const HOJA_ALIMENTO = 'Alimento';
export const HOJA_OTROS = 'Otros conceptos';

const TITULO_ALIMENTO = 'Stock de alimento — todas las granjas asignadas';
const TITULO_OTROS = 'Stock de otros conceptos — todas las granjas asignadas';
const SIN_REGISTROS = 'Sin registros para este grupo.';

/** Sin ubicación cae al id; sin ninguno de los dos, guion (mismo fallback que la grilla). */
function textoUbicacion(nombre: string | null | undefined, id: string | null | undefined): string {
  return nombre ?? id ?? '—';
}

/**
 * ¿La fila es alimento? Se decide por el **concepto** (`itemType` = `concepto ?? tipoItem` del
 * catálogo), NO por tener galpón: un alimento de una empresa que lo maneja a nivel granja sigue
 * siendo alimento aunque venga sin ubicación. Comparación insensible a mayúsculas porque el
 * catálogo tiene el concepto escrito de las dos formas (`alimento` y `Alimento`).
 */
export function esFilaAlimento(row: InventarioGestionStockDto): boolean {
  return (row.itemType ?? '').trim().toLowerCase() === 'alimento';
}

/** Reparte el stock en los dos grupos del archivo, conservando el orden del backend. */
export function particionarStockPorConcepto(rows: InventarioGestionStockDto[]): {
  alimento: InventarioGestionStockDto[];
  otros: InventarioGestionStockDto[];
} {
  const alimento: InventarioGestionStockDto[] = [];
  const otros: InventarioGestionStockDto[] = [];
  for (const r of rows) (esFilaAlimento(r) ? alimento : otros).push(r);
  return { alimento, otros };
}

/** Cabeceras de una hoja; con Núcleo/Galpón solo si la ubicación aplica. */
export function cabecerasStockExcel(incluirUbicacion: boolean): ExcelCell[] {
  return [
    'Granja',
    ...(incluirUbicacion ? ['Núcleo', 'Galpón'] : []),
    'Código',
    'Producto',
    'Tipo',
    'Fecha de ingreso',
    'Cantidad',
    'Unidad'
  ];
}

/**
 * Mapea el stock a filas del `.xlsx`. La **cantidad sale numérica** (no texto) para que el Excel
 * pueda sumarla y pivotearla: el pedido de operación es justamente comparar bodegas.
 */
export function construirFilasStockExcel(
  rows: InventarioGestionStockDto[],
  opts: { incluirUbicacion: boolean }
): ExcelCell[][] {
  return rows.map((s) => [
    s.granjaNombre ?? String(s.farmId),
    ...(opts.incluirUbicacion
      ? [textoUbicacion(s.nucleoNombre, s.nucleoId), textoUbicacion(s.galponNombre, s.galponId)]
      : []),
    s.itemCodigo,
    s.itemNombre,
    s.itemType,
    fechaCortaSinTz(s.fechaIngreso),
    s.quantity,
    s.unit
  ]);
}

/** Resumen que va bajo el título de cada hoja: cuántas filas y de cuántas granjas. */
function resumenHoja(rows: InventarioGestionStockDto[]): string {
  const granjas = new Set(rows.map((s) => s.granjaNombre ?? String(s.farmId)));
  return `Registros: ${rows.length} · Granjas con existencias: ${granjas.size}`;
}

function armarHoja(
  sheetName: string,
  title: string,
  rows: InventarioGestionStockDto[],
  incluirUbicacion: boolean,
  filtros: string[]
): HojaExcel {
  const headers = cabecerasStockExcel(incluirUbicacion);
  const filas = construirFilasStockExcel(rows, { incluirUbicacion });
  return {
    sheetName,
    headers,
    rows: filas.length ? filas : [[SIN_REGISTROS]],
    title,
    subtitles: [...filtros, resumenHoja(rows)]
  };
}

/**
 * Arma las dos hojas del archivo. Pura: no descarga nada, para poder testear el contenido.
 *
 * La hoja de otros conceptos normalmente NO lleva ubicación (esos ítems son a nivel granja); si
 * alguna fila trajera núcleo o galpón, las columnas se incluyen igual para no ocultar el dato.
 */
export function construirHojasStockExcel(
  rows: InventarioGestionStockDto[],
  meta: ExportarStockExcelMeta
): HojaExcel[] {
  const { alimento, otros } = particionarStockPorConcepto(rows);
  const otrosConUbicacion = meta.incluirUbicacion && otros.some((s) => !!s.nucleoId || !!s.galponId);
  return [
    armarHoja(HOJA_ALIMENTO, TITULO_ALIMENTO, alimento, meta.incluirUbicacion, meta.filtros),
    armarHoja(HOJA_OTROS, TITULO_OTROS, otros, otrosConUbicacion, meta.filtros)
  ];
}

/** Construye y descarga el `.xlsx` del stock (hoja Alimento + hoja Otros conceptos). */
export function exportarStockExcel(rows: InventarioGestionStockDto[], meta: ExportarStockExcelMeta): void {
  exportarMultiHojaExcel(construirHojasStockExcel(rows, meta), {
    filenameBase: 'stock-inventario-todas-granjas'
  });
}

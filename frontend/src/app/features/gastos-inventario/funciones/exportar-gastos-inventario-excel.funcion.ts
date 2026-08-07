/**
 * Reporte `.xlsx` del módulo **Gastos de inventario** — función PURA (sin `this`, sin DI, sin toast).
 *
 * Reemplaza al CSV armado a mano que:
 *   1. mezclaba consumos ELIMINADOS con los reales, sin forma de distinguirlos (el DTO ya traía
 *      `estado` pero el CSV no lo escribía), y
 *   2. solo listaba las referencias que tuvieron consumo, así que no servía para controlar el
 *      inventario completo.
 *
 * El libro tiene dos hojas:
 *   - **Consumos**: una fila por línea de consumo (el backend ya excluye los eliminados).
 *   - **Existencias**: TODOS los ítems no-alimento del catálogo por granja; si un ítem no tuvo
 *     consumo aparece igual con su saldo, que es el control de inventario que pidió el usuario final.
 */
import {
  ExcelCell,
  HojaExcel,
  exportarMultiHojaExcel
} from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import {
  InventarioGastoExistenciaDto,
  InventarioGastoExportRowDto
} from '../models/inventario-gasto.model';
import { sufijoArchivoRango } from './rango-fechas-gastos.funcion';

/** Filtros aplicados, para dejarlos escritos como subtítulo del reporte. */
export interface FiltrosReporteGastos {
  granjaNombre?: string | null;
  nucleoNombre?: string | null;
  galponNombre?: string | null;
  loteNombre?: string | null;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
  concepto?: string | null;
}

export const HEADERS_CONSUMOS: ExcelCell[] = [
  'Fecha',
  'Granja',
  'Núcleo',
  'Galpón',
  'Lote',
  'Concepto',
  'Código ítem',
  'Nombre ítem',
  'Tipo ítem',
  'Cantidad',
  'Unidad',
  'Stock antes',
  'Stock después',
  'Estado',
  'Observaciones',
  'Fecha registro'
];

export const HEADERS_EXISTENCIAS: ExcelCell[] = [
  'Granja',
  'Concepto',
  'Código ítem',
  'Nombre ítem',
  'Tipo ítem',
  'Unidad',
  'Saldo actual',
  'Consumido en el rango',
  'Gastos en el rango'
];

/** `yyyy-MM-dd` desde un ISO/fecha del backend, sin desplazar el día por zona horaria. */
function soloFecha(v: string | null | undefined): string {
  if (!v) return '';
  return typeof v === 'string' && v.length >= 10 ? v.slice(0, 10) : String(v);
}

/** Numérico para Excel: deja `number` (para que sume/filtre en la hoja) y vacío si no hay dato. */
function num(v: number | null | undefined): ExcelCell {
  return v == null || Number.isNaN(v) ? '' : Number(v);
}

/** Describe los filtros aplicados en una línea legible. Vacío si no hay ninguno. */
export function describirFiltros(f: FiltrosReporteGastos): string {
  const partes: string[] = [];
  if (f.granjaNombre) partes.push(`Granja: ${f.granjaNombre}`);
  if (f.nucleoNombre) partes.push(`Núcleo: ${f.nucleoNombre}`);
  if (f.galponNombre) partes.push(`Galpón: ${f.galponNombre}`);
  if (f.loteNombre) partes.push(`Lote: ${f.loteNombre}`);
  if (f.concepto) partes.push(`Concepto: ${f.concepto}`);
  if (f.fechaDesde || f.fechaHasta) {
    partes.push(`Rango: ${f.fechaDesde || 'inicio'} a ${f.fechaHasta || 'hoy'}`);
  }
  return partes.length ? `Filtros — ${partes.join(' · ')}` : 'Filtros — todos';
}

/** Filas de la hoja "Consumos" a partir del DTO del backend. */
export function construirFilasConsumos(rows: InventarioGastoExportRowDto[]): ExcelCell[][] {
  return rows.map((r) => [
    soloFecha(r.fecha),
    r.granjaNombre ?? '',
    r.nucleoNombre ?? r.nucleoId ?? '',
    r.galponNombre ?? r.galponId ?? '',
    r.loteNombre ?? (r.loteAveEngordeId != null ? String(r.loteAveEngordeId) : ''),
    r.conceptoLinea ?? '',
    r.itemCodigo,
    r.itemNombre,
    r.itemTipo,
    num(r.cantidad),
    r.unidad,
    num(r.stockAntes),
    num(r.stockDespues),
    r.estado,
    r.observacionesCabecera ?? '',
    soloFecha(r.createdAt)
  ]);
}

/** Filas de la hoja "Existencias" a partir del DTO del backend. */
export function construirFilasExistencias(rows: InventarioGastoExistenciaDto[]): ExcelCell[][] {
  return rows.map((e) => [
    e.granjaNombre ?? '',
    e.concepto ?? '',
    e.codigo,
    e.nombre,
    e.tipoItem,
    e.unidad,
    num(e.saldoActual),
    num(e.consumidoRango),
    num(e.gastosRango)
  ]);
}

/**
 * Aclara, en la hoja de Existencias, a qué período corresponde la columna «Consumido en el rango».
 * Sin rango es el histórico completo (comportamiento previo a que la pantalla ofreciera el filtro).
 */
export function leyendaRangoExistencias(f: FiltrosReporteGastos): string {
  const desde = (f.fechaDesde ?? '').trim();
  const hasta = (f.fechaHasta ?? '').trim();
  const periodo = desde && hasta
    ? `del ${desde} al ${hasta}`
    : desde
      ? `desde el ${desde}`
      : hasta
        ? `hasta el ${hasta}`
        : 'de TODO el histórico';
  return `«Consumido en el rango» y «Gastos en el rango» corresponden a los consumos ${periodo}; «Saldo actual» es el stock a la fecha de descarga.`;
}

/** Arma las dos hojas del reporte (pura: sin descargar; testeable). */
export function construirHojasReporteGastos(
  consumos: InventarioGastoExportRowDto[],
  existencias: InventarioGastoExistenciaDto[],
  filtros: FiltrosReporteGastos
): HojaExcel[] {
  const filtrosTxt = describirFiltros(filtros);
  return [
    {
      sheetName: 'Consumos',
      title: 'Gastos de inventario — Consumos registrados',
      subtitles: [filtrosTxt, 'No incluye consumos eliminados (su stock ya fue devuelto al inventario).'],
      headers: HEADERS_CONSUMOS,
      rows: construirFilasConsumos(consumos)
    },
    {
      sheetName: 'Existencias',
      title: 'Gastos de inventario — Existencias por concepto',
      subtitles: [
        filtrosTxt,
        'Incluye TODOS los ítems del catálogo (no solo los que tuvieron consumo).',
        leyendaRangoExistencias(filtros)
      ],
      headers: HEADERS_EXISTENCIAS,
      rows: construirFilasExistencias(existencias)
    }
  ];
}

/**
 * Nombre base del archivo. Lleva el rango pedido para que dos descargas de períodos distintos no se
 * pisen (el sello `_YYYYMMDD` del helper compartido es el día de la descarga, no el del rango).
 * Sin rango queda `gastos-inventario`, exactamente como antes.
 */
export function nombreBaseReporteGastos(f: FiltrosReporteGastos): string {
  return `gastos-inventario${sufijoArchivoRango(f.fechaDesde, f.fechaHasta)}`;
}

/** Arma y descarga el `.xlsx` de dos hojas del módulo. */
export function exportarGastosInventarioExcel(
  consumos: InventarioGastoExportRowDto[],
  existencias: InventarioGastoExistenciaDto[],
  filtros: FiltrosReporteGastos
): void {
  exportarMultiHojaExcel(construirHojasReporteGastos(consumos, existencias, filtros), {
    filenameBase: nombreBaseReporteGastos(filtros)
  });
}

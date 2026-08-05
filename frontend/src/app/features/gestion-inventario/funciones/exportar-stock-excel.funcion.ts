/**
 * Exportación a Excel del stock de inventario de TODAS las granjas asignadas.
 *
 * Vuelca a `.xlsx` lo mismo que muestra la pestaña **Stock**, pero sin recortar por granja: el
 * caller consulta el endpoint sin `farmId` (el backend ya devuelve todas las granjas asignadas al
 * usuario dentro de la empresa/país activos) y esta función arma cabeceras, filas y descarga.
 *
 * El **nivel de manejo lo decide el backend**, no esta función: las filas de alimento vienen con
 * núcleo/galpón y las de otros conceptos vienen sin ubicación (`null`). Acá solo se formatea:
 * ubicación ausente ⇒ `—`, igual que en la grilla.
 *
 * Función pura salvo la descarga, que es el efecto esperado.
 */
import { InventarioGestionStockDto } from '../services/gestion-inventario.service';
import { fechaCortaSinTz } from '../../../shared/utils/format';
import { ExcelCell, exportarTablaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';

/** Contexto de exportación: filtros legibles aplicados y si la ubicación aplica. */
export interface ExportarStockExcelMeta {
  /** Filtros aplicados, en texto, para dejarlos escritos en la cabecera del archivo. */
  filtros: string[];
  /**
   * `true` = incluye columnas Núcleo y Galpón. `false` en Colombia (inventario a nivel granja:
   * las columnas irían vacías en el 100 % de las filas). Espejo de `stockShowNucleoGalpon`.
   */
  incluirUbicacion: boolean;
}

const TITULO = 'Stock de inventario — todas las granjas asignadas';

/** Sin ubicación cae al id; sin ninguno de los dos, guion (mismo fallback que la grilla). */
function textoUbicacion(nombre: string | null | undefined, id: string | null | undefined): string {
  return nombre ?? id ?? '—';
}

/** Cabeceras del `.xlsx`; con Núcleo/Galpón solo si la ubicación aplica. */
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

/** Construye y descarga el `.xlsx` del stock con las filas y el contexto dados. */
export function exportarStockExcel(rows: InventarioGestionStockDto[], meta: ExportarStockExcelMeta): void {
  exportarTablaExcel(
    cabecerasStockExcel(meta.incluirUbicacion),
    construirFilasStockExcel(rows, { incluirUbicacion: meta.incluirUbicacion }),
    {
      filenameBase: 'stock-inventario-todas-granjas',
      sheetName: 'Stock',
      title: TITULO,
      subtitles: meta.filtros
    }
  );
}

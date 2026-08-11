/**
 * Tipos compartidos del módulo **Gastos de inventario**.
 *
 * Viven acá (y no en el servicio) para que las funciones de `funciones/` puedan tiparse sin
 * importar el servicio — que arrastraría `HttpClient` y crearía un import circular. El servicio
 * los **re-exporta**, así que los imports existentes siguen funcionando igual.
 */

export interface LoteFilterItemDto {
  loteId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId: string | null;
  galponId: string | null;
  loteErp?: string | null;
}

export interface FarmDto {
  id: number;
  name: string;
  companyId: number;
}

export interface NucleoDto {
  nucleoId: string;
  granjaId: number;
  nucleoNombre: string;
  granjaNombre?: string;
}

export interface GalponLiteDto {
  galponId: string;
  galponNombre: string;
  nucleoId: string;
  granjaId: number;
}

export interface FilterDataResponse {
  farms: FarmDto[];
  nucleos: NucleoDto[];
  galpones: GalponLiteDto[];
  lotes: LoteFilterItemDto[];
}

export interface InventarioGastoItemStockDto {
  itemInventarioEcuadorId: number;
  codigo: string;
  nombre: string;
  tipoItem: string;
  unidad: string;
  concepto: string | null;
  stockCantidad: number;
}

export interface InventarioGastoLineaRequest {
  itemInventarioEcuadorId: number;
  cantidad: number;
}

export interface CreateInventarioGastoRequest {
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  loteAveEngordeId: number | null;
  /**
   * Lote PROGRAMADO (lote base) cuando el lote real todavía no existe — desinsectación previa al
   * encaset. Excluyente con `loteAveEngordeId`. Al crearse el lote real desde esa programación, el
   * backend re-atribuye el gasto solo.
   */
  loteBaseEngordeId?: number | null;
  fecha: string; // yyyy-MM-dd
  observaciones?: string | null;
  concepto: string;
  lineas: InventarioGastoLineaRequest[];
}

/** Resumen de una línea/ítem consumido, para mostrarlo inline en la tabla. */
export interface InventarioGastoLineaResumenDto {
  codigo: string;
  nombre: string;
  cantidad: number;
  unidad: string;
}

export interface InventarioGastoListItemDto {
  id: number;
  fecha: string;
  farmId: number;
  granjaNombre: string | null;
  nucleoId: string | null;
  nucleoNombre: string | null;
  galponId: string | null;
  galponNombre: string | null;
  loteAveEngordeId: number | null;
  loteNombre: string | null;
  /** Lote programado (lote base) si el gasto todavía no se atribuyó a un lote real. */
  loteBaseEngordeId: number | null;
  loteBaseNombre: string | null;
  observaciones: string | null;
  estado: string;
  lineas: number;
  totalCantidad: number;
  unidad: string | null;
  createdAt: string;
  createdByUserId: string | null;
  items: InventarioGastoLineaResumenDto[];
}

export interface InventarioGastoDetalleDto {
  id: number;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemType: string;
  concepto: string | null;
  cantidad: number;
  unidad: string;
  stockAntes: number | null;
  stockDespues: number | null;
}

/** Una fila por línea de consumo del reporte. El backend NUNCA devuelve gastos eliminados acá. */
export interface InventarioGastoExportRowDto {
  inventarioGastoId: number;
  fecha: string;
  estado: string;
  observacionesCabecera: string | null;
  farmId: number;
  granjaNombre: string;
  nucleoId: string | null;
  nucleoNombre: string | null;
  galponId: string | null;
  galponNombre: string | null;
  loteAveEngordeId: number | null;
  loteNombre: string | null;
  /** Lote programado (lote base) si el gasto todavía no se atribuyó a un lote real. */
  loteBaseEngordeId: number | null;
  loteBaseNombre: string | null;
  detalleId: number;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemTipo: string;
  conceptoLinea: string | null;
  cantidad: number;
  unidad: string;
  stockAntes: number | null;
  stockDespues: number | null;
  createdAt: string;
  createdByUserId: string | null;
  deletedAt: string | null;
  deletedByUserId: string | null;
}

/**
 * Existencia de un ítem del catálogo en una granja. El universo son TODOS los ítems no-alimento
 * activos: un ítem sin consumo viene igual, con su saldo y `consumidoRango = 0`.
 */
export interface InventarioGastoExistenciaDto {
  farmId: number;
  granjaNombre: string | null;
  itemInventarioEcuadorId: number;
  codigo: string;
  nombre: string;
  tipoItem: string;
  unidad: string;
  concepto: string | null;
  saldoActual: number;
  consumidoRango: number;
  gastosRango: number;
}

export interface InventarioGastoDto {
  id: number;
  fecha: string;
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  loteAveEngordeId: number | null;
  loteNombre: string | null;
  /** Lote programado (lote base) si el gasto todavía no se atribuyó a un lote real. */
  loteBaseEngordeId: number | null;
  loteBaseNombre: string | null;
  observaciones: string | null;
  estado: string;
  createdAt: string;
  createdByUserId: string | null;
  deletedAt: string | null;
  deletedByUserId: string | null;
  detalles: InventarioGastoDetalleDto[];
}

/** Estado de gasto que el usuario puede pedir en el listado. `''` = todos. */
export type EstadoGastoFiltro = '' | 'Activo' | 'Eliminado';

/**
 * Tipos de la tabla de movimientos (listado).
 *
 * Se extraen del componente de página para poder reutilizarlos desde `funciones/`
 * (p. ej. `agrupar-despachos.funcion.ts`) sin generar imports circulares, y como base
 * de contratos compartidos para los futuros listados por país.
 */
import { MovimientoPolloEngordeDto } from '../services/movimiento-pollo-engorde.service';

/** Opción del dropdown Lote (solo Ave Engorde). */
export interface LoteOption {
  value: string; // "ae-123"
  tipo: 'ae';
  id: number;
  label: string;
}

/** Fila agrupada: varios movimientos de venta con el mismo número de despacho (mismo viaje). */
export interface FilaDespachoGrupo {
  kind: 'despacho-grupo';
  clave: string;
  numeroDespacho: string;
  fechaMovimiento: string;
  movimientos: MovimientoPolloEngordeDto[];
}

export interface FilaMovimientoSimple {
  kind: 'simple';
  movimiento: MovimientoPolloEngordeDto;
}

export type FilaTablaMovimiento = FilaDespachoGrupo | FilaMovimientoSimple;

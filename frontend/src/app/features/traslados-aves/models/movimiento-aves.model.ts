// src/app/features/traslados-aves/models/movimiento-aves.model.ts
// Movimientos de aves (`movimiento_aves`): el registro que deja todo traslado o venta.
//
// Extraido de `services/traslados-aves.service.ts` (3-sep-2026): el service tenia 29 interfaces
// de 4 dominios distintos en la cabecera. El service los RE-EXPORTA, asi que los imports que ya
// existian siguen funcionando sin tocarse.

// Movimiento de Aves
export interface UbicacionMovimientoDto {
  loteId?: number | null;
  loteNombre?: string | null;
  granjaId?: number | null;
  granjaNombre?: string | null;
  nucleoId?: string | null;
  nucleoNombre?: string | null;
  galponId?: string | null;
  galponNombre?: string | null;
}

export interface ResultadoMovimientoDto {
  success: boolean;
  message: string;
  movimientoId?: number | null;
  numeroMovimiento?: string | null;
  errores?: string[];
  movimiento?: MovimientoAvesDto | null;
}

export interface MovimientoAvesDto {
  id: number;
  numeroMovimiento: string;
  fechaMovimiento: string | Date;
  tipoMovimiento: string;
  origen?: UbicacionMovimientoDto | null;
  destino?: UbicacionMovimientoDto | null;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  totalAves: number;
  estado: string;
  motivoMovimiento?: string | null;
  observaciones?: string | null;
  usuarioMovimientoId: number;
  usuarioNombre?: string | null;
  fechaProcesamiento?: string | Date | null;
  fechaCancelacion?: string | Date | null;
  createdAt: string | Date;
  
  // Campos adicionales para compatibilidad (si vienen del backend)
  loteOrigenId?: number | null;
  loteDestinoId?: number | null;
  granjaOrigenId?: number | null;
  granjaDestinoId?: number | null;
  granjaOrigenNombre?: string | null;
  granjaDestinoNombre?: string | null;
}

export interface CreateMovimientoAvesDto {
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  tipoMovimiento: string;
  observaciones?: string;
  fechaMovimiento: Date;
}

export interface MovimientoAvesSearchRequest {
  numeroMovimiento?: string;
  tipoMovimiento?: string;
  estado?: string;
  loteOrigenId?: string;
  loteDestinoId?: string;
  granjaOrigenId?: number;
  granjaDestinoId?: number;
  fechaDesde?: Date;
  fechaHasta?: Date;
  usuarioMovimientoId?: number;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// src/app/features/traslados-aves/models/historial-inventario.model.ts
// Historial y trazabilidad del inventario de aves.
//
// Extraido de `services/traslados-aves.service.ts` (3-sep-2026): el service tenia 29 interfaces
// de 4 dominios distintos en la cabecera. El service los RE-EXPORTA, asi que los imports que ya
// existian siguen funcionando sin tocarse.

// Historial de Inventario
export interface HistorialInventarioDto {
  id: number;
  companyId: number;
  loteId: string;
  cantidadHembrasAntes: number;
  cantidadMachosAntes: number;
  cantidadHembrasDespues: number;
  cantidadMachosDespues: number;
  tipoEvento: string;
  referenciaMovimientoId?: string;
  fechaRegistro: Date;
  createdAt: Date;
  updatedAt?: Date;
}

export interface HistorialInventarioSearchRequest {
  inventarioId?: number;
  loteId?: string;
  tipoCambio?: string;
  movimientoId?: number;
  granjaId?: number;
  nucleoId?: string;
  galponId?: string;
  fechaDesde?: Date;
  fechaHasta?: Date;
  usuarioCambioId?: number;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

export interface EventoTrazabilidadDto {
  fecha: Date;
  tipoEvento: string;
  descripcion: string;
  cantidadHembrasAntes: number;
  cantidadMachosAntes: number;
  cantidadHembrasDespues: number;
  cantidadMachosDespues: number;
  usuario?: string;
  referenciaMovimiento?: string;
}

export interface TrazabilidadLoteDto {
  loteId: string;
  eventos: EventoTrazabilidadDto[];
}

// src/app/features/traslados-aves/models/inventario-aves.model.ts
// Inventario de aves: existencias por lote/ubicacion y sus ajustes.
//
// Extraido de `services/traslados-aves.service.ts` (3-sep-2026): el service tenia 29 interfaces
// de 4 dominios distintos en la cabecera. El service los RE-EXPORTA, asi que los imports que ya
// existian siguen funcionando sin tocarse.

// Inventario de Aves
export interface InventarioAvesDto {
  id: number;
  companyId: number;
  loteId: string;
  granjaId: number;
  nucleoId: string;
  galponId?: string;
  cantidadHembras: number;
  cantidadMachos: number;
  fechaUltimoConteo: Date;
  createdAt: Date;
  updatedAt?: Date;
}

export interface CreateInventarioAvesDto {
  loteId: string;
  granjaId: number;
  nucleoId: string;
  galponId?: string;
  cantidadHembras: number;
  cantidadMachos: number;
  fechaUltimoConteo: Date;
}

export interface UpdateInventarioAvesDto extends CreateInventarioAvesDto {
  id: number;
}

export interface InventarioAvesSearchRequest {
  loteId?: string;
  granjaId?: number;
  nucleoId?: string;
  galponId?: string;
  estado?: string;
  fechaDesde?: Date;
  fechaHasta?: Date;
  soloActivos?: boolean;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// Interfaces Auxiliares
export interface ResumenInventarioDto {
  totalLotes: number;
  totalHembras: number;
  totalMachos: number;
  totalAves: number;
  resumenPorGranja: ResumenPorGranjaDto[];
}

export interface ResumenPorGranjaDto {
  granjaId: number;
  granjaNombre: string;
  nucleoId: string;
  nucleoNombre?: string;
  galponId?: string;
  galponNombre?: string;
  cantidadLotes: number;
  totalHembras: number;
  totalMachos: number;
  totalAves: number;
  fechaUltimaActualizacion: Date;
}

export interface AjusteInventarioRequest {
  cantidadHembras: number;
  cantidadMachos: number;
  tipoEvento: string;
  observaciones?: string;
}

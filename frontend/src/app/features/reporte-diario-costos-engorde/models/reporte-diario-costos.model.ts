// frontend/src/app/features/reporte-diario-costos-engorde/models/reporte-diario-costos.model.ts
// Contrato del Reporte Diario Costos de pollo engorde (espejo de
// ReporteDiarioCostosEngordeDtos.cs — endpoint POST /api/ReporteDiarioCostosEngorde/generar).

/** Filtros del reporte. Sin lote base = TODOS los lotes de la granja. */
export interface ReporteDiarioCostosRequest {
  granjaId: number;
  loteBaseEngordeId?: number | null;
  /** 'yyyy-MM-dd'. NULL = regla del segundo lote (encaset del lote más reciente). */
  fechaInicio?: string | null;
  /** 'yyyy-MM-dd'. NULL = hoy. */
  fechaFin?: string | null;
}

/** Desglose de alimento del día (granja completa). stockKg null = sin snapshot. */
export interface ReporteDiarioCostosAlimento {
  nombreAlimento: string;
  stockKg: number | null;
  consumoKg: number;
}

/** Métricas del día para un galpón (columna dinámica). */
export interface ReporteDiarioCostosGalponDia {
  galponId: string;
  galponNombre: string;
  mortalidad: number;
  seleccion: number;
  errSexaje: number;
  mortSel: number;
  consumoKg: number;
  avesVivas: number;
}

/** Una fila del reporte = una fecha (todos los lotes unificados). */
export interface ReporteDiarioCostosFila {
  fecha: string;
  consumoTotalKg: number;
  mortSelTotal: number;
  avesVivasTotal: number;
  alimentos: ReporteDiarioCostosAlimento[];
  galpones: ReporteDiarioCostosGalponDia[];
}

export interface ReporteDiarioCostosLote {
  loteAveEngordeId: number;
  loteNombre: string;
  galponId: string;
  galponNombre: string;
  fechaEncaset: string | null;
  estadoOperativoLote: string | null;
}

export interface ReporteDiarioCostosGalponHeader {
  galponId: string;
  galponNombre: string;
  lotes: string[];
}

export interface ReporteDiarioCostosAlimentoTotal {
  nombreAlimento: string;
  consumoKg: number;
}

export interface ReporteDiarioCostosGalponTotal {
  galponId: string;
  galponNombre: string;
  mortalidad: number;
  seleccion: number;
  errSexaje: number;
  mortSel: number;
}

/** Footer: SUMA TOTAL global, por alimento y por galpón. */
export interface ReporteDiarioCostosTotales {
  consumoTotalKg: number;
  mortSelTotal: number;
  alimentos: ReporteDiarioCostosAlimentoTotal[];
  porGalpon: ReporteDiarioCostosGalponTotal[];
}

/** Aves vivas "actuales" (última fecha del reporte) por galpón. */
export interface ReporteDiarioCostosAvesActuales {
  galponId: string;
  galponNombre: string;
  avesVivas: number;
}

export interface ReporteDiarioCostosReporte {
  filtrosAplicados: ReporteDiarioCostosRequest;
  fechaInicioEfectiva: string | null;
  fechaFinEfectiva: string | null;
  granjaId: number;
  granjaNombre: string;
  loteBaseEngordeId: number | null;
  loteBaseNombre: string | null;
  lotes: ReporteDiarioCostosLote[];
  galpones: ReporteDiarioCostosGalponHeader[];
  avesVivasActuales: ReporteDiarioCostosAvesActuales[];
  avesVivasActualesTotal: number;
  filas: ReporteDiarioCostosFila[];
  totales: ReporteDiarioCostosTotales;
}

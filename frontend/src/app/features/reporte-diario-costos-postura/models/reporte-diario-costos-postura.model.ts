// frontend/src/app/features/reporte-diario-costos-postura/models/reporte-diario-costos-postura.model.ts
//
// Contrato del Reporte Diario Área de Costos de POSTURA (levante + producción).
// Espeja `ZooSanMarino.Application.DTOs.ReporteDiarioCostosPostura` en camelCase.
// NO tiene relación con el reporte homónimo de engorde.

/** "Levante" | "Produccion" | null = ambas. */
export type FasePostura = 'Levante' | 'Produccion';

export interface ReporteDiarioCostosPosturaRequest {
  granjaId?: number | null;
  regional?: string | null;
  lotePosturaBaseId?: number | null;
  /** null = ambas fases. */
  fase?: string | null;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
}

/** Un ítem de alimento del día para un sexo. `origen`: 'metadata' (desglose real) | 'tipo_alimento' (fallback). */
export interface ReporteDiarioCostosPosturaAlimento {
  sexo: 'H' | 'M' | string;
  nombre: string;
  cantidadKg: number;
  origen: string;
}

/** Huevo ya clasificado por el backend: fértil + comercial + inservible == total. */
export interface ReporteDiarioCostosPosturaHuevo {
  fertil: number;
  comercial: number;
  inservible: number;
  total: number;
  venta: number;
  trasladoPlanta: number;
  particionCuadra: boolean;
}

export interface ReporteDiarioCostosPosturaFila {
  fecha: string;
  fase: FasePostura | string;
  loteId: number;
  loteNombre: string;
  galponId: string;
  galponNombre: string;
  /** "lote : galpón" — la etiqueta que pide el diseño. */
  loteGalpon: string;
  nucleoId: string;
  granjaId: number;
  granjaNombre: string;
  regional: string;
  lotePosturaBaseId: number | null;
  loteBaseNombre: string;
  edadDias: number | null;
  semana: number | null;

  mortalidadH: number;
  mortalidadM: number;
  seleccionH: number;
  seleccionM: number;
  errorSexajeH: number;
  errorSexajeM: number;
  ventaAvesH: number;
  ventaAvesM: number;

  consumoKgH: number;
  consumoKgM: number;
  alimentos: ReporteDiarioCostosPosturaAlimento[];

  huevo: ReporteDiarioCostosPosturaHuevo;
}

export interface ReporteDiarioCostosPosturaTotalesAves {
  mortalidadH: number;
  mortalidadM: number;
  seleccionH: number;
  seleccionM: number;
  errorSexajeH: number;
  errorSexajeM: number;
  ventaAvesH: number;
  ventaAvesM: number;
  totalH: number;
  totalM: number;
  total: number;
}

export interface ReporteDiarioCostosPosturaTotalAlimento {
  sexo: string;
  nombre: string;
  cantidadKg: number;
}

export interface ReporteDiarioCostosPosturaTotales {
  aves: ReporteDiarioCostosPosturaTotalesAves;
  consumoKgH: number;
  consumoKgM: number;
  consumoKgTotal: number;
  alimentos: ReporteDiarioCostosPosturaTotalAlimento[];
  huevo: ReporteDiarioCostosPosturaHuevo;
}

export interface ReporteDiarioCostosPosturaLote {
  loteId: number;
  loteNombre: string;
  galponId: string;
  galponNombre: string;
  loteGalpon: string;
  granjaId: number;
  granjaNombre: string;
  lotePosturaBaseId: number | null;
  loteBaseNombre: string;
}

export interface ReporteDiarioCostosPosturaReporte {
  filtrosAplicados: ReporteDiarioCostosPosturaRequest;
  fechaDesdeEfectiva: string | null;
  fechaHastaEfectiva: string | null;
  fases: string[];
  lotes: ReporteDiarioCostosPosturaLote[];
  filas: ReporteDiarioCostosPosturaFila[];
  totales: ReporteDiarioCostosPosturaTotales;
}

/** Lote base de postura (catálogo del filtro). Subconjunto de `LotePosturaBaseDto`. */
export interface LotePosturaBaseOpcion {
  lotePosturaBaseId: number;
  loteNombre: string;
  farmId: number | null;
  farmNombre: string | null;
  companyId: number;
}

/** Fila expandida de la pestaña Alimento: un ítem por fila (decisión D4). */
export interface FilaAlimentoView {
  fecha: string;
  fechaFmt: string;
  fase: string;
  loteGalpon: string;
  /** Ítem de hembras de esta fila (puede faltar si el día tiene más ítems de machos). */
  hembraNombre: string | null;
  hembraKg: number | null;
  machoNombre: string | null;
  machoKg: number | null;
}

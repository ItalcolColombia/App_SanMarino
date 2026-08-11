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

  /** El lote tiene fila de levante Y de producción ese día (no siempre es un error). */
  diaEnAmbasEtapas: boolean;
  /** Se muestra pero NO suma: su día ya lo aporta producción (regla del corte de etapa). */
  excluidoDelTotal: boolean;
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

/**
 * Dónde ocurrió cada fase: una entrada por (fase, granja, lote base).
 * Es lo que permite leer «el levante fue en NIZA III y la producción en NIZA I».
 */
export interface ReporteDiarioCostosPosturaUbicacion {
  fase: string;
  granjaId: number;
  granjaNombre: string;
  loteBaseNombre: string;
  lotes: number;
  desde: string;
  hasta: string;
  dias: number;
}

export interface ReporteDiarioCostosPosturaReporte {
  filtrosAplicados: ReporteDiarioCostosPosturaRequest;
  fechaDesdeEfectiva: string | null;
  fechaHastaEfectiva: string | null;
  fases: string[];
  lotes: ReporteDiarioCostosPosturaLote[];
  filas: ReporteDiarioCostosPosturaFila[];
  totales: ReporteDiarioCostosPosturaTotales;
  ubicaciones: ReporteDiarioCostosPosturaUbicacion[] | null;
  /** Filas mostradas pero excluidas del total por estar registradas en las dos etapas. */
  diasDuplicados: number;
  /** Cuánto quedó FUERA del total por el traslape (null si no se excluyó nada). */
  totalesExcluidos: ReporteDiarioCostosPosturaTotales | null;
  /** El lote base elegido vivía fuera de la granja pedida y el reporte lo siguió. */
  alcanceExpandidoPorLoteBase: boolean;
}

/**
 * Lote base de postura (catálogo del filtro). Viene de
 * `GET /api/ReporteDiarioCostosPostura/lotes-base`, que lo lista por DÓNDE ESTÁN SUS LOTES
 * (no por el `farm_id` del catálogo) ⇒ una base puede aparecer bajo varias granjas.
 */
export interface LotePosturaBaseOpcion {
  lotePosturaBaseId: number;
  loteNombre: string;
  granjaIds: number[];
  granjaNombres: string[];
  lotes: number;
}

/** Fila expandida de la pestaña Alimento: un ítem por fila (decisión D4). */
export interface FilaAlimentoView {
  fecha: string;
  fechaFmt: string;
  fase: string;
  granjaNombre: string;
  excluidoDelTotal: boolean;
  loteGalpon: string;
  /** Ítem de hembras de esta fila (puede faltar si el día tiene más ítems de machos). */
  hembraNombre: string | null;
  hembraKg: number | null;
  machoNombre: string | null;
  machoKg: number | null;
}

// Modelos de la hoja «RESUMEN SEMANAL» del Informe RA Pesadas.
// Espejo 1:1 de ResumenSemanalRaPesadasDtos.cs.
//
// Es la contracara del Detalle: N lotes de UNA semana calendario, en vez de
// N semanas de UN lote. La semana del año usa la convención WEEKNUM de Excel
// (arranca en domingo), no la semana ISO.

export type EtapaResumen = 'levante' | 'produccion';

export interface ResumenSemanalRaPesadasRequest {
  anio: number;
  semanaAnio: number;
  etapa: EtapaResumen;
  granjaIds?: number[] | null;
  regional?: string | null;
  ciclo?: string | null;              // solo producción
  excluirTrasladados?: boolean;       // solo levante
}

/** Campos comunes a las dos etapas (identificación del lote + saldos). */
export interface ResumenSemanalFilaBase {
  loteNombre: string;
  granjaId: number;
  granjaNombre: string | null;
  nucleoNombre: string | null;
  regional: string | null;
  raza: string | null;
  anioGuia: number | null;
  edadSemana: number;
  fechaFinSemana: string;
  diasConRegistro: number;
  /** Saldo hembras del lote / Σ saldo hembras de la selección. */
  part: number | null;
  saldoHembras: number;
  saldoMachos: number;
}

export interface ResumenSemanalLevanteFila extends ResumenSemanalFilaBase {
  loteId: number;
  tuvoTraslado: boolean;
  mortHembrasPct: number | null;
  retiroAcumHembrasPct: number | null;
  retiroAcumHembrasGuia: number | null;
  difConsumoHembrasPct: number | null;
  difPesoHembrasPct: number | null;
  uniformidadHembras: number | null;
  cvHembras: number | null;
  mortMachosPct: number | null;
  retiroAcumMachosPct: number | null;
  retiroAcumMachosGuia: number | null;
  difConsumoMachosPct: number | null;
  difPesoMachosPct: number | null;
  uniformidadMachos: number | null;
  cvMachos: number | null;
}

export interface ResumenSemanalProduccionFila extends ResumenSemanalFilaBase {
  lotePosturaProduccionId: number;
  loteId: number | null;
  cicloProduccion: string | null;
  tipoNido: string | null;
  produccionPct: number | null;
  produccionPctGuia: number | null;
  difProduccionPct: number | null;
  htaa: number | null;
  htaaGuia: number | null;
  difHtaa: number | null;
  hiaa: number | null;
  hiaaGuia: number | null;
  difHiaa: number | null;
  aprovSemPct: number | null;
  aprovSemPctGuia: number | null;
  difAprovSemPct: number | null;
  grHuevoInc: number | null;
  mortHembrasPct: number | null;
  retiroAcumHembrasPct: number | null;
  retiroAcumHembrasGuia: number | null;
  mortMachosPct: number | null;
  retiroAcumMachosPct: number | null;
  retiroAcumMachosGuia: number | null;
  pesoMachoSobreHembra: number | null;
}

/**
 * Pie de la hoja. Los saldos SUMAN; `ponderados` son promedio ponderado por
 * saldo de hembras (no promedio simple), indexados por el nombre del indicador.
 */
export interface ResumenSemanalTotales {
  lotes: number;
  saldoHembras: number;
  saldoMachos: number;
  ponderados: Record<string, number | null>;
}

export interface ResumenSemanalRaPesadasLevanteResponse {
  anio: number;
  semanaAnio: number;
  fechaInicioSemana: string | null;
  fechaFinSemana: string | null;
  filas: ResumenSemanalLevanteFila[];
  totales: ResumenSemanalTotales;
}

export interface ResumenSemanalRaPesadasProduccionResponse {
  anio: number;
  semanaAnio: number;
  fechaInicioSemana: string | null;
  fechaFinSemana: string | null;
  filas: ResumenSemanalProduccionFila[];
  totales: ResumenSemanalTotales;
}

// ─────────────────────────────────────────────────────────────────────────────
// CURVA CONSOLIDADA — todos los lotes a lo largo de todas las EDADES.
// Tercera granularidad: el Resumen es «todos los lotes en una semana» y el
// Detalle «un lote en todas sus semanas»; ésta es la curva de la operación.
// ─────────────────────────────────────────────────────────────────────────────

export interface CurvaConsolidadaRequest {
  anio: number;
  etapa: EtapaResumen;
  granjaIds?: number[] | null;
  regional?: string | null;
  ciclo?: string | null;
  excluirTrasladados?: boolean;
}

export interface CurvaConsolidadaPunto {
  edadSemana: number;
  lotes: number;
  saldoHembras: number;
  saldoMachos: number;
  /** Indicador → promedio ponderado por saldo de hembras. */
  indicadores: Record<string, number | null>;
}

export interface CurvaConsolidadaResponse {
  anio: number;
  etapa: EtapaResumen;
  lotes: number;
  puntos: CurvaConsolidadaPunto[];
}

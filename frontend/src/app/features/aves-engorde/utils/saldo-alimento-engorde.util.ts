/**
 * Regla de negocio — saldo de alimento en galpón (kg), alineada con libro de seguimiento / Excel.
 *
 * Por cada paso de la secuencia cronológica (mismo día u otro):
 *
 * **SaldoNuevo = SaldoAnterior + IngresoAlimento + Traslado(entrada) − ConsumoReal − Traslado(salida)**
 *
 * En el histórico unificado, cada movimiento aporta un **delta** (kg):
 * - INV_INGRESO → +cantidad
 * - INV_TRASLADO_ENTRADA → +cantidad
 * - INV_TRASLADO_SALIDA → −cantidad
 * - Consumo del seguimiento del día → −(consumoKgHembras + consumoKgMachos)
 * - INV_OTRO (ajuste/eliminación de stock) → delta según tipo (no duplicar INV_CONSUMO de bodega)
 *
 * Tras cada movimiento se aplica **mínimo 0 kg**: no hay inventario negativo; un ingreso o traslado de entrada suma sobre el saldo disponible (nunca sobre una “deuda” de alimento).
 */
export const TEXTO_FORMULA_SALDO_ALIMENTO_TOOLTIP =
  'Saldo disponible (≥ 0): tras cada movimiento se aplica piso en 0; luego ingreso/traslado entrada − consumo (H+M) − traslado salida y ajustes según histórico.';

/** Saldo tras aplicar un único delta (un término de la fórmula). */
export function saldoTrasDeltaKg(saldoAnteriorKg: number, deltaKg: number): number {
  return saldoAnteriorKg + deltaKg;
}

/** Saldo tras aplicar en orden todos los deltas (equivale a una suma algebraica de la fórmula extendida). */
export function saldoTrasSecuenciaDeltasKg(saldoInicialKg: number, deltasKg: readonly number[]): number {
  let s = saldoInicialKg;
  for (const d of deltasKg) s += d;
  return s;
}

/** Comprueba que saldo final = saldo inicial + suma de deltas (validación interna de la secuencia). */
export function esCoherenteSecuenciaSaldoKg(
  saldoInicialKg: number,
  deltasKg: readonly number[],
  saldoFinalEsperadoKg: number,
  eps = 1e-6
): boolean {
  const calc = saldoTrasSecuenciaDeltasKg(saldoInicialKg, deltasKg);
  return Math.abs(calc - saldoFinalEsperadoKg) <= eps;
}

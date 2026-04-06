/**
 * Formatea un número con hasta `maxDecimals` decimales, sin ceros finales innecesarios
 * (p. ej. 400 en lugar de 400,00; 12,5 en lugar de 12,50).
 */
export function formatDecimalTrim(value: number, maxDecimals: number): string {
  if (!Number.isFinite(value)) {
    return '';
  }
  const rounded = Number.parseFloat(value.toFixed(maxDecimals));
  return String(rounded);
}

/**
 * Helpers de formato del módulo — re-exportados desde el central `shared/utils/format.ts`
 * (una sola fuente de verdad para todo el front). Se mantiene este archivo para no romper los
 * imports internos del módulo (`./formato.funcion`). Antes definían la lógica aquí; ahora vive
 * en `format.ts` y este archivo solo la re-expone.
 */
export { formatearNumero, fechaCorta, ymdToIsoUtcNoon } from '../../../shared/utils/format';

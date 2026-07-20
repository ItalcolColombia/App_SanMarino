/**
 * Helpers puros de clasificación de estado, formato y edad para la liquidación técnica de levante.
 *
 * Extraídos de `liquidacion-tecnica.component.ts` SIN cambiar comportamiento: mismos umbrales,
 * mismas cadenas devueltas, misma aritmética (`Math.abs`, `Math.floor`, divisiones y redondeos).
 * Módulo de cálculo financiero/técnico frágil: preservar cada número y cada rama exactamente.
 * Funciones puras: sin `this`, sin DI, sin estado de Angular.
 */

/** Clase CSS para el estado de un indicador según diferencia/tipo/cumple. */
export function getEstadoClase(diferencia: number | null | undefined, tipo: string, cumple?: boolean): string {
  if (cumple !== undefined) {
    return cumple ? 'estado-bueno' : 'estado-critico';
  }

  if (diferencia === null || diferencia === undefined) return 'estado-neutral';

  const umbral = tipo === 'porcentaje' ? 2 : 5; // 2% para porcentajes, 5% para otros

  if (Math.abs(diferencia) <= umbral) return 'estado-bueno';
  if (Math.abs(diferencia) <= umbral * 2) return 'estado-alerta';
  return 'estado-critico';
}

/** Formatea una fecha para mostrar (es-ES) o '—' si no hay valor. */
export function formatDate(date: Date | string | null | undefined): string {
  if (!date) return '—';
  const d = typeof date === 'string' ? new Date(date) : date;
  return d.toLocaleDateString('es-ES');
}

/** Mensaje de error amigable según el status HTTP. */
export function getErrorMessageLiquidacion(error: any): string {
  if (error.status === 404) {
    return 'Lote no encontrado o sin datos para liquidación técnica';
  }
  if (error.status === 400) {
    return 'Parámetros inválidos para el cálculo';
  }
  if (error.status === 500) {
    return 'Error interno del servidor';
  }
  return 'Error desconocido al calcular liquidación técnica';
}

/** Edad en semanas completas desde la fecha de encasetamiento hasta hoy. */
export function calcularEdadSemanas(fechaEncaset: string | Date): number {
  const fechaInicio = new Date(fechaEncaset);
  const fechaActual = new Date();
  const diferenciaDias = Math.floor((fechaActual.getTime() - fechaInicio.getTime()) / (1000 * 60 * 60 * 24));
  return Math.floor(diferenciaDias / 7);
}

/** Edad en días desde la fecha de encasetamiento hasta hoy (0 si no hay fecha). */
export function calcularEdadDias(fechaEncaset: string | Date | undefined): number {
  if (!fechaEncaset) return 0;
  const fechaInicio = new Date(fechaEncaset);
  const fechaActual = new Date();
  return Math.floor((fechaActual.getTime() - fechaInicio.getTime()) / (1000 * 60 * 60 * 24));
}

/** Clase CSS según la magnitud absoluta de una diferencia. */
export function getDiferenciaClass(diferencia: number): string {
  const absDiferencia = Math.abs(diferencia);
  if (absDiferencia <= 5) return 'diferencia-optima';
  if (absDiferencia <= 15) return 'diferencia-aceptable';
  return 'diferencia-problema';
}

/** Clase CSS de estado según el % de desviación esperado/real por tipo de indicador. */
export function getEstadoClass(tipo: string, esperado: number, real: number): string {
  const diferencia = Math.abs(esperado - real);
  const porcentaje = esperado > 0 ? (diferencia / esperado) * 100 : 100;

  switch (tipo) {
    case 'peso':
      return porcentaje <= 5 ? 'estado-optimo' : porcentaje <= 10 ? 'estado-aceptable' : 'estado-problema';
    case 'consumo':
      return porcentaje <= 10 ? 'estado-optimo' : porcentaje <= 20 ? 'estado-aceptable' : 'estado-problema';
    case 'mortalidad':
      return porcentaje <= 20 ? 'estado-optimo' : porcentaje <= 40 ? 'estado-aceptable' : 'estado-problema';
    default:
      return 'estado-aceptable';
  }
}

/** Texto de estado según el % de desviación esperado/real por tipo de indicador. */
export function getEstadoTexto(tipo: string, esperado: number, real: number): string {
  const diferencia = Math.abs(esperado - real);
  const porcentaje = esperado > 0 ? (diferencia / esperado) * 100 : 100;

  switch (tipo) {
    case 'peso':
      if (porcentaje <= 5) return 'Óptimo';
      if (porcentaje <= 10) return 'Aceptable';
      return real < esperado ? 'Bajo' : 'Alto';
    case 'consumo':
      if (porcentaje <= 10) return 'Óptimo';
      if (porcentaje <= 20) return 'Aceptable';
      return real < esperado ? 'Bajo' : 'Alto';
    case 'mortalidad':
      if (porcentaje <= 20) return 'Normal';
      if (porcentaje <= 40) return 'Aceptable';
      return real < esperado ? 'Baja' : 'Alta';
    default:
      return 'Aceptable';
  }
}

/** Clase CSS del cumplimiento general según el % acumulado. */
export function getCumplimientoClass(porcentajeCumplimientoGeneral: number): string {
  if (porcentajeCumplimientoGeneral >= 90) return 'cumplimiento-excelente';
  if (porcentajeCumplimientoGeneral >= 75) return 'cumplimiento-bueno';
  if (porcentajeCumplimientoGeneral >= 60) return 'cumplimiento-aceptable';
  return 'cumplimiento-problema';
}

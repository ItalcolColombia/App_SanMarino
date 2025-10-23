// src/app/features/lote-produccion/mappers/mapper.utils.ts
// Utilidades adicionales para los mappers

import { LoteProduccionDto } from '../services/lote-produccion.service';

// ==================== UTILIDADES DE FECHA ====================

export function formatDateForDisplay(date: string | Date): string {
  const d = new Date(date);
  const day = String(d.getDate()).padStart(2, '0');
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const year = d.getFullYear();
  return `${day}/${month}/${year}`;
}

export function formatDateForAPI(date: string): string {
  const [day, month, year] = date.split('/').map(Number);
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

export function getTodayISO(): string {
  return new Date().toISOString().split('T')[0];
}

export function getTodayDMY(): string {
  return formatDateForDisplay(new Date());
}

// ==================== UTILIDADES DE CÁLCULO ====================

export function calculateMortalidadTotal(registros: LoteProduccionDto[]): number {
  return registros.reduce((sum, r) => sum + r.mortalidadH + r.mortalidadM, 0);
}

export function calculateConsumoTotal(registros: LoteProduccionDto[]): number {
  return registros.reduce((sum, r) => sum + r.consKgH + r.consKgM, 0);
}

export function calculateHuevosTotal(registros: LoteProduccionDto[]): number {
  return registros.reduce((sum, r) => sum + r.huevoTot, 0);
}

export function calculatePesoPromedioHuevo(registros: LoteProduccionDto[]): number {
  if (registros.length === 0) return 0;
  const pesoTotal = registros.reduce((sum, r) => sum + r.pesoHuevo, 0);
  return pesoTotal / registros.length;
}

// ==================== UTILIDADES DE VALIDACIÓN ====================

export function isValidEtapa(etapa: number): boolean {
  return etapa >= 1 && etapa <= 3;
}

export function isValidPositiveNumber(value: number): boolean {
  return value >= 0;
}

export function isValidRequiredString(value: string): boolean {
  return Boolean(value && value.trim().length > 0);
}

// ==================== UTILIDADES DE FORMATEO ====================

export function formatNumber(value: number, decimals: number = 2): string {
  return value.toFixed(decimals);
}

export function formatPercentage(value: number, decimals: number = 1): string {
  return `${formatNumber(value, decimals)}%`;
}

export function formatWeight(grams: number): string {
  if (grams >= 1000) {
    return `${formatNumber(grams / 1000, 2)} kg`;
  }
  return `${formatNumber(grams, 0)} g`;
}

// ==================== UTILIDADES DE ARRAYS ====================

export function groupBy<T>(array: T[], key: keyof T): Record<string, T[]> {
  return array.reduce((groups, item) => {
    const group = String(item[key]);
    groups[group] = groups[group] || [];
    groups[group].push(item);
    return groups;
  }, {} as Record<string, T[]>);
}

export function sortBy<T>(array: T[], key: keyof T, direction: 'asc' | 'desc' = 'asc'): T[] {
  return [...array].sort((a, b) => {
    const aVal = a[key];
    const bVal = b[key];
    
    if (aVal < bVal) return direction === 'asc' ? -1 : 1;
    if (aVal > bVal) return direction === 'asc' ? 1 : -1;
    return 0;
  });
}

export function filterBy<T>(array: T[], predicate: (item: T) => boolean): T[] {
  return array.filter(predicate);
}

// ==================== UTILIDADES DE MAPAS ====================

export function createMapFromArray<T, K, V>(
  array: T[],
  keyExtractor: (item: T) => K,
  valueExtractor: (item: T) => V
): Map<K, V> {
  const map = new Map<K, V>();
  array.forEach(item => {
    map.set(keyExtractor(item), valueExtractor(item));
  });
  return map;
}

export function getMapValue<K, V>(map: Map<K, V>, key: K, defaultValue: V): V {
  return map.get(key) ?? defaultValue;
}

// ==================== UTILIDADES DE ESTADOS ====================

export function getEtapaLabel(etapa: number): string {
  const labels = {
    1: 'Inicio',
    2: 'Desarrollo',
    3: 'Producción'
  };
  return labels[etapa as keyof typeof labels] || 'Desconocida';
}

export function getEtapaColor(etapa: number): string {
  const colors = {
    1: '#4CAF50', // Verde
    2: '#FF9800', // Naranja
    3: '#2196F3'  // Azul
  };
  return colors[etapa as keyof typeof colors] || '#9E9E9E';
}

// ==================== UTILIDADES DE ERRORES ====================

export function createError(message: string, code?: string): Error {
  const error = new Error(message);
  if (code) {
    (error as any).code = code;
  }
  return error;
}

export function isValidationError(error: any): boolean {
  return error && error.code === 'VALIDATION_ERROR';
}

// ==================== UTILIDADES DE DEBUGGING ====================

export function logMapperOperation(operation: string, input: any, output: any): void {
  if (process.env['NODE_ENV'] === 'development') {
    console.log(`[Mapper] ${operation}:`, {
      input: JSON.stringify(input, null, 2),
      output: JSON.stringify(output, null, 2)
    });
  }
}

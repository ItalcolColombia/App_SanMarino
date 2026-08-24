// src/app/features/traslados-aves/funciones/inventario-dashboard-formato.funcion.ts
// Fecha, número y texto — extraído de InventarioDashboardComponent.
// Funciones PURAS: sin `this`, sin DI, sin estado del componente.

import { InventarioAvesDto } from '../services/traslados-aves.service';

// Rango Unicode de diacriticos combinables (U+0300-U+036F), construido por código de punto en vez
// de caracter literal para no arriesgar corrupción de codificación en el archivo fuente.
const COMBINING_DIACRITICS = new RegExp(
  '[' + String.fromCharCode(0x0300) + '-' + String.fromCharCode(0x036f) + ']', 'g'
);

export function hoyISO(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function calcularTotalAves(inv: InventarioAvesDto): number {
  return (inv?.cantidadHembras || 0) + (inv?.cantidadMachos || 0);
}

export function formatearFecha(fecha: Date | string): string {
  if (!fecha) return '—';
  const d = typeof fecha === 'string' ? new Date(fecha) : fecha;
  return d.toLocaleDateString('es-CO', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit'
  });
}

export function formatearNumero(n: number): string {
  return (n ?? 0).toLocaleString('es-CO', { maximumFractionDigits: 0 });
}

/** Quita tildes y pasa a minúsculas, para comparar texto sin acentos. */
export function normalize(s: string): string {
  return (s || '').toLowerCase().normalize('NFD').replace(COMBINING_DIACRITICS, '');
}

export function calcularEdadDias(fecha?: string | Date | null): number {
  if (!fecha) return 0;
  const inicio = new Date(fecha);
  const hoy = new Date();
  const msDia = 1000 * 60 * 60 * 24;
  return Math.floor((hoy.getTime() - inicio.getTime()) / msDia) + 1;
}

export function toYMD(input: Date | string): string {
  const d = typeof input === 'string' ? new Date(input) : input;
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

export function ymdToIsoNoon(ymd: string): string {
  return new Date(`${ymd}T12:00:00`).toISOString();
}

export function obtenerTipoMovimientoClass(tipo: string): string {
  const tipoLower = tipo?.toLowerCase() || '';
  if (tipoLower.includes('traslado')) return 'badge--info';
  if (tipoLower.includes('retiro') || tipoLower.includes('salida')) return 'badge--danger';
  if (tipoLower.includes('entrada')) return 'badge--success';
  if (tipoLower.includes('ajuste')) return 'badge--warning';
  return 'badge--default';
}

export function obtenerEstadoClass(estado: string): string {
  const estadoLower = estado?.toLowerCase() || '';
  if (estadoLower === 'completado') return 'badge--success';
  if (estadoLower === 'pendiente') return 'badge--warning';
  if (estadoLower === 'cancelado') return 'badge--danger';
  return 'badge--default';
}

// src/app/features/gestion-inventario/funciones/gestion-inventario-page-formato.funcion.ts
// Fecha y ubicación de un movimiento — extraído de GestionInventarioPageComponent.
// Funciones PURAS: sin `this`, sin DI, sin estado del componente.

import { InventarioGestionMovimientoDto, InventarioGestionSiloDto } from '../services/gestion-inventario.service';

export function formatFechaMovimiento(iso: string): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return isNaN(d.getTime())
    ? iso
    : d.toLocaleDateString('es', { dateStyle: 'short' }) + ' ' + d.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' });
}

/** Fecha de ingreso en la grilla de stock (solo fecha, sin hora). */
export function formatFechaIngresoStock(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return isNaN(d.getTime()) ? String(iso) : d.toLocaleDateString('es', { dateStyle: 'long' });
}

/** Convierte fecha del API a yyyy-MM-dd para input type="date". */
export function fechaIngresoStockToYmd(iso: string | null | undefined): string {
  if (!iso) return '';
  const head = String(iso).trim().match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (head) return `${head[1]}-${head[2]}-${head[3]}`;
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

/**
 * Ubicación del movimiento (granja de registro + núcleo/galpón, o el silo si la empresa ubica
 * por silo). El silo se decide por el DATO de la fila, no por el flag: así el histórico de una
 * empresa que migró sigue mostrando bien las filas viejas y las nuevas.
 */
export function ubicacionRegistroMovimiento(m: InventarioGestionMovimientoDto): string {
  const g = m.granjaNombre ?? String(m.farmId);
  if (m.siloId != null) return `${g} · Silo ${m.siloNombre ?? m.siloId}`;
  const n = m.nucleoNombre ?? m.nucleoId ?? '';
  const gp = m.galponNombre ?? m.galponId ?? '';
  if (!n && !gp) return g;
  return `${g} · Núc. ${n || '—'} · Galp. ${gp || '—'}`;
}

/** Origen/destino según tipo: contraparte del traslado o procedencia. */
export function otroExtremoMovimiento(m: InventarioGestionMovimientoDto): string {
  if (m.fromFarmId == null && !m.fromGranjaNombre) return '—';
  const g = m.fromGranjaNombre ?? (m.fromFarmId != null ? String(m.fromFarmId) : '');
  if (m.fromSiloId != null) return `${g} · Silo ${m.fromSiloNombre ?? m.fromSiloId}`;
  const n = m.fromNucleoNombre ?? m.fromNucleoId ?? '';
  const gp = m.fromGalponNombre ?? m.fromGalponId ?? '';
  if (!n && !gp) return g;
  return `${g} · Núc. ${n || '—'} · Galp. ${gp || '—'}`;
}

export function siloOptionLabel(s: InventarioGestionSiloDto): string {
  const erp = (s.codigoErpUbicacion ?? '').trim();
  const sufijo = erp ? ` · ${erp}` : '';
  return `${s.nombre}${sufijo}`;
}

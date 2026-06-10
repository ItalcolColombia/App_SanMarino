/**
 * Mapeo del formulario del modal Panamá → DTO del backend. Función pura (sin estado de Angular).
 * Solo incluye líneas con cantidad asignada (H+M > 0).
 */
import {
  CreateVentaPanamaDespachoDto,
  VentaPanamaLineaDto,
  VentaPanamaLineaUI
} from '../models/venta-panama.model';
import { ymdToIsoUtcNoon } from './formato.funcion';

/** Valor crudo del formulario de despacho del modal Panamá. */
export interface VentaPanamaFormValue {
  fechaMovimiento: string;
  motivoMovimiento: string | null;
  observaciones: string | null;
  numeroDespacho: string | null;
  edadAves: number | string | null;
  totalPollosGalpon: number | string | null;
  raza: string | null;
  placa: string | null;
  horaSalida: string | null;
  guiaAgrocalidad: string | null;
  sellos: string | null;
  ayuno: string | null;
  conductor: string | null;
  pesoBruto: number | string | null;
  pesoTara: number | string | null;
}

/** Lote de la granja (para resolver granja/núcleo/galpón de cada línea). */
export interface LoteGranjaRef {
  loteAveEngordeId: number;
  granjaId?: number | null;
  nucleoId?: string | null;
  galponId?: string | null;
}

export interface BuildVentaPanamaCtx {
  granjaId: number | null;
  usuarioMovimientoId: number;
  lineas: VentaPanamaLineaUI[];
  lotesGranja: LoteGranjaRef[];
}

function numOrNull(value: number | string | null | undefined): number | null {
  return value != null && value !== '' ? Number(value) : null;
}

/** DTO de despacho Panamá; `null` si no hay líneas con cantidad o si una línea referencia un lote inexistente. */
export function buildVentaPanamaDespachoDto(
  v: VentaPanamaFormValue,
  ctx: BuildVentaPanamaCtx
): CreateVentaPanamaDespachoDto | null {
  const conQty = ctx.lineas.filter((l) => l.h + l.m > 0);
  if (conQty.length === 0) return null;

  const lineas: VentaPanamaLineaDto[] = [];
  for (const linea of conQty) {
    const lote = ctx.lotesGranja.find((l) => l.loteAveEngordeId === linea.loteId);
    if (!lote) return null;
    const nid = lote.nucleoId != null && String(lote.nucleoId).trim() !== '' ? String(lote.nucleoId).trim() : null;
    const gpid = lote.galponId != null && String(lote.galponId).trim() !== '' ? String(lote.galponId).trim() : null;
    lineas.push({
      loteAveEngordeOrigenId: linea.loteId,
      granjaOrigenId: lote.granjaId ?? ctx.granjaId,
      nucleoOrigenId: nid,
      galponOrigenId: gpid,
      cantidadHembras: linea.h,
      cantidadMachos: linea.m
    });
  }

  return {
    fechaMovimiento: ymdToIsoUtcNoon(v.fechaMovimiento) ?? new Date(v.fechaMovimiento).toISOString(),
    tipoMovimiento: 'Venta',
    granjaOrigenId: ctx.granjaId,
    usuarioMovimientoId: ctx.usuarioMovimientoId,
    motivoMovimiento: v.motivoMovimiento || null,
    observaciones: v.observaciones || null,
    numeroDespacho: v.numeroDespacho || null,
    edadAves: numOrNull(v.edadAves),
    totalPollosGalpon: numOrNull(v.totalPollosGalpon),
    raza: v.raza || null,
    placa: v.placa || null,
    horaSalida: v.horaSalida ? `${v.horaSalida}:00` : null,
    guiaAgrocalidad: v.guiaAgrocalidad || null,
    sellos: v.sellos || null,
    ayuno: v.ayuno || null,
    conductor: v.conductor || null,
    pesoBruto: numOrNull(v.pesoBruto),
    pesoTara: numOrNull(v.pesoTara),
    lineas
  };
}

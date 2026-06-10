/**
 * Mapeo del valor del formulario del modal a los DTOs del backend.
 *
 * Tres constructores puros (crear / actualizar / venta por granja). Reciben el `getRawValue()` del
 * formulario y el contexto necesario (origen, usuario, líneas) y devuelven el DTO listo para enviar.
 * Sin estado de Angular → fáciles de testear y reutilizables por los modales de cada país.
 */
import {
  CreateMovimientoPolloEngordeDto,
  UpdateMovimientoPolloEngordeDto,
  CreateVentaGranjaDespachoDto,
  VentaGranjaDespachoLineaDto
} from '../services/movimiento-pollo-engorde.service';
import { LoteAveEngordeDto } from '../../lote-engorde/services/lote-engorde.service';
import { VentaLineaGranja } from '../models/venta-granja.model';
import { ymdToIsoUtcNoon } from './formato.funcion';

/** Valor crudo del formulario del modal (`form.getRawValue()`). */
export interface MovimientoModalFormValue {
  fechaMovimiento: string;
  tipoMovimiento: string;
  loteDestinoValue: string | null;
  cantidadHembras: number | string;
  cantidadMachos: number | string;
  cantidadMixtas: number | string;
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

export interface BuildCreateDtoCtx {
  loteOrigenValue: string;
  /** Venta: las aves salen a comprador externo; no se aplica lote destino interno. */
  isTipoVenta: boolean;
  usuarioMovimientoId: number;
}

export interface BuildVentaGranjaCtx {
  ventaLineasGranja: VentaLineaGranja[];
  lotesVentaGranja: LoteAveEngordeDto[];
  permitirSobrante: boolean;
  usuarioMovimientoId: number;
}

/** Parsea un value `ae-123` / `rae-456` a `{ tipo, id }`; `null` si no es válido. */
function parseLoteValue(value: string): { tipo: 'ae' | 'rae'; id: number } | null {
  if (!value) return null;
  if (value.startsWith('ae-')) {
    const id = parseInt(value.replace('ae-', ''), 10);
    return isNaN(id) ? null : { tipo: 'ae', id };
  }
  if (value.startsWith('rae-')) {
    const id = parseInt(value.replace('rae-', ''), 10);
    return isNaN(id) ? null : { tipo: 'rae', id };
  }
  return null;
}

/** Número o `null` desde un campo de formulario que puede venir vacío. */
function numOrNull(value: number | string | null | undefined): number | null {
  return value != null && value !== '' ? Number(value) : null;
}

/** DTO de creación de un movimiento individual; `null` si el origen no es válido. */
export function buildCreateDto(
  v: MovimientoModalFormValue,
  ctx: BuildCreateDtoCtx
): CreateMovimientoPolloEngordeDto | null {
  const origen = parseLoteValue(ctx.loteOrigenValue);
  if (!origen) return null;

  const dest = ctx.isTipoVenta ? null : v.loteDestinoValue ? parseLoteValue(v.loteDestinoValue) : null;

  return {
    fechaMovimiento: ymdToIsoUtcNoon(v.fechaMovimiento) ?? new Date(v.fechaMovimiento).toISOString(),
    tipoMovimiento: v.tipoMovimiento || 'Venta',
    loteAveEngordeOrigenId: origen.tipo === 'ae' ? origen.id : null,
    loteReproductoraAveEngordeOrigenId: origen.tipo === 'rae' ? origen.id : null,
    loteAveEngordeDestinoId: dest?.tipo === 'ae' ? dest.id : null,
    loteReproductoraAveEngordeDestinoId: dest?.tipo === 'rae' ? dest.id : null,
    cantidadHembras: Number(v.cantidadHembras) || 0,
    cantidadMachos: Number(v.cantidadMachos) || 0,
    cantidadMixtas: Number(v.cantidadMixtas) || 0,
    motivoMovimiento: v.motivoMovimiento || null,
    observaciones: v.observaciones || null,
    usuarioMovimientoId: ctx.usuarioMovimientoId,
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
    pesoTara: numOrNull(v.pesoTara)
  };
}

/** DTO de actualización (solo campos editables). */
export function buildUpdateDto(v: MovimientoModalFormValue): UpdateMovimientoPolloEngordeDto {
  return {
    fechaMovimiento: v.fechaMovimiento
      ? (ymdToIsoUtcNoon(v.fechaMovimiento) ?? new Date(v.fechaMovimiento).toISOString())
      : undefined,
    tipoMovimiento: v.tipoMovimiento || undefined,
    cantidadHembras: Number(v.cantidadHembras) ?? undefined,
    cantidadMachos: Number(v.cantidadMachos) ?? undefined,
    cantidadMixtas: Number(v.cantidadMixtas) ?? undefined,
    motivoMovimiento: v.motivoMovimiento || undefined,
    observaciones: v.observaciones || undefined,
    numeroDespacho: v.numeroDespacho || undefined,
    edadAves: numOrNull(v.edadAves) ?? undefined,
    totalPollosGalpon: numOrNull(v.totalPollosGalpon) ?? undefined,
    raza: v.raza || undefined,
    placa: v.placa || undefined,
    horaSalida: v.horaSalida ? `${v.horaSalida}:00` : undefined,
    guiaAgrocalidad: v.guiaAgrocalidad || undefined,
    sellos: v.sellos || undefined,
    ayuno: v.ayuno || undefined,
    conductor: v.conductor || undefined,
    pesoBruto: numOrNull(v.pesoBruto) ?? undefined,
    pesoTara: numOrNull(v.pesoTara) ?? undefined
  };
}

/**
 * DTO de venta por granja (despacho multi-lote): una línea por lote con cantidad > 0.
 * `null` si no hay líneas con cantidad o si alguna línea referencia un lote inexistente.
 */
export function buildVentaGranjaDespachoDto(
  v: MovimientoModalFormValue,
  ctx: BuildVentaGranjaCtx
): CreateVentaGranjaDespachoDto | null {
  const conQty = ctx.ventaLineasGranja.filter((l) => l.h + l.m + l.x > 0);
  if (conQty.length === 0) return null;

  const granjaId = ctx.lotesVentaGranja[0]?.granjaId ?? null;
  const lineas: VentaGranjaDespachoLineaDto[] = [];
  for (const linea of conQty) {
    const lote = ctx.lotesVentaGranja.find((l) => l.loteAveEngordeId === linea.loteId);
    if (!lote) return null;
    const nid = lote.nucleoId != null && String(lote.nucleoId).trim() !== '' ? String(lote.nucleoId).trim() : null;
    const gpid = lote.galponId != null && String(lote.galponId).trim() !== '' ? String(lote.galponId).trim() : null;
    lineas.push({
      loteAveEngordeOrigenId: linea.loteId,
      granjaOrigenId: lote.granjaId ?? null,
      nucleoOrigenId: nid,
      galponOrigenId: gpid,
      cantidadHembras: linea.h,
      cantidadMachos: linea.m,
      cantidadMixtas: linea.x
    });
  }

  return {
    fechaMovimiento: ymdToIsoUtcNoon(v.fechaMovimiento) ?? new Date(v.fechaMovimiento).toISOString(),
    tipoMovimiento: 'Venta',
    granjaOrigenId: granjaId,
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
    permitirSobrante: ctx.permitirSobrante,
    lineas
  };
}

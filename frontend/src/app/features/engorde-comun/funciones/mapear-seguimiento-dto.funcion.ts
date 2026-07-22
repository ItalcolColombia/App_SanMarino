/**
 * Mapeo puro del valor del formulario del modal de seguimiento engorde a los DTOs del backend,
 * más helpers de normalización (JSON, ids, ubicación, catálogo Ecuador).
 *
 * Sin `this`, sin DI, sin estado de Angular. Reciben datos por parámetro y devuelven un resultado.
 * NOTA: `mapearPanamaMixtoAHM` y `aplicarCerosSinAvesDisponibles` MUTAN el objeto `raw` recibido
 * (mismo comportamiento que el `Object.assign(raw, ...)` original de `onSave`).
 */
import { CatalogItemDto } from '../../catalogo-alimentos/services/catalogo-alimentos.service';
import { ItemInventarioDto } from '../../gestion-inventario/services/gestion-inventario.service';
import { ItemSeguimientoDto } from '../../lote-levante/services/seguimiento-lote-levante.service';
import { LoteDto } from '../../lote/services/lote.service';
import { AvesDisponiblesDto } from '../../lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service';
import { InventarioUbicacion } from '../models/inventario-ubicacion.model';
import { toNumOrNull, toKg } from './inventario-calculos.funcion';

/** itemsAdicionales u otros JSONB a veces llegan como string desde la API. */
export function normalizeJsonField(raw: any): any {
  if (raw == null) return null;
  if (typeof raw === 'string') {
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }
  return raw;
}

/** Ecuador envía itemInventarioEcuadorId; catálogo legacy usa catalogItemId. */
export function resolveItemCatalogId(item: any): number | null {
  if (!item || typeof item !== 'object') return null;
  const v =
    item.catalogItemId ??
    item.itemInventarioEcuadorId ??
    item.catalog_item_id ??
    item.item_inventario_ecuador_id;
  if (v === null || v === undefined || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

/** Núcleo/galpón del lote (strings normalizados o null) para consultar el stock por ubicación. */
export function getInventarioUbicacionFromLote(lote: LoteDto | undefined | null): InventarioUbicacion {
  if (!lote) return { nucleoId: null, galponId: null };
  const n = lote.nucleoId;
  const g = lote.galponId;
  const nucleoId = n != null && String(n).trim() !== '' ? String(n).trim() : null;
  const galponId = g != null && String(g).trim() !== '' ? String(g).trim() : null;
  return { nucleoId, galponId };
}

/** Adapta un ítem de item_inventario_ecuador al shape CatalogItemDto usado por la UI. */
export function itemEcuadorToCatalogItem(i: ItemInventarioDto): CatalogItemDto {
  return {
    id: i.id,
    codigo: i.codigo,
    nombre: i.nombre,
    metadata: { type_item: i.tipoItem, concepto: i.concepto },
    activo: i.activo
  } as CatalogItemDto;
}

/**
 * Construye ItemSeguimientoDto[] desde los valores crudos de un FormArray.
 * `forzarAlimento`: engorde pestaña hembras siempre usa tipoItem 'alimento'.
 */
export function construirItemsSeguimiento(
  valores: any[],
  opts: { forzarAlimento: boolean; isEcuadorOrPanama: boolean }
): ItemSeguimientoDto[] {
  const out: ItemSeguimientoDto[] = [];
  valores.forEach(itemValue => {
    const tipo = opts.forzarAlimento ? 'alimento' : itemValue.tipoItem;
    if (tipo && itemValue.catalogItemId && itemValue.cantidad > 0) {
      const catalogItemId = Number(itemValue.catalogItemId);
      // El backend (MetadataEngordeCalculos.ToKg) solo entiende kg/g. `qq` (quintal, Panamá) se
      // normaliza a kg AQUÍ antes de enviar: el consumo se guarda siempre en kg y no se toca el
      // path de descuento del backend. kg/g viajan tal cual (el backend hace g→kg).
      const unidadRaw = String(itemValue.unidad || 'kg').trim().toLowerCase();
      const esQuintal = unidadRaw === 'qq' || unidadRaw === 'quintal' || unidadRaw === 'quintales';
      const cantidad = esQuintal ? toKg(Number(itemValue.cantidad), unidadRaw) : Number(itemValue.cantidad);
      const unidad = esQuintal ? 'kg' : (itemValue.unidad || 'kg');
      out.push({
        tipoItem: tipo,
        catalogItemId,
        ...(opts.isEcuadorOrPanama ? { itemInventarioEcuadorId: catalogItemId } : {}),
        cantidad,
        unidad
      });
    }
  });
  return out;
}

/** itemsAdicionales JSONB: solo ítems que NO son alimento; null si no hay ninguno. */
export function construirItemsAdicionales(
  itemsHembras: ItemSeguimientoDto[],
  itemsMachos: ItemSeguimientoDto[]
): { itemsHembras?: ItemSeguimientoDto[]; itemsMachos?: ItemSeguimientoDto[] } | null {
  const otrosItemsHembras = itemsHembras.filter(item => item.tipoItem !== 'alimento');
  const otrosItemsMachos = itemsMachos.filter(item => item.tipoItem !== 'alimento');
  return (otrosItemsHembras.length > 0 || otrosItemsMachos.length > 0) ? {
    ...(otrosItemsHembras.length > 0 ? { itemsHembras: otrosItemsHembras } : {}),
    ...(otrosItemsMachos.length > 0 ? { itemsMachos: otrosItemsMachos } : {})
  } : null;
}

/** String descriptivo "H: <nombre> / M: <nombre>" a partir de los alimentos; `fallback` si no hay. */
export function construirTipoAlimentoStr(
  alimentosHembras: ItemSeguimientoDto[],
  alimentosMachos: ItemSeguimientoDto[],
  alimentosById: Map<number, CatalogItemDto>,
  fallback: string
): string {
  const nombresAlimentos: string[] = [];

  alimentosHembras.forEach(item => {
    const alimento = alimentosById.get(item.catalogItemId);
    if (alimento?.nombre) {
      nombresAlimentos.push(`H: ${alimento.nombre}`);
    }
  });

  alimentosMachos.forEach(item => {
    const alimento = alimentosById.get(item.catalogItemId);
    if (alimento?.nombre) {
      nombresAlimentos.push(`M: ${alimento.nombre}`);
    }
  });

  return nombresAlimentos.length > 0 ? nombresAlimentos.join(' / ') : fallback || '';
}

/**
 * Nuevo registro: si no quedan aves por sexo (o mixtas en Panamá), pone en cero mortalidad/selección/
 * error de sexaje y en null los pesos. MUTA el objeto `raw`.
 */
export function aplicarCerosSinAvesDisponibles(
  raw: any,
  isPanama: boolean,
  avesDisponibles: AvesDisponiblesDto
): void {
  if (isPanama) {
    // Panamá: si no hay mixtas, zeroise todos los campos de mixto
    if ((avesDisponibles.mixtasDisponibles ?? 0) <= 0) {
      Object.assign(raw, {
        mortalidadMixtas: 0, selMixtas: 0, errorSexajeMixtas: 0,
        pesoPromMixto: null, uniformidadMixta: null, cvMixto: null
      });
    }
  } else {
    const h0 = (avesDisponibles.hembrasDisponibles ?? 0) <= 0;
    const m0 = (avesDisponibles.machosDisponibles ?? 0) <= 0;
    if (h0) {
      Object.assign(raw, { mortalidadHembras: 0, selH: 0, errorSexajeHembras: 0, pesoPromH: null, uniformidadH: null, cvH: null });
    }
    if (m0) {
      Object.assign(raw, { mortalidadMachos: 0, selM: 0, errorSexajeMachos: 0, pesoPromM: null, uniformidadM: null, cvM: null });
    }
  }
}

/** Panamá: mapea campos Mixto → H (con M=0) en el DTO antes de enviar. MUTA el objeto `raw`. */
export function mapearPanamaMixtoAHM(raw: any): void {
  Object.assign(raw, {
    mortalidadHembras: Number(raw.mortalidadMixtas) || 0,
    mortalidadMachos: 0,
    selH: Number(raw.selMixtas) || 0,
    selM: 0,
    errorSexajeHembras: Number(raw.errorSexajeMixtas) || 0,
    errorSexajeMachos: 0,
    pesoPromH: raw.pesoPromMixto ?? null,
    pesoPromM: null,
    uniformidadH: raw.uniformidadMixta ?? null,
    uniformidadM: null,
    cvH: raw.cvMixto ?? null,
    cvM: null,
  });
}

export interface BuildBaseSeguimientoDtoCtx {
  /** Fecha de registro ya convertida a ISO (mediodía local). */
  fechaRegistroIso: string;
  /** `form.getRawValue()` ya ajustado (ceros/Panamá aplicados). */
  raw: any;
  lotePosturaLevanteId: number | null;
  itemsHembras: ItemSeguimientoDto[];
  itemsMachos: ItemSeguimientoDto[];
  itemsAdicionales: { itemsHembras?: ItemSeguimientoDto[]; itemsMachos?: ItemSeguimientoDto[] } | null;
  tipoAlimentoStr: string;
  isPanama: boolean;
  createdByUserId: string | null;
}

/**
 * Construye el DTO base (común a crear/actualizar) del seguimiento diario.
 * El backend acepta consumo con unidad y hace la conversión; los qq solo viajan en Panamá.
 */
export function buildBaseSeguimientoDto(ctx: BuildBaseSeguimientoDtoCtx): any {
  const { raw } = ctx;
  return {
    fechaRegistro: ctx.fechaRegistroIso,
    loteId: raw.loteId,
    lotePosturaLevanteId: ctx.lotePosturaLevanteId,
    mortalidadHembras: Number(raw.mortalidadHembras) || 0,
    mortalidadMachos: Number(raw.mortalidadMachos) || 0,
    selH: Number(raw.selH) || 0,
    selM: Number(raw.selM) || 0,
    errorSexajeHembras: Number(raw.errorSexajeHembras) || 0,
    errorSexajeMachos: Number(raw.errorSexajeMachos) || 0,
    tipoAlimento: ctx.tipoAlimentoStr || '',
    // Arrays de ítems (el backend separa alimentos de otros ítems)
    itemsHembras: ctx.itemsHembras.length > 0 ? ctx.itemsHembras : null,
    itemsMachos: ctx.itemsMachos.length > 0 ? ctx.itemsMachos : null,
    // Items adicionales JSONB (solo ítems que NO son alimentos)
    itemsAdicionales: ctx.itemsAdicionales,
    pesoPromH: toNumOrNull(raw.pesoPromH),
    pesoPromM: toNumOrNull(raw.pesoPromM),
    uniformidadH: toNumOrNull(raw.uniformidadH),
    uniformidadM: toNumOrNull(raw.uniformidadM),
    cvH: toNumOrNull(raw.cvH),
    cvM: toNumOrNull(raw.cvM),
    observaciones: raw.observaciones,
    kcalAlH: null,
    protAlH: null,
    kcalAveH: null,
    protAveH: null,
    ciclo: raw.ciclo,
    // Campos de agua (siempre enviar; el backend los persiste en seguimiento_diario_*)
    consumoAguaDiario: toNumOrNull(raw.consumoAguaDiario),
    consumoAguaPh: toNumOrNull(raw.consumoAguaPh),
    consumoAguaOrp: toNumOrNull(raw.consumoAguaOrp),
    consumoAguaTemperatura: toNumOrNull(raw.consumoAguaTemperatura),
    // Campos específicos Panamá: quintales de alimento por categoría.
    // Solo viajan en Panamá; en otros países las claves no se incluyen (contrato intacto).
    ...(ctx.isPanama
      ? {
          qqMixtas: toNumOrNull(raw.qqMixtas),
          qqHembras: toNumOrNull(raw.qqHembras),
          qqMachos: toNumOrNull(raw.qqMachos)
        }
      : {}),
    // Usuario en sesión y tipo para el servicio unificado seguimiento_diario
    createdByUserId: ctx.createdByUserId,
    tipoSeguimiento: 'levante',
  };
}

/**
 * Helpers puros de inventario/alimento para el módulo lote-levante.
 *
 * Extraídos de `modal-create-edit.component.ts` sin cambiar comportamiento: misma aritmética de
 * conversión de unidades (idénticos `Math.round` y orden) y mismo contrato de ids por país para
 * persistir un ítem. Funciones puras: sin `this`, sin DI, sin estado de Angular.
 */
import { CatalogItemDto } from '../../catalogo-alimentos/services/catalogo-alimentos.service';
import { ItemInventarioDto } from '../../gestion-inventario/services/gestion-inventario.service';
import { LoteDto } from '../../lote/services/lote.service';
import { InventarioUbicacion, ItemPersistContext, ItemPersistFields } from '../models/lote-levante-inventario.model';

/** Convierte cantidad a kg para validar contra inventario (gramos → /1000). */
export function toKg(cantidad: number, unidad: string | null | undefined): number {
  const u = String(unidad || 'kg').trim().toLowerCase();
  if (u === 'g' || u === 'gramo' || u === 'gramos') return cantidad / 1000;
  return cantidad;
}

/**
 * Convierte una cantidad de inventario a GRAMOS para mostrar en la interfaz.
 * Misma aritmética que el componente original: kg → *1000, g → tal cual, otra unidad → *1000
 * (asumiendo kg). `onUnknownUnit` permite conservar el `console.warn` de las rutas legacy sin
 * introducir un efecto secundario dentro de esta función pura.
 */
export function unidadToGramos(
  cantidadOriginal: number,
  unidad: string,
  onUnknownUnit?: (unidad: string) => void
): number {
  const unidadLower = unidad.toLowerCase();
  if (unidadLower === 'kg' || unidadLower === 'kilogramos' || unidadLower === 'kilogramo') {
    return Math.round(cantidadOriginal * 1000);
  } else if (unidadLower === 'g' || unidadLower === 'gramos' || unidadLower === 'gramo') {
    return Math.round(cantidadOriginal);
  } else {
    onUnknownUnit?.(unidad);
    return Math.round(cantidadOriginal * 1000);
  }
}

/** Adapta un ItemInventarioDto (EC/PA/Colombia) al shape CatalogItemDto usado por el dropdown. */
export function itemEcuadorToCatalogItem(i: ItemInventarioDto): CatalogItemDto {
  return {
    id: i.id,
    codigo: i.codigo,
    nombre: i.nombre,
    metadata: { type_item: i.tipoItem, concepto: i.concepto },
    activo: i.activo
  } as CatalogItemDto;
}

/** Núcleo/galpón del lote (string no vacío → valor, si no → null). */
export function getInventarioUbicacionFromLote(lote: LoteDto | undefined | null): InventarioUbicacion {
  if (!lote) return { nucleoId: null, galponId: null };
  const n = lote.nucleoId;
  const g = lote.galponId;
  const nucleoId = n != null && String(n).trim() !== '' ? String(n).trim() : null;
  const galponId = g != null && String(g).trim() !== '' ? String(g).trim() : null;
  return { nucleoId, galponId };
}

/**
 * Campos de id + nombre para persistir un ítem según el contrato de inventario del país.
 * `itemId` es el id seleccionado en el dropdown (EC/PA/Colombia = item_inventario_ecuador.id;
 * otros países = catalogo_items.id).
 * Colombia: si el ítem tiene espejo en catalogo_items (mismo código) se envía ese id (el backend
 * descuenta por código, camino-1); si es un ítem nuevo sin espejo (p.ej. "moises") se envía el id
 * de item_inventario_ecuador (camino-2 pass-through).
 */
export function buildItemPersistFields(itemId: number, ctx: ItemPersistContext): ItemPersistFields {
  const nombre = ctx.alimentosById.get(itemId)?.nombre ?? undefined;
  if (ctx.isEcuadorOrPanama) {
    return { catalogItemId: itemId, itemInventarioEcuadorId: itemId, nombre };
  }
  if (ctx.isColombia) {
    const item = ctx.itemsEcuadorPanama.find(i => i.id === itemId);
    const codigo = (item?.codigo ?? '').trim().toLowerCase();
    const catalogoItemsId = codigo ? ctx.catalogItemIdPorCodigo.get(codigo) : undefined;
    if (catalogoItemsId) {
      return { catalogItemId: catalogoItemsId, nombre };
    }
    return { catalogItemId: 0, itemInventarioEcuadorId: itemId, nombre };
  }
  return { catalogItemId: itemId, nombre };
}

/**
 * Helpers puros de parseo de metadata/JSONB para el módulo lote-levante.
 *
 * Extraídos de `modal-create-edit.component.ts` sin cambiar comportamiento. Se usan para leer los
 * campos JSONB que llegan desde la API (metadata, itemsAdicionales) que a veces vienen como string,
 * con envelope de JsonDocument, o con ids en distintas claves (EC vs. catálogo legacy). Funciones
 * puras: sin `this`, sin DI, sin estado de Angular.
 */
import { SeguimientoLoteLevanteDto } from '../services/seguimiento-lote-levante.service';

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

/**
 * Algunas respuestas serializan JsonDocument/JsonElement como objeto con raíz anidada.
 * También acepta { itemsHembras, itemsMachos } en cualquier nivel visible.
 */
export function unwrapJsonApiEnvelope(raw: any): any {
  if (raw == null || typeof raw !== 'object') return raw;
  const o = raw as Record<string, unknown>;
  if (o['rootElement'] != null && typeof o['rootElement'] === 'object') {
    return normalizeJsonField(o['rootElement']) ?? raw;
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

/** Para metadata legacy: escogemos un texto que probablemente mapea a un ítem del catálogo. */
export function pickLegacyFoodText(metadata: any, editing: SeguimientoLoteLevanteDto): string | null {
  const t = (metadata?.tipoAlimentoCodigo ?? metadata?.tipo_alimento_codigo ?? editing?.tipoAlimento ?? '').toString().trim();
  return t ? t : null;
}

/** Normaliza texto para búsqueda por clave (trim, espacios colapsados, minúsculas). */
export function normalizeKeyText(s: string): string {
  return (s ?? '')
    .toString()
    .trim()
    .replace(/\s+/g, ' ')
    .toLowerCase();
}

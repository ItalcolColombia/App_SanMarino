// src/app/features/lote/funciones/agrupar-huevo-items.funcion.ts
import { LoteHuevoItemDto } from '../services/lote-huevo-items.service';
import { GrupoHuevoItems, SIN_CATEGORIA_HUEVO } from '../models/huevo-items.model';

/**
 * Agrupa los ítems de huevo por `tipoHuevo` **conservando el orden que ya trae el backend**
 * (Primera → Pnc → resto, y por nombre dentro de cada grupo).
 *
 * <p>
 * **No reordena a propósito.** El orden es una sola regla y vive en `HuevoItemsCalculos.PesoTipoHuevo`
 * (backend), que ya está testeada. Duplicarla acá abriría la puerta a que las dos listas se
 * separen: el mismo catálogo saldría en un orden en el modal y en otro en el alta del lote.
 * </p>
 *
 * Función pura: sin `this`, sin DI, sin estado. La usan el modal 🥚 de la lista de lotes y la
 * sección de tipos de huevo del formulario de alta/edición.
 */
export function agruparHuevoItemsPorTipo(items: readonly LoteHuevoItemDto[]): GrupoHuevoItems[] {
  const grupos: GrupoHuevoItems[] = [];
  for (const item of items ?? []) {
    const clave = item.tipoHuevo?.trim() || SIN_CATEGORIA_HUEVO;
    const grupo = grupos.find(g => g.tipoHuevo === clave);
    if (grupo) grupo.items.push(item);
    else grupos.push({ tipoHuevo: clave, items: [item] });
  }
  return grupos;
}

/**
 * Los ítems que el lote ya declaró, listos para inicializar la selección del selector.
 * `activo` viene marcado por `GET /LoteHuevoItem/{loteId}/disponibles`; en el alta —donde el lote
 * todavía no existe— llega siempre en `false` y el set arranca vacío, que es lo correcto.
 */
export function seleccionInicialHuevoItems(items: readonly LoteHuevoItemDto[]): Set<number> {
  return new Set((items ?? []).filter(i => i.activo).map(i => i.catalogItemId));
}

/**
 * Ubicación (núcleo/galpón) por la que se consulta el stock de inventario de un lote.
 * En Ecuador/Panamá el stock se resuelve por esta ubicación y no agregado a toda la granja.
 */
export interface InventarioUbicacion {
  nucleoId: string | null;
  galponId: string | null;
}

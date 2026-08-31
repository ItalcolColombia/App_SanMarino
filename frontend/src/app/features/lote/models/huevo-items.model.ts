// src/app/features/lote/models/huevo-items.model.ts
import { LoteHuevoItemDto } from '../services/lote-huevo-items.service';

/**
 * Un grupo del selector de tipos de huevo (`Primera` / `Pnc` / `Sin categoría`) con sus ítems.
 *
 * Vive en `models/` y no dentro de un componente porque lo usan DOS pantallas: el modal 🥚 de la
 * lista de lotes y la sección de tipos de huevo del formulario de alta/edición.
 */
export interface GrupoHuevoItems {
  tipoHuevo: string;
  items: LoteHuevoItemDto[];
}

/** Etiqueta del grupo cuando el ítem del catálogo no trae `metadata.tipoHuevo`. */
export const SIN_CATEGORIA_HUEVO = 'Sin categoría';

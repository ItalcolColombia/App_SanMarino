/**
 * Tipos compartidos del inventario del módulo lote-levante.
 *
 * Se extraen del componente `modal-create-edit` para poder reutilizarlos desde `funciones/`
 * (p. ej. `lote-levante-inventario.funcion.ts`) sin generar imports circulares.
 */
import { CatalogItemDto } from '../../catalogo-alimentos/services/catalogo-alimentos.service';
import { ItemInventarioDto } from '../../gestion-inventario/services/gestion-inventario.service';

/** Núcleo/galpón del lote seleccionado: el stock EC/PA se consulta por esta ubicación. */
export interface InventarioUbicacion {
  nucleoId: string | null;
  galponId: string | null;
}

/** Campos de id + nombre para persistir un ítem según el contrato de inventario del país. */
export interface ItemPersistFields {
  catalogItemId: number;
  itemInventarioEcuadorId?: number;
  nombre?: string;
}

/**
 * Contexto (estado del componente) que necesita `buildItemPersistFields` para resolver
 * el contrato de ids por país. Se pasa por parámetro para mantener la función pura.
 */
export interface ItemPersistContext {
  isEcuadorOrPanama: boolean;
  isColombia: boolean;
  alimentosById: Map<number, CatalogItemDto>;
  itemsEcuadorPanama: ItemInventarioDto[];
  catalogItemIdPorCodigo: Map<string, number>;
}

// src/app/features/lote-produccion/models/seguimiento-metadata.model.ts

import { CatalogItemDto } from '../../catalogo-alimentos/services/catalogo-alimentos.service';

/** `CatalogItemDto` con el tipo/unidad que trae el ítem Ecuador/Panamá, para no perderlos al mapear. */
export interface CatalogItemExtended extends CatalogItemDto {
  tipoItem?: string;
  unidad?: string;
}

/** Metadata del seguimiento (puede venir como objeto o JSON string; soporta camelCase y snake_case). */
export interface MetadataSeguimientoNormalizada {
  itemsHembras: Array<{ tipoItem: string; catalogItemId: number; itemInventarioEcuadorId?: number; cantidad: number; unidad: string; siloId?: number | null }>;
  itemsMachos: Array<{ tipoItem: string; catalogItemId: number; itemInventarioEcuadorId?: number; cantidad: number; unidad: string; siloId?: number | null }>;
  consumoOriginalHembras?: number;
  unidadConsumoOriginalHembras?: string;
  consumoOriginalMachos?: number;
  unidadConsumoOriginalMachos?: string;
  tipoItemHembras?: string | null;
  tipoItemMachos?: string | null;
  tipoAlimentoHembras?: number | null;
  tipoAlimentoMachos?: number | null;
  [key: string]: unknown;
}

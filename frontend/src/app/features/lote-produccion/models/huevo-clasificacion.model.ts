// src/app/features/lote-produccion/models/huevo-clasificacion.model.ts
/**
 * Tipos de la CLASIFICACIÓN DE HUEVOS POR ÍTEMS (Santa Reyes).
 *
 * Empresas con `companies.clasificacion_huevo_por_items = true` no usan las 11 columnas fijas
 * (limpio, tratado, sucio…) sino los ítems de huevo de su catálogo (`catalogo_items` con
 * `item_type = 'huevo'`), cada uno categorizado como `Primera` o `Pnc` en su `metadata.tipoHuevo`.
 */

/** `catalogo_items.item_type` de los ítems de huevo. */
export const ITEM_TYPE_HUEVO = 'huevo';

/** Etiqueta del grupo cuando el ítem del catálogo no trae `metadata.tipoHuevo`. */
export const TIPO_HUEVO_SIN_CATEGORIA = 'Sin categoría';

/** Orden de los grupos en el select (los tipos conocidos primero, el resto alfabético). */
export const ORDEN_TIPOS_HUEVO: readonly string[] = ['Primera', 'Pnc'];

/** Opción del select de clasificación (ítem de huevo del catálogo de la empresa activa). */
export interface HuevoCatalogOption {
  /** `catalogo_items.id`. */
  id: number;
  codigo: string;
  nombre: string;
  /** `metadata.tipoHuevo` del catálogo: 'Primera' | 'Pnc' | null si el ítem no lo trae. */
  tipoHuevo: string | null;
  /** `metadata.um` del catálogo: 'UND' | 'KIL' | null. */
  um: string | null;
  /**
   * `metadata.primeraPostura` del catálogo: ítem que representa «Huevo de primera postura» de
   * alguna raza — sujeto a la vigencia de `Company.huevoPrimeraPosturaHastaSemana` (ver
   * `esVigentePrimeraPostura`). `false` para el resto (Primera común / Pnc).
   */
  primeraPostura: boolean;
  /** Texto mostrado en el `<option>`: "codigo — nombre". */
  label: string;
}

/** Grupo (`<optgroup>`) del select: un tipo de huevo con sus ítems. */
export interface HuevoCatalogGrupo {
  /** Etiqueta del grupo ('Primera' / 'Pnc' / 'Sin categoría'). */
  tipoHuevo: string;
  items: HuevoCatalogOption[];
}

// ===================== REPORTES / INDICADORES (FASE 5) =====================

/** Ítem del desglose semanal ya agrupado (una línea "codigo — nombre: cantidad"). */
export interface ClasificacionHuevoItemAgrupado {
  codigo: string;
  nombre: string;
  cantidad: number;
  /** Texto listo para la vista: "codigo — nombre" (o el que exista). */
  label: string;
}

/** Grupo Primera/Pnc dentro de una semana: subtotal + ítems que lo componen. */
export interface ClasificacionHuevoTipoAgrupado {
  /** Etiqueta del grupo ('Primera' / 'Pnc' / 'Sin categoría'). */
  tipoHuevo: string;
  subtotal: number;
  items: ClasificacionHuevoItemAgrupado[];
}

/**
 * Clasificación por ítem de UNA semana (lo que consume la fila expandible de indicadores
 * de producción cuando la empresa clasifica por ítems).
 */
export interface ClasificacionHuevoSemanaAgrupada {
  semana: number;
  total: number;
  tipos: ClasificacionHuevoTipoAgrupado[];
}

/** Forma mínima para clasificar por tipo (compatible estructuralmente con `HuevoItemSeguimiento`). */
export interface HuevoItemClasificable {
  /** Categoría del ítem: 'Primera' | 'Pnc' (según `metadata.tipoHuevo` del catálogo). */
  tipoHuevo?: string | null;
  cantidad: number;
}

/** Resumen Primera/Pnc de UN registro diario (columnas de la grilla de seguimiento). */
export interface ResumenHuevoPorTipo {
  primera: number;
  pnc: number;
  /** Cantidades con `tipoHuevo` desconocido: NO entran ni en Primera ni en Pnc (sí en `total`). */
  otros: number;
  total: number;
}

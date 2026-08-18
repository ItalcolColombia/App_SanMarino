/**
 * Separa las filas de stock de alimento en las que son **de este lote** y las que son de **otro
 * galpón**.
 *
 * Por qué existe: el modal de liquidación, cuando el galpón no tiene filas de alimento, vuelve a
 * consultar el inventario **sin filtrar por galpón** para no perder de vista el stock que vive a
 * nivel núcleo/granja (empresas con `maneja_alimento_por_galpon = false`). Esa segunda consulta trae
 * también las filas de los **galpones vecinos**, y contarlas hacía que un galpón vacío avisara «hay
 * alimento en inventario» con kilos ajenos — 15 lotes de Ecuador y Panamá lo hacían (medido 17ago26).
 *
 * La regla:
 * - fila **sin galpón** (nivel núcleo o granja) ⇒ **es del lote**;
 * - lote **sin galpón** ⇒ todo lo que devolvió la consulta es suyo;
 * - fila del **mismo** galpón ⇒ es del lote;
 * - fila de **otro** galpón ⇒ ajena: se muestra rotulada, pero no cuenta.
 *
 * La comparación es por texto recortado, igual que el backend (`COALESCE(TRIM(galpon_id), '')`).
 */

/** Entrada estructural: acepta el DTO real sin acoplarse al service. */
export interface FilaStockUbicacionLike {
  galponId?: string | null;
  quantity?: number | null;
}

export interface StockSeparadoPorUbicacion<T> {
  /** Filas que corresponden al lote (su galpón, o stock de nivel núcleo/granja). */
  propias: T[];
  /** Filas de otros galpones del núcleo. */
  ajenas: T[];
  kgPropias: number;
  kgAjenas: number;
}

export function separarStockPorUbicacion<T extends FilaStockUbicacionLike>(
  filas: readonly T[] | null | undefined,
  galponLote: string | null | undefined
): StockSeparadoPorUbicacion<T> {
  const galpon = (galponLote ?? '').trim();
  const propias: T[] = [];
  const ajenas: T[] = [];

  for (const fila of filas ?? []) {
    const galponFila = (fila?.galponId ?? '').trim();
    const esPropia = galponFila === '' || galpon === '' || galponFila === galpon;
    (esPropia ? propias : ajenas).push(fila);
  }

  return {
    propias,
    ajenas,
    kgPropias: sumarKg(propias),
    kgAjenas: sumarKg(ajenas)
  };
}

function sumarKg(filas: readonly FilaStockUbicacionLike[]): number {
  return filas.reduce((total, fila) => total + (Number(fila?.quantity) || 0), 0);
}

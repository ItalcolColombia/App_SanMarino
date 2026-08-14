// frontend/src/app/shared/utils/inventario/stock-por-silo.funcion.ts
// Fase C del plan de silos: el saldo que ve el operario en el seguimiento diario es el del SILO, no
// el de la granja. Funciones PURAS (sin DI, sin `this`) — las usan los modales de levante y de
// producción, que comparten el mismo problema.

/** Fila de stock tal como la devuelve `GET /inventario-gestion/stock` (solo lo que acá se usa). */
export interface FilaStockSilo {
  itemInventarioEcuadorId: number;
  quantity: number;
  unit?: string | null;
  siloId?: number | null;
}

/** Saldo de un ítem en una ubicación. */
export interface SaldoItem {
  quantity: number;
  unit: string;
}

/**
 * Clave del mapa de saldos: ítem + silo. El silo ausente/nulo es una clave propia (`0`), que es el
 * saldo «a nivel granja» de las empresas sin el flag — la misma convención que el índice único de la
 * BD, que usa `COALESCE(silo_id, 0)`.
 */
export function claveItemSilo(itemId: number, siloId: number | null | undefined): string {
  return `${itemId}|${siloId ?? 0}`;
}

/**
 * Agrupa las filas de stock por (ítem, silo) sumando cantidades.
 *
 * <p>Sin esto, dos silos del mismo alimento se sumaban en un solo «disponible» y el operario veía
 * 2.000 kg donde el silo del que iba a sacar tenía 300.</p>
 */
export function agruparStockPorItemSilo(filas: FilaStockSilo[] | null | undefined): Map<string, SaldoItem> {
  const mapa = new Map<string, SaldoItem>();
  for (const f of filas ?? []) {
    const id = Number(f?.itemInventarioEcuadorId);
    if (!id) continue;
    const clave = claveItemSilo(id, f.siloId);
    const previo = mapa.get(clave);
    mapa.set(clave, {
      quantity: (previo?.quantity ?? 0) + Number(f.quantity ?? 0),
      unit: previo?.unit ?? f.unit ?? 'kg'
    });
  }
  return mapa;
}

/** Ítems con saldo positivo en un silo concreto. Sin silo devuelve el conjunto vacío. */
export function itemsConStockEnSilo(
  stockPorItemSilo: Map<string, SaldoItem>,
  siloId: number | null | undefined
): Set<number> {
  const conStock = new Set<number>();
  if (!siloId) return conStock;
  const sufijo = `|${siloId}`;
  for (const [clave, saldo] of stockPorItemSilo.entries()) {
    if (!clave.endsWith(sufijo)) continue;
    if (saldo.quantity > 0) conStock.add(Number(clave.split('|')[0]));
  }
  return conStock;
}

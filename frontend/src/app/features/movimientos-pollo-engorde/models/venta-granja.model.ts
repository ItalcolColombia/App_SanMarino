/**
 * Tipos del modal de movimiento / venta por granja.
 *
 * Se extraen del componente modal para reutilizarlos desde `funciones/`
 * (`mapear-movimiento-dto`, `prorateo-peso`) y desde los futuros modales por país.
 */

export interface LoteDestinoOption {
  value: string;
  label: string;
}

export interface AvailableBirds {
  total: number;
  hembras?: number;
  machos?: number;
  mixtas?: number;
}

/** Una fila por lote en venta por granja (despacho multi-galpón). */
export interface VentaLineaGranja {
  loteId: number;
  loteNombre: string;
  galponId: string;
  galponLabel: string;
  maxH: number;
  maxM: number;
  maxX: number;
  h: number;
  m: number;
  x: number;
  /** Texto del input (solo dígitos); evita [value] numérico que pisa el cursor al escribir. */
  hStr: string;
  mStr: string;
  xStr: string;
  /** Breve aviso visual si el usuario intentó superar el máximo (se ajusta al tope). */
  flashExcesoH?: boolean;
  flashExcesoM?: boolean;
  flashExcesoX?: boolean;
  /** true si el lote está cerrado o es una corrida anterior en el mismo galpón (ver `detectar-lotes-bloqueados-venta.funcion.ts`). */
  bloqueada?: boolean;
  /**
   * ¿El permiso `movimientos_pollo_engorde.vender_lotes_cerrados` destraba ESTA línea?
   *
   * Solo la corrida anterior. Un lote **liquidado** lo rechaza el gate de liquidación congelada del
   * backend (`LiquidacionCongeladaGateCalculos.ValidarEscritura` → 400) y **ningún permiso lo abre**:
   * su bypass (`omitirGateLiquidado`) existe solo para las herramientas de reparación. Además,
   * mientras el lote está cerrado los reportes leen la copia congelada, así que una venta que
   * entrara quedaría invisible. El camino correcto es **reabrir el lote**.
   */
  bypassablePorPermiso?: boolean;
  /** Motivo del bloqueo a mostrar en la UI ("Lote cerrado" | "Corrida anterior en este galpón"). */
  motivoBloqueo?: string;
}

export interface MovimientoPolloEngordeSaveDetail {
  ventaGranjaBatchCount?: number;
}

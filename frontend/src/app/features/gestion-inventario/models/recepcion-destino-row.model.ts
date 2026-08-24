// src/app/features/gestion-inventario/models/recepcion-destino-row.model.ts

/** Fila del reparto de una recepción de tránsito entre galpones (o silos) de la granja destino. */
export interface RecepcionDestinoRow {
  nucleoId: string | null;
  galponId: string | null;
  quantity: number | null;
  /** Silo/bodega destino de esta fila (empresas con inventario por silo). */
  siloId: number | null;
}

/**
 * Espejo de `CuadrePendienteDto` (backend `Application/DTOs/Sync/SyncPushDtos.cs`).
 *
 * Una fila acá es una captura que **sí se guardó** —el día de campo existe en la base— pero que
 * entró **sin descontar inventario** porque al llegar al servidor ya no había stock de ese ítem.
 * No es un error del galponero: es el sistema avisando que su número de stock está atrasado
 * respecto de lo que pasó físicamente en la granja.
 */
export interface CuadrePendiente {
  /** Id de la fila en `sync_operaciones`. Es lo que se manda a `resolver`. */
  id: number;

  /** Uno de los tipos del contrato de sync (`seguimiento_levante_crear`, …). */
  tipo: string;

  /** Id del seguimiento ya guardado. Puede venir nulo si el servidor no lo pudo resolver. */
  entidadId: number | null;

  /** Qué ítem faltó, cuánto había y cuánto se pedía. Texto para el humano. */
  detalle: string | null;

  /** Informativo: qué dispositivo la capturó. */
  deviceId: string | null;

  /** Cuándo la recibió el servidor (ISO-8601). */
  recibidoAt: string;
}

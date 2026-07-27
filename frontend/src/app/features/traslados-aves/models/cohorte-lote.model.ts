/**
 * Cohortes de aves de un lote (Fase 3 — Santa Reyes).
 *
 * Un lote puede tener aves de VARIAS edades: las propias (cuentan desde su
 * `fecha_encaset`) y las recibidas por traslado desde otro lote, que conservan la
 * fecha de encasetamiento del lote ORIGEN. Cada grupo recibido es una "cohorte".
 *
 * Contrato backend: `GET {apiUrl}/traslados/cohortes/{loteId}` (loteId = lote base).
 * Las edades las calcula SIEMPRE el backend (no se recalculan en el front).
 */

/** Una cohorte recibida por traslado desde otro lote. */
export interface CohorteLoteDto {
  id: number;
  loteOrigenId?: number | null;
  loteOrigenNombre?: string | null;
  /** Fecha en la que las aves ingresaron al lote receptor. */
  fechaIngreso?: string | null;
  /** Fecha de encasetamiento del lote origen (base de la edad de la cohorte). */
  fechaEncasetCohorte?: string | null;
  /** Edad ACTUAL de la cohorte en días (la calcula el backend). */
  edadDias?: number | null;
  /** Edad ACTUAL de la cohorte en semanas (la calcula el backend). */
  edadSemanas?: number | null;
  cantidadHembras?: number | null;
  cantidadMachos?: number | null;
  observaciones?: string | null;
}

/** Respuesta de `GET /traslados/cohortes/{loteId}`: aves propias + cohortes recibidas. */
export interface CohortesLoteDto {
  loteId: number;
  loteNombre?: string | null;
  /** Encaset del propio lote (base de la edad de las aves propias). */
  fechaEncasetPropia?: string | null;
  edadPropiaDias?: number | null;
  edadPropiaSemanas?: number | null;
  cohortes?: CohorteLoteDto[] | null;
}

/** Fila lista para pintar en la tabla "Edades en el lote" (ya formateada, sin lógica en el template). */
export interface FilaEdadLote {
  /** Clave estable para `track` (evita re-crear nodos en cada ciclo). */
  clave: string;
  /** `propia` = aves del lote; `cohorte` = aves recibidas de otro lote. */
  tipo: 'propia' | 'cohorte';
  /** Etiqueta de la primera columna (origen de las aves). */
  origen: string;
  /** Fecha de ingreso al lote (`—` en la fila propia: son las aves originales). */
  fechaIngreso: string;
  /** Fecha de encasetamiento que define la edad de la fila. */
  fechaEncaset: string;
  /** `null` en la fila propia (el endpoint de cohortes no trae el saldo del lote). */
  hembras: number | null;
  machos: number | null;
  total: number | null;
  edadDias: number | null;
  edadSemanas: number | null;
  observaciones: string | null;
}

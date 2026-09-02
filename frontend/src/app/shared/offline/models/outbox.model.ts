/**
 * Tipos de la cola de captura offline (F3).
 *
 * Viven en `models/` para que las funciones puras de `funciones/` se tipen sin importar servicios
 * ni componentes (convención de CLAUDE.md).
 */

/**
 * Estado LOCAL de una operación. No es el estado del servidor: una operación aplicada se **borra**
 * de la cola, no queda marcada como aplicada. Lo que sobrevive es lo que todavía tiene que salir o
 * lo que el servidor rechazó y necesita una persona.
 */
export type EstadoOperacionLocal =
  /** Esperando red o su próximo intento. */
  | 'pendiente'
  /** El servidor la rechazó por una razón que no se arregla reintentando. Va a la bandeja. */
  | 'rechazada';

/** Una mutación capturada sin red. */
export interface OperacionPendiente {
  /**
   * UUID generado por el dispositivo al encolar. **No cambia entre reintentos**: es la única
   * garantía de que un reintento no aplique el efecto dos veces.
   */
  clientOpId: string;

  /** `{userId}|{companyId}|{paisId}` — permite listar lo del usuario actual sin recorrer todo. */
  particion: string;

  /** Tipo de operación del contrato con el servidor. */
  tipo: string;

  companyId: number;
  paisId: number;
  userId: string;

  /** Identificador del equipo. Informativo; el servidor nunca autoriza con esto. */
  deviceId: string;

  /** Momento de la captura, en ISO. El reloj del equipo puede estar corrido. */
  capturadoAtDispositivo: string;

  /** Método y URL originales, para diagnóstico y para reconstruir qué pantalla lo generó. */
  metodo: string;
  url: string;

  /** Cuerpo tal como se habría mandado con red. */
  payload: unknown;

  estado: EstadoOperacionLocal;

  /** Intentos de envío ya hechos. Alimenta el backoff. */
  intentos: number;

  /** Epoch ms del próximo intento permitido. `null` = puede salir ya. */
  proximoIntentoEn: number | null;

  /** Código estable devuelto por el servidor, cuando fue rechazada. */
  errorCodigo?: string | null;

  /** Texto para el humano. */
  detalle?: string | null;

  creadoEn: number;
}

/** Resultado de una operación tal como lo devuelve `POST /api/Sync/push`. */
export interface ResultadoPush {
  clientOpId: string;
  estado: 'aplicada' | 'rechazada' | 'requiere_cuadre';
  errorCodigo?: string | null;
  detalle?: string | null;
  entidadId?: number | null;
  replay?: boolean;
}

/** Qué hacer con una operación después de ver su resultado. */
export type AccionTrasPush =
  /** Confirmada por el servidor: se borra de la cola. */
  | 'borrar'
  /** Falló por algo transitorio: sigue en la cola con backoff. */
  | 'reintentar'
  /** Rechazo definitivo: queda en la bandeja para que una persona decida. */
  | 'bandeja';

/** Resumen para la UI (contador y bandeja). */
export interface EstadoOutbox {
  disponible: boolean;
  pendientes: number;
  rechazadas: number;
  masAntigua: number | null;
}

/**
 * Lo que la pantalla de un lote necesita saber de una captura sin enviar: qué día es y si quedó
 * rechazada. **A propósito NO trae el payload ni ningún número capturado** — ver el doc-comment de
 * `resumirCapturasPendientes`: un número que el servidor nunca vio, mostrado junto a los que sí,
 * termina en el Excel y en los indicadores como si fuera real.
 */
export interface CapturaPendienteResumen {
  clientOpId: string;
  /** Fecha del registro capturado (`YYYY-MM-DD`), o `null` si el payload no la trae. */
  fecha: string | null;
  /** Momento de la captura en el dispositivo (ISO). Desempata el orden. */
  capturadoAt: string;
  /** El servidor la rechazó: necesita que una persona la mire en `/diagnostico`. */
  rechazada: boolean;
}

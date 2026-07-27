/**
 * Política pura de fin de sesión del cliente.
 *
 * Vive aparte de `SessionTimeoutService` porque es la regla más delicada de toda la app
 * offline-first: decide cuándo se expulsa a un operario que puede estar en una granja sin
 * señal, y el camino de expulsión es **el mismo que borra el almacenamiento local**.
 *
 * ## Qué cambió respecto de la versión anterior
 *
 * 1. **Perder la red ya no cierra la sesión.** Antes, 2 heartbeats fallidos (~3 min sin
 *    señal) llamaban a `endSession('sin_conexion')`, que purgaba el storage y mandaba al
 *    login. En una granja sin cobertura eso deslogueaba al usuario en tres minutos, y sin
 *    red **no puede volver a entrar** (el login necesita el backend, y en producción además
 *    reCAPTCHA, que necesita alcanzar a Google). Quedaba encerrado afuera.
 * 2. **Estando sin red, la inactividad tampoco cierra.** Mismo motivo: el logout sería
 *    irreversible hasta recuperar señal.
 * 3. **Con trabajo sin sincronizar, no se cierra nunca por tiempo.** Cerrar purga, y purgar
 *    con capturas pendientes destruye trabajo de campo que no se puede reconstruir.
 * 4. **Aparece un tope duro de jornada** (decisión D4): la sesión sin contacto con el
 *    servidor vale una jornada. Pasado eso hay que reconectarse. Sin ese tope, un dispositivo
 *    perdido sería una ventana de acceso abierta indefinidamente.
 */

export type MotivoFinDeSesion =
  /** El usuario estuvo inactivo con red disponible. */
  | 'inactividad'
  /** El servidor rechazó el token (401 de autenticación). */
  | 'expirada'
  /** Se agotó la jornada permitida sin contacto con el servidor. */
  | 'jornada_offline_vencida';

export interface EstadoSesion {
  /** Ahora, en ms epoch. */
  ahora: number;
  /** Última interacción del usuario, en ms epoch. */
  ultimaActividad: number;
  /** Último heartbeat exitoso contra el backend, en ms epoch. */
  ultimoContactoOk: number;
  /** `false` si el navegador reporta sin red o los heartbeats vienen fallando. */
  enLinea: boolean;
  /** Operaciones capturadas y todavía no sincronizadas. */
  operacionesPendientes: number;
}

export interface LimitesSesion {
  /** Inactividad tolerada CON red. */
  inactividadMs: number;
  /** Tiempo máximo sin contacto con el servidor antes de exigir reconexión. */
  jornadaOfflineMs: number;
}

/** Límites por defecto. La jornada sale de la decisión D4 (12-16 h); se toma el extremo alto. */
export const LIMITES_SESION_POR_DEFECTO: LimitesSesion = {
  inactividadMs: 5 * 60 * 1000,
  jornadaOfflineMs: 16 * 60 * 60 * 1000
};

/**
 * Decide si corresponde cerrar la sesión por tiempo, y por qué motivo.
 * Devuelve `null` si la sesión debe seguir viva.
 *
 * No evalúa `'expirada'`: ese motivo lo dispara el servidor con un 401 de autenticación,
 * no el paso del tiempo.
 */
export function evaluarFinDeSesion(
  estado: EstadoSesion,
  limites: LimitesSesion = LIMITES_SESION_POR_DEFECTO
): MotivoFinDeSesion | null {
  // Regla que gana sobre todas: con trabajo sin subir, no se cierra por tiempo.
  // Cerrar implica purgar, y purgar acá significa perder capturas de campo.
  if (estado.operacionesPendientes > 0) return null;

  if (!estado.enLinea) {
    // Sin red la inactividad NO cierra: el logout sería irreversible hasta recuperar señal.
    // Lo único que cierra es el tope duro de jornada.
    return estado.ahora - estado.ultimoContactoOk >= limites.jornadaOfflineMs
      ? 'jornada_offline_vencida'
      : null;
  }

  // Con red, la política de inactividad de siempre.
  return estado.ahora - estado.ultimaActividad >= limites.inactividadMs
    ? 'inactividad'
    : null;
}

/**
 * ¿Corresponde hacer el heartbeat ahora?
 *
 * Solo si el usuario está activo, igual que antes: pingear a un usuario que dejó la pestaña
 * abierta es tráfico inútil. La diferencia es que ahora un heartbeat fallido no cierra nada,
 * solo marca el modo sin conexión.
 */
export function debeHacerHeartbeat(estado: EstadoSesion, limites: LimitesSesion = LIMITES_SESION_POR_DEFECTO): boolean {
  return estado.ahora - estado.ultimaActividad < limites.inactividadMs;
}

/**
 * Mensaje que se le muestra al usuario. Los dos primeros son los históricos, byte a byte.
 * `sin_conexion` desapareció como motivo de cierre a propósito: ya no cierra sesión.
 */
export function mensajeFinDeSesion(motivo: MotivoFinDeSesion): string {
  switch (motivo) {
    case 'inactividad':
      return 'Tu sesión se cerró por inactividad. Vuelve a iniciar sesión.';
    case 'expirada':
      return 'Tu sesión expiró. Inicia sesión nuevamente.';
    case 'jornada_offline_vencida':
      return 'Llevás demasiado tiempo sin conectarte al servidor. Conectate a una red para seguir trabajando.';
  }
}

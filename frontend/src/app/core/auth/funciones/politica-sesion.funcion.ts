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
  | 'jornada_offline_vencida'
  /** Alguien apagó esta sesión desde el servidor (B1). No es que venció: la revocaron. */
  | 'revocada';

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
 * Qué hacer con una navegación cuando el token ya venció (fix **F-2**).
 *
 * `permitir` no significa que el servidor vaya a aceptar nada: significa que el operario puede
 * seguir usando lo que ya tiene en el dispositivo. Es la diferencia entre una app que sigue
 * mostrando la grilla del galpón y una que lo manda a un login que sin señal no se puede completar.
 */
export type AccesoOffline =
  /** Dejar navegar. */
  | 'permitir'
  /** Cerrar sesión —con la purga que eso implica— y mandar al login. Es lo que se hacía SIEMPRE. */
  | 'cerrar_sesion'
  /** Al login, pero **sin** cerrar sesión: se acabó la jornada offline y no hay red para volver. */
  | 'denegar_jornada_vencida'
  /** Al login, pero sin purgar: hay capturas sin subir y el camino que cierra es el que purga. */
  | 'denegar_trabajo_pendiente';

export interface EstadoAccesoOffline {
  /** Ya lo evaluó `estaVencido` contra el token guardado. */
  tokenVencido: boolean;
  /** Pesimista: `false` también cuando hay wifi pero el backend no contesta. */
  enLinea: boolean;
  ahora: number;
  /** Último contacto seguro con el servidor, en ms epoch. Sale del token, no de un contador vivo. */
  ultimoContactoOk: number;
  /** Capturas de campo todavía sin subir. */
  operacionesPendientes: number;
}

/**
 * Decide si una navegación puede seguir con el token vencido.
 *
 * ## El defecto que corrige
 *
 * El `authGuard` rechazaba **todo** token vencido y llamaba `logout()`, que purga. El JWT dura 60
 * min. O sea que un operario sin señal, al minuto 61, en la primera navegación quedaba deslogueado y
 * con la caché borrada, **sin red para volver a entrar**. La jornada de 16 h de la decisión D4
 * estaba implementada solo para el camino del timer (`evaluarFinDeSesion`); el guard la anulaba.
 *
 * ## Las cuatro salidas
 *
 * | | con red | sin red |
 * |---|---|---|
 * | token vivo | permitir | permitir |
 * | token vencido, dentro de la jornada | cerrar sesión (como siempre) | **permitir** |
 * | token vencido, jornada agotada | cerrar sesión (como siempre) | **denegar, sin purgar** |
 *
 * Con red se cierra igual que antes —el usuario puede volver a entrar ahí mismo—, salvo que haya
 * trabajo sin subir: ésa es la misma regla que `evaluarFinDeSesion` ya protege, porque el camino que
 * cierra es el que purga. Igual va al login; simplemente no se purga en el trayecto.
 *
 * Sin red **nunca** se devuelve `cerrar_sesion`: el logout es irreversible hasta recuperar señal.
 */
export function evaluarAccesoOffline(
  estado: EstadoAccesoOffline,
  limites: LimitesSesion = LIMITES_SESION_POR_DEFECTO
): AccesoOffline {
  if (!estado.tokenVencido) {
    return 'permitir';
  }

  if (!estado.enLinea) {
    return estado.ahora - estado.ultimoContactoOk >= limites.jornadaOfflineMs
      ? 'denegar_jornada_vencida'
      : 'permitir';
  }

  return estado.operacionesPendientes > 0 ? 'denegar_trabajo_pendiente' : 'cerrar_sesion';
}

/** Lo que se le dice al operario cuando se le niega el paso. `null` = no hay nada que avisar. */
export function mensajeAccesoDenegado(acceso: AccesoOffline): string | null {
  switch (acceso) {
    case 'denegar_jornada_vencida':
      return mensajeFinDeSesion('jornada_offline_vencida');
    case 'denegar_trabajo_pendiente':
      return 'Tu sesión expiró y quedaron capturas sin enviar. Volvé a entrar para que salgan.';
    default:
      // `cerrar_sesion` era silencioso y se deja silencioso: el aviso lo da el cierre por timer.
      return null;
  }
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
    // Se dice QUIÉN la cerró y no «expiró»: quien pierde una tablet y la reportan necesita
    // entender que fue a propósito, no un problema de la app.
    case 'revocada':
      return 'Un administrador cerró esta sesión. Iniciá sesión de nuevo.';
  }
}

/**
 * ¿Esta respuesta la fabricó el interceptor porque no había red?
 *
 * Devuelve el `clientOpId` de la operación encolada, o `null` si la respuesta vino del servidor.
 *
 * ## Por qué hace falta una función y no un `if` suelto
 *
 * Cuando el interceptor encola, la pantalla recibe un **202 sintético** en vez del DTO que esperaba.
 * Si el componente no lo distingue, muestra "guardado" a secas y el operario cierra la app
 * convencido de que el dato salió del dispositivo — que es exactamente el modo de falla que la cola
 * debía evitar. Cada pantalla que capture offline tiene que preguntar esto, así que la pregunta vive
 * en un solo lugar y se prueba una sola vez.
 */
export function esRespuestaPendiente(cuerpo: unknown): string | null {
  if (!cuerpo || typeof cuerpo !== 'object') {
    return null;
  }

  const marcado = cuerpo as { __offlinePendiente?: unknown; clientOpId?: unknown };
  if (marcado.__offlinePendiente !== true) {
    return null;
  }

  return typeof marcado.clientOpId === 'string' && marcado.clientOpId.trim() !== ''
    ? marcado.clientOpId
    : null;
}

/** Mensaje único para el operario. Vive acá para que todas las pantallas digan lo mismo. */
export const MENSAJE_GUARDADO_SIN_RED =
  'Guardado en este dispositivo. Se envía solo cuando vuelva la señal.';

/**
 * Espera antes del próximo intento de envío.
 *
 * ## Por qué con jitter
 *
 * Una granja recupera la señal para **todos** los equipos a la vez. Sin jitter, veinte tablets
 * calculan el mismo retardo y vuelven a golpear el backend en el mismo instante, indefinidamente:
 * el backoff sincronizado convierte una caída de red en una tormenta de reintentos.
 */

/** Primer retardo. Un fallo aislado no debería demorar la sincronización más que unos segundos. */
export const BASE_MS = 5_000;

/** Techo: pasada media hora, seguir duplicando no aporta y solo retrasa la recuperación. */
export const TECHO_MS = 30 * 60_000;

/** Proporción del retardo que se sortea, para desincronizar equipos. */
export const JITTER = 0.25;

/**
 * Retardo para el intento número `intentos` (0 = todavía no falló nunca).
 *
 * @param intentos  fallos acumulados de esa operación.
 * @param aleatorio inyectable para que el test sea determinista. Por defecto `Math.random`.
 */
export function calcularEspera(intentos: number, aleatorio: () => number = Math.random): number {
  if (intentos <= 0) {
    return 0;
  }

  const exponencial = Math.min(BASE_MS * 2 ** (intentos - 1), TECHO_MS);

  // Jitter simétrico: ±25 %. Nunca negativo, y nunca por encima del techo.
  const desvio = exponencial * JITTER * (aleatorio() * 2 - 1);
  return Math.max(0, Math.min(Math.round(exponencial + desvio), TECHO_MS));
}

/**
 * Traduce la cabecera `Retry-After` a milisegundos.
 *
 * El servidor sabe mejor que el cliente cuándo va a poder atender: si la manda, **gana** sobre el
 * cálculo local. Acepta las dos formas del estándar (segundos o fecha HTTP).
 */
export function esperaDeRetryAfter(cabecera: string | null | undefined, ahora: number): number | null {
  if (!cabecera) {
    return null;
  }

  const texto = cabecera.trim();
  if (texto === '') {
    return null;
  }

  const segundos = Number(texto);
  if (Number.isFinite(segundos)) {
    return segundos <= 0 ? 0 : Math.round(segundos * 1000);
  }

  const fecha = Date.parse(texto);
  if (Number.isNaN(fecha)) {
    return null;
  }

  return Math.max(0, fecha - ahora);
}

/**
 * Momento (epoch ms) del próximo intento. `Retry-After` tiene prioridad sobre el backoff local.
 */
export function proximoIntento(
  intentos: number,
  ahora: number,
  retryAfter?: string | null,
  aleatorio: () => number = Math.random
): number {
  const delServidor = esperaDeRetryAfter(retryAfter, ahora);
  const espera = delServidor ?? calcularEspera(intentos, aleatorio);
  return ahora + espera;
}

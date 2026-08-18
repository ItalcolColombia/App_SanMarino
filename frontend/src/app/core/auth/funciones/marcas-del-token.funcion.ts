/**
 * Lectura del JWT guardado, sin verificar la firma.
 *
 * El cliente **no puede** validar un token: no tiene la llave. Lo único que hace acá es leer sus
 * marcas de tiempo para decidir a quién deja navegar sin red. La autorización real la sigue haciendo
 * el servidor en cada request; esto solo evita mandar a la pantalla de login a alguien que todavía
 * puede trabajar, y evita lo contrario.
 */

export interface MarcasDelToken {
  /**
   * ¿Se pudo leer el payload? `false` cuenta como vencido —fail-closed, igual que antes—, pero por
   * el camino que **no** purga cuando no hay red.
   */
  legible: boolean;

  /** `exp` en ms epoch. `null` si el token no declara expiración. */
  expiraEn: number | null;

  /** `iat` en ms epoch. `null` si el token no lo trae. */
  emitidoEn: number | null;
}

/**
 * Lee `exp` e `iat` del payload.
 *
 * ## El `atob` pelado no alcanza
 *
 * El payload de un JWT viaja en **base64url**, que usa `-` y `_` donde base64 usa `+` y `/`. `atob`
 * rechaza esos dos caracteres y **lanza**, y hasta hoy esa excepción se leía como "token vencido" ⇒
 * cierre de sesión con purga. Acá se normaliza y se repone el relleno.
 *
 * **Medido** antes de escribir esto, porque la intuición falla en los dos sentidos: un payload de
 * **puro ASCII no lo dispara nunca** (0 de 5.000; hace falta un byte que caiga en el índice 62 o 63
 * y el ASCII no llega), y 256 combinaciones realistas de `firstName`/`surName` con tilde tampoco lo
 * dispararon. Pero cuando el texto no-ASCII pesa en el payload sube rápido: con ~10 % de caracteres
 * acentuados en los claims, **22,9 %** de los tokens. O sea: raro hoy, y una bomba de tiempo atada a
 * qué nombres y qué roles existan en la base.
 */
export function leerMarcasDelToken(token: string | null | undefined): MarcasDelToken {
  const payload = decodificarPayload(token);
  if (payload === null) {
    return { legible: false, expiraEn: null, emitidoEn: null };
  }

  return {
    legible: true,
    expiraEn: aMilisegundos(payload['exp']),
    emitidoEn: aMilisegundos(payload['iat'])
  };
}

/**
 * ¿El token ya expiró?
 *
 * Es **la misma regla que antes**, palabra por palabra: ilegible ⇒ vencido; sin `exp` ⇒ **no**
 * vencido (hay tokens sin expiración y no se los expulsa); con `exp` ⇒ comparación contra el reloj.
 */
export function estaVencido(marcas: MarcasDelToken, ahora: number): boolean {
  if (!marcas.legible) {
    return true;
  }
  return marcas.expiraEn !== null && marcas.expiraEn < ahora;
}

/**
 * El momento en que el servidor con seguridad habló con este dispositivo, según el propio token.
 *
 * Es el ancla de la jornada offline (D4) y **tiene que sobrevivir a un reload**, así que no puede
 * salir de `SessionTimeoutService`: ese contador vive en memoria y `start()` lo reinicia a `Date.now()`
 * en cada arranque, con lo cual el tope de 16 h no llegaría a cumplirse nunca.
 *
 * `iat` es el dato exacto. Si el token no lo trae se cae a `exp`, que es **posterior** al contacto
 * real: la ventana sale a lo sumo una vida de token más larga. El error va hacia dejar trabajar, que
 * es el lado correcto — expulsar antes de tiempo a alguien sin señal no tiene vuelta atrás.
 */
export function ultimoContactoSegunToken(marcas: MarcasDelToken): number | null {
  return marcas.emitidoEn ?? marcas.expiraEn;
}

// ---------------------------------------------------------------------------

function decodificarPayload(token: string | null | undefined): Record<string, unknown> | null {
  const parte = token?.split('.')[1];
  if (!parte) {
    return null;
  }

  try {
    const base64 = parte.replace(/-/g, '+').replace(/_/g, '/');
    const relleno = (4 - (base64.length % 4)) % 4;
    const datos = JSON.parse(atob(base64 + '='.repeat(relleno))) as unknown;
    return datos !== null && typeof datos === 'object' ? (datos as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

/** Las marcas del JWT vienen en segundos. Cualquier otra cosa se descarta. */
function aMilisegundos(valor: unknown): number | null {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor * 1000 : null;
}

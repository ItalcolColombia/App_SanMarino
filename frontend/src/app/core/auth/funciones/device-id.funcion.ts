/**
 * Identificador del equipo.
 *
 * **Sobrevive al logout a propósito**: identifica la tablet, no la sesión. Es lo que permite decir
 * «esta sesión es la del galpón 3» al listar los dispositivos de alguien, y lo que hace que revocar
 * la tablet perdida no toque la del compañero.
 *
 * ⚠️ **El servidor jamás autoriza con esto.** Es una etiqueta, no una credencial: quien la manda es
 * el cliente y puede escribir lo que quiera. La autorización sale del token y de la fila de
 * `sesiones_activas`.
 *
 * Vivía como privado de `OutboxService`; se extrajo acá para que el outbox y el interceptor manden
 * **el mismo** valor. Misma clave, mismo comportamiento.
 */

/** Clave del identificador de equipo. La comparte el outbox — cambiarla parte la trazabilidad en dos. */
export const CLAVE_DEVICE_ID = 'italgranja.deviceId';

/** Valor cuando el storage está bloqueado por política: se opera igual, sólo se pierde la traza. */
export const DEVICE_ID_DESCONOCIDO = 'desconocido';

/** Lee el id del equipo; si no existe, lo crea y lo persiste. Nunca lanza. */
export function obtenerDeviceId(): string {
  try {
    const guardado = localStorage.getItem(CLAVE_DEVICE_ID);
    if (guardado) return guardado;

    const nuevo = nuevoUuid();
    localStorage.setItem(CLAVE_DEVICE_ID, nuevo);
    return nuevo;
  } catch {
    // Storage bloqueado por política: la operación igual se encola, sólo pierde trazabilidad.
    return DEVICE_ID_DESCONOCIDO;
  }
}

/**
 * UUID v4. Usa `crypto.randomUUID` cuando está, con respaldo para contextos donde no exista.
 * No puede fallar: sin un id no hay idempotencia posible.
 */
export function nuevoUuid(): string {
  const cripto = globalThis.crypto as Crypto | undefined;

  if (cripto?.randomUUID) {
    return cripto.randomUUID();
  }

  if (cripto?.getRandomValues) {
    const bytes = cripto.getRandomValues(new Uint8Array(16));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const hex = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
  }

  // Último recurso. Peor entropía, pero una operación sin id no se puede encolar.
  const azar = () => Math.floor(Math.random() * 0x10000).toString(16).padStart(4, '0');
  return `${azar()}${azar()}-${azar()}-4${azar().slice(1)}-a${azar().slice(1)}-${azar()}${azar()}${azar()}`;
}

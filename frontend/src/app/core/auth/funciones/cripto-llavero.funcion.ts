import type { AuthSession } from '../auth.models';

/**
 * Cifrado del llavero de sesiones aparcadas. **Solo `crypto.subtle`.**
 *
 * ## Por qué acá SÍ se cifra, si D3 dijo que no
 *
 * La decisión **D3** eligió no cifrar el dato de negocio en reposo, y **B9** advirtió que hacerlo con
 * el `EncryptionService` de la app sería «teatro»: llave pública en el bundle, salt fijo, AES-CBC sin
 * MAC. Las dos siguen vigentes y este archivo **no las contradice** — el dato de negocio en IndexedDB
 * sigue sin cifrar.
 *
 * Lo que cambia es el contenido: un blob de negocio robado es un snapshot viejo; **N tokens robados
 * son N sesiones vivas de 16 h que nadie puede revocar** (B1 no existe todavía). Ese cálculo es otro.
 *
 * ## Cómo, sin repetir el error de B9
 *
 * - **PBKDF2-SHA256, 210.000 iteraciones**, salt **aleatorio de 16 bytes por slot**. Nada del
 *   `'sanmarino-salt'` fijo.
 * - **AES-GCM** (trae MAC), IV aleatorio de 12 bytes **por escritura**.
 * - La llave se deriva del **PIN**, no de algo que viaje en el bundle. Es la diferencia entre cifrado
 *   y ofuscación: **la tablet robada no contiene la llave.**
 * - `CryptoKey` con `extractable: false`, nunca persistida.
 * - **Fail-closed**: sin `crypto.subtle` (contexto no seguro) devuelve `null` y el llavero se
 *   deshabilita entero — la app se comporta como hoy, una sola sesión. **No hay respaldo débil.**
 *
 * ## El PIN no se compara
 *
 * No se guarda ni un hash ni un `pinCorrecto` que alguien pueda dar vuelta desde el inspector: el PIN
 * es la entrada del KDF. PIN equivocado ⇒ el tag GCM no valida ⇒ `abrir` **lanza**. No hay bypass
 * posible desde el cliente, y por eso `abrir` nunca devuelve `null` «silencioso»: devolver algo sería
 * peor que fallar.
 */

/** Iteraciones del PBKDF2. Alto a propósito: el PIN es de 6 dígitos, o sea poca entropía. */
export const ITERACIONES_PBKDF2 = 210_000;

/** Bytes del salt por slot. Un salt es público por diseño; lo que importa es que sea único. */
export const BYTES_SALT = 16;

/** Bytes del IV de AES-GCM. 12 es el tamaño que el estándar recomienda para GCM. */
export const BYTES_IV = 12;

/** Lo mínimo que se le pide al entorno. Se recibe por parámetro para poder probar su ausencia. */
export type FuenteCripto = Pick<Crypto, 'subtle' | 'getRandomValues'> | null | undefined;

/**
 * ¿Se puede usar el llavero en este entorno?
 *
 * `crypto.subtle` solo existe en contexto seguro. En prod la PWA es HTTPS y en dev es `localhost`:
 * los dos lo son. Si falta, la respuesta correcta no es degradar el cifrado, es no ofrecer llavero.
 */
export function hayCripto(cripto: FuenteCripto = globalThis.crypto): boolean {
  return !!cripto?.subtle && typeof cripto.getRandomValues === 'function';
}

/** Salt nuevo para un slot, en base64. `null` si no hay cripto. */
export function nuevoSaltB64(cripto: FuenteCripto = globalThis.crypto): string | null {
  if (!hayCripto(cripto)) {
    return null;
  }
  return aBase64(cripto!.getRandomValues(new Uint8Array(BYTES_SALT)));
}

/**
 * Id de un slot nuevo. Nombra la clave de su blob en `localStorage`.
 *
 * Sale de **la misma fuente de cripto** que el salt y no de `crypto.randomUUID()` global: si la fuente
 * es la única autoridad de azar, apagarla apaga el llavero completo y no queda una llamada suelta que
 * en un dispositivo sin cripto tire una excepción en vez de devolver `null`.
 */
export function nuevoIdSlot(cripto: FuenteCripto = globalThis.crypto): string | null {
  if (!hayCripto(cripto)) {
    return null;
  }

  const bytes = cripto!.getRandomValues(new Uint8Array(16));
  const hex = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

/**
 * Deriva la llave del slot a partir del PIN y su salt.
 *
 * Devuelve `null` **solo** cuando el entorno no da para cifrar (o el salt es ilegible). Un PIN
 * equivocado deriva una llave perfectamente válida: no se puede saber acá que está mal, y ése es
 * justamente el diseño — el veredicto lo da el tag GCM al abrir.
 */
export async function derivarLlave(
  pin: string,
  saltB64: string,
  cripto: FuenteCripto = globalThis.crypto
): Promise<CryptoKey | null> {
  if (!hayCripto(cripto) || !pin || !saltB64) {
    return null;
  }

  const salt = desdeBase64(saltB64);
  if (salt === null) {
    return null;
  }

  try {
    const base = await cripto!.subtle.importKey('raw', new TextEncoder().encode(pin), 'PBKDF2', false, [
      'deriveKey'
    ]);

    return await cripto!.subtle.deriveKey(
      { name: 'PBKDF2', salt, iterations: ITERACIONES_PBKDF2, hash: 'SHA-256' },
      base,
      { name: 'AES-GCM', length: 256 },
      // No extraíble: ni el propio código puede sacarla del navegador.
      false,
      ['encrypt', 'decrypt']
    );
  } catch {
    return null;
  }
}

/**
 * Cifra la sesión. El blob resultante es `base64(iv ‖ ciphertext)`.
 *
 * El IV va **adelante y en claro**, como corresponde: no es secreto, tiene que ser único. Por eso dos
 * sellados de la misma sesión con la misma llave dan blobs distintos.
 */
export async function sellar(
  sesion: AuthSession,
  llave: CryptoKey,
  cripto: FuenteCripto = globalThis.crypto
): Promise<string | null> {
  if (!hayCripto(cripto)) {
    return null;
  }

  const iv = cripto!.getRandomValues(new Uint8Array(BYTES_IV));
  const claro = new TextEncoder().encode(JSON.stringify(sesion));
  const cifrado = new Uint8Array(await cripto!.subtle.encrypt({ name: 'AES-GCM', iv }, llave, claro));

  const blob = new Uint8Array(iv.length + cifrado.length);
  blob.set(iv, 0);
  blob.set(cifrado, iv.length);
  return aBase64(blob);
}

/**
 * Descifra el blob. **Lanza** si el PIN no es el correcto o si el blob fue tocado: eso es AES-GCM
 * haciendo su trabajo, y es la única validación de PIN que existe en todo el llavero.
 */
export async function abrir(
  blob: string,
  llave: CryptoKey,
  cripto: FuenteCripto = globalThis.crypto
): Promise<AuthSession> {
  if (!hayCripto(cripto)) {
    throw new Error('El llavero necesita crypto.subtle (contexto seguro).');
  }

  const bytes = desdeBase64(blob);
  if (bytes === null || bytes.length <= BYTES_IV) {
    throw new Error('El blob del llavero no tiene forma de blob.');
  }

  const iv = bytes.slice(0, BYTES_IV);
  const cifrado = bytes.slice(BYTES_IV);

  // Si el PIN está mal, esto tira DOMException y NO se atrapa: propagarla es el contrato.
  const claro = await cripto!.subtle.decrypt({ name: 'AES-GCM', iv }, llave, cifrado);
  return JSON.parse(new TextDecoder().decode(claro)) as AuthSession;
}

// ---------------------------------------------------------------------------

function aBase64(bytes: Uint8Array): string {
  let binario = '';
  bytes.forEach(b => (binario += String.fromCharCode(b)));
  return btoa(binario);
}

/**
 * El tipo lleva el `<ArrayBuffer>` explícito: en TypeScript 6 `Uint8Array` es genérico sobre su
 * buffer y el `Uint8Array<ArrayBufferLike>` que sale por defecto **no** es un `BufferSource` válido
 * para `crypto.subtle` (podría ser un `SharedArrayBuffer`). Sin esto no compila.
 */
function desdeBase64(texto: string): Uint8Array<ArrayBuffer> | null {
  try {
    const binario = atob(texto);
    const bytes = new Uint8Array(new ArrayBuffer(binario.length));
    for (let i = 0; i < binario.length; i++) {
      bytes[i] = binario.charCodeAt(i);
    }
    return bytes;
  } catch {
    return null;
  }
}

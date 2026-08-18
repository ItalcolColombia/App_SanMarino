import {
  BYTES_IV,
  FuenteCripto,
  abrir,
  derivarLlave,
  hayCripto,
  nuevoSaltB64,
  sellar
} from './cripto-llavero.funcion';
import type { AuthSession } from '../auth.models';

/**
 * El cifrado del llavero.
 *
 * Es lo único cifrado de verdad en toda la app, y el motivo es concreto: un blob de negocio robado es
 * un snapshot viejo, pero N tokens robados son N sesiones vivas de 16 h que **nadie puede revocar**
 * (B1 no existe). Los tests fijan las tres propiedades que lo separan de la ofuscación: el PIN
 * equivocado **falla** en vez de devolver basura, dos slots con el mismo PIN dan blobs distintos, y
 * sin `crypto.subtle` el llavero se apaga en vez de degradar.
 */
describe('cripto del llavero', () => {
  const PIN = '482913';
  const PIN_MALO = '482914';

  const sesion = {
    accessToken: 'token.de.mentira',
    user: { id: 'guid-alex', userId: 42, firstName: 'José', surName: 'Muñoz' },
    companies: ['Agroavicola Sanmarino'],
    activeCompanyId: 1,
    activePaisId: 1,
    menu: [],
    menusByRole: []
  } as unknown as AuthSession;

  /** Un entorno sin `crypto.subtle`: es el navegador en contexto no seguro. */
  const sinSubtle: FuenteCripto = { getRandomValues: globalThis.crypto.getRandomValues.bind(globalThis.crypto) } as FuenteCripto;

  let salt: string;
  let llave: CryptoKey;

  beforeAll(async () => {
    // Derivar cuesta 210.000 iteraciones de PBKDF2: se hace una sola vez para toda la suite.
    salt = nuevoSaltB64()!;
    llave = (await derivarLlave(PIN, salt))!;
  });

  it('el entorno de test tiene cripto (si esto falla, el resto no prueba nada)', () => {
    expect(hayCripto()).toBeTrue();
    expect(llave).toBeTruthy();
  });

  it('🔑 round-trip: abrir(sellar(s)) devuelve la sesión idéntica', async () => {
    const blob = await sellar(sesion, llave);

    expect(blob).toBeTruthy();
    await expectAsync(abrir(blob!, llave)).toBeResolvedTo(sesion);
  });

  it('🔑 PIN incorrecto ⇒ LANZA. Nunca devuelve basura ni un null silencioso', async () => {
    // Devolver algo sería peor que fallar: una sesión a medio descifrar entraría a la app como buena.
    const blob = (await sellar(sesion, llave))!;
    const llaveMala = (await derivarLlave(PIN_MALO, salt))!;

    await expectAsync(abrir(blob, llaveMala)).toBeRejected();
  });

  it('un blob manoseado también lanza: AES-GCM trae MAC, no es AES-CBC pelado', async () => {
    const blob = (await sellar(sesion, llave))!;
    // Se toca un byte del ciphertext, después del IV.
    const bytes = atob(blob).split('');
    bytes[BYTES_IV + 2] = String.fromCharCode(bytes[BYTES_IV + 2].charCodeAt(0) ^ 0xff);

    await expectAsync(abrir(btoa(bytes.join('')), llave)).toBeRejected();
  });

  it('🔑 el mismo PIN en dos slots da blobs distintos: el salt es por slot', async () => {
    const otroSalt = nuevoSaltB64()!;
    const otraLlave = (await derivarLlave(PIN, otroSalt))!;

    expect(otroSalt).not.toBe(salt);
    expect(await sellar(sesion, otraLlave)).not.toBe(await sellar(sesion, llave));
  });

  it('sellar dos veces con la MISMA llave también da blobs distintos: el IV es por escritura', async () => {
    expect(await sellar(sesion, llave)).not.toBe(await sellar(sesion, llave));
  });

  it('y las dos versiones se abren igual', async () => {
    const uno = (await sellar(sesion, llave))!;
    const dos = (await sellar(sesion, llave))!;

    await expectAsync(abrir(uno, llave)).toBeResolvedTo(sesion);
    await expectAsync(abrir(dos, llave)).toBeResolvedTo(sesion);
  });

  describe('fail-closed: sin crypto.subtle el llavero se apaga', () => {
    it('hayCripto ⇒ false', () => {
      expect(hayCripto(sinSubtle)).toBeFalse();
      expect(hayCripto(null)).toBeFalse();
      // Ojo: `hayCripto(undefined)` NO es este caso. Un `undefined` explícito dispara el parámetro
      // por defecto y termina preguntando por el `crypto` real, que en el test existe. `null` sí
      // llega tal cual. Es la diferencia entre «sin cripto» y «no dije nada».
      expect(hayCripto(undefined)).toBeTrue();
    });

    it('🔑 derivarLlave ⇒ null, que es lo que deshabilita el llavero entero', async () => {
      await expectAsync(derivarLlave(PIN, salt, sinSubtle)).toBeResolvedTo(null);
      await expectAsync(derivarLlave(PIN, salt, null)).toBeResolvedTo(null);
    });

    it('nuevoSaltB64 ⇒ null', () => {
      expect(nuevoSaltB64(sinSubtle)).toBeNull();
    });

    it('sellar ⇒ null: no hay respaldo débil, no se guarda en claro', async () => {
      await expectAsync(sellar(sesion, llave, sinSubtle)).toBeResolvedTo(null);
    });

    it('abrir ⇒ lanza, no devuelve una sesión vacía', async () => {
      await expectAsync(abrir('lo-que-sea', llave, sinSubtle)).toBeRejected();
    });
  });

  describe('entradas inválidas', () => {
    it('PIN vacío o salt vacío ⇒ null (no se deriva de la nada)', async () => {
      await expectAsync(derivarLlave('', salt)).toBeResolvedTo(null);
      await expectAsync(derivarLlave(PIN, '')).toBeResolvedTo(null);
    });

    it('un salt que no es base64 ⇒ null', async () => {
      await expectAsync(derivarLlave(PIN, '@@@ esto no es base64 @@@')).toBeResolvedTo(null);
    });

    it('un blob más corto que el IV lanza, no intenta descifrar aire', async () => {
      await expectAsync(abrir(btoa('corto'), llave)).toBeRejected();
      await expectAsync(abrir('', llave)).toBeRejected();
    });
  });
});

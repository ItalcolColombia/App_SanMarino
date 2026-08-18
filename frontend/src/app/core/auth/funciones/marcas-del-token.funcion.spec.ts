import { estaVencido, leerMarcasDelToken, ultimoContactoSegunToken } from './marcas-del-token.funcion';

/**
 * La lectura del token es lo que decide a quién se expulsa. Hasta ahora vivía inline en el guard,
 * sin un solo test, y su rama de error mandaba a `logout()` — o sea que un fallo de parseo costaba
 * la sesión y la caché.
 */
describe('leerMarcasDelToken', () => {
  const AHORA = 1_700_000_000_000;

  /** Arma un JWT de mentira: solo importa el payload, la firma no se verifica en el cliente. */
  function token(payload: unknown, base64url = true): string {
    const json = JSON.stringify(payload);
    const bytes = new TextEncoder().encode(json);
    let binario = '';
    bytes.forEach(b => (binario += String.fromCharCode(b)));
    let cuerpo = btoa(binario);
    if (base64url) {
      cuerpo = cuerpo.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }
    return `cabecera.${cuerpo}.firma`;
  }

  it('lee exp e iat, que vienen en SEGUNDOS', () => {
    const marcas = leerMarcasDelToken(token({ exp: 1_700_000_060, iat: 1_699_996_460 }));

    expect(marcas.legible).toBeTrue();
    expect(marcas.expiraEn).toBe(1_700_000_060_000);
    expect(marcas.emitidoEn).toBe(1_699_996_460_000);
  });

  it('🔑 un payload con `-` y `_` se lee igual: es base64url, no base64', () => {
    // `atob` pelado LANZA ante un `-` o un `_`, y esa excepción se leía como "token vencido" ⇒
    // cierre de sesión con purga. El payload va fijo y no generado: que un JSON caiga en esos dos
    // caracteres depende del alineamiento de sus bytes, así que un payload "con tildes" armado al
    // vuelo NO garantiza el caso — la primera versión de este test se colgó de eso y falló.
    // Decodifica a {"exp":1700000060,"iat":1699996460,"x":"<bytes altos>"}.
    const conBase64Url = 'cabecera.eyJleHAiOjE3MDAwMDAwNjAsImlhdCI6MTY5OTk5NjQ2MCwieCI6IqPp1522hr_-In0.firma';

    expect(conBase64Url).toContain('-');
    expect(conBase64Url).toContain('_');

    const marcas = leerMarcasDelToken(conBase64Url);
    expect(marcas.legible).toBeTrue();
    expect(marcas.expiraEn).toBe(1_700_000_060_000);
    expect(marcas.emitidoEn).toBe(1_699_996_460_000);
  });

  it('también lee un payload en base64 clásico con relleno', () => {
    expect(leerMarcasDelToken(token({ exp: 1_700_000_060 }, false)).expiraEn).toBe(1_700_000_060_000);
  });

  describe('lo que no se puede leer', () => {
    const basura = ['', '   ', 'sin-puntos', 'a.b', 'a.@@@no-es-base64@@@.c', null, undefined];

    for (const valor of basura) {
      it(`${JSON.stringify(valor)} ⇒ ilegible`, () => {
        const marcas = leerMarcasDelToken(valor as string);
        expect(marcas.legible).toBeFalse();
        expect(marcas.expiraEn).toBeNull();
        expect(marcas.emitidoEn).toBeNull();
      });
    }

    it('un payload que no es un objeto ⇒ ilegible', () => {
      expect(leerMarcasDelToken(token(42)).legible).toBeFalse();
      expect(leerMarcasDelToken(token('hola')).legible).toBeFalse();
    });
  });

  it('exp/iat que no son números se descartan, sin romper', () => {
    const marcas = leerMarcasDelToken(token({ exp: '1700000060', iat: null }));

    expect(marcas.legible).toBeTrue();
    expect(marcas.expiraEn).toBeNull();
    expect(marcas.emitidoEn).toBeNull();
  });

  describe('estaVencido — la MISMA regla que antes, palabra por palabra', () => {
    it('exp en el futuro ⇒ vivo', () => {
      expect(estaVencido(leerMarcasDelToken(token({ exp: 1_700_000_060 })), AHORA)).toBeFalse();
    });

    it('exp en el pasado ⇒ vencido', () => {
      expect(estaVencido(leerMarcasDelToken(token({ exp: 1_699_999_999 })), AHORA)).toBeTrue();
    });

    it('🔑 sin exp ⇒ NO vencido: hay tokens sin expiración y no se expulsa a nadie por eso', () => {
      expect(estaVencido(leerMarcasDelToken(token({ sub: 'u-1' })), AHORA)).toBeFalse();
    });

    it('ilegible ⇒ vencido (fail-closed)', () => {
      expect(estaVencido(leerMarcasDelToken('basura'), AHORA)).toBeTrue();
    });
  });

  describe('ultimoContactoSegunToken — el ancla de la jornada offline', () => {
    it('usa iat cuando está', () => {
      const marcas = leerMarcasDelToken(token({ exp: 1_700_000_060, iat: 1_699_996_460 }));
      expect(ultimoContactoSegunToken(marcas)).toBe(1_699_996_460_000);
    });

    it('🔑 sin iat se cae a exp, que es POSTERIOR al contacto real', () => {
      // Erra hacia dejar trabajar. Expulsar antes de tiempo a alguien sin señal no tiene vuelta.
      const marcas = leerMarcasDelToken(token({ exp: 1_700_000_060 }));
      expect(ultimoContactoSegunToken(marcas)).toBe(1_700_000_060_000);
    });

    it('token ilegible ⇒ null (el guard lo trata como jornada agotada, sin purgar)', () => {
      expect(ultimoContactoSegunToken(leerMarcasDelToken('basura'))).toBeNull();
    });
  });
});

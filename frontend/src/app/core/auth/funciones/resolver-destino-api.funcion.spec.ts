import { resolverDestinoApi } from './resolver-destino-api.funcion';

describe('resolverDestinoApi', () => {
  const base = 'https://app.example/';
  const api = 'https://api.example/api';

  for (const url of [
    'https://api.example/api',
    'https://API.EXAMPLE:443/api/lotes?pagina=1',
    '//api.example/api/lotes',
    'https://api.example/api/a/../lotes'
  ]) {
    it(`reconoce el origen y el prefijo normalizados: ${url}`, () => {
      expect(resolverDestinoApi(url, api, base).esApi).toBeTrue();
    });
  }

  for (const url of [
    'https://api.example.evil.test/api/lotes',
    'https://api.example@evil.test/api/lotes',
    'https://usuario@api.example/api/lotes',
    'https://usuario:clave@api.example/api/lotes',
    'http://api.example/api/lotes',
    'https://api.example:444/api/lotes',
    'https://api.example/api-externa/lotes',
    'https://api.example/apix',
    'https://api.example/api/../externo',
    'https://api.example/api/%2e%2e/externo',
    'https://api.example/api/%2E./externo',
    'https://api.example/api/%2f../externo',
    'https://api.example/api/%5c../externo',
    'https://api.example/api/%252e%252e/externo',
    'https://app.example/api/lotes',
    'https://api.example/imagen.svg?url=/api/lotes',
    'data:text/plain,/api/lotes',
    'https://[',
    ''
  ]) {
    it(`rechaza destinos ajenos o ambiguos: ${url}`, () => {
      expect(resolverDestinoApi(url, api, base)).toEqual({ esApi: false, esLogin: false });
    });
  }

  it('resuelve api y peticiones relativas con la base real del documento', () => {
    expect(resolverDestinoApi('/api/lotes', '/api', base).esApi).toBeTrue();
    expect(resolverDestinoApi('api/lotes', '/api', base).esApi).toBeTrue();
    expect(resolverDestinoApi('./api/lotes', './api/', 'https://app.example/portal/').esApi).toBeTrue();
    expect(resolverDestinoApi('../api/lotes', '/api', 'https://app.example/portal/').esApi).toBeTrue();
    expect(resolverDestinoApi('/assets/icon.svg', '/api', base).esApi).toBeFalse();
    expect(resolverDestinoApi('api/lotes', '/api', 'https://app.example/portal/').esApi).toBeFalse();
  });

  it('reconoce únicamente el endpoint de login dentro del API', () => {
    for (const ruta of ['/Auth/login', '/auth/LOGIN/', '/Auth/login?returnUrl=/']) {
      expect(resolverDestinoApi(`${api}${ruta}`, api, base)).toEqual({ esApi: true, esLogin: true });
    }
    for (const ruta of ['/Auth/login-extra', '/Auth/login/sesiones', '/lotes?next=/Auth/login']) {
      expect(resolverDestinoApi(`${api}${ruta}`, api, base)).toEqual({ esApi: true, esLogin: false });
    }
    expect(resolverDestinoApi('https://evil.test/api/Auth/login', api, base).esLogin).toBeFalse();
  });

  it('falla cerrado ante una configuración ausente o inválida', () => {
    for (const configuracion of ['', 'https://[', 'data:text/plain,/api', 'https://usuario@api.example/api']) {
      expect(resolverDestinoApi(`${api}/lotes`, configuracion, base).esApi).toBeFalse();
    }
  });
});

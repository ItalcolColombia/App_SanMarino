import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';

import {
  debeCerrarSesionPor401,
  MOTIVO_FALLA_PLATAFORMA
} from './debe-cerrar-sesion-por-401.funcion';

function error401(cuerpo: unknown, cabeceras?: Record<string, string>): HttpErrorResponse {
  return new HttpErrorResponse({
    status: 401,
    statusText: 'Unauthorized',
    error: cuerpo,
    headers: cabeceras ? new HttpHeaders(cabeceras) : undefined,
    url: 'http://localhost:5002/api/lote'
  });
}

describe('debeCerrarSesionPor401', () => {
  describe('sí cierra la sesión', () => {
    it('401 de autenticación en una petición con token', () => {
      const err = error401({ error: 'Unauthorized', message: 'Token expirado' });
      expect(debeCerrarSesionPor401(err, true)).toBeTrue();
    });

    it('401 con cuerpo vacío en una petición con token', () => {
      expect(debeCerrarSesionPor401(error401(null), true)).toBeTrue();
    });

    it('401 con un errorCode que no es el de plataforma', () => {
      const err = error401({ errorCode: 'otra-cosa' });
      expect(debeCerrarSesionPor401(err, true)).toBeTrue();
    });
  });

  describe('NO cierra la sesión', () => {
    it('401 del gate de plataforma por errorCode en el cuerpo', () => {
      const err = error401({ error: 'Unauthorized', errorCode: MOTIVO_FALLA_PLATAFORMA });
      expect(debeCerrarSesionPor401(err, true)).toBeFalse();
    });

    it('401 del gate de plataforma con el cuerpo como string', () => {
      const err = error401(`{"errorCode":"${MOTIVO_FALLA_PLATAFORMA}"}`);
      expect(debeCerrarSesionPor401(err, true)).toBeFalse();
    });

    it('401 del gate de plataforma reconocido por la cabecera (mismo origen)', () => {
      const err = error401(null, { 'X-Auth-Failure': MOTIVO_FALLA_PLATAFORMA });
      expect(debeCerrarSesionPor401(err, true)).toBeFalse();
    });

    it('401 del login: sin token, es "credenciales inválidas"', () => {
      const err = error401({ message: 'Usuario o contraseña incorrectos' });
      expect(debeCerrarSesionPor401(err, false)).toBeFalse();
    });

    it('otros estados no cierran sesión', () => {
      for (const status of [0, 400, 403, 404, 429, 500, 502, 504]) {
        const err = new HttpErrorResponse({ status, statusText: 'x' });
        expect(debeCerrarSesionPor401(err, true))
          .withContext(`status ${status}`)
          .toBeFalse();
      }
    });

    it('un error que no es HttpErrorResponse', () => {
      expect(debeCerrarSesionPor401(new Error('boom'), true)).toBeFalse();
      expect(debeCerrarSesionPor401(null, true)).toBeFalse();
      expect(debeCerrarSesionPor401(undefined, true)).toBeFalse();
    });
  });

  it('el 0 (sin red) NO cierra sesión — es el caso de campo sin señal', () => {
    // Con la PWA offline-first esto es crítico: perder señal no puede desloguear a nadie.
    const sinRed = new HttpErrorResponse({ status: 0, statusText: 'Unknown Error' });
    expect(debeCerrarSesionPor401(sinRed, true)).toBeFalse();
  });
});

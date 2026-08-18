import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import type { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';

import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { TokenStorageService } from './token-storage.service';
import { TRABAJO_PENDIENTE_OFFLINE } from './session-timeout.service';
import { ConexionService } from '../pwa/conexion.service';
import { ToastService } from '../../shared/services/toast.service';

/**
 * El cableado del guard, que es donde la política pura no llega.
 *
 * Lo que estos tests fijan no es la decisión —eso ya lo cubre `evaluarAccesoOffline`— sino **de
 * dónde salen sus entradas** y, sobre todo, que `logout()` no se llame sin red. `logout()` purga, y
 * purgar a un operario que está en una granja sin señal lo deja afuera hasta que consiga cobertura:
 * es el bug que este guard tenía y el que ningún test de función pura habría detectado.
 */
describe('authGuard', () => {
  const MIN = 60 * 1000;
  const HORA = 60 * MIN;

  let logout: jasmine.Spy;
  let navigate: jasmine.Spy;
  let warning: jasmine.Spy;
  let hayConexionReal: jasmine.Spy;
  let token: string | null;
  let pendientes: number;

  /** JWT de mentira con las marcas pedidas. El cliente no verifica la firma. */
  function conMarcas(expMs: number, iatMs: number): string {
    const payload = btoa(JSON.stringify({ exp: Math.floor(expMs / 1000), iat: Math.floor(iatMs / 1000) }));
    return `cabecera.${payload}.firma`;
  }

  function correr(): boolean {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)
    ) as boolean;
  }

  beforeEach(() => {
    logout = jasmine.createSpy('logout');
    navigate = jasmine.createSpy('navigate');
    warning = jasmine.createSpy('warning');
    hayConexionReal = jasmine.createSpy('hayConexionReal').and.returnValue(false);
    token = conMarcas(Date.now() + HORA, Date.now());
    pendientes = 0;

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { isAuthenticated: () => token !== null, logout } },
        { provide: TokenStorageService, useValue: { getToken: () => token } },
        { provide: ConexionService, useValue: { hayConexionReal } },
        { provide: ToastService, useValue: { warning } },
        { provide: Router, useValue: { navigate } },
        { provide: TRABAJO_PENDIENTE_OFFLINE, useValue: { operacionesPendientes: () => pendientes } }
      ]
    });
  });

  it('sin sesión manda al login, sin cerrar nada', () => {
    token = null;

    expect(correr()).toBeFalse();
    expect(navigate).toHaveBeenCalledWith(['/login']);
    expect(logout).not.toHaveBeenCalled();
  });

  it('token vivo pasa', () => {
    expect(correr()).toBeTrue();
    expect(navigate).not.toHaveBeenCalled();
    expect(logout).not.toHaveBeenCalled();
  });

  describe('🔑 sin red, el caso que rompía', () => {
    it('token vencido al minuto 61: sigue trabajando y NO se purga', () => {
      token = conMarcas(Date.now() - MIN, Date.now() - 61 * MIN);

      expect(correr()).toBeTrue();
      expect(logout).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('pasadas las 16 h se le niega el paso, pero TAMPOCO se purga', () => {
      token = conMarcas(Date.now() - 15 * HORA, Date.now() - 17 * HORA);

      expect(correr()).toBeFalse();
      expect(logout).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/login']);
      expect(warning).toHaveBeenCalled();
    });

    it('un token ilegible tampoco purga: se niega y listo', () => {
      token = 'esto-no-es-un-jwt';

      expect(correr()).toBeFalse();
      expect(logout).not.toHaveBeenCalled();
    });
  });

  describe('con red', () => {
    beforeEach(() => hayConexionReal.and.returnValue(true));

    it('token vencido cierra sesión, como siempre, y sin avisar', () => {
      token = conMarcas(Date.now() - MIN, Date.now() - 61 * MIN);

      expect(correr()).toBeFalse();
      expect(logout).toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/login']);
      expect(warning).not.toHaveBeenCalled();
    });

    it('con capturas sin subir NO cierra: el camino que cierra es el que purga', () => {
      token = conMarcas(Date.now() - MIN, Date.now() - 61 * MIN);
      pendientes = 3;

      expect(correr()).toBeFalse();
      expect(logout).not.toHaveBeenCalled();
      expect(warning).toHaveBeenCalled();
    });
  });

  it('🔑 el estado de red sale de hayConexionReal, que es el pesimista', () => {
    // `enLinea()` a secas es `navigator.onLine`: dice true con el wifi del galpón levantado aunque
    // no llegue a ningún lado. Ahí el logout sería irreversible igual.
    token = conMarcas(Date.now() - MIN, Date.now() - 61 * MIN);

    correr();

    expect(hayConexionReal).toHaveBeenCalled();
  });
});

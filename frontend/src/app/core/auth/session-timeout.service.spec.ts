import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

import { SessionTimeoutService } from './session-timeout.service';
import { TokenStorageService } from './token-storage.service';
import { AuthService } from './auth.service';
import { LlaveroSesionesService } from './llavero-sesiones.service';
import { ToastService } from '../../shared/services/toast.service';
import type { AuthSession } from './auth.models';

/**
 * La jornada de 16 h es **por operario** (R-M8), y lo único que la renueva es hablar con el servidor.
 *
 * El riesgo concreto que fijan estos tests: si el arranque de la app o el evento `online` del navegador
 * refrescaran el padrón, la ventana se renovaría sola con un F5 o con el wifi del galpón levantado —o
 * sea, no habría tope—, que es justo lo que la decisión D4 vino a poner para que una tablet perdida no
 * sea una ventana abierta indefinidamente.
 */
describe('SessionTimeoutService · la jornada por slot', () => {
  const sesion = { accessToken: 'token', user: { id: 'guid-alex' } } as unknown as AuthSession;

  let servicio: SessionTimeoutService;
  let session$: BehaviorSubject<AuthSession | null>;
  let marcarContactoOk: jasmine.Spy;
  let heartbeat: jasmine.Spy;

  /** `checkHeartbeat` es privado y vive detrás de un `setInterval` de 90 s: se llama directo. */
  function dispararHeartbeat(): void {
    (servicio as unknown as { checkHeartbeat: () => void }).checkHeartbeat();
  }

  beforeEach(() => {
    session$ = new BehaviorSubject<AuthSession | null>(null);
    marcarContactoOk = jasmine.createSpy('marcarContactoOk');
    heartbeat = jasmine.createSpy('heartbeat').and.returnValue(of({}));

    TestBed.configureTestingModule({
      providers: [
        SessionTimeoutService,
        { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
        {
          provide: TokenStorageService,
          useValue: { session$: session$.asObservable(), get: () => session$.value }
        },
        { provide: AuthService, useValue: { heartbeat, logout: () => undefined } },
        { provide: LlaveroSesionesService, useValue: { marcarContactoOk } },
        { provide: ToastService, useValue: { info: () => undefined } }
      ]
    });

    servicio = TestBed.inject(SessionTimeoutService);
    servicio.init();
  });

  afterEach(() => {
    // Soltar la sesión para el `stop()` interno: si no, quedan dos `setInterval` vivos por test.
    session$.next(null);
  });

  it('🔑 un heartbeat con 200 refresca la jornada del slot activo', () => {
    session$.next(sesion);

    dispararHeartbeat();

    expect(heartbeat).toHaveBeenCalled();
    expect(marcarContactoOk).toHaveBeenCalledTimes(1);
    expect(marcarContactoOk.calls.mostRecent().args[0]).toBe('guid-alex');
  });

  it('🔑 arrancar la app NO la refresca: un F5 no es hablar con el servidor', () => {
    // Sin esto, dejar la tablet cerrada y volver a abrirla renovaría la ventana de 16 h para siempre.
    session$.next(sesion);

    expect(marcarContactoOk).not.toHaveBeenCalled();
  });

  it('🔑 un heartbeat que falla por falta de red tampoco la refresca', () => {
    session$.next(sesion);
    heartbeat.and.returnValue(throwError(() => new HttpErrorResponse({ status: 0 })));

    dispararHeartbeat();

    expect(marcarContactoOk).not.toHaveBeenCalled();
  });

  it('un 500 tampoco: el servidor contestó, pero no confirmó la sesión', () => {
    session$.next(sesion);
    heartbeat.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    dispararHeartbeat();

    expect(marcarContactoOk).not.toHaveBeenCalled();
  });

  it('🔑 volver a tener red TAMPOCO la refresca: la interfaz levantada no es el servidor', () => {
    // Hay que pasar de verdad por «sin conexión» primero: `marcarEnLinea(true)` corta antes si el
    // estado ya era `true`, así que sin este paso el test pasaría con la guarda rota (lo dio verde la
    // primera versión de esta prueba).
    session$.next(sesion);
    heartbeat.and.returnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
    dispararHeartbeat();
    dispararHeartbeat();
    expect(servicio.enLinea).toBeFalse();

    marcarContactoOk.calls.reset();
    window.dispatchEvent(new Event('online'));

    expect(servicio.enLinea).toBeTrue();
    expect(marcarContactoOk).not.toHaveBeenCalled();
  });

  it('sin sesión no se llama al heartbeat ni se toca el padrón', () => {
    dispararHeartbeat();

    expect(heartbeat).not.toHaveBeenCalled();
    expect(marcarContactoOk).not.toHaveBeenCalled();
  });

  it('cada heartbeat bueno vuelve a marcarlo: la ventana se corre con el uso real', () => {
    session$.next(sesion);

    dispararHeartbeat();
    dispararHeartbeat();

    expect(marcarContactoOk).toHaveBeenCalledTimes(2);
  });
});

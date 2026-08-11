import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { TokenStorageService } from '../../core/auth/token-storage.service';
import { CacheConsultasService } from './cache-consultas.service';
import { offlineCacheInterceptor } from './offline-cache.interceptor';
import { NOMBRE_BD } from './offline-db';

/**
 * Test de INTEGRACIÓN del interceptor: corre en un Chrome real, o sea con **IndexedDB real**.
 * No hay dobles de la base — lo que se prueba es el camino completo que va a correr en la tablet.
 *
 * El escenario central es el que no se puede probar a mano de forma confiable: la petición falla
 * con `status === 0` (sin red) y la respuesta tiene que salir de lo guardado.
 */
describe('offlineCacheInterceptor (IndexedDB real)', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let cache: CacheConsultasService;

  const SESION = {
    accessToken: 'x',
    user: { id: 'usuario-de-prueba' },
    companies: [],
    activeCompanyId: 5,
    activePaisId: 1,
    menu: [],
    menusByRole: []
  };

  /**
   * Sesión que ve el interceptor. Mutable para poder probar la regla D6 (una cuenta multiempresa no
   * cachea) sin rearmar el TestBed.
   */
  let sesionActual: Record<string, unknown> = SESION;

  /**
   * Borra la base entre pruebas: cada caso arranca de cero.
   *
   * El `race` contra un timeout no es paranoia: `deleteDatabase` queda **pendiente** mientras
   * exista otra conexión abierta a la base, y `CacheConsultasService` mantiene la suya viva entre
   * pruebas. Sin el corte, el `beforeEach` cuelga y todas las pruebas del bloque mueren por
   * timeout de Jasmine, apuntando a la prueba en vez de a la limpieza.
   */
  function borrarBd(): Promise<void> {
    return Promise.race([
      new Promise<void>(resolve => {
        const req = indexedDB.deleteDatabase(NOMBRE_BD);
        req.onsuccess = req.onerror = req.onblocked = () => resolve();
      }),
      new Promise<void>(resolve => setTimeout(resolve, 500))
    ]);
  }

  beforeEach(async () => {
    await borrarBd();
    sesionActual = SESION;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([offlineCacheInterceptor])),
        provideHttpClientTesting(),
        {
          provide: TokenStorageService,
          useValue: { get: () => sesionActual }
        }
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    cache = TestBed.inject(CacheConsultasService);
  });

  afterEach(async () => {
    httpMock.verify();
    // Sin cerrar la conexión, el `deleteDatabase` del próximo `beforeEach` queda bloqueado
    // para siempre y toda la suite muere por timeout.
    cache.cerrarConexion();
    await borrarBd();
  });

  /**
   * Espera hasta que una condición se cumpla, con tope.
   *
   * La escritura en IndexedDB es asíncrona y deliberadamente NO bloquea la respuesta HTTP, así que
   * el test tiene que esperar a que ocurra. Se sondea en vez de dormir un tiempo fijo: un sleep
   * calibrado en esta máquina es un test que falla de forma intermitente en el CI.
   */
  async function esperarHasta(condicion: () => boolean | Promise<boolean>, topeMs = 3000): Promise<void> {
    const limite = Date.now() + topeMs;
    while (Date.now() < limite) {
      if (await condicion()) return;
      await new Promise(r => setTimeout(r, 25));
    }
  }

  /** Espera a que la entrada quede guardada para la identidad por defecto. */
  function esperarGuardado(url: string): Promise<void> {
    return esperarHasta(async () =>
      (await cache.recuperar({ userId: 'usuario-de-prueba', companyId: 5, paisId: 1 }, 'GET', url)) !== undefined
    );
  }

  it('con red: devuelve lo de la red y lo guarda', async () => {
    const cuerpo = [{ id: 1, nombre: 'Lote A' }];

    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush(cuerpo);

    await esperarGuardado('/api/Lote');

    const guardado = await cache.recuperar(
      { userId: 'usuario-de-prueba', companyId: 5, paisId: 1 },
      'GET',
      '/api/Lote'
    );
    expect(guardado?.cuerpo).toEqual(cuerpo);
  });

  it('🔴 SIN RED: sirve la consulta guardada', async () => {
    const cuerpo = [{ id: 1, nombre: 'Lote A' }];

    // 1) Una consulta con red, que deja la entrada guardada.
    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush(cuerpo);
    await esperarGuardado('/api/Lote');

    // 2) La misma consulta, ahora sin red (status 0 = la petición no llegó a ningún lado).
    let recibido: unknown = null;
    http.get('/api/Lote').subscribe(r => (recibido = r));
    httpMock.expectOne('/api/Lote').error(new ProgressEvent('error'), { status: 0 });

    await esperarHasta(() => recibido !== null);

    expect(recibido).toEqual(cuerpo);
    expect(cache.sirviendoDesdeCache()).toBeTrue();
  });

  it('🔴 sin red y SIN nada guardado: propaga el error, no una respuesta vacía', async () => {
    // Devolver un cuerpo vacío dejaría una pantalla en blanco que el usuario leería como
    // "no hay datos" — una afirmación distinta y falsa.
    let error: HttpErrorResponse | null = null;
    http.get('/api/Lote').subscribe({ error: e => (error = e) });
    httpMock.expectOne('/api/Lote').error(new ProgressEvent('error'), { status: 0 });

    await esperarHasta(() => error !== null);

    expect(error).toBeTruthy();
    expect(error!.status).toBe(0);
  });

  it('🔴 un 500 NO se tapa con la caché', async () => {
    // Un 5xx significa que hay red y el backend tiene algo que decir. Servir datos viejos
    // escondería el problema real.
    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush([{ id: 1 }]);
    await esperarGuardado('/api/Lote');

    let error: HttpErrorResponse | null = null;
    http.get('/api/Lote').subscribe({ error: e => (error = e) });
    httpMock.expectOne('/api/Lote').flush('boom', { status: 500, statusText: 'Server Error' });

    await esperarHasta(() => error !== null);

    expect(error!.status).toBe(500);
  });

  it('🔴 un endpoint fuera de la lista blanca no se guarda ni se sirve', async () => {
    http.get('/api/ReporteDiarioCostosEngorde/resumen').subscribe({ error: () => {} });
    httpMock.expectOne('/api/ReporteDiarioCostosEngorde/resumen').flush([{ costo: 1000 }]);

    let error: HttpErrorResponse | null = null;
    http.get('/api/ReporteDiarioCostosEngorde/resumen').subscribe({ error: e => (error = e) });
    httpMock.expectOne('/api/ReporteDiarioCostosEngorde/resumen').error(new ProgressEvent('error'), { status: 0 });

    await esperarHasta(() => error !== null);

    expect(error!.status).toBe(0);
  });

  it('🔴 la caché de OTRA empresa no se sirve (partición)', async () => {
    const cuerpo = [{ id: 1, nombre: 'Lote de la empresa 5' }];

    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush(cuerpo);
    await esperarGuardado('/api/Lote');

    // Misma URL, misma sesión de usuario, OTRA empresa: no debe encontrar nada.
    const deOtraEmpresa = await cache.recuperar(
      { userId: 'usuario-de-prueba', companyId: 6, paisId: 1 },
      'GET',
      '/api/Lote'
    );

    expect(deOtraEmpresa).toBeUndefined();
  });

  it('purgar todo deja la caché vacía (logout)', async () => {
    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush([{ id: 1 }]);
    await esperarGuardado('/api/Lote');

    await cache.purgarTodo();

    const tras = await cache.recuperar({ userId: 'usuario-de-prueba', companyId: 5, paisId: 1 }, 'GET', '/api/Lote');
    expect(tras).toBeUndefined();
  });

  it('purgar la partición borra solo la empresa que se deja', async () => {
    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush([{ id: 1 }]);
    await esperarGuardado('/api/Lote');

    await cache.purgarParticionDe({ userId: 'usuario-de-prueba', companyId: 5, paisId: 1 });

    const tras = await cache.recuperar({ userId: 'usuario-de-prueba', companyId: 5, paisId: 1 }, 'GET', '/api/Lote');
    expect(tras).toBeUndefined();
  });

  it('sin identidad completa no se guarda nada (fail-closed)', async () => {
    const guardado = await cache.guardar({ userId: 'u', companyId: null, paisId: 1 }, 'GET', '/api/Lote', [{ id: 1 }]);
    expect(guardado).toBeFalse();
  });

  // ── D6: cuentas con alcance global o multiempresa ────────────────────────────────

  it('🔴 una cuenta MULTIEMPRESA no guarda nada (D6)', async () => {
    // La partición evita que una sesión lea lo de otra, pero no que el mismo dispositivo junte
    // los datos de todas las empresas que este usuario visita. Y el dato en reposo no se cifra.
    sesionActual = { ...SESION, user: { id: 'multi-1' }, companyIds: [1, 4], activeCompanyId: 61 };

    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush([{ id: 1 }]);

    // Se le da tiempo de sobra: lo que se afirma es que NO aparece, así que hay que esperar
    // más de lo que tardaría en aparecer si el gate no estuviera.
    await esperarHasta(async () =>
      (await cache.recuperar({ userId: 'multi-1', companyId: 61, paisId: 1 }, 'GET', '/api/Lote')) !== undefined,
      500
    );

    const guardado = await cache.recuperar({ userId: 'multi-1', companyId: 61, paisId: 1 }, 'GET', '/api/Lote');
    expect(guardado).toBeUndefined();
  });

  it('🔴 un SUPER ADMIN no guarda nada (D6): su alcance es global', async () => {
    sesionActual = { ...SESION, user: { id: 'super-1', isSuperAdmin: true }, activeCompanyId: 62 };

    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush([{ id: 1 }]);

    await esperarHasta(async () =>
      (await cache.recuperar({ userId: 'super-1', companyId: 62, paisId: 1 }, 'GET', '/api/Lote')) !== undefined,
      500
    );

    expect(await cache.recuperar({ userId: 'super-1', companyId: 62, paisId: 1 }, 'GET', '/api/Lote')).toBeUndefined();
  });

  it('🔴 lo que una cuenta no elegible YA tenía guardado se PURGA', async () => {
    // Un gate que solo impide escribir dejaría intacto —y se seguiría sirviendo— lo cacheado
    // antes del cambio. Sin la purga, el arreglo da una falsa sensación de cierre.
    const identidad = { userId: 'multi-2', companyId: 63, paisId: 1 };

    // Guardado directo, como si viniera de una versión anterior de la app.
    await cache.guardar(identidad, 'GET', '/api/Lote', [{ id: 1 }]);
    expect(await cache.recuperar(identidad, 'GET', '/api/Lote')).toBeDefined();

    sesionActual = { ...SESION, user: { id: 'multi-2' }, companyIds: [1, 4], activeCompanyId: 63 };

    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush([{ id: 2 }]);

    await esperarHasta(async () => (await cache.recuperar(identidad, 'GET', '/api/Lote')) === undefined);

    expect(await cache.recuperar(identidad, 'GET', '/api/Lote')).toBeUndefined();
  });

  it('🔴 una cuenta no elegible tampoco SIRVE caché sin red: propaga el error', async () => {
    const identidad = { userId: 'multi-3', companyId: 64, paisId: 1 };
    await cache.guardar(identidad, 'GET', '/api/Lote', [{ id: 1 }]);

    sesionActual = { ...SESION, user: { id: 'multi-3' }, companyIds: [1, 4], activeCompanyId: 64 };

    let error: HttpErrorResponse | null = null;
    http.get('/api/Lote').subscribe({ error: e => (error = e) });
    httpMock.expectOne('/api/Lote').error(new ProgressEvent('error'), { status: 0 });

    await esperarHasta(() => error !== null);

    expect(error).toBeTruthy();
    expect(error!.status).toBe(0);
  });

  it('el operario de UNA sola empresa sigue cacheando igual que antes', async () => {
    // El caso que no se puede romper: D6 restringe cuentas de alcance amplio, no al usuario de campo.
    sesionActual = { ...SESION, user: { id: 'operario-1' }, companyIds: [7], activeCompanyId: 7 };

    http.get('/api/Lote').subscribe();
    httpMock.expectOne('/api/Lote').flush([{ id: 9 }]);

    await esperarHasta(async () =>
      (await cache.recuperar({ userId: 'operario-1', companyId: 7, paisId: 1 }, 'GET', '/api/Lote')) !== undefined
    );

    const guardado = await cache.recuperar({ userId: 'operario-1', companyId: 7, paisId: 1 }, 'GET', '/api/Lote');
    expect(guardado?.cuerpo).toEqual([{ id: 9 }]);
  });
});

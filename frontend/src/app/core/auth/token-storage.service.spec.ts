import { TestBed } from '@angular/core/testing';

import { TokenStorageService } from './token-storage.service';
import { CacheConsultasService } from '../../shared/offline/cache-consultas.service';
import type { AuthSession } from './auth.models';
import type { IdentidadParticion } from '../../shared/offline/models/offline.model';

/**
 * Qué borra cada salida.
 *
 * Es la clase de bug que no se ve al probarlo: en una tablet con un solo usuario, purgar «su
 * partición» y purgar «todo» dan exactamente el mismo resultado. La diferencia aparece recién en el
 * equipo compartido — y ahí lo que se destruye es el **alistamiento** del otro operario, que cuesta
 * un viaje a la oficina con wifi. Por eso está fijado con tests y no con una prueba en pantalla.
 */
describe('TokenStorageService · purgas', () => {
  let servicio: TokenStorageService;
  let purgarParticionDe: jasmine.Spy;
  let purgarTodo: jasmine.Spy;

  const sesion: AuthSession = {
    accessToken: 'token-de-mentira',
    user: { id: 'guid-alex', userId: 42 },
    companies: ['Agroavicola Sanmarino'],
    activeCompanyId: 1,
    activePaisId: 1,
    menu: [],
    menusByRole: []
  };

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();

    purgarParticionDe = jasmine.createSpy('purgarParticionDe').and.resolveTo(undefined);
    purgarTodo = jasmine.createSpy('purgarTodo').and.resolveTo(undefined);

    TestBed.configureTestingModule({
      providers: [
        TokenStorageService,
        { provide: CacheConsultasService, useValue: { purgarParticionDe, purgarTodo } }
      ]
    });

    servicio = TestBed.inject(TokenStorageService);
    servicio.save(sesion, true);
    purgarParticionDe.calls.reset();
  });

  afterEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  describe('clear() — cerrar sesión', () => {
    it('🔑 purga SOLO la partición propia: lo de los otros operarios sobrevive', () => {
      servicio.clear();

      expect(purgarTodo).not.toHaveBeenCalled();
      expect(purgarParticionDe).toHaveBeenCalledTimes(1);
    });

    it('🔑 purga con la identidad de la sesión que se está yendo, no con nulos', () => {
      // La purga tiene que dispararse ANTES de limpiar el storage. Al revés no da error:
      // `purgarParticionDe` es fail-closed y no borraría nada, en silencio.
      servicio.clear();

      const identidad = purgarParticionDe.calls.mostRecent().args[0] as IdentidadParticion;
      expect(identidad).toEqual({ userId: 'guid-alex', companyId: 1, paisId: 1 });
    });

    it('deja el storage sin sesión', () => {
      servicio.clear();

      expect(servicio.get()).toBeNull();
      expect(localStorage.getItem('auth_session')).toBeNull();
    });
  });

  describe('clearAllTemporal() — el botón del sidebar', () => {
    it('sigue siendo un cierre de sesión: partición propia, no todo', () => {
      servicio.clearAllTemporal();

      expect(purgarTodo).not.toHaveBeenCalled();
      expect(purgarParticionDe).toHaveBeenCalledTimes(1);
    });

    it('vacía el sessionStorage entero, que es lo que lo distingue de clear()', () => {
      sessionStorage.setItem('otra-cosa', 'x');

      servicio.clearAllTemporal();

      expect(sessionStorage.getItem('otra-cosa')).toBeNull();
    });
  });

  describe('borrarDispositivo() — el equipo cambia de manos', () => {
    it('🔑 ahí sí purga TODA la caché', () => {
      servicio.borrarDispositivo();

      expect(purgarTodo).toHaveBeenCalledTimes(1);
      expect(purgarParticionDe).not.toHaveBeenCalled();
    });

    it('y deja el storage limpio', () => {
      servicio.borrarDispositivo();

      expect(servicio.get()).toBeNull();
      expect(localStorage.getItem('auth_session')).toBeNull();
    });
  });

  it('🔑 ninguna salida borra la cola de capturas: solo se llama a la caché de consultas', () => {
    // R9. `purgarTodo`/`purgarParticionDe` operan sobre el store `consultas`; el `outbox` no tiene
    // ningún método de borrado masivo y este servicio no lo inyecta. El test fija que no aparezca.
    servicio.clear();
    servicio.clearAllTemporal();
    servicio.borrarDispositivo();

    const metodosUsados = Object.keys(TestBed.inject(CacheConsultasService) as object);
    expect(metodosUsados.sort()).toEqual(['purgarParticionDe', 'purgarTodo']);
  });
});

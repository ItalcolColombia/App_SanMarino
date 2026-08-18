import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';

import { AuthService } from './auth.service';
import { TokenStorageService } from './token-storage.service';
import { EncryptionService } from './encryption.service';
import { LlaveroSesionesService } from './llavero-sesiones.service';
import type { AuthSession } from './auth.models';
import type { PadronSlots } from './models/slot-sesion.model';

/**
 * Qué se lleva puesto cada salida (R-M6).
 *
 * Es una tabla de tres filas y cuatro columnas, y equivocarse en una celda no se ve al probarlo: en
 * una tablet con un solo usuario, «purgar lo mío» y «purgar todo» dan el mismo resultado. La
 * diferencia aparece recién en el equipo compartido, que es el que nadie tiene a mano.
 *
 * | Acción | Sesión | Slot | Caché propia | Caché de los otros | Cola |
 * |---|---|---|---|---|---|
 * | Cambiar de usuario | aparcada, cifrada | se conserva | se conserva | se conserva | intacta |
 * | Cerrar sesión | se va | se elimina | se purga | **se conserva** | intacta |
 * | Borrar el dispositivo | se va | se eliminan todos | se purga | se purga | intacta |
 */
describe('AuthService · salidas', () => {
  const sesion = {
    accessToken: 'token',
    user: { id: 'guid-alex' },
    companies: [],
    activeCompanyId: 1,
    activePaisId: 1,
    menu: [],
    menusByRole: []
  } as unknown as AuthSession;

  const padron: PadronSlots = {
    version: 1,
    slots: [
      {
        slotId: 'slot-alex',
        userId: 'guid-alex',
        nombre: 'Alex',
        email: 'alex@sanmarino.com.co',
        empresa: 'Agroavicola Sanmarino',
        companyId: 1,
        paisId: 1,
        ultimoUsoEn: 1,
        ultimoContactoOkEn: 1,
        saltB64: 'salt',
        intentosFallidos: 0
      }
    ]
  };

  let servicio: AuthService;
  let storage: jasmine.SpyObj<TokenStorageService>;
  let llavero: jasmine.SpyObj<LlaveroSesionesService>;

  beforeEach(() => {
    storage = jasmine.createSpyObj<TokenStorageService>(
      'TokenStorageService',
      ['get', 'clear', 'clearAllTemporal', 'borrarDispositivo', 'aparcarSesion'],
      { session$: new BehaviorSubject<AuthSession | null>(sesion).asObservable() }
    );
    storage.get.and.returnValue(sesion);

    llavero = jasmine.createSpyObj<LlaveroSesionesService>('LlaveroSesionesService', [
      'aparcar',
      'eliminar',
      'borrarTodos',
      'leerPadron'
    ]);
    llavero.leerPadron.and.returnValue(padron);
    llavero.aparcar.and.resolveTo(true);
    llavero.eliminar.and.resolveTo(undefined);

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        { provide: HttpClient, useValue: {} },
        { provide: EncryptionService, useValue: {} },
        { provide: TokenStorageService, useValue: storage },
        { provide: LlaveroSesionesService, useValue: llavero }
      ]
    });

    servicio = TestBed.inject(AuthService);
  });

  describe('cambiarDeUsuario (aparcar)', () => {
    it('🔑 sella el blob ANTES de soltar la sesión activa', async () => {
      // El orden no es intercambiable: al revés, un fallo de cifrado deja al operario sin sesión y
      // sin copia, sin red, encerrado afuera.
      const orden: string[] = [];
      llavero.aparcar.and.callFake(async () => {
        orden.push('aparcar');
        return true;
      });
      storage.aparcarSesion.and.callFake(() => {
        orden.push('soltar');
      });

      await servicio.cambiarDeUsuario('482913');

      expect(orden).toEqual(['aparcar', 'soltar']);
    });

    it('🔑 si el cifrado falla NO suelta la sesión', async () => {
      llavero.aparcar.and.resolveTo(false);

      expect(await servicio.cambiarDeUsuario('482913')).toBeFalse();
      expect(storage.aparcarSesion).not.toHaveBeenCalled();
    });

    it('🔑 aparcar NO purga ninguna caché ni elimina el slot: quien aparca vuelve', async () => {
      await servicio.cambiarDeUsuario('482913');

      expect(storage.clear).not.toHaveBeenCalled();
      expect(storage.clearAllTemporal).not.toHaveBeenCalled();
      expect(storage.borrarDispositivo).not.toHaveBeenCalled();
      expect(llavero.eliminar).not.toHaveBeenCalled();
      expect(llavero.borrarTodos).not.toHaveBeenCalled();
    });
  });

  describe('logout() — cerrar sesión', () => {
    it('elimina el slot propio y purga solo lo propio', () => {
      servicio.logout({ hard: true });

      expect(llavero.eliminar).toHaveBeenCalledWith('slot-alex');
      expect(storage.clearAllTemporal).toHaveBeenCalled();
      expect(storage.borrarDispositivo).not.toHaveBeenCalled();
      expect(llavero.borrarTodos).not.toHaveBeenCalled();
    });

    it('sin `hard` usa `clear()`, como siempre (guard y timer)', () => {
      servicio.logout();

      expect(storage.clear).toHaveBeenCalled();
      expect(storage.clearAllTemporal).not.toHaveBeenCalled();
    });

    it('🔑 nunca borra los slots de los demás', () => {
      servicio.logout();

      expect(llavero.borrarTodos).not.toHaveBeenCalled();
    });

    it('si el usuario no tiene slot anotado no rompe', () => {
      llavero.leerPadron.and.returnValue({ version: 1, slots: [] });

      servicio.logout();

      expect(llavero.eliminar).not.toHaveBeenCalled();
      expect(storage.clear).toHaveBeenCalled();
    });
  });

  describe("logout({ alcance: 'dispositivo' })", () => {
    it('🔑 ahí sí se van TODOS los slots y toda la caché', () => {
      servicio.logout({ alcance: 'dispositivo' });

      expect(llavero.borrarTodos).toHaveBeenCalled();
      expect(storage.borrarDispositivo).toHaveBeenCalled();
    });

    it('y no toma el camino del logout normal', () => {
      servicio.logout({ alcance: 'dispositivo' });

      expect(storage.clear).not.toHaveBeenCalled();
      expect(storage.clearAllTemporal).not.toHaveBeenCalled();
      expect(llavero.eliminar).not.toHaveBeenCalled();
    });
  });
});

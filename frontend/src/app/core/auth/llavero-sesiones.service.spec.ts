import { TestBed } from '@angular/core/testing';

import { FUENTE_CRIPTO_LLAVERO, LlaveroSesionesService } from './llavero-sesiones.service';
import { TokenStorageService } from './token-storage.service';
import { CacheConsultasService } from '../../shared/offline/cache-consultas.service';
import { OutboxService } from '../../shared/offline/outbox.service';
import { MAX_INTENTOS_PIN, MAX_SLOTS } from './funciones/llavero-sesiones.funcion';
import { derivarLlave, sellar } from './funciones/cripto-llavero.funcion';
import type { AuthSession } from './auth.models';
import type { OperacionPendiente } from '../../shared/offline/models/outbox.model';

/**
 * El llavero de punta a punta, con `localStorage` y cripto REALES.
 *
 * Las dos decisiones (a quién se expulsa, cómo se cifra) ya tienen tests puros; lo que se prueba acá
 * es el pegamento, que es donde puede pasar lo peor: aparcar y creer que se aparcó, pisar la sesión
 * activa con basura, o llevarse puesta la cola de alguien al expulsarlo.
 */
describe('LlaveroSesionesService', () => {
  const PIN = '482913';
  const CLAVE_PADRON = 'italgranja.slots.indice';

  let servicio: LlaveroSesionesService;
  let storage: TokenStorageService;
  let purgarParticionDe: jasmine.Spy;
  let cola: OperacionPendiente[];

  function sesionDe(userId: string, companyId = 1, paisId = 1): AuthSession {
    return {
      accessToken: `token-${userId}`,
      user: { id: userId, userId: 7, username: `${userId}@sanmarino.com.co`, firstName: 'José', surName: 'Muñoz' },
      companies: ['Agroavicola Sanmarino'],
      activeCompany: 'Agroavicola Sanmarino',
      activeCompanyId: companyId,
      activePaisId: paisId,
      menu: [],
      menusByRole: []
    } as unknown as AuthSession;
  }

  function operacion(particion: string, clientOpId: string): OperacionPendiente {
    return {
      clientOpId,
      particion,
      tipo: 'seguimiento_levante',
      companyId: 1,
      paisId: 1,
      userId: 'x',
      deviceId: 'tablet-1',
      capturadoAtDispositivo: '2026-08-18T10:00:00.000Z',
      metodo: 'POST',
      url: '/api/SeguimientoLoteLevante',
      payload: {},
      estado: 'pendiente',
      intentos: 0,
      proximoIntentoEn: null,
      creadoEn: 1
    };
  }

  /** Deja a `userId` registrado en el padrón como si acabara de hacer login. */
  async function conSlotDe(userId: string, ahora = Date.now()): Promise<void> {
    storage.save(sesionDe(userId), true);
    await servicio.registrarLogin(sesionDe(userId), ahora);
  }

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    cola = [];

    purgarParticionDe = jasmine.createSpy('purgarParticionDe').and.resolveTo(undefined);

    TestBed.configureTestingModule({
      providers: [
        LlaveroSesionesService,
        TokenStorageService,
        { provide: CacheConsultasService, useValue: { purgarParticionDe, purgarTodo: () => Promise.resolve() } },
        { provide: OutboxService, useValue: { listarTodas: () => Promise.resolve(cola) } }
      ]
    });

    servicio = TestBed.inject(LlaveroSesionesService);
    storage = TestBed.inject(TokenStorageService);
  });

  afterEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('el entorno de test tiene cripto (si no, el resto no prueba nada)', () => {
    expect(servicio.disponible()).toBeTrue();
  });

  describe('registrarLogin', () => {
    it('deja el slot en el padrón, con salt y slotId propios', async () => {
      await conSlotDe('guid-alex');

      const slots = servicio.leerPadron().slots;
      expect(slots.length).toBe(1);
      expect(slots[0].userId).toBe('guid-alex');
      expect(slots[0].saltB64.length).toBeGreaterThan(0);
      expect(slots[0].slotId.length).toBeGreaterThan(0);
    });

    it('el nombre sale de firstName + surName cuando no hay fullName', async () => {
      await conSlotDe('guid-alex');

      expect(servicio.leerPadron().slots[0].nombre).toBe('José Muñoz');
    });

    it('un segundo login del mismo usuario no duplica ni cambia el salt', async () => {
      await conSlotDe('guid-alex');
      const antes = servicio.leerPadron().slots[0];

      await conSlotDe('guid-alex');
      const despues = servicio.leerPadron().slots;

      expect(despues.length).toBe(1);
      expect(despues[0].saltB64).toBe(antes.saltB64);
      expect(despues[0].slotId).toBe(antes.slotId);
    });

    it('🔑 sin los tres ids de partición no se registra nada: no se podría ni purgar su caché', async () => {
      const sinEmpresa = { ...sesionDe('guid-alex'), activeCompanyId: undefined } as unknown as AuthSession;

      await servicio.registrarLogin(sinEmpresa);

      expect(servicio.leerPadron().slots.length).toBe(0);
    });

    it('un padrón corrupto en localStorage no rompe el registro', async () => {
      localStorage.setItem(CLAVE_PADRON, '{esto no es json');

      await conSlotDe('guid-alex');

      expect(servicio.leerPadron().slots.length).toBe(1);
    });

    describe('con el padrón lleno', () => {
      beforeEach(async () => {
        // Cuatro usuarios, del más viejo al más nuevo.
        for (let i = 0; i < MAX_SLOTS; i++) {
          await conSlotDe(`guid-${i}`, 1_000 + i);
        }
        expect(servicio.leerPadron().slots.length).toBe(MAX_SLOTS);
      });

      it('el quinto expulsa al más viejo y purga SU caché', async () => {
        const victima = servicio.leerPadron().slots[0];

        await conSlotDe('guid-nuevo');

        expect(servicio.leerPadron().slots.some(s => s.userId === victima.userId)).toBeFalse();
        expect(purgarParticionDe).toHaveBeenCalledTimes(1);
        expect((purgarParticionDe.calls.mostRecent().args[0] as { userId: string }).userId).toBe(victima.userId);
      });

      it('🔑 al expulsar borra su blob, pero NUNCA su cola', async () => {
        const victima = servicio.leerPadron().slots[0];
        storage.save(sesionDe(victima.userId), true);
        await servicio.aparcar(PIN);
        expect(localStorage.getItem(`italgranja.slots.${victima.slotId}`)).toBeTruthy();

        cola = [operacion('otra-particion|9|9', 'op-ajena')];
        await conSlotDe('guid-nuevo');

        expect(localStorage.getItem(`italgranja.slots.${victima.slotId}`)).toBeNull();
        // La cola es del service de outbox y el llavero no tiene forma de borrarla: sigue igual.
        expect(cola.length).toBe(1);
      });

      it('🔑 si el más viejo tiene capturas pendientes, se expulsa al siguiente', async () => {
        const slots = servicio.leerPadron().slots;
        cola = [operacion(`${slots[0].userId}|1|1`, 'op-1')];

        await conSlotDe('guid-nuevo');

        const quedan = servicio.leerPadron().slots.map(s => s.userId);
        expect(quedan).toContain(slots[0].userId);
        expect(quedan).not.toContain(slots[1].userId);
      });

      it('🔑 si TODOS tienen pendientes se rechaza, el padrón NO se toca y el login sigue', async () => {
        const slots = servicio.leerPadron().slots;
        cola = slots.map((s, i) => operacion(`${s.userId}|1|1`, `op-${i}`));

        const r = await servicio.registrarLogin(sesionDe('guid-nuevo'));

        expect(r.estado).toBe('rechazado');
        expect(servicio.leerPadron().slots.map(s => s.userId)).toEqual(slots.map(s => s.userId));
        expect(purgarParticionDe).not.toHaveBeenCalled();
      });
    });
  });

  describe('aparcar + activar (el round-trip real)', () => {
    beforeEach(async () => {
      await conSlotDe('guid-alex');
    });

    it('🔑 aparcar cifra la sesión y activar la devuelve idéntica', async () => {
      const original = storage.get()!;

      expect(await servicio.aparcar(PIN)).toBeTrue();

      // Otro operario entra y pisa la sesión activa.
      storage.save(sesionDe('guid-lady', 3, 2), true);
      expect(storage.get()?.user?.id).toBe('guid-lady');

      const slotId = servicio.leerPadron().slots.find(s => s.userId === 'guid-alex')!.slotId;
      const r = await servicio.activar(slotId, PIN);

      expect(r.estado).toBe('activado');
      if (r.estado !== 'activado') return;
      expect(r.sesion).toEqual(original);
      expect(storage.get()).toEqual(original);
    });

    it('🔑 el blob guardado NO contiene el token en claro', async () => {
      await servicio.aparcar(PIN);

      const slotId = servicio.leerPadron().slots[0].slotId;
      const blob = localStorage.getItem(`italgranja.slots.${slotId}`)!;

      expect(blob).not.toContain('token-guid-alex');
      expect(blob).not.toContain('accessToken');
    });

    it('el padrón sigue sin cifrar, a propósito: el selector se pinta sin PIN', () => {
      expect(localStorage.getItem(CLAVE_PADRON)).toContain('guid-alex');
    });

    it('activar deja el slot como el más reciente y borra su blob', async () => {
      await servicio.aparcar(PIN);
      const slotId = servicio.leerPadron().slots[0].slotId;

      await servicio.activar(slotId, PIN, 9_999_999);

      expect(localStorage.getItem(`italgranja.slots.${slotId}`)).toBeNull();
      expect(servicio.leerPadron().slots[0].ultimoUsoEn).toBe(9_999_999);
    });

    it('🔑 PIN incorrecto no toca la sesión activa y devuelve cuántos intentos quedan', async () => {
      await servicio.aparcar(PIN);
      storage.save(sesionDe('guid-lady', 3, 2), true);
      const slotId = servicio.leerPadron().slots.find(s => s.userId === 'guid-alex')!.slotId;

      const r = await servicio.activar(slotId, '000000');

      expect(r.estado).toBe('pin_incorrecto');
      if (r.estado !== 'pin_incorrecto') return;
      expect(r.intentosRestantes).toBe(MAX_INTENTOS_PIN - 1);
      expect(storage.get()?.user?.id).toBe('guid-lady');
      expect(localStorage.getItem(`italgranja.slots.${slotId}`)).toBeTruthy();
    });

    it(`🔑 al ${MAX_INTENTOS_PIN}.º PIN fallido el blob se destruye y la entrada queda marcada`, async () => {
      await servicio.aparcar(PIN);
      const slotId = servicio.leerPadron().slots[0].slotId;

      let ultimo = await servicio.activar(slotId, '000000');
      for (let i = 1; i < MAX_INTENTOS_PIN; i++) {
        ultimo = await servicio.activar(slotId, '000000');
      }

      expect(ultimo.estado).toBe('slot_destruido');
      expect(localStorage.getItem(`italgranja.slots.${slotId}`)).toBeNull();
      // La entrada se CONSERVA: desaparecer de la lista se lee como "se perdió".
      const slot = servicio.leerPadron().slots.find(s => s.slotId === slotId)!;
      expect(slot.requiereReingreso).toBeTrue();
    });

    it('un PIN correcto después de fallar limpia el contador', async () => {
      await servicio.aparcar(PIN);
      const slotId = servicio.leerPadron().slots[0].slotId;

      await servicio.activar(slotId, '000000');
      expect(servicio.leerPadron().slots[0].intentosFallidos).toBe(1);

      await servicio.activar(slotId, PIN);
      expect(servicio.leerPadron().slots[0].intentosFallidos).toBe(0);
    });

    it('activar un slot sin blob ⇒ no_disponible (no hay nada que descifrar)', async () => {
      const slotId = servicio.leerPadron().slots[0].slotId;

      await expectAsync(servicio.activar(slotId, PIN)).toBeResolvedTo({ estado: 'no_disponible' });
    });

    it('activar un slotId inexistente ⇒ no_disponible', async () => {
      await expectAsync(servicio.activar('no-existe', PIN)).toBeResolvedTo({ estado: 'no_disponible' });
    });

    it('🔑 un blob que descifra bien pero sin token NO pisa la sesión activa', async () => {
      // El PIN es correcto —el tag GCM valida— y aun así el contenido no sirve. Sin esta guarda la
      // sesión buena se reemplazaría por una sin token, que es peor que no poder activar.
      const slot = servicio.leerPadron().slots[0];
      const llave = (await derivarLlave(PIN, slot.saltB64))!;
      const blobVacio = (await sellar({ user: { id: 'guid-alex' } } as unknown as AuthSession, llave))!;
      localStorage.setItem(`italgranja.slots.${slot.slotId}`, blobVacio);

      storage.save(sesionDe('guid-lady', 3, 2), true);
      const r = await servicio.activar(slot.slotId, PIN);

      expect(r.estado).toBe('no_disponible');
      expect(storage.get()?.user?.id).toBe('guid-lady');
    });

    it('🔑 aparcar sin slot en el padrón devuelve false y NO escribe blob', async () => {
      localStorage.removeItem(CLAVE_PADRON);

      expect(await servicio.aparcar(PIN)).toBeFalse();
      expect(Object.keys(localStorage).filter(k => k.startsWith('italgranja.slots.')).length).toBe(0);
    });

    it('aparcar no borra la sesión activa: de eso se encarga quien llame, después', async () => {
      await servicio.aparcar(PIN);

      expect(storage.get()?.user?.id).toBe('guid-alex');
    });
  });

  describe('slotsAparcados', () => {
    it('no incluye al de la sesión activa: son los que el selector ofrece', async () => {
      await conSlotDe('guid-alex');
      await conSlotDe('guid-lady');

      // La sesión activa quedó en guid-lady (fue el último login).
      expect(servicio.slotsAparcados().map(s => s.userId)).toEqual(['guid-alex']);
    });
  });

  describe('marcarContactoOk', () => {
    it('mueve solo la jornada de ese slot (R-M8)', async () => {
      await conSlotDe('guid-alex', 1_000);
      await conSlotDe('guid-lady', 1_000);

      servicio.marcarContactoOk('guid-alex', 5_000);

      const slots = servicio.leerPadron().slots;
      expect(slots.find(s => s.userId === 'guid-alex')!.ultimoContactoOkEn).toBe(5_000);
      expect(slots.find(s => s.userId === 'guid-lady')!.ultimoContactoOkEn).toBe(1_000);
    });

    it('un userId desconocido no hace nada', async () => {
      await conSlotDe('guid-alex', 1_000);

      servicio.marcarContactoOk('guid-fantasma', 5_000);

      expect(servicio.leerPadron().slots[0].ultimoContactoOkEn).toBe(1_000);
    });
  });

  describe('eliminar y borrarTodos', () => {
    it('eliminar saca la entrada, el blob y purga su caché', async () => {
      await conSlotDe('guid-alex');
      await servicio.aparcar(PIN);
      const slotId = servicio.leerPadron().slots[0].slotId;

      await servicio.eliminar(slotId);

      expect(servicio.leerPadron().slots.length).toBe(0);
      expect(localStorage.getItem(`italgranja.slots.${slotId}`)).toBeNull();
      expect(purgarParticionDe).toHaveBeenCalled();
    });

    it('borrarTodos deja el dispositivo sin llavero', async () => {
      await conSlotDe('guid-alex');
      await servicio.aparcar(PIN);
      await conSlotDe('guid-lady');

      servicio.borrarTodos();

      expect(servicio.leerPadron().slots.length).toBe(0);
      expect(Object.keys(localStorage).filter(k => k.startsWith('italgranja.slots.')).length).toBe(0);
    });
  });
});

/**
 * El dispositivo sin cripto real: contexto no seguro, o un webview de Android que no la expone.
 *
 * Es la propiedad más importante de todo el llavero y la que en Chrome no se puede ejercitar sin un
 * seam, porque ahí `crypto.subtle` está siempre. Sin estos tests la rama fail-closed quedaría escrita
 * y no verificada, que a los efectos es lo mismo que no estar.
 */
describe('LlaveroSesionesService sin crypto.subtle', () => {
  let servicio: LlaveroSesionesService;
  let storage: TokenStorageService;

  const sesion = {
    accessToken: 'token-guid-alex',
    user: { id: 'guid-alex', username: 'alex@sanmarino.com.co' },
    companies: [],
    activeCompany: 'Agroavicola Sanmarino',
    activeCompanyId: 1,
    activePaisId: 1,
    menu: [],
    menusByRole: []
  } as unknown as AuthSession;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        LlaveroSesionesService,
        TokenStorageService,
        { provide: CacheConsultasService, useValue: { purgarParticionDe: () => Promise.resolve(), purgarTodo: () => Promise.resolve() } },
        { provide: OutboxService, useValue: { listarTodas: () => Promise.resolve([]) } },
        // Hay `getRandomValues` pero NO `subtle`: alcanza para apagar el llavero entero.
        { provide: FUENTE_CRIPTO_LLAVERO, useValue: { getRandomValues: (a: Uint8Array) => a } }
      ]
    });

    servicio = TestBed.inject(LlaveroSesionesService);
    storage = TestBed.inject(TokenStorageService);
    storage.save(sesion, true);
  });

  afterEach(() => localStorage.clear());

  it('🔑 disponible() en false: no hay respaldo débil', () => {
    expect(servicio.disponible()).toBeFalse();
  });

  it('🔑 registrarLogin no anota nada: sin cripto no hay slot que valga', async () => {
    await servicio.registrarLogin(sesion);

    expect(servicio.leerPadron().slots.length).toBe(0);
    expect(localStorage.getItem('italgranja.slots.indice')).toBeNull();
  });

  it('🔑 aparcar devuelve false y NO escribe ningún blob (jamás en claro)', async () => {
    expect(await servicio.aparcar('482913')).toBeFalse();
    expect(Object.keys(localStorage).filter(k => k.startsWith('italgranja.slots.')).length).toBe(0);
  });

  it('activar responde no_disponible', async () => {
    await expectAsync(servicio.activar('cualquiera', '482913')).toBeResolvedTo({ estado: 'no_disponible' });
  });

  it('y la sesión activa sigue intacta: la app se comporta como hoy, con una sola sesión', () => {
    expect(storage.get()?.user?.id).toBe('guid-alex');
  });
});

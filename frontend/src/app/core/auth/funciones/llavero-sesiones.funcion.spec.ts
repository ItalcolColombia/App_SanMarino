import {
  MAX_INTENTOS_PIN,
  MAX_SLOTS,
  PADRON_VACIO,
  eliminarSlot,
  elegirVictima,
  registrarContactoOk,
  registrarPinFallido,
  registrarSlot,
  registrarUsoOk,
  slotVencido
} from './llavero-sesiones.funcion';
import type { DatosSlot, PadronSlots, SlotSesion } from '../models/slot-sesion.model';

/**
 * A quién se expulsa de la tablet.
 *
 * Expulsar un slot **purga su caché**, o sea que destruye su alistamiento: instalar la app y entrar
 * una vez con señal, que en campo es un viaje a la oficina con wifi. Los dos errores no se pagan
 * igual — negarle el lugar a un quinto operario es una molestia; borrarle el turno a uno que tenía
 * capturas sin subir es trabajo de campo perdido. Por eso ante la duda se rechaza.
 */
describe('llavero de sesiones', () => {
  const AHORA = 1_800_000_000_000;
  const MIN = 60 * 1000;
  const HORA = 60 * MIN;

  function datos(userId: string, over: Partial<DatosSlot> = {}): DatosSlot {
    return {
      userId,
      nombre: `Operario ${userId}`,
      email: `${userId}@sanmarino.com.co`,
      empresa: 'Agroavicola Sanmarino',
      companyId: 1,
      paisId: 1,
      slotId: `slot-${userId}`,
      saltB64: `salt-${userId}`,
      ...over
    };
  }

  function slot(userId: string, ultimoUsoEn: number, over: Partial<SlotSesion> = {}): SlotSesion {
    return {
      slotId: `slot-${userId}`,
      userId,
      nombre: `Operario ${userId}`,
      email: `${userId}@sanmarino.com.co`,
      empresa: 'Agroavicola Sanmarino',
      companyId: 1,
      paisId: 1,
      ultimoUsoEn,
      ultimoContactoOkEn: ultimoUsoEn,
      saltB64: `salt-${userId}`,
      intentosFallidos: 0,
      ...over
    };
  }

  function padron(...slots: SlotSesion[]): PadronSlots {
    return { version: 1, slots };
  }

  /** Cuatro slots, del más viejo al más nuevo. Es el padrón lleno con el que se prueba la expulsión. */
  const lleno = padron(
    slot('a', AHORA - 4 * HORA),
    slot('b', AHORA - 3 * HORA),
    slot('c', AHORA - 2 * HORA),
    slot('d', AHORA - HORA)
  );

  describe('registrarSlot', () => {
    it('alta en un padrón vacío ⇒ 1 entrada', () => {
      const r = registrarSlot(PADRON_VACIO, datos('a'), {}, AHORA);

      expect(r.estado).toBe('registrado');
      if (r.estado !== 'registrado') return;
      expect(r.padron.slots.length).toBe(1);
      expect(r.padron.slots[0].userId).toBe('a');
      expect(r.expulsado).toBeNull();
    });

    it('funciona con un padrón nulo o corrupto: sale de localStorage, no de un tipo', () => {
      for (const basura of [null, undefined, {} as PadronSlots, { version: 1, slots: null } as unknown as PadronSlots]) {
        const r = registrarSlot(basura, datos('a'), {}, AHORA);
        expect(r.estado).toBe('registrado');
      }
    });

    it('🔑 re-login del mismo userId ACTUALIZA, no duplica', () => {
      const antes = registrarSlot(PADRON_VACIO, datos('a'), {}, AHORA - HORA);
      expect(antes.estado).toBe('registrado');
      if (antes.estado !== 'registrado') return;

      const r = registrarSlot(antes.padron, datos('a', { empresa: 'ItalcolEcuador', companyId: 3 }), {}, AHORA);

      expect(r.estado).toBe('registrado');
      if (r.estado !== 'registrado') return;
      expect(r.padron.slots.length).toBe(1);
      expect(r.padron.slots[0].empresa).toBe('ItalcolEcuador');
      expect(r.padron.slots[0].ultimoUsoEn).toBe(AHORA);
    });

    it('🔑 al actualizar conserva slotId y salt: son lo que ata la entrada a su blob ya cifrado', () => {
      const antes = registrarSlot(PADRON_VACIO, datos('a'), {}, AHORA - HORA);
      if (antes.estado !== 'registrado') return;

      const r = registrarSlot(antes.padron, datos('a', { slotId: 'otro-slot', saltB64: 'otro-salt' }), {}, AHORA);
      if (r.estado !== 'registrado') return;

      expect(r.padron.slots[0].slotId).toBe('slot-a');
      expect(r.padron.slots[0].saltB64).toBe('salt-a');
    });

    it('un re-login no cuenta contra el tope aunque el padrón esté lleno', () => {
      const r = registrarSlot(lleno, datos('b'), {}, AHORA);

      expect(r.estado).toBe('registrado');
      if (r.estado !== 'registrado') return;
      expect(r.padron.slots.length).toBe(MAX_SLOTS);
      expect(r.expulsado).toBeNull();
    });

    it(`el quinto usuario con ${MAX_SLOTS} slots expulsa al de uso más viejo`, () => {
      const r = registrarSlot(lleno, datos('e'), {}, AHORA);

      expect(r.estado).toBe('registrado');
      if (r.estado !== 'registrado') return;
      expect(r.expulsado?.userId).toBe('a');
      expect(r.padron.slots.map(s => s.userId).sort()).toEqual(['b', 'c', 'd', 'e']);
    });

    it('🔑 si el LRU tiene capturas pendientes, expulsa al SIGUIENTE sin pendientes', () => {
      const r = registrarSlot(lleno, datos('e'), { 'slot-a': 3 }, AHORA);

      expect(r.estado).toBe('registrado');
      if (r.estado !== 'registrado') return;
      expect(r.expulsado?.userId).toBe('b');
      expect(r.padron.slots.some(s => s.userId === 'a')).toBeTrue();
    });

    it('🔑 si TODOS tienen pendientes, se rechaza el quinto login con motivo tipado', () => {
      // Fail-closed (R-M2): antes que destruir trabajo de campo, se niega la comodidad.
      const r = registrarSlot(lleno, datos('e'), { 'slot-a': 1, 'slot-b': 2, 'slot-c': 1, 'slot-d': 5 }, AHORA);

      expect(r.estado).toBe('rechazado');
      if (r.estado !== 'rechazado') return;
      expect(r.motivo).toBe('todos_con_capturas_pendientes');
      // Ordenados por antigüedad: el mensaje tiene que poder nombrar a alguien concreto.
      expect(r.conPendientes.map(s => s.userId)).toEqual(['a', 'b', 'c', 'd']);
    });

    it('no muta el padrón que recibe', () => {
      const copia = JSON.parse(JSON.stringify(lleno)) as PadronSlots;

      registrarSlot(lleno, datos('e'), {}, AHORA);

      expect(lleno).toEqual(copia);
    });
  });

  describe('elegirVictima', () => {
    it('el de uso más viejo entre los que no tienen nada esperando', () => {
      expect(elegirVictima(lleno.slots, {})?.userId).toBe('a');
    });

    it('un slot con 0 pendientes explícitos es elegible; uno sin entrada también', () => {
      expect(elegirVictima(lleno.slots, { 'slot-a': 0 })?.userId).toBe('a');
    });

    it('todos con pendientes ⇒ null, que significa «a nadie», no «error»', () => {
      expect(elegirVictima(lleno.slots, { 'slot-a': 1, 'slot-b': 1, 'slot-c': 1, 'slot-d': 1 })).toBeNull();
    });

    it('padrón vacío ⇒ null', () => {
      expect(elegirVictima([], {})).toBeNull();
    });
  });

  describe('slotVencido — la jornada es POR SLOT (R-M8)', () => {
    it('recién usado, no vencido', () => {
      expect(slotVencido(slot('a', AHORA), AHORA)).toBeFalse();
    });

    it('a las 16 h exactas, vencido (mismo límite inclusivo que el guard)', () => {
      expect(slotVencido(slot('a', AHORA, { ultimoContactoOkEn: AHORA - 16 * HORA }), AHORA)).toBeTrue();
    });

    it('un milisegundo antes, no', () => {
      expect(slotVencido(slot('a', AHORA, { ultimoContactoOkEn: AHORA - 16 * HORA + 1 }), AHORA)).toBeFalse();
    });

    it('🔑 mira ultimoContactoOkEn, NO ultimoUsoEn: usar la app no es hablar con el servidor', () => {
      const usadoReciénPeroSinRedHaceDosDías = slot('a', AHORA, { ultimoContactoOkEn: AHORA - 48 * HORA });

      expect(slotVencido(usadoReciénPeroSinRedHaceDosDías, AHORA)).toBeTrue();
    });

    it('🔑 un slot vencido NO se borra solo: sigue en el padrón (borrar es purgar)', () => {
      const conVencido = padron(slot('a', AHORA - 40 * HORA, { ultimoContactoOkEn: AHORA - 40 * HORA }));

      const r = registrarSlot(conVencido, datos('b'), {}, AHORA);

      expect(r.estado).toBe('registrado');
      if (r.estado !== 'registrado') return;
      expect(r.padron.slots.some(s => s.userId === 'a')).toBeTrue();
    });
  });

  describe('registrarPinFallido', () => {
    it('suma un intento y devuelve cuántos quedan', () => {
      const r = registrarPinFallido(padron(slot('a', AHORA)), 'slot-a');

      expect(r.destruir).toBeFalse();
      expect(r.intentosRestantes).toBe(MAX_INTENTOS_PIN - 1);
      expect(r.padron.slots[0].intentosFallidos).toBe(1);
    });

    it(`🔑 al ${MAX_INTENTOS_PIN}.º manda a destruir el blob y marca requiereReingreso`, () => {
      let actual = padron(slot('a', AHORA));
      let destruir = false;

      for (let i = 0; i < MAX_INTENTOS_PIN; i++) {
        const r = registrarPinFallido(actual, 'slot-a');
        actual = r.padron;
        destruir = r.destruir;
      }

      expect(destruir).toBeTrue();
      expect(actual.slots[0].requiereReingreso).toBeTrue();
      expect(actual.slots[0].intentosFallidos).toBe(0);
    });

    it('🔑 la entrada se CONSERVA aunque el blob se destruya: desaparecer se lee como «se perdió»', () => {
      let actual = padron(slot('a', AHORA));
      for (let i = 0; i < MAX_INTENTOS_PIN; i++) {
        actual = registrarPinFallido(actual, 'slot-a').padron;
      }

      expect(actual.slots.length).toBe(1);
    });

    it('un slotId que no existe no rompe ni inventa entradas', () => {
      const r = registrarPinFallido(padron(slot('a', AHORA)), 'slot-fantasma');

      expect(r.destruir).toBeFalse();
      expect(r.padron.slots.length).toBe(1);
      expect(r.padron.slots[0].intentosFallidos).toBe(0);
    });
  });

  describe('registrarUsoOk', () => {
    it('lo vuelve el más reciente y limpia los intentos fallidos', () => {
      const previo = padron(slot('a', AHORA - 5 * HORA, { intentosFallidos: 3, requiereReingreso: true }));

      const r = registrarUsoOk(previo, 'slot-a', AHORA);

      expect(r.slots[0].ultimoUsoEn).toBe(AHORA);
      expect(r.slots[0].intentosFallidos).toBe(0);
      expect(r.slots[0].requiereReingreso).toBeFalse();
    });

    it('🔑 NO renueva la jornada offline: activar un slot no es hablar con el servidor', () => {
      const previo = padron(slot('a', AHORA - 20 * HORA, { ultimoContactoOkEn: AHORA - 20 * HORA }));

      const r = registrarUsoOk(previo, 'slot-a', AHORA);

      expect(r.slots[0].ultimoContactoOkEn).toBe(AHORA - 20 * HORA);
      expect(slotVencido(r.slots[0], AHORA)).toBeTrue();
    });
  });

  describe('registrarContactoOk', () => {
    it('ahí sí arranca de nuevo la jornada de ESE slot', () => {
      const previo = padron(
        slot('a', AHORA, { ultimoContactoOkEn: AHORA - 20 * HORA }),
        slot('b', AHORA, { ultimoContactoOkEn: AHORA - 20 * HORA })
      );

      const r = registrarContactoOk(previo, 'slot-a', AHORA);

      expect(slotVencido(r.slots[0], AHORA)).toBeFalse();
      // Que A hable con el servidor no le renueva la jornada a B (R-M8).
      expect(slotVencido(r.slots[1], AHORA)).toBeTrue();
    });
  });

  describe('eliminarSlot', () => {
    it('saca solo esa entrada', () => {
      const r = eliminarSlot(lleno, 'slot-b');

      expect(r.slots.map(s => s.userId)).toEqual(['a', 'c', 'd']);
    });

    it('un slotId inexistente deja el padrón como estaba', () => {
      expect(eliminarSlot(lleno, 'slot-fantasma').slots.length).toBe(MAX_SLOTS);
    });
  });
});

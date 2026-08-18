import { filasSelector, tiempoRelativo } from './filas-selector.funcion';
import type { SlotSesion } from '../../../../core/auth/models/slot-sesion.model';

/**
 * Lo que ve alguien que levanta la tablet sin señal.
 *
 * La regla que gobierna estos tests: **ninguna fila se esconde**. Un slot que no se puede abrir sigue
 * en la lista, apagado y con el motivo — desaparecer se lee como «se perdió mi sesión», y si adentro
 * hay capturas sin subir eso es exactamente lo que no hay que hacer sentir.
 */
describe('filasSelector', () => {
  const AHORA = 1_800_000_000_000;
  const MIN = 60 * 1000;
  const HORA = 60 * MIN;

  function slot(userId: string, over: Partial<SlotSesion> = {}): SlotSesion {
    return {
      slotId: `slot-${userId}`,
      userId,
      nombre: `Operario ${userId}`,
      email: `${userId}@sanmarino.com.co`,
      empresa: 'Agroavicola Sanmarino',
      companyId: 1,
      paisId: 1,
      ultimoUsoEn: AHORA - HORA,
      ultimoContactoOkEn: AHORA - HORA,
      saltB64: 'salt',
      intentosFallidos: 0,
      ...over
    };
  }

  it('ordena del más reciente al más viejo: el que vuelve es el último que usó el equipo', () => {
    const filas = filasSelector(
      [
        slot('viejo', { ultimoUsoEn: AHORA - 5 * HORA }),
        slot('nuevo', { ultimoUsoEn: AHORA - MIN }),
        slot('medio', { ultimoUsoEn: AHORA - 2 * HORA })
      ],
      {},
      AHORA
    );

    expect(filas.map(f => f.slot.userId)).toEqual(['nuevo', 'medio', 'viejo']);
  });

  it('un slot recién usado es activable', () => {
    expect(filasSelector([slot('a')], {}, AHORA)[0].estado).toBe('activable');
  });

  it('pasadas las 16 h queda jornada_vencida, y NO desaparece de la lista', () => {
    const filas = filasSelector([slot('a', { ultimoContactoOkEn: AHORA - 17 * HORA })], {}, AHORA);

    expect(filas.length).toBe(1);
    expect(filas[0].estado).toBe('jornada_vencida');
  });

  it('🔑 requiereReingreso gana sobre el vencimiento: el motivo que se le dice es otro', () => {
    // Los dos llevan al login con red, pero «se agotaron los intentos» y «llevás mucho sin
    // conectarte» mandan a buscar el problema a lugares distintos.
    const filas = filasSelector(
      [slot('a', { requiereReingreso: true, ultimoContactoOkEn: AHORA - 17 * HORA })],
      {},
      AHORA
    );

    expect(filas[0].estado).toBe('requiere_reingreso');
  });

  it('el vencimiento mira ultimoContactoOkEn, no ultimoUsoEn', () => {
    const usadoReciénSinRed = slot('a', { ultimoUsoEn: AHORA - MIN, ultimoContactoOkEn: AHORA - 20 * HORA });

    expect(filasSelector([usadoReciénSinRed], {}, AHORA)[0].estado).toBe('jornada_vencida');
  });

  it('las capturas pendientes vienen del mapa, y sin entrada son 0', () => {
    const filas = filasSelector([slot('a'), slot('b')], { 'slot-a': 3 }, AHORA);

    expect(filas.find(f => f.slot.userId === 'a')!.pendientes).toBe(3);
    expect(filas.find(f => f.slot.userId === 'b')!.pendientes).toBe(0);
  });

  it('sin slots devuelve []', () => {
    expect(filasSelector([], {}, AHORA)).toEqual([]);
    expect(filasSelector(null, {}, AHORA)).toEqual([]);
    expect(filasSelector(undefined, {}, AHORA)).toEqual([]);
  });

  it('no muta el arreglo que recibe (lo ordena sobre una copia)', () => {
    const slots = [slot('a', { ultimoUsoEn: 1 }), slot('b', { ultimoUsoEn: 2 })];

    filasSelector(slots, {}, AHORA);

    expect(slots.map(s => s.userId)).toEqual(['a', 'b']);
  });
});

describe('tiempoRelativo', () => {
  const AHORA = 1_800_000_000_000;
  const MIN = 60 * 1000;
  const HORA = 60 * MIN;
  const DIA = 24 * HORA;

  it('menos de un minuto ⇒ recién', () => {
    expect(tiempoRelativo(AHORA - 30_000, AHORA)).toBe('recién');
  });

  it('minutos', () => {
    expect(tiempoRelativo(AHORA - 20 * MIN, AHORA)).toBe('hace 20 min');
    expect(tiempoRelativo(AHORA - 59 * MIN, AHORA)).toBe('hace 59 min');
  });

  it('🔑 trunca hacia abajo: 100 minutos es «hace 1 h», nunca «hace 2 h»', () => {
    // Un redondeo al más cercano diría «hace 2 h» de algo de hace 100 min, y el operario mediría su
    // jornada con ese número. Se prefiere quedarse corto.
    expect(tiempoRelativo(AHORA - 100 * MIN, AHORA)).toBe('hace 1 h');
  });

  it('horas y días', () => {
    expect(tiempoRelativo(AHORA - 5 * HORA, AHORA)).toBe('hace 5 h');
    expect(tiempoRelativo(AHORA - 23 * HORA, AHORA)).toBe('hace 23 h');
    expect(tiempoRelativo(AHORA - DIA, AHORA)).toBe('ayer');
    expect(tiempoRelativo(AHORA - 3 * DIA, AHORA)).toBe('hace 3 días');
  });

  it('🔑 con el reloj del equipo adelantado dice «recién», no un negativo', () => {
    // Las tablets de granja tienen el reloj corrido; «hace -4 min» es peor que no decir nada.
    expect(tiempoRelativo(AHORA + 4 * MIN, AHORA)).toBe('recién');
  });
});

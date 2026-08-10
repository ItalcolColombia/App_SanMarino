import { TTL_CONSULTA_MS, antiguedadLegible, vigenciaCache } from './vigencia-cache.funcion';

const HORA = 60 * 60 * 1000;
const AHORA = 1_800_000_000_000; // instante fijo: nada acá depende del reloj real

describe('vigenciaCache', () => {
  it('lo recién guardado está vigente', () => {
    expect(vigenciaCache(AHORA, AHORA)).toBe('vigente');
  });

  it('sigue vigente justo antes de las 16 h', () => {
    expect(vigenciaCache(AHORA - (16 * HORA - 1000), AHORA)).toBe('vigente');
  });

  it('vence justo después de las 16 h', () => {
    expect(vigenciaCache(AHORA - (16 * HORA + 1000), AHORA)).toBe('vencida');
  });

  it('la ventana es la jornada offline de la decisión D4', () => {
    expect(TTL_CONSULTA_MS).toBe(16 * HORA);
  });

  it('🔴 un guardado en el FUTURO vence', () => {
    // El reloj del dispositivo se movió: con el reloj corrido no se puede afirmar nada sobre la
    // antigüedad del dato, así que no se sirve.
    expect(vigenciaCache(AHORA + HORA, AHORA)).toBe('vencida');
  });

  it('valores no numéricos vencen', () => {
    expect(vigenciaCache(NaN, AHORA)).toBe('vencida');
    expect(vigenciaCache(AHORA, NaN)).toBe('vencida');
  });

  it('acepta un TTL propio (para pruebas y para futuras ventanas por dataset)', () => {
    expect(vigenciaCache(AHORA - 2 * HORA, AHORA, HORA)).toBe('vencida');
    expect(vigenciaCache(AHORA - 30 * 60 * 1000, AHORA, HORA)).toBe('vigente');
  });
});

describe('antiguedadLegible', () => {
  it('describe la antigüedad en la unidad que el operario entiende', () => {
    expect(antiguedadLegible(AHORA, AHORA)).toBe('hace instantes');
    expect(antiguedadLegible(AHORA - 5 * 60 * 1000, AHORA)).toBe('hace 5 min');
    expect(antiguedadLegible(AHORA - HORA, AHORA)).toBe('hace 1 h');
    expect(antiguedadLegible(AHORA - 3 * HORA, AHORA)).toBe('hace 3 h');
  });

  it('no inventa una antigüedad si el reloj está corrido', () => {
    expect(antiguedadLegible(AHORA + HORA, AHORA)).toBe('fecha desconocida');
  });
});

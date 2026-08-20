import {
  describirDispositivo,
  esDispositivoMovil,
  estadoDeSesion,
  haceCuanto,
  SesionDescribible
} from './describir-sesion.funcion';

const AHORA = Date.parse('2026-08-18T12:00:00Z');

function sesion(parcial: Partial<SesionDescribible> = {}): SesionDescribible {
  return {
    deviceId: null,
    userAgent: null,
    expiresAt: '2026-08-19T04:00:00Z',
    revokedAt: null,
    lastSeenAt: null,
    ...parcial
  };
}

describe('describirDispositivo', () => {
  it('nombra la plataforma, que es lo que una persona reconoce', () => {
    const s = sesion({ userAgent: 'Mozilla/5.0 (Linux; Android 14) AppleWebKit' });
    expect(describirDispositivo(s)).toContain('Android');
  });

  it('agrega el prefijo del deviceId para desempatar dos equipos iguales', () => {
    const s = sesion({ userAgent: 'Android', deviceId: 'abcdef12-3456-7890-abcd-ef1234567890' });
    expect(describirDispositivo(s)).toBe('Android · abcdef12');
  });

  it('sin user-agent cae al id del equipo', () => {
    expect(describirDispositivo(sesion({ deviceId: 'ffffffff-0000' }))).toBe('Equipo ffffffff');
  });

  it('sin nada, lo dice en vez de mostrar un hueco', () => {
    expect(describirDispositivo(sesion())).toBe('Equipo desconocido');
  });
});

describe('esDispositivoMovil', () => {
  it('reconoce los móviles', () => {
    expect(esDispositivoMovil(sesion({ userAgent: 'Android 14' }))).toBeTrue();
    expect(esDispositivoMovil(sesion({ userAgent: 'iPhone OS 18' }))).toBeTrue();
  });

  it('un escritorio no lo es', () => {
    expect(esDispositivoMovil(sesion({ userAgent: 'Windows NT 10.0' }))).toBeFalse();
    expect(esDispositivoMovil(sesion())).toBeFalse();
  });
});

describe('estadoDeSesion', () => {
  it('viva y vigente = activa', () => {
    expect(estadoDeSesion(sesion(), AHORA)).toBe('activa');
  });

  it('vencida', () => {
    expect(estadoDeSesion(sesion({ expiresAt: '2026-08-18T11:00:00Z' }), AHORA)).toBe('vencida');
  });

  it('REVOCADA gana sobre vencida — misma precedencia que el backend', () => {
    // Si alguien la apagó a propósito, eso es lo que hay que mostrar: «venció» esconde el hecho.
    const s = sesion({ expiresAt: '2026-08-18T11:00:00Z', revokedAt: '2026-08-18T10:00:00Z' });
    expect(estadoDeSesion(s, AHORA)).toBe('revocada');
  });
});

describe('haceCuanto', () => {
  it('sin contacto todavía', () => {
    expect(haceCuanto(null, AHORA)).toBe('sin contacto todavía');
  });

  it('minutos, horas y días', () => {
    expect(haceCuanto('2026-08-18T11:57:00Z', AHORA)).toBe('hace 3 min');
    expect(haceCuanto('2026-08-18T09:00:00Z', AHORA)).toBe('hace 3 h');
    expect(haceCuanto('2026-08-17T12:00:00Z', AHORA)).toBe('hace 1 día');
    expect(haceCuanto('2026-08-15T12:00:00Z', AHORA)).toBe('hace 3 días');
  });

  it('un reloj adelantado no produce «hace -2 min»', () => {
    expect(haceCuanto('2026-08-18T12:02:00Z', AHORA)).toBe('recién');
  });
});

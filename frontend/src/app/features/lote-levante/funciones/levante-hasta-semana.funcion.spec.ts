import {
  fechaLimiteLevante,
  maxFechaRegistroLevante,
  mensajeLevanteFueraDeLimite,
  permiteRegistroLevante
} from './levante-hasta-semana.funcion';

describe('levante-hasta-semana.funcion', () => {
  const encaset = '2026-01-01';

  it('sin límite todo pasa y no hay fecha límite', () => {
    expect(permiteRegistroLevante(encaset, '2030-01-01', null)).toBeTrue();
    expect(fechaLimiteLevante(encaset, null)).toBeNull();
  });

  it('sin fecha de encaset no restringe', () => {
    expect(permiteRegistroLevante(null, '2030-01-01', 25)).toBeTrue();
    expect(fechaLimiteLevante(null, 25)).toBeNull();
  });

  it('el último día de la semana límite pasa y el siguiente no (mismo borde que el backend)', () => {
    expect(fechaLimiteLevante(encaset, 25)).toBe('2026-06-24');
    expect(permiteRegistroLevante(encaset, '2026-06-24', 25)).toBeTrue();
    expect(permiteRegistroLevante(encaset, '2026-06-25', 25)).toBeFalse();
  });

  it('el max del date picker es el menor entre la ventana y el último día', () => {
    expect(maxFechaRegistroLevante('2026-10-02', '2026-06-24')).toBe('2026-06-24');
    expect(maxFechaRegistroLevante('2026-03-01', '2026-06-24')).toBe('2026-03-01');
    expect(maxFechaRegistroLevante('2026-10-02', null)).toBe('2026-10-02');
  });

  it('el mensaje cita límite, último día y semana del registro', () => {
    const msg = mensajeLevanteFueraDeLimite(25, '2026-06-24', 26);
    expect(msg).toContain('hasta la semana 25');
    expect(msg).toContain('último día: 24/06/2026');
    expect(msg).toContain('semana 26');
  });
});

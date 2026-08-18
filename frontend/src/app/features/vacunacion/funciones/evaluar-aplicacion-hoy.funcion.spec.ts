import { evaluarAplicacionHoy, hoyEnBaseDelServidor } from './evaluar-aplicacion-hoy.funcion';

/**
 * Espejo de `VacunacionCalculosTests.ProyectarAplicacion_*`: los mismos bordes, del lado del
 * navegador. Si estos dejan de coincidir con el backend, el modal vuelve a mentirle al usuario.
 */
describe('evaluarAplicacionHoy', () => {
  const INICIO = '2026-08-10T00:00:00';
  const FIN = '2026-08-16T00:00:00';

  it('dentro de la franja no pide nada', () => {
    const r = evaluarAplicacionHoy(INICIO, FIN, '2026-08-13');

    expect(r.fueraDeRango).toBeFalse();
    expect(r.diasDesviacion).toBe(0);
    expect(r.mensaje).toBeNull();
  });

  it('el primer día de la franja ya está dentro', () => {
    expect(evaluarAplicacionHoy(INICIO, FIN, '2026-08-10').fueraDeRango).toBeFalse();
  });

  it('el ÚLTIMO día de la franja todavía cumple', () => {
    // Frontera que el backend comparte: ese día ProyectarAplicacion no exige motivo.
    expect(evaluarAplicacionHoy(INICIO, FIN, '2026-08-16').fueraDeRango).toBeFalse();
  });

  it('el día siguiente al fin ya es tardío por un día', () => {
    const r = evaluarAplicacionHoy(INICIO, FIN, '2026-08-17');

    expect(r.fueraDeRango).toBeTrue();
    expect(r.diasDesviacion).toBe(1);
    expect(r.mensaje).toContain('1 día');
  });

  it('tardío de varios días: el mensaje va en plural', () => {
    const r = evaluarAplicacionHoy(INICIO, FIN, '2026-08-22');

    expect(r.diasDesviacion).toBe(6);
    expect(r.mensaje).toContain('6 días');
  });

  it('antes de que abra la franja, la desviación es negativa', () => {
    const r = evaluarAplicacionHoy(INICIO, FIN, '2026-08-07');

    expect(r.fueraDeRango).toBeTrue();
    expect(r.diasDesviacion).toBe(-3);
    expect(r.mensaje).toContain('abre en 3 días');
  });

  it('sin franja no inventa una exigencia', () => {
    expect(evaluarAplicacionHoy(null, FIN).fueraDeRango).toBeFalse();
    expect(evaluarAplicacionHoy(INICIO, undefined).fueraDeRango).toBeFalse();
  });

  it('ignora el sufijo horario de la fecha del API', () => {
    const conZ = evaluarAplicacionHoy('2026-08-10T00:00:00Z', '2026-08-16T00:00:00Z', '2026-08-17');
    const sinZ = evaluarAplicacionHoy(INICIO, FIN, '2026-08-17');

    expect(conZ).toEqual(sinZ);
  });

  it('cruza el cambio de mes sin errores de conteo', () => {
    const r = evaluarAplicacionHoy('2026-08-28T00:00:00', '2026-08-31T00:00:00', '2026-09-02');

    expect(r.diasDesviacion).toBe(2);
  });
});

describe('hoyEnBaseDelServidor', () => {
  it('usa el día UTC, no el local', () => {
    // 20:30 del 17-ago en Bogotá (UTC−5) ya es el 18-ago en UTC: el servidor sella el 18 y la UI
    // tiene que decir lo mismo. Con la fecha local, este caso rompía y el usuario veía un 400.
    const nocheEnBogota = new Date('2026-08-18T01:30:00Z');

    expect(hoyEnBaseDelServidor(nocheEnBogota)).toBe('2026-08-18');
  });

  it('devuelve el formato que espera evaluarAplicacionHoy', () => {
    expect(hoyEnBaseDelServidor(new Date('2026-01-05T12:00:00Z'))).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});

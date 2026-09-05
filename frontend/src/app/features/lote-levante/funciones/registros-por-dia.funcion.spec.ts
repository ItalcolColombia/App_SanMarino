import { posicionesEnElDia } from './registros-por-dia.funcion';

/**
 * Contrato de `posicionesEnElDia`, que es lo que evita que la grilla de registros diarios muestre
 * la misma fecha/semana/edad dos veces cuando la empresa tiene
 * `permite_multiples_seguimientos_diarios` (Santa Reyes) — y que el resumen de inventario del día
 * se sume una vez por registro en vez de una vez por día.
 *
 * El caso que más importa es el PRIMERO: con un registro por día —todas las demás empresas— el
 * resultado tiene que ser indistinguible del comportamiento anterior.
 */
describe('posicionesEnElDia', () => {
  it('un registro por día: todas las filas son la primera de su día (delta cero)', () => {
    const r = posicionesEnElDia(['2026-03-01', '2026-03-02', '2026-03-03']);
    expect(r.map(x => x.ordinal)).toEqual([1, 1, 1]);
    expect(r.map(x => x.total)).toEqual([1, 1, 1]);
    expect(r.every(x => x.esPrimero)).toBe(true);
  });

  it('dos registros el mismo día: sólo el primero rotula el día', () => {
    const r = posicionesEnElDia(['2026-03-01', '2026-03-01', '2026-03-02']);
    expect(r.map(x => x.ordinal)).toEqual([1, 2, 1]);
    expect(r.map(x => x.total)).toEqual([2, 2, 1]);
    expect(r.map(x => x.esPrimero)).toEqual([true, false, true]);
  });

  it('el total del día lo conocen TODAS sus filas, también la primera', () => {
    // Se usa para el rótulo «1 de 3»: si el total sólo lo supiera la última, la primera fila no
    // podría avisar que ese día tiene más registros abajo.
    const r = posicionesEnElDia(['2026-03-01', '2026-03-01', '2026-03-01']);
    expect(r.map(x => x.total)).toEqual([3, 3, 3]);
    expect(r.map(x => x.ordinal)).toEqual([1, 2, 3]);
  });

  it('días no consecutivos: cada día cuenta por separado', () => {
    const r = posicionesEnElDia(['2026-03-01', '2026-03-02', '2026-03-01']);
    // La lista llega ordenada en el uso real; igual la función no depende de eso para el total.
    expect(r.map(x => x.total)).toEqual([2, 1, 2]);
    expect(r.map(x => x.ordinal)).toEqual([1, 1, 2]);
  });

  it('una fecha irresoluble no se agrupa con otra: cada null es su propio día', () => {
    const r = posicionesEnElDia([null, null, '2026-03-01']);
    expect(r.map(x => x.ordinal)).toEqual([1, 1, 1]);
    expect(r.map(x => x.total)).toEqual([1, 1, 1]);
    expect(r.every(x => x.esPrimero)).toBe(true);
  });

  it('lista vacía', () => {
    expect(posicionesEnElDia([])).toEqual([]);
  });
});

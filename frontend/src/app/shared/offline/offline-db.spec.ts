import { aplicarMigraciones } from './offline-db';

/**
 * El test que el plan madre exige explícitamente (§5.2): abrir una base en v1, llevarla a vN de un
 * salto, y verificar que corrieron **todos** los pasos intermedios.
 *
 * IndexedDB entrega **un solo** `upgradeneeded` de v1 a v5. El patrón intuitivo —un `if/else if`
 * sobre `oldVersion`, o un `switch` sin fallthrough— ejecuta un solo paso y se saltea los demás en
 * silencio. El dispositivo que hace ese salto es justo el que estuvo meses sin abrir la app, o sea
 * el que nadie tiene a mano para depurar.
 */
describe('aplicarMigraciones (IndexedDB)', () => {
  const dbFalsa = {} as IDBDatabase;

  it('🔴 un salto de v1 a v3 corre los pasos 2 Y 3, no solo uno', () => {
    const corridos: number[] = [];
    const pasos = {
      1: () => corridos.push(1),
      2: () => corridos.push(2),
      3: () => corridos.push(3)
    };

    const aplicados = aplicarMigraciones(dbFalsa, 1, 3, pasos);

    expect(corridos).toEqual([2, 3]);
    expect(aplicados).toEqual([2, 3]);
  });

  it('una base nueva (oldVersion 0) corre TODOS los pasos desde el 1', () => {
    const corridos: number[] = [];
    const pasos = {
      1: () => corridos.push(1),
      2: () => corridos.push(2),
      3: () => corridos.push(3)
    };

    aplicarMigraciones(dbFalsa, 0, 3, pasos);

    expect(corridos).toEqual([1, 2, 3]);
  });

  it('los pasos corren EN ORDEN', () => {
    const corridos: number[] = [];
    const pasos = {
      1: () => corridos.push(1),
      2: () => corridos.push(2),
      3: () => corridos.push(3),
      4: () => corridos.push(4)
    };

    aplicarMigraciones(dbFalsa, 0, 4, pasos);

    expect(corridos).toEqual([1, 2, 3, 4]);
  });

  it('una base ya al día no corre nada', () => {
    const corridos: number[] = [];
    const pasos = { 1: () => corridos.push(1) };

    expect(aplicarMigraciones(dbFalsa, 1, 1, pasos)).toEqual([]);
    expect(corridos).toEqual([]);
  });

  it('una versión sin paso definido no rompe la cadena', () => {
    const corridos: number[] = [];
    const pasos = {
      1: () => corridos.push(1),
      3: () => corridos.push(3) // la v2 no tuvo cambios de esquema
    };

    aplicarMigraciones(dbFalsa, 0, 3, pasos);

    expect(corridos).toEqual([1, 3]);
  });
});

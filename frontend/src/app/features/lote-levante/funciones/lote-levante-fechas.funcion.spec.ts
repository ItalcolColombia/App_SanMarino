import { toYMD } from './lote-levante-fechas.funcion';
import { toYMD as toYMDProduccion } from '../../lote-produccion/funciones/modal-seguimiento-diario-calculos.funcion';
import { toYMD as toYMDEngorde } from '../../engorde-comun/funciones/fecha.funcion';

/**
 * El defecto que fija este spec: el regex de `YYYY-MM-DD` está ANCLADO, así que un ISO con 'T' no
 * matchea y caía al `new Date(s)` con getters LOCALES — que en UTC-5 resta un día. Las filas de la
 * carga masiva viven a 00:00 UTC (su convención legítima), y el modal las abría en el día anterior:
 * la grilla y el modal mostraban días distintos del mismo registro, y guardar devolvía «ya existe
 * otro seguimiento para esa fecha».
 *
 * Las tres copias de `toYMD` tienen que decir lo mismo. Engorde ya estaba bien y es la referencia.
 */
describe('toYMD — misma regla en levante, producción y engorde', () => {
  const copias: ReadonlyArray<readonly [string, (v: string) => string | null]> = [
    ['levante', toYMD],
    ['producción', toYMDProduccion],
    ['engorde', toYMDEngorde],
  ];

  for (const [nombre, fn] of copias) {
    describe(nombre, () => {
      it('un ISO a medianoche UTC conserva su día (era el bug: restaba uno en UTC-5)', () => {
        expect(fn('2026-02-12T00:00:00Z')).toBe('2026-02-12');
      });

      it('un ISO con offset se resuelve por el instante UTC', () => {
        // 2026-02-11 20:00 en UTC-5 es el 12 en UTC.
        expect(fn('2026-02-11T20:00:00-05:00')).toBe('2026-02-12');
      });

      it('un ISO SIN zona se toma literal: la API guarda la fecha digitada tal cual', () => {
        expect(fn('2026-02-12T17:00:00')).toBe('2026-02-12');
      });

      it('un YYYY-MM-DD pelado no se toca', () => {
        expect(fn('2026-02-12')).toBe('2026-02-12');
      });

      it('dd/mm/aaaa sigue resolviéndose como antes', () => {
        expect(fn('25/12/2026')).toBe('2026-12-25');
      });

      it('mm/dd/aaaa sigue resolviéndose como antes', () => {
        expect(fn('12/25/2026')).toBe('2026-12-25');
      });

      it('lo que no es fecha devuelve null', () => {
        expect(fn('no es una fecha')).toBeNull();
      });
    });
  }

  it('las tres copias coinciden en el caso que estaba roto', () => {
    const entrada = '2026-02-12T00:00:00Z';
    const salidas = copias.map(([, fn]) => fn(entrada));

    expect(new Set(salidas).size).toBe(1);
    expect(salidas[0]).toBe('2026-02-12');
  });
});

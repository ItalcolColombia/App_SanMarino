import { buscarRegistroPorId, detalleTotalDia, filasGrillaProduccion, registrosDeLaGrilla } from './filas-grilla-produccion.funcion';
import type { SeguimientoItemDto } from '../services/produccion.service';

const seg = (id: number, extra: Partial<SeguimientoItemDto> = {}): SeguimientoItemDto =>
  ({ id, fechaRegistro: '2026-09-01T12:00:00Z', ...extra } as SeguimientoItemDto);

describe('filasGrillaProduccion', () => {
  it('sin días con varios registros: una fila 1 de 1 por día, con el mismo objeto', () => {
    const a = seg(1);
    const b = seg(2);
    const filas = filasGrillaProduccion([a, b]);

    expect(filas.length).toBe(2);
    expect(filas[0].seg).toBe(a);
    expect(filas.every(f => f.ordinal === 1 && f.total === 1 && f.esPrimero)).toBeTrue();
  });

  it('un día con 2 registros se despliega en 2 filas, solo la primera rotula', () => {
    const r1 = seg(10);
    const r2 = seg(11);
    const dia = seg(10, { mortalidadH: 5, registrosDelDia: [r1, r2] });

    const filas = filasGrillaProduccion([seg(20), dia]);

    expect(filas.map(f => f.seg.id)).toEqual([20, 10, 11]);
    expect(filas[1]).toEqual({ seg: r1, ordinal: 1, total: 2, esPrimero: true, totalDia: dia });
    expect(filas[2]).toEqual({ seg: r2, ordinal: 2, total: 2, esPrimero: false, totalDia: null });
  });

  // Santa Reyes, 18-sep-2026 (Galpón 4): la primera línea del día decía «huevos 0» aunque el 2.º, el 3.º y el 4.º
  // registro traían los 7.010, y el operario repitió la carga. El total del día se cuelga SOLO de esa primera línea.
  describe('totalDia (la primera línea del día dice cuántos huevos hay)', () => {
    it('el caso real: 4 registros, el 1.º sin huevos → la primera fila lleva el total del día (21.030 huevos)', () => {
      const registros = [
        seg(692, { huevosTotales: 0, mortalidadH: 15 }),
        seg(693, { huevosTotales: 7010 }),
        seg(694, { huevosTotales: 7010 }),
        seg(695, { huevosTotales: 7010 })
      ];
      const dia = seg(692, { huevosTotales: 21030, mortalidadH: 15, registrosDelDia: registros });

      const filas = filasGrillaProduccion([dia]);

      expect(filas.map(f => f.totalDia)).toEqual([dia, null, null, null]);
      expect(filas[0].seg.huevosTotales).toBe(0);          // la fila sigue mostrando SU registro
      expect(filas[0].totalDia!.huevosTotales).toBe(21030);
    });

    it('un día con un solo registro no lleva total del día (la tabla queda como siempre)', () => {
      expect(filasGrillaProduccion([seg(1), seg(2)]).every(f => f.totalDia === null)).toBeTrue();
      expect(filasGrillaProduccion([seg(10, { registrosDelDia: [seg(10)] })])[0].totalDia).toBeNull();
    });
  });

  describe('detalleTotalDia', () => {
    it('lista solo lo que trae valor y dice cuántos registros suma', () => {
      const dia = seg(1, { mortalidadH: 15, selH: 0, consKgH: 1215, registrosDelDia: [seg(1), seg(2), seg(3)] });
      expect(detalleTotalDia(dia, false)).toBe('Suma de los 3 registros del día: mortalidad 15 · consumo 1215 kg');
    });

    it('sin cifras de aves ni consumo queda solo el encabezado', () => {
      const dia = seg(1, { registrosDelDia: [seg(1), seg(2)] });
      expect(detalleTotalDia(dia, false)).toBe('Suma de los 2 registros del día');
    });

    it('los machos solo entran si la empresa los maneja', () => {
      const dia = seg(1, { mortalidadH: 3, mortalidadM: 2, consKgH: 10, consKgM: 5, registrosDelDia: [seg(1), seg(2)] });
      expect(detalleTotalDia(dia, false)).toContain('mortalidad 3 ·');
      expect(detalleTotalDia(dia, true)).toBe('Suma de los 2 registros del día: mortalidad 5 · consumo 15 kg');
    });
  });

  it('registrosDelDia con un solo elemento se trata como día normal', () => {
    const dia = seg(10, { registrosDelDia: [seg(10)] });
    const filas = filasGrillaProduccion([dia]);
    expect(filas.length).toBe(1);
    expect(filas[0].seg).toBe(dia);
  });

  it('null o vacío ⇒ sin filas', () => {
    expect(filasGrillaProduccion(null)).toEqual([]);
    expect(filasGrillaProduccion([])).toEqual([]);
  });

  describe('buscarRegistroPorId', () => {
    it('sin días desplegados es un find por id', () => {
      const a = seg(1);
      expect(buscarRegistroPorId([a, seg(2)], 1)).toBe(a);
      expect(buscarRegistroPorId([a], 99)).toBeUndefined();
      expect(buscarRegistroPorId([a], null)).toBeUndefined();
    });

    it('encuentra el 2.º registro de un día, que no está en la lista de días', () => {
      const r2 = seg(11, { mortalidadH: 7 });
      const dia = seg(10, { mortalidadH: 12, registrosDelDia: [seg(10, { mortalidadH: 5 }), r2] });
      expect(buscarRegistroPorId([dia], 11)).toBe(r2);
    });

    it('para el 1.º devuelve el REGISTRO, no la fila agrupada que comparte su id', () => {
      const r1 = seg(10, { mortalidadH: 5 });
      const dia = seg(10, { mortalidadH: 12, registrosDelDia: [r1, seg(11, { mortalidadH: 7 })] });
      const encontrado = buscarRegistroPorId([dia], 10);
      expect(encontrado).toBe(r1);
      expect(encontrado?.mortalidadH).toBe(5);
    });
  });

  it('registrosDeLaGrilla devuelve los registros que pinta la tabla', () => {
    const dia = seg(10, { registrosDelDia: [seg(10), seg(11)] });
    expect(registrosDeLaGrilla(filasGrillaProduccion([dia])).map(s => s.id)).toEqual([10, 11]);
  });
});

import { buscarRegistroPorId, filasGrillaProduccion, registrosDeLaGrilla } from './filas-grilla-produccion.funcion';
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
    expect(filas[1]).toEqual({ seg: r1, ordinal: 1, total: 2, esPrimero: true });
    expect(filas[2]).toEqual({ seg: r2, ordinal: 2, total: 2, esPrimero: false });
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

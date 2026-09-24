import {
  agruparMovimientosAlimentoPorDia,
  movimientosSinSeguimiento
} from './movimientos-alimento-seguimiento.funcion';
import { MovimientoAlimentoSeguimientoDto } from '../models/movimiento-alimento-seguimiento.model';

describe('movimientos alimento del seguimiento', () => {
  const movimiento = (
    id: number,
    fecha: string,
    tipoMovimiento: string,
    referencia: string
  ): MovimientoAlimentoSeguimientoDto => ({
    id,
    fecha,
    tipoMovimiento,
    cantidadKg: 100,
    alimento: 'ALI-01 — Alimento postura',
    referencia
  });

  it('agrupa por fecha sin netear traslado entrada y salida', () => {
    const dias = agruparMovimientosAlimentoPorDia([
      movimiento(1, '2026-09-22T12:00:00Z', 'INV_INGRESO', 'GUIA-1'),
      movimiento(2, '2026-09-22T12:00:00Z', 'INV_TRASLADO_ENTRADA', 'GUIA-2'),
      movimiento(3, '2026-09-22T12:00:00Z', 'INV_TRASLADO_SALIDA', 'GUIA-3')
    ]);

    expect(dias.length).toBe(1);
    expect(dias[0].ingresos.map(x => x.id)).toEqual([1]);
    expect(dias[0].traslados.map(x => x.id)).toEqual([2, 3]);
  });

  it('deduplica referencias conservando su orden', () => {
    const dias = agruparMovimientosAlimentoPorDia([
      movimiento(1, '2026-09-22', 'INV_INGRESO', 'GUIA-1'),
      movimiento(2, '2026-09-22', 'INV_INGRESO', 'GUIA-1'),
      movimiento(3, '2026-09-22', 'INV_TRASLADO_ENTRADA', 'GUIA-2')
    ]);

    expect(dias[0].referencias).toEqual(['GUIA-1', 'GUIA-2']);
  });

  it('identifica fechas con movimiento aunque no exista seguimiento diario', () => {
    const dias = agruparMovimientosAlimentoPorDia([
      movimiento(1, '2026-09-21', 'INV_INGRESO', 'A'),
      movimiento(2, '2026-09-22', 'INV_INGRESO', 'B')
    ]);

    expect(movimientosSinSeguimiento(dias, ['2026-09-21T12:00:00Z']).map(x => x.fecha))
      .toEqual(['2026-09-22']);
  });
});

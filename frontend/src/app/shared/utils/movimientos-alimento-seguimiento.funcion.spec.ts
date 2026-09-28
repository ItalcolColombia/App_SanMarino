import {
  indexarMovimientosAlimentoPorDia,
  movimientosSinSeguimiento
} from './movimientos-alimento-seguimiento.funcion';
import { ResumenMovimientosAlimentoDia } from '../models/movimiento-alimento-seguimiento.model';

describe('movimientos alimento del seguimiento', () => {
  const dias: ResumenMovimientosAlimentoDia[] = [
    { fecha: '2026-09-21', ingresos: [], traslados: [], referencias: ['A'] },
    { fecha: '2026-09-22', ingresos: [], traslados: [], referencias: ['B'] }
  ];

  it('indexa sin reagrupar el contrato diario que entrega PostgreSQL', () => {
    const indice = indexarMovimientosAlimentoPorDia(dias);
    expect(indice.get('2026-09-22')?.referencias).toEqual(['B']);
  });

  it('identifica fechas con movimiento aunque no exista seguimiento diario', () => {
    expect(movimientosSinSeguimiento(dias, ['2026-09-21T12:00:00Z']).map(x => x.fecha))
      .toEqual(['2026-09-22']);
  });
});

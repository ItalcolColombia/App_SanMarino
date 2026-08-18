import { separarStockPorUbicacion } from './separar-stock-por-ubicacion.funcion';

/**
 * El aviso de alimento del modal de liquidación no puede contar kilos de otros galpones: es el único
 * detector de «lote liquidado con alimento pendiente» que hoy ve un humano, y mentía en 15 lotes.
 */
describe('separarStockPorUbicacion', () => {
  const fila = (galponId: string | null, quantity: number) => ({ galponId, quantity });

  it('las filas del mismo galpón son del lote', () => {
    const r = separarStockPorUbicacion([fila('G0025', 100)], 'G0025');
    expect(r.propias.length).toBe(1);
    expect(r.kgPropias).toBe(100);
    expect(r.ajenas.length).toBe(0);
  });

  it('🔑 las filas de OTRO galpón son ajenas y no suman', () => {
    const r = separarStockPorUbicacion([fila('G0025', 19160)], 'G0031');
    expect(r.propias.length).toBe(0);
    expect(r.kgPropias).toBe(0);
    expect(r.kgAjenas).toBe(19160);
  });

  it('la fila SIN galpón es stock de nivel núcleo/granja ⇒ es del lote', () => {
    const r = separarStockPorUbicacion([fila(null, 500), fila('', 300)], 'G0031');
    expect(r.propias.length).toBe(2);
    expect(r.kgPropias).toBe(800);
  });

  it('lote sin galpón ⇒ todo lo consultado es suyo', () => {
    const r = separarStockPorUbicacion([fila('G0025', 100), fila(null, 50)], null);
    expect(r.propias.length).toBe(2);
    expect(r.kgPropias).toBe(150);
    expect(r.ajenas.length).toBe(0);
  });

  it('mezcla: separa y suma cada lado', () => {
    const r = separarStockPorUbicacion(
      [fila('G0031', 4160), fila('G0025', 9000), fila(' G0031 ', 40), fila(null, 10)],
      'G0031'
    );
    expect(r.kgPropias).toBe(4210);
    expect(r.kgAjenas).toBe(9000);
  });

  it('cantidades nulas o no numéricas no rompen la suma', () => {
    const r = separarStockPorUbicacion(
      [{ galponId: 'G0031', quantity: null }, { galponId: 'G0031', quantity: undefined }],
      'G0031'
    );
    expect(r.kgPropias).toBe(0);
  });

  it('lista vacía o nula devuelve todo en cero', () => {
    expect(separarStockPorUbicacion([], 'G0031').kgPropias).toBe(0);
    expect(separarStockPorUbicacion(null, 'G0031').propias.length).toBe(0);
    expect(separarStockPorUbicacion(undefined, null).ajenas.length).toBe(0);
  });
});

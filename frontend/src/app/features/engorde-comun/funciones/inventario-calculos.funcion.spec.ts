import { toKg, KG_POR_QUINTAL, toNumOrNull } from './inventario-calculos.funcion';
import { construirItemsSeguimiento } from './mapear-seguimiento-dto.funcion';

describe('inventario-calculos: toKg (incluye qq)', () => {
  it('kg se conserva tal cual', () => {
    expect(toKg(12.5, 'kg')).toBe(12.5);
    expect(toKg(0, 'kg')).toBe(0);
  });

  it('g/gramos → /1000 (sin cambios de comportamiento)', () => {
    expect(toKg(1000, 'g')).toBe(1);
    expect(toKg(250, 'gramos')).toBe(0.25);
  });

  it('unidad ausente/desconocida se asume kg', () => {
    expect(toKg(7, null)).toBe(7);
    expect(toKg(7, undefined)).toBe(7);
    expect(toKg(7, 'unidades')).toBe(7);
  });

  it('qq/quintal/quintales → × 45.36', () => {
    expect(KG_POR_QUINTAL).toBe(45.36);
    expect(toKg(1, 'qq')).toBe(45.36);
    expect(toKg(1, 'QQ')).toBe(45.36);
    expect(toKg(0, 'qq')).toBe(0);
    expect(toKg(2.5, 'quintal')).toBeCloseTo(113.4, 6);
    expect(toKg(3, 'quintales')).toBeCloseTo(136.08, 6);
  });
});

describe('construirItemsSeguimiento: normaliza qq → kg antes de enviar', () => {
  const opts = { forzarAlimento: true, isEcuadorOrPanama: true };

  it('qq se convierte a kg y viaja como unidad kg (consumo siempre en kg)', () => {
    const out = construirItemsSeguimiento([{ catalogItemId: 5, cantidad: 2, unidad: 'qq' }], opts);
    expect(out.length).toBe(1);
    expect(out[0].unidad).toBe('kg');
    expect(out[0].cantidad).toBeCloseTo(90.72, 6); // 2 × 45.36
    expect(out[0].catalogItemId).toBe(5);
    expect((out[0] as any).itemInventarioEcuadorId).toBe(5);
  });

  it('kg viaja sin cambios (comportamiento previo intacto)', () => {
    const out = construirItemsSeguimiento([{ catalogItemId: 8, cantidad: 40, unidad: 'kg' }], opts);
    expect(out[0].unidad).toBe('kg');
    expect(out[0].cantidad).toBe(40);
  });

  it('g viaja sin cambios (el backend hace g→kg)', () => {
    const out = construirItemsSeguimiento([{ catalogItemId: 9, cantidad: 500, unidad: 'g' }], opts);
    expect(out[0].unidad).toBe('g');
    expect(out[0].cantidad).toBe(500);
  });

  it('cantidad 0 o sin ítem se descarta', () => {
    const out = construirItemsSeguimiento(
      [
        { catalogItemId: 5, cantidad: 0, unidad: 'qq' },
        { catalogItemId: null, cantidad: 3, unidad: 'qq' }
      ],
      opts
    );
    expect(out.length).toBe(0);
  });
});

describe('toNumOrNull (sanity)', () => {
  it('vacío → null; numérico → number', () => {
    expect(toNumOrNull('')).toBeNull();
    expect(toNumOrNull(null)).toBeNull();
    expect(toNumOrNull('3.5')).toBe(3.5);
  });
});

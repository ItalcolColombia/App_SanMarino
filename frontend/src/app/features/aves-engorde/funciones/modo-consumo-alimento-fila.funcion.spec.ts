import {
  esConsumoAlimentoMixto,
  esConsumoAlimentoPorGenero,
  modoConsumoAlimentoFila,
  USUARIO_CRUCE_REPRODUCTORA
} from './modo-consumo-alimento-fila.funcion';

/**
 * Equivocarse acá NO mueve ningún kg, pero sí rotula mal el alimento: es exactamente el defecto que
 * se está corrigiendo (la ración mixta del día 8 en adelante mostrada bajo «Consumo hembras»).
 * Los casos salen de datos reales del lote «94 - 2» (id 163, ItalcolPanama).
 */
describe('modoConsumoAlimentoFila', () => {
  it('fila del cruce reproductora con H y M > 0 → género (días 1–7)', () => {
    const f = { createdByUserId: USUARIO_CRUCE_REPRODUCTORA, consumoKgMachos: 362.878 };

    expect(modoConsumoAlimentoFila(f)).toBe('genero');
    expect(esConsumoAlimentoPorGenero(f)).toBeTrue();
  });

  it('fila del cruce con machos en 0 sigue siendo género — manda el autor, no el número', () => {
    // 8 de las 203 filas de cruce tienen consumo de machos 0; rotularlas «mixto» sería falso.
    expect(modoConsumoAlimentoFila({ createdByUserId: USUARIO_CRUCE_REPRODUCTORA, consumoKgMachos: 0 }))
      .toBe('genero');
  });

  it('fila del módulo en Panamá (H = 1905,108 · M = 0) → mixto', () => {
    const f = { createdByUserId: '4efb520a-fbd9-43c8-addf-5beeabfe596c', consumoKgMachos: 0 };

    expect(modoConsumoAlimentoFila(f)).toBe('mixto');
    expect(esConsumoAlimentoMixto(f)).toBeTrue();
  });

  it('fila de Ecuador sin consumo de machos → mixto', () => {
    expect(modoConsumoAlimentoFila({ createdByUserId: '1757918108', consumoKgMachos: 0 })).toBe('mixto');
  });

  it('fila de Ecuador con consumo de machos > 0 conserva el desglose', () => {
    expect(modoConsumoAlimentoFila({ createdByUserId: '2e317035-0fe5-4007-94a1-d87de7d7cb59', consumoKgMachos: 12.5 }))
      .toBe('genero');
  });

  it('fila de movimiento sin seguimiento (sin autor, todo en 0) → mixto', () => {
    expect(modoConsumoAlimentoFila({ createdByUserId: null, consumoKgMachos: null })).toBe('mixto');
    expect(modoConsumoAlimentoFila({})).toBe('mixto');
  });

  it('el autor se compara sin espacios y respetando el literal en mayúsculas', () => {
    expect(modoConsumoAlimentoFila({ createdByUserId: '  SYSTEM_CRUCE  ', consumoKgMachos: 0 })).toBe('genero');
    expect(modoConsumoAlimentoFila({ createdByUserId: 'system_cruce', consumoKgMachos: 0 })).toBe('mixto');
  });
});

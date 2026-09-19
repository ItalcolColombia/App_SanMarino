import {
  consumoKgDelGuardado,
  huevosDelGuardado,
  resumirGuardadoSeguimiento,
  type SeguimientoGuardadoResumible
} from './resumen-guardado-seguimiento.funcion';

/**
 * Santa Reyes, 18-sep-2026: el operario cargó tres veces los mismos 7.010 huevos porque nada le confirmaba que la
 * primera había entrado. El aviso de éxito ahora repite lo que el servidor aceptó.
 */
describe('resumirGuardadoSeguimiento', () => {
  const base: SeguimientoGuardadoResumible = { mortalidadH: 0, mortalidadM: 0, selH: 0, selM: 0, huevosTotales: 0 };

  it('el caso real del Galpón 4: solo huevos por ítems (4 tipos) → dice los 7010', () => {
    const texto = resumirGuardadoSeguimiento({
      ...base,
      huevoItems: [{ cantidad: 6600 }, { cantidad: 250 }, { cantidad: 100 }, { cantidad: 60 }]
    }, false);

    expect(texto).toBe('Seguimiento creado: huevos 7010.');
  });

  it('mortalidad + consumo por ítems de alimento + huevos, en ese orden de importancia para el operario', () => {
    const texto = resumirGuardadoSeguimiento({
      ...base,
      mortalidadH: 15,
      huevoItems: [{ cantidad: 7010 }],
      itemsHembras: [{ tipoItem: 'alimento', cantidad: 1215, unidad: 'kg' }]
    }, false);

    expect(texto).toBe('Seguimiento creado: huevos 7010 · mortalidad 15 · consumo 1215 kg.');
  });

  it('solo se nombra lo que trae valor', () => {
    expect(resumirGuardadoSeguimiento({ ...base, mortalidadH: 5 }, false)).toBe('Seguimiento creado: mortalidad 5.');
    expect(resumirGuardadoSeguimiento({ ...base, selH: 2 }, false)).toBe('Seguimiento creado: selección 2.');
  });

  it('mortalidad y selección suman hembras y machos', () => {
    const texto = resumirGuardadoSeguimiento({ ...base, mortalidadH: 3, mortalidadM: 2, selH: 1, selM: 1 }, false);
    expect(texto).toBe('Seguimiento creado: mortalidad 5 · selección 2.');
  });

  it('un registro vacío (captura parcial) lo dice, no queda mudo', () => {
    expect(resumirGuardadoSeguimiento(base, false)).toBe('Seguimiento creado (sin huevos, mortalidad ni consumo).');
  });

  it('edición: cambia el título, no las cifras', () => {
    expect(resumirGuardadoSeguimiento({ ...base, huevoItems: [{ cantidad: 100 }] }, true))
      .toBe('Seguimiento actualizado: huevos 100.');
  });

  describe('huevos', () => {
    it('con ítems manda la suma de ítems, aunque `huevosTotales` venga en 0 (Santa Reyes lo manda en 0)', () => {
      expect(huevosDelGuardado({ huevosTotales: 0, huevoItems: [{ cantidad: 10 }, { cantidad: 5 }] })).toBe(15);
    });

    it('sin ítems (11 categorías fijas) usa `huevosTotales`', () => {
      expect(huevosDelGuardado({ huevosTotales: 4120, huevoItems: undefined })).toBe(4120);
      expect(huevosDelGuardado({ huevosTotales: 4120, huevoItems: [] })).toBe(4120);
    });

    it('ignora cantidades negativas, NaN y nulas', () => {
      expect(huevosDelGuardado({ huevoItems: [{ cantidad: -5 }, { cantidad: NaN }, { cantidad: null }, { cantidad: 8 }] })).toBe(8);
    });
  });

  describe('consumo (kg)', () => {
    it('con ítems suma SOLO los de alimento: un medicamento no es consumo de alimento', () => {
      const kg = consumoKgDelGuardado({
        itemsHembras: [
          { tipoItem: 'alimento', cantidad: 100, unidad: 'kg' },
          { tipoItem: 'medicamento', cantidad: 50, unidad: 'kg' }
        ]
      });
      expect(kg).toBe(100);
    });

    it('suma hembras y machos, y convierte gramos a kilos', () => {
      const kg = consumoKgDelGuardado({
        itemsHembras: [{ tipoItem: 'alimento', cantidad: 500, unidad: 'kg' }],
        itemsMachos: [{ tipoItem: 'Alimento', cantidad: 2500, unidad: 'g' }]
      });
      expect(kg).toBe(502.5);
    });

    it('sin ítems usa el consumo escalar con su unidad (kg por defecto, g convertido)', () => {
      expect(consumoKgDelGuardado({ consumoH: 1200 })).toBe(1200);
      expect(consumoKgDelGuardado({ consumoH: 800, unidadConsumoH: 'g', consumoM: 1, unidadConsumoM: 'kg' })).toBe(1.8);
    });

    it('decimales: hasta dos, sin ceros de relleno', () => {
      const texto = resumirGuardadoSeguimiento({
        ...base,
        itemsHembras: [{ tipoItem: 'alimento', cantidad: 12.5, unidad: 'kg' }]
      }, false);
      expect(texto).toBe('Seguimiento creado: consumo 12.5 kg.');
    });
  });
});

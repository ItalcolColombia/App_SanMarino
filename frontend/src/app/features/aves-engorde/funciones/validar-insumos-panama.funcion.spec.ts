import { validarInsumosEnterosPanama } from './validar-insumos-panama.funcion';

/**
 * El backend rechaza un decimal en un campo `int` con un 400 sin mensaje utilizable — este validador
 * debe atajarlo ANTES del viaje al servidor (lote 13-1, ItalcolPanama, 26-ago-2026).
 */
describe('validarInsumosEnterosPanama', () => {
  const base = { diasEnGranja: 42, diasEngorde: 40, avesFinalGranja: 24046, avesBeneficiada: 24000 };

  it('4 enteros válidos ⇒ null (nada que mostrar)', () => {
    expect(validarInsumosEnterosPanama(base)).toBeNull();
  });

  it('🔑 decimal en avesFinalGranja ⇒ mensaje nombrando el campo', () => {
    const msg = validarInsumosEnterosPanama({ ...base, avesFinalGranja: 24046.5 });
    expect(msg).toContain('Aves Finales en Granja');
    expect(msg).toContain('entero');
  });

  it('decimal en avesBeneficiada ⇒ mensaje nombrando ESE campo', () => {
    const msg = validarInsumosEnterosPanama({ ...base, avesBeneficiada: 100.25 });
    expect(msg).toContain('Aves Beneficiadas');
  });

  it('decimal en diasEnGranja o diasEngorde también se detecta', () => {
    expect(validarInsumosEnterosPanama({ ...base, diasEnGranja: 42.5 })).toContain('Días en Granja');
    expect(validarInsumosEnterosPanama({ ...base, diasEngorde: 40.1 })).toContain('Días de Engorde');
  });

  it('null en un campo no dispara el mensaje (lo cubre panamaCamposCompletos, no este validador)', () => {
    expect(validarInsumosEnterosPanama({ ...base, avesFinalGranja: null })).toBeNull();
  });
});

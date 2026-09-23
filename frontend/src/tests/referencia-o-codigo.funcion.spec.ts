/**
 * Código de referencia ERP vs código interno (`fase_de_desarrollo/catalogo_items_santa_reyes_erp_plan.md`):
 * el selector de alimento debe mostrar la referencia del ERP cuando existe, y el código interno de
 * siempre cuando no (empresas sin código de referencia no ven ningún cambio).
 */
import { referenciaOCodigo } from '../app/shared/utils/format';

describe('referenciaOCodigo', () => {
  it('prefiere la referencia cuando está presente', () => {
    expect(referenciaOCodigo('1005', '1918')).toBe('1005');
  });

  it('cae al código interno cuando no hay referencia (empresa sin ERP, sin cambio visible)', () => {
    expect(referenciaOCodigo(null, 'ALI001')).toBe('ALI001');
    expect(referenciaOCodigo(undefined, 'ALI001')).toBe('ALI001');
    expect(referenciaOCodigo('', 'ALI001')).toBe('ALI001');
    expect(referenciaOCodigo('   ', 'ALI001')).toBe('ALI001');
  });

  it('recorta espacios de la referencia', () => {
    expect(referenciaOCodigo('  1005  ', '1918')).toBe('1005');
  });

  it('sin código ni referencia devuelve vacío', () => {
    expect(referenciaOCodigo(null, null)).toBe('');
    expect(referenciaOCodigo(undefined, undefined)).toBe('');
  });
});

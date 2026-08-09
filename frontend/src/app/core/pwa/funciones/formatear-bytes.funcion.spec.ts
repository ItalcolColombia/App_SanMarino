import { formatearBytes } from './formatear-bytes.funcion';

describe('formatearBytes', () => {
  it('devuelve "0 B" para cero', () => {
    expect(formatearBytes(0)).toBe('0 B');
  });

  it('no pone decimales en los bytes crudos', () => {
    expect(formatearBytes(512)).toBe('512 B');
  });

  it('usa unidades binarias (1024), que es lo que reporta storage.estimate()', () => {
    expect(formatearBytes(1024)).toBe('1.0 KB');
    expect(formatearBytes(1024 * 1024)).toBe('1.0 MB');
    expect(formatearBytes(1024 * 1024 * 1024)).toBe('1.0 GB');
  });

  it('respeta la cantidad de decimales pedida', () => {
    expect(formatearBytes(1536, 2)).toBe('1.50 KB');
  });

  it('devuelve un guión ante null/undefined/NaN, NO "0 B"', () => {
    // El navegador no siempre expone la cuota. Un "0 B" ahí se leería como
    // "no hay nada guardado", que es un diagnóstico distinto y equivocado.
    expect(formatearBytes(undefined)).toBe('—');
    expect(formatearBytes(null)).toBe('—');
    expect(formatearBytes(NaN)).toBe('—');
  });

  it('no inventa unidades más allá de TB', () => {
    const enorme = Math.pow(1024, 6);
    expect(formatearBytes(enorme)).toContain('TB');
  });
});

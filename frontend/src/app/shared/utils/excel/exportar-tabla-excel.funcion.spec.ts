import { construirAoaExcel, nombreArchivoXlsx } from './exportar-tabla-excel.funcion';
import { dateStampCompact, sanitizeFileName, formatearNumero, fechaCorta } from '../format';

describe('exportar-tabla-excel (funciones puras)', () => {
  const headers = ['A', 'B'];
  const rows = [
    [1, 'x'],
    [2, 'y'],
  ];

  it('construirAoaExcel sin título ni subtítulos = headers + filas', () => {
    expect(construirAoaExcel(headers, rows)).toEqual([
      ['A', 'B'],
      [1, 'x'],
      [2, 'y'],
    ]);
  });

  it('construirAoaExcel con título + subtítulos = [title],[sub...],[],headers,...rows (patrón canónico)', () => {
    const aoa = construirAoaExcel(headers, rows, 'Título', ['Filtros: uno · dos']);
    expect(aoa).toEqual([
      ['Título'],
      ['Filtros: uno · dos'],
      [],
      ['A', 'B'],
      [1, 'x'],
      [2, 'y'],
    ]);
  });

  it('nombreArchivoXlsx sanitiza y agrega sello YYYYMMDD por defecto', () => {
    const name = nombreArchivoXlsx({ filenameBase: 'Venta a/b:c' });
    expect(name).toBe(`Venta a_b_c_${dateStampCompact()}.xlsx`);
  });

  it('nombreArchivoXlsx respeta stamp:false', () => {
    expect(nombreArchivoXlsx({ filenameBase: 'reporte', stamp: false })).toBe('reporte.xlsx');
  });
});

describe('format (helpers puros)', () => {
  it('sanitizeFileName quita caracteres inválidos', () => {
    expect(sanitizeFileName('a\\b/c:d*e?f"g<h>i|j')).toBe('a_b_c_d_e_f_g_h_i_j');
  });

  it('dateStampCompact devuelve YYYYMMDD con padding', () => {
    expect(dateStampCompact(new Date(2026, 0, 5))).toBe('20260105');
  });

  it('formatearNumero usa separador de miles es-CO', () => {
    expect(formatearNumero(1234567)).toBe('1.234.567');
  });

  it('fechaCorta devuelve — sin valor', () => {
    expect(fechaCorta(null)).toBe('—');
    expect(fechaCorta('')).toBe('—');
  });
});

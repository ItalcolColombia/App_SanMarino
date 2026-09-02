import { fechaTrasladoHistorialLote } from './fecha-traslado-historial-lote.funcion';

describe('fechaTrasladoHistorialLote', () => {
  it('muestra el dia del traslado, no el de digitacion', () => {
    // Movido el 25-ago, cargado el 1-sep: la columna dice 25.
    expect(fechaTrasladoHistorialLote({
      fechaTraslado: '2026-08-25',
      createdAt: '2026-09-01T14:30:00Z'
    })).toBe('25/8/2026');
  });

  it('no corre la fecha pura un dia hacia atras', () => {
    // new Date("2026-09-01") es medianoche UTC: formateada en local (UTC-5) daria 31/08.
    expect(fechaTrasladoHistorialLote({ fechaTraslado: '2026-09-01' })).toBe('1/9/2026');
  });

  it('cae a createdAt cuando la fila es anterior a la columna', () => {
    expect(fechaTrasladoHistorialLote({
      fechaTraslado: null,
      createdAt: '2026-07-14T10:00:00Z'
    })).not.toBe('—');
  });

  it('devuelve el guion solo cuando no hay ninguna fecha', () => {
    expect(fechaTrasladoHistorialLote({ fechaTraslado: null, createdAt: null })).toBe('—');
    expect(fechaTrasladoHistorialLote(null)).toBe('—');
    expect(fechaTrasladoHistorialLote(undefined)).toBe('—');
  });
});

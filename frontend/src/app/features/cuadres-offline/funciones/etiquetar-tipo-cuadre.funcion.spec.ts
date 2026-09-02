import { etiquetarTipoCuadre } from './etiquetar-tipo-cuadre.funcion';

describe('etiquetarTipoCuadre', () => {
  it('traduce todos los tipos del contrato de sync', () => {
    expect(etiquetarTipoCuadre('seguimiento_levante_crear')).toBe('Seguimiento diario · levante');
    expect(etiquetarTipoCuadre('seguimiento_produccion_crear')).toBe('Seguimiento diario · producción');
    expect(etiquetarTipoCuadre('seguimiento_engorde_crear')).toBe('Seguimiento diario · pollo engorde');
    expect(etiquetarTipoCuadre('seguimiento_reproductora_engorde_crear'))
      .toBe('Seguimiento diario · reproductora engorde');
    // H4. Faltaba, y se vio recien al abrir la bandeja con datos reales: la columna Origen mostraba
    // `gasto_inventario_crear` crudo. No rompia nada -- ese es el fallback -- pero se lee mal.
    expect(etiquetarTipoCuadre('gasto_inventario_crear')).toBe('Gasto de inventario');
  });

  it('devuelve el identificador CRUDO cuando el tipo no se conoce', () => {
    // Un servidor más nuevo puede mandar un tipo que este cliente no tiene mapeado. Mostrarlo tal
    // cual deja al supervisor con algo que reportar; «Desconocido» no.
    expect(etiquetarTipoCuadre('venta_engorde_crear')).toBe('venta_engorde_crear');
    expect(etiquetarTipoCuadre('lo_que_sea')).toBe('lo_que_sea');
  });

  it('nunca devuelve undefined ni la cadena "undefined"', () => {
    for (const entrada of ['seguimiento_levante_crear', 'inventado', '']) {
      const salida = etiquetarTipoCuadre(entrada);
      expect(typeof salida).toBe('string');
      expect(salida).not.toBe('undefined');
    }
  });

  it('sin tipo devuelve un guion, no una etiqueta inventada', () => {
    expect(etiquetarTipoCuadre(null)).toBe('—');
    expect(etiquetarTipoCuadre(undefined)).toBe('—');
    expect(etiquetarTipoCuadre('')).toBe('—');
  });
});

// src/app/features/lote/funciones/anos-guia-genetica-error.funcion.spec.ts
import { mensajeErrorAnosGuiaGenetica } from './anos-guia-genetica-error.funcion';

describe('mensajeErrorAnosGuiaGenetica', () => {
  it('conserva el motivo que ya tradujo GuiaGeneticaService.handleError', () => {
    // El caso que motivó el fix: sesión vencida en plena capacitación. El servicio ya sabe decir
    // qué pasó; el componente lo tiraba y culpaba a la raza.
    const msg = mensajeErrorAnosGuiaGenetica(new Error('No autorizado. Inicie sesión nuevamente'));

    expect(msg).toBe('No se pudo consultar la guía genética: No autorizado. Inicie sesión nuevamente');
  });

  it('nunca afirma que la raza no tiene años cargados', () => {
    // La invariante del fix: pase lo que pase con la request, este texto no puede acusar a la guía.
    const entradas: unknown[] = [
      new Error('Error interno del servidor'),
      new Error('  '),
      { message: 42 },
      {},
      null,
      undefined
    ];

    for (const entrada of entradas) {
      const msg = mensajeErrorAnosGuiaGenetica(entrada);
      expect(msg).toContain('No se pudo consultar la guía genética');
      expect(msg).not.toContain('No se encontraron años disponibles');
      expect(msg).not.toContain('no tiene años de tabla genética');
    }
  });

  it('cae a un texto accionable cuando el error no trae mensaje utilizable', () => {
    const esperado =
      'No se pudo consultar la guía genética. Reintente; si el problema persiste, vuelva a iniciar sesión.';

    expect(mensajeErrorAnosGuiaGenetica(null)).toBe(esperado);
    expect(mensajeErrorAnosGuiaGenetica(undefined)).toBe(esperado);
    expect(mensajeErrorAnosGuiaGenetica({})).toBe(esperado);
    expect(mensajeErrorAnosGuiaGenetica(new Error('   '))).toBe(esperado);
  });
});

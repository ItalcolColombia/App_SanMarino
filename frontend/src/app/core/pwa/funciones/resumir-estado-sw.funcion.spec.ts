import { resumirEstadoSw } from './resumir-estado-sw.funcion';

describe('resumirEstadoSw', () => {
  it('avisa cuando el navegador no soporta Service Worker', () => {
    const estado = resumirEstadoSw({ soportado: false, registrado: false, controlando: false });

    expect(estado.severidad).toBe('aviso');
    expect(estado.etiqueta).toContain('No soportado');
  });

  it('avisa cuando no hay registro (build de desarrollo)', () => {
    const estado = resumirEstadoSw({ soportado: true, registrado: false, controlando: false });

    expect(estado.severidad).toBe('aviso');
  });

  it('marca OK cuando el SW controla la app', () => {
    const estado = resumirEstadoSw({ soportado: true, registrado: true, controlando: true });

    expect(estado.severidad).toBe('ok');
    expect(estado.etiqueta).toContain('controlando');
  });

  // --- El par de casos que justifica que esta función exista --------------------
  // La MISMA combinación de booleanos significa cosas opuestas según sea o no el
  // primer load. Sin distinguirlos, o se alarma de más en la primera visita o no se
  // detecta nunca el safe mode.

  it('en el PRIMER load, "registrado pero no controla" es normal', () => {
    const estado = resumirEstadoSw({
      soportado: true,
      registrado: true,
      controlando: false,
      primerLoad: true
    });

    expect(estado.severidad).toBe('aviso');
    expect(estado.etiqueta).toContain('Instalándose');
  });

  it('después del primer load, "registrado pero no controla" es ERROR (safe mode)', () => {
    const estado = resumirEstadoSw({
      soportado: true,
      registrado: true,
      controlando: false,
      primerLoad: false
    });

    expect(estado.severidad).toBe('error');
    expect(estado.etiqueta).toContain('safe mode');
  });

  it('preserva los booleanos crudos para el JSON de soporte', () => {
    const estado = resumirEstadoSw({ soportado: true, registrado: true, controlando: true });

    expect(estado.soportado).toBeTrue();
    expect(estado.registrado).toBeTrue();
    expect(estado.controlando).toBeTrue();
  });
});

import {
  decidirActualizacion,
  decidirAnteEstadoIrrecuperable,
  decidirPorBuildId
} from './decidir-actualizacion.funcion';

/**
 * El costo de equivocarse acá es un BUCLE DE RECARGA en un dispositivo de campo, que es
 * indistinguible de "la app no arranca". Por eso la regla está aislada y tiene tests propios.
 */
describe('decidirActualizacion', () => {
  it('ofrece la actualización cuando la versión está lista y el hash cambió', () => {
    const decision = decidirActualizacion({
      type: 'VERSION_READY',
      currentVersion: { hash: 'aaa' },
      latestVersion: { hash: 'bbb' }
    });

    expect(decision.accion).toBe('ofrecer');
    expect(decision.motivo).toContain('bbb');
  });

  it('NO ofrece si VERSION_READY trae el mismo hash — es el caso que produce el bucle', () => {
    const decision = decidirActualizacion({
      type: 'VERSION_READY',
      currentVersion: { hash: 'aaa' },
      latestVersion: { hash: 'aaa' }
    });

    expect(decision.accion).toBe('ninguna');
  });

  it('no hace nada con VERSION_DETECTED: la versión todavía se está descargando', () => {
    const decision = decidirActualizacion({ type: 'VERSION_DETECTED', version: { hash: 'bbb' } });

    expect(decision.accion).toBe('ninguna');
    expect(decision.motivo).toContain('descarga en curso');
  });

  it('no molesta al usuario si la instalación falló: el SW reintenta solo', () => {
    const decision = decidirActualizacion({
      type: 'VERSION_INSTALLATION_FAILED',
      error: 'Failed to fetch'
    });

    expect(decision.accion).toBe('ninguna');
    expect(decision.motivo).toContain('Failed to fetch');
  });

  it('es fail-closed ante un evento desconocido', () => {
    expect(decidirActualizacion({ type: 'ALGO_NUEVO' }).accion).toBe('ninguna');
  });

  it('tolera null, undefined y eventos sin tipo', () => {
    expect(decidirActualizacion(null).accion).toBe('ninguna');
    expect(decidirActualizacion(undefined).accion).toBe('ninguna');
    expect(decidirActualizacion({} as never).accion).toBe('ninguna');
  });
});

describe('decidirAnteEstadoIrrecuperable', () => {
  it('fuerza la recarga: es el único caso en que el SW ya no puede servir la app', () => {
    const decision = decidirAnteEstadoIrrecuperable('Hash mismatch');

    expect(decision.accion).toBe('recargar-forzado');
    expect(decision.motivo).toContain('Hash mismatch');
  });

  it('funciona sin detalle del error', () => {
    expect(decidirAnteEstadoIrrecuperable(null).accion).toBe('recargar-forzado');
  });
});

describe('decidirPorBuildId (fallback sin Service Worker)', () => {
  it('ofrece cuando la versión publicada difiere de la compilada', () => {
    expect(decidirPorBuildId('2026-08-09T10:00:00.000Z', '2026-08-09T18:00:00.000Z').accion)
      .toBe('ofrecer');
  });

  it('NO ofrece cuando son iguales', () => {
    const mismo = '2026-08-09T10:00:00.000Z';
    expect(decidirPorBuildId(mismo, mismo).accion).toBe('ninguna');
  });

  it('queda apagado en un build local (BUILD_ID = dev)', () => {
    expect(decidirPorBuildId('dev', '2026-08-09T18:00:00.000Z').accion).toBe('ninguna');
  });

  it('no decide nada si no se pudo leer version.json (sin red o 404)', () => {
    expect(decidirPorBuildId('2026-08-09T10:00:00.000Z', null).accion).toBe('ninguna');
  });
});

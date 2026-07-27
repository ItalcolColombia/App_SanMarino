import {
  EstadoSesion,
  LIMITES_SESION_POR_DEFECTO,
  debeHacerHeartbeat,
  evaluarFinDeSesion,
  mensajeFinDeSesion
} from './politica-sesion.funcion';

const AHORA = 1_800_000_000_000;
const MIN = 60 * 1000;
const HORA = 60 * MIN;

function estado(over: Partial<EstadoSesion> = {}): EstadoSesion {
  return {
    ahora: AHORA,
    ultimaActividad: AHORA,
    ultimoContactoOk: AHORA,
    enLinea: true,
    operacionesPendientes: 0,
    ...over
  };
}

describe('evaluarFinDeSesion', () => {
  describe('con red (comportamiento histórico preservado)', () => {
    it('no cierra si hubo actividad reciente', () => {
      expect(evaluarFinDeSesion(estado({ ultimaActividad: AHORA - 4 * MIN }))).toBeNull();
    });

    it('cierra por inactividad a los 5 minutos exactos', () => {
      expect(evaluarFinDeSesion(estado({ ultimaActividad: AHORA - 5 * MIN }))).toBe('inactividad');
    });

    it('no cierra un milisegundo antes del límite', () => {
      expect(evaluarFinDeSesion(estado({ ultimaActividad: AHORA - 5 * MIN + 1 }))).toBeNull();
    });
  });

  describe('sin red — el cambio de fondo', () => {
    it('la inactividad NO cierra la sesión', () => {
      // Antes esto deslogueaba; sin red el usuario no puede volver a entrar.
      const sinRedEInactivo = estado({ enLinea: false, ultimaActividad: AHORA - 3 * HORA });
      expect(evaluarFinDeSesion(sinRedEInactivo)).toBeNull();
    });

    it('perder la señal por horas no cierra la sesión mientras la jornada no venza', () => {
      const s = estado({ enLinea: false, ultimoContactoOk: AHORA - 15 * HORA, ultimaActividad: AHORA });
      expect(evaluarFinDeSesion(s)).toBeNull();
    });

    it('cierra al vencer la jornada de 16 h sin contacto con el servidor', () => {
      const s = estado({ enLinea: false, ultimoContactoOk: AHORA - 16 * HORA });
      expect(evaluarFinDeSesion(s)).toBe('jornada_offline_vencida');
    });

    it('la jornada se mide desde el último contacto OK, no desde la última actividad', () => {
      const s = estado({
        enLinea: false,
        ultimoContactoOk: AHORA - 17 * HORA,
        ultimaActividad: AHORA          // el usuario está usando la app ahora mismo
      });
      expect(evaluarFinDeSesion(s)).toBe('jornada_offline_vencida');
    });
  });

  describe('trabajo sin sincronizar — gana sobre todo lo demás', () => {
    it('no cierra por inactividad con red si hay operaciones pendientes', () => {
      const s = estado({ ultimaActividad: AHORA - 2 * HORA, operacionesPendientes: 1 });
      expect(evaluarFinDeSesion(s)).toBeNull();
    });

    it('no cierra por jornada vencida si hay operaciones pendientes', () => {
      // Cerrar purga, y purgar acá destruye capturas de campo irrecuperables.
      const s = estado({
        enLinea: false,
        ultimoContactoOk: AHORA - 48 * HORA,
        operacionesPendientes: 7
      });
      expect(evaluarFinDeSesion(s)).toBeNull();
    });
  });

  it('respeta límites personalizados', () => {
    const limites = { inactividadMs: 30 * MIN, jornadaOfflineMs: 2 * HORA };
    expect(evaluarFinDeSesion(estado({ ultimaActividad: AHORA - 20 * MIN }), limites)).toBeNull();
    expect(evaluarFinDeSesion(estado({ ultimaActividad: AHORA - 31 * MIN }), limites)).toBe('inactividad');
    expect(
      evaluarFinDeSesion(estado({ enLinea: false, ultimoContactoOk: AHORA - 3 * HORA }), limites)
    ).toBe('jornada_offline_vencida');
  });

  it('los límites por defecto son 5 min de inactividad y 16 h de jornada (decisión D4)', () => {
    expect(LIMITES_SESION_POR_DEFECTO.inactividadMs).toBe(5 * MIN);
    expect(LIMITES_SESION_POR_DEFECTO.jornadaOfflineMs).toBe(16 * HORA);
  });
});

describe('debeHacerHeartbeat', () => {
  it('pinguea si el usuario está activo', () => {
    expect(debeHacerHeartbeat(estado({ ultimaActividad: AHORA - 1 * MIN }))).toBeTrue();
  });

  it('no pinguea si el usuario está inactivo', () => {
    expect(debeHacerHeartbeat(estado({ ultimaActividad: AHORA - 10 * MIN }))).toBeFalse();
  });
});

describe('mensajeFinDeSesion', () => {
  it('conserva los mensajes históricos byte a byte', () => {
    expect(mensajeFinDeSesion('inactividad'))
      .toBe('Tu sesión se cerró por inactividad. Vuelve a iniciar sesión.');
    expect(mensajeFinDeSesion('expirada'))
      .toBe('Tu sesión expiró. Inicia sesión nuevamente.');
  });

  it('tiene mensaje propio para la jornada vencida', () => {
    expect(mensajeFinDeSesion('jornada_offline_vencida')).toContain('sin conectarte al servidor');
  });
});

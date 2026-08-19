import {
  AccesoOffline,
  EstadoAccesoOffline,
  EstadoSesion,
  LIMITES_SESION_POR_DEFECTO,
  debeHacerHeartbeat,
  evaluarAccesoOffline,
  evaluarFinDeSesion,
  mensajeAccesoDenegado,
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

  it('la sesión revocada dice QUIÉN la cerró, y no que expiró', () => {
    const mensaje = mensajeFinDeSesion('revocada');

    expect(mensaje).toBe('Un administrador cerró esta sesión. Iniciá sesión de nuevo.');
    // Si dijera «expiró», quien perdió la tablet creería que es un problema de la app.
    expect(mensaje).not.toBe(mensajeFinDeSesion('expirada'));
  });
});


/**
 * La puerta de las rutas protegidas (fix F-2).
 *
 * El caso que motiva todo: **minuto 61 en una granja sin señal**. El JWT dura 60 min, así que a esa
 * altura está vencido; antes eso era `logout()` —con purga— y el operario quedaba afuera, sin red
 * para volver a entrar. La jornada de 16 h de D4 existía solo para el camino del timer.
 */
describe('evaluarAccesoOffline', () => {
  function acceso(over: Partial<EstadoAccesoOffline> = {}): EstadoAccesoOffline {
    return {
      tokenVencido: true,
      enLinea: false,
      ahora: AHORA,
      ultimoContactoOk: AHORA,
      operacionesPendientes: 0,
      ...over
    };
  }

  describe('token vivo', () => {
    it('con red pasa', () => {
      expect(evaluarAccesoOffline(acceso({ tokenVencido: false, enLinea: true }))).toBe('permitir');
    });

    it('sin red pasa igual', () => {
      expect(evaluarAccesoOffline(acceso({ tokenVencido: false }))).toBe('permitir');
    });

    it('pasa aunque la jornada esté agotada: el tope es para el token vencido, no un tope de uso', () => {
      expect(
        evaluarAccesoOffline(acceso({ tokenVencido: false, ultimoContactoOk: AHORA - 40 * HORA }))
      ).toBe('permitir');
    });
  });

  describe('token vencido CON red — se cierra como siempre', () => {
    it('cierra sesión', () => {
      expect(evaluarAccesoOffline(acceso({ enLinea: true }))).toBe('cerrar_sesion');
    });

    it('cierra aunque la jornada offline no se haya agotado: con red se puede volver a entrar ya', () => {
      expect(evaluarAccesoOffline(acceso({ enLinea: true, ultimoContactoOk: AHORA }))).toBe('cerrar_sesion');
    });

    it('🔑 con capturas sin subir NO se cierra: el camino que cierra es el que purga', () => {
      // Misma regla que ya protegía `evaluarFinDeSesion`. Igual va al login —hay red—, sin purgar.
      expect(evaluarAccesoOffline(acceso({ enLinea: true, operacionesPendientes: 1 })))
        .toBe('denegar_trabajo_pendiente');
    });
  });

  describe('token vencido SIN red — el fix', () => {
    it('🔑 al minuto 61 sigue trabajando: el caso que rompe hoy', () => {
      expect(evaluarAccesoOffline(acceso({ ultimoContactoOk: AHORA - 61 * MIN }))).toBe('permitir');
    });

    it('a las 15 h 59 min todavía pasa', () => {
      expect(evaluarAccesoOffline(acceso({ ultimoContactoOk: AHORA - 16 * HORA + MIN }))).toBe('permitir');
    });

    it('a las 16 h exactas se corta (límite inclusivo, igual que el timer)', () => {
      expect(evaluarAccesoOffline(acceso({ ultimoContactoOk: AHORA - 16 * HORA })))
        .toBe('denegar_jornada_vencida');
    });

    it('un milisegundo antes, no', () => {
      expect(evaluarAccesoOffline(acceso({ ultimoContactoOk: AHORA - 16 * HORA + 1 }))).toBe('permitir');
    });

    it('sin ancla de contacto (token ilegible ⇒ 0) se niega, pero SIN purgar', () => {
      expect(evaluarAccesoOffline(acceso({ ultimoContactoOk: 0 }))).toBe('denegar_jornada_vencida');
    });

    it('🔑 SIN RED NUNCA se devuelve cerrar_sesion: el logout sería irreversible', () => {
      const combinaciones: Array<Partial<EstadoAccesoOffline>> = [
        { ultimoContactoOk: AHORA },
        { ultimoContactoOk: AHORA - 61 * MIN },
        { ultimoContactoOk: AHORA - 40 * HORA },
        { ultimoContactoOk: 0, operacionesPendientes: 5 },
        { ultimoContactoOk: AHORA - 40 * HORA, operacionesPendientes: 5 }
      ];

      for (const over of combinaciones) {
        expect(evaluarAccesoOffline(acceso(over))).not.toBe('cerrar_sesion');
      }
    });
  });

  it('la jornada se puede parametrizar, igual que en el timer', () => {
    const limites = { ...LIMITES_SESION_POR_DEFECTO, jornadaOfflineMs: 2 * HORA };

    expect(evaluarAccesoOffline(acceso({ ultimoContactoOk: AHORA - 3 * HORA }), limites))
      .toBe('denegar_jornada_vencida');
    expect(evaluarAccesoOffline(acceso({ ultimoContactoOk: AHORA - HORA }), limites)).toBe('permitir');
  });

  describe('mensajeAccesoDenegado', () => {
    it('la jornada vencida reusa el mensaje del timer: es la misma situación', () => {
      expect(mensajeAccesoDenegado('denegar_jornada_vencida')).toBe(mensajeFinDeSesion('jornada_offline_vencida'));
    });

    it('el trabajo pendiente dice qué hacer', () => {
      expect(mensajeAccesoDenegado('denegar_trabajo_pendiente')).toContain('capturas sin enviar');
    });

    it('permitir y cerrar_sesion no avisan nada (cerrar era silencioso y se deja silencioso)', () => {
      for (const caso of ['permitir', 'cerrar_sesion'] as AccesoOffline[]) {
        expect(mensajeAccesoDenegado(caso)).toBeNull();
      }
    });
  });
});

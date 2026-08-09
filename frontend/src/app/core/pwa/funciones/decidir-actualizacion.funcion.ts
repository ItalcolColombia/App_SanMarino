import type { DecisionActualizacion, EventoVersionSw } from '../models/pwa.model';

/**
 * Decide qué hacer ante un evento de `SwUpdate.versionUpdates`.
 *
 * Función PURA: no toca `window`, no recarga, no muestra nada. Solo devuelve la decisión.
 * Quien la ejecuta es `PwaActualizacionService`.
 *
 * ## El criterio, y por qué es este
 *
 * | Evento | Decisión | Motivo |
 * |---|---|---|
 * | `VERSION_DETECTED` | `ninguna` | Se detectó una versión nueva pero **todavía se está descargando**. Ofrecerla acá haría que el usuario aceptara y no pasara nada |
 * | `VERSION_READY` | `ofrecer` | La versión está completa en disco. Aplicarla es instantáneo |
 * | `VERSION_INSTALLATION_FAILED` | `ninguna` | La descarga falló (red de granja). El SW reintenta solo en el próximo chequeo. Molestar al usuario con un error que se corrige solo no aporta nada |
 * | cualquier otro | `ninguna` | Fail-closed: ante un evento desconocido no se toca nada |
 *
 * `VERSION_READY` con el **mismo hash** que el actual se ignora: es el caso que produce el
 * **bucle de recarga**. Aparece cuando el SW re-emite el estado, y una implementación ingenua
 * ofrece → el usuario aplica → recarga → mismo hash → ofrece otra vez.
 */
export function decidirActualizacion(evento: EventoVersionSw | null | undefined): DecisionActualizacion {
  if (!evento || typeof evento.type !== 'string') {
    return { accion: 'ninguna', motivo: 'evento vacío o sin tipo' };
  }

  switch (evento.type) {
    case 'VERSION_READY': {
      const actual = evento.currentVersion?.hash;
      const nueva = evento.latestVersion?.hash;

      if (actual && nueva && actual === nueva) {
        return { accion: 'ninguna', motivo: 'VERSION_READY con el mismo hash: nada que aplicar' };
      }
      return { accion: 'ofrecer', motivo: `versión lista (${nueva ?? 'hash desconocido'})` };
    }

    case 'VERSION_DETECTED':
      return { accion: 'ninguna', motivo: 'versión detectada, descarga en curso' };

    case 'VERSION_INSTALLATION_FAILED':
      return {
        accion: 'ninguna',
        motivo: `instalación fallida (${evento.error ?? 'sin detalle'}); el SW reintenta solo`
      };

    default:
      return { accion: 'ninguna', motivo: `evento no manejado: ${evento.type}` };
  }
}

/**
 * Decide qué hacer ante un `SwUpdate.unrecoverable`.
 *
 * Es el ÚNICO caso que justifica una recarga forzada: el SW perdió archivos que necesita y ya
 * no puede servir la app. Sin recargar, el usuario ve una pantalla rota sin salida.
 */
export function decidirAnteEstadoIrrecuperable(razon: string | null | undefined): DecisionActualizacion {
  return {
    accion: 'recargar-forzado',
    motivo: `estado irrecuperable del Service Worker: ${razon ?? 'sin detalle'}`
  };
}

/**
 * Fallback para navegadores sin Service Worker: compara el `buildId` compilado dentro del
 * bundle contra el publicado en `/version.json`.
 *
 * Sigue **sin recargar**: levanta el mismo banner que el camino del SW. Que el navegador sea
 * viejo no es razón para tirarle el formulario al operario.
 */
export function decidirPorBuildId(
  compilado: string | null | undefined,
  publicado: string | null | undefined
): DecisionActualizacion {
  // 'dev' es el valor commiteado de `build-info.ts`: en local no hay versión publicada
  // contra la cual comparar (lo escribe `scripts/build-version.js prepare` en el build).
  if (!compilado || compilado === 'dev') {
    return { accion: 'ninguna', motivo: 'build local: chequeo de versión apagado' };
  }
  if (!publicado) {
    return { accion: 'ninguna', motivo: 'no se pudo leer /version.json (sin red o 404)' };
  }
  if (publicado === compilado) {
    return { accion: 'ninguna', motivo: 'versión publicada igual a la compilada' };
  }
  return { accion: 'ofrecer', motivo: `versión publicada distinta (${publicado})` };
}

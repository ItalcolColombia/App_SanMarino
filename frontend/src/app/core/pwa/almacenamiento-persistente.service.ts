import { Injectable, inject, signal } from '@angular/core';

import { TokenStorageService } from '../auth/token-storage.service';
import { decidirPedirPersistencia } from './funciones/decidir-pedir-persistencia.funcion';

/** Resultado de la gestión de persistencia, para mostrar en `/diagnostico`. */
export type EstadoAlmacenamiento =
  | 'sin-api'        // el navegador no expone la Storage API
  | 'sin-pedir'      // todavía no correspondía pedirla (p. ej. sin sesión)
  | 'concedida'      // el almacenamiento NO puede ser desalojado
  | 'denegada';      // se pidió y el navegador dijo que no

/**
 * Pide que el almacenamiento local sea **persistente**, o sea que el navegador no pueda desalojarlo.
 *
 * ## El problema que resuelve
 *
 * `/diagnostico` ya informaba `navigator.storage.persisted()`, pero **nadie llamaba nunca a
 * `persist()`**: la app miraba el estado sin pedirlo jamás. Sin la concesión, la base de la consulta
 * offline es *best-effort* y el navegador puede borrarla ante presión de disco. Ese es el peor modo
 * de falla que queda en la PWA porque **es silencioso**: sin error y sin log, el operario abre la app
 * en la granja, sin red, y la pantalla está vacía como si nunca hubiera consultado nada.
 *
 * ## Es seguro llamarlo siempre
 *
 * No destruye nada y no puede romper la app: si el navegador deniega, o la API no existe, todo queda
 * exactamente como antes. Por eso todas las llamadas van en `try/catch` y un rechazo se guarda como
 * **estado**, no como error.
 */
@Injectable({ providedIn: 'root' })
export class AlmacenamientoPersistenteService {
  private readonly storage = inject(TokenStorageService);

  /** Último estado conocido; lo lee la pantalla de diagnóstico. */
  readonly estado = signal<EstadoAlmacenamiento>('sin-pedir');

  private yaPedida = false;

  /**
   * Evalúa y, si corresponde, pide la persistencia. Idempotente: se puede llamar en cada arranque y
   * en cada login sin efectos indeseados.
   */
  async asegurar(): Promise<EstadoAlmacenamiento> {
    const apiDisponible = typeof navigator !== 'undefined' && !!navigator.storage?.persist;

    let yaConcedida: boolean | null = null;
    if (apiDisponible && navigator.storage.persisted) {
      try {
        yaConcedida = await navigator.storage.persisted();
      } catch {
        yaConcedida = null;
      }
    }

    if (yaConcedida === true) {
      this.estado.set('concedida');
      return this.estado();
    }

    const debePedir = decidirPedirPersistencia({
      apiDisponible,
      yaConcedida,
      yaPedidaEnEstaSesion: this.yaPedida,
      haySesion: !!this.storage.getToken()
    });

    if (!debePedir) {
      this.estado.set(apiDisponible ? 'sin-pedir' : 'sin-api');
      return this.estado();
    }

    this.yaPedida = true;
    try {
      const concedida = await navigator.storage.persist();
      this.estado.set(concedida ? 'concedida' : 'denegada');
    } catch {
      // Un rechazo no es un fallo de la app: queda registrado como estado y la consulta offline
      // sigue funcionando, solo que expuesta al desalojo.
      this.estado.set('denegada');
    }

    return this.estado();
  }
}

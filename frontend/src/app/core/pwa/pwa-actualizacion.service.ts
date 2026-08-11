import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SwUpdate } from '@angular/service-worker';
import { catchError, map, of } from 'rxjs';

import { BUILD_ID } from '../build-info';
import {
  decidirActualizacion,
  decidirAnteEstadoIrrecuperable,
  decidirPorBuildId
} from './funciones/decidir-actualizacion.funcion';
import type { EventoVersionSw } from './models/pwa.model';

/**
 * Detecta que se publicó una versión nueva del front y **le ofrece al usuario** aplicarla.
 *
 * ## Reemplaza a `VersionCheckService` (eliminado en esta misma entrega)
 *
 * Aquel servicio bajaba `/version.json` cada 5 minutos y, ante cualquier diferencia, llamaba
 * `window.location.reload()` **un segundo después, sin preguntar**. Con un Service Worker
 * encima eso es peor que antes por dos razones concretas:
 *
 *  1. **Dos autoridades de recarga se pelean.** El SW decide cuándo hay versión nueva mirando
 *     `ngsw.json`; el polling decide mirando `version.json`. Los dos criterios no se activan al
 *     mismo tiempo (el SW espera a tener la versión completa en disco), así que conviven mal:
 *     el polling recarga, el SW sigue sirviendo el bundle viejo desde caché, el polling vuelve a
 *     ver la diferencia y recarga otra vez. Es un **bucle**, y en un dispositivo de campo es
 *     indistinguible de "la app no arranca".
 *  2. **Recargar sin preguntar tira la captura en curso.** Un galponero cargando el seguimiento
 *     diario pierde el formulario a mitad.
 *
 * Ahora hay **una sola** autoridad —el SW cuando existe, `version.json` como fallback cuando el
 * navegador no lo soporta— y en ningún caso se recarga por decisión propia. Salvo un estado
 * irrecuperable, que es justamente cuando el SW ya no puede servir la app.
 */
@Injectable({ providedIn: 'root' })
export class PwaActualizacionService {
  /** Cada cuánto se le pregunta al servidor si hay versión nueva. */
  private readonly INTERVALO_CHEQUEO_MS = 30 * 60 * 1000; // 30 min

  private readonly swUpdate = inject(SwUpdate);
  private readonly http = inject(HttpClient);

  /** Hay una versión lista y el usuario todavía no la aplicó. Lo consume la barra de estado. */
  readonly actualizacionDisponible = signal(false);

  /** Último motivo evaluado. Solo para la pantalla de diagnóstico; no se le muestra al operario. */
  readonly ultimoMotivo = signal<string>('sin evaluar');

  /** Versión con la que se compiló este bundle (la escribe `scripts/build-version.js prepare`). */
  readonly buildId = BUILD_ID;

  private temporizador: ReturnType<typeof setInterval> | null = null;
  private aplicando = false;

  /** Arranca la vigilancia. Se llama una sola vez, desde `AppComponent`. */
  iniciar(): void {
    if (this.swUpdate.isEnabled) {
      this.vigilarPorServiceWorker();
    } else {
      // Sin SW (dev server, navegador viejo, o SW deshabilitado): fallback por version.json.
      this.vigilarPorVersionJson();
    }
  }

  detener(): void {
    if (this.temporizador) {
      clearInterval(this.temporizador);
      this.temporizador = null;
    }
  }

  // ---------------------------------------------------------------------------
  // Camino principal: Service Worker
  // ---------------------------------------------------------------------------

  private vigilarPorServiceWorker(): void {
    this.swUpdate.versionUpdates.subscribe(evento => {
      const decision = decidirActualizacion(evento as unknown as EventoVersionSw);
      this.ultimoMotivo.set(decision.motivo);

      if (decision.accion === 'ofrecer') {
        this.actualizacionDisponible.set(true);
      }
    });

    // `unrecoverable`: el SW perdió archivos que necesita y ya no puede servir la app.
    // Es el único caso en el que recargar sin preguntar es lo correcto — la alternativa
    // es dejar al usuario mirando una pantalla rota sin salida.
    this.swUpdate.unrecoverable.subscribe(evento => {
      const decision = decidirAnteEstadoIrrecuperable(evento?.reason);
      this.ultimoMotivo.set(decision.motivo);
      console.error('[pwa]', decision.motivo);

      if (decision.accion === 'recargar-forzado') {
        document.location.reload();
      }
    });

    // Chequeo periódico. `checkForUpdate()` maneja solo la falta de red (rechaza la promesa).
    this.temporizador = setInterval(() => this.buscarActualizaciones(), this.INTERVALO_CHEQUEO_MS);
  }

  /** Chequeo manual — lo usa el botón de la pantalla de diagnóstico. */
  async buscarActualizaciones(): Promise<boolean> {
    if (!this.swUpdate.isEnabled) {
      return false;
    }
    try {
      return await this.swUpdate.checkForUpdate();
    } catch (error) {
      // Sin red es lo normal en campo: no es un error que valga la pena escalar.
      this.ultimoMotivo.set(`chequeo fallido: ${error}`);
      return false;
    }
  }

  /**
   * Aplica la versión ya descargada. Lo dispara **el usuario** desde el banner.
   *
   * `activateUpdate()` cambia la versión que sirve el SW; recién después se recarga, para que
   * la página que se pinta sea la nueva. El orden inverso serviría el bundle viejo otra vez.
   */
  async aplicarActualizacion(): Promise<void> {
    if (this.aplicando) {
      return;
    }
    this.aplicando = true;

    try {
      if (this.swUpdate.isEnabled) {
        await this.swUpdate.activateUpdate();
      }
      document.location.reload();
    } catch (error) {
      this.aplicando = false;
      this.ultimoMotivo.set(`no se pudo activar: ${error}`);
      // Recargar igual: con `no-cache` en index.html y ngsw.json, el arranque siguiente
      // vuelve a intentar la actualización desde cero.
      document.location.reload();
    }
  }

  /** El usuario decidió seguir trabajando. Se le vuelve a ofrecer en el próximo arranque. */
  posponer(): void {
    this.actualizacionDisponible.set(false);
  }

  // ---------------------------------------------------------------------------
  // Fallback: /version.json (navegadores sin Service Worker)
  // ---------------------------------------------------------------------------

  private vigilarPorVersionJson(): void {
    // En un build local `BUILD_ID` vale 'dev' y no hay contra qué comparar.
    if (this.buildId === 'dev') {
      this.ultimoMotivo.set('build local: chequeo de versión apagado');
      return;
    }

    this.chequearVersionJson();
    this.temporizador = setInterval(() => this.chequearVersionJson(), this.INTERVALO_CHEQUEO_MS);
  }

  private chequearVersionJson(): void {
    // Cache-buster además del `no-cache` de nginx: hay proxies de operador que lo ignoran.
    this.http
      .get<{ buildId?: string }>(`/version.json?v=${Date.now()}`, {
        headers: { 'Cache-Control': 'no-cache', Pragma: 'no-cache' }
      })
      .pipe(
        map(res => (res && typeof res.buildId === 'string' ? res.buildId : null)),
        catchError(() => of(null))
      )
      .subscribe(publicado => {
        const decision = decidirPorBuildId(this.buildId, publicado);
        this.ultimoMotivo.set(decision.motivo);

        if (decision.accion === 'ofrecer') {
          this.actualizacionDisponible.set(true);
        }
      });
  }
}

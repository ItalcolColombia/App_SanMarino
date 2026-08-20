import { Injectable, InjectionToken, NgZone, inject } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, Subscription } from 'rxjs';
import { TokenStorageService } from './token-storage.service';
import { AuthService } from './auth.service';
import { LlaveroSesionesService } from './llavero-sesiones.service';
import { ToastService } from '../../shared/services/toast.service';
import { debeCerrarSesionPor401, esSesionRevocada } from './funciones/debe-cerrar-sesion-por-401.funcion';
import {
  EstadoSesion,
  LIMITES_SESION_POR_DEFECTO,
  MotivoFinDeSesion,
  debeHacerHeartbeat,
  evaluarFinDeSesion,
  mensajeFinDeSesion
} from './funciones/politica-sesion.funcion';

/**
 * Proveedor del trabajo capturado y todavía no sincronizado.
 *
 * Lo implementará la capa offline (`shared/offline/outbox`) cuando exista. Hasta entonces
 * el default reporta 0 y el comportamiento es el de una app puramente online.
 *
 * Existe desde ahora porque la política de sesión **necesita** consultarlo: cerrar la sesión
 * dispara una purga, y purgar con capturas pendientes destruye trabajo de campo.
 */
export interface ProveedorTrabajoPendiente {
  /** Cantidad de operaciones capturadas sin subir al servidor. */
  operacionesPendientes(): number;
}

export const TRABAJO_PENDIENTE_OFFLINE = new InjectionToken<ProveedorTrabajoPendiente>(
  'TRABAJO_PENDIENTE_OFFLINE'
);

/**
 * Sesión deslizante por inactividad, consciente del modo sin conexión.
 *
 * - Con red: cierra la sesión tras 5 min sin interacción (cada interacción reinicia el contador).
 * - **Sin red: no cierra nada.** Ni por inactividad ni por heartbeats fallidos. Lo único que
 *   cierra es el tope duro de jornada (16 h sin contacto con el servidor, decisión D4).
 * - Con operaciones pendientes de sincronizar, no cierra por tiempo bajo ninguna circunstancia.
 * - Un 401 de autenticación sí cierra; un 401 del gate de plataforma (SECRET_UP) no.
 *
 * La política vive aislada y con tests en `funciones/politica-sesion.funcion.ts`; acá solo se
 * orquestan los timers, los listeners y el estado. No cambia el JWT (sigue de 60 min).
 *
 * Se arranca/detiene solo según haya sesión en storage. Corre fuera de la zona Angular para
 * que los listeners de `mousemove` no disparen change detection.
 */
@Injectable({ providedIn: 'root' })
export class SessionTimeoutService {
  private readonly HEARTBEAT_MS = 90 * 1000;   // ping de conexión cada 90 s (solo si activo)
  private readonly IDLE_CHECK_MS = 15 * 1000;  // frecuencia de evaluación de la política
  private readonly MAX_HEARTBEAT_FAILS = 2;    // fallos de red seguidos para darse por sin conexión

  private readonly limites = LIMITES_SESION_POR_DEFECTO;

  private readonly zone = inject(NgZone);
  private readonly router = inject(Router);
  private readonly storage = inject(TokenStorageService);
  private readonly auth = inject(AuthService);
  private readonly llavero = inject(LlaveroSesionesService);
  private readonly toast = inject(ToastService);
  private readonly trabajoPendiente = inject(TRABAJO_PENDIENTE_OFFLINE, { optional: true });

  private readonly ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'mousemove', 'scroll', 'touchstart', 'wheel'];

  private running = false;
  private ending = false;
  private lastActivity = 0;
  private ultimoContactoOk = 0;
  private heartbeatFails = 0;
  private idleTimer: ReturnType<typeof setInterval> | null = null;
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null;
  private heartbeatSub?: Subscription;
  private sessionSub?: Subscription;

  /** Estado de conexión observable, para que la UI pueda mostrar el modo sin red. */
  private readonly enLinea$$ = new BehaviorSubject<boolean>(true);
  readonly enLinea$: Observable<boolean> = this.enLinea$$.asObservable();
  get enLinea(): boolean { return this.enLinea$$.value; }

  private readonly onActivity = () => { this.lastActivity = Date.now(); };
  private readonly onVisibility = () => { if (document.visibilityState === 'visible') this.lastActivity = Date.now(); };
  // El navegador avisa de los cambios de conectividad; es más rápido que esperar al heartbeat.
  private readonly onOnline = () => { this.marcarEnLinea(true); };
  private readonly onOffline = () => { this.marcarEnLinea(false); };

  /** Llamar una vez al arranque (AppComponent). Arranca/detiene según haya sesión. */
  init(): void {
    if (this.sessionSub) return;
    this.sessionSub = this.storage.session$.subscribe(session => {
      if (session?.accessToken) this.start();
      else this.stop();
    });
  }

  /** Notificado por el interceptor ante un 401 de AUTENTICACIÓN (no el de plataforma). */
  onUnauthorized(): void {
    if (this.running) this.zone.run(() => this.endSession('expirada'));
  }

  private start(): void {
    if (this.running) return;
    this.running = true;
    this.ending = false;
    this.heartbeatFails = 0;
    this.lastActivity = Date.now();
    // Arrancar la jornada offline ahora: si el dispositivo nunca llega a hablar con el
    // servidor, el tope se cuenta desde el inicio de sesión y no desde el epoch.
    this.ultimoContactoOk = Date.now();
    this.marcarEnLinea(navigator.onLine !== false);
    this.zone.runOutsideAngular(() => {
      this.ACTIVITY_EVENTS.forEach(ev => window.addEventListener(ev, this.onActivity, { passive: true }));
      document.addEventListener('visibilitychange', this.onVisibility);
      window.addEventListener('online', this.onOnline);
      window.addEventListener('offline', this.onOffline);
      this.idleTimer = setInterval(() => this.evaluarPolitica(), this.IDLE_CHECK_MS);
      this.heartbeatTimer = setInterval(() => this.checkHeartbeat(), this.HEARTBEAT_MS);
    });
  }

  private stop(): void {
    if (!this.running && !this.idleTimer && !this.heartbeatTimer) return;
    this.running = false;
    this.ACTIVITY_EVENTS.forEach(ev => window.removeEventListener(ev, this.onActivity));
    document.removeEventListener('visibilitychange', this.onVisibility);
    window.removeEventListener('online', this.onOnline);
    window.removeEventListener('offline', this.onOffline);
    if (this.idleTimer) { clearInterval(this.idleTimer); this.idleTimer = null; }
    if (this.heartbeatTimer) { clearInterval(this.heartbeatTimer); this.heartbeatTimer = null; }
    this.heartbeatSub?.unsubscribe();
    this.heartbeatSub = undefined;
  }

  /** Foto del estado actual para alimentar la política pura. */
  private estadoActual(): EstadoSesion {
    return {
      ahora: Date.now(),
      ultimaActividad: this.lastActivity,
      ultimoContactoOk: this.ultimoContactoOk,
      enLinea: this.enLinea,
      operacionesPendientes: this.trabajoPendiente?.operacionesPendientes() ?? 0
    };
  }

  private evaluarPolitica(): void {
    if (!this.running) return;
    const motivo = evaluarFinDeSesion(this.estadoActual(), this.limites);
    if (motivo) this.zone.run(() => this.endSession(motivo));
  }

  private checkHeartbeat(): void {
    if (!this.running) return;
    if (!debeHacerHeartbeat(this.estadoActual(), this.limites)) return;

    this.heartbeatSub?.unsubscribe();
    this.heartbeatSub = this.auth.heartbeat().subscribe({
      next: () => {
        this.heartbeatFails = 0;
        this.registrarContactoReal();
        this.marcarEnLinea(true);
      },
      error: (err: HttpErrorResponse) => {
        // 401 de autenticación: el token ya no vale, la sesión terminó de verdad.
        // El 401 del gate de plataforma NO cuenta (ver debe-cerrar-sesion-por-401.funcion.ts).
        if (debeCerrarSesionPor401(err, true)) {
          // «Revocada» y «expirada» cierran igual; lo que cambia es qué se le dice al operario.
          // Decirle «expiró» a alguien cuya tablet acaban de apagar a propósito lo manda a esperar
          // en vez de a hablar con quien la cerró.
          const motivo: MotivoFinDeSesion = esSesionRevocada(err) ? 'revocada' : 'expirada';
          this.zone.run(() => this.endSession(motivo));
          return;
        }

        // status 0 = red caída. Antes esto cerraba la sesión a los 2 fallos (~3 min) y dejaba
        // al operario encerrado afuera en una granja sin señal. Ahora solo marca el modo sin
        // conexión; la política decide, y sin red no cierra por inactividad.
        // Otros estados (500, 502…) significan que el backend respondió: seguimos en línea.
        if (err?.status === 0) {
          this.heartbeatFails++;
          if (this.heartbeatFails >= this.MAX_HEARTBEAT_FAILS) {
            this.zone.run(() => this.marcarEnLinea(false));
          }
        } else {
          this.heartbeatFails = 0;
        }
      }
    });
  }

  /**
   * Hubo contacto REAL con el servidor: 200 del heartbeat.
   *
   * Además del contador propio, refresca la jornada de **este slot** en el padrón (R-M8): la ventana
   * de 16 h es por operario, y sin esto la del selector se contaría desde el login para siempre.
   *
   * ## Por qué solo acá, y no en los otros dos lugares que tocan `ultimoContactoOk`
   *
   * `start()` corre en **cada arranque de la app** con una sesión guardada, y el evento `online` del
   * navegador solo dice que la interfaz de red se levantó. Ninguno de los dos es hablar con el
   * servidor. Propagarlos al padrón dejaría la jornada de 16 h renovándose sola con un F5 o con el
   * wifi del galpón —o sea, sin tope—, que es justo lo que D4 vino a poner.
   *
   * ## Por qué no se desalinea con el guard
   *
   * El guard mide la jornada desde el token (`iat`/`exp`), no desde acá. Los dos números no pueden
   * separarse más que la vida del token: un heartbeat con 200 **implica** un token todavía válido
   * (uno vencido devuelve 401 y cierra la sesión), así que el último contacto nunca queda a más de
   * una hora del `iat`.
   */
  private registrarContactoReal(): void {
    const ahora = Date.now();
    this.ultimoContactoOk = ahora;
    this.llavero.marcarContactoOk(this.storage.get()?.user?.id ?? null, ahora);
  }

  private marcarEnLinea(valor: boolean): void {
    if (this.enLinea$$.value === valor) return;
    this.enLinea$$.next(valor);
    if (valor) {
      this.heartbeatFails = 0;
      this.ultimoContactoOk = Date.now();
    }
  }

  private endSession(reason: MotivoFinDeSesion): void {
    if (this.ending || !this.running) return;
    this.ending = true;
    this.stop();

    this.toast.info(mensajeFinDeSesion(reason), 'Sesión finalizada', 6000);

    // `logout()` limpia la sesión guardada (token y datos de usuario). La cola offline vive
    // en IndexedDB y NO se purga desde acá: esa decisión es de la capa offline, que solo debe
    // purgar cuando no queda nada pendiente. Ver ProveedorTrabajoPendiente arriba.
    this.auth.logout();
    this.router.navigate(['/login'], { queryParams: { reason } });
  }
}

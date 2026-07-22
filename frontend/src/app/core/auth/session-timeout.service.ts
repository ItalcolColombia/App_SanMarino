import { Injectable, NgZone, inject } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { TokenStorageService } from './token-storage.service';
import { AuthService } from './auth.service';
import { ToastService } from '../../shared/services/toast.service';

/**
 * Sesión deslizante por inactividad (client-side).
 *
 * - Cierra la sesión tras 5 min SIN interacción del usuario (cada interacción reinicia el contador).
 * - Mientras el usuario está activo, un heartbeat verifica la conexión con el backend:
 *     · error de red sostenido (status 0) → cierra la sesión por "sin conexión" (tolerante a microcortes).
 *     · 401 → el token expiró/invalidó → cierra la sesión.
 * - No cambia el JWT (sigue de 60 min); la política de 5 min se enforca acá.
 *
 * Se arranca/detiene solo según haya sesión en storage. Corre fuera de la zona Angular
 * para que los listeners de mousemove no disparen change detection.
 */
@Injectable({ providedIn: 'root' })
export class SessionTimeoutService {
  private readonly IDLE_LIMIT_MS = 5 * 60 * 1000;   // 5 min sin interacción → logout
  private readonly HEARTBEAT_MS = 90 * 1000;         // ping de conexión cada 90 s (solo si activo)
  private readonly IDLE_CHECK_MS = 15 * 1000;        // frecuencia de chequeo de inactividad
  private readonly MAX_HEARTBEAT_FAILS = 2;          // fallos de red seguidos para "sin conexión"

  private readonly zone = inject(NgZone);
  private readonly router = inject(Router);
  private readonly storage = inject(TokenStorageService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  private readonly ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'mousemove', 'scroll', 'touchstart', 'wheel'];

  private running = false;
  private ending = false;
  private lastActivity = 0;
  private heartbeatFails = 0;
  private idleTimer: ReturnType<typeof setInterval> | null = null;
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null;
  private heartbeatSub?: Subscription;
  private sessionSub?: Subscription;

  private readonly onActivity = () => { this.lastActivity = Date.now(); };
  private readonly onVisibility = () => { if (document.visibilityState === 'visible') this.lastActivity = Date.now(); };

  /** Llamar una vez al arranque (AppComponent). Arranca/detiene según haya sesión. */
  init(): void {
    if (this.sessionSub) return;
    this.sessionSub = this.storage.session$.subscribe(session => {
      if (session?.accessToken) this.start();
      else this.stop();
    });
  }

  /** Notificado por el interceptor ante un 401 en una petición autenticada. */
  onUnauthorized(): void {
    if (this.running) this.zone.run(() => this.endSession('expirada'));
  }

  private start(): void {
    if (this.running) return;
    this.running = true;
    this.ending = false;
    this.heartbeatFails = 0;
    this.lastActivity = Date.now();
    this.zone.runOutsideAngular(() => {
      this.ACTIVITY_EVENTS.forEach(ev => window.addEventListener(ev, this.onActivity, { passive: true }));
      document.addEventListener('visibilitychange', this.onVisibility);
      this.idleTimer = setInterval(() => this.checkIdle(), this.IDLE_CHECK_MS);
      this.heartbeatTimer = setInterval(() => this.checkHeartbeat(), this.HEARTBEAT_MS);
    });
  }

  private stop(): void {
    if (!this.running && !this.idleTimer && !this.heartbeatTimer) return;
    this.running = false;
    this.ACTIVITY_EVENTS.forEach(ev => window.removeEventListener(ev, this.onActivity));
    document.removeEventListener('visibilitychange', this.onVisibility);
    if (this.idleTimer) { clearInterval(this.idleTimer); this.idleTimer = null; }
    if (this.heartbeatTimer) { clearInterval(this.heartbeatTimer); this.heartbeatTimer = null; }
    this.heartbeatSub?.unsubscribe();
    this.heartbeatSub = undefined;
  }

  private checkIdle(): void {
    if (!this.running) return;
    if (Date.now() - this.lastActivity >= this.IDLE_LIMIT_MS) {
      this.zone.run(() => this.endSession('inactividad'));
    }
  }

  private checkHeartbeat(): void {
    if (!this.running) return;
    // Solo pingear si el usuario está activo (dentro de la ventana de inactividad).
    if (Date.now() - this.lastActivity >= this.IDLE_LIMIT_MS) return;
    this.heartbeatSub?.unsubscribe();
    this.heartbeatSub = this.auth.heartbeat().subscribe({
      next: () => { this.heartbeatFails = 0; },
      error: (err: HttpErrorResponse) => {
        if (err?.status === 401) {
          this.zone.run(() => this.endSession('expirada'));
          return;
        }
        // status 0 = red caída/sin conexión. Otros (500, etc.) = el back respondió → no contar.
        if (err?.status === 0) {
          this.heartbeatFails++;
          if (this.heartbeatFails >= this.MAX_HEARTBEAT_FAILS) {
            this.zone.run(() => this.endSession('sin_conexion'));
          }
        }
      }
    });
  }

  private endSession(reason: 'inactividad' | 'sin_conexion' | 'expirada'): void {
    if (this.ending || !this.running) return;
    this.ending = true;
    this.stop();
    const messages: Record<typeof reason, string> = {
      inactividad: 'Tu sesión se cerró por inactividad. Vuelve a iniciar sesión.',
      sin_conexion: 'Se perdió la conexión con el servidor. Vuelve a iniciar sesión.',
      expirada: 'Tu sesión expiró. Inicia sesión nuevamente.'
    };
    this.toast.info(messages[reason], 'Sesión finalizada', 6000);
    this.auth.logout();
    this.router.navigate(['/login'], { queryParams: { reason } });
  }
}

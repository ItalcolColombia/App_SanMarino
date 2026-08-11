import { Injectable, NgZone, inject, signal } from '@angular/core';

/**
 * Estado de conexión del dispositivo.
 *
 * `navigator.onLine` es **optimista**: dice `true` cuando hay una interfaz de red levantada,
 * aunque esa red no llegue a ningún lado (el caso clásico de la granja: wifi del galpón
 * conectado, sin salida a internet). Por eso el servicio expone dos cosas distintas:
 *
 *  - `enLinea` — lo que dice el navegador. Sirve para reaccionar rápido a "se cortó".
 *  - `marcarFalloDeRed()` / `marcarExitoDeRed()` — lo que la app **verificó** contra el
 *    backend. El indicador muestra "sin conexión" si cualquiera de los dos lo dice.
 *
 * `SessionTimeoutService` ya distingue el `status === 0` de sus heartbeats (F0.B / B2: perder
 * la red dejó de cerrar la sesión). Este servicio no lo reemplaza; le da a la UI una señal
 * que se puede pintar.
 *
 * Se usan `signal()` en vez de `BehaviorSubject` porque los consumidores son plantillas.
 */
@Injectable({ providedIn: 'root' })
export class ConexionService {
  private readonly zone = inject(NgZone);

  /** Lo que reporta el navegador. */
  readonly enLinea = signal<boolean>(typeof navigator !== 'undefined' ? navigator.onLine : true);

  /** Lo último que se verificó realmente contra el backend. `null` = todavía sin evidencia. */
  readonly backendAlcanzable = signal<boolean | null>(null);

  constructor() {
    if (typeof window === 'undefined') {
      return;
    }

    // Los listeners de `window` corren fuera de la zona de Angular en algunos navegadores;
    // se re-entra explícitamente para que la vista se repinte (ver CLAUDE.md, change detection).
    window.addEventListener('online', () => this.zone.run(() => this.enLinea.set(true)));
    window.addEventListener('offline', () =>
      this.zone.run(() => {
        this.enLinea.set(false);
        this.backendAlcanzable.set(false);
      })
    );
  }

  /** ¿Se puede trabajar contra el servidor ahora mismo? Pesimista a propósito. */
  hayConexionReal(): boolean {
    return this.enLinea() && this.backendAlcanzable() !== false;
  }

  marcarExitoDeRed(): void {
    this.backendAlcanzable.set(true);
    if (!this.enLinea()) {
      // El navegador se equivocaba: una respuesta del backend es evidencia más fuerte.
      this.enLinea.set(true);
    }
  }

  marcarFalloDeRed(): void {
    this.backendAlcanzable.set(false);
  }
}

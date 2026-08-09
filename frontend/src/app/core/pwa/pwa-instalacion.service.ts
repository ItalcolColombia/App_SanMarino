import { Injectable, NgZone, inject, signal } from '@angular/core';

/** Forma del evento `beforeinstallprompt` (no está en los tipos estándar del DOM). */
interface EventoInstalacion extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

/**
 * Instalación de la app en el dispositivo ("Agregar a pantalla de inicio").
 *
 * ## Por qué hay que capturar el evento y no alcanza con dejar que el navegador haga lo suyo
 *
 * Chrome dispara `beforeinstallprompt` **una sola vez** y, si nadie llama a `preventDefault()`,
 * decide él cuándo (y si) mostrar el mini-infobar. En una tablet de granja eso significa que la
 * instalación depende de que el operario acierte con un banner que aparece a criterio del
 * navegador. Guardando el evento, el alistamiento en oficina tiene un botón explícito: se
 * instala con wifi, antes de mandar el dispositivo a campo.
 *
 * ## iOS
 *
 * Safari **no implementa `beforeinstallprompt`**: la instalación es manual (Compartir →
 * Agregar a inicio) y no hay API para dispararla. `esIos()` existe para poder mostrar esa
 * instrucción en vez de un botón que no haría nada. La decisión D5 del plan madre fijó Android
 * como plataforma objetivo, así que iOS es un camino informativo, no soportado.
 */
@Injectable({ providedIn: 'root' })
export class PwaInstalacionService {
  private readonly zone = inject(NgZone);

  /** Hay un prompt de instalación guardado y listo para disparar. */
  readonly puedeInstalar = signal(false);

  /** La app ya corre instalada (standalone), así que no hay nada que ofrecer. */
  readonly yaInstalada = signal(false);

  private evento: EventoInstalacion | null = null;

  constructor() {
    if (typeof window === 'undefined') {
      return;
    }

    this.yaInstalada.set(this.estaEnModoInstalado());

    window.addEventListener('beforeinstallprompt', e => {
      // Sin esto Chrome muestra su propio mini-infobar cuando quiere y el evento se pierde.
      e.preventDefault();

      this.zone.run(() => {
        this.evento = e as EventoInstalacion;
        this.puedeInstalar.set(true);
      });
    });

    window.addEventListener('appinstalled', () => {
      this.zone.run(() => {
        this.evento = null;
        this.puedeInstalar.set(false);
        this.yaInstalada.set(true);
      });
    });
  }

  /** ¿La página se está mostrando como app instalada? */
  estaEnModoInstalado(): boolean {
    if (typeof window === 'undefined') {
      return false;
    }
    const standalone = window.matchMedia?.('(display-mode: standalone)')?.matches ?? false;
    // `navigator.standalone` es la variante de Safari iOS; no está en los tipos estándar.
    const iosStandalone = (window.navigator as unknown as { standalone?: boolean }).standalone === true;
    return standalone || iosStandalone;
  }

  /** iOS/iPadOS: no hay API de instalación, solo se puede explicar el camino manual. */
  esIos(): boolean {
    if (typeof navigator === 'undefined') {
      return false;
    }
    const ua = navigator.userAgent;
    // iPadOS 13+ se anuncia como Mac; se lo distingue por el soporte táctil.
    const iPadOsModerno = /Macintosh/.test(ua) && navigator.maxTouchPoints > 1;
    return /iPhone|iPad|iPod/.test(ua) || iPadOsModerno;
  }

  /**
   * Dispara el diálogo de instalación. Devuelve `true` si el usuario aceptó.
   *
   * El evento es de **un solo uso**: una vez consumido, el navegador no lo vuelve a emitir
   * hasta la próxima visita, así que se descarta pase lo que pase.
   */
  async instalar(): Promise<boolean> {
    if (!this.evento) {
      return false;
    }

    const evento = this.evento;
    this.evento = null;
    this.puedeInstalar.set(false);

    await evento.prompt();
    const { outcome } = await evento.userChoice;
    return outcome === 'accepted';
  }
}

import { ChangeDetectionStrategy, Component, Injector, inject } from '@angular/core';

import { ConexionService } from '../../../core/pwa/conexion.service';
import { PwaActualizacionService } from '../../../core/pwa/pwa-actualizacion.service';
import { PwaInstalacionService } from '../../../core/pwa/pwa-instalacion.service';
import { CacheConsultasService } from '../../offline/cache-consultas.service';
import { OutboxService } from '../../offline/outbox.service';

/**
 * Barra flotante con los tres avisos del ciclo de vida de la PWA:
 * sin conexión · hay versión nueva · se puede instalar.
 *
 * Es deliberadamente NO intrusiva: se ancla abajo, no bloquea la pantalla y no tiene overlay.
 * El aviso de actualización que había antes era un `window.location.reload()` a 1 segundo sin
 * preguntar (ver `PwaActualizacionService`); el objetivo de este componente es que el operario
 * termine de cargar lo que está cargando y decida él.
 *
 * `changeDetection: Eager` — regla del repo para todo componente con estado mutable. Acá el
 * estado son señales de tres servicios; con OnPush el banner podría no repintarse al volver la
 * red (el evento `online` llega de fuera de la vista).
 */
@Component({
  selector: 'app-pwa-barra-estado',
  standalone: true,
  // `href` en vez de `routerLink` a propósito: /diagnostico es la pantalla a la que se recurre
  // cuando algo anda mal, y una navegación dura funciona incluso con el router en mal estado.
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './pwa-barra-estado.component.html',
  styleUrls: ['./pwa-barra-estado.component.scss']
})
export class PwaBarraEstadoComponent {
  private readonly actualizacion = inject(PwaActualizacionService);
  private readonly instalacion = inject(PwaInstalacionService);
  private readonly cache = inject(CacheConsultasService);
  private readonly outbox = inject(OutboxService);
  private readonly injector = inject(Injector);
  readonly conexion = inject(ConexionService);

  readonly hayActualizacion = this.actualizacion.actualizacionDisponible;
  readonly puedeInstalar = this.instalacion.puedeInstalar;

  /** Lo que se está viendo salió de una consulta guardada, no de la red. */
  readonly desdeCache = this.cache.sirviendoDesdeCache;
  readonly antiguedadCache = this.cache.antiguedad;

  /**
   * Capturas que todavía están en el dispositivo (F3).
   *
   * Es lo que cierra la pregunta "¿dónde quedó lo que acabo de registrar?": el formulario se cierra
   * y la tabla no puede mostrar la fila (la sirve la caché, que no la tiene), así que sin este
   * contador el operario no tiene **ninguna** señal de que su captura existe.
   */
  readonly pendientes = this.outbox.pendientes;
  readonly rechazadas = this.outbox.rechazadas;

  /** El usuario cerró el aviso de instalación en esta sesión. */
  instalacionDescartada = false;

  aplicando = false;
  enviando = false;

  get sinConexion(): boolean {
    return !this.conexion.enLinea();
  }

  /** Total de capturas sin confirmar: las que esperan y las que quedaron rechazadas. */
  get totalSinEnviar(): number {
    return this.pendientes() + this.rechazadas();
  }

  /**
   * Con red y con cola: el envío automático puede estar esperando su backoff, así que se ofrece
   * forzarlo. Sin red no se muestra este aviso — lo dice el de "sin conexión", que ya incluye el
   * conteo. Dos barras apiladas tapan el formulario en una tablet.
   */
  get mostrarPendientes(): boolean {
    return !this.sinConexion && this.totalSinEnviar > 0 && !this.hayActualizacion();
  }

  /**
   * Fuerza un envío. El servicio se carga con `import()` diferido: esta barra está en el bundle
   * inicial y traer el sync entero acá empujaba el presupuesto por encima del techo de error.
   */
  async enviarAhora(): Promise<void> {
    this.enviando = true;
    try {
      const { SyncService } = await import('../../offline/sync.service');
      await this.injector.get(SyncService).sincronizar();
    } finally {
      this.enviando = false;
    }
  }

  get mostrarInstalar(): boolean {
    // La actualización y las capturas sin enviar tienen prioridad: dos banners a la vez tapan media
    // pantalla en una tablet, y "instalá la app" es lo menos urgente de los tres.
    return this.puedeInstalar()
      && !this.instalacionDescartada
      && !this.hayActualizacion()
      && !this.mostrarPendientes;
  }

  async aplicar(): Promise<void> {
    this.aplicando = true;
    await this.actualizacion.aplicarActualizacion();
  }

  posponer(): void {
    this.actualizacion.posponer();
  }

  async instalar(): Promise<void> {
    await this.instalacion.instalar();
  }

  descartarInstalacion(): void {
    this.instalacionDescartada = true;
  }
}

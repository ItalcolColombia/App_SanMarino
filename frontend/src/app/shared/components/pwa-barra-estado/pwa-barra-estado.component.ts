import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { ConexionService } from '../../../core/pwa/conexion.service';
import { PwaActualizacionService } from '../../../core/pwa/pwa-actualizacion.service';
import { PwaInstalacionService } from '../../../core/pwa/pwa-instalacion.service';
import { CacheConsultasService } from '../../offline/cache-consultas.service';

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
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './pwa-barra-estado.component.html',
  styleUrls: ['./pwa-barra-estado.component.scss']
})
export class PwaBarraEstadoComponent {
  private readonly actualizacion = inject(PwaActualizacionService);
  private readonly instalacion = inject(PwaInstalacionService);
  private readonly cache = inject(CacheConsultasService);
  readonly conexion = inject(ConexionService);

  readonly hayActualizacion = this.actualizacion.actualizacionDisponible;
  readonly puedeInstalar = this.instalacion.puedeInstalar;

  /** Lo que se está viendo salió de una consulta guardada, no de la red. */
  readonly desdeCache = this.cache.sirviendoDesdeCache;
  readonly antiguedadCache = this.cache.antiguedad;

  /** El usuario cerró el aviso de instalación en esta sesión. */
  instalacionDescartada = false;

  aplicando = false;

  get sinConexion(): boolean {
    return !this.conexion.enLinea();
  }

  get mostrarInstalar(): boolean {
    // La actualización tiene prioridad: dos banners a la vez tapan media pantalla en una tablet.
    return this.puedeInstalar() && !this.instalacionDescartada && !this.hayActualizacion();
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

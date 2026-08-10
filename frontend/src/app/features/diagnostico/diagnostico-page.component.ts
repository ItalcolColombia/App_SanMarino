import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ConexionService } from '../../core/pwa/conexion.service';
import { PwaActualizacionService } from '../../core/pwa/pwa-actualizacion.service';
import { PwaInstalacionService } from '../../core/pwa/pwa-instalacion.service';
import { AlmacenamientoPersistenteService } from '../../core/pwa/almacenamiento-persistente.service';
import { CacheConsultasService } from '../../shared/offline/cache-consultas.service';
import { TokenStorageService } from '../../core/auth/token-storage.service';
import type { EstadoCacheOffline } from '../../shared/offline/models/offline.model';
import { formatearBytes } from '../../core/pwa/funciones/formatear-bytes.funcion';
import { resumirEstadoSw } from '../../core/pwa/funciones/resumir-estado-sw.funcion';
import type { DiagnosticoPwa, EstadoSw } from '../../core/pwa/models/pwa.model';

/** Clave para recordar que este navegador ya tuvo un SW controlando alguna vez. */
const CLAVE_PRIMER_LOAD = 'italgranja.pwa.swVisto';

/**
 * Pantalla de diagnóstico del dispositivo.
 *
 * ## Por qué NO tiene `authGuard`
 *
 * Es la pantalla a la que se recurre cuando *nada más* funciona: sesión vencida sin red para
 * renovarla, Service Worker en safe mode, app que no arranca. Ponerle un guard la haría
 * inalcanzable exactamente en el escenario para el que existe. No expone ningún dato de negocio
 * —build, estado del SW, cuota del dispositivo y nombres de caché— así que no hay nada que
 * proteger. Ver decisión D-F del plan `pwa_f1_shell_plan.md`.
 *
 * ## Por qué el botón de exportar
 *
 * El canal real de soporte en campo es WhatsApp. "Mandame una captura de la consola" no es
 * ejecutable en una tablet; "tocá Copiar diagnóstico y pegalo" sí.
 *
 * `changeDetection: Eager` — hay estado mutable que se llena desde promesas
 * (`storage.estimate()`, `caches.keys()`); con OnPush la pantalla quedaría en "Cargando…".
 */
@Component({
  selector: 'app-diagnostico-page',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './diagnostico-page.component.html'
})
export class DiagnosticoPageComponent implements OnInit {
  private readonly actualizacionSrv = inject(PwaActualizacionService);
  private readonly instalacion = inject(PwaInstalacionService);
  private readonly conexion = inject(ConexionService);
  private readonly almacenamiento = inject(AlmacenamientoPersistenteService);

  private readonly cacheOffline = inject(CacheConsultasService);
  private readonly storage = inject(TokenStorageService);

  cargando = true;
  diagnostico: DiagnosticoPwa | null = null;

  /** Estado de la caché de consulta offline (F2). */
  cache: EstadoCacheOffline | null = null;
  copiado = false;
  buscando = false;
  mensajeChequeo = '';

  /** Se expone para el template (evita `formatearBytes` suelto en la vista). */
  readonly formatear = formatearBytes;

  async ngOnInit(): Promise<void> {
    await this.recargar();
  }

  async recargar(): Promise<void> {
    this.cargando = true;
    this.diagnostico = await this.construirDiagnostico();

    const s = this.storage.get();
    this.cache = await this.cacheOffline.estado({
      userId: s?.user?.id ?? s?.user?.userId ?? null,
      companyId: s?.activeCompanyId ?? null,
      paisId: s?.activePaisId ?? null
    });

    this.cargando = false;
  }

  async buscarActualizaciones(): Promise<void> {
    this.buscando = true;
    this.mensajeChequeo = '';
    const hubo = await this.actualizacionSrv.buscarActualizaciones();
    this.buscando = false;
    this.mensajeChequeo = hubo
      ? 'Se encontró una versión nueva; el aviso aparece abajo cuando termine de descargarse.'
      : 'No hay versión nueva (o no hay conexión para consultar).';
  }

  /** Copia el diagnóstico al portapapeles como JSON, para pegarlo en el chat de soporte. */
  async copiar(): Promise<void> {
    if (!this.diagnostico) {
      return;
    }
    try {
      await navigator.clipboard.writeText(JSON.stringify(this.diagnostico, null, 2));
      this.copiado = true;
      setTimeout(() => (this.copiado = false), 2500);
    } catch {
      // Sin permiso de portapapeles (o contexto no seguro): el JSON igual se ve en pantalla.
      this.copiado = false;
    }
  }

  get json(): string {
    return this.diagnostico ? JSON.stringify(this.diagnostico, null, 2) : '';
  }

  // ---------------------------------------------------------------------------

  private async construirDiagnostico(): Promise<DiagnosticoPwa> {
    const sw = await this.leerEstadoSw();

    let usado: number | undefined;
    let cuota: number | undefined;
    let persistente: boolean | null = null;

    if (navigator.storage?.estimate) {
      const est = await navigator.storage.estimate();
      usado = est.usage;
      cuota = est.quota;
    }
    if (navigator.storage?.persisted) {
      try {
        persistente = await navigator.storage.persisted();
      } catch {
        persistente = null;
      }
    }

    let nombresCache: string[] = [];
    if (typeof caches !== 'undefined') {
      try {
        nombresCache = await caches.keys();
      } catch {
        nombresCache = [];
      }
    }

    return {
      generadoEn: new Date().toISOString(),
      buildId: this.actualizacionSrv.buildId,
      url: window.location.href,
      sw,
      enLinea: this.conexion.enLinea(),
      modoInstalado: this.instalacion.estaEnModoInstalado(),
      almacenamiento: {
        usadoBytes: usado,
        cuotaBytes: cuota,
        usadoLegible: formatearBytes(usado),
        cuotaLegible: formatearBytes(cuota),
        persistente,
        gestionPersistencia: this.almacenamiento.estado()
      },
      caches: nombresCache,
      navegador: navigator.userAgent
    };
  }

  private async leerEstadoSw(): Promise<EstadoSw> {
    const soportado = 'serviceWorker' in navigator;

    if (!soportado) {
      return resumirEstadoSw({ soportado: false, registrado: false, controlando: false });
    }

    const registros = await navigator.serviceWorker.getRegistrations();
    const controlando = navigator.serviceWorker.controller !== null;

    // "Registrado pero no controla" es normal en el PRIMER load de la vida de la app y es el
    // síntoma de safe mode a partir del segundo. La única forma de distinguirlos es recordar
    // si alguna vez hubo un SW controlando en este navegador.
    const yaHuboSw = localStorage.getItem(CLAVE_PRIMER_LOAD) === '1';
    if (controlando && !yaHuboSw) {
      localStorage.setItem(CLAVE_PRIMER_LOAD, '1');
    }

    return resumirEstadoSw({
      soportado: true,
      registrado: registros.length > 0,
      controlando,
      primerLoad: !yaHuboSw
    });
  }
}

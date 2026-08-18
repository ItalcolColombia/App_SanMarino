import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ConexionService } from '../../core/pwa/conexion.service';
import { PwaActualizacionService } from '../../core/pwa/pwa-actualizacion.service';
import { PwaInstalacionService } from '../../core/pwa/pwa-instalacion.service';
import { AlmacenamientoPersistenteService } from '../../core/pwa/almacenamiento-persistente.service';
import { CacheConsultasService } from '../../shared/offline/cache-consultas.service';
import { OutboxService } from '../../shared/offline/outbox.service';
import { SyncService } from '../../shared/offline/sync.service';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { TokenStorageService } from '../../core/auth/token-storage.service';
import type { EstadoCacheOffline } from '../../shared/offline/models/offline.model';
import type { OperacionPendiente } from '../../shared/offline/models/outbox.model';
import { clasificarCapturasDiagnostico } from './funciones/clasificar-capturas-diagnostico.funcion';
import type { CapturaDiagnostico } from './models/captura-diagnostico.model';
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
 * inalcanzable exactamente en el escenario para el que existe. Ver decisión D-F del plan
 * `pwa_f1_shell_plan.md`.
 *
 * ⚠️ Este comentario decía «no expone ningún dato de negocio —build, estado del SW, cuota y nombres
 * de caché—, así que no hay nada que proteger». Era cierto en F1 y **dejó de serlo en F3.1**, que
 * agregó la cola con el `JSON.stringify` de cada payload y el botón de descartar: en una tablet
 * compartida, cualquiera que la levante leería y borraría lo capturado por todos, sin sesión. Por
 * eso las capturas **ajenas** se listan pero van enmascaradas (`clasificarCapturasDiagnostico`).
 * Lo que se protege es el payload y las dos acciones, no la pantalla.
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
  private readonly outbox = inject(OutboxService);
  private readonly sync = inject(SyncService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly storage = inject(TokenStorageService);

  cargando = true;
  diagnostico: DiagnosticoPwa | null = null;

  /** Estado de la caché de consulta offline (F2). */
  cache: EstadoCacheOffline | null = null;

  /**
   * Capturas hechas sin red y todavía no confirmadas por el servidor (F3), cada una con la decisión
   * de si es de esta sesión. Se arma una vez por recarga: un getter que reconstruya el arreglo en
   * cada ciclo rompería el `track` del `@for`.
   */
  capturas: CapturaDiagnostico[] = [];

  /** ¿Hay alguna que esta sesión pueda empujar? Si no, «Enviar ahora» no tiene nada que hacer. */
  hayPropias = false;
  enviando = false;
  mensajeEnvio = '';

  copiado = false;
  buscando = false;
  mensajeChequeo = '';

  /** `clientOpId` de la última captura copiada, para el aviso «Copiado» de esa fila. */
  capturaCopiada: string | null = null;

  /** Se expone para el template (evita `formatearBytes` suelto en la vista). */
  readonly formatear = formatearBytes;

  async ngOnInit(): Promise<void> {
    await this.recargar();
  }

  async recargar(): Promise<void> {
    this.cargando = true;
    this.diagnostico = await this.construirDiagnostico();

    const identidad = this.storage.identidadActual();
    this.cache = await this.cacheOffline.estado(identidad);

    // Toda la cola, no solo la de la partición activa: si el operario cambió de empresa, lo que
    // quedó pendiente de la anterior tiene que seguir viéndose. Esconderlo sería la peor variante
    // de "se perdió". Lo que NO se muestra de las ajenas es el payload, y sobre ellas no se ofrece
    // ni copiar ni descartar.
    this.capturas = clasificarCapturasDiagnostico(await this.outbox.listarTodas(), identidad);
    this.hayPropias = this.capturas.some(c => c.propia);

    this.cargando = false;
  }

  /** Fuerza un envío desde la pantalla, sin esperar al evento de reconexión. */
  async enviarPendientes(): Promise<void> {
    this.enviando = true;
    this.mensajeEnvio = '';

    const confirmadas = await this.sync.sincronizar();
    await this.recargar();

    this.enviando = false;
    this.mensajeEnvio = confirmadas > 0
      ? `Se enviaron ${confirmadas} captura(s).`
      : 'No se pudo enviar nada todavía (sin conexión, o esperando el próximo intento).';
  }

  /**
   * Lo que el operario cargó, legible. El payload **ya estaba guardado** en la cola desde F3.1;
   * lo que faltaba era mostrarlo: sin esto, la única acción posible sobre un rechazo es descartar,
   * y descartar a ciegas obliga a acordarse de memoria de lo que se capturó en el galpón.
   */
  payloadLegible(operacion: OperacionPendiente): string {
    try {
      return JSON.stringify(operacion.payload, null, 2);
    } catch {
      // Un payload no serializable (referencia circular) no puede tumbar la pantalla de rescate.
      return String(operacion.payload);
    }
  }

  /** Copia la captura completa, para pegarla en el chat de soporte o rehacerla a mano. */
  async copiarCaptura(captura: CapturaDiagnostico): Promise<void> {
    // La guarda va acá y no solo en la plantilla: esconder un botón es una mitigación de front, y
    // este repo ya pagó esa lección (la marca `para_proximo_ciclo`, apagada en el front y viva en
    // la API durante meses).
    if (!captura.propia) return;

    const operacion = captura.operacion;
    const texto = [
      `${operacion.tipo} · ${operacion.metodo} ${operacion.url}`,
      `capturada ${operacion.capturadoAtDispositivo} · empresa ${operacion.companyId}`,
      operacion.errorCodigo ? `rechazada (${operacion.errorCodigo}): ${operacion.detalle ?? ''}` : '',
      '',
      this.payloadLegible(operacion)
    ].filter(Boolean).join('\n');

    try {
      await navigator.clipboard.writeText(texto);
      this.capturaCopiada = operacion.clientOpId;
      setTimeout(() => (this.capturaCopiada = null), 2500);
    } catch {
      // Sin permiso de portapapeles: el JSON igual está en pantalla y se puede seleccionar.
      this.capturaCopiada = null;
    }
  }

  /**
   * Descarta una captura. **El único camino que borra trabajo no sincronizado**, y por eso pide
   * confirmación: lo que se descarta no existe en ningún otro lado.
   */
  async descartar(captura: CapturaDiagnostico): Promise<void> {
    // Ídem: sin esto, el descarte de trabajo ajeno queda a un `document.querySelector` de distancia.
    if (!captura.propia) return;

    const operacion = captura.operacion;
    const confirmado = await this.confirmDialog.ask({
      title: 'Descartar captura',
      message:
        `Se va a descartar la captura de tipo "${operacion.tipo}" del ${operacion.capturadoAtDispositivo}. ` +
        `No llegó al servidor y no está guardada en ningún otro lado: si la descartás, se pierde. ` +
        `Copiala antes si vas a rehacerla a mano.`,
      type: 'error',
      confirmText: 'Descartar'
    });
    if (!confirmado) return;

    await this.outbox.descartar(operacion.clientOpId);
    await this.recargar();
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

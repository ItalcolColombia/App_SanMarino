// features/sincronizacion-panama/pages/sincronizacion-panama-page/sincronizacion-panama-page.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { CompanySelectorComponent } from '../../../../shared/components/company-selector/company-selector.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';

import { SincronizacionPanamaService } from '../../services/sincronizacion-panama.service';
import { ResultadoSincronizacionComponent } from '../../components/resultado-sincronizacion/resultado-sincronizacion.component';
import {
  PanamaCliente, PanamaGranja, ProbarConexionResult, ResultadoSincronizacionDto,
  SincronizacionHistorialDetalleDto, SincronizacionHistorialPagedDto
} from '../../models/sincronizacion-panama.model';
import { construirRequest, ConexionFormValues, FiltrosFormValues } from '../../funciones/construir-request.funcion';
import { construirConfirmacion } from '../../funciones/construir-confirmacion.funcion';
import { badgeEstadoResultado } from '../../funciones/estado-lote.funcion';
import { fechaCorta } from '../../../../shared/utils/format';

/** URL Swagger del origen, prellenada por defecto (editable). */
const DEFAULT_BASE_URL = 'https://italapp.italcol.com/ZooPanamaPollo';

/**
 * Página orquestadora del puente de integración Panamá (ZooPanamaPollo → Pollo Engorde).
 * SIGNALS + OnPush: el interceptor cifra el SECRET_UP con Web Crypto, por lo que las respuestas HTTP
 * emiten FUERA de la zona de Angular; con signals el refresco de vista no depende de Zone.js. El
 * componente es delgado: arma request (funciones puras), llama al servicio y maneja estado/UI.
 */
@Component({
  selector: 'app-sincronizacion-panama-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, CompanySelectorComponent, ResultadoSincronizacionComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sincronizacion-panama-page.component.html',
  styleUrl: './sincronizacion-panama-page.component.scss'
})
export class SincronizacionPanamaPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly svc = inject(SincronizacionPanamaService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly destroyRef = inject(DestroyRef);

  /** Conexión al origen. Vacíos → el backend usa su configuración. */
  readonly conexionForm = this.fb.nonNullable.group({
    baseUrl: DEFAULT_BASE_URL,
    email: '',
    password: ''
  });

  /** Filtros de la corrida. */
  readonly filtrosForm = this.fb.group({
    anio: this.fb.control<number | null>(null),
    clienteIdOrigen: this.fb.control<number | null>(null),
    granjaIdOrigen: this.fb.control<number | null>(null),
    fechaHasta: this.fb.nonNullable.control<string>(''),
    geneticaRaza: this.fb.nonNullable.control<string>(''),
    geneticaAnio: this.fb.control<number | null>(null),
    // ⚠ Debe arrancar en TRUE: con false el backend no importa la guía del origen y TODOS los lotes
    // quedan "Pendiente (sin guía)" sin crearse (causa raíz de la corrida del 2026-07-16 con 287 pendientes).
    importarGuiaGenetica: this.fb.nonNullable.control<boolean>(true),
    /** Red de seguridad: crea una guía de PRUEBA (marcada FAKE) si la real falta y el origen no responde. */
    crearGuiaFakeSiFalta: this.fb.nonNullable.control<boolean>(true)
  });

  // ── Estado async (signals: refrescan la vista aunque el HTTP emita fuera de zona) ──
  readonly clientes = signal<PanamaCliente[]>([]);
  readonly granjas = signal<PanamaGranja[]>([]);

  readonly probando = signal(false);
  readonly cargandoClientes = signal(false);
  readonly cargandoGranjas = signal(false);
  readonly previsualizando = signal(false);
  readonly sincronizando = signal(false);

  readonly testResult = signal<ProbarConexionResult | null>(null);
  readonly resultado = signal<ResultadoSincronizacionDto | null>(null);

  // ── Historial de corridas ──
  readonly historial = signal<SincronizacionHistorialPagedDto | null>(null);
  readonly cargandoHistorial = signal(false);
  readonly incluirValidaciones = signal(true);
  /** Detalle de la corrida seleccionada del historial (reutiliza el componente de resultado). */
  readonly detalleHistorial = signal<SincronizacionHistorialDetalleDto | null>(null);
  /** Id de la corrida cuyo detalle se está cargando (spinner por fila). */
  readonly cargandoDetalleId = signal<number | null>(null);

  /** Cualquier corrida real/dry-run en curso: bloquea acciones concurrentes. */
  readonly procesando = computed(() => this.previsualizando() || this.sincronizando());

  /** Banner de confirmación de la última corrida (función pura, memoizada). */
  readonly confirmacion = computed(() => {
    const r = this.resultado();
    return r ? construirConfirmacion(r) : null;
  });

  /** Filas del historial con badge de estado precomputado (referencia estable). */
  readonly filasHistorial = computed(() =>
    (this.historial()?.items ?? []).map((h) => ({ h, badge: badgeEstadoResultado(h.estado) }))
  );

  readonly totalPaginasHistorial = computed(() => {
    const p = this.historial();
    return p ? Math.max(1, Math.ceil(p.total / p.pageSize)) : 1;
  });

  constructor() {
    // Al cambiar el cliente, reseteo la granja y recargo el listado dependiente.
    this.filtrosForm.controls.clienteIdOrigen.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.filtrosForm.controls.granjaIdOrigen.setValue(null);
        this.granjas.set([]);
      });

    this.cargarHistorial(1);
  }

  // ── Lecturas del formulario ──
  private getConexion(): ConexionFormValues {
    return this.conexionForm.getRawValue();
  }
  private getFiltros(): FiltrosFormValues {
    return this.filtrosForm.getRawValue() as FiltrosFormValues;
  }

  // ── 1) Conexión ──
  probarConexion(): void {
    this.probando.set(true);
    this.testResult.set(null);
    this.svc.probarConexion(this.getConexion())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.probando.set(false);
          this.testResult.set(r);
          if (r.ok) this.toast.success(r.mensaje ?? 'Conexión establecida con el origen.');
          else this.toast.error(r.mensaje ?? 'No se pudo conectar con el origen.');
        },
        error: (e) => {
          this.probando.set(false);
          const msg = this.mensajeError(e, 'No se pudo probar la conexión.');
          this.testResult.set({ ok: false, mensaje: msg });
          this.toast.error(msg);
        }
      });
  }

  // ── 2) Filtros: poblar dropdowns desde el origen ──
  cargarClientes(): void {
    this.cargandoClientes.set(true);
    this.svc.clientes(this.getConexion())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.cargandoClientes.set(false);
          this.clientes.set(list ?? []);
          if (!list?.length) this.toast.info('El origen no devolvió clientes.');
        },
        error: (e) => {
          this.cargandoClientes.set(false);
          this.toast.error(this.mensajeError(e, 'No se pudieron cargar los clientes.'));
        }
      });
  }

  cargarGranjas(): void {
    const clienteId = this.filtrosForm.controls.clienteIdOrigen.value;
    this.cargandoGranjas.set(true);
    this.svc.granjas(this.getConexion(), clienteId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.cargandoGranjas.set(false);
          this.granjas.set(list ?? []);
          if (!list?.length) this.toast.info('El origen no devolvió granjas para el filtro actual.');
        },
        error: (e) => {
          this.cargandoGranjas.set(false);
          this.toast.error(this.mensajeError(e, 'No se pudieron cargar las granjas.'));
        }
      });
  }

  // ── 3) Previsualizar (dry-run) ──
  previsualizar(): void {
    if (this.procesando()) return;
    this.previsualizando.set(true);
    this.resultado.set(null);
    const req = construirRequest(this.getConexion(), this.getFiltros(), true);
    this.svc.previsualizar(req)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.previsualizando.set(false);
          this.resultado.set(r);
          this.toast.success(`Previsualización lista: ${r.lotes?.length ?? 0} lote(s) analizado(s).`);
          this.cargarHistorial(1); // la validación también queda auditada
        },
        error: (e) => {
          this.previsualizando.set(false);
          this.toast.error(this.mensajeError(e, 'No se pudo previsualizar la sincronización.'));
        }
      });
  }

  // ── 4) Sincronizar (real) ──
  async sincronizar(): Promise<void> {
    if (this.procesando()) return;

    const filtros = this.getFiltros();
    // La raza global es OPCIONAL: sin ella el backend usa la línea genética que trae cada lote del
    // origen (ROSS 308 AP / COBB 500). Solo se pide confirmación para que sea una decisión consciente.
    if (!filtros.geneticaRaza.trim()) {
      const seguir = await this.confirmDialog.ask({
        title: 'Sin raza global',
        message: 'No indicaste una raza global: cada lote usará la línea genética que traiga del origen (ej. ROSS 308 AP). ¿Continuar así?',
        type: 'warning',
        confirmText: 'Continuar'
      });
      if (!seguir) return;
    }

    const anioTxt = filtros.anio != null ? `del año ${filtros.anio}` : 'de todos los años';
    const ok = await this.confirmDialog.ask({
      title: 'Confirmar sincronización',
      message: `Vas a insertar/actualizar en la empresa activa los datos ${anioTxt} traídos desde ZooPanamaPollo. Esta acción modifica la base de datos. ¿Continuar?`,
      type: 'warning',
      confirmText: 'Sincronizar'
    });
    if (!ok) return;

    this.sincronizando.set(true);
    this.resultado.set(null);
    const req = construirRequest(this.getConexion(), filtros, false);
    this.svc.sincronizar(req)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.sincronizando.set(false);
          this.resultado.set(r);
          if (r.lotesConError > 0) {
            this.toast.warning(`Sincronización con ${r.lotesConError} lote(s) con error. Revisá el detalle.`);
          } else if (r.lotesPendientes > 0) {
            this.toast.warning(`Sincronización terminada, pero ${r.lotesPendientes} lote(s) quedaron PENDIENTES sin crear. Revisá los mensajes del resultado.`);
          } else {
            this.toast.success(`Sincronización completada en ${r.duracionMs} ms.`);
          }
          this.cargarHistorial(1); // refresca el historial con la corrida recién auditada
        },
        error: (e) => {
          this.sincronizando.set(false);
          this.toast.error(this.mensajeError(e, 'La sincronización falló.'));
        }
      });
  }

  // ── 5) Historial de sincronizaciones ──
  cargarHistorial(page?: number): void {
    const destino = page ?? this.historial()?.page ?? 1;
    this.cargandoHistorial.set(true);
    this.svc.historial(destino, 10, this.incluirValidaciones())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (p) => {
          this.cargandoHistorial.set(false);
          this.historial.set(p);
        },
        error: (e) => {
          this.cargandoHistorial.set(false);
          // 404 = backend sin la mejora de historial todavía (falta reiniciar); no ensuciar con toasts rojos.
          if ((e as { status?: number })?.status !== 404) {
            this.toast.error(this.mensajeError(e, 'No se pudo cargar el historial de sincronizaciones.'));
          }
        }
      });
  }

  toggleValidaciones(incluir: boolean): void {
    this.incluirValidaciones.set(incluir);
    this.cargarHistorial(1);
  }

  verDetalleHistorial(id: number): void {
    if (this.cargandoDetalleId() != null) return;
    this.cargandoDetalleId.set(id);
    this.svc.historialDetalle(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (d) => {
          this.cargandoDetalleId.set(null);
          this.detalleHistorial.set(d);
          if (!d.resultado) {
            this.toast.info('Esta corrida es anterior a la mejora de historial: solo hay metadatos, sin detalle completo.');
          }
        },
        error: (e) => {
          this.cargandoDetalleId.set(null);
          this.toast.error(this.mensajeError(e, 'No se pudo cargar el detalle de la corrida.'));
        }
      });
  }

  cerrarDetalleHistorial(): void {
    this.detalleHistorial.set(null);
  }

  /** Fecha corta para la tabla del historial (delegación al formato central). */
  fechaHistorial(iso: string | null | undefined): string {
    return fechaCorta(iso);
  }

  /** Mensajes por status HTTP; prioriza el `error.message`/`error.error` que mande el backend. */
  private mensajeError(e: unknown, fallback: string): string {
    const err = e as { status?: number; error?: { message?: string; error?: string } };
    if (err?.status === 401) return 'Sesión expirada. Iniciá sesión nuevamente.';
    if (err?.status === 0) return 'Sin conexión con el servidor. Verificá tu red e intentá de nuevo.';
    if (err?.status === 400 && (err.error?.message || err.error?.error)) {
      return err.error?.message ?? err.error?.error ?? fallback;
    }
    if (err?.status === 502 || err?.status === 504) return 'El origen (ZooPanamaPollo) no respondió a tiempo.';
    if (err?.status === 500) return 'Error interno del servidor al procesar la sincronización.';
    return err?.error?.message ?? err?.error?.error ?? fallback;
  }
}

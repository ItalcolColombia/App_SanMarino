// src/app/features/tickets/components/tareas-panel/tareas-panel.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, Input, OnInit, computed, inject, signal } from '@angular/core';
import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketTareaService } from '../../services/ticket-tarea.service';
import {
  COLUMNAS_TAREA, CreateTicketTareaRequest, EstadoTarea, PRIORIDAD_ACENTO,
  TAREA_ESTADO_DOT, TAREA_ESTADO_LABEL, TAREA_TIPO_COLOR, TAREA_TIPO_LABEL,
  TicketTarea, UpdateTicketTareaRequest,
} from '../../models/ticket-tarea.models';
import { OpcionAsignable, TareaModalComponent } from '../tarea-modal/tarea-modal.component';
import { TicketPrioridadBadgeComponent } from '../ticket-prioridad-badge/ticket-prioridad-badge.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';

/** Columna del tablero de tareas con sus tarjetas ya agrupadas. */
interface ColumnaTareas { estado: EstadoTarea; label: string; dot: string; tareas: TicketTarea[]; }

/**
 * Tablero de tareas de un caso: columnas con drag & drop (CDK), alta/edición por modal y
 * subtareas anidadas. Estado mutable + subscribe ⇒ `Eager` es obligatorio (en Angular 22
 * omitir `changeDetection` equivale a OnPush y el panel se quedaría colgado).
 */
@Component({
  selector: 'app-tareas-panel',
  standalone: true,
  imports: [CdkDropListGroup, CdkDropList, CdkDrag, TareaModalComponent, TicketPrioridadBadgeComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './tareas-panel.component.html',
})
export class TareasPanelComponent implements OnInit {
  @Input({ required: true }) ticketId!: number;
  /** Solo con permiso de gestión se puede crear, mover o borrar; el solicitante mira. */
  @Input() puedeGestionar = false;
  @Input() asignables: OpcionAsignable[] = [];

  private readonly svc = inject(TicketTareaService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly destroyRef = inject(DestroyRef);

  readonly tareas = signal<TicketTarea[]>([]);
  readonly cargando = signal(false);
  readonly guardando = signal(false);

  readonly estadoLabel = TAREA_ESTADO_LABEL;
  readonly estadoDot = TAREA_ESTADO_DOT;
  readonly tipoColor = TAREA_TIPO_COLOR;
  readonly tipoLabel = TAREA_TIPO_LABEL;
  readonly prioridadAcento = PRIORIDAD_ACENTO;

  /** Vista compacta (lista) o tablero. La lista es más cómoda dentro del detalle. */
  vista: 'lista' | 'tablero' = 'lista';

  // Modal
  mostrarModal = false;
  tareaEnEdicion: TicketTarea | null = null;
  estadoInicialModal: EstadoTarea = 'BACKLOG';
  parentTareaId: number | null = null;
  parentTitulo: string | null = null;

  /** Ids de las listas CDK — el grupo permite arrastrar entre columnas. */
  readonly idsColumnas = COLUMNAS_TAREA.map(c => `col-${c}`);

  /**
   * Columnas derivadas del listado. Se memoiza con `computed` para no devolver arrays nuevos
   * en cada ciclo de detección (rompería la estabilidad de referencias del template).
   */
  readonly columnas = computed<ColumnaTareas[]>(() => {
    const todas = this.tareas();
    return COLUMNAS_TAREA.map(estado => ({
      estado,
      label: TAREA_ESTADO_LABEL[estado],
      dot: TAREA_ESTADO_DOT[estado],
      tareas: todas.filter(t => t.estado === estado).sort((a, b) => a.orden - b.orden),
    }));
  });

  /** Tareas de primer nivel con sus subtareas colgando, para la vista de lista. */
  readonly arbol = computed<{ padre: TicketTarea; hijas: TicketTarea[] }[]>(() => {
    const todas = this.tareas();
    return todas
      .filter(t => !t.parentTareaId)
      .map(padre => ({ padre, hijas: todas.filter(h => h.parentTareaId === padre.id) }));
  });

  readonly total = computed(() => this.tareas().length);
  readonly listas = computed(() => this.tareas().filter(t => t.estado === 'LISTO').length);
  readonly avance = computed(() => {
    const total = this.total();
    return total === 0 ? 0 : Math.round((this.listas() * 100) / total);
  });
  readonly horasEstimadas = computed(() =>
    this.tareas().reduce((acc, t) => acc + (t.horasEstimadas ?? 0), 0));
  readonly horasRegistradas = computed(() =>
    this.tareas().reduce((acc, t) => acc + (t.horasRegistradas ?? 0), 0));

  ngOnInit(): void { this.cargar(); }

  cargar(): void {
    this.cargando.set(true);
    this.svc.listar(this.ticketId)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.cargando.set(false)))
      .subscribe({
        next: t => this.tareas.set(t),
        error: () => this.toast.error('No se pudieron cargar las tareas del caso.'),
      });
  }

  // ── Modal ────────────────────────────────────────────────────
  abrirNueva(estado: EstadoTarea = 'BACKLOG'): void {
    this.tareaEnEdicion = null;
    this.estadoInicialModal = estado;
    this.parentTareaId = null;
    this.parentTitulo = null;
    this.mostrarModal = true;
  }

  abrirSubtarea(padre: TicketTarea): void {
    this.tareaEnEdicion = null;
    this.estadoInicialModal = 'BACKLOG';
    this.parentTareaId = padre.id;
    this.parentTitulo = padre.titulo;
    this.mostrarModal = true;
  }

  abrirEdicion(tarea: TicketTarea): void {
    this.tareaEnEdicion = tarea;
    this.parentTareaId = null;
    this.parentTitulo = null;
    this.mostrarModal = true;
  }

  cerrarModal(): void {
    this.mostrarModal = false;
    this.tareaEnEdicion = null;
  }

  onGuardar(req: CreateTicketTareaRequest | UpdateTicketTareaRequest): void {
    this.guardando.set(true);
    const enEdicion = this.tareaEnEdicion;
    const peticion$ = enEdicion
      ? this.svc.editar(this.ticketId, enEdicion.id, req as UpdateTicketTareaRequest)
      : this.svc.crear(this.ticketId, req as CreateTicketTareaRequest);

    peticion$.pipe(finalize(() => this.guardando.set(false))).subscribe({
      next: () => {
        this.toast.success(enEdicion ? 'Tarea actualizada.' : 'Tarea creada.');
        this.cerrarModal();
        this.cargar();
      },
      error: e => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo guardar la tarea.'),
    });
  }

  async eliminar(tarea: TicketTarea): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar tarea',
      message: `¿Eliminar «${tarea.titulo}»? Los tiempos registrados se conservan en el caso.`,
      type: 'error',
      confirmText: 'Eliminar',
    });
    if (!ok) return;

    this.svc.eliminar(this.ticketId, tarea.id).subscribe({
      next: () => { this.toast.success('Tarea eliminada.'); this.cargar(); },
      error: e => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo eliminar la tarea.'),
    });
  }

  // ── Drag & drop ──────────────────────────────────────────────
  soltar(evento: CdkDragDrop<ColumnaTareas>): void {
    if (!this.puedeGestionar) return;

    const tarea: TicketTarea = evento.item.data;
    const destino = evento.container.data.estado;
    const mismaColumna = evento.previousContainer === evento.container;
    if (mismaColumna && evento.previousIndex === evento.currentIndex) return;

    // Estado previo para revertir si el servidor rechaza el movimiento.
    const anterior = this.tareas();

    // Movimiento optimista: la tarjeta queda donde la soltaron sin esperar la respuesta.
    this.aplicarMovimientoLocal(tarea.id, destino, evento.currentIndex);

    this.svc.mover(this.ticketId, tarea.id, { estado: destino, indice: evento.currentIndex })
      .subscribe({
        next: t => this.tareas.set(t),
        error: e => {
          this.tareas.set(anterior);
          this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo mover la tarea.');
        },
      });
  }

  /** Reordena en memoria igual que lo hará el backend, para que la UI no parpadee. */
  private aplicarMovimientoLocal(tareaId: number, destino: EstadoTarea, indice: number): void {
    const todas = [...this.tareas()];
    const movida = todas.find(t => t.id === tareaId);
    if (!movida) return;

    const origen = movida.estado;
    const enDestino = todas
      .filter(t => t.id !== tareaId && t.estado === destino)
      .sort((a, b) => a.orden - b.orden);
    enDestino.splice(Math.max(0, Math.min(indice, enDestino.length)), 0, { ...movida, estado: destino });

    const actualizadas = todas.map(t => {
      if (t.id === tareaId) {
        return { ...t, estado: destino, orden: enDestino.findIndex(x => x.id === tareaId) };
      }
      if (t.estado === destino) {
        const idx = enDestino.findIndex(x => x.id === t.id);
        return idx >= 0 ? { ...t, orden: idx } : t;
      }
      return t;
    });

    if (origen !== destino) {
      const enOrigen = actualizadas
        .filter(t => t.id !== tareaId && t.estado === origen)
        .sort((a, b) => a.orden - b.orden);
      enOrigen.forEach((t, i) => { t.orden = i; });
    }

    this.tareas.set(actualizadas);
  }

  // ── Helpers de plantilla ─────────────────────────────────────
  iniciales(nombre: string | null): string {
    if (!nombre) return '?';
    return nombre.trim().split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }

  etiquetasDe(tarea: TicketTarea): string[] {
    return (tarea.etiquetas ?? '').split(',').map(e => e.trim()).filter(Boolean);
  }
}

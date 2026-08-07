// src/app/features/italjira/pages/backlog/backlog.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { ItalJiraService } from '../../services/italjira.service';
import { TicketPerfilService } from '../../../tickets/services/ticket-perfil.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import {
  HistoriaModalComponent, type OpcionResponsable,
} from '../../components/historia-modal/historia-modal.component';
import {
  TareaModalComponent, type OpcionAsignable,
} from '../../../tickets/components/tarea-modal/tarea-modal.component';
import {
  TAREA_ESTADO_DOT, TAREA_ESTADO_LABEL, TAREA_TIPO_COLOR, TAREA_TIPO_LABEL,
  PRIORIDAD_ACENTO, PRIORIDAD_LABEL, COLUMNAS_TAREA,
  type CreateHistoriaRequest, type Historia, type HistoriaDetalle, type ItalJiraBacklog,
  type ItalJiraCaso, type ItalJiraFiltro, type UpdateHistoriaRequest,
} from '../../models/historia.models';
import type {
  CreateTicketTareaRequest, TicketTarea, UpdateTicketTareaRequest,
} from '../../../tickets/models/ticket-tarea.models';
import { armarArbolTareas, type NodoTarea } from '../../funciones/armar-arbol-backlog.funcion';
import { exportarBacklogExcel } from '../../funciones/exportar-backlog-excel.funcion';

/** Una historia con su árbol ya armado, listo para el template. */
interface FilaHistoria {
  historia: Historia;
  nodos: NodoTarea[];
  casos: ItalJiraCaso[];
}

/**
 * Backlog de ItalJira: el árbol **historia → tarea → subtarea/bug**, más la bandeja «sin historia»
 * donde caen los casos que registran los usuarios y las tareas todavía sin agrupar.
 *
 * Estado mutable + `subscribe` ⇒ `Eager` obligatorio (en Angular 22 omitir `changeDetection`
 * equivale a OnPush y deja la pantalla colgada en «Cargando…» aunque la red haya devuelto 200).
 */
@Component({
  selector: 'app-italjira-backlog',
  standalone: true,
  imports: [FormsModule, RouterLink, HistoriaModalComponent, TareaModalComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './backlog.component.html',
})
export class BacklogComponent implements OnInit {
  private readonly svc = inject(ItalJiraService);
  private readonly perfiles = inject(TicketPerfilService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly destroyRef = inject(DestroyRef);

  readonly data = signal<ItalJiraBacklog | null>(null);
  readonly filas = signal<FilaHistoria[]>([]);
  /** Bandeja de sueltas, ya anidada: el backend las manda planas (raíces + subtareas). */
  readonly nodosSueltos = signal<NodoTarea[]>([]);
  readonly cargando = signal(false);
  readonly guardando = signal(false);

  /** Ids de las historias desplegadas. */
  readonly expandidas = signal<Set<number>>(new Set());
  /** Ids de las tareas con sus subtareas desplegadas. */
  readonly tareasAbiertas = signal<Set<number>>(new Set());
  readonly entradaAbierta = signal(true);

  readonly estadoLabel = TAREA_ESTADO_LABEL;
  readonly estadoDot = TAREA_ESTADO_DOT;
  readonly tipoLabel = TAREA_TIPO_LABEL;
  readonly tipoColor = TAREA_TIPO_COLOR;
  readonly prioridadLabel = PRIORIDAD_LABEL;
  readonly prioridadAcento = PRIORIDAD_ACENTO;
  readonly columnas = COLUMNAS_TAREA;

  // Filtros de la barra superior
  texto = '';
  estadoFiltro = '';
  incluirTerminadas = true;

  // Modales
  readonly modalHistoria = signal(false);
  readonly historiaEnEdicion = signal<Historia | null>(null);
  readonly modalTarea = signal(false);
  readonly tareaEnEdicion = signal<TicketTarea | null>(null);
  private historiaDestino: number | null = null;
  readonly parentTareaId = signal<number | null>(null);
  readonly parentTitulo = signal<string | null>(null);

  readonly responsables = signal<OpcionResponsable[]>([]);

  /** Las opciones del modal de tarea son las mismas personas: un solo origen, dos formas. */
  get asignables(): OpcionAsignable[] { return this.responsables(); }

  ngOnInit(): void {
    this.cargar();
    this.cargarResponsables();
  }

  // ───────────────────────────── Carga ─────────────────────────────

  cargar(): void {
    this.cargando.set(true);

    const filtro: ItalJiraFiltro = {
      texto: this.texto.trim() || undefined,
      estado: this.estadoFiltro || undefined,
      incluirTerminadas: this.incluirTerminadas,
    };

    this.svc.backlog(filtro)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.cargando.set(false)))
      .subscribe({
        next: b => {
          this.data.set(b);
          this.filas.set(b.historias.map(d => this.aFila(d)));
          this.nodosSueltos.set(armarArbolTareas(b.tareasSinHistoria));
        },
        error: () => this.toast.error('No se pudo cargar el backlog.'),
      });
  }

  private aFila(detalle: HistoriaDetalle): FilaHistoria {
    return {
      historia: detalle.historia,
      nodos: armarArbolTareas(detalle.tareas),
      casos: detalle.casos,
    };
  }

  private cargarResponsables(): void {
    this.perfiles.getAsignables('DESARROLLO')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: a => this.responsables.set(
          a.map(x => ({ guid: x.userId, nombre: x.nombreCompleto }))),
        error: () => { /* sin responsables el módulo sigue usable: se asigna después */ },
      });
  }

  // ───────────────────────────── Despliegue ─────────────────────────────

  estaExpandida(id: number): boolean { return this.expandidas().has(id); }

  alternarHistoria(id: number): void {
    // Set NUEVO en cada cambio: mutar el existente no cambia la referencia y la vista no repinta.
    const set = new Set(this.expandidas());
    set.has(id) ? set.delete(id) : set.add(id);
    this.expandidas.set(set);
  }

  estaAbierta(tareaId: number): boolean { return this.tareasAbiertas().has(tareaId); }

  alternarTarea(tareaId: number): void {
    const set = new Set(this.tareasAbiertas());
    set.has(tareaId) ? set.delete(tareaId) : set.add(tareaId);
    this.tareasAbiertas.set(set);
  }

  expandirTodo(): void {
    this.expandidas.set(new Set(this.filas().map(f => f.historia.id)));
  }

  colapsarTodo(): void {
    this.expandidas.set(new Set());
    this.tareasAbiertas.set(new Set());
  }

  // ───────────────────────────── Historias ─────────────────────────────

  nuevaHistoria(): void {
    this.historiaEnEdicion.set(null);
    this.modalHistoria.set(true);
  }

  editarHistoria(h: Historia): void {
    this.historiaEnEdicion.set(h);
    this.modalHistoria.set(true);
  }

  cerrarModalHistoria(): void {
    this.modalHistoria.set(false);
    this.historiaEnEdicion.set(null);
  }

  guardarHistoria(req: CreateHistoriaRequest | UpdateHistoriaRequest): void {
    const enEdicion = this.historiaEnEdicion();
    this.guardando.set(true);

    const peticion = enEdicion
      ? this.svc.editarHistoria(enEdicion.id, req as UpdateHistoriaRequest)
      : this.svc.crearHistoria(req as CreateHistoriaRequest);

    peticion
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.guardando.set(false)))
      .subscribe({
        next: () => {
          this.toast.success(enEdicion ? 'Historia actualizada.' : 'Historia creada.');
          this.cerrarModalHistoria();
          this.cargar();
        },
        error: e => this.toast.error(e?.error ?? 'No se pudo guardar la historia.'),
      });
  }

  async eliminarHistoria(h: Historia): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar historia',
      message: `¿Eliminar «${h.titulo}»? Sus tareas y casos NO se borran: vuelven a la bandeja «sin historia».`,
      type: 'error',
      confirmText: 'Eliminar',
    });
    if (!ok) return;

    this.svc.eliminarHistoria(h.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => { this.toast.success('Historia eliminada.'); this.cargar(); },
        error: e => this.toast.error(e?.error ?? 'No se pudo eliminar la historia.'),
      });
  }

  // ───────────────────────────── Tareas ─────────────────────────────

  nuevaTarea(historiaId: number | null): void {
    this.tareaEnEdicion.set(null);
    this.historiaDestino = historiaId;
    this.parentTareaId.set(null);
    this.parentTitulo.set(null);
    this.modalTarea.set(true);
  }

  nuevaSubtarea(padre: TicketTarea): void {
    this.tareaEnEdicion.set(null);
    this.historiaDestino = padre.historiaId;
    this.parentTareaId.set(padre.id);
    this.parentTitulo.set(padre.titulo);
    this.modalTarea.set(true);
  }

  editarTarea(t: TicketTarea): void {
    this.tareaEnEdicion.set(t);
    this.historiaDestino = t.historiaId;
    this.parentTareaId.set(null);
    this.parentTitulo.set(null);
    this.modalTarea.set(true);
  }

  cerrarModalTarea(): void {
    this.modalTarea.set(false);
    this.tareaEnEdicion.set(null);
    this.parentTareaId.set(null);
    this.parentTitulo.set(null);
  }

  guardarTarea(req: CreateTicketTareaRequest | UpdateTicketTareaRequest): void {
    const enEdicion = this.tareaEnEdicion();
    this.guardando.set(true);

    // El modal es el mismo que usa el detalle del caso; ItalJira solo agrega a qué historia entra.
    const peticion = enEdicion
      ? this.svc.editarTarea(enEdicion.id, req as UpdateTicketTareaRequest)
      : this.svc.crearTarea({ ...(req as CreateTicketTareaRequest), historiaId: this.historiaDestino });

    peticion
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.guardando.set(false)))
      .subscribe({
        next: () => {
          this.toast.success(enEdicion ? 'Tarea actualizada.' : 'Tarea creada.');
          this.cerrarModalTarea();
          this.cargar();
        },
        error: e => this.toast.error(e?.error ?? 'No se pudo guardar la tarea.'),
      });
  }

  async eliminarTarea(t: TicketTarea): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar tarea',
      message: `¿Eliminar «${t.titulo}»?`,
      type: 'error',
      confirmText: 'Eliminar',
    });
    if (!ok) return;

    this.svc.eliminarTarea(t.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => { this.toast.success('Tarea eliminada.'); this.cargar(); },
        error: e => this.toast.error(e?.error ?? 'No se pudo eliminar la tarea.'),
      });
  }

  // ───────────────────────── Agrupar trabajo existente ─────────────────────────

  moverCasoAHistoria(caso: ItalJiraCaso, valor: string): void {
    const historiaId = valor ? Number(valor) : null;

    this.svc.asignarCaso(caso.id, { historiaId })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(historiaId ? 'Caso movido a la historia.' : 'Caso devuelto a la bandeja.');
          this.cargar();
        },
        error: e => this.toast.error(e?.error ?? 'No se pudo mover el caso.'),
      });
  }

  moverTareaAHistoria(tarea: TicketTarea, valor: string): void {
    const historiaId = valor ? Number(valor) : null;

    this.svc.asignarTarea(tarea.id, { historiaId })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(historiaId ? 'Tarea movida a la historia.' : 'Tarea devuelta a la bandeja.');
          this.cargar();
        },
        error: e => this.toast.error(e?.error ?? 'No se pudo mover la tarea.'),
      });
  }

  /** Cambio rápido de columna sin abrir el modal (el tablero completo vive en su propia vista). */
  cambiarEstadoTarea(tarea: TicketTarea, estado: string): void {
    this.svc.editarTarea(tarea.id, { estado: estado as TicketTarea['estado'] })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.cargar(),
        error: e => this.toast.error(e?.error ?? 'No se pudo mover la tarea de columna.'),
      });
  }

  // ───────────────────────────── Utilidades de vista ─────────────────────────────

  limpiarFiltros(): void {
    this.texto = '';
    this.estadoFiltro = '';
    this.incluirTerminadas = true;
    this.cargar();
  }

  exportar(): void {
    const b = this.data();
    if (!b) return;

    exportarBacklogExcel(b);
    this.toast.success(
      `${b.historias.length} historia(s) y ${b.casosSinHistoria.length} caso(s) sin historia exportados.`);
  }

  /** Color de la barra de avance: verde al terminar, ámbar en curso, gris sin empezar. */
  colorAvance(pct: number): string {
    if (pct >= 100) return 'bg-emerald-500';
    if (pct > 0) return 'bg-ital-orange';
    return 'bg-slate-300';
  }

  /** Opciones del selector «mover a historia» (todas las historias cargadas). */
  get opcionesHistoria(): Historia[] {
    return this.filas().map(f => f.historia);
  }
}

// src/app/features/implementacion/pages/plan-detail/plan-detail.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ModalTareaImplementacionComponent } from '../../components/modal-tarea/modal-tarea.component';
import { ModalParticipantesImplementacionComponent } from '../../components/modal-participantes/modal-participantes.component';
import { ModalFirmasImplementacionComponent } from '../../components/modal-firmas/modal-firmas.component';
import {
  agruparTareasPorCategoria,
  GrupoCategoria,
  trackByGrupo,
  trackByTarea,
} from '../../funciones/agrupar-tareas-por-categoria.funcion';
import {
  estiloEstadoPlan,
  estiloEstadoTarea,
  estiloTipoPlan,
} from '../../funciones/estado-tarea.funcion';
import {
  FILTROS_TAREAS_VACIOS,
  FiltrosTareas,
  filtrarTareas,
  hayFiltrosTareas,
} from '../../funciones/filtrar-tareas.funcion';
import {
  mensajeErrorHttp,
  RESUMEN_FIRMAS_VACIO,
  ResumenFirmas,
  resumenFirmas,
} from '../../funciones/resumen-firmas.funcion';
import {
  ImplementacionPlanDto,
  ImplementacionRolAsignableDto,
  ImplementacionTareaDto,
  ImplementacionUsuarioAsignableDto,
} from '../../models/implementacion.models';

@Component({
  selector: 'app-implementacion-plan-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ModalTareaImplementacionComponent,
    ModalParticipantesImplementacionComponent,
    ModalFirmasImplementacionComponent,
  ],
  templateUrl: './plan-detail.page.html',
  styleUrls: ['../../styles/implementacion-shared.scss'],
})
export class PlanDetailPage implements OnInit {
  readonly trackByGrupo = trackByGrupo;
  readonly trackByTarea = trackByTarea;
  readonly estiloPlan = estiloEstadoPlan;
  readonly estiloTarea = estiloEstadoTarea;
  readonly estiloTipo = estiloTipoPlan;

  planId = 0;
  plan: ImplementacionPlanDto | null = null;
  /** Todas las tareas del plan (sin filtrar); los filtros trabajan client-side sobre esto. */
  tareas: ImplementacionTareaDto[] = [];
  grupos: GrupoCategoria[] = [];
  categoriasExistentes: string[] = [];
  usuarios: ImplementacionUsuarioAsignableDto[] = [];
  roles: ImplementacionRolAsignableDto[] = [];

  filtros: FiltrosTareas = { ...FILTROS_TAREAS_VACIOS };

  /** Resúmenes de firmas memoizados por tarea (referencias estables para el template). */
  private resumenPorTarea = new Map<number, ResumenFirmas>();
  firmasPlan: ResumenFirmas = RESUMEN_FIRMAS_VACIO;

  cargando = false;
  errorMsg: string | null = null;

  modalTareaAbierto = false;
  tareaEditando: ImplementacionTareaDto | null = null;

  modalParticipantesAbierto = false;
  modalFirmasAbierto = false;
  tareaSeleccionada: ImplementacionTareaDto | null = null;

  constructor(
    private route: ActivatedRoute,
    private svc: ImplementacionService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService
  ) {}

  async ngOnInit(): Promise<void> {
    this.planId = Number(this.route.snapshot.paramMap.get('id'));
    await Promise.all([this.cargar(), this.cargarCombosAsignacion()]);
  }

  async cargar(): Promise<void> {
    this.cargando = true;
    this.errorMsg = null;
    try {
      const detalle = await firstValueFrom(this.svc.getPlanDetalle(this.planId));
      this.plan = detalle.plan;
      // Backend anterior sin firmas → normalizar para no romper filtros/conteos.
      this.tareas = detalle.tareas.map((t) => ({ ...t, firmas: t.firmas ?? [] }));
      this.categoriasExistentes = [...new Set(this.tareas.map((t) => t.categoria))];
      this.resumenPorTarea = new Map(this.tareas.map((t) => [t.id, resumenFirmas(t.firmas)]));
      this.firmasPlan = resumenFirmas(this.tareas.flatMap((t) => t.firmas));
      this.aplicarFiltros();
    } catch (err: any) {
      this.errorMsg = mensajeErrorHttp(err, 'No se pudo cargar el cronograma de implementación.');
    } finally {
      this.cargando = false;
    }
  }

  private async cargarCombosAsignacion(): Promise<void> {
    try {
      const [usuarios, roles] = await Promise.all([
        firstValueFrom(this.svc.getUsuariosAsignables()),
        firstValueFrom(this.svc.getRolesAsignables()),
      ]);
      this.usuarios = usuarios;
      this.roles = roles;
    } catch {
      this.toast.warning('No se pudieron cargar usuarios/roles para asignación.');
    }
  }

  aplicarFiltros(): void {
    this.grupos = agruparTareasPorCategoria(filtrarTareas(this.tareas, this.filtros));
  }

  limpiarFiltros(): void {
    this.filtros = { ...FILTROS_TAREAS_VACIOS };
    this.aplicarFiltros();
  }

  get hayFiltros(): boolean {
    return hayFiltrosTareas(this.filtros);
  }

  /** Devuelve el resumen memoizado (referencia estable; no aloca en el ciclo de CD). */
  firmasDe(tareaId: number): ResumenFirmas {
    return this.resumenPorTarea.get(tareaId) ?? RESUMEN_FIRMAS_VACIO;
  }

  // ── Modales ────────────────────────────────────────────────────────────────

  abrirNuevaTarea(): void {
    this.tareaEditando = null;
    this.modalTareaAbierto = true;
  }

  abrirEditarTarea(tarea: ImplementacionTareaDto): void {
    this.tareaEditando = tarea;
    this.modalTareaAbierto = true;
  }

  abrirParticipantes(tarea: ImplementacionTareaDto): void {
    this.tareaSeleccionada = tarea;
    this.modalParticipantesAbierto = true;
  }

  abrirFirmas(tarea: ImplementacionTareaDto): void {
    this.tareaSeleccionada = tarea;
    this.modalFirmasAbierto = true;
  }

  cerrarModales(): void {
    this.modalTareaAbierto = false;
    this.modalParticipantesAbierto = false;
    this.modalFirmasAbierto = false;
    this.tareaEditando = null;
    this.tareaSeleccionada = null;
  }

  async onGuardado(): Promise<void> {
    this.cerrarModales();
    await this.cargar();
  }

  // ── Acciones de tarea ──────────────────────────────────────────────────────

  /** Check del gestor: la tarea queda completada con fecha y usuario. */
  async completar(tarea: ImplementacionTareaDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Marcar como completada',
      message: `¿Marcar "${tarea.titulo}" como completada? Quedará registrado tu usuario y la fecha; luego ${
        tarea.asignadoNombre ? tarea.asignadoNombre + ' deberá confirmarla' : 'el usuario asignado deberá confirmarla'
      } desde "Mis tareas".`,
      type: 'info',
      confirmText: 'Completar',
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.svc.completarTarea(tarea.id));
      this.toast.success('Ítem marcado como completado.');
      await this.cargar();
    } catch (err: any) {
      this.toast.error(mensajeErrorHttp(err, 'No se pudo completar el ítem.'));
    }
  }

  async reabrir(tarea: ImplementacionTareaDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Reabrir ítem',
      message: `¿Reabrir "${tarea.titulo}"? Se borra el check${tarea.estado === 'confirmada' ? ' y la confirmación' : ''} y vuelve a pendiente.`,
      type: 'warning',
      confirmText: 'Reabrir',
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.svc.reabrirTarea(tarea.id));
      this.toast.success('Ítem reabierto.');
      await this.cargar();
    } catch (err: any) {
      this.toast.error(mensajeErrorHttp(err, 'No se pudo reabrir el ítem.'));
    }
  }

  async eliminarTarea(tarea: ImplementacionTareaDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar ítem',
      message: `¿Eliminar "${tarea.titulo}" del checklist?`,
      type: 'error',
      confirmText: 'Eliminar',
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.svc.deleteTarea(tarea.id));
      this.toast.success('Ítem eliminado.');
      await this.cargar();
    } catch (err: any) {
      this.toast.error(mensajeErrorHttp(err, 'No se pudo eliminar el ítem.'));
    }
  }
}

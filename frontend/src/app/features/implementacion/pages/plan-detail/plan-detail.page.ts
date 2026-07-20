// src/app/features/implementacion/pages/plan-detail/plan-detail.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ModalTareaImplementacionComponent } from '../../components/modal-tarea/modal-tarea.component';
import {
  agruparTareasPorCategoria,
  GrupoCategoria,
  trackByGrupo,
  trackByTarea,
} from '../../funciones/agrupar-tareas-por-categoria.funcion';
import { estiloEstadoPlan, estiloEstadoTarea } from '../../funciones/estado-tarea.funcion';
import {
  ImplementacionPlanDto,
  ImplementacionRolAsignableDto,
  ImplementacionTareaDto,
  ImplementacionUsuarioAsignableDto,
} from '../../models/implementacion.models';

@Component({
  selector: 'app-implementacion-plan-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, ModalTareaImplementacionComponent],
  templateUrl: './plan-detail.page.html',
})
export class PlanDetailPage implements OnInit {
  readonly trackByGrupo = trackByGrupo;
  readonly trackByTarea = trackByTarea;
  readonly estiloPlan = estiloEstadoPlan;
  readonly estiloTarea = estiloEstadoTarea;

  planId = 0;
  plan: ImplementacionPlanDto | null = null;
  grupos: GrupoCategoria[] = [];
  categoriasExistentes: string[] = [];
  usuarios: ImplementacionUsuarioAsignableDto[] = [];
  roles: ImplementacionRolAsignableDto[] = [];

  cargando = false;
  modalAbierto = false;
  tareaEditando: ImplementacionTareaDto | null = null;

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
    try {
      const detalle = await firstValueFrom(this.svc.getPlanDetalle(this.planId));
      this.plan = detalle.plan;
      this.grupos = agruparTareasPorCategoria(detalle.tareas);
      this.categoriasExistentes = this.grupos.map((g) => g.categoria);
    } catch {
      this.toast.error('No se pudo cargar el plan de implementación.');
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

  abrirNuevaTarea(): void {
    this.tareaEditando = null;
    this.modalAbierto = true;
  }

  abrirEditarTarea(tarea: ImplementacionTareaDto): void {
    this.tareaEditando = tarea;
    this.modalAbierto = true;
  }

  cerrarModal(): void {
    this.modalAbierto = false;
    this.tareaEditando = null;
  }

  async onGuardado(): Promise<void> {
    this.cerrarModal();
    await this.cargar();
  }

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
      this.toast.success('Tarea marcada como completada.');
      await this.cargar();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo completar la tarea.');
    }
  }

  async reabrir(tarea: ImplementacionTareaDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Reabrir tarea',
      message: `¿Reabrir "${tarea.titulo}"? Se borra el check${tarea.estado === 'confirmada' ? ' y la confirmación' : ''} y vuelve a pendiente.`,
      type: 'warning',
      confirmText: 'Reabrir',
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.svc.reabrirTarea(tarea.id));
      this.toast.success('Tarea reabierta.');
      await this.cargar();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo reabrir la tarea.');
    }
  }

  async eliminarTarea(tarea: ImplementacionTareaDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar tarea',
      message: `¿Eliminar "${tarea.titulo}" del checklist?`,
      type: 'error',
      confirmText: 'Eliminar',
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.svc.deleteTarea(tarea.id));
      this.toast.success('Tarea eliminada.');
      await this.cargar();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo eliminar la tarea.');
    }
  }
}

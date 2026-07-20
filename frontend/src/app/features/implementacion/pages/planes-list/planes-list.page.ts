// src/app/features/implementacion/pages/planes-list/planes-list.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ModalPlanImplementacionComponent } from '../../components/modal-plan/modal-plan.component';
import { estiloEstadoPlan } from '../../funciones/estado-tarea.funcion';
import { ImplementacionPlanDto } from '../../models/implementacion.models';

@Component({
  selector: 'app-implementacion-planes-list',
  standalone: true,
  imports: [CommonModule, ModalPlanImplementacionComponent],
  templateUrl: './planes-list.page.html',
})
export class PlanesListPage implements OnInit {
  readonly trackByPlan = (_: number, p: ImplementacionPlanDto): number => p.id;
  readonly estiloPlan = estiloEstadoPlan;

  planes: ImplementacionPlanDto[] = [];
  cargando = false;

  modalAbierto = false;
  planEditando: ImplementacionPlanDto | null = null;

  constructor(
    private svc: ImplementacionService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService,
    private router: Router
  ) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando = true;
    try {
      this.planes = await firstValueFrom(this.svc.getPlanes());
    } catch {
      this.toast.error('No se pudieron cargar los planes de implementación.');
    } finally {
      this.cargando = false;
    }
  }

  abrirCrear(): void {
    this.planEditando = null;
    this.modalAbierto = true;
  }

  abrirEditar(plan: ImplementacionPlanDto): void {
    this.planEditando = plan;
    this.modalAbierto = true;
  }

  cerrarModal(): void {
    this.modalAbierto = false;
    this.planEditando = null;
  }

  async onGuardado(): Promise<void> {
    this.cerrarModal();
    await this.cargar();
  }

  verDetalle(plan: ImplementacionPlanDto): void {
    this.router.navigate(['/implementacion/planes', plan.id]);
  }

  async cancelarPlan(plan: ImplementacionPlanDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Cancelar plan',
      message: `¿Cancelar el plan "${plan.nombre}"? Las tareas quedan congeladas hasta reactivarlo.`,
      type: 'warning',
      confirmText: 'Cancelar plan',
    });
    if (!ok) return;
    await this.cambiarEstado(plan, 'cancelado', 'Plan cancelado.');
  }

  async reactivarPlan(plan: ImplementacionPlanDto): Promise<void> {
    await this.cambiarEstado(plan, 'en_progreso', 'Plan reactivado.');
  }

  async eliminar(plan: ImplementacionPlanDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar plan',
      message: `¿Eliminar el plan "${plan.nombre}" y su checklist? Esta acción lo quita de la lista.`,
      type: 'error',
      confirmText: 'Eliminar',
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.svc.deletePlan(plan.id));
      this.toast.success('Plan eliminado.');
      await this.cargar();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo eliminar el plan.');
    }
  }

  private async cambiarEstado(plan: ImplementacionPlanDto, estado: string, msgOk: string): Promise<void> {
    try {
      await firstValueFrom(
        this.svc.updatePlan(plan.id, {
          nombre: plan.nombre,
          descripcion: plan.descripcion,
          fechaInicio: plan.fechaInicio,
          fechaFin: plan.fechaFin,
          estado,
        })
      );
      this.toast.success(msgOk);
      await this.cargar();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo actualizar el estado del plan.');
    }
  }
}

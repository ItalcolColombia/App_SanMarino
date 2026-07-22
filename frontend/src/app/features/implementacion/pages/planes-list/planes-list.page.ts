// src/app/features/implementacion/pages/planes-list/planes-list.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ModalPlanImplementacionComponent } from '../../components/modal-plan/modal-plan.component';
import { estiloEstadoPlan, estiloTipoPlan } from '../../funciones/estado-tarea.funcion';
import {
  FILTROS_PLANES_VACIOS,
  FiltrosPlanes,
  filtrarPlanes,
  hayFiltrosPlanes,
} from '../../funciones/filtrar-planes.funcion';
import { mensajeErrorHttp } from '../../funciones/resumen-firmas.funcion';
import {
  ImplementacionPlanDto,
  ImplementacionUsuarioAsignableDto,
} from '../../models/implementacion.models';

@Component({
  selector: 'app-implementacion-planes-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ModalPlanImplementacionComponent],
  templateUrl: './planes-list.page.html',
  styleUrls: ['../../styles/implementacion-shared.scss'],
})
export class PlanesListPage implements OnInit {
  readonly trackByPlan = (_: number, p: ImplementacionPlanDto): number => p.id;
  readonly estiloPlan = estiloEstadoPlan;
  readonly estiloTipo = estiloTipoPlan;

  planes: ImplementacionPlanDto[] = [];
  planesFiltrados: ImplementacionPlanDto[] = [];
  usuarios: ImplementacionUsuarioAsignableDto[] = [];

  filtros: FiltrosPlanes = { ...FILTROS_PLANES_VACIOS };

  cargando = false;
  /** La carga siempre termina en datos, vacío o este error visible (nunca spinner infinito). */
  errorMsg: string | null = null;

  modalAbierto = false;
  planEditando: ImplementacionPlanDto | null = null;

  constructor(
    private svc: ImplementacionService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService,
    private router: Router
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([this.cargar(), this.cargarUsuarios()]);
  }

  async cargar(): Promise<void> {
    this.cargando = true;
    this.errorMsg = null;
    try {
      this.planes = await firstValueFrom(this.svc.getPlanes());
      this.aplicarFiltros();
    } catch (err: any) {
      this.errorMsg = mensajeErrorHttp(err, 'No se pudieron cargar los cronogramas de implementación.');
    } finally {
      this.cargando = false;
    }
  }

  /** Combo para elegir "implementador diferente" en el modal (tolerante: solo avisa si falla). */
  private async cargarUsuarios(): Promise<void> {
    try {
      this.usuarios = await firstValueFrom(this.svc.getUsuariosAsignables());
    } catch {
      this.toast.warning('No se pudo cargar la lista de usuarios para elegir encargado.');
    }
  }

  aplicarFiltros(): void {
    this.planesFiltrados = filtrarPlanes(this.planes, this.filtros);
  }

  limpiarFiltros(): void {
    this.filtros = { ...FILTROS_PLANES_VACIOS };
    this.aplicarFiltros();
  }

  get hayFiltros(): boolean {
    return hayFiltrosPlanes(this.filtros);
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
      title: 'Cancelar cronograma',
      message: `¿Cancelar "${plan.nombre}"? Los ítems quedan congelados hasta reactivarlo.`,
      type: 'warning',
      confirmText: 'Cancelar cronograma',
    });
    if (!ok) return;
    await this.cambiarEstado(plan, 'cancelado', 'Cronograma cancelado.');
  }

  async reactivarPlan(plan: ImplementacionPlanDto): Promise<void> {
    await this.cambiarEstado(plan, 'en_progreso', 'Cronograma reactivado.');
  }

  async eliminar(plan: ImplementacionPlanDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar cronograma',
      message: `¿Eliminar "${plan.nombre}" y su checklist? Esta acción lo quita de la lista.`,
      type: 'error',
      confirmText: 'Eliminar',
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.svc.deletePlan(plan.id));
      this.toast.success('Cronograma eliminado.');
      await this.cargar();
    } catch (err: any) {
      this.toast.error(mensajeErrorHttp(err, 'No se pudo eliminar el cronograma.'));
    }
  }

  private async cambiarEstado(plan: ImplementacionPlanDto, estado: string, msgOk: string): Promise<void> {
    try {
      await firstValueFrom(
        this.svc.updatePlan(plan.id, {
          nombre: plan.nombre,
          descripcion: plan.descripcion,
          tipo: plan.tipo,
          fechaInicio: plan.fechaInicio,
          fechaFin: plan.fechaFin,
          implementadorUserId: plan.implementadorUserId,
          estado,
        })
      );
      this.toast.success(msgOk);
      await this.cargar();
    } catch (err: any) {
      this.toast.error(mensajeErrorHttp(err, 'No se pudo actualizar el estado del cronograma.'));
    }
  }
}

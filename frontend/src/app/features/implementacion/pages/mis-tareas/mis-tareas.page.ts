// src/app/features/implementacion/pages/mis-tareas/mis-tareas.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { estiloEstadoTarea } from '../../funciones/estado-tarea.funcion';
import { ImplementacionMiTareaDto } from '../../models/implementacion.models';

@Component({
  selector: 'app-implementacion-mis-tareas',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './mis-tareas.page.html',
})
export class MisTareasPage implements OnInit {
  readonly trackByTarea = (_: number, t: ImplementacionMiTareaDto): number => t.id;
  readonly estiloTarea = estiloEstadoTarea;

  /** Completadas por el gestor, a la espera de MI confirmación. */
  porConfirmar: ImplementacionMiTareaDto[] = [];
  /** Asignadas a mí, aún sin check del gestor. */
  enProceso: ImplementacionMiTareaDto[] = [];
  /** Ya confirmadas por mí (historial/auditoría). */
  confirmadas: ImplementacionMiTareaDto[] = [];

  cargando = false;
  confirmandoId: number | null = null;

  constructor(
    private svc: ImplementacionService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando = true;
    try {
      const tareas = await firstValueFrom(this.svc.getMisTareas());
      this.porConfirmar = tareas.filter((t) => t.estado === 'completada');
      this.enProceso = tareas.filter((t) => t.estado === 'pendiente');
      this.confirmadas = tareas.filter((t) => t.estado === 'confirmada');
    } catch {
      this.toast.error('No se pudieron cargar tus tareas de implementación.');
    } finally {
      this.cargando = false;
    }
  }

  /** Confirmación del asignado: queda registrado con mi usuario y la fecha (auditoría de entrega). */
  async confirmar(tarea: ImplementacionMiTareaDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Confirmar cumplimiento',
      message: `¿Confirmás que "${tarea.titulo}" (plan ${tarea.planNombre}) se cumplió/recibió correctamente? Quedará registrado con tu usuario y la fecha.`,
      type: 'info',
      confirmText: 'Sí, confirmo',
    });
    if (!ok) return;

    this.confirmandoId = tarea.id;
    try {
      await firstValueFrom(this.svc.confirmarTarea(tarea.id, { observaciones: null }));
      this.toast.success('¡Gracias! Tu confirmación quedó registrada.');
      await this.cargar();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo registrar la confirmación.');
    } finally {
      this.confirmandoId = null;
    }
  }
}

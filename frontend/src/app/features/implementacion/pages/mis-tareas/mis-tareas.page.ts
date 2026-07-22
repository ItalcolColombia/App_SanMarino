// src/app/features/implementacion/pages/mis-tareas/mis-tareas.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import {
  ModalFirmarImplementacionComponent,
  ResultadoFirma,
} from '../../components/modal-firmar/modal-firmar.component';
import { estiloEstadoFirma, estiloEstadoTarea } from '../../funciones/estado-tarea.funcion';
import { mensajeErrorHttp } from '../../funciones/resumen-firmas.funcion';
import { ImplementacionMiFirmaDto, ImplementacionMiTareaDto } from '../../models/implementacion.models';

@Component({
  selector: 'app-implementacion-mis-tareas',
  standalone: true,
  imports: [CommonModule, ModalFirmarImplementacionComponent],
  templateUrl: './mis-tareas.page.html',
  styleUrls: ['../../styles/implementacion-shared.scss'],
})
export class MisTareasPage implements OnInit {
  readonly trackByTarea = (_: number, t: ImplementacionMiTareaDto): number => t.id;
  readonly trackByFirma = (_: number, f: ImplementacionMiFirmaDto): number => f.firmaId;
  readonly estiloTarea = estiloEstadoTarea;
  readonly estiloFirma = estiloEstadoFirma;

  /** Puntos donde soy participante y me falta firmar. */
  porFirmar: ImplementacionMiFirmaDto[] = [];
  /** Mis firmas/novedades ya respondidas (historial). */
  firmasRespondidas: ImplementacionMiFirmaDto[] = [];

  /** Completadas por el gestor, a la espera de MI confirmación. */
  porConfirmar: ImplementacionMiTareaDto[] = [];
  /** Asignadas a mí, aún sin check del gestor. */
  enProceso: ImplementacionMiTareaDto[] = [];
  /** Ya confirmadas por mí (historial/auditoría). */
  confirmadas: ImplementacionMiTareaDto[] = [];

  cargando = false;
  errorMsg: string | null = null;
  confirmandoId: number | null = null;

  modalFirmarAbierto = false;
  firmaSeleccionada: ImplementacionMiFirmaDto | null = null;

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
    this.errorMsg = null;
    try {
      const tareas = await firstValueFrom(this.svc.getMisTareas());
      // Backend anterior sin /mis-firmas (404) → sección de firmas vacía sin romper la página.
      let firmas: ImplementacionMiFirmaDto[] = [];
      try {
        firmas = await firstValueFrom(this.svc.getMisFirmas());
      } catch (err: any) {
        if (err?.status !== 404) throw err;
      }
      this.porConfirmar = tareas.filter((t) => t.estado === 'completada');
      this.enProceso = tareas.filter((t) => t.estado === 'pendiente');
      this.confirmadas = tareas.filter((t) => t.estado === 'confirmada');
      this.porFirmar = firmas.filter((f) => f.miEstado === 'pendiente');
      this.firmasRespondidas = firmas.filter((f) => f.miEstado !== 'pendiente');
    } catch (err: any) {
      this.errorMsg = mensajeErrorHttp(err, 'No se pudieron cargar tus tareas de implementación.');
    } finally {
      this.cargando = false;
    }
  }

  get sinNada(): boolean {
    return (
      !this.porFirmar.length &&
      !this.firmasRespondidas.length &&
      !this.porConfirmar.length &&
      !this.enProceso.length &&
      !this.confirmadas.length
    );
  }

  // ── Firmas de participante ─────────────────────────────────────────────────

  abrirFirmar(f: ImplementacionMiFirmaDto): void {
    this.firmaSeleccionada = f;
    this.modalFirmarAbierto = true;
  }

  cerrarModalFirmar(): void {
    this.modalFirmarAbierto = false;
    this.firmaSeleccionada = null;
  }

  async onRespondido(resultado: ResultadoFirma): Promise<void> {
    this.cerrarModalFirmar();
    if (resultado.accion === 'firmada') {
      this.toast.success('¡Gracias! Tu firma quedó registrada.');
      await this.cargar();
      return;
    }

    // Novedad registrada → guiar a crear el ticket con el motivo.
    this.toast.warning('Tu novedad quedó registrada para el encargado.');
    await this.cargar();
    const crear = await this.confirmDialog.ask({
      title: 'Crear ticket con tu novedad',
      message:
        `Registraste que NO firmás "${resultado.firma.tareaTitulo}". ` +
        'Para hacerle seguimiento, creá un ticket contando el motivo. ¿Querés crearlo ahora?',
      type: 'warning',
      confirmText: 'Sí, crear ticket',
    });
    if (crear) {
      this.router.navigate(['/tickets/nuevo']);
    }
  }

  // ── Confirmación del asignado (flujo existente) ────────────────────────────

  /** Confirmación del asignado: queda registrado con mi usuario y la fecha (auditoría de entrega). */
  async confirmar(tarea: ImplementacionMiTareaDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Confirmar cumplimiento',
      message: `¿Confirmás que "${tarea.titulo}" (cronograma ${tarea.planNombre}) se cumplió/recibió correctamente? Quedará registrado con tu usuario y la fecha.`,
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
      this.toast.error(mensajeErrorHttp(err, 'No se pudo registrar la confirmación.'));
    } finally {
      this.confirmandoId = null;
    }
  }
}

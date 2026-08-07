// src/app/features/tickets/components/worklog-panel/worklog-panel.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, Input, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { TicketTareaService } from '../../services/ticket-tarea.service';
import { TicketResumenTiempos, TicketTarea, TicketTiempo } from '../../models/ticket-tarea.models';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { fechaCortaSinTz } from '../../../../shared/utils/format';

/**
 * Control de tiempos del caso: alta rápida de horas, totales, desvío contra la estimación
 * y desglose por persona. Estado mutable + subscribe ⇒ `Eager`.
 */
@Component({
  selector: 'app-worklog-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './worklog-panel.component.html',
})
export class WorklogPanelComponent implements OnInit {
  @Input({ required: true }) ticketId!: number;
  /** Solo el equipo que atiende imputa horas. */
  @Input() puedeRegistrar = false;
  /** Tareas del caso, para poder imputar a una en particular. */
  @Input() tareas: TicketTarea[] = [];

  private readonly svc = inject(TicketTareaService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly destroyRef = inject(DestroyRef);

  readonly registros = signal<TicketTiempo[]>([]);
  readonly resumen = signal<TicketResumenTiempos | null>(null);
  readonly cargando = signal(false);
  readonly guardando = signal(false);

  // Formulario de alta rápida
  mostrarForm = false;
  horas: number | null = null;
  fecha = this.hoyYmd();
  descripcion = '';
  tareaId: number | null = null;

  readonly total = computed(() => this.resumen()?.horasRegistradas ?? 0);
  readonly estimadas = computed(() => this.resumen()?.horasEstimadas ?? null);
  readonly desvio = computed(() => this.resumen()?.desvioHoras ?? null);

  /** % consumido de la estimación (tope 100 para que la barra no se desborde). */
  readonly consumo = computed(() => {
    const est = this.estimadas();
    if (!est || est <= 0) return 0;
    return Math.min(100, Math.round((this.total() * 100) / est));
  });

  ngOnInit(): void { this.cargar(); }

  cargar(): void {
    this.cargando.set(true);
    forkJoin({
      registros: this.svc.listarTiempos(this.ticketId),
      resumen: this.svc.resumenTiempos(this.ticketId),
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.cargando.set(false)))
      .subscribe({
        next: r => { this.registros.set(r.registros); this.resumen.set(r.resumen); },
        error: () => this.toast.error('No se pudieron cargar los tiempos del caso.'),
      });
  }

  abrirForm(): void {
    this.mostrarForm = true;
    this.horas = null;
    this.fecha = this.hoyYmd();
    this.descripcion = '';
    this.tareaId = null;
  }

  registrar(): void {
    if (!this.horas || this.horas <= 0) {
      this.toast.warning('Indicá cuántas horas dedicaste.');
      return;
    }
    this.guardando.set(true);
    this.svc.registrarTiempo(this.ticketId, {
      horas: this.horas,
      fecha: this.fecha || null,
      descripcion: this.descripcion.trim() || null,
      tareaId: this.tareaId,
    })
      .pipe(finalize(() => this.guardando.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Tiempo registrado.');
          this.mostrarForm = false;
          this.cargar();
        },
        error: e => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo registrar el tiempo.'),
      });
  }

  async eliminar(registro: TicketTiempo): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar registro',
      message: `¿Eliminar el registro de ${registro.horas} h del ${this.fechaCorta(registro.fecha)}?`,
      type: 'error',
      confirmText: 'Eliminar',
    });
    if (!ok) return;

    this.svc.eliminarTiempo(this.ticketId, registro.id).subscribe({
      next: () => { this.toast.success('Registro eliminado.'); this.cargar(); },
      error: e => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo eliminar el registro.'),
    });
  }

  fechaCorta(iso: string | null): string { return fechaCortaSinTz(iso) || '—'; }

  iniciales(nombre: string | null): string {
    if (!nombre) return '?';
    return nombre.trim().split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }

  /** Fecha de hoy en formato `yyyy-MM-dd` (valor que espera `<input type="date">`). */
  private hoyYmd(): string {
    const d = new Date();
    const mes = `${d.getMonth() + 1}`.padStart(2, '0');
    const dia = `${d.getDate()}`.padStart(2, '0');
    return `${d.getFullYear()}-${mes}-${dia}`;
  }
}

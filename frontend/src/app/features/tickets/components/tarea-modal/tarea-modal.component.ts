// src/app/features/tickets/components/tarea-modal/tarea-modal.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  COLUMNAS_TAREA, CreateTicketTareaRequest, EstadoTarea, PRIORIDADES, PRIORIDAD_LABEL,
  PrioridadTicket, TAREA_ESTADO_LABEL, TAREA_TIPO_COLOR, TicketTarea, TIPOS_TAREA, TipoTarea,
  UpdateTicketTareaRequest,
} from '../../models/ticket-tarea.models';

/** Persona que puede quedar como responsable de la tarea. */
export interface OpcionAsignable { guid: string; nombre: string; }

/**
 * Alta y edición de una tarea del caso. Emite el request ya armado; el guardado (HTTP,
 * toast y recarga) queda en el contenedor.
 */
@Component({
  selector: 'app-tarea-modal',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './tarea-modal.component.html',
})
export class TareaModalComponent implements OnChanges {
  /** Tarea a editar; null = alta. */
  @Input() tarea: TicketTarea | null = null;
  /** Columna inicial cuando se crea desde el botón "+" de una columna. */
  @Input() estadoInicial: EstadoTarea = 'BACKLOG';
  @Input() asignables: OpcionAsignable[] = [];
  @Input() guardando = false;
  /** Tarea padre cuando se está creando una subtarea. */
  @Input() parentTareaId: number | null = null;
  @Input() parentTitulo: string | null = null;

  @Output() guardar = new EventEmitter<CreateTicketTareaRequest | UpdateTicketTareaRequest>();
  @Output() cerrar = new EventEmitter<void>();

  readonly tipos = TIPOS_TAREA;
  readonly columnas = COLUMNAS_TAREA;
  readonly prioridades = PRIORIDADES;
  readonly estadoLabel = TAREA_ESTADO_LABEL;
  readonly prioridadLabel = PRIORIDAD_LABEL;
  readonly tipoColor = TAREA_TIPO_COLOR;

  titulo = '';
  descripcion = '';
  tipo: TipoTarea = 'TAREA';
  estado: EstadoTarea = 'BACKLOG';
  prioridad: PrioridadTicket = 'MEDIA';
  asignadoGuid = '';
  horasEstimadas: number | null = null;
  fechaInicioPlan = '';
  fechaFinPlan = '';
  etiquetas = '';

  get esEdicion(): boolean { return this.tarea !== null; }

  ngOnChanges(): void {
    if (this.tarea) {
      this.titulo = this.tarea.titulo;
      this.descripcion = this.tarea.descripcion ?? '';
      this.tipo = this.tarea.tipo;
      this.estado = this.tarea.estado;
      this.prioridad = this.tarea.prioridad;
      this.asignadoGuid = this.tarea.asignadoUserGuid ?? '';
      this.horasEstimadas = this.tarea.horasEstimadas;
      this.fechaInicioPlan = this.tarea.fechaInicioPlan ?? '';
      this.fechaFinPlan = this.tarea.fechaFinPlan ?? '';
      this.etiquetas = this.tarea.etiquetas ?? '';
    } else {
      this.titulo = '';
      this.descripcion = '';
      this.tipo = this.parentTareaId ? 'SUBTAREA' : 'TAREA';
      this.estado = this.estadoInicial;
      this.prioridad = 'MEDIA';
      this.asignadoGuid = '';
      this.horasEstimadas = null;
      this.fechaInicioPlan = '';
      this.fechaFinPlan = '';
      this.etiquetas = '';
    }
  }

  /** True si falta el título o las fechas están invertidas (deshabilita Guardar). */
  get invalido(): boolean {
    if (!this.titulo.trim()) return true;
    return !!this.fechaInicioPlan && !!this.fechaFinPlan && this.fechaFinPlan < this.fechaInicioPlan;
  }

  onGuardar(): void {
    if (this.invalido || this.guardando) return;

    const comun = {
      titulo: this.titulo.trim(),
      descripcion: this.descripcion.trim() || null,
      tipo: this.tipo,
      estado: this.estado,
      prioridad: this.prioridad,
      horasEstimadas: this.horasEstimadas ?? null,
      fechaInicioPlan: this.fechaInicioPlan || null,
      fechaFinPlan: this.fechaFinPlan || null,
      etiquetas: this.etiquetas.trim() || null,
    };

    if (this.esEdicion) {
      // En edición, null significa "no tocar": quitar el responsable necesita su propio flag.
      const req: UpdateTicketTareaRequest = {
        ...comun,
        asignadoUserGuid: this.asignadoGuid || null,
        quitarAsignado: !this.asignadoGuid,
      };
      this.guardar.emit(req);
      return;
    }

    const req: CreateTicketTareaRequest = {
      ...comun,
      asignadoUserGuid: this.asignadoGuid || null,
      parentTareaId: this.parentTareaId,
    };
    this.guardar.emit(req);
  }
}

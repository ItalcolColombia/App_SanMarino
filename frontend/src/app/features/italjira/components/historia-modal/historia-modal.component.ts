// src/app/features/italjira/components/historia-modal/historia-modal.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  COLUMNAS_TAREA, CreateHistoriaRequest, EstadoHistoria, Historia, PRIORIDADES, PRIORIDAD_LABEL,
  PrioridadTicket, TAREA_ESTADO_LABEL, UpdateHistoriaRequest,
} from '../../models/historia.models';

/** Persona que puede quedar como responsable de la historia. */
export interface OpcionResponsable { guid: string; nombre: string; }

/**
 * Alta y edición de una HISTORIA (épica). Emite el request ya armado; el guardado (HTTP, toast y
 * recarga) queda en el contenedor — mismo contrato que `TareaModalComponent`.
 *
 * Estado mutable + `@Input` que se rehidrata en `ngOnChanges` ⇒ `Eager` obligatorio (en Angular 22
 * omitir `changeDetection` deja el modal colgado en «Cargando…»).
 */
@Component({
  selector: 'app-historia-modal',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './historia-modal.component.html',
})
export class HistoriaModalComponent implements OnChanges {
  /** Historia a editar; null = alta. */
  @Input() historia: Historia | null = null;
  @Input() responsables: OpcionResponsable[] = [];
  @Input() guardando = false;

  @Output() guardar = new EventEmitter<CreateHistoriaRequest | UpdateHistoriaRequest>();
  @Output() cerrar = new EventEmitter<void>();

  readonly columnas = COLUMNAS_TAREA;
  readonly prioridades = PRIORIDADES;
  readonly estadoLabel = TAREA_ESTADO_LABEL;
  readonly prioridadLabel = PRIORIDAD_LABEL;

  titulo = '';
  descripcion = '';
  estado: EstadoHistoria = 'BACKLOG';
  prioridad: PrioridadTicket = 'MEDIA';
  responsableGuid = '';
  horasEstimadas: number | null = null;
  fechaInicioPlan = '';
  fechaFinPlan = '';
  etiquetas = '';

  get esEdicion(): boolean { return this.historia !== null; }

  ngOnChanges(): void {
    if (this.historia) {
      this.titulo = this.historia.titulo;
      this.descripcion = this.historia.descripcion ?? '';
      this.estado = this.historia.estado;
      this.prioridad = this.historia.prioridad;
      this.responsableGuid = this.historia.responsableUserGuid ?? '';
      this.horasEstimadas = this.historia.horasEstimadas;
      this.fechaInicioPlan = this.historia.fechaInicioPlan ?? '';
      this.fechaFinPlan = this.historia.fechaFinPlan ?? '';
      this.etiquetas = this.historia.etiquetas ?? '';
    } else {
      this.titulo = '';
      this.descripcion = '';
      this.estado = 'BACKLOG';
      this.prioridad = 'MEDIA';
      this.responsableGuid = '';
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
      estado: this.estado,
      prioridad: this.prioridad,
      horasEstimadas: this.horasEstimadas ?? null,
      fechaInicioPlan: this.fechaInicioPlan || null,
      fechaFinPlan: this.fechaFinPlan || null,
      etiquetas: this.etiquetas.trim() || null,
    };

    if (this.esEdicion) {
      // En edición, null significa «no tocar»: quitar el responsable necesita su propio flag.
      const req: UpdateHistoriaRequest = {
        ...comun,
        responsableUserGuid: this.responsableGuid || null,
        quitarResponsable: !this.responsableGuid,
      };
      this.guardar.emit(req);
      return;
    }

    const req: CreateHistoriaRequest = {
      ...comun,
      responsableUserGuid: this.responsableGuid || null,
    };
    this.guardar.emit(req);
  }
}

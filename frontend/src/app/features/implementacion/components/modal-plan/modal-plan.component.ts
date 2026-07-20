// src/app/features/implementacion/components/modal-plan/modal-plan.component.ts
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ImplementacionPlanDto } from '../../models/implementacion.models';

@Component({
  selector: 'app-modal-plan-implementacion',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div
      *ngIf="open"
      class="fixed inset-0 z-50 flex items-center justify-center p-4"
      style="background: rgba(0, 0, 0, 0.48); backdrop-filter: blur(2px)"
      (click)="cerrar()"
    >
      <div
        class="flex max-h-[90vh] w-full max-w-lg flex-col overflow-hidden rounded-2xl bg-white shadow-xl"
        style="border: 1px solid var(--ital-green-100)"
        role="dialog"
        aria-modal="true"
        aria-labelledby="titulo-modal-plan"
        (click)="$event.stopPropagation()"
      >
        <div class="flex items-center justify-between border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <h3 id="titulo-modal-plan" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
            {{ plan ? 'Editar plan de implementación' : 'Nuevo plan de implementación' }}
          </h3>
          <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
        </div>

        <div class="flex-1 space-y-4 overflow-y-auto px-5 py-4 text-sm">
          <div>
            <label class="form-label" for="mp-nombre">Nombre *</label>
            <input
              id="mp-nombre"
              type="text"
              class="form-input"
              maxlength="200"
              placeholder="Ej. Implementación Italcol Panamá — Engorde"
              [(ngModel)]="nombre"
            />
          </div>

          <div>
            <label class="form-label" for="mp-descripcion">Descripción</label>
            <textarea
              id="mp-descripcion"
              rows="3"
              class="form-input"
              maxlength="2000"
              placeholder="Alcance de la entrega, módulos incluidos, acuerdos…"
              [(ngModel)]="descripcion"
            ></textarea>
          </div>

          <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label class="form-label" for="mp-inicio">Fecha inicio</label>
              <input id="mp-inicio" type="date" class="form-input" [(ngModel)]="fechaInicio" />
            </div>
            <div>
              <label class="form-label" for="mp-fin">Fecha fin</label>
              <input id="mp-fin" type="date" class="form-input" [(ngModel)]="fechaFin" />
            </div>
          </div>

          <label *ngIf="!plan" class="flex items-start gap-2" style="color: var(--ital-text)">
            <input type="checkbox" class="mt-0.5" [(ngModel)]="usarPlantilla" />
            <span>
              Usar plantilla estándar de entrega
              <span class="block text-xs" style="color: var(--ital-muted)">
                Crea el checklist base: parametrizaciones, capacitación, carga de datos y puesta en marcha.
              </span>
            </span>
          </label>
        </div>

        <div class="flex justify-end gap-2 border-t px-5 py-4" style="border-color: var(--ital-green-100); background: var(--ital-cream)">
          <button type="button" class="btn-ghost text-sm" (click)="cerrar()">Cancelar</button>
          <button type="button" class="btn-primary text-sm" [disabled]="guardando" (click)="guardar()">
            {{ guardando ? 'Guardando…' : plan ? 'Guardar cambios' : 'Crear plan' }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ModalPlanImplementacionComponent implements OnChanges {
  @Input() open = false;
  @Input() plan: ImplementacionPlanDto | null = null;

  @Output() cerrado = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  nombre = '';
  descripcion = '';
  fechaInicio = '';
  fechaFin = '';
  usarPlantilla = true;
  guardando = false;

  constructor(private svc: ImplementacionService, private toast: ToastService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue) {
      this.nombre = this.plan?.nombre ?? '';
      this.descripcion = this.plan?.descripcion ?? '';
      this.fechaInicio = this.plan?.fechaInicio?.substring(0, 10) ?? '';
      this.fechaFin = this.plan?.fechaFin?.substring(0, 10) ?? '';
      this.usarPlantilla = true;
      this.guardando = false;
    }
  }

  cerrar(): void {
    this.cerrado.emit();
  }

  async guardar(): Promise<void> {
    if (!this.nombre.trim()) {
      this.toast.warning('El nombre del plan es obligatorio.');
      return;
    }
    this.guardando = true;
    const base = {
      nombre: this.nombre.trim(),
      descripcion: this.descripcion.trim() || null,
      fechaInicio: this.fechaInicio || null,
      fechaFin: this.fechaFin || null,
    };
    try {
      if (this.plan) {
        await firstValueFrom(this.svc.updatePlan(this.plan.id, { ...base, estado: null }));
        this.toast.success('Plan actualizado.');
      } else {
        await firstValueFrom(this.svc.createPlan({ ...base, usarPlantilla: this.usarPlantilla }));
        this.toast.success('Plan creado.');
      }
      this.guardado.emit();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo guardar el plan.');
    } finally {
      this.guardando = false;
    }
  }
}

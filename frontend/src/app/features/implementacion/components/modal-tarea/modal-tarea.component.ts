// src/app/features/implementacion/components/modal-tarea/modal-tarea.component.ts
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import {
  ImplementacionRolAsignableDto,
  ImplementacionTareaDto,
  ImplementacionUsuarioAsignableDto,
} from '../../models/implementacion.models';

@Component({
  selector: 'app-modal-tarea-implementacion',
  standalone: true,
  imports: [CommonModule, FormsModule],
  styleUrls: ['../../styles/implementacion-shared.scss'],
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
        aria-labelledby="titulo-modal-tarea"
        (click)="$event.stopPropagation()"
      >
        <div class="flex items-center justify-between border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <h3 id="titulo-modal-tarea" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
            {{ tarea ? 'Editar ítem del checklist' : 'Nuevo ítem de validación' }}
          </h3>
          <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
        </div>

        <div class="flex-1 space-y-4 overflow-y-auto px-5 py-4 text-sm">
          <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label class="form-label" for="mt-categoria">Categoría *</label>
              <input
                id="mt-categoria"
                type="text"
                class="input-italfoods"
                maxlength="100"
                list="mt-categorias-sugeridas"
                placeholder="Ej. Parametrizaciones"
                [(ngModel)]="categoria"
              />
              <datalist id="mt-categorias-sugeridas">
                <option *ngFor="let c of categoriasSugeridas" [value]="c"></option>
              </datalist>
            </div>
            <div>
              <label class="form-label" for="mt-fecha">Fecha programada</label>
              <input id="mt-fecha" type="date" class="input-italfoods" [(ngModel)]="fechaProgramada" />
            </div>
          </div>

          <div>
            <label class="form-label" for="mt-titulo">Título *</label>
            <input
              id="mt-titulo"
              type="text"
              class="input-italfoods"
              maxlength="300"
              placeholder="Ej. Parametrización de granjas, núcleos y galpones"
              [(ngModel)]="titulo"
            />
          </div>

          <div>
            <label class="form-label" for="mt-descripcion">Descripción</label>
            <textarea id="mt-descripcion" rows="2" class="input-italfoods" maxlength="2000" [(ngModel)]="descripcion"></textarea>
          </div>

          <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label class="form-label" for="mt-rol">Rol responsable</label>
              <select id="mt-rol" class="input-italfoods" [(ngModel)]="roleId">
                <option [ngValue]="null">Sin rol específico</option>
                <option *ngFor="let r of roles" [ngValue]="r.id">{{ r.nombre }}</option>
              </select>
            </div>
            <div>
              <label class="form-label" for="mt-usuario">Usuario que confirma</label>
              <select id="mt-usuario" class="input-italfoods" [(ngModel)]="asignadoUserId">
                <option [ngValue]="null">Sin usuario asignado</option>
                <option *ngFor="let u of usuarios" [ngValue]="u.id">{{ u.nombre }} ({{ u.cedula }})</option>
              </select>
              <p class="mt-1 text-xs" style="color: var(--ital-muted)">
                Solo este usuario podrá confirmar el cumplimiento desde "Mis tareas".
              </p>
            </div>
          </div>
        </div>

        <div class="flex justify-end gap-2 border-t px-5 py-4" style="border-color: var(--ital-green-100); background: var(--ital-cream)">
          <button type="button" class="btn-italfoods-secondary text-sm" (click)="cerrar()">Cancelar</button>
          <button type="button" class="btn-italfoods-primary text-sm" [disabled]="guardando" (click)="guardar()">
            {{ guardando ? 'Guardando…' : tarea ? 'Guardar cambios' : 'Agregar ítem' }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ModalTareaImplementacionComponent implements OnChanges {
  @Input() open = false;
  @Input() planId!: number;
  @Input() tarea: ImplementacionTareaDto | null = null;
  @Input() usuarios: ImplementacionUsuarioAsignableDto[] = [];
  @Input() roles: ImplementacionRolAsignableDto[] = [];
  @Input() categoriasSugeridas: string[] = [];

  @Output() cerrado = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  categoria = '';
  titulo = '';
  descripcion = '';
  fechaProgramada = '';
  roleId: number | null = null;
  asignadoUserId: string | null = null;
  guardando = false;

  constructor(private svc: ImplementacionService, private toast: ToastService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue) {
      this.categoria = this.tarea?.categoria ?? '';
      this.titulo = this.tarea?.titulo ?? '';
      this.descripcion = this.tarea?.descripcion ?? '';
      this.fechaProgramada = this.tarea?.fechaProgramada?.substring(0, 10) ?? '';
      this.roleId = this.tarea?.roleId ?? null;
      this.asignadoUserId = this.tarea?.asignadoUserId ?? null;
      this.guardando = false;
    }
  }

  cerrar(): void {
    this.cerrado.emit();
  }

  async guardar(): Promise<void> {
    if (!this.categoria.trim() || !this.titulo.trim()) {
      this.toast.warning('Categoría y título son obligatorios.');
      return;
    }
    this.guardando = true;
    const req = {
      categoria: this.categoria.trim(),
      titulo: this.titulo.trim(),
      descripcion: this.descripcion.trim() || null,
      orden: this.tarea?.orden ?? null,
      fechaProgramada: this.fechaProgramada || null,
      roleId: this.roleId,
      asignadoUserId: this.asignadoUserId,
    };
    try {
      if (this.tarea) {
        await firstValueFrom(this.svc.updateTarea(this.tarea.id, req));
        this.toast.success('Ítem actualizado.');
      } else {
        await firstValueFrom(this.svc.createTarea(this.planId, req));
        this.toast.success('Ítem agregado al checklist.');
      }
      this.guardado.emit();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo guardar la tarea.');
    } finally {
      this.guardando = false;
    }
  }
}

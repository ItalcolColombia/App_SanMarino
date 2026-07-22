// src/app/features/implementacion/components/modal-plan/modal-plan.component.ts
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { mensajeErrorHttp } from '../../funciones/resumen-firmas.funcion';
import {
  ImplementacionPlanDto,
  ImplementacionUsuarioAsignableDto,
  TipoPlan,
} from '../../models/implementacion.models';

@Component({
  selector: 'app-modal-plan-implementacion',
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
        aria-labelledby="titulo-modal-plan"
        (click)="$event.stopPropagation()"
      >
        <div class="flex items-center justify-between border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <h3 id="titulo-modal-plan" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
            {{ plan ? 'Editar cronograma' : 'Nuevo cronograma de implementación' }}
          </h3>
          <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
        </div>

        <div class="flex-1 space-y-4 overflow-y-auto px-5 py-4 text-sm">
          <div>
            <label class="form-label" for="mp-nombre">Nombre *</label>
            <input
              id="mp-nombre"
              type="text"
              class="input-italfoods"
              maxlength="200"
              placeholder="Ej. Implementación Panamá — Engorde"
              [(ngModel)]="nombre"
            />
          </div>

          <div>
            <label class="form-label" for="mp-descripcion">Descripción</label>
            <textarea
              id="mp-descripcion"
              rows="3"
              class="input-italfoods"
              maxlength="2000"
              placeholder="Qué se va a implementar/capacitar. Ej: integrar ItalGranja en todo Panamá…"
              [(ngModel)]="descripcion"
            ></textarea>
          </div>

          <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label class="form-label" for="mp-tipo">Tipo de cronograma</label>
              <select id="mp-tipo" class="input-italfoods" [(ngModel)]="tipo">
                <option value="implementacion">Implementación (entrega)</option>
                <option value="capacitacion">Capacitación</option>
                <option value="mixto">Implementación + capacitación</option>
              </select>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="form-label" for="mp-inicio">Fecha inicio</label>
                <input id="mp-inicio" type="date" class="input-italfoods" [(ngModel)]="fechaInicio" />
              </div>
              <div>
                <label class="form-label" for="mp-fin">Fecha fin</label>
                <input id="mp-fin" type="date" class="input-italfoods" [(ngModel)]="fechaFin" />
              </div>
            </div>
          </div>

          <!-- Creador + encargado -->
          <div class="rounded-xl border p-3" style="border-color: var(--ital-green-100); background: var(--ital-cream)">
            <p class="text-xs font-bold" style="color: var(--ital-orange-dark)">Responsables</p>
            <p class="mt-1 text-xs" style="color: var(--ital-muted)">
              Creado por: <strong style="color: var(--ital-text)">{{ creadorNombre }}</strong>
              <ng-container *ngIf="plan?.creadoPorEmail"> · {{ plan?.creadoPorEmail }}</ng-container>
            </p>

            <label class="mt-2 flex items-start gap-2" style="color: var(--ital-text)">
              <input type="checkbox" class="mt-0.5" [(ngModel)]="implementadorDiferente" />
              <span>
                El encargado de la implementación es otra persona
                <span class="block text-xs" style="color: var(--ital-muted)">
                  Si no se marca, el encargado queda el mismo creador.
                </span>
              </span>
            </label>

            <div *ngIf="implementadorDiferente" class="mt-2">
              <label class="form-label" for="mp-implementador">Implementador / encargado *</label>
              <select id="mp-implementador" class="input-italfoods" [(ngModel)]="implementadorUserId">
                <option [ngValue]="null">— Elegí el usuario encargado —</option>
                <option *ngFor="let u of usuarios" [ngValue]="u.id">
                  {{ u.nombre }} ({{ u.cedula }}){{ u.email ? ' · ' + u.email : '' }}
                </option>
              </select>
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
          <button type="button" class="btn-italfoods-secondary text-sm" (click)="cerrar()">Cancelar</button>
          <button type="button" class="btn-italfoods-primary text-sm" [disabled]="guardando" (click)="guardar()">
            {{ guardando ? 'Guardando…' : plan ? 'Guardar cambios' : 'Crear cronograma' }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ModalPlanImplementacionComponent implements OnChanges {
  @Input() open = false;
  @Input() plan: ImplementacionPlanDto | null = null;
  @Input() usuarios: ImplementacionUsuarioAsignableDto[] = [];

  @Output() cerrado = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  nombre = '';
  descripcion = '';
  tipo: TipoPlan = 'implementacion';
  fechaInicio = '';
  fechaFin = '';
  implementadorDiferente = false;
  implementadorUserId: string | null = null;
  usarPlantilla = true;
  guardando = false;

  constructor(
    private svc: ImplementacionService,
    private toast: ToastService,
    private storage: TokenStorageService
  ) {}

  /** Al crear, el creador es el usuario logueado; al editar, el que quedó registrado. */
  get creadorNombre(): string {
    if (this.plan) return this.plan.creadoPorNombre || '—';
    return this.storage.get()?.user?.fullName || 'vos (usuario actual)';
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue) {
      this.nombre = this.plan?.nombre ?? '';
      this.descripcion = this.plan?.descripcion ?? '';
      this.tipo = this.plan?.tipo ?? 'implementacion';
      this.fechaInicio = this.plan?.fechaInicio?.substring(0, 10) ?? '';
      this.fechaFin = this.plan?.fechaFin?.substring(0, 10) ?? '';
      this.implementadorDiferente = !!(
        this.plan?.implementadorUserId &&
        this.plan.implementadorUserId !== this.plan.creadoPorUserGuid
      );
      this.implementadorUserId = this.implementadorDiferente ? this.plan!.implementadorUserId : null;
      this.usarPlantilla = true;
      this.guardando = false;
    }
  }

  cerrar(): void {
    this.cerrado.emit();
  }

  async guardar(): Promise<void> {
    if (!this.nombre.trim()) {
      this.toast.warning('El nombre del cronograma es obligatorio.');
      return;
    }
    if (this.implementadorDiferente && !this.implementadorUserId) {
      this.toast.warning('Elegí el usuario encargado de la implementación.');
      return;
    }
    this.guardando = true;
    const base = {
      nombre: this.nombre.trim(),
      descripcion: this.descripcion.trim() || null,
      tipo: this.tipo,
      fechaInicio: this.fechaInicio || null,
      fechaFin: this.fechaFin || null,
      implementadorUserId: this.implementadorDiferente ? this.implementadorUserId : null,
    };
    try {
      if (this.plan) {
        await firstValueFrom(this.svc.updatePlan(this.plan.id, { ...base, estado: null }));
        this.toast.success('Cronograma actualizado.');
      } else {
        await firstValueFrom(this.svc.createPlan({ ...base, usarPlantilla: this.usarPlantilla }));
        this.toast.success('Cronograma creado. Ahora agregá sus ítems de validación.');
      }
      this.guardado.emit();
    } catch (err: any) {
      this.toast.error(mensajeErrorHttp(err, 'No se pudo guardar el cronograma.'));
    } finally {
      this.guardando = false;
    }
  }
}

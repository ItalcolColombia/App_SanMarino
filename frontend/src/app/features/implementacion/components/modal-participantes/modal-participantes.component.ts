// src/app/features/implementacion/components/modal-participantes/modal-participantes.component.ts
// Asignación de participantes (asistentes) de un ítem: quiénes estuvieron en la capacitación/entrega
// y deben firmar el recibido. Los que ya respondieron (firma o novedad) no se pueden quitar.
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { mensajeErrorHttp } from '../../funciones/resumen-firmas.funcion';
import {
  ImplementacionTareaDto,
  ImplementacionUsuarioAsignableDto,
} from '../../models/implementacion.models';

@Component({
  selector: 'app-modal-participantes-implementacion',
  standalone: true,
  imports: [CommonModule, FormsModule],
  styleUrls: ['../../styles/implementacion-shared.scss'],
  template: `
    <div
      *ngIf="open && tarea"
      class="fixed inset-0 z-50 flex items-center justify-center p-4"
      style="background: rgba(0, 0, 0, 0.48); backdrop-filter: blur(2px)"
      (click)="cerrar()"
    >
      <div
        class="flex max-h-[90vh] w-full max-w-lg flex-col overflow-hidden rounded-2xl bg-white shadow-xl"
        style="border: 1px solid var(--ital-green-100)"
        role="dialog"
        aria-modal="true"
        aria-labelledby="titulo-modal-participantes"
        (click)="$event.stopPropagation()"
      >
        <div class="border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <div class="flex items-center justify-between">
            <h3 id="titulo-modal-participantes" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
              Participantes que firman
            </h3>
            <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
          </div>
          <p class="mt-1 text-xs" style="color: var(--ital-muted)">
            {{ tarea!.titulo }} — marcá los usuarios que estuvieron/reciben este punto; cada uno podrá
            verlo y firmarlo desde "Mis tareas".
          </p>
        </div>

        <div class="border-b px-5 py-3" style="border-color: var(--ital-green-100)">
          <input
            type="text"
            class="input-italfoods"
            placeholder="Buscar usuario por nombre, cédula o correo…"
            [(ngModel)]="busqueda"
            (ngModelChange)="filtrar()"
          />
        </div>

        <div class="flex-1 overflow-y-auto px-5 py-3 text-sm">
          <p *ngIf="!usuariosFiltrados.length" class="py-6 text-center text-xs" style="color: var(--ital-muted)">
            No hay usuarios que coincidan con la búsqueda.
          </p>
          <label
            *ngFor="let u of usuariosFiltrados; trackBy: trackByUsuario"
            class="flex items-start justify-between gap-2 border-b py-2 last:border-b-0"
            style="border-color: var(--ital-green-50)"
          >
            <span class="flex items-start gap-2">
              <input
                type="checkbox"
                class="mt-1"
                [checked]="seleccion.has(u.id)"
                [disabled]="bloqueados.has(u.id)"
                (change)="toggle(u.id)"
              />
              <span>
                <span class="block font-semibold" style="color: var(--ital-text)">{{ u.nombre }}</span>
                <span class="block text-xs" style="color: var(--ital-muted)">
                  {{ u.cedula }}{{ u.email ? ' · ' + u.email : '' }}
                </span>
              </span>
            </span>
            <span
              *ngIf="bloqueados.has(u.id)"
              class="chip"
              style="color: var(--success); background: color-mix(in srgb, var(--success) 12%, transparent)"
              title="Ya respondió; su firma/novedad queda como auditoría y no se puede quitar."
            >
              respondió
            </span>
          </label>
        </div>

        <div
          class="flex items-center justify-between gap-2 border-t px-5 py-4"
          style="border-color: var(--ital-green-100); background: var(--ital-cream)"
        >
          <span class="text-xs font-semibold" style="color: var(--ital-muted)">
            {{ seleccion.size }} seleccionado(s)
          </span>
          <div class="flex gap-2">
            <button type="button" class="btn-italfoods-secondary text-sm" (click)="cerrar()">Cancelar</button>
            <button type="button" class="btn-italfoods-primary text-sm" [disabled]="guardando" (click)="guardar()">
              {{ guardando ? 'Guardando…' : 'Guardar participantes' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class ModalParticipantesImplementacionComponent implements OnChanges {
  @Input() open = false;
  @Input() tarea: ImplementacionTareaDto | null = null;
  @Input() usuarios: ImplementacionUsuarioAsignableDto[] = [];

  @Output() cerrado = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  readonly trackByUsuario = (_: number, u: ImplementacionUsuarioAsignableDto): string => u.id;

  busqueda = '';
  /** Lista memoizada (referencia estable para el template; se recalcula solo al escribir o abrir). */
  usuariosFiltrados: ImplementacionUsuarioAsignableDto[] = [];
  seleccion = new Set<string>();
  /** Participantes que ya firmaron o registraron novedad: no se pueden desmarcar. */
  bloqueados = new Set<string>();
  guardando = false;

  constructor(private svc: ImplementacionService, private toast: ToastService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue && this.tarea) {
      this.busqueda = '';
      this.guardando = false;
      this.seleccion = new Set(this.tarea.firmas.map((f) => f.userId));
      this.bloqueados = new Set(this.tarea.firmas.filter((f) => f.estado !== 'pendiente').map((f) => f.userId));
      this.filtrar();
    } else if (changes['usuarios']) {
      this.filtrar();
    }
  }

  filtrar(): void {
    const q = this.busqueda.trim().toLowerCase();
    this.usuariosFiltrados = !q
      ? this.usuarios
      : this.usuarios.filter((u) =>
          [u.nombre, u.cedula, u.email].some((v) => (v ?? '').toLowerCase().includes(q))
        );
  }

  toggle(userId: string): void {
    if (this.bloqueados.has(userId)) return;
    if (this.seleccion.has(userId)) this.seleccion.delete(userId);
    else this.seleccion.add(userId);
  }

  cerrar(): void {
    this.cerrado.emit();
  }

  async guardar(): Promise<void> {
    if (!this.tarea) return;
    this.guardando = true;
    try {
      await firstValueFrom(this.svc.setParticipantes(this.tarea.id, { userIds: [...this.seleccion] }));
      this.toast.success('Participantes actualizados.');
      this.guardado.emit();
    } catch (err: any) {
      this.toast.error(mensajeErrorHttp(err, 'No se pudieron guardar los participantes.'));
    } finally {
      this.guardando = false;
    }
  }
}

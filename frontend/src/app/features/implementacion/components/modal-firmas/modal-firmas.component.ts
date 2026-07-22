// src/app/features/implementacion/components/modal-firmas/modal-firmas.component.ts
// Detalle (solo lectura) de las firmas de un ítem: quién estuvo, quién firmó (firma digitada + nota,
// usuario con su correo de la aplicación) y las novedades (rechazos con motivo) resaltadas.
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { estiloEstadoFirma } from '../../funciones/estado-tarea.funcion';
import { ImplementacionFirmaDto, ImplementacionTareaDto } from '../../models/implementacion.models';

@Component({
  selector: 'app-modal-firmas-implementacion',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['../../styles/implementacion-shared.scss'],
  template: `
    <div
      *ngIf="open && tarea"
      class="fixed inset-0 z-50 flex items-center justify-center p-4"
      style="background: rgba(0, 0, 0, 0.48); backdrop-filter: blur(2px)"
      (click)="cerrar()"
    >
      <div
        class="flex max-h-[90vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl"
        style="border: 1px solid var(--ital-green-100)"
        role="dialog"
        aria-modal="true"
        aria-labelledby="titulo-modal-firmas"
        (click)="$event.stopPropagation()"
      >
        <div class="border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <div class="flex items-center justify-between">
            <h3 id="titulo-modal-firmas" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
              Firmas del punto
            </h3>
            <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
          </div>
          <p class="mt-1 text-xs" style="color: var(--ital-muted)">{{ tarea!.categoria }} · {{ tarea!.titulo }}</p>
          <p *ngIf="tarea!.fechaCompletada" class="mt-0.5 text-xs" style="color: var(--ital-muted)">
            Realizado el {{ tarea!.fechaCompletada | date: 'dd/MM/yyyy HH:mm' }}
            <ng-container *ngIf="tarea!.completadaPorNombre"> por {{ tarea!.completadaPorNombre }}</ng-container>
          </p>
        </div>

        <div class="flex-1 overflow-y-auto px-5 py-4 text-sm">
          <p *ngIf="!tarea!.firmas.length" class="py-6 text-center text-xs" style="color: var(--ital-muted)">
            Este punto todavía no tiene participantes asignados.
          </p>

          <div
            *ngFor="let f of tarea!.firmas; trackBy: trackByFirma"
            class="mb-2 rounded-xl border p-3"
            [style.border-color]="f.estado === 'rechazada' ? 'var(--danger)' : 'var(--ital-green-100)'"
          >
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div>
                <span class="font-semibold" style="color: var(--ital-text)">{{ f.nombre }}</span>
                <span class="ml-1 text-xs" style="color: var(--ital-muted)">
                  {{ f.cedula }}{{ f.email ? ' · ' + f.email : '' }}
                </span>
              </div>
              <span class="chip" [style.color]="estiloFirma(f.estado).fg" [style.background]="estiloFirma(f.estado).bg">
                {{ estiloFirma(f.estado).label }}
              </span>
            </div>

            <div *ngIf="f.estado === 'firmada'" class="firma-detalle mt-1">
              ✍️ Firma digitada: <span class="firma-detalle__texto">"{{ f.firmaTexto }}"</span>
              · {{ f.fechaRespuesta | date: 'dd/MM/yyyy HH:mm' }}
              <div *ngIf="f.nota" class="mt-0.5">Observación: {{ f.nota }}</div>
            </div>

            <div *ngIf="f.estado === 'rechazada'" class="firma-detalle mt-1" style="color: var(--danger)">
              ⚠️ Novedad ({{ f.fechaRespuesta | date: 'dd/MM/yyyy HH:mm' }}): {{ f.nota }}
              <div class="mt-0.5" style="color: var(--ital-muted)">
                Se le indicó crear un ticket con el motivo para hacer seguimiento.
              </div>
            </div>

            <div *ngIf="f.estado === 'pendiente'" class="firma-detalle mt-1">
              Aún no firma este punto.
            </div>
          </div>
        </div>

        <div class="flex justify-end border-t px-5 py-4" style="border-color: var(--ital-green-100); background: var(--ital-cream)">
          <button type="button" class="btn-italfoods-secondary text-sm" (click)="cerrar()">Cerrar</button>
        </div>
      </div>
    </div>
  `,
})
export class ModalFirmasImplementacionComponent {
  @Input() open = false;
  @Input() tarea: ImplementacionTareaDto | null = null;

  @Output() cerrado = new EventEmitter<void>();

  readonly trackByFirma = (_: number, f: ImplementacionFirmaDto): number => f.id;
  readonly estiloFirma = estiloEstadoFirma;

  cerrar(): void {
    this.cerrado.emit();
  }
}

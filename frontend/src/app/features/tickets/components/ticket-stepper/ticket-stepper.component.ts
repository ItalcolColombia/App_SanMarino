// src/app/features/tickets/components/ticket-stepper/ticket-stepper.component.ts
import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

import {
  EstadoTicket, ESTADO_LABEL, ESTADO_BADGE, STEPPER_STEPS, ESTADOS_ESPECIALES,
} from '../../models/ticket.models';

interface Step {
  key: EstadoTicket;
  label: string;
  done: boolean;
  current: boolean;
}

/**
 * Barra de progreso (stepper) del estado del ticket.
 * Horizontal en desktop, vertical en móvil. Los estados especiales
 * (Transferido / Suspendido) no rompen la línea: se muestran como badge aparte.
 */
@Component({
  selector: 'app-ticket-stepper',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div>
      <!-- Con siete fases la barra no entra en pantallas medianas: scrollea DENTRO de su caja
           en vez de ensanchar la página (que arrastraba scroll horizontal a todo el detalle).
           w-max + min-w-full = ocupa todo el ancho cuando sobra, y crece con scroll cuando falta. -->
      <div class="-mx-1 overflow-x-auto px-1 pb-1">
        <ol class="flex flex-col gap-4 md:w-max md:min-w-full md:flex-row md:items-center md:gap-0" [class.opacity-60]="esEspecial">
        @for (step of steps; track step.key; let i = $index; let last = $last) {
          <li class="flex items-center gap-3 md:flex-1">
            <span
              class="grid h-8 w-8 shrink-0 place-items-center rounded-full text-xs font-semibold ring-4 transition"
              [class.bg-ital-green]="step.done"
              [class.text-white]="step.done || step.current"
              [class.bg-ital-orange]="step.current"
              [class.ring-ital-orange-100]="step.current"
              [class.ring-transparent]="!step.current"
              [class.bg-slate-200]="!step.done && !step.current"
              [class.text-slate-400]="!step.done && !step.current">
              @if (step.done) {
                <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3">
                  <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                </svg>
              } @else {
                {{ i + 1 }}
              }
            </span>
            <!-- Etiquetas: todas en móvil (la lista va vertical) y en lg+. Entre md y lg solo la
                 de la fase actual — las otras ni se renderizan, para no reservar su ancho. -->
            <span class="whitespace-nowrap text-xs md:hidden lg:inline"
              [class.font-semibold]="step.current"
              [class.text-slate-800]="step.done || step.current"
              [class.text-slate-400]="!step.done && !step.current">
              {{ step.label }}
            </span>
            @if (step.current) {
              <span class="hidden whitespace-nowrap text-xs font-semibold text-slate-800 md:inline lg:hidden">
                {{ step.label }}
              </span>
            }
            @if (!last) {
              <span class="mx-2 hidden h-0.5 flex-1 md:block"
                [class.bg-ital-green]="step.done"
                [class.bg-slate-200]="!step.done"></span>
            }
          </li>
        }
      </ol>
      </div>

      @if (esEspecial) {
        <div class="mt-4 flex items-center gap-2">
          <span class="text-xs text-slate-400">Estado actual:</span>
          <span class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset" [class]="especialBadge">
            <span class="h-1.5 w-1.5 rounded-full bg-current opacity-70"></span>
            {{ especialLabel }}
          </span>
        </div>
      }
    </div>
  `,
})
export class TicketStepperComponent {
  @Input({ required: true }) estado!: EstadoTicket;

  get esEspecial(): boolean { return ESTADOS_ESPECIALES.includes(this.estado); }
  get especialLabel(): string { return ESTADO_LABEL[this.estado] ?? this.estado; }
  get especialBadge(): string { return ESTADO_BADGE[this.estado] ?? ''; }

  get steps(): Step[] {
    const idx = STEPPER_STEPS.indexOf(this.estado);
    return STEPPER_STEPS.map((s, i) => ({
      key: s,
      label: ESTADO_LABEL[s],
      done: idx >= 0 && i < idx,
      current: idx >= 0 && i === idx,
    }));
  }
}

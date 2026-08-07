// src/app/features/tickets/components/ticket-prioridad-badge/ticket-prioridad-badge.component.ts
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { PRIORIDAD_BADGE, PRIORIDAD_LABEL, PrioridadTicket } from '../../models/ticket-tarea.models';

/**
 * Chip de prioridad del caso o de la tarea. Presentacional puro (solo `@Input`) ⇒ OnPush
 * es la estrategia correcta acá.
 */
@Component({
  selector: 'app-ticket-prioridad-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ring-1 ring-inset"
          [class]="clase">
      <svg class="h-2.5 w-2.5" viewBox="0 0 12 12" fill="currentColor" aria-hidden="true">
        @if (prioridad === 'CRITICA' || prioridad === 'ALTA') {
          <path d="M6 1.5 10.5 8.5H1.5z" />
        } @else if (prioridad === 'BAJA') {
          <path d="M6 10.5 1.5 3.5h9z" />
        } @else {
          <rect x="1.5" y="5" width="9" height="2" rx="1" />
        }
      </svg>
      {{ label }}
    </span>
  `,
})
export class TicketPrioridadBadgeComponent {
  @Input({ required: true }) prioridad: PrioridadTicket = 'MEDIA';

  get label(): string { return PRIORIDAD_LABEL[this.prioridad] ?? this.prioridad; }
  get clase(): string { return PRIORIDAD_BADGE[this.prioridad] ?? PRIORIDAD_BADGE.MEDIA; }
}

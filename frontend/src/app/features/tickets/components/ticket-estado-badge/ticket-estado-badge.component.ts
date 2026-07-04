// src/app/features/tickets/components/ticket-estado-badge/ticket-estado-badge.component.ts
import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { EstadoTicket, ESTADO_LABEL, ESTADO_BADGE } from '../../models/ticket.models';

/** Badge de color para el estado de un ticket. */
@Component({
  selector: 'app-ticket-estado-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <span
      class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset"
      [class]="badgeClass">
      <span class="h-1.5 w-1.5 rounded-full bg-current opacity-70"></span>
      {{ label }}
    </span>
  `,
})
export class TicketEstadoBadgeComponent {
  @Input({ required: true }) estado!: EstadoTicket;

  get label(): string {
    return ESTADO_LABEL[this.estado] ?? this.estado;
  }

  get badgeClass(): string {
    return ESTADO_BADGE[this.estado] ?? 'bg-slate-100 text-slate-600 ring-slate-200';
  }
}

// src/app/features/tickets/components/ticket-sla-chip/ticket-sla-chip.component.ts
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { EstadoSla, SLA_BADGE, SLA_LABEL } from '../../models/ticket-tarea.models';

/**
 * Semáforo del compromiso de solución. Muestra el estado y, si el caso sigue corriendo,
 * cuánto falta (o cuánto lleva vencido). Presentacional puro ⇒ OnPush.
 */
@Component({
  selector: 'app-ticket-sla-chip',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (estado !== 'SIN_SLA' || mostrarSinSla) {
      <span class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ring-1 ring-inset"
            [class]="clase" [title]="titulo">
        <svg class="h-3 w-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" aria-hidden="true">
          <circle cx="12" cy="12" r="9" />
          <path stroke-linecap="round" d="M12 7v5l3 2" />
        </svg>
        {{ texto }}
      </span>
    }
  `,
})
export class TicketSlaChipComponent {
  @Input({ required: true }) estado: EstadoSla = 'SIN_SLA';
  /** Horas restantes; negativas si ya venció. Null = no se muestra el contador. */
  @Input() horasParaVencer: number | null = null;
  /** Por defecto los casos sin compromiso no pintan chip (ruido innecesario en las listas). */
  @Input() mostrarSinSla = false;

  get clase(): string { return SLA_BADGE[this.estado] ?? SLA_BADGE.SIN_SLA; }

  get texto(): string {
    const base = SLA_LABEL[this.estado] ?? this.estado;
    if (this.horasParaVencer === null || this.estado === 'SIN_SLA') return base;
    if (this.estado === 'CUMPLIDO' || this.estado === 'INCUMPLIDO') return base;
    return `${base} · ${this.humano(this.horasParaVencer)}`;
  }

  get titulo(): string {
    if (this.horasParaVencer === null) return SLA_LABEL[this.estado] ?? this.estado;
    return this.horasParaVencer < 0
      ? `Vencido hace ${this.humano(this.horasParaVencer)}`
      : `Faltan ${this.humano(this.horasParaVencer)}`;
  }

  /** «3 d» / «5 h» / «40 min» — se usa el valor absoluto: el signo lo comunica el color. */
  private humano(horas: number): string {
    const abs = Math.abs(horas);
    if (abs >= 24) return `${Math.round(abs / 24)} d`;
    if (abs >= 1)  return `${Math.round(abs)} h`;
    return `${Math.max(1, Math.round(abs * 60))} min`;
  }
}

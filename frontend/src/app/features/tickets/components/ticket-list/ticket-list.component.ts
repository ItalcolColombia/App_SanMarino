// src/app/features/tickets/components/ticket-list/ticket-list.component.ts
import { Component, EventEmitter, Input, Output, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TicketListItem, TIPO_LABEL, ESTADO_BORDER } from '../../models/ticket.models';
import { TicketEstadoBadgeComponent } from '../ticket-estado-badge/ticket-estado-badge.component';

/**
 * Lista presentacional de tickets (cards mobile-first + paginación).
 * Reutilizada por las bandejas mis-tickets / gestión / admin. Cada card enlaza al detalle.
 */
@Component({
  selector: 'app-ticket-list',
  standalone: true,
  imports: [CommonModule, RouterLink, TicketEstadoBadgeComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    @if (loading) {
      <!-- Skeleton -->
      <div class="space-y-3">
        @for (s of [1,2,3,4]; track s) {
          <div class="h-[88px] animate-pulse rounded-2xl border-l-4 border-l-slate-200 bg-white p-4 shadow-sm ring-1 ring-slate-200/70">
            <div class="mb-2 flex gap-2">
              <div class="h-4 w-20 rounded bg-slate-100"></div>
              <div class="h-4 w-16 rounded bg-slate-100"></div>
            </div>
            <div class="h-4 w-2/3 rounded bg-slate-100"></div>
          </div>
        }
      </div>
    } @else if (items.length === 0) {
      <!-- Empty -->
      <div class="rounded-3xl border border-dashed border-slate-200 bg-white/60 px-6 py-16 text-center">
        <div class="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-2xl bg-ital-cream text-ital-orange">
          <svg class="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5.586a1 1 0 0 1 .707.293l5.414 5.414a1 1 0 0 1 .293.707V19a2 2 0 0 1-2 2Z" />
          </svg>
        </div>
        <p class="text-sm font-medium text-slate-600">{{ emptyText }}</p>
      </div>
    } @else {
      <div class="space-y-3">
        @for (t of items; track t.id) {
          <a [routerLink]="['/tickets', t.id]"
             class="group relative block overflow-hidden rounded-2xl border-l-4 bg-white p-4 shadow-sm ring-1 ring-slate-200/70 transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md hover:ring-ital-green/30 sm:p-5"
             [class]="border[t.estado]">
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0 flex-1">
                <div class="mb-1.5 flex flex-wrap items-center gap-2">
                  <span class="rounded-md bg-slate-900/[0.06] px-1.5 py-0.5 font-mono text-[11px] font-bold tracking-wider text-slate-500">{{ t.codigo }}</span>
                  <span class="inline-flex items-center rounded-md bg-ital-orange-50 px-1.5 py-0.5 text-[11px] font-semibold text-ital-orange-dark">{{ tipoLabel[t.tipo] }}</span>
                </div>
                <h3 class="truncate text-[15px] font-semibold text-slate-800 transition-colors group-hover:text-ital-green-dark">{{ t.titulo }}</h3>
                <div class="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ital-muted">
                  <span class="inline-flex items-center gap-1">
                    <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8"><path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5"/></svg>
                    {{ t.createdAt | date:'dd MMM yyyy' }}
                  </span>
                  <span class="inline-flex items-center gap-1">
                    <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8"><path stroke-linecap="round" stroke-linejoin="round" d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.5 19.5a7.5 7.5 0 0 1 15 0"/></svg>
                    {{ t.createdByNombre || ('#' + t.createdByUserId) }}
                    @if (t.createdByRol) { <span class="text-slate-400">· {{ t.createdByRol }}</span> }
                  </span>
                  @if (showAsignado && (t.assignedToNombre || t.assignedToUserId)) {
                    <span class="inline-flex items-center gap-1 text-ital-green-dark">
                      <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8"><path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5"/></svg>
                      {{ t.assignedToNombre || ('#' + t.assignedToUserId) }}
                      @if (t.assignedToRol) { <span class="text-slate-400">· {{ t.assignedToRol }}</span> }
                    </span>
                  }
                  @if (showPais) {
                    <span class="inline-flex items-center gap-1">
                      <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8"><path stroke-linecap="round" stroke-linejoin="round" d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"/><path stroke-linecap="round" stroke-linejoin="round" d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z"/></svg>
                      País {{ t.paisId }}
                    </span>
                  }
                  @if (t.cantidadImagenes > 0) {
                    <span class="inline-flex items-center gap-1">
                      <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8"><path stroke-linecap="round" stroke-linejoin="round" d="m2.25 15.75 5.159-5.159a2.25 2.25 0 0 1 3.182 0l5.159 5.159m-1.5-1.5 1.409-1.409a2.25 2.25 0 0 1 3.182 0l2.909 2.909M4.5 19.5h15a2.25 2.25 0 0 0 2.25-2.25V6.75A2.25 2.25 0 0 0 19.5 4.5h-15A2.25 2.25 0 0 0 2.25 6.75v10.5A2.25 2.25 0 0 0 4.5 19.5Z"/></svg>
                      {{ t.cantidadImagenes }}
                    </span>
                  }
                  @if (t.cantidadNotas > 0) {
                    <span class="inline-flex items-center gap-1">
                      <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8"><path stroke-linecap="round" stroke-linejoin="round" d="M8.625 12a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm0 0H8.25m4.125 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm0 0H12m4.125 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm0 0h-.375M21 12c0 4.556-4.03 8.25-9 8.25a9.764 9.764 0 0 1-2.555-.337A5.972 5.972 0 0 1 5.41 20.97a5.969 5.969 0 0 1-.474-.065 4.48 4.48 0 0 0 .978-2.025c.09-.457-.133-.901-.467-1.226C3.93 16.178 3 14.189 3 12c0-4.556 4.03-8.25 9-8.25s9 3.694 9 8.25Z"/></svg>
                      {{ t.cantidadNotas }}
                    </span>
                  }
                </div>
              </div>
              <div class="flex shrink-0 flex-col items-end gap-2">
                <app-ticket-estado-badge [estado]="t.estado"></app-ticket-estado-badge>
                <svg class="h-4 w-4 -translate-x-1 text-slate-300 opacity-0 transition-all group-hover:translate-x-0 group-hover:text-ital-green group-hover:opacity-100" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5"/></svg>
              </div>
            </div>
          </a>
        }
      </div>

      <!-- Paginación -->
      @if (totalPages > 1 || total > 0) {
        <div class="mt-5 flex items-center justify-between text-sm">
          <span class="text-ital-muted">{{ total }} ticket(s)</span>
          <div class="flex items-center gap-1.5">
            <button (click)="prev.emit()" [disabled]="page <= 1"
                    class="grid h-8 w-8 place-items-center rounded-lg bg-white ring-1 ring-slate-200 transition hover:bg-slate-50 disabled:opacity-40">
              <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5"/></svg>
            </button>
            <span class="px-2 text-slate-600">{{ page }} / {{ totalPages }}</span>
            <button (click)="next.emit()" [disabled]="page >= totalPages"
                    class="grid h-8 w-8 place-items-center rounded-lg bg-white ring-1 ring-slate-200 transition hover:bg-slate-50 disabled:opacity-40">
              <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5"/></svg>
            </button>
          </div>
        </div>
      }
    }
  `,
})
export class TicketListComponent {
  @Input() items: TicketListItem[] = [];
  @Input() loading = false;
  @Input() page = 1;
  @Input() totalPages = 1;
  @Input() total = 0;
  @Input() emptyText = 'No hay tickets para este filtro.';
  @Input() showAsignado = false;
  @Input() showPais = false;
  @Output() prev = new EventEmitter<void>();
  @Output() next = new EventEmitter<void>();

  readonly tipoLabel = TIPO_LABEL;
  readonly border = ESTADO_BORDER;
}

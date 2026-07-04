// src/app/features/tickets/pages/mis-asignados/mis-asignados.component.ts
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketService } from '../../services/ticket.service';
import {
  TicketListItem, PagedResult, EstadoTicket,
  ESTADOS_TICKET, ESTADO_LABEL, TIPOS_TICKET, TIPO_LABEL,
} from '../../models/ticket.models';
import { TicketListComponent } from '../../components/ticket-list/ticket-list.component';
import { ToastService } from '../../../../shared/services/toast.service';

/** Bandeja personal del resolutor: tickets asignados a mí. */
@Component({
  selector: 'app-mis-asignados',
  standalone: true,
  imports: [FormsModule, TicketListComponent],
  template: `
    <div class="min-h-full bg-gradient-to-b from-ital-cream/60 to-transparent">
      <div class="mx-auto max-w-5xl px-4 py-6 sm:px-6 sm:py-8">

        <div class="mb-6 flex items-center gap-3.5">
          <div class="grid h-12 w-12 place-items-center rounded-2xl bg-gradient-to-br from-indigo-500 to-indigo-700 text-white shadow-lg shadow-indigo-500/25">
            <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8"><path stroke-linecap="round" stroke-linejoin="round" d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.5 19.5a7.5 7.5 0 0 1 15 0"/></svg>
          </div>
          <div>
            <h1 class="text-2xl font-bold tracking-tight text-slate-800">Asignados a mí</h1>
            <p class="text-sm text-ital-muted">Tickets que te asignaron para gestionar.</p>
          </div>
        </div>

        <div class="mb-5 flex flex-wrap items-end gap-3 rounded-2xl bg-white p-3.5 shadow-sm ring-1 ring-slate-200/70">
          <div>
            <label class="mb-1 block text-xs font-medium text-ital-muted">Año</label>
            <select [(ngModel)]="anio" (ngModelChange)="load()"
                    class="rounded-lg border-slate-200 py-1.5 pl-3 pr-8 text-sm font-medium text-slate-700 shadow-sm focus:border-ital-green focus:ring-ital-green">
              @for (a of anios; track a) { <option [ngValue]="a">{{ a }}</option> }
            </select>
          </div>
          <div>
            <label class="mb-1 block text-xs font-medium text-ital-muted">Estado</label>
            <select [(ngModel)]="estado" (ngModelChange)="load()"
                    class="rounded-lg border-slate-200 py-1.5 pl-3 pr-8 text-sm font-medium text-slate-700 shadow-sm focus:border-ital-green focus:ring-ital-green">
              <option value="">Todos</option>
              @for (e of estados; track e) { <option [ngValue]="e">{{ estadoLabel[e] }}</option> }
            </select>
          </div>
          <div>
            <label class="mb-1 block text-xs font-medium text-ital-muted">Tipo</label>
            <select [(ngModel)]="tipo" (ngModelChange)="load()"
                    class="rounded-lg border-slate-200 py-1.5 pl-3 pr-8 text-sm font-medium text-slate-700 shadow-sm focus:border-ital-green focus:ring-ital-green">
              <option value="">Todos</option>
              @for (t of tipos; track t.value) { <option [ngValue]="t.value">{{ t.label }}</option> }
            </select>
          </div>
        </div>

        <app-ticket-list
          [items]="data()?.items ?? []"
          [loading]="loading()"
          [page]="page"
          [totalPages]="totalPages()"
          [total]="data()?.total ?? 0"
          [showAsignado]="false"
          emptyText="No tenés tickets asignados para este filtro."
          (prev)="prevPage()"
          (next)="nextPage()">
        </app-ticket-list>
      </div>
    </div>
  `,
})
export class MisAsignadosComponent implements OnInit {
  private readonly svc = inject(TicketService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(false);
  readonly data = signal<PagedResult<TicketListItem> | null>(null);
  readonly estados = ESTADOS_TICKET;
  readonly estadoLabel = ESTADO_LABEL;
  readonly tipos = TIPOS_TICKET;

  anio = new Date().getFullYear();
  estado: EstadoTicket | '' = '';
  tipo = '';
  page = 1;
  pageSize = 10;
  readonly anios = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i);
  readonly totalPages = computed(() => {
    const d = this.data(); return d ? Math.max(1, Math.ceil(d.total / d.pageSize)) : 1;
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.svc.asignados({ anio: this.anio, estado: this.estado || undefined, tipo: this.tipo || undefined, page: this.page, pageSize: this.pageSize })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => this.data.set(r),
        error: () => this.toast.error('No se pudo cargar la bandeja.'),
      });
  }

  prevPage(): void { if (this.page > 1) { this.page--; this.load(); } }
  nextPage(): void { if (this.page < this.totalPages()) { this.page++; this.load(); } }
}

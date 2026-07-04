// src/app/features/tickets/pages/gestion-tickets/gestion-tickets.component.ts
import { Component, DestroyRef, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketService } from '../../services/ticket.service';
import {
  TicketListItem, PagedResult, EstadoTicket, TipoTicket,
  ESTADOS_TICKET, ESTADO_LABEL, TIPOS_TICKET,
} from '../../models/ticket.models';
import { TicketListComponent } from '../../components/ticket-list/ticket-list.component';
import { ToastService } from '../../../../shared/services/toast.service';

/** Bandeja de gestión (Perfil B: Resolutor). País inyectado por el backend desde el request. */
@Component({
  selector: 'app-gestion-tickets',
  standalone: true,
  imports: [FormsModule, TicketListComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './gestion-tickets.component.html',
})
export class GestionTicketsComponent implements OnInit {
  private readonly svc = inject(TicketService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(false);
  readonly data = signal<PagedResult<TicketListItem> | null>(null);

  readonly estados = ESTADOS_TICKET;
  readonly estadoLabel = ESTADO_LABEL;
  readonly tipos = TIPOS_TICKET;

  anio: number = new Date().getFullYear();
  estado: EstadoTicket | '' = '';
  tipo: TipoTicket | '' = '';
  page = 1;
  pageSize = 10;

  readonly anios: number[] = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i);
  readonly totalPages = computed(() => {
    const d = this.data();
    return d ? Math.max(1, Math.ceil(d.total / d.pageSize)) : 1;
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.svc.gestion({
      anio: this.anio,
      estado: this.estado || undefined,
      tipo: this.tipo || undefined,
      page: this.page,
      pageSize: this.pageSize,
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => this.data.set(res),
        error: () => this.toast.error('No se pudo cargar la bandeja de gestión.'),
      });
  }

  onFilterChange(): void { this.page = 1; this.load(); }
  prevPage(): void { if (this.page > 1) { this.page--; this.load(); } }
  nextPage(): void { if (this.page < this.totalPages()) { this.page++; this.load(); } }
}

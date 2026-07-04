// src/app/features/tickets/pages/mis-tickets/mis-tickets.component.ts
import { Component, DestroyRef, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketService } from '../../services/ticket.service';
import {
  TicketListItem, PagedResult, EstadoTicket,
  ESTADOS_TICKET, ESTADO_LABEL, TIPO_LABEL, ESTADO_DOT, TICKET_PERMS,
} from '../../models/ticket.models';
import { TicketListComponent } from '../../components/ticket-list/ticket-list.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';

/** Bandeja "Mis solicitudes" (Perfil A: Solicitante).
 *  Si el usuario no tiene tickets.crear pero sí gestionar/admin,
 *  redirige a la bandeja correcta para evitar ver una lista vacía. */
@Component({
  selector: 'app-mis-tickets',
  standalone: true,
  imports: [FormsModule, RouterLink, TicketListComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './mis-tickets.component.html',
})
export class MisTicketsComponent implements OnInit {
  private readonly svc = inject(TicketService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly perm = inject(UserPermissionService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly data = signal<PagedResult<TicketListItem> | null>(null);

  readonly estados = ESTADOS_TICKET;
  readonly estadoLabel = ESTADO_LABEL;
  readonly tipoLabel = TIPO_LABEL;
  readonly estadoDot = ESTADO_DOT;

  // Filtros (filtro por año y estado, como pide el PRD)
  anio: number = new Date().getFullYear();
  estado: EstadoTicket | '' = '';
  page = 1;
  pageSize = 10;

  readonly anios: number[] = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i);
  readonly totalPages = computed(() => {
    const d = this.data();
    return d ? Math.max(1, Math.ceil(d.total / d.pageSize)) : 1;
  });

  ngOnInit(): void {
    // Usuarios sin tickets.crear no deben aterrizar aquí: su bandeja real es gestión/admin.
    if (!this.perm.has(TICKET_PERMS.crear)) {
      if (this.perm.has(TICKET_PERMS.admin)) {
        this.router.navigate(['/tickets/admin'], { replaceUrl: true });
        return;
      }
      if (this.perm.has(TICKET_PERMS.gestionar)) {
        this.router.navigate(['/tickets/gestion'], { replaceUrl: true });
        return;
      }
    }
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.svc.misTickets({
      anio: this.anio,
      estado: this.estado || undefined,
      page: this.page,
      pageSize: this.pageSize,
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => this.data.set(res),
        error: () => this.toast.error('No se pudieron cargar tus tickets.'),
      });
  }

  /** Reinicia a la primera página al cambiar un filtro. */
  onFilterChange(): void {
    this.page = 1;
    this.load();
  }

  /** Selecciona un estado desde el filtro segmentado. */
  selectEstado(e: EstadoTicket | ''): void {
    this.estado = e;
    this.onFilterChange();
  }

  prevPage(): void {
    if (this.page > 1) { this.page--; this.load(); }
  }

  nextPage(): void {
    if (this.page < this.totalPages()) { this.page++; this.load(); }
  }
}

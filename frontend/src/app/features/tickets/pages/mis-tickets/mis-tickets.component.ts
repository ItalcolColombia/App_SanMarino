// src/app/features/tickets/pages/mis-tickets/mis-tickets.component.ts
import { Component, DestroyRef, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketService } from '../../services/ticket.service';
import {
  TicketListItem, PagedResult, EstadoTicket,
  ESTADOS_TICKET, ESTADO_LABEL, TIPO_LABEL, ESTADO_DOT, ESTADO_BORDER, TICKET_PERMS,
} from '../../models/ticket.models';
import { TicketTimelineEvento } from '../../models/ticket-tarea.models';
import { TicketEstadoBadgeComponent } from '../../components/ticket-estado-badge/ticket-estado-badge.component';
import { TicketPrioridadBadgeComponent } from '../../components/ticket-prioridad-badge/ticket-prioridad-badge.component';
import { TicketSlaChipComponent } from '../../components/ticket-sla-chip/ticket-sla-chip.component';
import { TicketTimelineComponent } from '../../components/ticket-timeline/ticket-timeline.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';

/**
 * Bandeja "Mis solicitudes" (Perfil A: Solicitante).
 * Cada caso muestra su avance y despliega su línea de tiempo sin salir de la pantalla.
 * Si el usuario no tiene tickets.crear pero sí gestionar/admin, redirige a su bandeja real.
 */
@Component({
  selector: 'app-mis-tickets',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    TicketEstadoBadgeComponent, TicketPrioridadBadgeComponent, TicketSlaChipComponent,
    TicketTimelineComponent,
  ],
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

  /** Casos SOLUCIONADOS esperando que el solicitante confirme el cierre. */
  readonly pendientesConfirmar = signal(0);

  // Línea de tiempo desplegable, cacheada por caso para no repetir la consulta.
  readonly casoAbierto = signal<number | null>(null);
  readonly timelinePorCaso = signal<Record<number, TicketTimelineEvento[]>>({});
  readonly cargandoTimeline = signal(false);

  readonly estados = ESTADOS_TICKET;
  readonly estadoLabel = ESTADO_LABEL;
  readonly tipoLabel = TIPO_LABEL;
  readonly estadoDot = ESTADO_DOT;
  readonly estadoBorder = ESTADO_BORDER;

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

  readonly items = computed<TicketListItem[]>(() => this.data()?.items ?? []);

  ngOnInit(): void {
    // Usuarios sin tickets.crear no deben aterrizar aquí: su bandeja real es gestión/admin.
    if (!this.perm.has(TICKET_PERMS.crear)) {
      if (this.perm.has(TICKET_PERMS.admin)) {
        this.router.navigate(['/tickets/tablero'], { replaceUrl: true });
        return;
      }
      if (this.perm.has(TICKET_PERMS.gestionar)) {
        this.router.navigate(['/tickets/gestion'], { replaceUrl: true });
        return;
      }
    }
    this.load();
    this.cargarPendientes();
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
        next: res => {
          this.data.set(res);
          this.casoAbierto.set(null);
        },
        error: () => this.toast.error('No se pudieron cargar tus solicitudes.'),
      });
  }

  /** Cuenta los casos que esperan tu confirmación. Consulta liviana: solo se lee el total. */
  private cargarPendientes(): void {
    this.svc.misTickets({ anio: this.anio, estado: 'SOLUCIONADO', page: 1, pageSize: 1 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: r => this.pendientesConfirmar.set(r.total),
        error: () => this.pendientesConfirmar.set(0),
      });
  }

  /** Reinicia a la primera página al cambiar un filtro. */
  onFilterChange(): void {
    this.page = 1;
    this.load();
    this.cargarPendientes();
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

  // ── Línea de tiempo desplegable ──────────────────────────────

  alternarTimeline(caso: TicketListItem): void {
    if (this.casoAbierto() === caso.id) { this.casoAbierto.set(null); return; }

    this.casoAbierto.set(caso.id);
    if (this.timelinePorCaso()[caso.id]) return;   // ya está en caché

    this.cargandoTimeline.set(true);
    this.svc.timeline(caso.id)
      .pipe(finalize(() => this.cargandoTimeline.set(false)))
      .subscribe({
        next: eventos => this.timelinePorCaso.update(actual => ({ ...actual, [caso.id]: eventos })),
        error: () => this.toast.error('No se pudo cargar el seguimiento del caso.'),
      });
  }

  timelineDe(id: number): TicketTimelineEvento[] {
    return this.timelinePorCaso()[id] ?? [];
  }

  /** Días transcurridos desde la creación. */
  diasDesde(iso: string): number {
    return Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000));
  }
}

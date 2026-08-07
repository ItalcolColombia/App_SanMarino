// src/app/features/italjira/pages/configuracion/configuracion.component.ts
import { Component, DestroyRef, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketService } from '../../../tickets/services/ticket.service';
import {
  TicketListItem, PagedResult, EstadoTicket, TipoTicket,
  ESTADOS_TICKET, ESTADO_LABEL, TIPOS_TICKET, ResolutorAdminDto,
} from '../../../tickets/models/ticket.models';
import { TicketListComponent } from '../../../tickets/components/ticket-list/ticket-list.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { environment } from '../../../../../environments/environment';

interface PaisOpcion { paisId: number; paisNombre: string; }

/**
 * Configuración de ItalJira: bandeja global de casos (todos los tickets, sin filtro de empresa) y
 * administración de resolutores del módulo.
 *
 * Antes vivía en `/tickets/admin`. La ruta cambió a `/italjira/configuracion` no solo por la
 * mudanza de módulo: AWS WAF devuelve 403 a cualquier path que contenga `admin` — incidente ya
 * documentado en el repo.
 */
@Component({
  selector: 'app-italjira-configuracion',
  standalone: true,
  imports: [FormsModule, RouterLink, TicketListComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './configuracion.component.html',
})
export class ItalJiraConfiguracionComponent implements OnInit {
  private readonly svc = inject(TicketService);
  private readonly toast = inject(ToastService);
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(false);
  readonly data = signal<PagedResult<TicketListItem> | null>(null);

  readonly estados = ESTADOS_TICKET;
  readonly estadoLabel = ESTADO_LABEL;
  readonly tipos = TIPOS_TICKET;

  anio: number = new Date().getFullYear();
  estado: EstadoTicket | '' = '';
  tipo: TipoTicket | '' = '';
  paisId: number | null = null;
  assignedToGuid: string | null = null;
  page = 1;
  pageSize = 10;

  paises: PaisOpcion[] = [];
  resolutores: ResolutorAdminDto[] = [];

  readonly anios: number[] = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i);
  readonly totalPages = computed(() => {
    const d = this.data();
    return d ? Math.max(1, Math.ceil(d.total / d.pageSize)) : 1;
  });

  ngOnInit(): void {
    this.loadCatalogos();
    this.load();
  }

  private loadCatalogos(): void {
    this.http.get<PaisOpcion[]>(`${environment.apiUrl}/pais`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: p => this.paises = p, error: () => {} });

    this.svc.getResolutoresAdmin()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: r => this.resolutores = r, error: () => {} });
  }

  load(): void {
    this.loading.set(true);
    this.svc.admin({
      anio: this.anio,
      estado: this.estado || undefined,
      tipo: this.tipo || undefined,
      paisId: this.paisId ?? undefined,
      assignedToGuid: this.assignedToGuid ?? undefined,
      page: this.page,
      pageSize: this.pageSize,
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => this.data.set(res),
        error: () => this.toast.error('No se pudo cargar la bandeja de administración.'),
      });
  }

  onFilterChange(): void { this.page = 1; this.load(); }
  prevPage(): void { if (this.page > 1) { this.page--; this.load(); } }
  nextPage(): void { if (this.page < this.totalPages()) { this.page++; this.load(); } }
}

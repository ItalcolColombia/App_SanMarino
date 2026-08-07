// src/app/features/tickets/pages/tablero/tablero.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketService } from '../../services/ticket.service';
import {
  ESTADO_DOT, ESTADO_LABEL, ResolutorAdminDto, TIPOS_TICKET, TIPO_LABEL,
  TicketListItem, EstadoTicket, TipoTicket,
} from '../../models/ticket.models';
import {
  PRIORIDADES, PRIORIDAD_ACENTO, PRIORIDAD_LABEL, PrioridadTicket,
  TicketTablero, TicketTableroColumna,
} from '../../models/ticket-tarea.models';
import { TicketPrioridadBadgeComponent } from '../../components/ticket-prioridad-badge/ticket-prioridad-badge.component';
import { TicketSlaChipComponent } from '../../components/ticket-sla-chip/ticket-sla-chip.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { environment } from '../../../../../environments/environment';

interface PaisOpcion { paisId: number; paisNombre: string; }

/**
 * Tablero kanban de CASOS (perfil administrador): columnas por fase, tarjetas arrastrables,
 * filtros e indicadores de cabecera. Estado mutable + subscribe ⇒ `Eager` obligatorio.
 */
@Component({
  selector: 'app-tickets-tablero',
  standalone: true,
  imports: [
    FormsModule, RouterLink, CdkDropListGroup, CdkDropList, CdkDrag,
    TicketPrioridadBadgeComponent, TicketSlaChipComponent,
  ],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './tablero.component.html',
})
export class TableroComponent implements OnInit {
  private readonly svc = inject(TicketService);
  private readonly toast = inject(ToastService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly data = signal<TicketTablero | null>(null);
  readonly cargando = signal(false);
  readonly moviendo = signal(false);

  readonly estadoLabel = ESTADO_LABEL;
  readonly estadoDot = ESTADO_DOT;
  readonly tipoLabel = TIPO_LABEL;
  readonly tipos = TIPOS_TICKET;
  readonly prioridades = PRIORIDADES;
  readonly prioridadLabel = PRIORIDAD_LABEL;
  readonly prioridadAcento = PRIORIDAD_ACENTO;

  // Filtros
  anio: number | null = new Date().getFullYear();
  tipo: TipoTicket | '' = '';
  prioridad: PrioridadTicket | '' = '';
  paisId: number | null = null;
  assignedToGuid = '';
  texto = '';

  paises: PaisOpcion[] = [];
  resolutores: ResolutorAdminDto[] = [];

  readonly anios: number[] = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i);

  readonly columnas = computed<TicketTableroColumna[]>(() => this.data()?.columnas ?? []);
  readonly resumen = computed(() => this.data()?.resumen ?? null);

  /** Ids de las listas CDK; el grupo habilita arrastrar entre columnas. */
  readonly idsColumnas = computed(() => this.columnas().map(c => `caso-col-${c.estado}`));

  ngOnInit(): void {
    this.cargarCatalogos();
    this.cargar();
  }

  private cargarCatalogos(): void {
    this.http.get<PaisOpcion[]>(`${environment.apiUrl}/pais`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: p => this.paises = p, error: () => {} });

    this.svc.getResolutoresAdmin()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: r => this.resolutores = r, error: () => {} });
  }

  cargar(): void {
    this.cargando.set(true);
    this.svc.tablero({
      anio: this.anio ?? undefined,
      tipo: this.tipo || undefined,
      prioridad: this.prioridad || undefined,
      paisId: this.paisId ?? undefined,
      assignedToGuid: this.assignedToGuid || undefined,
      texto: this.texto.trim() || undefined,
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.cargando.set(false)))
      .subscribe({
        next: t => this.data.set(t),
        error: () => this.toast.error('No se pudo cargar el tablero.'),
      });
  }

  onFiltroChange(): void { this.cargar(); }

  limpiarFiltros(): void {
    this.anio = new Date().getFullYear();
    this.tipo = '';
    this.prioridad = '';
    this.paisId = null;
    this.assignedToGuid = '';
    this.texto = '';
    this.cargar();
  }

  abrirCaso(caso: TicketListItem): void {
    this.router.navigate(['/tickets', caso.id]);
  }

  // ── Drag & drop ──────────────────────────────────────────────
  soltar(evento: CdkDragDrop<TicketTableroColumna>): void {
    const caso: TicketListItem = evento.item.data;
    const destino = evento.container.data.estado;
    const mismaColumna = evento.previousContainer === evento.container;
    if (mismaColumna && evento.previousIndex === evento.currentIndex) return;

    // Snapshot para revertir: el backend puede rechazar la transición.
    const anterior = this.data();

    this.aplicarMovimientoLocal(evento, caso, destino);
    this.moviendo.set(true);

    this.svc.moverCaso(caso.id, { estado: destino, indice: evento.currentIndex })
      .pipe(finalize(() => this.moviendo.set(false)))
      .subscribe({
        next: () => { this.toast.success(`${caso.codigo ?? 'Caso'} → ${ESTADO_LABEL[destino]}.`); this.cargar(); },
        error: e => {
          this.data.set(anterior);
          this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo mover el caso.');
        },
      });
  }

  /**
   * Mueve la tarjeta en memoria para que la UI responda al instante. Se reconstruyen las
   * columnas con arrays nuevos (el signal necesita una referencia distinta para notificar).
   */
  private aplicarMovimientoLocal(
    evento: CdkDragDrop<TicketTableroColumna>, caso: TicketListItem, destino: EstadoTicket): void {
    const actual = this.data();
    if (!actual) return;

    const origen = evento.previousContainer.data.estado;

    const columnas = actual.columnas.map(col => {
      if (col.estado === origen && origen !== destino) {
        const items = col.items.filter(i => i.id !== caso.id);
        return { ...col, items, total: Math.max(0, col.total - 1) };
      }
      if (col.estado === destino) {
        const items = col.items.filter(i => i.id !== caso.id);
        items.splice(Math.max(0, Math.min(evento.currentIndex, items.length)), 0, { ...caso, estado: destino });
        return { ...col, items, total: origen === destino ? col.total : col.total + 1 };
      }
      return col;
    });

    this.data.set({ ...actual, columnas });
  }

  // ── Helpers de plantilla ─────────────────────────────────────
  iniciales(nombre: string | null): string {
    if (!nombre) return '?';
    return nombre.trim().split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }

  /** Días transcurridos desde la creación (para el chip de antigüedad de la tarjeta). */
  diasDesde(iso: string): number {
    const ms = Date.now() - new Date(iso).getTime();
    return Math.max(0, Math.floor(ms / 86_400_000));
  }
}

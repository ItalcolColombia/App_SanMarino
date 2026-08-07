// src/app/features/tickets/components/ticket-filtros/ticket-filtros.component.ts
import {
  ChangeDetectionStrategy, Component, DestroyRef, EventEmitter, Input, OnInit, Output,
  inject, signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TicketService } from '../../services/ticket.service';
import {
  ESTADOS_TICKET, ESTADO_LABEL, ResolutorAdminDto, TIPOS_TICKET, EstadoTicket, TipoTicket,
} from '../../models/ticket.models';
import {
  EstadoSla, PRIORIDADES, PRIORIDAD_LABEL, PrioridadTicket, SLA_LABEL, TicketTableroFiltro,
} from '../../models/ticket-tarea.models';
import { environment } from '../../../../../environments/environment';

interface PaisOpcion { paisId: number; paisNombre: string; }
interface EmpresaOpcion { id: number; name: string; }

const OPCIONES_SLA: { value: EstadoSla | ''; label: string }[] = [
  { value: '',           label: 'Todos' },
  { value: 'VENCIDO',    label: SLA_LABEL.VENCIDO },
  { value: 'POR_VENCER', label: SLA_LABEL.POR_VENCER },
  { value: 'EN_TIEMPO',  label: SLA_LABEL.EN_TIEMPO },
  { value: 'CUMPLIDO',   label: SLA_LABEL.CUMPLIDO },
  { value: 'INCUMPLIDO', label: SLA_LABEL.INCUMPLIDO },
  { value: 'SIN_SLA',    label: SLA_LABEL.SIN_SLA },
];

/**
 * Barra de filtros compartida por el tablero, el roadmap y el panel de control.
 *
 * Existe para que las tres vistas filtren EXACTAMENTE igual: si cada una armara los suyos,
 * el Excel del panel dejaría de coincidir con lo que se ve en el tablero. Emite el
 * `TicketTableroFiltro` ya construido; la página solo recarga.
 *
 * Estado mutable + subscribe ⇒ `Eager` obligatorio.
 */
@Component({
  selector: 'app-ticket-filtros',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './ticket-filtros.component.html',
})
export class TicketFiltrosComponent implements OnInit {
  /** En el tablero el estado ES la columna: filtrarlo vaciaría las demás. */
  @Input() ocultarEstado = false;
  /** Arranca con el bloque avanzado abierto (el panel lo quiere desplegado). */
  @Input() expandidoInicial = false;

  @Output() cambio = new EventEmitter<TicketTableroFiltro>();

  private readonly svc = inject(TicketService);
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly estados = ESTADOS_TICKET;
  readonly estadoLabel = ESTADO_LABEL;
  readonly tipos = TIPOS_TICKET;
  readonly prioridades = PRIORIDADES;
  readonly prioridadLabel = PRIORIDAD_LABEL;
  readonly opcionesSla = OPCIONES_SLA;

  readonly expandido = signal(false);

  // ── Estado de los filtros ────────────────────────────────────
  anio: number | null = new Date().getFullYear();
  texto = '';
  tipo: TipoTicket | '' = '';
  prioridad: PrioridadTicket | '' = '';
  estado: EstadoTicket | '' = '';
  estadoSla: EstadoSla | '' = '';
  assignedToGuid = '';
  desde = '';
  hasta = '';
  paisIds: number[] = [];
  companyIds: number[] = [];

  paises: PaisOpcion[] = [];
  empresas: EmpresaOpcion[] = [];
  resolutores: ResolutorAdminDto[] = [];

  readonly anios: number[] = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i);

  ngOnInit(): void {
    this.expandido.set(this.expandidoInicial);
    this.cargarCatalogos();
    // Primera emisión FUERA del ciclo de detección en curso: si se emite acá mismo, el padre
    // prende su `cargando` mientras Angular está verificando su propia vista y salta NG0100.
    queueMicrotask(() => this.emitir());
  }

  private cargarCatalogos(): void {
    this.http.get<PaisOpcion[]>(`${environment.apiUrl}/pais`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: p => this.paises = p ?? [], error: () => {} });

    // Ruta "global" (no "admin"): el WAF bloquea cualquier path con /admin.
    this.http.get<EmpresaOpcion[]>(`${environment.apiUrl}/Company/global`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: e => this.empresas = e ?? [], error: () => {} });

    this.svc.getResolutoresAdmin()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: r => this.resolutores = r ?? [], error: () => {} });
  }

  /** Arma el filtro y lo publica. */
  emitir(): void {
    this.cambio.emit({
      anio: this.anio ?? undefined,
      texto: this.texto.trim() || undefined,
      tipo: this.tipo || undefined,
      prioridad: this.prioridad || undefined,
      estado: this.ocultarEstado ? undefined : (this.estado || undefined),
      estadoSla: this.estadoSla || undefined,
      assignedToGuid: this.assignedToGuid || undefined,
      desde: this.desde || undefined,
      hasta: this.hasta || undefined,
      paisIds: this.paisIds.length ? this.paisIds : undefined,
      companyIds: this.companyIds.length ? this.companyIds : undefined,
    });
  }

  alternarPais(paisId: number): void {
    this.paisIds = this.paisIds.includes(paisId)
      ? this.paisIds.filter(p => p !== paisId)
      : [...this.paisIds, paisId];
    this.emitir();
  }

  paisSeleccionado(paisId: number): boolean { return this.paisIds.includes(paisId); }

  alternarEmpresa(companyId: number): void {
    this.companyIds = this.companyIds.includes(companyId)
      ? this.companyIds.filter(c => c !== companyId)
      : [...this.companyIds, companyId];
    this.emitir();
  }

  empresaSeleccionada(companyId: number): boolean { return this.companyIds.includes(companyId); }

  alternarExpandido(): void { this.expandido.update(v => !v); }

  limpiar(): void {
    this.anio = new Date().getFullYear();
    this.texto = '';
    this.tipo = '';
    this.prioridad = '';
    this.estado = '';
    this.estadoSla = '';
    this.assignedToGuid = '';
    this.desde = '';
    this.hasta = '';
    this.paisIds = [];
    this.companyIds = [];
    this.emitir();
  }

  /**
   * Cuenta los recortes aplicados. Es un método y no un `computed` a propósito: los filtros son
   * campos mutables, no señales, y un `computed` sobre ellos se quedaría con el valor viejo.
   *
   * El año NO cuenta: siempre hay uno puesto y sumarlo daría la impresión de que el tablero está
   * filtrado cuando en realidad está en su estado normal.
   */
  activos(): number {
    let n = 0;
    if (this.texto.trim()) n++;
    if (this.tipo) n++;
    if (this.prioridad) n++;
    if (!this.ocultarEstado && this.estado) n++;
    if (this.estadoSla) n++;
    if (this.assignedToGuid) n++;
    if (this.desde || this.hasta) n++;
    if (this.paisIds.length) n++;
    if (this.companyIds.length) n++;
    return n;
  }
}

// src/app/features/tickets/pages/panel/panel.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketService } from '../../services/ticket.service';
import {
  ESTADOS_TICKET, ESTADO_DOT, ESTADO_LABEL, ResolutorAdminDto, TIPOS_TICKET, TIPO_LABEL,
  EstadoTicket, TipoTicket,
} from '../../models/ticket.models';
import {
  EstadoSla, PRIORIDADES, PRIORIDAD_ACENTO, PRIORIDAD_LABEL, PrioridadTicket,
  SLA_LABEL, TicketIndicadores, TicketTableroFiltro,
} from '../../models/ticket-tarea.models';
import { ToastService } from '../../../../shared/services/toast.service';
import { exportarMultiHojaExcel, HojaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { fechaHoraCorta } from '../../../../shared/utils/format';
import { environment } from '../../../../../environments/environment';

interface PaisOpcion { paisId: number; paisNombre: string; }
interface EmpresaOpcion { id: number; name: string; }

/** Semáforos disponibles para filtrar. */
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
 * Panel de control del administrador: indicadores del conjunto filtrado (volumen, efectividad,
 * tiempos y desgloses por país / estado / tipo / prioridad / responsable) y la descarga del
 * reporte detallado a Excel. Estado mutable + subscribe ⇒ `Eager` obligatorio.
 */
@Component({
  selector: 'app-tickets-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './panel.component.html',
})
export class PanelComponent implements OnInit {
  private readonly svc = inject(TicketService);
  private readonly toast = inject(ToastService);
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly data = signal<TicketIndicadores | null>(null);
  readonly cargando = signal(false);
  readonly descargando = signal(false);

  readonly estados = ESTADOS_TICKET;
  readonly estadoLabel = ESTADO_LABEL;
  readonly estadoDot = ESTADO_DOT;
  readonly tipos = TIPOS_TICKET;
  readonly tipoLabel = TIPO_LABEL;
  readonly prioridades = PRIORIDADES;
  readonly prioridadLabel = PRIORIDAD_LABEL;
  readonly prioridadAcento = PRIORIDAD_ACENTO;
  readonly opcionesSla = OPCIONES_SLA;

  // ── Filtros ──────────────────────────────────────────────────
  /** Selección múltiple de países: vacío = todos. */
  paisIds: number[] = [];
  /** Selección múltiple de empresas: vacío = todas. */
  companyIds: number[] = [];
  tipo: TipoTicket | '' = '';
  estado: EstadoTicket | '' = '';
  prioridad: PrioridadTicket | '' = '';
  estadoSla: EstadoSla | '' = '';
  assignedToGuid = '';
  texto = '';
  desde = '';
  hasta = '';

  paises: PaisOpcion[] = [];
  empresas: EmpresaOpcion[] = [];
  resolutores: ResolutorAdminDto[] = [];

  readonly resumen = computed(() => this.data()?.resumen ?? null);

  /** Escala para las barras del desglose: el mayor total manda el 100 %. */
  readonly maxPorEstado = computed(() =>
    Math.max(1, ...(this.data()?.porEstado ?? []).map(e => e.total)));
  readonly maxPorPais = computed(() =>
    Math.max(1, ...(this.data()?.porPais ?? []).map(p => p.total)));
  readonly maxPorEmpresa = computed(() =>
    Math.max(1, ...(this.data()?.porEmpresa ?? []).map(p => p.total)));

  ngOnInit(): void {
    this.cargarCatalogos();
    this.cargar();
  }

  private cargarCatalogos(): void {
    this.http.get<PaisOpcion[]>(`${environment.apiUrl}/pais`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: p => this.paises = p, error: () => {} });

    // Ruta "global" (no "admin"): el WAF bloquea cualquier path con /admin.
    this.http.get<EmpresaOpcion[]>(`${environment.apiUrl}/Company/global`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: e => this.empresas = e ?? [], error: () => {} });

    this.svc.getResolutoresAdmin()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: r => this.resolutores = r, error: () => {} });
  }

  /** Filtro vigente — el mismo objeto alimenta indicadores y reporte. */
  private filtro(): TicketTableroFiltro {
    return {
      paisIds: this.paisIds.length ? this.paisIds : undefined,
      companyIds: this.companyIds.length ? this.companyIds : undefined,
      tipo: this.tipo || undefined,
      estado: this.estado || undefined,
      prioridad: this.prioridad || undefined,
      estadoSla: this.estadoSla || undefined,
      assignedToGuid: this.assignedToGuid || undefined,
      texto: this.texto.trim() || undefined,
      desde: this.desde || undefined,
      hasta: this.hasta || undefined,
    };
  }

  cargar(): void {
    this.cargando.set(true);
    this.svc.indicadores(this.filtro())
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.cargando.set(false)))
      .subscribe({
        next: d => this.data.set(d),
        error: () => this.toast.error('No se pudieron cargar los indicadores.'),
      });
  }

  onFiltroChange(): void { this.cargar(); }

  /** Marca/desmarca un país del multi-select. */
  alternarPais(paisId: number): void {
    this.paisIds = this.paisIds.includes(paisId)
      ? this.paisIds.filter(p => p !== paisId)
      : [...this.paisIds, paisId];
    this.cargar();
  }

  paisSeleccionado(paisId: number): boolean { return this.paisIds.includes(paisId); }

  /** Marca/desmarca una empresa del multi-select. */
  alternarEmpresa(companyId: number): void {
    this.companyIds = this.companyIds.includes(companyId)
      ? this.companyIds.filter(c => c !== companyId)
      : [...this.companyIds, companyId];
    this.cargar();
  }

  empresaSeleccionada(companyId: number): boolean { return this.companyIds.includes(companyId); }

  limpiarFiltros(): void {
    this.paisIds = [];
    this.companyIds = [];
    this.tipo = '';
    this.estado = '';
    this.prioridad = '';
    this.estadoSla = '';
    this.assignedToGuid = '';
    this.texto = '';
    this.desde = '';
    this.hasta = '';
    this.cargar();
  }

  // ── Descarga del reporte ─────────────────────────────────────

  descargarExcel(): void {
    this.descargando.set(true);
    this.svc.reporte(this.filtro())
      .pipe(finalize(() => this.descargando.set(false)))
      .subscribe({
        next: r => {
          if (!r.casos.length) {
            this.toast.warning('No hay casos para los filtros elegidos.');
            return;
          }
          exportarMultiHojaExcel(this.armarHojas(r), {
            filenameBase: 'Reporte_Tickets',
            subtitles: r.filtrosAplicados,
          });
          this.toast.success(`Reporte descargado: ${r.casos.length} caso(s).`);
        },
        error: () => this.toast.error('No se pudo generar el reporte.'),
      });
  }

  /** Cinco hojas: indicadores, países, casos, tareas y tiempos. */
  private armarHojas(r: import('../../models/ticket-tarea.models').TicketReporte): HojaExcel[] {
    const res = r.indicadores.resumen;
    const filtros = r.filtrosAplicados;

    const indicadores: HojaExcel = {
      sheetName: 'Indicadores',
      title: 'Indicadores de gestión de tickets',
      subtitles: filtros,
      headers: ['Indicador', 'Valor'],
      rows: [
        ['Casos totales', res.total],
        ['Abiertos (sin arrancar)', res.abiertos],
        ['En curso', res.enCurso],
        ['Solucionados', res.solucionados],
        ['Cerrados', res.cerrados],
        ['Suspendidos', res.suspendidos],
        ['% resueltos', res.porcentajeResueltos],
        ['Con compromiso de solución', res.conCompromiso],
        ['Compromisos cumplidos', res.compromisoCumplido],
        ['Efectividad (%)', res.efectividad ?? 'Sin compromisos'],
        ['Vencidos', res.vencidos],
        ['Por vencer', res.porVencer],
        ['Sin responsable', res.sinAsignar],
        ['Promedio primera respuesta (h)', res.promedioPrimeraRespuesta ?? 'Sin datos'],
        ['Promedio resolución (h)', res.promedioResolucion ?? 'Sin datos'],
        ['Promedio confirmación de cierre (h)', res.promedioConfirmacionCierre ?? 'Sin datos'],
        ['Tareas totales', res.tareasTotal],
        ['Tareas terminadas', res.tareasListas],
        ['Tareas pendientes', res.tareasPendientes],
        ['Avance de tareas (%)', res.avanceTareas],
        ['Horas estimadas', res.horasEstimadas],
        ['Horas registradas', res.horasRegistradas],
      ],
    };

    const paises: HojaExcel = {
      sheetName: 'Por país',
      title: 'Desglose por país',
      subtitles: filtros,
      headers: ['País', 'Casos', 'Abiertos', 'En curso', 'Resueltos', 'Vencidos',
                'Horas registradas', 'Avance tareas (%)', 'Prom. resolución (h)', 'Efectividad (%)'],
      rows: r.indicadores.porPais.map(p => [
        p.nombre, p.total, p.abiertos, p.enCurso, p.resueltos, p.vencidos,
        p.horasRegistradas, p.avanceTareas, p.promedioResolucion ?? '', p.efectividad ?? '',
      ]),
    };

    const empresas: HojaExcel = {
      sheetName: 'Por empresa',
      title: 'Desglose por empresa',
      subtitles: filtros,
      headers: ['Empresa', 'Casos', 'Abiertos', 'En curso', 'Resueltos', 'Vencidos',
                'Horas registradas', 'Avance tareas (%)', 'Prom. resolución (h)', 'Efectividad (%)'],
      rows: r.indicadores.porEmpresa.map(e => [
        e.nombre, e.total, e.abiertos, e.enCurso, e.resueltos, e.vencidos,
        e.horasRegistradas, e.avanceTareas, e.promedioResolucion ?? '', e.efectividad ?? '',
      ]),
    };

    const casos: HojaExcel = {
      sheetName: 'Casos',
      title: 'Detalle de casos',
      subtitles: filtros,
      headers: ['Código', 'País', 'Empresa', 'Tipo', 'Estado', 'Prioridad', 'Título',
                'Solicitante', 'Correo solicitante', 'Registrado por', 'Responsable',
                'Creado', 'Primera apertura', 'Solucionado', 'Cerrado', 'Compromiso', 'SLA',
                'Primera respuesta (h)', 'Resolución (h)',
                'Inicio planificado', 'Fin planificado',
                'Horas estimadas', 'Horas registradas', 'Desvío (h)',
                'Tareas', 'Tareas listas', 'Avance (%)', 'Solución'],
      rows: r.casos.map(c => [
        c.codigo, c.pais, c.empresa, this.tipoLabel[c.tipo as TipoTicket] ?? c.tipo,
        this.estadoLabel[c.estado as EstadoTicket] ?? c.estado,
        this.prioridadLabel[c.prioridad as PrioridadTicket] ?? c.prioridad, c.titulo,
        c.solicitante, c.solicitanteEmail, c.registradoPor, c.responsable,
        this.fecha(c.createdAt), this.fecha(c.primeraApertura), this.fecha(c.fechaSolucion),
        this.fecha(c.fechaCierre), this.fecha(c.fechaLimite),
        SLA_LABEL[c.estadoSla] ?? c.estadoSla,
        c.horasPrimeraRespuesta ?? '', c.horasResolucion,
        this.soloFecha(c.fechaInicioPlan), this.soloFecha(c.fechaFinPlan),
        c.horasEstimadas ?? '', c.horasRegistradas, c.desvioHoras ?? '',
        c.tareasTotal, c.tareasListas, c.avanceTareas, c.solucionDescripcion,
      ]),
    };

    const tareas: HojaExcel = {
      sheetName: 'Tareas',
      title: 'Detalle de tareas',
      subtitles: filtros,
      headers: ['Caso', 'Título del caso', 'País', 'Código tarea', 'Tipo', 'Estado', 'Prioridad',
                'Tarea', 'Responsable', 'Horas estimadas', 'Horas registradas',
                'Inicio planificado', 'Fin planificado', 'Inicio real', 'Fin real', 'Creada'],
      rows: r.tareas.map(t => [
        t.codigoCaso, t.tituloCaso, t.pais, t.codigo, t.tipo, t.estado, t.prioridad, t.titulo,
        t.responsable, t.horasEstimadas ?? '', t.horasRegistradas,
        this.soloFecha(t.fechaInicioPlan), this.soloFecha(t.fechaFinPlan),
        this.fecha(t.fechaInicioReal), this.fecha(t.fechaFinReal), this.fecha(t.createdAt),
      ]),
    };

    const tiempos: HojaExcel = {
      sheetName: 'Tiempos',
      title: 'Registro de tiempos',
      subtitles: filtros,
      headers: ['Caso', 'Título del caso', 'País', 'Tarea', 'Persona', 'Fecha', 'Horas', 'Detalle'],
      rows: r.tiempos.map(w => [
        w.codigoCaso, w.tituloCaso, w.pais, w.tarea, w.persona,
        this.soloFecha(w.fecha), w.horas, w.descripcion,
      ]),
    };

    // Las hojas de detalle vacías se omiten: una pestaña sin filas confunde más de lo que aporta.
    return [indicadores, paises, empresas, casos,
            ...(tareas.rows.length ? [tareas] : []),
            ...(tiempos.rows.length ? [tiempos] : [])];
  }

  // ── Helpers de plantilla ─────────────────────────────────────

  /** «2 h», «1,5 d», «40 min» — null se muestra como raya. */
  horas(valor: number | null | undefined): string {
    if (valor === null || valor === undefined) return '—';
    if (valor >= 24) return `${(valor / 24).toFixed(1).replace('.0', '')} d`;
    if (valor >= 1) return `${Math.round(valor)} h`;
    return `${Math.max(1, Math.round(valor * 60))} min`;
  }

  porcentaje(valor: number | null | undefined): string {
    return valor === null || valor === undefined ? '—' : `${valor}%`;
  }

  /** Ancho de la barra del desglose, en % del máximo de su grupo. */
  ancho(valor: number, max: number): number {
    return max <= 0 ? 0 : Math.round((valor * 100) / max);
  }

  // Los desgloses llegan con la clave como `string` (el backend no conoce los tipos del front),
  // así que la búsqueda en los mapas se hace acá y no indexando en la plantilla.

  dotDeEstado(clave: string): string {
    return ESTADO_DOT[clave as EstadoTicket] ?? 'bg-slate-300';
  }

  acentoDePrioridad(clave: string): string {
    return PRIORIDAD_ACENTO[clave as PrioridadTicket] ?? 'bg-slate-300';
  }

  labelDePrioridad(clave: string, fallback: string): string {
    return PRIORIDAD_LABEL[clave as PrioridadTicket] ?? fallback;
  }

  private fecha(iso: string | null): string { return iso ? fechaHoraCorta(iso) : ''; }

  /** `yyyy-MM-dd` → `dd/MM/yyyy`, sin pasar por Date (evita corrimientos de zona). */
  private soloFecha(ymd: string | null): string {
    if (!ymd) return '';
    const [a, m, d] = ymd.slice(0, 10).split('-');
    return `${d}/${m}/${a}`;
  }
}

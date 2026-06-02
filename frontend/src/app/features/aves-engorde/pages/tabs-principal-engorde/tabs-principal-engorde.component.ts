import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import { SeguimientoLoteLevanteDto, LoteRegistroHistoricoUnificadoDto, SeguimientoDiarioTablaFilaDto } from '../../services/seguimiento-aves-engorde.service';
import { LoteDto, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';
import { TablaIndicadoresDiariosEngordeComponent } from '../tabla-indicadores-diarios-engorde/tabla-indicadores-diarios-engorde.component';
import { GraficasIndicadoresDiariosEngordeComponent } from '../graficas-indicadores-diarios-engorde/graficas-indicadores-diarios-engorde.component';
import { GraficasProductividadEngordeComponent } from '../graficas-productividad-engorde/graficas-productividad-engorde.component';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { TEXTO_FORMULA_SALDO_ALIMENTO_TOOLTIP } from '../../utils/saldo-alimento-engorde.util';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { ModalCuadrarSaldosEngordeComponent } from '../modal-cuadrar-saldos-engorde/modal-cuadrar-saldos-engorde.component';

/** Texto explicativo del saldo de alimento (modal de ayuda en seguimiento diario). */
export const TEXTO_AYUDA_SEGUIMIENTO_DIARIO_ENGORDE = `Orden cronológico por fecha de registro. Ingreso/traslado/documento y despachos vienen del historial unificado. El saldo de alimento (kg) parte del stock ya registrado en el histórico con fecha anterior al primer día de seguimiento; a partir de ahí se aplican ingresos, traslados de entrada, ajustes; restas por traslado de salida, eliminaciones y consumo del día en seguimiento (hembras + machos); no se duplica INV_CONSUMO del histórico. Tras cada movimiento el saldo no baja de 0 kg: si el consumo supera lo disponible, queda en 0 y los ingresos o traslados de entrada posteriores suman sobre ese saldo disponible.`;

@Component({
  selector: 'app-tabs-principal-engorde',
  standalone: true,
  imports: [CommonModule, FormsModule, TablaIndicadoresDiariosEngordeComponent, GraficasIndicadoresDiariosEngordeComponent, GraficasProductividadEngordeComponent, HasPermissionDirective, ModalCuadrarSaldosEngordeComponent],
  templateUrl: './tabs-principal-engorde.component.html',
  styleUrls: ['./tabs-principal-engorde.component.scss']
})
export class TabsPrincipalEngordeComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() resumenLevante: LoteMortalidadResumenDto | null = null;
  @Input() loading: boolean = false;
  @Input() disableCreateEditDelete: boolean = false;
  /** Solo bloquea editar/eliminar (lote cerrado operativamente). No impide crear nuevos registros. */
  @Input() disableEditDelete: boolean = false;
  @Input() showExportSeguimientoExcel: boolean = false;
  @Input() exportSeguimientoLoteNombre: string = '';
  @Input() historicoUnificado: LoteRegistroHistoricoUnificadoDto[] = [];
  /** Tabla diaria precalculada por fn_seguimiento_diario_engorde (Ecuador). */
  @Input() tablaFilas: SeguimientoDiarioTablaFilaDto[] = [];
  /** Engorde: siempre true desde la lista (para traer ventas/ingresos/traslados). */
  @Input() enriquecerTablaConHistoricoInventario = true;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoLoteLevanteDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoLoteLevanteDto>();
  /** Emitido cuando se aplicaron correcciones de cuadre de saldos y hay que recargar datos. */
  @Output() saldosCuadrados = new EventEmitter<void>();

  activeTab: 'general' | 'indicadores' | 'grafica' = 'general';
  isAdmin: boolean = false;
  /** País activo: condiciona qué set de gráficas se muestra en la pestaña Gráficas. */
  isEcuador: boolean = false;
  isPanama: boolean = false;

  /** Tooltip columna saldo alimento: fórmula explícita (validación de negocio). */
  readonly textoFormulaSaldoAlimento = TEXTO_FORMULA_SALDO_ALIMENTO_TOOLTIP;

  readonly textoAyudaSeguimientoDiario = TEXTO_AYUDA_SEGUIMIENTO_DIARIO_ENGORDE;
  readonly semanasFiltroOpciones = [1, 2, 3, 4, 5, 6, 7, 8] as const;

  /** Modal ayuda (saldo / histórico). */
  modalAyudaSeguimientoAbierto = false;
  /** Modal cuadrar saldos. */
  modalCuadrarSaldosAbierto = false;

  /** Filtros tabla seguimiento (solo vista; no altera datos del servidor). */
  filtroFechaDesde = '';
  filtroFechaHasta = '';
  /** null = todas las semanas */
  filtroSemana: number | null = null;
  /** '' = todos los tipos */
  filtroTipoAlimento = '';

  constructor(
    private storageService: TokenStorageService,
    private countryFilter: CountryFilterService
  ) {}

  ngOnInit(): void {
    const session = this.storageService.get();
    this.isAdmin = !!session?.user?.roles?.includes('Admin');
    this.isEcuador = this.countryFilter.isEcuador();
    this.isPanama = this.countryFilter.isPanama();
  }

  ngOnChanges(_changes: SimpleChanges): void {
    // tablaFilas llega precalculada del padre vía fn_seguimiento_diario_engorde.
    // No hay construcción local; diarioFilasFiltradas filtra directamente sobre tablaFilas.
  }

  @HostListener('document:keydown.escape')
  onEscapeCerrarAyuda(): void {
    if (this.modalAyudaSeguimientoAbierto) this.modalAyudaSeguimientoAbierto = false;
  }

  // ─── Columnas ────────────────────────────────────────────────────────────

  /** Columnas de la tabla de registros diarios (sin Acciones). */
  get colspanRegistroDiario(): number {
    // +2 por las columnas de peso individual de despacho (R3.5)
    return 30 + (this.enriquecerTablaConHistoricoInventario ? 3 : 0);
  }

  // segId puede ser null (movs sin seguimiento, fix #14) → usar fecha como fallback único para trackBy
  trackByDiarioFila = (_: number, f: SeguimientoDiarioTablaFilaDto) => f.segId ?? `mov-${f.fecha}`;

  // ─── Filtros ─────────────────────────────────────────────────────────────

  /** Tipos de alimento distintos en los registros del lote (para el select de filtro). */
  get opcionesTipoAlimento(): string[] {
    const s = new Set<string>();
    for (const x of this.tablaFilas ?? []) {
      const t = (x.tipoAlimento ?? '').trim();
      if (t) s.add(t);
    }
    return [...s].sort((a, b) => a.localeCompare(b, 'es'));
  }

  get hayFiltrosDiarioActivos(): boolean {
    return !!(
      (this.filtroFechaDesde && this.filtroFechaDesde.trim()) ||
      (this.filtroFechaHasta && this.filtroFechaHasta.trim()) ||
      this.filtroSemana != null ||
      (this.filtroTipoAlimento && this.filtroTipoAlimento.trim())
    );
  }

  /** Filas visibles según filtros; con acumConsumoKg recalculado en el subconjunto. */
  get diarioFilasFiltradas(): SeguimientoDiarioTablaFilaDto[] {
    const base = this.tablaFilas ?? [];
    if (!this.hayFiltrosDiarioActivos) return base;
    const filtered = base.filter(f => this.pasaFiltrosDiario(f));
    if (filtered.length === 0) return [];
    let acum = 0;
    return filtered.map(f => {
      acum += f.consumoDiaKg;
      return { ...f, acumConsumoKg: acum };
    });
  }

  get diarioFilasVaciasPorFiltro(): boolean {
    return (this.tablaFilas?.length ?? 0) > 0 && this.diarioFilasFiltradas.length === 0 && this.hayFiltrosDiarioActivos;
  }

  limpiarFiltrosDiario(): void {
    this.filtroFechaDesde = '';
    this.filtroFechaHasta = '';
    this.filtroSemana = null;
    this.filtroTipoAlimento = '';
  }

  private pasaFiltrosDiario(f: SeguimientoDiarioTablaFilaDto): boolean {
    const ymd = this.toYMD(f.fecha);
    const desde = (this.filtroFechaDesde || '').trim();
    const hasta = (this.filtroFechaHasta || '').trim();
    if (desde && ymd && ymd < desde) return false;
    if (hasta && ymd && ymd > hasta) return false;
    if (this.filtroSemana != null && f.semana !== this.filtroSemana) return false;
    const ft = (this.filtroTipoAlimento || '').trim();
    if (ft) {
      const full = (f.tipoAlimento || '').trim().toLowerCase();
      if (full !== ft.toLowerCase()) return false;
    }
    return true;
  }

  // ─── Acciones ────────────────────────────────────────────────────────────

  onTabChange(tab: 'general' | 'indicadores' | 'grafica'): void { this.activeTab = tab; }
  onCreate(): void { this.create.emit(); }
  onEdit(seg: SeguimientoLoteLevanteDto): void { this.edit.emit(seg); }
  onDelete(id: number | null): void {
    if (id == null) return; // Movimiento sin seguimiento → no se puede eliminar
    this.delete.emit(id);
  }
  onViewDetail(seg: SeguimientoLoteLevanteDto): void { this.viewDetail.emit(seg); }

  // Las firmas aceptan number | null (fix #14: filas sin seguimiento tienen segId=null)
  onViewDetailById(segId: number | null): void {
    if (segId == null) return; // Movimiento sin seguimiento → no hay detalle que ver
    const seg = this.seguimientos.find(s => s.id === segId);
    if (seg) this.viewDetail.emit(seg);
  }
  onEditById(segId: number | null): void {
    if (segId == null) return;
    const seg = this.seguimientos.find(s => s.id === segId);
    if (seg) this.edit.emit(seg);
  }

  // ─── Tooltip saldo alimento ───────────────────────────────────────────────

  titleSaldoAlimentoCelda(_f?: SeguimientoDiarioTablaFilaDto): string {
    return this.textoFormulaSaldoAlimento;
  }

  // ─── Exportación Excel ───────────────────────────────────────────────────

  exportSeguimientoDiarioExcel(): void {
    if (!this.showExportSeguimientoExcel || !this.diarioFilasFiltradas?.length) return;
    const headers = [
      'Fecha',
      'Semana',
      'Edad (días vida)',
      'Día (calendario)',
      'Mortalidad hembras',
      'Mortalidad machos',
      'Selección hembras',
      'Selección machos',
      'TOTAL MORT+ SEL / DÍA',
      'Despacho hembras',
      'Despacho machos',
      ...(this.enriquecerTablaConHistoricoInventario
        ? ['Despacho mixtas', 'Consumo bodega (kg)', 'Saldo alimento (kg)']
        : []),
      'Saldo aves vivas',
      'Tipo alimento',
      'Ingreso alimento',
      'Traslado',
      'Documento',
      'Consumo kg hembras',
      'Consumo kg machos',
      'Consumo real día (kg)',
      'Consumo acumulado (kg)',
      'Agua (litros)',
      '% pérdidas del día',
      'Peso prom. hembras (kg)',
      'Peso prom. machos (kg)',
      'Observaciones'
    ];
    const rows = this.diarioFilasFiltradas.map(f => [
      this.formatDMY(f.fecha),
      f.semana,
      f.edadDia,
      this.formatDiaSemanaCorto(f.fecha),
      f.mortalidadHembras ?? '',
      f.mortalidadMachos ?? '',
      f.selH ?? '',
      f.selM ?? '',
      f.totalMortSelDia,
      f.despachoHembras || '',
      f.despachoMachos || '',
      ...(this.enriquecerTablaConHistoricoInventario
        ? [
            f.despachoMixtas || '',
            f.consumoBodegaKg || '',
            f.saldoAlimentoKg ?? ''
          ]
        : []),
      f.saldoAves,
      this.tipoAlimentoCorto(f.tipoAlimento),
      f.ingresoAlimentoKg > 0 ? `${f.ingresoAlimentoKg} kg` : '',
      this.buildTrasladoTexto(f),
      f.documento ?? '',
      f.consumoKgHembras ?? '',
      f.consumoKgMachos ?? 0,
      f.consumoDiaKg,
      f.acumConsumoKg,
      f.consumoAguaDiario ?? '',
      f.pctPerdidasDia != null ? Math.round(f.pctPerdidasDia * 100) / 100 : '',
      f.pesoPromHembras ?? '',
      f.pesoPromMachos ?? '',
      (f.observaciones || '').trim()
    ]);
    const titleBase = this.exportSeguimientoLoteNombre.trim()
      ? `Seguimiento diario pollo engorde — Lote: ${this.exportSeguimientoLoteNombre.trim()}`
      : 'Seguimiento diario pollo engorde';
    const title = this.hayFiltrosDiarioActivos ? `${titleBase} (filtros aplicados)` : titleBase;
    const aoa: (string | number)[][] = [[title], [], headers, ...rows];
    const ws = XLSX.utils.aoa_to_sheet(aoa);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Seguimiento');
    const safe = (this.exportSeguimientoLoteNombre.trim() || 'seguimiento_engorde').replace(/[\\/:*?"<>|]/g, '_');
    const d = new Date();
    const stamp = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
    XLSX.writeFile(wb, `Seguimiento_engorde_${safe}_${stamp}.xlsx`);
  }

  // ─── Helpers visuales (sin cálculos de negocio) ──────────────────────────

  buildTrasladoTexto(f: SeguimientoDiarioTablaFilaDto): string {
    const parts: string[] = [];
    if (f.trasladoEntradaKg > 0) parts.push(`Entrada ${f.trasladoEntradaKg} kg`);
    if (f.trasladoSalidaKg > 0) parts.push(`Salida ${f.trasladoSalidaKg} kg`);
    return parts.join(' · ');
  }

  tipoAlimentoCorto(tipo: string | null | undefined): string {
    const t = (tipo ?? '').toUpperCase();
    if (t.includes('PRE')) return 'PRE';
    if (t.includes('INI')) return 'INI';
    if (t.includes('ENG')) return 'ENG';
    if (t.includes('FIN')) return 'FIN-D';
    if (!tipo?.trim()) return '—';
    return tipo.length > 8 ? tipo.slice(0, 8) + '…' : tipo;
  }

  formatDiaSemanaCorto(iso: string | Date | null | undefined): string {
    const ymd = this.toYMD(iso);
    if (!ymd) return '';
    const d = new Date(`${ymd}T12:00:00`);
    if (isNaN(d.getTime())) return '';
    try {
      return new Intl.DateTimeFormat('es-CO', { weekday: 'short', day: 'numeric', month: 'short' }).format(d);
    } catch {
      return '';
    }
  }

  calcularEdadDias(fechaRegistro: string | Date): number {
    if (!this.selectedLote?.fechaEncaset) return 0;
    const encYmd = this.toYMD(this.selectedLote.fechaEncaset);
    const regYmd = this.toYMD(fechaRegistro);
    if (!encYmd || !regYmd) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const enc = this.ymdToLocalNoonDate(encYmd);
    const reg = this.ymdToLocalNoonDate(regYmd);
    if (!enc || !reg) return 0;
    return Math.max(0, Math.floor((reg.getTime() - enc.getTime()) / MS_DAY));
  }

  private toYMD(input: string | Date | null | undefined): string | null {
    if (input == null || input === '') return null;
    if (input instanceof Date && !isNaN(input.getTime())) {
      return `${input.getFullYear()}-${String(input.getMonth() + 1).padStart(2, '0')}-${String(input.getDate()).padStart(2, '0')}`;
    }
    const s = String(input).trim();
    const head = s.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (head) return `${head[1]}-${head[2]}-${head[3]}`;
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    return null;
  }

  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) return null;
    const d = new Date(`${ymd}T12:00:00`);
    return isNaN(d.getTime()) ? null : d;
  }

  formatDMY(input: string | Date | null | undefined): string {
    const ymd = this.toYMD(input);
    if (!ymd) return '';
    const [y, m, day] = ymd.split('-');
    return `${day}/${m}/${y}`;
  }
}

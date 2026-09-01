import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, HostListener, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { exportarTablaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
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
import { TabReproductoraEngordeComponent } from '../../components/tab-reproductora-engorde/tab-reproductora-engorde.component';
import { CuadrarSaldosEngordeApi } from '../../../engorde-comun/services/cuadrar-saldos-engorde.api';
import { SeguimientoAvesEngordeService } from '../../services/seguimiento-aves-engorde.service';
import {
  desplazamientoPrimerDia,
  diaDeNegocioDesdeEdad,
  semanaDeNegocio
} from '../../../engorde-comun/funciones/dia-negocio-engorde.funcion';
import {
  esConsumoAlimentoMixto,
  esConsumoAlimentoPorGenero
} from '../../funciones/modo-consumo-alimento-fila.funcion';

/** Texto explicativo del saldo de alimento (modal de ayuda en seguimiento diario). */
export const TEXTO_AYUDA_SEGUIMIENTO_DIARIO_ENGORDE = `Orden cronológico por fecha de registro. Ingreso/traslado/documento y despachos vienen del historial unificado. El saldo de alimento (kg) parte del stock ya registrado en el histórico con fecha anterior al primer día de seguimiento; a partir de ahí se aplican ingresos, traslados de entrada, ajustes; restas por traslado de salida, eliminaciones y consumo del día en seguimiento (hembras + machos); no se duplica INV_CONSUMO del histórico. Tras cada movimiento el saldo no baja de 0 kg: si el consumo supera lo disponible, queda en 0 y los ingresos o traslados de entrada posteriores suman sobre ese saldo disponible.`;

@Component({
  selector: 'app-tabs-principal-engorde',
  standalone: true,
  imports: [CommonModule, FormsModule, TablaIndicadoresDiariosEngordeComponent, GraficasIndicadoresDiariosEngordeComponent, GraficasProductividadEngordeComponent, HasPermissionDirective, ModalCuadrarSaldosEngordeComponent, TabReproductoraEngordeComponent],
  templateUrl: './tabs-principal-engorde.component.html',
  styleUrls: ['./tabs-principal-engorde.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  providers: [{ provide: CuadrarSaldosEngordeApi, useExisting: SeguimientoAvesEngordeService }]
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
  /** Controla la visibilidad del tab R. Reproductora. */
  @Input() tieneReproductoras: boolean = false;
  /** Hora de llegada de las aves del lote seleccionado (HH:mm). Sin hora, el desplazamiento es 0. */
  @Input() horaEncasetamiento: string | null = null;

  /**
   * Doble validación. La tabla no decide nada: recibe el mapa `seguimientoId → estado` que arma el
   * contenedor desde el backend, que es el único que conoce el flag de la empresa y el plazo.
   */
  @Input() requiereValidacion = false;
  /** Si el usuario tiene el permiso de validar. Sin él la columna se ve, pero sin el botón. */
  @Input() puedeValidar = false;
  /** seguimientoId → VALIDADO | PENDIENTE | EN_RETRASO. Lo que no está en el mapa, está validado. */
  @Input() estadoValidacionPorId = new Map<number, string>();

  @Output() validar = new EventEmitter<number>();
  /** Contracara de `validar`: devuelve alimento y aves y vuelve a dejar el registro editable. */
  @Output() quitarValidacion = new EventEmitter<number>();
  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoLoteLevanteDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoLoteLevanteDto>();
  /** Emitido cuando se aplicaron correcciones de cuadre de saldos y hay que recargar datos. */
  @Output() saldosCuadrados = new EventEmitter<void>();

  activeTab: 'general' | 'reproductora' | 'indicadores' | 'grafica' = 'general';

  /**
   * Lote LIQUIDADO ⇒ la tabla diaria que llega en `tablaFilas` es la COPIA CONGELADA
   * (fn_seguimiento_diario_engorde v13 devuelve la foto guardada al liquidar, no un recálculo).
   * La señal es la misma de los gates del backend: estado_operativo_lote === 'Cerrado'.
   */
  get loteLiquidado(): boolean {
    return (this.selectedLote?.estadoOperativoLote ?? '').trim().toLowerCase() === 'cerrado';
  }

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
    // Base 30 (no-Panamá).
    // Panamá: fórmula antigua era +2 (consumo H+M extra). Una sesión mergeó 4 pares H/M en Mixto → −4.
    // Ago-2026: se suma la columna «Consumo mixto (kg)» → +1. Net: +2 − 4 + 1 = −1.
    return 30
      + (this.enriquecerTablaConHistoricoInventario ? 3 : 0)
      + (this.isPanama ? -1 : 0);
  }

  // ─── Consumo de alimento: por género (días 1–7, cruce reproductora) vs mixto ──────────────────
  // El desglose por sexo solo existe en las filas que vienen del cruce de lotes reproductora. A
  // partir del día 8 el registro se hace desde este módulo con una sola ración mixta que se persiste
  // en `consumoKgHembras`; mostrarla bajo «hembras» hacía leer que solo comían las hembras.
  // La regla vive en `funciones/modo-consumo-alimento-fila.funcion.ts` (pura, con tests).

  /** true cuando la fila trae consumo desglosado H/M → se llenan las columnas Hembras y Machos. */
  esConsumoPorGenero(f: SeguimientoDiarioTablaFilaDto): boolean {
    return esConsumoAlimentoPorGenero(f);
  }

  /** true cuando la fila trae una ración mixta → se llena la columna Consumo mixto. */
  esConsumoMixto(f: SeguimientoDiarioTablaFilaDto): boolean {
    return esConsumoAlimentoMixto(f);
  }

  // segId puede ser null (movs sin seguimiento, fix #14) → usar fecha como fallback único para trackBy
  trackByDiarioFila = (_: number, f: SeguimientoDiarioTablaFilaDto) => f.segId ?? `mov-${f.fecha}`;

  // ─── Numeración del día (presentación) ───────────────────────────────────
  // El backend manda la EDAD (0 el día del encaset) y sigue siendo la que usan la guía genética,
  // los indicadores, el informe semanal y la liquidación. Acá se muestra el DÍA DE NEGOCIO: el
  // primer día con registro del lote es el día 1, igual que en reproductora.

  /** Días que se corre el primer día con registro por la hora de llegada: 0 o 1. */
  private get desplazamientoPrimerDia(): number {
    return desplazamientoPrimerDia(this.horaEncasetamiento);
  }

  /** Número de día que se muestra en la columna «Edad»: 1 el primer día con registro. */
  diaNegocio(f: SeguimientoDiarioTablaFilaDto): number {
    return diaDeNegocioDesdeEdad(f.edadDia, this.desplazamientoPrimerDia);
  }

  /**
   * Semana del día de negocio (1..7 → semana 1). Sin desplazamiento da exactamente el mismo número
   * que `f.semana` del backend, así que solo cambia algo en un lote que llegó tarde.
   */
  semanaNegocio(f: SeguimientoDiarioTablaFilaDto): number {
    return semanaDeNegocio(this.diaNegocio(f));
  }

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
    // Filtra por la MISMA semana que ve el usuario en la tabla (día de negocio), no por la del backend.
    if (this.filtroSemana != null && this.semanaNegocio(f) !== this.filtroSemana) return false;
    const ft = (this.filtroTipoAlimento || '').trim();
    if (ft) {
      const full = (f.tipoAlimento || '').trim().toLowerCase();
      if (full !== ft.toLowerCase()) return false;
    }
    return true;
  }

  // ─── Acciones ────────────────────────────────────────────────────────────

  onTabChange(tab: 'general' | 'reproductora' | 'indicadores' | 'grafica'): void { this.activeTab = tab; }
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
      // El consumo del día cae en UNA de las tres: H/M solo en las filas del cruce reproductora
      // (días 1–7), Mixto de ahí en adelante. Las tres suman «Consumo real día (kg)».
      'Consumo kg hembras',
      'Consumo kg machos',
      'Consumo kg mixto',
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
      this.semanaNegocio(f),
      this.diaNegocio(f),
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
      this.esConsumoPorGenero(f) ? (f.consumoKgHembras ?? '') : '',
      this.esConsumoPorGenero(f) ? (f.consumoKgMachos ?? 0) : '',
      this.esConsumoMixto(f) ? f.consumoDiaKg : '',
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
    exportarTablaExcel(headers, rows, {
      filenameBase: `Seguimiento_engorde_${this.exportSeguimientoLoteNombre.trim() || 'seguimiento_engorde'}`,
      sheetName: 'Seguimiento',
      title,
    });
  }

  // ─── Helpers visuales (sin cálculos de negocio) ──────────────────────────

  buildTrasladoTexto(f: SeguimientoDiarioTablaFilaDto): string {
    const parts: string[] = [];
    if (f.trasladoEntradaKg > 0) parts.push(`Entrada ${f.trasladoEntradaKg} kg`);
    if (f.trasladoSalidaKg > 0) parts.push(`Salida ${f.trasladoSalidaKg} kg`);
    return parts.join(' · ');
  }

  // ─── «Ingreso inicial del ciclo» (v15: alimento previo al encaset, visible) ──────────────────
  // La fn ya absorbía este alimento en el saldo de apertura desde v9; v15 solo lo expone en la
  // primera fila del ciclo vía `aperturaAlimentoKg`/`aperturaDocumentos`. El saldo NO cambia acá,
  // solo la presentación de esa fila. Gate doble (flag Y campo) para que, si algún día este mismo
  // componente se usa con `enriquecerTablaConHistoricoInventario=false`, quede invisible.

  /** true solo en la fila del día 1 con alimento absorbido en la apertura del ciclo. */
  hayIngresoInicialCiclo(f: SeguimientoDiarioTablaFilaDto): boolean {
    return this.enriquecerTablaConHistoricoInventario && (f.aperturaAlimentoKg ?? 0) > 0;
  }

  /** Tooltip del badge de ingreso inicial: documentos/facturas reales absorbidos en la apertura. */
  tituloIngresoInicialCiclo(f: SeguimientoDiarioTablaFilaDto): string {
    return (f.aperturaDocumentos || '').trim();
  }

  /** Celda «Documento»: concatena el documento propio del día con los de apertura, si los hay. */
  documentoCeldaTexto(f: SeguimientoDiarioTablaFilaDto): string {
    const propio = (f.documento || '').trim();
    const apertura = this.hayIngresoInicialCiclo(f) ? (f.aperturaDocumentos || '').trim() : '';
    if (propio && apertura) return `${propio} · ${apertura}`;
    return propio || apertura;
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

  // ─── Doble validación ───────────────────────────────────────────────────────

  /**
   * Estado de la fila. Lo que no está en el mapa se considera VALIDADO: el backend solo devuelve los
   * pendientes, así que ausencia es "ya se descontó" — y con el flag apagado el mapa viene vacío, que
   * deja toda la tabla como estaba.
   */
  estadoValidacionFila(segId: number | null | undefined): string {
    if (segId == null) return 'VALIDADO';
    return this.estadoValidacionPorId.get(segId) ?? 'VALIDADO';
  }

  /** Texto del badge. */
  etiquetaValidacionFila(segId: number | null | undefined): string {
    switch (this.estadoValidacionFila(segId)) {
      case 'EN_RETRASO': return 'En retraso';
      case 'PENDIENTE':  return 'Pendiente';
      default:           return 'Validado';
    }
  }

  /** Clase del badge. */
  claseBadgeValidacion(segId: number | null | undefined): string {
    switch (this.estadoValidacionFila(segId)) {
      case 'EN_RETRASO': return 'badge-validacion badge-validacion--retraso';
      case 'PENDIENTE':  return 'badge-validacion badge-validacion--pendiente';
      default:           return 'badge-validacion badge-validacion--validado';
    }
  }

  /** Clase de la FILA: solo se pinta la vencida, para que el rojo siga significando algo. */
  claseFilaValidacion(segId: number | null | undefined): string {
    return this.estadoValidacionFila(segId) === 'EN_RETRASO' ? 'fila-validacion--retraso' : '';
  }

  /** Tooltip: dice qué implica el estado, que es lo que el usuario necesita saber. */
  tooltipValidacionFila(segId: number | null | undefined): string {
    switch (this.estadoValidacionFila(segId)) {
      case 'EN_RETRASO':
        return 'En retraso — superó el plazo de validación. Mientras no se valide, el lote no acepta días nuevos.';
      case 'PENDIENTE':
        return 'Pendiente de validar — el alimento y las aves están separados, todavía no descontados. Se puede editar y eliminar.';
      default:
        return 'Validado — el alimento y las aves ya se descontaron. El registro es de solo lectura.';
    }
  }

  /** ¿Esta fila puede validarse? Solo las que siguen pendientes y con permiso. */
  puedeValidarFila(segId: number | null | undefined): boolean {
    return this.puedeValidar && this.estadoValidacionFila(segId) !== 'VALIDADO';
  }

  /** Un registro validado es de solo lectura: hay que quitarle la validación para corregirlo. */
  esSoloLecturaPorValidacion(segId: number | null | undefined): boolean {
    return this.requiereValidacion && this.estadoValidacionFila(segId) === 'VALIDADO';
  }

  /**
   * ¿Se le puede quitar la validación a esta fila?
   *
   * 🔴 Es la contracara del botón ✓, y faltaba. El backend expone `desvalidar` desde el primer día y
   * todos los mensajes de rechazo mandan a usarlo («hay que quitarle la validación primero»), pero
   * NINGÚN componente lo llamaba: con el flag encendido, un registro validado quedaba sin ninguna
   * vía de corrección desde la pantalla. La salida que la gente encontraba —borrar y recrear— es la
   * que hacía desaparecer el alimento sin devolverlo.
   */
  puedeQuitarValidacionFila(segId: number | null | undefined): boolean {
    return this.puedeValidar && this.estadoValidacionFila(segId) === 'VALIDADO';
  }

  onValidar(segId: number | null | undefined): void {
    if (segId == null) return;
    this.validar.emit(segId);
  }

  onQuitarValidacion(segId: number | null | undefined): void {
    if (segId == null) return;
    this.quitarValidacion.emit(segId);
  }
}

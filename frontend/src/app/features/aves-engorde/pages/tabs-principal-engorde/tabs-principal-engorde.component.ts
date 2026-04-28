import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-aves-engorde.service';
import { LoteDto, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';
import { TablaIndicadoresDiariosEngordeComponent } from '../tabla-indicadores-diarios-engorde/tabla-indicadores-diarios-engorde.component';
import { GraficasIndicadoresDiariosEngordeComponent } from '../graficas-indicadores-diarios-engorde/graficas-indicadores-diarios-engorde.component';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { LoteRegistroHistoricoUnificadoDto } from '../../services/seguimiento-aves-engorde.service';
import { TEXTO_FORMULA_SALDO_ALIMENTO_TOOLTIP } from '../../utils/saldo-alimento-engorde.util';

/** Texto explicativo del saldo de alimento (modal de ayuda en seguimiento diario). */
export const TEXTO_AYUDA_SEGUIMIENTO_DIARIO_ENGORDE = `Orden cronológico por fecha de registro. Ingreso/traslado/documento y despachos vienen del historial unificado. El saldo de alimento (kg) parte del stock ya registrado en el histórico con fecha anterior al primer día de seguimiento; a partir de ahí se aplican ingresos, traslados de entrada, ajustes; restas por traslado de salida, eliminaciones y consumo del día en seguimiento (hembras + machos); no se duplica INV_CONSUMO del histórico. Tras cada movimiento el saldo no baja de 0 kg: si el consumo supera lo disponible, queda en 0 y los ingresos o traslados de entrada posteriores suman sobre ese saldo disponible.`;

/** Totales del historial unificado por una fecha (YYYY-MM-DD), alineados con el backend. */
interface AggregadoHistoricoDia {
  ingresoKg: number;
  /** Desglose de ingreso por item_resumen (para evitar mezclar alimentos diferentes el mismo día). */
  ingresoKgPorItem: Map<string, number>;
  trasladoEntradaKg: number;
  trasladoSalidaKg: number;
  consumoBodegaKg: number;
  refsDocumento: string[];
  ventaH: number;
  ventaM: number;
  ventaX: number;
}

/** Fila enriquecida para la tabla de registros diarios (libro de seguimiento / pestaña Seguimiento). */
export interface RegistroDiarioTablaFilaEngorde {
  seg: SeguimientoLoteLevanteDto;
  edadDia: number;
  semana: number;
  diaCorto: string;
  totalMortSelDia: number;
  saldoAves: number;
  consumoDiaKg: number;
  acumConsumoKg: number;
  ingresoAlimento: string;
  traslado: string;
  documento: string;
  despachoH: number | null;
  despachoM: number | null;
  despachoX: number | null;
  consumoBodegaKg: number | null;
  tipoAlimentoCorto: string;
  pctPerdidasDia: number | null;
  /** Saldo alimento (kg) al cierre de la fila (≥ 0; existencias disponibles). Sin duplicar INV_CONSUMO del histórico. */
  saldoAlimentoKgMostrado: number | null;
}

@Component({
  selector: 'app-tabs-principal-engorde',
  standalone: true,
  imports: [CommonModule, FormsModule, TablaIndicadoresDiariosEngordeComponent, GraficasIndicadoresDiariosEngordeComponent],
  templateUrl: './tabs-principal-engorde.component.html',
  styleUrls: ['./tabs-principal-engorde.component.scss']
})
export class TabsPrincipalEngordeComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() resumenLevante: LoteMortalidadResumenDto | null = null;
  @Input() loading: boolean = false;
  @Input() disableCreateEditDelete: boolean = false;
  @Input() showExportSeguimientoExcel: boolean = false;
  @Input() exportSeguimientoLoteNombre: string = '';
  @Input() historicoUnificado: LoteRegistroHistoricoUnificadoDto[] = [];
  /** Engorde: siempre true desde la lista (para traer ventas/ingresos/traslados). */
  @Input() enriquecerTablaConHistoricoInventario = true;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoLoteLevanteDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoLoteLevanteDto>();

  activeTab: 'general' | 'indicadores' | 'grafica' = 'general';
  isAdmin: boolean = false;
  diarioFilas: RegistroDiarioTablaFilaEngorde[] = [];
  /** Hay al menos un registro sin stock previo al primer consumo (desde encasetamiento). */
  advertenciaSaldoSinIngresoPrevio = false;

  /** Tooltip columna saldo alimento: fórmula explícita (validación de negocio). */
  readonly textoFormulaSaldoAlimento = TEXTO_FORMULA_SALDO_ALIMENTO_TOOLTIP;

  readonly textoAyudaSeguimientoDiario = TEXTO_AYUDA_SEGUIMIENTO_DIARIO_ENGORDE;
  readonly semanasFiltroOpciones = [1, 2, 3, 4, 5, 6, 7, 8] as const;

  /** Modal ayuda (saldo / histórico). */
  modalAyudaSeguimientoAbierto = false;

  /** Filtros tabla seguimiento (solo vista; no altera datos del servidor). */
  filtroFechaDesde = '';
  filtroFechaHasta = '';
  /** null = todas las semanas */
  filtroSemana: number | null = null;
  /** '' = todos los tipos */
  filtroTipoAlimento = '';

  constructor(private storageService: TokenStorageService) {}

  ngOnInit(): void {
    const session = this.storageService.get();
    this.isAdmin = !!session?.user?.roles?.includes('Admin');
  }

  @HostListener('document:keydown.escape')
  onEscapeCerrarAyuda(): void {
    if (this.modalAyudaSeguimientoAbierto) this.modalAyudaSeguimientoAbierto = false;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote'] || changes['historicoUnificado'] || changes['enriquecerTablaConHistoricoInventario']) {
      this.diarioFilas = this.buildDiarioFilas();
    }
  }

  /** Columnas de la tabla de registros diarios (sin Acciones). */
  get colspanRegistroDiario(): number {
    // 28 columnas visibles (incluye despachoH/M, ingreso, traslado, documento, agua) + (histórico: 3)
    return 28 + (this.enriquecerTablaConHistoricoInventario ? 3 : 0);
  }

  trackByDiarioFila = (_: number, f: RegistroDiarioTablaFilaEngorde) => f.seg.id;

  /** Tipos de alimento distintos en los registros del lote (para filtro). */
  get opcionesTipoAlimento(): string[] {
    const s = new Set<string>();
    for (const x of this.seguimientos ?? []) {
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

  /** Filas visibles según filtros; con consumo acumulado recalculado en el subconjunto. */
  get diarioFilasFiltradas(): RegistroDiarioTablaFilaEngorde[] {
    const base = this.diarioFilas ?? [];
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
    return (this.diarioFilas?.length ?? 0) > 0 && this.diarioFilasFiltradas.length === 0 && this.hayFiltrosDiarioActivos;
  }

  limpiarFiltrosDiario(): void {
    this.filtroFechaDesde = '';
    this.filtroFechaHasta = '';
    this.filtroSemana = null;
    this.filtroTipoAlimento = '';
  }

  private pasaFiltrosDiario(f: RegistroDiarioTablaFilaEngorde): boolean {
    const ymd = this.toYMD(f.seg.fechaRegistro);
    const desde = (this.filtroFechaDesde || '').trim();
    const hasta = (this.filtroFechaHasta || '').trim();
    if (desde && ymd && ymd < desde) return false;
    if (hasta && ymd && ymd > hasta) return false;
    if (this.filtroSemana != null && f.semana !== this.filtroSemana) return false;
    const ft = (this.filtroTipoAlimento || '').trim();
    if (ft) {
      const full = (f.seg.tipoAlimento || '').trim().toLowerCase();
      if (full !== ft.toLowerCase()) return false;
    }
    return true;
  }

  private buildDiarioFilas(): RegistroDiarioTablaFilaEngorde[] {
    this.advertenciaSaldoSinIngresoPrevio = false;
    const list = [...(this.seguimientos || [])];
    if (list.length === 0) return [];
    list.sort((a, b) => {
      const ya = this.toYMD(a.fechaRegistro) ?? '';
      const yb = this.toYMD(b.fechaRegistro) ?? '';
      if (ya !== yb) return ya.localeCompare(yb);
      return (a.id ?? 0) - (b.id ?? 0);
    });

    const histPorFecha = this.enriquecerTablaConHistoricoInventario ? this.aggregateHistoricoPorFecha() : null;
    const saldoCalc = this.enriquecerTablaConHistoricoInventario
      ? this.computeSaldoAlimentoKgPorSeguimiento(list)
      : null;
    const saldoPorSegId = saldoCalc?.porSegId ?? null;
    this.advertenciaSaldoSinIngresoPrevio = !!saldoCalc?.sinIngresoPrevioAlPrimerConsumo;

    const inicial = this.avesInicialesLote();
    let acumTodasPerdidas = 0;
    let acumCons = 0;
    const out: RegistroDiarioTablaFilaEngorde[] = [];

    for (const seg of list) {
      const mh = seg.mortalidadHembras ?? 0;
      const mm = seg.mortalidadMachos ?? 0;
      const selh = seg.selH ?? 0;
      const selm = seg.selM ?? 0;
      const erh = seg.errorSexajeHembras ?? 0;
      const erm = seg.errorSexajeMachos ?? 0;
      const totalMortSelDia = mh + mm + selh + selm;
      const perdidasTodasDia = totalMortSelDia + erh + erm;
      acumTodasPerdidas += perdidasTodasDia;

      const ch = Number(seg.consumoKgHembras ?? 0);
      const cm = Number(seg.consumoKgMachos ?? 0);
      const consDia = ch + cm;
      acumCons += consDia;

      const saldo = Math.max(0, inicial - acumTodasPerdidas);
      const saldoInicioDia = saldo + perdidasTodasDia;
      const pctPerdidasDia =
        saldoInicioDia > 0
          ? (100 * totalMortSelDia) / saldoInicioDia
          : totalMortSelDia > 0
            ? 100
            : null;

      const edadDia = this.calcularEdadDias(seg.fechaRegistro);
      const semana = Math.max(1, Math.min(8, Math.ceil((edadDia + 1) / 7)));

      const ymd = this.toYMD(seg.fechaRegistro);
      const agg = ymd && histPorFecha ? histPorFecha.get(ymd) : undefined;

      const metaIng = this.metaStr(seg, 'ingresoAlimento', 'ingreso_alimento', 'ingresoAlimentoKg');
      const metaTras = this.metaStr(seg, 'traslado', 'notaTraslado', 'trasladoAlimento', 'textoTraslado', 'trasladoTexto');
      const metaDoc = this.metaStr(seg, 'documento', 'documentoAlimento', 'nroDocumento', 'numeroDocumento');
      const metaDh = this.metaNum(seg, 'despachoHembras', 'despachoH', 'despacho_hembra');
      const metaDm = this.metaNum(seg, 'despachoMachos', 'despachoM', 'despacho_macho');

      let ingresoAlimento = metaIng;
      let traslado = metaTras;
      let documento = metaDoc;
      let despachoH = metaDh;
      let despachoM = metaDm;
      let despachoX: number | null = null;
      let consumoBodegaKg: number | null = null;

      if (agg) {
        const ingresoKgMostrado = this.resolveIngresoKgMostrado(agg, seg);
        if (ingresoKgMostrado > 0) ingresoAlimento = `${this.formatKgNumber(ingresoKgMostrado)} kg`;
        const partesTr: string[] = [];
        if (agg.trasladoEntradaKg > 0) partesTr.push(`Entrada ${this.formatKgNumber(agg.trasladoEntradaKg)} kg`);
        if (agg.trasladoSalidaKg > 0) partesTr.push(`Salida ${this.formatKgNumber(agg.trasladoSalidaKg)} kg`);
        if (partesTr.length) traslado = partesTr.join(' · ');
        if (agg.refsDocumento.length) documento = [...new Set(agg.refsDocumento)].join(', ');
        if (agg.ventaH > 0) despachoH = agg.ventaH;
        if (agg.ventaM > 0) despachoM = agg.ventaM;
        despachoX = agg.ventaX > 0 ? agg.ventaX : null;
        consumoBodegaKg = agg.consumoBodegaKg > 0 ? agg.consumoBodegaKg : null;
      }

      out.push({
        seg,
        edadDia,
        semana,
        diaCorto: this.formatDiaSemanaCorto(seg.fechaRegistro),
        totalMortSelDia,
        saldoAves: saldo,
        consumoDiaKg: consDia,
        acumConsumoKg: acumCons,
        ingresoAlimento,
        traslado,
        documento,
        despachoH,
        despachoM,
        despachoX,
        consumoBodegaKg,
        tipoAlimentoCorto: this.tipoAlimentoCorto(seg.tipoAlimento),
        pctPerdidasDia,
        saldoAlimentoKgMostrado: this.resolveSaldoAlimentoMostrado(seg, saldoPorSegId)
      });
    }
    return out;
  }

  /**
   * Fecha calendario (YYYY-MM-DD) para agrupar y ordenar movimientos del histórico.
   * Si `referencia` trae la fecha real del movimiento (p. ej. "Seguimiento aves engorde #336 2026-03-05"),
   * se usa en lugar de `fechaOperacion` cuando el backend consolidó muchas líneas en un solo día.
   */
  private ymdHistoricoEfectivo(h: LoteRegistroHistoricoUnificadoDto): string | null {
    const ref = `${h.referencia ?? ''} ${h.numeroDocumento ?? ''}`.trim();
    const mSeg = ref.match(/Seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})/i);
    if (mSeg) return mSeg[1];
    if (h.tipoEvento === 'INV_CONSUMO') {
      const mAny = ref.match(/(\d{4}-\d{2}-\d{2})/);
      if (mAny) return mAny[1];
    }
    return this.toYMD(h.fechaOperacion);
  }

  /** Orden estable dentro del mismo día (createdAt del movimiento o id). */
  private tsHistorico(h: LoteRegistroHistoricoUnificadoDto): number {
    const t = Date.parse(h.createdAt);
    return Number.isFinite(t) ? t : h.id;
  }

  private tsSeguimiento(seg: SeguimientoLoteLevanteDto): number {
    const t = Date.parse(String(seg.fechaRegistro ?? ''));
    return Number.isFinite(t) ? t : (seg.id ?? 0) * 1e9;
  }

  // Solo movimientos físicos de alimento afectan el saldo: INV_INGRESO, INV_TRASLADO_ENTRADA,
  // INV_TRASLADO_SALIDA. INV_OTRO (AjusteStock / EliminacionStock) son correcciones administrativas
  // del registro de stock en el módulo de inventario-gestión y no representan alimento físico
  // que entre o salga del galpón; incluirlos inflaría el saldo de forma incorrecta.
  private deltaHistoricoMovimientoStock(
    h: LoteRegistroHistoricoUnificadoDto
  ): { delta: number; ord: number } | null {
    if (h.anulado) return null;
    const kg = Number(h.cantidadKg ?? 0);
    switch (h.tipoEvento) {
      case 'INV_INGRESO':
        if (kg === 0) return null;
        return { delta: kg, ord: 0 };
      case 'INV_TRASLADO_ENTRADA':
        if (kg === 0) return null;
        return { delta: kg, ord: 1 };
      case 'INV_TRASLADO_SALIDA':
        if (kg === 0) return null;
        return { delta: -Math.abs(kg), ord: 2 };
      default:
        return null;
    }
  }

  /**
   * Stock de alimento (kg) después de cada seguimiento, en orden cronológico.
   * - Saldo inicial (antes del primer día de seguimiento): ingresos y traslados del histórico con fecha
   *   anterior al primer registro de seguimiento (stock ya en galpón de días/meses previos; sin filtro encaset).
   * - A partir de ese día: movimientos de histórico desde encasetamiento + consumos del seguimiento.
   * - Tras cada evento se aplica piso en 0: no hay saldo negativo; el siguiente ingreso suma sobre lo disponible.
   * - No incluye INV_CONSUMO del histórico (duplicaría el consumo del seguimiento).
   */
  private computeSaldoAlimentoKgPorSeguimiento(sortedSeg: SeguimientoLoteLevanteDto[]): {
    porSegId: Map<number, number>;
    sinIngresoPrevioAlPrimerConsumo: boolean;
  } | null {
    const hist = this.historicoUnificado ?? [];
    if (hist.length === 0) return null;

    const firstSegYmd = this.toYMD(sortedSeg[0]?.fechaRegistro);
    if (!firstSegYmd) return null;

    /** Solo movimientos de histórico desde encaset (no duplicar líneas ya en apertura). */
    const encYmd = this.toYMD(this.selectedLote?.fechaEncaset);

    const openingKg = this.computeSaldoAperturaGalponAntesPrimerSeguimiento(hist, firstSegYmd, encYmd);

    type Ev = { ymd: string; ord: number; tie: number; segId: number | null; delta: number };
    const ev: Ev[] = [];

    for (const h of hist) {
      const ymd = this.ymdHistoricoEfectivo(h);
      if (!ymd) continue;
      if (ymd < firstSegYmd) continue;
      if (encYmd && ymd < encYmd) continue;
      const d = this.deltaHistoricoMovimientoStock(h);
      if (!d) continue;
      ev.push({ ymd, ord: d.ord, tie: this.tsHistorico(h), segId: null, delta: d.delta });
    }

    for (const seg of sortedSeg) {
      const ymd = this.toYMD(seg.fechaRegistro);
      if (!ymd) continue;
      const ch = Number(seg.consumoKgHembras ?? 0);
      const cm = Number(seg.consumoKgMachos ?? 0);
      const cons = ch + cm;
      if (seg.id == null) continue;
      ev.push({ ymd, ord: 3, tie: this.tsSeguimiento(seg), segId: seg.id, delta: -cons });
    }

    ev.sort((a, b) => {
      if (a.ymd !== b.ymd) return a.ymd.localeCompare(b.ymd);
      if (a.ord !== b.ord) return a.ord - b.ord;
      if (a.tie !== b.tie) return a.tie - b.tie;
      return (a.segId ?? 0) - (b.segId ?? 0);
    });

    const map = new Map<number, number>();
    let bal = openingKg;
    let saldoAntesPrimerConsumoSeg: number | null = null;
    for (const e of ev) {
      if (e.segId != null && saldoAntesPrimerConsumoSeg === null) {
        saldoAntesPrimerConsumoSeg = bal;
      }
      bal += e.delta;
      bal = Math.max(0, bal);
      if (e.segId != null) {
        map.set(e.segId, bal);
      }
    }
    const sinIngresoPrevioAlPrimerConsumo =
      saldoAntesPrimerConsumoSeg !== null && saldoAntesPrimerConsumoSeg <= 0;

    return { porSegId: map, sinIngresoPrevioAlPrimerConsumo };
  }

  /**
   * Stock disponible (kg) en galpón antes del primer día de registro de seguimiento: suma ingresos,
   * traslados de entrada y ajustes; resta traslados de salida y eliminaciones; en orden cronológico.
   * Tras cada movimiento se aplica piso en 0 (misma regla que la secuencia principal).
   */
  private computeSaldoAperturaGalponAntesPrimerSeguimiento(
    hist: LoteRegistroHistoricoUnificadoDto[],
    firstSegYmd: string,
    encYmd: string | null
  ): number {
    type Row = { ymd: string; ts: number; delta: number };
    const rows: Row[] = [];
    for (const h of hist) {
      const ymd = this.ymdHistoricoEfectivo(h);
      if (!ymd || ymd >= firstSegYmd) continue;
      // Importante: el saldo de apertura debe considerar solo movimientos del ciclo del lote,
      // no stock viejo previo al encasetamiento que podría pertenecer a otro ciclo/galpón.
      if (encYmd && ymd < encYmd) continue;
      const d = this.deltaHistoricoMovimientoStock(h);
      if (!d) continue;
      rows.push({ ymd, ts: this.tsHistorico(h), delta: d.delta });
    }
    rows.sort((a, b) => (a.ymd !== b.ymd ? a.ymd.localeCompare(b.ymd) : a.ts - b.ts));
    let bal = 0;
    for (const r of rows) {
      bal += r.delta;
      bal = Math.max(0, bal);
    }
    return bal;
  }

  private resolveSaldoAlimentoMostrado(
    seg: SeguimientoLoteLevanteDto,
    saldoPorSegId: Map<number, number> | null
  ): number | null {
    if (seg.id != null && saldoPorSegId?.has(seg.id)) {
      return saldoPorSegId.get(seg.id)!;
    }
    const raw = (seg as unknown as { saldoAlimentoKg?: unknown }).saldoAlimentoKg;
    if (raw != null && raw !== '') {
      const n = Number(raw);
      if (!Number.isNaN(n)) return Math.max(0, n);
    }
    return null;
  }

  private aggregateHistoricoPorFecha(): Map<string, AggregadoHistoricoDia> {
    const map = new Map<string, AggregadoHistoricoDia>();
    const ensure = (ymd: string): AggregadoHistoricoDia => {
      let a = map.get(ymd);
      if (!a) {
        a = {
          ingresoKg: 0,
          ingresoKgPorItem: new Map<string, number>(),
          trasladoEntradaKg: 0,
          trasladoSalidaKg: 0,
          consumoBodegaKg: 0,
          refsDocumento: [],
          ventaH: 0,
          ventaM: 0,
          ventaX: 0
        };
        map.set(ymd, a);
      }
      return a;
    };

    const pushRef = (a: AggregadoHistoricoDia, h: LoteRegistroHistoricoUnificadoDto) => {
      const r = (h.numeroDocumento?.trim() || h.referencia?.trim() || '').trim();
      if (r) a.refsDocumento.push(r);
    };

    for (const h of this.historicoUnificado ?? []) {
      const ymd =
        h.tipoEvento === 'VENTA_AVES' ? this.toYMD(h.fechaOperacion) : this.ymdHistoricoEfectivo(h);
      if (!ymd) continue;
      const a = ensure(ymd);
      const kg = Number(h.cantidadKg ?? 0);

      switch (h.tipoEvento) {
        case 'INV_INGRESO':
          a.ingresoKg += kg;
          {
            const key = (h.itemResumen ?? '').trim() || '(sin ítem)';
            a.ingresoKgPorItem.set(key, (a.ingresoKgPorItem.get(key) ?? 0) + kg);
          }
          pushRef(a, h);
          break;
        case 'INV_TRASLADO_ENTRADA':
          a.trasladoEntradaKg += kg;
          break;
        case 'INV_TRASLADO_SALIDA':
          a.trasladoSalidaKg += kg;
          break;
        case 'INV_CONSUMO':
          a.consumoBodegaKg += kg;
          break;
        case 'VENTA_AVES':
          a.ventaH += h.cantidadHembras ?? 0;
          a.ventaM += h.cantidadMachos ?? 0;
          a.ventaX += h.cantidadMixtas ?? 0;
          pushRef(a, h);
          break;
        default:
          break;
      }
    }
    return map;
  }

  private resolveIngresoKgMostrado(agg: AggregadoHistoricoDia, seg: SeguimientoLoteLevanteDto): number {
    const tipo = String((seg as unknown as { tipoAlimento?: unknown }).tipoAlimento ?? '').trim();
    if (!tipo) return agg.ingresoKg;

    const tipoNorm = tipo.toLowerCase();
    let best: { key: string; kg: number } | null = null;
    for (const [k, v] of agg.ingresoKgPorItem.entries()) {
      const kn = k.toLowerCase();
      if (!kn || kn === '(sin ítem)') continue;
      if (kn.includes(tipoNorm) || tipoNorm.includes(kn)) {
        if (!best || v > best.kg) best = { key: k, kg: v };
      }
    }
    return best ? best.kg : agg.ingresoKg;
  }

  private formatKgNumber(n: number): string {
    return Number(n.toFixed(3)).toString();
  }

  private avesInicialesLote(): number {
    const l = this.selectedLote as Record<string, unknown> | null;
    if (!l) return 0;
    const h = Number(l['hembrasL'] ?? 0);
    const m = Number(l['machosL'] ?? 0);
    if (h + m > 0) return Math.round(h + m);
    const av = l['avesEncasetadas'];
    if (av != null && av !== '') return Math.round(Number(av));
    return 0;
  }

  private metaStr(seg: SeguimientoLoteLevanteDto, ...keys: string[]): string {
    const raw = (seg as any).metadata;
    if (!raw || typeof raw !== 'object') return '';
    const m = raw as Record<string, unknown>;
    for (const k of keys) {
      const v = m[k];
      if (v != null && String(v).trim() !== '') return String(v).trim();
    }
    return '';
  }

  private metaNum(seg: SeguimientoLoteLevanteDto, ...keys: string[]): number | null {
    const raw = (seg as any).metadata;
    if (!raw || typeof raw !== 'object') return null;
    const m = raw as Record<string, unknown>;
    for (const k of keys) {
      const v = m[k];
      if (v == null || v === '') continue;
      const n = Number(v);
      if (!Number.isNaN(n)) return n;
    }
    return null;
  }

  private tipoAlimentoCorto(tipo: string | null | undefined): string {
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

  onTabChange(tab: 'general' | 'indicadores' | 'grafica'): void {
    this.activeTab = tab;
  }
  onCreate(): void { this.create.emit(); }
  onEdit(seg: SeguimientoLoteLevanteDto): void { this.edit.emit(seg); }
  onDelete(id: number): void { this.delete.emit(id); }
  onViewDetail(seg: SeguimientoLoteLevanteDto): void { this.viewDetail.emit(seg); }

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
      ...(this.enriquecerTablaConHistoricoInventario ? ['Despacho mixtas', 'Consumo bodega (kg)', 'Saldo alimento (kg)'] : []),
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
    const rows = this.diarioFilasFiltradas.map(f => {
      const s = f.seg;
      return [
        this.formatDMY(s.fechaRegistro),
        f.semana,
        f.edadDia,
        f.diaCorto,
        s.mortalidadHembras ?? '',
        s.mortalidadMachos ?? '',
        s.selH ?? '',
        s.selM ?? '',
        f.totalMortSelDia,
        f.despachoH ?? '',
        f.despachoM ?? '',
        ...(this.enriquecerTablaConHistoricoInventario
          ? [
              f.despachoX ?? '',
              f.consumoBodegaKg != null ? f.consumoBodegaKg : '',
              f.saldoAlimentoKgMostrado != null ? f.saldoAlimentoKgMostrado : ''
            ]
          : []),
        f.saldoAves,
        f.tipoAlimentoCorto,
        f.ingresoAlimento || '',
        f.traslado || '',
        f.documento || '',
        (s as any).consumoKgHembras ?? '',
        (s as any).consumoKgMachos ?? 0,
        f.consumoDiaKg,
        f.acumConsumoKg,
        (s as any).consumoAguaDiario != null ? (s as any).consumoAguaDiario : '',
        f.pctPerdidasDia != null ? Math.round(f.pctPerdidasDia * 100) / 100 : '',
        (s as any).pesoPromH != null ? (s as any).pesoPromH : '',
        (s as any).pesoPromM != null ? (s as any).pesoPromM : '',
        ((s as any).observaciones || '').trim()
      ];
    });
    const titleBase = this.exportSeguimientoLoteNombre.trim()
      ? `Seguimiento diario pollo engorde — Lote: ${this.exportSeguimientoLoteNombre.trim()}`
      : 'Seguimiento diario pollo engorde';
    const title = this.hayFiltrosDiarioActivos ? `${titleBase} (filtros aplicados)` : titleBase;
    const adv = this.enriquecerTablaConHistoricoInventario && this.advertenciaSaldoSinIngresoPrevio
      ? [
          'Advertencia: sin ingreso/traslado de entrada desde encasetamiento antes del primer consumo en seguimiento (no hay saldo previo del que consumir hasta registrar entradas).'
        ]
      : [];
    const aoa: (string | number)[][] = [[title], ...adv.map(a => [a]), [], headers, ...rows];
    const ws = XLSX.utils.aoa_to_sheet(aoa);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Seguimiento');
    const safe = (this.exportSeguimientoLoteNombre.trim() || 'seguimiento_engorde').replace(/[\\/:*?"<>|]/g, '_');
    const d = new Date();
    const stamp = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
    XLSX.writeFile(wb, `Seguimiento_engorde_${safe}_${stamp}.xlsx`);
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
    const diff = Math.floor((reg.getTime() - enc.getTime()) / MS_DAY);
    return Math.max(0, diff);
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

  /** Tooltip de la celda de saldo alimento: fórmula de negocio y advertencia si aplica. */
  titleSaldoAlimentoCelda(_f: RegistroDiarioTablaFilaEngorde): string {
    const parts = [this.textoFormulaSaldoAlimento];
    if (this.advertenciaSaldoSinIngresoPrevio) {
      parts.push('Advertencia: sin saldo previo al primer consumo (desde encasetamiento). Revise ingresos en el histórico.');
    }
    return parts.join(' ');
  }

  formatDMY(input: string | Date | null | undefined): string {
    const ymd = this.toYMD(input);
    if (!ymd) return '';
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  }
}


import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import * as XLSX from 'xlsx';
import { exportarTablaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';
import { TablaListaIndicadoresComponent } from '../tabla-lista-indicadores/tabla-lista-indicadores.component';
import { GraficasPrincipalComponent } from '../graficas-principal/graficas-principal.component';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { LoteRegistroHistoricoUnificadoDto } from '../../../aves-engorde/services/seguimiento-aves-engorde.service';

/** Totales del historial unificado por una fecha (YYYY-MM-DD), alineados con el backend. */
interface AggregadoHistoricoDia {
  ingresoKg: number;
  trasladoEntradaKg: number;
  trasladoSalidaKg: number;
  consumoBodegaKg: number;
  refsDocumento: string[];
  ventaH: number;
  ventaM: number;
  ventaX: number;
}

/** Fila enriquecida para la tabla de registros diarios (libro de seguimiento / pestaña Seguimiento). */
export interface RegistroDiarioTablaFila {
  seg: SeguimientoLoteLevanteDto;
  /** Día de vida 1…n (encaset = día 1). */
  edadDia: number;
  semana: number;
  diaCorto: string;
  /** Solo mortalidad + selección (como TOTAL MORT+ SEL / DÍA). */
  totalMortSelDia: number;
  saldoAves: number;
  consumoDiaKg: number;
  acumConsumoKg: number;
  ingresoAlimento: string;
  traslado: string;
  documento: string;
  despachoH: number | null;
  despachoM: number | null;
  /** Ventas mixtas (historial unificado); solo pollo engorde. */
  despachoX: number | null;
  /** INV_CONSUMO sumado del inventario (kg); solo pollo engorde. */
  consumoBodegaKg: number | null;
  tipoAlimentoCorto: string;
  /** % pérdidas del día sobre aves vivas al inicio del día. */
  pctPerdidasDia: number | null;
}

interface ReporteSemanaFila {
  semana: number;
  dias: number;
  mortH: number;
  mortM: number;
  mortTotal: number;
  selH: number;
  selM: number;
  selTotal: number;
  errH: number;
  errM: number;
  errTotal: number;
  bajasTotal: number;
  consumoHkg: number;
  consumoMkg: number;
  consumoTotalkg: number;
  pctConsumoH: number | null;
  pctConsumoM: number | null;
  saldoInicio: number | null;
  saldoFin: number | null;
  pctBajasSobreInicio: number | null;
}

@Component({
  selector: 'app-tabs-principal',
  standalone: true,
  imports: [CommonModule, TablaListaIndicadoresComponent, GraficasPrincipalComponent],
  templateUrl: './tabs-principal.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabs-principal.component.scss']
})
export class TabsPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  /** LoteDto (aves-engorde) o LotePosturaLevanteDto (seguimiento levante). */
  @Input() selectedLote: LoteDto | LotePosturaLevanteDto | null = null;
  /** Resumen de descuentos (mortalidad, descarte, error sexaje) sobre el lote en Levante. */
  @Input() resumenLevante: LoteMortalidadResumenDto | null = null;
  @Input() loading: boolean = false;
  /** Si true, deshabilita botones Crear / Editar / Eliminar (ej. lote sin aves o cerrado). */
  @Input() disableCreateEditDelete: boolean = false;
  /** Si true, muestra aviso "Lote cerrado" en la información del lote. */
  @Input() isLoteCerrado: boolean = false;
  /** Solo módulo seguimiento pollo engorde: botón para exportar la tabla de registros diarios a Excel. */
  @Input() showExportSeguimientoExcel: boolean = false;
  /** Nombre del lote (nombre de archivo y fila de contexto en el Excel). */
  @Input() exportSeguimientoLoteNombre: string = '';
  /** Filas de lote_registro_historico_unificado (misma respuesta que por-lote). Solo pollo engorde. */
  @Input() historicoUnificado: LoteRegistroHistoricoUnificadoDto[] = [];
  /**
   * Si true, agrupa el historial por fecha de operación y rellena Ingreso, Traslado, Documento,
   * Despacho H/M/X y consumo bodega en la tabla principal (sin segunda tabla).
   */
  @Input() enriquecerTablaConHistoricoInventario = false;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoLoteLevanteDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoLoteLevanteDto>();

  activeTab: 'general' | 'indicadores' | 'reporteSemana' | 'grafica' = 'general';

  // Verificar si el usuario es admin
  isAdmin: boolean = false;

  /** Registros ordenados por fecha (asc) con acumulados y campos de metadata (traslado, ingreso, etc.). */
  diarioFilas: RegistroDiarioTablaFila[] = [];

  constructor(
    private storageService: TokenStorageService
  ) { }

  ngOnInit(): void {
    this.checkAdminRole();
  }

  // Verificar si el usuario tiene rol de Admin
  private checkAdminRole(): void {
    const session = this.storageService.get();
    if (session?.user?.roles) {
      this.isAdmin = session.user.roles.includes('Admin');
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote'] || changes['historicoUnificado'] || changes['enriquecerTablaConHistoricoInventario']) {
      this.diarioFilas = this.buildDiarioFilas();
    }
  }

  /** Columnas de la tabla de registros diarios. Feature 13: ahora son 4 columnas
   *  por género (↘ Ing.H, ↘ Ing.M, ↗ Sal.H, ↗ Sal.M) en lugar de 2 totales,
   *  por lo que sumamos +2 columnas al cómputo base. */
  get colspanRegistroDiario(): number {
    // 24 columnas base (se quitó "Día (calendario)"; la fecha ya lo cubre — REQ-007e).
    return 24 + (this.enriquecerTablaConHistoricoInventario ? 3 : 0);
  }

  trackByDiarioFila = (_: number, f: RegistroDiarioTablaFila) => f.seg.id;

  /** Registros de seguimiento diario cargados para el lote (debe coincidir con filas de la tabla). */
  get cantidadRegistrosSeguimiento(): number {
    return (this.seguimientos ?? []).length;
  }

  get fechaEncasetLote(): string | null {
    const l = this.selectedLote as LoteDto | LotePosturaLevanteDto | null;
    const raw = l && 'fechaEncaset' in l ? (l as { fechaEncaset?: string | null }).fechaEncaset : null;
    return raw && String(raw).trim() ? String(raw) : null;
  }

  /** Mortalidad acumulada (hembras + machos) según resumen API. */
  get totalMortalidadAcumulada(): number {
    const r = this.resumenLevante;
    if (!r) return 0;
    return (r.mortalidadAcumHembras ?? 0) + (r.mortalidadAcumMachos ?? 0);
  }

  /** Selección / descarte acumulado (hembras + machos). */
  get totalSeleccionAcumulada(): number {
    const r = this.resumenLevante;
    if (!r) return 0;
    return (r.selAcumHembras ?? 0) + (r.selAcumMachos ?? 0);
  }

  /** Error de sexaje acumulado (hembras + machos). */
  get totalErrorSexajeAcumulado(): number {
    const r = this.resumenLevante;
    if (!r) return 0;
    return (r.errorSexajeAcumHembras ?? 0) + (r.errorSexajeAcumMachos ?? 0);
  }

  private buildDiarioFilas(): RegistroDiarioTablaFila[] {
    const list = [...(this.seguimientos || [])];
    if (list.length === 0) return [];
    list.sort((a, b) => {
      const ya = this.toYMD(a.fechaRegistro) ?? '';
      const yb = this.toYMD(b.fechaRegistro) ?? '';
      if (ya !== yb) return ya.localeCompare(yb);
      return (a.id ?? 0) - (b.id ?? 0);
    });

    const histPorFecha = this.enriquecerTablaConHistoricoInventario ? this.aggregateHistoricoPorFecha() : null;

    const inicial = this.avesInicialesLote();
    /** Acumulado de todas las bajas (mort + sel + err. sexaje) para saldo de aves. */
    let acumTodasPerdidas = 0;
    /** Acumulado de traslados ingresos (+) y salidas (-) — Feature 13. */
    let acumTrasIn = 0;
    let acumTrasOut = 0;
    let acumCons = 0;
    const out: RegistroDiarioTablaFila[] = [];

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

      // 🔀 Feature 13 — acumular traslados por fila (en orden cronológico)
      const tIn  = (seg.trasladoIngresoHembras ?? 0) + (seg.trasladoIngresoMachos ?? 0);
      const tOut = (seg.trasladoSalidaHembras  ?? 0) + (seg.trasladoSalidaMachos  ?? 0);
      acumTrasIn  += tIn;
      acumTrasOut += tOut;

      const ch = Number(seg.consumoKgHembras ?? 0);
      const cm = Number(seg.consumoKgMachos ?? 0);
      const consDia = ch + cm;
      acumCons += consDia;

      // saldo = inicial − bajas + ingresos_traslado − salidas_traslado
      const saldo = Math.max(0, inicial - acumTodasPerdidas + acumTrasIn - acumTrasOut);
      const saldoInicioDia = saldo + perdidasTodasDia;
      const pctPerdidasDia =
        saldoInicioDia > 0
          ? (100 * totalMortSelDia) / saldoInicioDia
          : totalMortSelDia > 0
            ? 100
            : null;

      const edad0 = this.calcularEdadDias(seg.fechaRegistro);
      /** Días de vida: el día del encasetamiento es 1. */
      const edadDia = Math.max(1, edad0 + 1);
      /** Semana de cría: semana 1 = días 1..7, semana 2 = 8..14, etc. (sin tope). */
      const semana = Math.max(1, Math.ceil(edadDia / 7));

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
        if (agg.ingresoKg > 0) {
          ingresoAlimento = `${this.formatKgNumber(agg.ingresoKg)} kg`;
        }
        const partesTr: string[] = [];
        if (agg.trasladoEntradaKg > 0) partesTr.push(`Entrada ${this.formatKgNumber(agg.trasladoEntradaKg)} kg`);
        if (agg.trasladoSalidaKg > 0) partesTr.push(`Salida ${this.formatKgNumber(agg.trasladoSalidaKg)} kg`);
        if (partesTr.length) {
          traslado = partesTr.join(' · ');
        }
        if (agg.refsDocumento.length) {
          documento = [...new Set(agg.refsDocumento)].join(', ');
        }
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
        pctPerdidasDia
      });
    }
    return out;
  }

  /** Agrupa historial unificado por fecha de operación (misma lógica que el backfill de metadata). */
  private aggregateHistoricoPorFecha(): Map<string, AggregadoHistoricoDia> {
    const map = new Map<string, AggregadoHistoricoDia>();
    const ensure = (ymd: string): AggregadoHistoricoDia => {
      let a = map.get(ymd);
      if (!a) {
        a = {
          ingresoKg: 0,
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
      const ymd = this.toYMD(h.fechaOperacion);
      if (!ymd) continue;
      const a = ensure(ymd);
      const kg = Number(h.cantidadKg ?? 0);

      switch (h.tipoEvento) {
        case 'INV_INGRESO':
          a.ingresoKg += kg;
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

  private formatKgNumber(n: number): string {
    return Number(n.toFixed(3)).toString();
  }

  /** Aves al inicio del ciclo (hembras + machos del lote, o aves encasetadas). */
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
    const raw = seg.metadata;
    if (!raw || typeof raw !== 'object') return '';
    const m = raw as Record<string, unknown>;
    for (const k of keys) {
      const v = m[k];
      if (v != null && String(v).trim() !== '') return String(v).trim();
    }
    return '';
  }

  private metaNum(seg: SeguimientoLoteLevanteDto, ...keys: string[]): number | null {
    const raw = seg.metadata;
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

  /** Ej. "vie 16 ene" (es-EC). */
  formatDiaSemanaCorto(iso: string | Date | null | undefined): string {
    const ymd = this.toYMD(iso);
    if (!ymd) return '';
    const d = new Date(`${ymd}T12:00:00`);
    if (isNaN(d.getTime())) return '';
    try {
      return new Intl.DateTimeFormat('es-EC', { weekday: 'short', day: 'numeric', month: 'short' }).format(d);
    } catch {
      return '';
    }
  }

  // ================== EVENTOS ==================
  onTabChange(tab: 'general' | 'indicadores' | 'grafica'): void {
    this.activeTab = tab;
  }

  onTabChangeExtended(tab: 'general' | 'indicadores' | 'reporteSemana' | 'grafica'): void {
    this.activeTab = tab;
  }

  get reporteSemanaFilas(): ReporteSemanaFila[] {
    const filas = this.diarioFilas ?? [];
    if (filas.length === 0) return [];

    const porSemana = new Map<number, RegistroDiarioTablaFila[]>();
    for (const f of filas) {
      const s = Number(f.semana);
      if (!porSemana.has(s)) porSemana.set(s, []);
      porSemana.get(s)!.push(f);
    }

    const out: ReporteSemanaFila[] = [];
    const semanas = [...porSemana.keys()].sort((a, b) => a - b);
    for (const semana of semanas) {
      const list = porSemana.get(semana) ?? [];
      if (list.length === 0) continue;
      // vienen ya ordenadas por fecha en buildDiarioFilas()
      const first = list[0];
      const last = list[list.length - 1];

      let mortH = 0, mortM = 0, selH = 0, selM = 0, errH = 0, errM = 0;
      let consumoHkg = 0, consumoMkg = 0;
      for (const f of list) {
        const s = f.seg;
        mortH += Number(s.mortalidadHembras ?? 0);
        mortM += Number(s.mortalidadMachos ?? 0);
        selH += Number(s.selH ?? 0);
        selM += Number(s.selM ?? 0);
        errH += Number(s.errorSexajeHembras ?? 0);
        errM += Number(s.errorSexajeMachos ?? 0);
        consumoHkg += Number(s.consumoKgHembras ?? 0);
        consumoMkg += Number(s.consumoKgMachos ?? 0);
      }

      const mortTotal = mortH + mortM;
      const selTotal = selH + selM;
      const errTotal = errH + errM;
      const bajasTotal = mortTotal + selTotal + errTotal;
      const consumoTotalkg = consumoHkg + consumoMkg;
      const pctConsumoH = consumoTotalkg > 0 ? (100 * consumoHkg) / consumoTotalkg : null;
      const pctConsumoM = consumoTotalkg > 0 ? (100 * consumoMkg) / consumoTotalkg : null;

      // Saldo inicio de semana: saldo fin + bajas acumuladas dentro de la semana (aprox. aves vivas al iniciar)
      const saldoFin = last.saldoAves;
      const saldoInicio = saldoFin + bajasTotal;
      const pctBajasSobreInicio = saldoInicio > 0 ? (100 * bajasTotal) / saldoInicio : null;

      out.push({
        semana,
        dias: list.length,
        mortH, mortM, mortTotal,
        selH, selM, selTotal,
        errH, errM, errTotal,
        bajasTotal,
        consumoHkg,
        consumoMkg,
        consumoTotalkg,
        pctConsumoH,
        pctConsumoM,
        saldoInicio: Number.isFinite(saldoInicio) ? saldoInicio : null,
        saldoFin: Number.isFinite(saldoFin) ? saldoFin : null,
        pctBajasSobreInicio
      });
    }
    return out;
  }

  onCreate(): void {
    this.create.emit();
  }

  onEdit(seg: SeguimientoLoteLevanteDto): void {
    this.edit.emit(seg);
  }

  onDelete(id: number): void {
    this.delete.emit(id);
  }

  onViewDetail(seg: SeguimientoLoteLevanteDto): void {
    this.viewDetail.emit(seg);
  }

  /** Exporta las mismas columnas que la tabla «Registros Diarios» + cabecera detallada del lote.
   *  Feature 13: incluye traslados por género, saldo de aves vivas por fila, y usuario que registró. */
  exportSeguimientoDiarioExcel(): void {
    if (!this.showExportSeguimientoExcel || !this.diarioFilas?.length) return;

    // ─── Cabecera detallada del lote ───────────────────────────────────
    const sel = this.selectedLote as any;
    const r = this.resumenLevante;
    const lpl = sel && 'lotePosturaLevanteId' in (sel ?? {}) ? sel as any : null;

    const granjaNombre = sel?.farm?.name ?? lpl?.farm?.name ?? '—';
    const nucleoNombre = sel?.nucleo?.nucleoNombre ?? lpl?.nucleo?.nucleoNombre ?? '—';
    const galponNombre = sel?.galpon?.galponNombre ?? lpl?.galpon?.galponNombre ?? '—';
    const fase = lpl ? 'Levante' : (sel?.fase ?? 'Levante');
    const raza = sel?.raza ?? lpl?.raza ?? '—';
    const fechaEncaset = sel?.fechaEncaset ?? lpl?.fechaEncaset ?? null;
    const hembrasIni = (r?.hembrasIniciales ?? sel?.hembrasL ?? 0) as number;
    const machosIni  = (r?.machosIniciales  ?? sel?.machosL  ?? 0) as number;
    const avesIni = hembrasIni + machosIni;
    const saldoH = r?.saldoHembras ?? 0;
    const saldoM = r?.saldoMachos  ?? 0;
    const totMortAcumH = r?.mortalidadAcumHembras ?? 0;
    const totMortAcumM = r?.mortalidadAcumMachos  ?? 0;
    const totSelAcumH  = r?.selAcumHembras ?? 0;
    const totSelAcumM  = r?.selAcumMachos  ?? 0;
    const trasInH  = (r as any)?.levanteTrasladoIngresoHembras ?? 0;
    const trasInM  = (r as any)?.levanteTrasladoIngresoMachos  ?? 0;
    const trasOutH = (r as any)?.levanteTrasladoSalidaHembras  ?? 0;
    const trasOutM = (r as any)?.levanteTrasladoSalidaMachos   ?? 0;

    const loteNombre = this.exportSeguimientoLoteNombre.trim();
    const fechaGen = new Date();
    const fechaGenStr = `${String(fechaGen.getDate()).padStart(2,'0')}/${String(fechaGen.getMonth()+1).padStart(2,'0')}/${fechaGen.getFullYear()} ${String(fechaGen.getHours()).padStart(2,'0')}:${String(fechaGen.getMinutes()).padStart(2,'0')}`;

    const cabecera: (string | number)[][] = [
      ['Seguimiento Diario de Levante'],
      [`Generado: ${fechaGenStr}`],
      [],
      ['INFORMACIÓN DEL LOTE'],
      ['Lote:', loteNombre || '—', '', 'Fase:', fase],
      ['Granja:', granjaNombre, '', 'Núcleo:', nucleoNombre, '', 'Galpón:', galponNombre],
      ['Raza:', raza, '', 'Fecha encasetamiento:', fechaEncaset ? this.formatDMY(fechaEncaset) : '—'],
      ['Hembras encasetadas:', hembrasIni, '', 'Machos encasetados:', machosIni, '', 'Total encasetadas:', avesIni],
      ['Aves vivas (H):', saldoH, '', 'Aves vivas (M):', saldoM, '', 'Total vivas:', saldoH + saldoM],
      ['Mortalidad acum. (H):', totMortAcumH, '', 'Mortalidad acum. (M):', totMortAcumM],
      ['Selección acum. (H):', totSelAcumH, '', 'Selección acum. (M):', totSelAcumM],
      ['Ingreso traslados (H):', trasInH, '', 'Ingreso traslados (M):', trasInM, '', 'Total ingresos:', trasInH + trasInM],
      ['Salida traslados (H):',  trasOutH, '', 'Salida traslados (M):',  trasOutM, '', 'Total salidas:',  trasOutH + trasOutM],
      [],
      ['REGISTROS DIARIOS'],
      []
    ];

    // ─── Encabezados de tabla ──────────────────────────────────────────
    const headers = [
      'Fecha',
      'Semana',
      'Edad (días vida)',
      'Mortalidad hembras',
      'Mortalidad machos',
      'Selección hembras',
      'Selección machos',
      'Error sexaje hembras',
      'Error sexaje machos',
      'TOTAL MORT+ SEL / DÍA',
      // 🔀 Feature 13 — traslados dedicados por género
      'Ingreso traslado hembras',
      'Ingreso traslado machos',
      'Salida traslado hembras',
      'Salida traslado machos',
      ...(this.enriquecerTablaConHistoricoInventario
        ? ['Despacho mixtas', 'Consumo bodega (kg)', 'Saldo alimento (kg)']
        : []),
      'Saldo aves vivas',
      'Tipo alimento',
      'Consumo kg hembras',
      'Consumo kg machos',
      'Consumo real día (kg)',
      'Consumo acumulado (kg)',
      '% Retiro (Mort+Sel)/aves',
      'Peso prom. hembras (kg)',
      'Peso prom. machos (kg)',
      'Observaciones',
      // Auditoría
      'Registrado por',
      'Fecha registro',
      'Última actualización',
      'Actualizado por'
    ];

    const rows = this.diarioFilas.map(f => {
      const s: any = f.seg;
      return [
        this.formatDMY(s.fechaRegistro),
        f.semana,
        f.edadDia,
        s.mortalidadHembras ?? 0,
        s.mortalidadMachos ?? 0,
        s.selH ?? 0,
        s.selM ?? 0,
        s.errorSexajeHembras ?? 0,
        s.errorSexajeMachos ?? 0,
        f.totalMortSelDia,
        s.trasladoIngresoHembras ?? 0,
        s.trasladoIngresoMachos  ?? 0,
        s.trasladoSalidaHembras  ?? 0,
        s.trasladoSalidaMachos   ?? 0,
        ...(this.enriquecerTablaConHistoricoInventario
          ? [
              f.despachoX ?? '',
              f.consumoBodegaKg != null ? f.consumoBodegaKg : '',
              f.seg.saldoAlimentoKg != null ? f.seg.saldoAlimentoKg : ''
            ]
          : []),
        f.saldoAves,
        f.tipoAlimentoCorto,
        s.consumoKgHembras ?? 0,
        s.consumoKgMachos ?? 0,
        f.consumoDiaKg,
        f.acumConsumoKg,
        f.pctPerdidasDia != null ? Math.round(f.pctPerdidasDia * 100) / 100 : '',
        s.pesoPromH != null ? s.pesoPromH : '',
        s.pesoPromM != null ? s.pesoPromM : '',
        (s.observaciones || '').trim() || '—',
        s.createdByUserId ?? '—',
        s.createdAt ? this.formatDMY(s.createdAt) : '—',
        s.updatedAt ? this.formatDMY(s.updatedAt) : '—',
        s.updatedByUserId ?? '—'
      ];
    });

    const aoa: (string | number)[][] = [...cabecera, headers, ...rows];
    const ws = XLSX.utils.aoa_to_sheet(aoa);

    // Anchos de columnas razonables para que el Excel se vea bien al abrir
    const colCount = headers.length;
    (ws as any)['!cols'] = Array.from({ length: colCount }, (_, i) => ({ wch: i === 0 ? 14 : i === headers.length - 4 ? 28 : 18 }));

    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Seguimiento');
    const safe = (loteNombre || 'lote').replace(/[\\/:*?"<>|]/g, '_');
    const d = new Date();
    const stamp = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
    XLSX.writeFile(wb, `Seguimiento_Diario_de_Levante_${safe}_${stamp}.xlsx`);
  }

  exportReporteSemanaExcel(): void {
    const filas = this.reporteSemanaFilas ?? [];
    if (!filas.length) return;

    const headers = [
      'Semana',
      'Días',
      'Mort. H',
      'Mort. M',
      'Mort. Total',
      'Sel. H',
      'Sel. M',
      'Sel. Total',
      'Err. H',
      'Err. M',
      'Err. Total',
      'Bajas total',
      'Consumo H (kg)',
      'Consumo M (kg)',
      'Consumo total (kg)',
      '% consumo H',
      '% consumo M',
      'Saldo inicio',
      'Saldo fin',
      '% bajas/ini'
    ];

    const rows = filas.map(r => ([
      r.semana,
      r.dias,
      r.mortH,
      r.mortM,
      r.mortTotal,
      r.selH,
      r.selM,
      r.selTotal,
      r.errH,
      r.errM,
      r.errTotal,
      r.bajasTotal,
      r.consumoHkg,
      r.consumoMkg,
      r.consumoTotalkg,
      r.pctConsumoH != null ? Math.round(r.pctConsumoH * 100) / 100 : '',
      r.pctConsumoM != null ? Math.round(r.pctConsumoM * 100) / 100 : '',
      r.saldoInicio != null ? r.saldoInicio : '',
      r.saldoFin != null ? r.saldoFin : '',
      r.pctBajasSobreInicio != null ? Math.round(r.pctBajasSobreInicio * 100) / 100 : ''
    ]));

    const loteNombre = this.exportSeguimientoLoteNombre.trim();
    const title = loteNombre
      ? `Reporte semana — Lote: ${loteNombre}`
      : 'Reporte semana';

    exportarTablaExcel(headers, rows, {
      filenameBase: `Reporte_Semana_${loteNombre || 'lote'}`,
      sheetName: 'ReporteSemana',
      title,
    });
  }

  // ================== CALCULO DE EDAD ==================
  /**
   * Edad del lote en la fecha del registro (días de calendario desde encasetamiento).
   * Retorna 0 si la fecha es igual al encasetamiento; para UI se muestra como día 1 (edad0 + 1).
   */
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

  formatDMY(input: string | Date | null | undefined): string {
    const ymd = this.toYMD(input);
    if (!ymd) return '';
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  }
}

import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import * as XLSX from 'xlsx';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-aves-engorde.service';
import { LoteDto, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';
import { TablaIndicadoresDiariosEngordeComponent } from '../tabla-indicadores-diarios-engorde/tabla-indicadores-diarios-engorde.component';
import { GraficasIndicadoresDiariosEngordeComponent } from '../graficas-indicadores-diarios-engorde/graficas-indicadores-diarios-engorde.component';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { LoteRegistroHistoricoUnificadoDto } from '../../services/seguimiento-aves-engorde.service';

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
}

@Component({
  selector: 'app-tabs-principal-engorde',
  standalone: true,
  imports: [CommonModule, TablaIndicadoresDiariosEngordeComponent, GraficasIndicadoresDiariosEngordeComponent],
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

  constructor(private storageService: TokenStorageService) {}

  ngOnInit(): void {
    const session = this.storageService.get();
    this.isAdmin = !!session?.user?.roles?.includes('Admin');
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote'] || changes['historicoUnificado'] || changes['enriquecerTablaConHistoricoInventario']) {
      this.diarioFilas = this.buildDiarioFilas();
    }
  }

  /** Columnas de la tabla de registros diarios (sin Acciones). */
  get colspanRegistroDiario(): number {
    // 29 columnas visibles (incluye despachoH/M, ingreso, traslado, documento, agua) + (histórico: 3)
    return 29 + (this.enriquecerTablaConHistoricoInventario ? 3 : 0);
  }

  trackByDiarioFila = (_: number, f: RegistroDiarioTablaFilaEngorde) => f.seg.id;

  private buildDiarioFilas(): RegistroDiarioTablaFilaEngorde[] {
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
        if (agg.ingresoKg > 0) ingresoAlimento = `${this.formatKgNumber(agg.ingresoKg)} kg`;
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
        pctPerdidasDia
      });
    }
    return out;
  }

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
    if (!this.showExportSeguimientoExcel || !this.diarioFilas?.length) return;
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
    const rows = this.diarioFilas.map(f => {
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
              (s as any).saldoAlimentoKg != null ? (s as any).saldoAlimentoKg : ''
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
    const title = this.exportSeguimientoLoteNombre.trim()
      ? `Seguimiento diario pollo engorde — Lote: ${this.exportSeguimientoLoteNombre.trim()}`
      : 'Seguimiento diario pollo engorde';
    const aoa: (string | number)[][] = [[title], [], headers, ...rows];
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

  formatDMY(input: string | Date | null | undefined): string {
    const ymd = this.toYMD(input);
    if (!ymd) return '';
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  }
}


import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';
import { TablaListaIndicadoresComponent } from '../tabla-lista-indicadores/tabla-lista-indicadores.component';
import { TablaIndicadoresDiariosComponent } from '../tabla-indicadores-diarios/tabla-indicadores-diarios.component';
import { GraficasPrincipalComponent } from '../graficas-principal/graficas-principal.component';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';

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
  tipoAlimentoCorto: string;
  /** % pérdidas del día sobre aves vivas al inicio del día. */
  pctPerdidasDia: number | null;
}

@Component({
  selector: 'app-tabs-principal',
  standalone: true,
  imports: [CommonModule, TablaListaIndicadoresComponent, TablaIndicadoresDiariosComponent, GraficasPrincipalComponent],
  templateUrl: './tabs-principal.component.html',
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

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoLoteLevanteDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoLoteLevanteDto>();

  activeTab: 'general' | 'indicadores' | 'grafica' = 'general';

  /** Subpestaña dentro de Indicadores: diario (guía Ecuador mixto) vs semanal. */
  indicadoresSubTab: 'diario' | 'semanal' = 'diario';

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
    if (changes['seguimientos'] || changes['selectedLote']) {
      this.diarioFilas = this.buildDiarioFilas();
    }
  }

  trackByDiarioFila = (_: number, f: RegistroDiarioTablaFila) => f.seg.id;

  private buildDiarioFilas(): RegistroDiarioTablaFila[] {
    const list = [...(this.seguimientos || [])];
    if (list.length === 0) return [];
    list.sort((a, b) => {
      const ya = this.toYMD(a.fechaRegistro) ?? '';
      const yb = this.toYMD(b.fechaRegistro) ?? '';
      if (ya !== yb) return ya.localeCompare(yb);
      return (a.id ?? 0) - (b.id ?? 0);
    });

    const inicial = this.avesInicialesLote();
    /** Acumulado de todas las bajas (mort + sel + err. sexaje) para saldo de aves. */
    let acumTodasPerdidas = 0;
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

      const edad0 = this.calcularEdadDias(seg.fechaRegistro);
      const edadDia = edad0 + 1;
      const semana = Math.max(1, Math.min(8, Math.ceil(edadDia / 7)));

      out.push({
        seg,
        edadDia,
        semana,
        diaCorto: this.formatDiaSemanaCorto(seg.fechaRegistro),
        totalMortSelDia,
        saldoAves: saldo,
        consumoDiaKg: consDia,
        acumConsumoKg: acumCons,
        ingresoAlimento: this.metaStr(seg, 'ingresoAlimento', 'ingreso_alimento', 'ingresoAlimentoKg'),
        traslado: this.metaStr(seg, 'traslado', 'notaTraslado', 'trasladoAlimento', 'textoTraslado', 'trasladoTexto'),
        documento: this.metaStr(seg, 'documento', 'documentoAlimento', 'nroDocumento', 'numeroDocumento'),
        despachoH: this.metaNum(seg, 'despachoHembras', 'despachoH', 'despacho_hembra'),
        despachoM: this.metaNum(seg, 'despachoMachos', 'despachoM', 'despacho_macho'),
        tipoAlimentoCorto: this.tipoAlimentoCorto(seg.tipoAlimento),
        pctPerdidasDia
      });
    }
    return out;
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

  setIndicadoresSubTab(sub: 'diario' | 'semanal'): void {
    this.indicadoresSubTab = sub;
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

  // ================== CALCULO DE EDAD ==================
  /**
   * Edad del lote en la fecha del registro (días de calendario desde encasetamiento).
   * El mismo día del encasetamiento = 0; no usa ceil ni zona UTC sobre la cadena ISO.
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

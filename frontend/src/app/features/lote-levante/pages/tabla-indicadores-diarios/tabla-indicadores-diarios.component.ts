import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import {
  GuiaGeneticaEcuadorDetalleDto,
  GuiaGeneticaEcuadorService
} from '../../../config/guia-genetica-ecuador/guia-genetica-ecuador.service';

export interface IndicadorDiarioFila {
  fechaYmd: string;
  dia: number;
  /** Aves vivas al inicio del día (misma base para consumo y % mort.+sel.). */
  avesInicioDia: number;
  pesoRealG: number;
  pesoTablaG: number;
  gananciaDiariaRealG: number | null;
  gananciaDiariaTablaG: number;
  /** g/ave/día: alimento total del registro ese día ÷ aves al inicio del día. */
  consumoDiarioRealG: number;
  consumoDiarioTablaG: number;
  alimentoAcumRealG: number;
  alimentoAcumTablaG: number;
  caReal: number | null;
  caTabla: number;
  mortSelRealPct: number;
  mortSelTablaPct: number;
  difPesoVsTablaPct: number;
}

export type ComparativoTag = 'ok' | 'warn' | 'bad' | 'na';

interface MetadataItem {
  unidad?: string;
  cantidad?: number;
  tipoItem?: string;
}

@Component({
  selector: 'app-tabla-indicadores-diarios',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-indicadores-diarios.component.html',
  styleUrls: ['./tabla-indicadores-diarios.component.scss']
})
export class TablaIndicadoresDiariosComponent implements OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  @Input() selectedLote: LoteDto | LotePosturaLevanteDto | null = null;
  @Input() loading = false;

  filas: IndicadorDiarioFila[] = [];
  cargandoGuia = false;
  errorGuia: string | null = null;
  /** Hay al menos una fila de guía Ecuador mixto cargada */
  guiaOk = false;
  /** Texto fijo para la cabecera: raza y año de la curva consultada */
  etiquetaGuiaCargada = '';

  constructor(private guiaEcuador: GuiaGeneticaEcuadorService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      void this.rebuild();
    }
  }

  private async rebuild(): Promise<void> {
    this.errorGuia = null;
    this.guiaOk = false;
    this.filas = [];

    if (!this.seguimientos?.length || !this.selectedLote) {
      return;
    }

    const raza = (this.selectedLote.raza ?? '').trim();
    const anio = this.selectedLote.anoTablaGenetica;
    this.etiquetaGuiaCargada = '';
    if (!raza || anio == null || anio <= 0) {
      this.errorGuia =
        'Configure raza y año de tabla genética en el lote de engorde para comparar con la guía Ecuador (mixto).';
      return;
    }
    this.etiquetaGuiaCargada = `Guía genética Ecuador · sexo mixto · ${raza} · año ${anio}`;

    this.cargandoGuia = true;
    let guiaPorDia = new Map<number, GuiaGeneticaEcuadorDetalleDto>();
    try {
      const detalle = await firstValueFrom(this.guiaEcuador.getDatos(raza, anio, 'mixto'));
      for (const d of detalle ?? []) {
        if (!guiaPorDia.has(d.dia)) {
          guiaPorDia.set(d.dia, d);
        }
      }
      this.guiaOk = guiaPorDia.size > 0;
      if (!this.guiaOk) {
        this.errorGuia =
          `No hay datos de guía Ecuador (sexo mixto) para ${raza} / ${anio}. Importe o cargue la tabla en Configuración.`;
      }
    } catch {
      this.errorGuia = 'No se pudo cargar la guía genética Ecuador. Intente de nuevo.';
      guiaPorDia = new Map();
    } finally {
      this.cargandoGuia = false;
    }

    const diasOrdenados = this.agruparRegistrosPorDia();
    const pesoInicial = this.pesoInicialMixtoLote();
    let aves = this.avesInicialesLote();
    let acumAlimentoPorAveG = 0;
    /** Último peso mixto medido (solo avanza si hay peso en registro). */
    let ultimoPesoMedido = pesoInicial;

    const out: IndicadorDiarioFila[] = [];

    for (const { ymd, regs } of diasOrdenados) {
      const fechaRef = regs[0]?.fechaRegistro ?? ymd;
      const dia = this.calcularDiaVida(fechaRef);
      const g = this.guiaParaDia(guiaPorDia, dia);

      const consumoKg = regs.reduce((s, r) => s + this.consumoAlimentoKgDesdeRegistro(r), 0);
      const mort = regs.reduce((s, r) => s + (r.mortalidadHembras ?? 0) + (r.mortalidadMachos ?? 0), 0);
      const sel = regs.reduce((s, r) => s + (r.selH ?? 0) + (r.selM ?? 0), 0);
      const ultimo = regs[regs.length - 1];
      const pesoReal = this.pesoMixtoReg(ultimo);

      const avesInicio = aves;
      const consumoPorAveG =
        avesInicio > 0 ? (consumoKg * 1000) / avesInicio : 0;
      acumAlimentoPorAveG += consumoPorAveG;

      const pesoTablaG = g?.pesoCorporalG ?? 0;
      const gananciaTablaG = g?.gananciaDiariaG ?? 0;
      const consumoTablaG = g?.cantidadAlimentoDiarioG ?? 0;
      const alimentoAcumTablaG = g?.alimentoAcumuladoG ?? 0;
      const caTabla = g?.ca ?? 0;
      const mortTablaPct = g?.mortalidadSeleccionDiaria ?? 0;

      let gananciaReal: number | null = null;
      if (pesoReal > 0) {
        gananciaReal = pesoReal - ultimoPesoMedido;
      }

      const pesoParaCa = pesoReal > 0 ? pesoReal : 0;
      const caReal =
        pesoParaCa > 0 && acumAlimentoPorAveG > 0 ? acumAlimentoPorAveG / pesoParaCa : null;

      /** % sobre aves al inicio del día (mortalidad + selección del propio día). */
      const mortSelRealPct = avesInicio > 0 ? ((mort + sel) / avesInicio) * 100 : 0;

      const difPesoVsTablaPct =
        pesoTablaG > 0 && pesoReal > 0 ? ((pesoReal - pesoTablaG) / pesoTablaG) * 100 : 0;

      out.push({
        fechaYmd: ymd,
        dia,
        avesInicioDia: avesInicio,
        pesoRealG: pesoReal,
        pesoTablaG,
        gananciaDiariaRealG: gananciaReal,
        gananciaDiariaTablaG: gananciaTablaG,
        consumoDiarioRealG: consumoPorAveG,
        consumoDiarioTablaG: consumoTablaG,
        alimentoAcumRealG: acumAlimentoPorAveG,
        alimentoAcumTablaG,
        caReal,
        caTabla,
        mortSelRealPct,
        mortSelTablaPct: mortTablaPct,
        difPesoVsTablaPct
      });

      aves = aves - mort - sel;
      if (pesoReal > 0) {
        ultimoPesoMedido = pesoReal;
      }
    }

    this.filas = out;
  }

  private agruparRegistrosPorDia(): { ymd: string; regs: SeguimientoLoteLevanteDto[] }[] {
    const sorted = [...this.seguimientos].sort((a, b) => {
      const ya = this.toYMD(a.fechaRegistro) ?? '';
      const yb = this.toYMD(b.fechaRegistro) ?? '';
      return ya.localeCompare(yb);
    });
    const out: { ymd: string; regs: SeguimientoLoteLevanteDto[] }[] = [];
    for (const r of sorted) {
      const y = this.toYMD(r.fechaRegistro);
      if (!y) continue;
      const last = out[out.length - 1];
      if (last && last.ymd === y) {
        last.regs.push(r);
      } else {
        out.push({ ymd: y, regs: [r] });
      }
    }
    return out;
  }

  private guiaParaDia(
    map: Map<number, GuiaGeneticaEcuadorDetalleDto>,
    dia: number
  ): GuiaGeneticaEcuadorDetalleDto | undefined {
    if (map.has(dia)) {
      return map.get(dia);
    }
    let best = -1;
    for (const k of map.keys()) {
      if (k <= dia && k > best) {
        best = k;
      }
    }
    return best >= 0 ? map.get(best) : undefined;
  }

  private calcularDiaVida(fechaRegistro: string | Date): number {
    const encYmd = this.toYMD(this.selectedLote?.fechaEncaset);
    const regYmd = this.toYMD(fechaRegistro);
    if (!encYmd || !regYmd) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const enc = this.ymdToLocalNoonDate(encYmd);
    const reg = this.ymdToLocalNoonDate(regYmd);
    if (!enc || !reg) return 0;
    return Math.max(0, Math.floor((reg.getTime() - enc.getTime()) / MS_DAY));
  }

  private pesoMixtoReg(r: SeguimientoLoteLevanteDto): number {
    const h = r.pesoPromH ?? 0;
    const m = r.pesoPromM ?? 0;
    if (h > 0 && m > 0) {
      return (h + m) / 2;
    }
    return h > 0 ? h : m;
  }

  /**
   * Kg de alimento del día: primero consumoKgHembras/Machos; si vienen en 0, suma ítems tipo alimento en metadata (Ecuador).
   */
  private consumoAlimentoKgDesdeRegistro(r: SeguimientoLoteLevanteDto): number {
    const direct = (r.consumoKgHembras ?? 0) + (r.consumoKgMachos ?? 0);
    if (direct > 0) {
      return direct;
    }
    const meta = r.metadata as
      | { itemsHembras?: MetadataItem[]; itemsMachos?: MetadataItem[] }
      | null
      | undefined;
    if (!meta) {
      return 0;
    }
    return this.sumaAlimentoKgItems(meta.itemsHembras) + this.sumaAlimentoKgItems(meta.itemsMachos);
  }

  private sumaAlimentoKgItems(items?: MetadataItem[] | null): number {
    if (!items?.length) {
      return 0;
    }
    let t = 0;
    for (const it of items) {
      const tipo = String(it?.tipoItem ?? '').toLowerCase();
      if (!tipo.includes('alimento')) {
        continue;
      }
      const c = Number(it?.cantidad);
      if (!Number.isFinite(c) || c <= 0) {
        continue;
      }
      const u = String(it?.unidad ?? '')
        .toLowerCase()
        .trim();
      if (u === 'kg' || u === 'kilogramo' || u === 'kilogramos') {
        t += c;
      } else if (u === 'g' || u === 'gr' || u === 'gramos') {
        t += c / 1000;
      } else if (u === 'unidades' || u === 'unidad' || u === '') {
        // Inventario a veces usa "unidades" para sacos/bolsas: se asume kg si no hay consumoKg
        t += c;
      } else {
        t += c;
      }
    }
    return t;
  }

  private pesoInicialMixtoLote(): number {
    const l = this.selectedLote;
    if (!l) return 0;
    if (l.pesoMixto != null && l.pesoMixto > 0) {
      return l.pesoMixto;
    }
    const h = l.pesoInicialH ?? 0;
    const m = l.pesoInicialM ?? 0;
    if (h > 0 && m > 0) {
      return (h + m) / 2;
    }
    return h || m;
  }

  private avesInicialesLote(): number {
    const n = this.selectedLote?.avesEncasetadas;
    if (n != null && n > 0) {
      return n;
    }
    const l = this.selectedLote as LoteDto | undefined;
    const h = l?.hembrasL ?? 0;
    const m = l?.machosL ?? 0;
    const x = l?.mixtas ?? 0;
    const t = h + m + x;
    return t > 0 ? t : 0;
  }

  private toYMD(value: string | Date | null | undefined): string | null {
    if (value == null || value === '') {
      return null;
    }
    if (typeof value === 'string') {
      const m = value.match(/^(\d{4}-\d{2}-\d{2})/);
      if (m) {
        return m[1];
      }
      const d = new Date(value);
      if (isNaN(d.getTime())) {
        return null;
      }
      const y = d.getFullYear();
      const mo = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${y}-${mo}-${day}`;
    }
    const d = value;
    const y = d.getFullYear();
    const mo = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${mo}-${day}`;
  }

  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) {
      return null;
    }
    const m = ymd.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (!m) {
      return null;
    }
    const y = Number(m[1]);
    const mo = Number(m[2]) - 1;
    const day = Number(m[3]);
    return new Date(y, mo, day, 12, 0, 0, 0);
  }

  formatDMY(ymd: string): string {
    const p = ymd.split('-');
    if (p.length !== 3) {
      return ymd;
    }
    return `${p[2]}/${p[1]}/${p[0]}`;
  }

  formatNum(v: number | null | undefined, decimals = 1): string {
    if (v == null || Number.isNaN(v)) {
      return '—';
    }
    return v.toFixed(decimals);
  }

  formatPct(v: number | null | undefined, decimals = 2): string {
    if (v == null || Number.isNaN(v)) {
      return '—';
    }
    return `${v.toFixed(decimals)}%`;
  }

  /** Etiqueta comparativa: registro vs guía (sin “promedio ganancia”; solo ganancia diaria de la tabla). */
  tagPeso(row: IndicadorDiarioFila): ComparativoTag {
    if (row.pesoRealG <= 0 || row.pesoTablaG <= 0) {
      return 'na';
    }
    const a = Math.abs(row.difPesoVsTablaPct);
    if (a <= 5) {
      return 'ok';
    }
    if (a <= 12) {
      return 'warn';
    }
    return 'bad';
  }

  tagGanancia(row: IndicadorDiarioFila): ComparativoTag {
    if (row.gananciaDiariaRealG == null || row.gananciaDiariaTablaG <= 0) {
      return 'na';
    }
    const d = Math.abs(row.gananciaDiariaRealG - row.gananciaDiariaTablaG);
    const tol = Math.max(3, row.gananciaDiariaTablaG * 0.12);
    if (d <= tol) {
      return 'ok';
    }
    if (d <= tol * 2) {
      return 'warn';
    }
    return 'bad';
  }

  tagConsumoDiario(row: IndicadorDiarioFila): ComparativoTag {
    if (row.consumoDiarioTablaG <= 0) {
      return 'na';
    }
    const rel = Math.abs(row.consumoDiarioRealG - row.consumoDiarioTablaG) / row.consumoDiarioTablaG;
    if (rel <= 0.1) {
      return 'ok';
    }
    if (rel <= 0.2) {
      return 'warn';
    }
    return 'bad';
  }

  tagAlimentoAcum(row: IndicadorDiarioFila): ComparativoTag {
    if (row.alimentoAcumTablaG <= 0) {
      return 'na';
    }
    const rel = Math.abs(row.alimentoAcumRealG - row.alimentoAcumTablaG) / row.alimentoAcumTablaG;
    if (rel <= 0.1) {
      return 'ok';
    }
    if (rel <= 0.18) {
      return 'warn';
    }
    return 'bad';
  }

  tagCa(row: IndicadorDiarioFila): ComparativoTag {
    if (row.caReal == null || row.caTabla <= 0) {
      return 'na';
    }
    const rel = Math.abs(row.caReal - row.caTabla) / row.caTabla;
    if (rel <= 0.08) {
      return 'ok';
    }
    if (rel <= 0.15) {
      return 'warn';
    }
    return 'bad';
  }

  /** Menor mort.+sel. vs guía suele ser mejor; alerta si real supera guía de forma clara. */
  tagMortSel(row: IndicadorDiarioFila): ComparativoTag {
    if (row.mortSelTablaPct <= 0 && row.mortSelRealPct <= 0) {
      return 'na';
    }
    if (row.mortSelTablaPct <= 0) {
      return row.mortSelRealPct <= 0.15 ? 'ok' : 'warn';
    }
    if (row.mortSelRealPct <= row.mortSelTablaPct * 1.1) {
      return 'ok';
    }
    if (row.mortSelRealPct <= row.mortSelTablaPct * 1.35) {
      return 'warn';
    }
    return 'bad';
  }

  tagClass(t: ComparativoTag): string {
    return `cmp-tag cmp-tag--${t}`;
  }

  tagLabel(t: ComparativoTag): string {
    switch (t) {
      case 'ok':
        return 'OK';
      case 'warn':
        return 'Rev.';
      case 'bad':
        return 'Alerta';
      default:
        return '—';
    }
  }

  /** Bandas de color alineadas a fases típicas de la guía (días 0–7, 8–14, 15+). */
  faseDiaClass(dia: number): Record<string, boolean> {
    if (dia <= 7) {
      return { 'indicadores-diarios__row--fase1': true };
    }
    if (dia <= 14) {
      return { 'indicadores-diarios__row--fase2': true };
    }
    return { 'indicadores-diarios__row--fase3': true };
  }
}

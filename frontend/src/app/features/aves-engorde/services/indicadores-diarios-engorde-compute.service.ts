import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SeguimientoLoteLevanteDto } from '../../lote-levante/services/seguimiento-lote-levante.service';
import { LoteDto } from '../../lote/services/lote.service';
import { LotePosturaLevanteDto } from '../../lote/services/lote-postura-levante.service';
import {
  GuiaGeneticaEcuadorDetalleDto,
  GuiaGeneticaEcuadorService
} from '../../config/guia-genetica-ecuador/guia-genetica-ecuador.service';
import { IndicadorDiarioFila } from '../../lote-levante/services/indicadores-diarios.models';

interface MetadataItem {
  unidad?: string;
  cantidad?: number;
  tipoItem?: string;
}

export interface IndicadoresDiariosEngordeComputeResult {
  filas: IndicadorDiarioFila[];
  errorGuia: string | null;
  guiaOk: boolean;
  etiquetaGuiaCargada: string;
}

/**
 * Indicadores diarios solo para seguimiento pollo engorde: consumo mixto desde `consumoKgHembras`
 * (+ ítems generales), un solo saldo de aves.
 */
@Injectable({ providedIn: 'root' })
export class IndicadoresDiariosEngordeComputeService {
  constructor(private guiaEcuador: GuiaGeneticaEcuadorService) {}

  async compute(
    seguimientos: SeguimientoLoteLevanteDto[],
    selectedLote: LoteDto | LotePosturaLevanteDto | null
  ): Promise<IndicadoresDiariosEngordeComputeResult> {
    const empty: IndicadoresDiariosEngordeComputeResult = {
      filas: [],
      errorGuia: null,
      guiaOk: false,
      etiquetaGuiaCargada: ''
    };

    if (!seguimientos?.length || !selectedLote) {
      return empty;
    }

    const raza = (selectedLote.raza ?? '').trim();
    const anio = Number(selectedLote.anoTablaGenetica);
    let etiquetaGuiaCargada = '';
    if (!raza || !Number.isFinite(anio) || anio <= 0) {
      return {
        ...empty,
        errorGuia:
          'Configure raza y año de tabla genética en el lote de engorde para comparar con la guía Ecuador (mixto).'
      };
    }
    etiquetaGuiaCargada = `Guía genética Ecuador · sexo mixto · ${raza} · año ${anio}`;

    let guiaPorDia = new Map<number, GuiaGeneticaEcuadorDetalleDto>();
    try {
      const detalle = await firstValueFrom(this.guiaEcuador.getDatos(raza, anio, 'mixto'));
      for (const d of detalle ?? []) {
        if (!guiaPorDia.has(d.dia)) {
          guiaPorDia.set(d.dia, d);
        }
      }
      if (guiaPorDia.size === 0) {
        return {
          ...empty,
          errorGuia: `No hay datos de guía Ecuador (sexo mixto) para ${raza} / ${anio}. Importe o cargue la tabla en Configuración.`,
          etiquetaGuiaCargada
        };
      }
    } catch {
      return {
        ...empty,
        errorGuia: 'No se pudo cargar la guía genética Ecuador. Intente de nuevo.',
        etiquetaGuiaCargada
      };
    }

    const diasOrdenados = this.agruparRegistrosPorDia(seguimientos);
    const inicialAves = this.avesInicialesLote(selectedLote);
    let avesTotal = inicialAves;
    const mixtoSinDesgloseSexo = true;

    const pesoIni = this.pesoInicialMixtoLote(selectedLote);

    let acumMix = 0;
    let ultimoPesoMedido = pesoIni;

    const out: IndicadorDiarioFila[] = [];

    for (const { ymd, regs } of diasOrdenados) {
      const fechaRef = regs[0]?.fechaRegistro ?? ymd;
      const dia = this.calcularDiaVida(selectedLote, fechaRef);
      const g = this.guiaParaDia(guiaPorDia, dia);

      const mort = regs.reduce((s, r) => s + (r.mortalidadHembras ?? 0) + (r.mortalidadMachos ?? 0), 0);
      const sel = regs.reduce((s, r) => s + (r.selH ?? 0) + (r.selM ?? 0), 0);
      const errH = regs.reduce((s, r) => s + (r.errorSexajeHembras ?? 0), 0);
      const errM = regs.reduce((s, r) => s + (r.errorSexajeMachos ?? 0), 0);

      const ultimo = regs[regs.length - 1];
      const pesoReal = this.pesoMixtoReg(ultimo);
      const pesoH = ultimo.pesoPromH ?? 0;
      const pesoM = ultimo.pesoPromM ?? 0;

      const avesInicio = avesTotal;

      const consumoKgTotal = regs.reduce((s, r) => s + this.consumoAlimentoKgPolloEngordeMixto(r), 0);

      const consumoPorAveMix = avesInicio > 0 ? (consumoKgTotal * 1000) / avesInicio : 0;
      acumMix += consumoPorAveMix;

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
      const caRealMix = pesoParaCa > 0 && acumMix > 0 ? acumMix / pesoParaCa : null;

      const mortSelRealPct = avesInicio > 0 ? ((mort + sel) / avesInicio) * 100 : 0;

      const difPesoVsTablaPct =
        pesoTablaG > 0 && pesoReal > 0 ? ((pesoReal - pesoTablaG) / pesoTablaG) * 100 : 0;

      avesTotal = Math.max(0, avesTotal - mort - sel - errH - errM);

      const avesFin = avesTotal;
      const mortAcumPct = inicialAves > 0 ? ((inicialAves - avesFin) / inicialAves) * 100 : 0;

      out.push({
        fechaYmd: ymd,
        dia,
        avesInicioDia: avesInicio,
        avesHembrasInicioDia: 0,
        avesMachosInicioDia: 0,
        mixtoSinDesgloseSexo,
        pesoRealG: pesoReal,
        pesoTablaG,
        pesoRealGA: pesoH,
        pesoRealGB: pesoM,
        gananciaDiariaRealG: gananciaReal,
        gananciaDiariaTablaG: gananciaTablaG,
        consumoDiarioRealGA: null,
        consumoDiarioRealGB: null,
        consumoDiarioRealG: consumoPorAveMix,
        consumoDiarioTablaG: consumoTablaG,
        alimentoAcumRealGA: null,
        alimentoAcumRealGB: null,
        alimentoAcumRealG: acumMix,
        alimentoAcumTablaG,
        caRealA: null,
        caRealB: null,
        caReal: caRealMix,
        caTabla,
        mortSelRealPct,
        mortSelTablaPct: mortTablaPct,
        difPesoVsTablaPct,
        mortAcumPct
      });

      if (pesoReal > 0) {
        ultimoPesoMedido = pesoReal;
      }
    }

    return {
      filas: out,
      errorGuia: null,
      guiaOk: true,
      etiquetaGuiaCargada
    };
  }

  private consumoAlimentoKgPolloEngordeMixto(r: SeguimientoLoteLevanteDto): number {
    const h = r.consumoKgHembras ?? 0;
    const gen = this.consumoAlimentoKgGeneralesSolo(r);
    if (h + gen > 0) {
      return h + gen;
    }
    return this.consumoAlimentoKgTotalDesdeRegistro(r);
  }

  private consumoAlimentoKgTotalDesdeRegistro(r: SeguimientoLoteLevanteDto): number {
    const direct = (r.consumoKgHembras ?? 0) + (r.consumoKgMachos ?? 0);
    if (direct > 0) {
      return direct + this.consumoAlimentoKgGeneralesSolo(r);
    }
    const meta = r.metadata as
      | { itemsHembras?: MetadataItem[]; itemsMachos?: MetadataItem[]; itemsGenerales?: MetadataItem[] }
      | null
      | undefined;
    const ad = r.itemsAdicionales as { itemsGenerales?: MetadataItem[] } | null | undefined;
    return (
      this.sumaAlimentoKgItems(meta?.itemsHembras) +
      this.sumaAlimentoKgItems(meta?.itemsMachos) +
      this.sumaAlimentoKgItems(meta?.itemsGenerales) +
      this.sumaAlimentoKgItems(ad?.itemsGenerales)
    );
  }

  private consumoAlimentoKgGeneralesSolo(r: SeguimientoLoteLevanteDto): number {
    const meta = r.metadata as { itemsGenerales?: MetadataItem[] } | null | undefined;
    let kg = this.sumaAlimentoKgItems(meta?.itemsGenerales);
    const ad = r.itemsAdicionales as { itemsGenerales?: MetadataItem[] } | null | undefined;
    kg += this.sumaAlimentoKgItems(ad?.itemsGenerales);
    return kg;
  }

  private agruparRegistrosPorDia(
    seguimientos: SeguimientoLoteLevanteDto[]
  ): { ymd: string; regs: SeguimientoLoteLevanteDto[] }[] {
    const sorted = [...seguimientos].sort((a, b) => {
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

  private calcularDiaVida(
    selectedLote: LoteDto | LotePosturaLevanteDto,
    fechaRegistro: string | Date
  ): number {
    const encYmd = this.toYMD(selectedLote?.fechaEncaset);
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
        t += c;
      } else {
        t += c;
      }
    }
    return t;
  }

  private pesoInicialMixtoLote(l: LoteDto | LotePosturaLevanteDto): number {
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

  private avesInicialesLote(l: LoteDto | LotePosturaLevanteDto): number {
    const n = l.avesEncasetadas;
    if (n != null && n > 0) {
      return n;
    }
    const ld = l as LoteDto;
    const h = ld?.hembrasL ?? 0;
    const m = ld?.machosL ?? 0;
    const x = ld?.mixtas ?? 0;
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
}

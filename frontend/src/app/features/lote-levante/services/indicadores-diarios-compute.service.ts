import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SeguimientoLoteLevanteDto } from './seguimiento-lote-levante.service';
import { LotePosturaLevanteDto } from '../../lote/services/lote-postura-levante.service';
import { LoteDto } from '../../lote/services/lote.service';
import {
  GuiaGeneticaEcuadorDetalleDto,
  GuiaGeneticaEcuadorService
} from '../../config/guia-genetica-ecuador/guia-genetica-ecuador.service';
import { IndicadorDiarioFila } from './indicadores-diarios.models';

interface MetadataItem {
  unidad?: string;
  cantidad?: number;
  tipoItem?: string;
}

export interface IndicadoresDiariosComputeResult {
  filas: IndicadorDiarioFila[];
  errorGuia: string | null;
  guiaOk: boolean;
  etiquetaGuiaCargada: string;
}

@Injectable({ providedIn: 'root' })
export class IndicadoresDiariosComputeService {
  constructor(private guiaEcuador: GuiaGeneticaEcuadorService) {}

  async compute(
    seguimientos: SeguimientoLoteLevanteDto[],
    selectedLote: LoteDto | LotePosturaLevanteDto | null
  ): Promise<IndicadoresDiariosComputeResult> {
    const empty: IndicadoresDiariosComputeResult = {
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
    const { h: inicialH, m: inicialM, mixtoSinDesgloseSexo } = this.inicialAvesHembrasMachos(
      selectedLote,
      inicialAves
    );

    let avesH = inicialH;
    let avesM = inicialM;
    let avesTotal = mixtoSinDesgloseSexo ? inicialAves : inicialH + inicialM;

    const pesoIni = this.pesoInicialMixtoLote(selectedLote);
    const { h: pesoIniH, m: pesoIniM } = this.pesoInicialHembrasMachos(selectedLote, pesoIni);

    let acumA = 0;
    let acumB = 0;
    let acumMix = 0;
    let ultimoPesoH = pesoIniH;
    let ultimoPesoM = pesoIniM;
    let ultimoPesoMedido = pesoIni;

    const out: IndicadorDiarioFila[] = [];

    for (const { ymd, regs } of diasOrdenados) {
      const fechaRef = regs[0]?.fechaRegistro ?? ymd;
      const dia = this.calcularDiaVida(selectedLote, fechaRef);
      const g = this.guiaParaDia(guiaPorDia, dia);

      const mort = regs.reduce((s, r) => s + (r.mortalidadHembras ?? 0) + (r.mortalidadMachos ?? 0), 0);
      const sel = regs.reduce((s, r) => s + (r.selH ?? 0) + (r.selM ?? 0), 0);
      const mortH = regs.reduce((s, r) => s + (r.mortalidadHembras ?? 0), 0);
      const mortM = regs.reduce((s, r) => s + (r.mortalidadMachos ?? 0), 0);
      const selH = regs.reduce((s, r) => s + (r.selH ?? 0), 0);
      const selM = regs.reduce((s, r) => s + (r.selM ?? 0), 0);

      const ultimo = regs[regs.length - 1];
      const pesoReal = this.pesoMixtoReg(ultimo);
      const pesoH = ultimo.pesoPromH ?? 0;
      const pesoM = ultimo.pesoPromM ?? 0;

      const avesHInicio = avesH;
      const avesMInicio = avesM;
      const avesInicio = mixtoSinDesgloseSexo ? avesTotal : avesHInicio + avesMInicio;

      let consumoKgTotal = 0;
      let consumoKgH = 0;
      let consumoKgM = 0;

      if (mixtoSinDesgloseSexo) {
        consumoKgTotal = regs.reduce((s, r) => s + this.consumoAlimentoKgTotalDesdeRegistro(r), 0);
      } else {
        const part = this.consumoKgHembrasMachosYGeneralesPorDia(regs, avesHInicio, avesMInicio);
        consumoKgH = part.kgH;
        consumoKgM = part.kgM;
        consumoKgTotal = consumoKgH + consumoKgM;
      }

      const consumoPorAveMix =
        avesInicio > 0 ? (consumoKgTotal * 1000) / avesInicio : 0;

      let consumoPorAveA: number | null = null;
      let consumoPorAveB: number | null = null;
      if (!mixtoSinDesgloseSexo) {
        consumoPorAveA = avesHInicio > 0 ? (consumoKgH * 1000) / avesHInicio : 0;
        consumoPorAveB = avesMInicio > 0 ? (consumoKgM * 1000) / avesMInicio : 0;
        acumA += consumoPorAveA;
        acumB += consumoPorAveB;
      }
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
      const caRealMix =
        pesoParaCa > 0 && acumMix > 0 ? acumMix / pesoParaCa : null;

      let caRealA: number | null = null;
      let caRealB: number | null = null;
      if (!mixtoSinDesgloseSexo) {
        const pH = pesoH > 0 ? pesoH : 0;
        const pM = pesoM > 0 ? pesoM : 0;
        caRealA = pH > 0 && acumA > 0 ? acumA / pH : null;
        caRealB = pM > 0 && acumB > 0 ? acumB / pM : null;
      }

      const mortSelRealPct =
        avesInicio > 0 ? ((mort + sel) / avesInicio) * 100 : 0;

      const difPesoVsTablaPct =
        pesoTablaG > 0 && pesoReal > 0 ? ((pesoReal - pesoTablaG) / pesoTablaG) * 100 : 0;

      if (mixtoSinDesgloseSexo) {
        avesTotal = avesTotal - mort - sel;
      } else {
        avesH = avesH - mortH - selH;
        avesM = avesM - mortM - selM;
      }

      const avesFin = mixtoSinDesgloseSexo ? avesTotal : avesH + avesM;
      const mortAcumPct =
        inicialAves > 0 ? ((inicialAves - avesFin) / inicialAves) * 100 : 0;

      out.push({
        fechaYmd: ymd,
        dia,
        avesInicioDia: avesInicio,
        avesHembrasInicioDia: mixtoSinDesgloseSexo ? 0 : avesHInicio,
        avesMachosInicioDia: mixtoSinDesgloseSexo ? 0 : avesMInicio,
        mixtoSinDesgloseSexo,
        pesoRealG: pesoReal,
        pesoTablaG,
        pesoRealGA: pesoH,
        pesoRealGB: pesoM,
        gananciaDiariaRealG: gananciaReal,
        gananciaDiariaTablaG: gananciaTablaG,
        consumoDiarioRealGA: mixtoSinDesgloseSexo ? null : consumoPorAveA,
        consumoDiarioRealGB: mixtoSinDesgloseSexo ? null : consumoPorAveB,
        consumoDiarioRealG: consumoPorAveMix,
        consumoDiarioTablaG: consumoTablaG,
        alimentoAcumRealGA: mixtoSinDesgloseSexo ? null : acumA,
        alimentoAcumRealGB: mixtoSinDesgloseSexo ? null : acumB,
        alimentoAcumRealG: acumMix,
        alimentoAcumTablaG,
        caRealA: caRealA,
        caRealB: caRealB,
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
      if (pesoH > 0) {
        ultimoPesoH = pesoH;
      }
      if (pesoM > 0) {
        ultimoPesoM = pesoM;
      }
    }

    return {
      filas: out,
      errorGuia: null,
      guiaOk: true,
      etiquetaGuiaCargada
    };
  }

  /** Desglose hembras/machos desde ficha de lote; si no hay, solo totales (mixto). */
  private inicialAvesHembrasMachos(
    l: LoteDto | LotePosturaLevanteDto,
    totalEncasetadas: number
  ): { h: number; m: number; mixtoSinDesgloseSexo: boolean } {
    const ld = l as LoteDto;
    const hem = ld?.hembrasL ?? 0;
    const mach = ld?.machosL ?? 0;
    const mix = ld?.mixtas ?? 0;
    if (hem > 0 || mach > 0 || mix > 0) {
      const mixH = Math.floor(mix / 2);
      const mixM = mix - mixH;
      return { h: hem + mixH, m: mach + mixM, mixtoSinDesgloseSexo: false };
    }
    if (totalEncasetadas > 0) {
      return { h: 0, m: 0, mixtoSinDesgloseSexo: true };
    }
    return { h: 0, m: 0, mixtoSinDesgloseSexo: true };
  }

  private pesoInicialHembrasMachos(
    l: LoteDto | LotePosturaLevanteDto,
    pesoMixto: number
  ): { h: number; m: number } {
    const hi = l.pesoInicialH ?? 0;
    const mi = l.pesoInicialM ?? 0;
    if (hi > 0 && mi > 0) {
      return { h: hi, m: mi };
    }
    if (hi > 0) {
      return { h: hi, m: pesoMixto > 0 ? pesoMixto : hi };
    }
    if (mi > 0) {
      return { h: pesoMixto > 0 ? pesoMixto : mi, m: mi };
    }
    return { h: pesoMixto, m: pesoMixto };
  }

  private consumoKgHembrasMachosYGeneralesPorDia(
    regs: SeguimientoLoteLevanteDto[],
    avesHInicio: number,
    avesMInicio: number
  ): { kgH: number; kgM: number } {
    let kgH = 0;
    let kgM = 0;
    const t = avesHInicio + avesMInicio;
    for (const r of regs) {
      kgH += this.consumoAlimentoKgHembrasSolo(r);
      kgM += this.consumoAlimentoKgMachosSolo(r);
      const kgGen = this.consumoAlimentoKgGeneralesSolo(r);
      if (t > 0 && kgGen > 0) {
        kgH += kgGen * (avesHInicio / t);
        kgM += kgGen * (avesMInicio / t);
      }
    }
    return { kgH, kgM };
  }

  /** Igual que antes: hembras + machos + metadata; para modo mixto sin sexo. */
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

  private consumoAlimentoKgHembrasSolo(r: SeguimientoLoteLevanteDto): number {
    const direct = r.consumoKgHembras ?? 0;
    if (direct > 0) {
      return direct;
    }
    const meta = r.metadata as { itemsHembras?: MetadataItem[] } | null | undefined;
    return this.sumaAlimentoKgItems(meta?.itemsHembras);
  }

  private consumoAlimentoKgMachosSolo(r: SeguimientoLoteLevanteDto): number {
    const direct = r.consumoKgMachos ?? 0;
    if (direct > 0) {
      return direct;
    }
    const meta = r.metadata as { itemsMachos?: MetadataItem[] } | null | undefined;
    return this.sumaAlimentoKgItems(meta?.itemsMachos);
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

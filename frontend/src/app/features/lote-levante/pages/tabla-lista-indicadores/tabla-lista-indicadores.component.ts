import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { exportarObjetosMultiHojaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { SeguimientoLoteLevanteDto, SeguimientoLoteLevanteService, IndicadorSemanalLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';

interface IndicadorSemanal {
  semana: number;
  fechaInicio: string;
  fechaFin: string;
  // Información del lote
  region?: string | null;
  granja?: string | null;
  nave?: string | null;
  sublote?: string | null;
  // Aves
  avesInicioSemana: number;
  avesFinSemana: number;
  // Consumo
  consumoDiario: number; // Consumo diario por ave (g/ave/día) - para comparar con tabla
  consumoTabla: number; // Consumo esperado de tabla (g/ave/día)
  consumoTotalSemana: number; // Consumo total de la semana en gramos
  // Guía genética / comparativos
  pesoTabla: number; // peso esperado tabla (promedio H/M)
  unifReal: number; // uniformidad real (promedio H/M del último registro)
  unifTabla: number; // uniformidad esperada tabla
  mortTabla: number; // mortalidad esperada tabla (promedio H/M)
  difPesoPct: number; // diferencia % vs tabla (peso)
  // Ganancia
  gananciaSemana: number;
  gananciaDiariaAcumulada: number;
  gananciaTabla: number; // ganancia esperada según tabla (pesoTabla - pesoTablaAnterior)
  // Mortalidad & Selección
  mortalidadSem: number;
  seleccionSem: number;
  errorSexajeSem: number; // Error de sexaje semanal (%)
  mortalidadMasSeleccion: number;
  // Indicadores
  eficiencia: number;
  ip: number;
  vpi: number;
  // ── TK-2026-000022: series POR SEXO ───────────────────────────────────────
  // La tabla mostraba una sola serie sin decir de qué sexo era, y `pesoCierre`/`unifReal` son
  // el promedio aritmético de hembras y machos: un número que no le corresponde a ninguna ave.
  // Todas llegan de la fn (el front no calcula) y son `null` cuando el sexo no tiene aves, la
  // semana no tuvo pesaje o la guía no trae el dato del sexo.
  avesInicioHembras?: number | null;
  avesFinHembras?: number | null;
  avesInicioMachos?: number | null;
  avesFinMachos?: number | null;
  consumoDiarioHembras?: number | null;
  consumoDiarioMachos?: number | null;
  consumoTablaHembras?: number | null;
  consumoTablaMachos?: number | null;
  consumoTotalSemanaHembras?: number | null;
  consumoTotalSemanaMachos?: number | null;
  gananciaHembras?: number | null;
  gananciaMachos?: number | null;
  pesoHembras?: number | null;
  pesoMachos?: number | null;
  pesoTablaHembras?: number | null;
  pesoTablaMachos?: number | null;
  difPesoPctHembras?: number | null;
  difPesoPctMachos?: number | null;
  unifHembras?: number | null;
  unifMachos?: number | null;
  mortPctHembras?: number | null;
  mortPctMachos?: number | null;
  mortTablaHembras?: number | null;
  mortTablaMachos?: number | null;
  seleccionPctHembras?: number | null;
  seleccionPctMachos?: number | null;
  errorSexajePctHembras?: number | null;
  errorSexajePctMachos?: number | null;
  retiroPctHembras?: number | null;
  retiroPctMachos?: number | null;
  // Otros
  saldoAvesSemanal: number;
  mortalidadAcum: number;
  seleccionAcum: number;
  mortalidadMasSeleccionAcum: number;
  pisoTermicoVisible: boolean;
  pesoInicial: number;
  pesoCierre: number;
  pesoAnterior: number;
  pesoTablaAnterior: number;
  observaciones?: string | null; // Observaciones de la semana (del último registro)
}

@Component({
  selector: 'app-tabla-lista-indicadores',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-lista-indicadores.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabla-lista-indicadores.component.scss']
})
export class TablaListaIndicadoresComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  /** LoteDto (aves-engorde) o LotePosturaLevanteDto (seguimiento levante). */
  @Input() selectedLote: LoteDto | LotePosturaLevanteDto | null = null;
  @Input() loading: boolean = false;

  // Datos calculados
  indicadoresSemanales: IndicadorSemanal[] = [];
  
  // Control de modal de fórmulas
  mostrarFormulas: boolean = false;
  
  /** Origen de los valores "tabla" mostrado como hint; con los indicadores en BD siempre es la guía Colombia ('clasica'). */
  fuenteGuiaIndicadores: 'ecuador-mixto' | 'clasica' | null = null;

  /** Estados de la carga contra la BD. Antes el endpoint podía fallar y la tabla simplemente
   *  quedaba vacía con el mismo mensaje que «este lote no tiene datos»: el usuario no podía
   *  distinguir «no hay» de «se cayó». Producción ya los tenía; ahora los dos. */
  cargandoIndicadores = false;
  errorIndicadores: string | null = null;

  constructor(
    private seguimientoSvc: SeguimientoLoteLevanteService
  ) { }

  /** Descarga UN libro Excel con HOJAS SEPARADAS: "Seguimiento" (registros diarios) e "Indicadores". */
  descargarExcel(): void {
    const nombre = ((this.selectedLote as any)?.loteNombre || 'lote')
      .toString().trim().replace(/[\\/:*?"<>|]+/g, '-').slice(0, 100) || 'lote';
    const stamp = new Date().toISOString().slice(0, 10);

    const seg = (this.seguimientos || []).map(s => ({
      Id: s.id,
      Fecha: (s.fechaRegistro || '').toString().slice(0, 10),
      MortalidadH: s.mortalidadHembras ?? 0,
      MortalidadM: s.mortalidadMachos ?? 0,
      SeleccionH: s.selH ?? 0,
      SeleccionM: s.selM ?? 0,
      ErrorSexajeH: s.errorSexajeHembras ?? 0,
      ErrorSexajeM: s.errorSexajeMachos ?? 0,
      TipoAlimento: s.tipoAlimento,
      ConsumoKgH: s.consumoKgHembras ?? 0,
      ConsumoKgM: s.consumoKgMachos ?? 0,
      PesoPromH: s.pesoPromH ?? null,
      PesoPromM: s.pesoPromM ?? null,
      UniformidadH: s.uniformidadH ?? null,
      UniformidadM: s.uniformidadM ?? null,
      CvH: s.cvH ?? null,
      CvM: s.cvM ?? null
    }));
    // TK-2026-000022 - el Excel sale con las mismas columnas que la pantalla: cada parametro
    // dice de que sexo es. Al final se conservan las columnas del lote completo que ya venian,
    // para no romper las planillas que alguien tenga armadas encima.
    const ind = (this.indicadoresSemanales || []).map((i: any) => ({
      Semana: i.semana,
      AvesInicioH: i.avesInicioHembras,
      AvesFinH: i.avesFinHembras,
      AvesInicioM: i.avesInicioMachos,
      AvesFinM: i.avesFinMachos,
      ConsumoDiaGrAveH: i.consumoDiarioHembras,
      ConsumoGuiaGrAveH: i.consumoTablaHembras,
      ConsumoDiaGrAveM: i.consumoDiarioMachos,
      ConsumoGuiaGrAveM: i.consumoTablaMachos,
      ConsumoLoteSemanaG: i.consumoTotalSemana,
      ConsumoSemanaGH: i.consumoTotalSemanaHembras,
      ConsumoSemanaGM: i.consumoTotalSemanaMachos,
      GananciaSemH: i.gananciaHembras,
      GananciaSemM: i.gananciaMachos,
      PesoRealH: i.pesoHembras,
      PesoGuiaH: i.pesoTablaHembras,
      DifPesoPctH: i.difPesoPctHembras,
      PesoRealM: i.pesoMachos,
      PesoGuiaM: i.pesoTablaMachos,
      DifPesoPctM: i.difPesoPctMachos,
      UnifRealH: i.unifHembras,
      UnifRealM: i.unifMachos,
      UnifGuia: i.unifTabla,
      PorcMortSemH: i.mortPctHembras,
      MortGuiaH: i.mortTablaHembras,
      PorcMortSemM: i.mortPctMachos,
      MortGuiaM: i.mortTablaMachos,
      PorcSelSemH: i.seleccionPctHembras,
      PorcSelSemM: i.seleccionPctMachos,
      PorcErrSexajeSemH: i.errorSexajePctHembras,
      PorcErrSexajeSemM: i.errorSexajePctMachos,
      PorcRetiroSemH: i.retiroPctHembras,
      PorcRetiroSemM: i.retiroPctMachos,
      // Lote completo (H+M). Ojo: PesoLotePromedio y UnifLotePromedio son el promedio
      // aritmetico de los dos sexos, no un peso ni una uniformidad de ave real.
      AvesInicioLote: i.avesInicioSemana,
      AvesFinLote: i.avesFinSemana,
      PesoLotePromedio: i.pesoCierre,
      UnifLotePromedio: i.unifReal,
      PorcMortSemLote: i.mortalidadSem,
      PorcSelSemLote: i.seleccionSem,
      PorcRetiroSemLote: i.mortalidadMasSeleccion
    }));
    exportarObjetosMultiHojaExcel([
      ...(seg.length ? [{ sheetName: 'Seguimiento', rows: seg }] : []),
      ...(ind.length ? [{ sheetName: 'Indicadores', rows: ind }] : []),
    ], { filenameFull: `levante-lote-${nombre}-seguimiento-indicadores-${stamp}.xlsx` });
  }

  ngOnInit(): void {
    this.calcularIndicadores().catch(error => {
      console.error('Error calculando indicadores:', error);
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      if (changes['selectedLote']) {
        this.fuenteGuiaIndicadores = null;
      }
      this.calcularIndicadores().catch(error => {
        console.error('Error calculando indicadores:', error);
      });
    }
  }

  // ================== INDICADORES (calculados en la BD) ==================
  /**
   * Los indicadores semanales de levante se calculan en la BD
   * (fn_indicadores_levante_postura, endpoint …/por-lote/{id}/indicadores).
   * El front SOLO pinta: NO recalcula desde los seguimientos crudos ni consulta
   * la guía en el cliente. La función usa la guía Colombia correcta
   * (guia_genetica_sanmarino_colombia), no la Ecuador-mixto que el front usaba antes.
   * Sin fallback de cálculo cliente (misma fuente que las gráficas): si no se
   * resuelve el loteId o el endpoint falla, la tabla queda vacía.
   */
  async calcularIndicadores(): Promise<void> {
    this.errorIndicadores = null;
    if (!this.seguimientos || this.seguimientos.length === 0 || !this.selectedLote) {
      this.indicadoresSemanales = [];
      this.fuenteGuiaIndicadores = null;
      return;
    }

    const loteId = this.resolverLoteId();
    if (loteId == null) {
      this.indicadoresSemanales = [];
      this.fuenteGuiaIndicadores = null;
      return;
    }
    this.cargandoIndicadores = true;
    try {
      const dto = await firstValueFrom(this.seguimientoSvc.getIndicadores(loteId));
      this.indicadoresSemanales = (dto || []).map(d => this.mapDtoAIndicador(d));
      this.fuenteGuiaIndicadores = 'clasica'; // guía Colombia real (BD)
    } catch (e) {
      console.warn('Indicadores desde BD no disponibles para la tabla:', e);
      this.indicadoresSemanales = [];
      this.fuenteGuiaIndicadores = null;
      this.errorIndicadores = 'No se pudieron cargar los indicadores de este lote.';
    } finally {
      this.cargandoIndicadores = false;
    }
  }

  /** loteId numérico (lotes.lote_id) para pedir los indicadores a la BD. */
  private resolverLoteId(): number | null {
    const candidatos = [
      (this.selectedLote as any)?.loteId,
      (this.seguimientos?.[0] as any)?.loteId
    ];
    for (const c of candidatos) {
      const n = Number(c);
      if (Number.isFinite(n) && n > 0) return n;
    }
    return null;
  }

  /** Mapea el DTO de la BD al modelo de la tabla; los campos de contexto (fechas, ubicación, observaciones) no son cálculo. */
  private mapDtoAIndicador(d: IndicadorSemanalLevanteDto): IndicadorSemanal {
    const obsSem = this.observacionesDeSemana(d.semana);
    return {
      semana: d.semana,
      fechaInicio: this.obtenerFechaInicioSemana(d.semana),
      fechaFin: this.obtenerFechaFinSemana(d.semana),
      region: this.selectedLote?.regional || null,
      granja: (this.selectedLote as any)?.farm?.name || null,
      nave: (this.selectedLote as any)?.nucleo?.nucleoNombre || null,
      sublote: this.extraerSublote((this.selectedLote as any)?.loteNombre || ''),
      avesInicioSemana: d.avesInicioSemana,
      avesFinSemana: d.avesFinSemana,
      consumoDiario: d.consumoDiario,
      consumoTabla: d.consumoTabla,
      consumoTotalSemana: d.consumoTotalSemana,
      pesoTabla: d.pesoTabla,
      unifReal: d.unifReal,
      unifTabla: d.unifTabla,
      mortTabla: d.mortTabla,
      difPesoPct: d.difPesoPct,
      gananciaSemana: d.gananciaSemana,
      gananciaDiariaAcumulada: d.gananciaDiariaAcumulada,
      gananciaTabla: d.gananciaTabla,
      mortalidadSem: d.mortalidadSem,
      seleccionSem: d.seleccionSem,
      errorSexajeSem: d.errorSexajeSem,
      mortalidadMasSeleccion: d.mortalidadMasSeleccion,
      eficiencia: d.eficiencia,
      ip: d.ip,
      vpi: d.vpi,
      // TK-2026-000022: se pasan tal cual (el front no calcula, solo pinta).
      avesInicioHembras: d.avesInicioHembras ?? null,
      avesFinHembras: d.avesFinHembras ?? null,
      avesInicioMachos: d.avesInicioMachos ?? null,
      avesFinMachos: d.avesFinMachos ?? null,
      consumoDiarioHembras: d.consumoDiarioHembras ?? null,
      consumoDiarioMachos: d.consumoDiarioMachos ?? null,
      consumoTablaHembras: d.consumoTablaHembras ?? null,
      consumoTablaMachos: d.consumoTablaMachos ?? null,
      consumoTotalSemanaHembras: d.consumoTotalSemanaHembras ?? null,
      consumoTotalSemanaMachos: d.consumoTotalSemanaMachos ?? null,
      gananciaHembras: d.gananciaHembras ?? null,
      gananciaMachos: d.gananciaMachos ?? null,
      pesoHembras: d.pesoHembras ?? null,
      pesoMachos: d.pesoMachos ?? null,
      pesoTablaHembras: d.pesoTablaHembras ?? null,
      pesoTablaMachos: d.pesoTablaMachos ?? null,
      difPesoPctHembras: d.difPesoPctHembras ?? null,
      difPesoPctMachos: d.difPesoPctMachos ?? null,
      unifHembras: d.unifHembras ?? null,
      unifMachos: d.unifMachos ?? null,
      mortPctHembras: d.mortPctHembras ?? null,
      mortPctMachos: d.mortPctMachos ?? null,
      mortTablaHembras: d.mortTablaHembras ?? null,
      mortTablaMachos: d.mortTablaMachos ?? null,
      seleccionPctHembras: d.seleccionPctHembras ?? null,
      seleccionPctMachos: d.seleccionPctMachos ?? null,
      errorSexajePctHembras: d.errorSexajePctHembras ?? null,
      errorSexajePctMachos: d.errorSexajePctMachos ?? null,
      retiroPctHembras: d.retiroPctHembras ?? null,
      retiroPctMachos: d.retiroPctMachos ?? null,
      saldoAvesSemanal: d.saldoAvesSemanal,
      mortalidadAcum: d.mortalidadAcum,
      seleccionAcum: d.seleccionAcum,
      mortalidadMasSeleccionAcum: d.mortalidadMasSeleccionAcum,
      pisoTermicoVisible: d.pisoTermicoVisible,
      pesoInicial: d.pesoInicial,
      pesoCierre: d.pesoCierre,
      pesoAnterior: d.pesoInicial,
      pesoTablaAnterior: 0,
      observaciones: obsSem
    };
  }

  /** Observaciones del último registro de la semana (contexto, no cálculo). */
  private observacionesDeSemana(semana: number): string | null {
    const regs = (this.seguimientos || [])
      .filter(r => this.calcularSemana(r.fechaRegistro) === semana)
      .sort((a, b) => (this.toYMD(a.fechaRegistro) ?? '').localeCompare(this.toYMD(b.fechaRegistro) ?? ''));
    return regs.length ? (regs[regs.length - 1].observaciones || null) : null;
  }

  /**
   * Semana 1 = días 0–6 desde encaset (mismo día encaset = semana 1). Alineado a calendario local (YYYY-MM-DD), no ISO UTC crudo.
   */
  private calcularSemana(fechaRegistro: string | Date): number {
    const encYmd = this.toYMD(this.selectedLote?.fechaEncaset);
    const regYmd = this.toYMD(fechaRegistro);
    if (!encYmd || !regYmd) return 1;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const enc = this.ymdToLocalNoonDate(encYmd);
    const reg = this.ymdToLocalNoonDate(regYmd);
    if (!enc || !reg) return 1;
    const diffDays = Math.floor((reg.getTime() - enc.getTime()) / MS_DAY);
    const semana = Math.floor(diffDays / 7) + 1;
    return Math.max(1, Math.min(25, semana));
  }

  // ================== VALIDACIONES Y ALERTAS ==================
  validarConsumo(consumoDiario: number | null | undefined, consumoTabla: number | null | undefined): { 
    esValido: boolean; 
    mensaje: string; 
    tipo: 'success' | 'warning' | 'error' 
  } {
    // Sin dato del sexo (o sin guía) no hay nada que validar: se pinta el guion sin alerta.
    if (consumoDiario === null || consumoDiario === undefined ||
        consumoTabla === null || consumoTabla === undefined) {
      return { esValido: true, mensaje: '', tipo: 'success' };
    }
    const diferencia = Math.abs(consumoDiario - consumoTabla);
    const porcentajeDiferencia = consumoTabla > 0 ? (diferencia / consumoTabla) * 100 : 0;
    
    // Rangos más específicos basados en la guía genética
    if (porcentajeDiferencia <= 5) {
      return {
        esValido: true,
        mensaje: `Consumo óptimo (${porcentajeDiferencia.toFixed(1)}% diferencia)`,
        tipo: 'success'
      };
    } else if (porcentajeDiferencia <= 15) {
      return {
        esValido: true,
        mensaje: `Consumo aceptable (${porcentajeDiferencia.toFixed(1)}% diferencia)`,
        tipo: 'success'
      };
    } else if (porcentajeDiferencia <= 25) {
      return {
        esValido: false,
        mensaje: `Consumo ${consumoDiario < consumoTabla ? 'bajo' : 'alto'} (${porcentajeDiferencia.toFixed(1)}% diferencia)`,
        tipo: 'warning'
      };
    } else {
      return {
        esValido: false,
        mensaje: `Consumo ${consumoDiario < consumoTabla ? 'muy bajo' : 'muy alto'} (${porcentajeDiferencia.toFixed(1)}% diferencia)`,
        tipo: 'error'
      };
    }
  }

  validarGanancia(gananciaSemana: number, semana: number, gananciaTabla?: number): { 
    esValido: boolean; 
    mensaje: string; 
    tipo: 'success' | 'warning' | 'error' 
  } {
    if (gananciaTabla && gananciaTabla > 0) {
      const diff = gananciaSemana - gananciaTabla;
      const pct = (Math.abs(diff) / gananciaTabla) * 100;
      if (pct <= 10) {
        return { esValido: true, mensaje: `Ganancia vs tabla OK (${pct.toFixed(1)}%)`, tipo: 'success' };
      }
      if (pct <= 25) {
        return { esValido: false, mensaje: `Ganancia difiere de tabla (${pct.toFixed(1)}%)`, tipo: 'warning' };
      }
      return { esValido: false, mensaje: `Ganancia muy diferente a tabla (${pct.toFixed(1)}%)`, tipo: 'error' };
    }

    // Rangos de ganancia esperada según la edad
    const gananciaEsperadaPorSemana: { [key: number]: { min: number; max: number; ideal: number } } = {
      1: { min: 8, max: 15, ideal: 12 },   // Semana 1: 8-15g/día, ideal 12g
      2: { min: 12, max: 20, ideal: 16 }, // Semana 2: 12-20g/día, ideal 16g
      3: { min: 15, max: 25, ideal: 20 },  // Semana 3: 15-25g/día, ideal 20g
      4: { min: 18, max: 28, ideal: 23 },  // Semana 4: 18-28g/día, ideal 23g
      5: { min: 20, max: 30, ideal: 25 },  // Semana 5: 20-30g/día, ideal 25g
      6: { min: 22, max: 32, ideal: 27 }, // Semana 6: 22-32g/día, ideal 27g
      7: { min: 24, max: 34, ideal: 29 }, // Semana 7: 24-34g/día, ideal 29g
      8: { min: 26, max: 36, ideal: 31 },  // Semana 8: 26-36g/día, ideal 31g
    };

    const rangoEsperado = gananciaEsperadaPorSemana[semana] || { min: 20, max: 35, ideal: 28 };
    
    if (gananciaSemana >= rangoEsperado.min && gananciaSemana <= rangoEsperado.max) {
      const diferenciaDelIdeal = Math.abs(gananciaSemana - rangoEsperado.ideal);
      const porcentajeDelIdeal = (diferenciaDelIdeal / rangoEsperado.ideal) * 100;
      
      if (porcentajeDelIdeal <= 10) {
        return {
          esValido: true,
          mensaje: `Ganancia óptima (${gananciaSemana.toFixed(1)}g/día)`,
          tipo: 'success'
        };
      } else {
        return {
          esValido: true,
          mensaje: `Ganancia aceptable (${gananciaSemana.toFixed(1)}g/día)`,
          tipo: 'success'
        };
      }
    } else if (gananciaSemana > 0) {
      return {
        esValido: false,
        mensaje: `Ganancia ${gananciaSemana < rangoEsperado.min ? 'baja' : 'alta'} (${gananciaSemana.toFixed(1)}g/día, esperado: ${rangoEsperado.min}-${rangoEsperado.max}g)`,
        tipo: 'warning'
      };
    } else if (gananciaSemana === 0) {
      return {
        esValido: false,
        mensaje: 'Sin ganancia de peso',
        tipo: 'warning'
      };
    } else {
      return {
        esValido: false,
        mensaje: `Pérdida de peso: ${Math.abs(gananciaSemana).toFixed(2)}g/día`,
        tipo: 'error'
      };
    }
  }

  validarMortalidad(mortalidadSemana: number | null | undefined, semana: number, mortalidadTabla?: number | null): { 
    esValido: boolean; 
    mensaje: string; 
    tipo: 'success' | 'warning' | 'error' 
  } {
    if (mortalidadSemana === null || mortalidadSemana === undefined) {
      return { esValido: true, mensaje: '', tipo: 'success' };
    }
    if (mortalidadTabla && mortalidadTabla > 0) {
      const diff = mortalidadSemana - mortalidadTabla;
      const pct = (Math.abs(diff) / mortalidadTabla) * 100;
      if (pct <= 15) {
        return { esValido: true, mensaje: `Mortalidad vs tabla OK (${pct.toFixed(1)}%)`, tipo: 'success' };
      }
      if (pct <= 40) {
        return { esValido: false, mensaje: `Mortalidad difiere de tabla (${pct.toFixed(1)}%)`, tipo: 'warning' };
      }
      return { esValido: false, mensaje: `Mortalidad muy diferente a tabla (${pct.toFixed(1)}%)`, tipo: 'error' };
    }

    // Mortalidad esperada según la edad (porcentaje semanal)
    const mortalidadEsperadaPorSemana: { [key: number]: { min: number; max: number; ideal: number } } = {
      1: { min: 0.1, max: 0.5, ideal: 0.3 },   // Semana 1: 0.1-0.5%, ideal 0.3%
      2: { min: 0.1, max: 0.4, ideal: 0.25 },  // Semana 2: 0.1-0.4%, ideal 0.25%
      3: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 3: 0.1-0.3%, ideal 0.2%
      4: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 4: 0.1-0.3%, ideal 0.2%
      5: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 5: 0.1-0.3%, ideal 0.2%
      6: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 6: 0.1-0.3%, ideal 0.2%
      7: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 7: 0.1-0.3%, ideal 0.2%
      8: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 8: 0.1-0.3%, ideal 0.2%
    };

    const rangoEsperado = mortalidadEsperadaPorSemana[semana] || { min: 0.1, max: 0.3, ideal: 0.2 };
    
    if (mortalidadSemana >= rangoEsperado.min && mortalidadSemana <= rangoEsperado.max) {
      return {
        esValido: true,
        mensaje: `Mortalidad normal (${mortalidadSemana.toFixed(2)}%)`,
        tipo: 'success'
      };
    } else if (mortalidadSemana < rangoEsperado.min) {
      return {
        esValido: true,
        mensaje: `Mortalidad baja (${mortalidadSemana.toFixed(2)}%)`,
        tipo: 'success'
      };
    } else if (mortalidadSemana <= rangoEsperado.max * 1.5) {
      return {
        esValido: false,
        mensaje: `Mortalidad alta (${mortalidadSemana.toFixed(2)}%, esperado: ${rangoEsperado.min}-${rangoEsperado.max}%)`,
        tipo: 'warning'
      };
    } else {
      return {
        esValido: false,
        mensaje: `Mortalidad crítica (${mortalidadSemana.toFixed(2)}%, esperado: ${rangoEsperado.min}-${rangoEsperado.max}%)`,
        tipo: 'error'
      };
    }
  }

  // ================== HELPERS DE FECHA ==================
  /** Prefijo YYYY-MM-DD desde string ISO o Date (calendario local para Date). */
  private toYMD(value: string | Date | null | undefined): string | null {
    if (value == null || value === '') return null;
    if (typeof value === 'string') {
      const m = value.match(/^(\d{4}-\d{2}-\d{2})/);
      if (m) return m[1];
      const d = new Date(value);
      if (isNaN(d.getTime())) return null;
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
    if (!ymd) return null;
    const m = ymd.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (!m) return null;
    const y = Number(m[1]);
    const mo = Number(m[2]) - 1;
    const day = Number(m[3]);
    return new Date(y, mo, day, 12, 0, 0, 0);
  }

  private addDaysToYmd(ymd: string, days: number): string | null {
    const d = this.ymdToLocalNoonDate(ymd);
    if (!d) return null;
    d.setDate(d.getDate() + days);
    const y = d.getFullYear();
    const mo = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${mo}-${day}`;
  }

  private obtenerFechaInicioSemana(semana: number): string {
    const encYmd = this.toYMD(this.selectedLote?.fechaEncaset);
    if (!encYmd) return '';
    return this.addDaysToYmd(encYmd, (semana - 1) * 7) ?? '';
  }

  private obtenerFechaFinSemana(semana: number): string {
    const encYmd = this.toYMD(this.selectedLote?.fechaEncaset);
    if (!encYmd) return '';
    return this.addDaysToYmd(encYmd, semana * 7 - 1) ?? '';
  }

  // ================== FORMATO ==================
  formatNumber = (value: number, decimals: number = 2): string => {
    return value.toFixed(decimals);
  };

  formatPercentage = (value: number, decimals: number = 2): string => {
    return `${value.toFixed(decimals)}%`;
  };

  /**
   * TK-2026-000022 — las series POR SEXO llegan en `null` cuando ese sexo no tiene saldo en el
   * lote, la semana no tuvo pesaje o la guía no trae el dato. Se pinta un guion, NO un cero: un
   * cero en una columna de peso o de uniformidad se lee como medición real.
   */
  formatOpcional = (value: number | null | undefined, decimals: number = 2): string =>
    value === null || value === undefined ? '—' : Number(value).toFixed(decimals);

  /** Igual que {@link formatOpcional} pero con el `%` (mismo criterio de guion para el null). */
  formatOpcionalPct = (value: number | null | undefined, decimals: number = 2): string =>
    value === null || value === undefined ? '—' : `${Number(value).toFixed(decimals)}%`;

  formatDate = (date: string): string => {
    return new Date(date).toLocaleDateString('es-ES');
  };

  // ================== HELPERS ==================
  private extraerSublote(loteNombre: string): string {
    if (!loteNombre) return '';
    const partes = loteNombre.trim().split(' ');
    if (partes.length > 1 && partes[partes.length - 1].length === 1) {
      return partes[partes.length - 1];
    }
    return '';
  }

  // ================== CHIPS DE CONTEXTO (Regional · Granja · Módulo · Sub Lote) ==================
  /** Regional del lote; el backend resuelve el nombre ("Oriente"/"Occidente"). Fallback '-' si no viene. */
  get chipRegional(): string {
    return this.selectedLote?.regional || '-';
  }

  get chipGranja(): string {
    return (this.selectedLote as any)?.farm?.name || '-';
  }

  get chipModulo(): string {
    return (this.selectedLote as any)?.nucleo?.nucleoNombre || '-';
  }

  get chipSubLote(): string {
    return this.extraerSublote((this.selectedLote as any)?.loteNombre || '') || '-';
  }

  // ================== FÓRMULAS DE INDICADORES ==================
  get gruposFormulas() {
    return [
      {
        titulo: '📊 Información Básica',
        formulas: [
          {
            nombre: 'Semana',
            formula: 'Número de semana desde la fecha de encaset'
          },
          {
            nombre: 'Período',
            formula: 'Rango de fechas de inicio y fin de la semana'
          },
          {
            nombre: 'Aves Inicio',
            formula: 'Aves al inicio de la semana (después de mortalidad y selección de semana anterior)'
          },
          {
            nombre: 'Aves Fin',
            formula: 'Aves Inicio - Mortalidad Total - Selección Total'
          }
        ]
      },
      {
        titulo: '🍽️ Consumo',
        formulas: [
          {
            nombre: 'Consumo Diario (g/ave/día)',
            formula: '(Consumo Total Semana en g) / (Aves Promedio × Días con Registro)'
          },
          {
            nombre: 'Consumo Tabla (g/ave/día)',
            formula: 'Valor de referencia de la guía genética según semana y raza'
          },
          {
            nombre: 'Consumo Total Semana (g)',
            formula: 'Σ(Consumo Hembras + Consumo Machos) × 1000 (conversión kg a g)'
          }
        ]
      },
      {
        titulo: '📈 Ganancia',
        formulas: [
          {
            nombre: 'Ganancia Semana (g)',
            formula: 'Peso Promedio Final - Peso Promedio Anterior'
          },
          {
            nombre: 'Ganancia Diaria (g/día)',
            formula: 'Ganancia Semana / 7 días'
          }
        ]
      },
      {
        titulo: '💀 Mortalidad & Selección',
        formulas: [
          {
            nombre: 'Mortalidad (%)',
            formula: '(Total Mortalidad Hembras + Mortalidad Machos) / Aves Inicio × 100'
          },
          {
            nombre: 'Selección (%)',
            formula: '(Total Selección Hembras + Selección Machos) / Aves Inicio × 100'
          },
          {
            nombre: 'Error Sexaje (%)',
            formula: '(Total Error Sexaje Hembras + Error Sexaje Machos) / Aves Inicio × 100'
          },
          {
            nombre: 'Mortalidad + Selección (%)',
            formula: 'Mortalidad % + Selección %'
          }
        ]
      },
      {
        titulo: '♀♂ Lectura por sexo',
        formulas: [
          {
            nombre: 'Todos los parámetros',
            formula: 'Cada bloque dice si es de HEMBRAS o de MACHOS. Se calculan con el saldo del propio sexo (aves al inicio de la semana de ese sexo), sin promediar con el otro.'
          },
          {
            nombre: 'Guión (—)',
            formula: 'El sexo no tiene aves en el lote, la semana no tuvo pesaje o la guía genética no trae ese dato para el sexo. No es un cero.'
          },
          {
            nombre: 'Uniformidad Guía',
            formula: 'La guía genética trae UNA sola columna de uniformidad (no la abre por sexo), así que la referencia es la misma para hembras y machos.'
          }
        ]
      },
      {
        titulo: '🌡️ Otros',
        formulas: [
          {
            nombre: 'Piso Térmico',
            formula: 'Guía clásica: según tabla. Guía Ecuador mixto (backend): true si el bloque semanal termina en día de vida ≤21 (≈ primeras 3 semanas).'
          },
          {
            nombre: 'Uniformidad tabla',
            formula: 'Guía Ecuador mixto no trae columna en la importación; el valor tabla se muestra 0 (compare uniformidad real del lote aparte).'
          }
        ]
      }
    ];
  }
}

import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import * as XLSX from 'xlsx';
import { SeguimientoLoteLevanteDto, SeguimientoLoteLevanteService, IndicadorSemanalLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';
import { GuiaGeneticaDto, GuiaGeneticaService } from '../../../../services/guia-genetica.service';

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
  conversionAlimenticia: number;
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
  
  // Control de peticiones secuenciales
  private cacheGuiaRango: Map<string, Map<number, GuiaGeneticaDto>> = new Map();
  private guiaRangoKeyActual: string | null = null;

  /** Origen de los valores "tabla" para comparativos: Ecuador mixto (prioritario) o guía clásica produccion_avicola. */
  fuenteGuiaIndicadores: 'ecuador-mixto' | 'clasica' | null = null;

  constructor(
    private guiaGeneticaService: GuiaGeneticaService,
    private seguimientoSvc: SeguimientoLoteLevanteService
  ) { }

  /** Descarga UN libro Excel con HOJAS SEPARADAS: "Seguimiento" (registros diarios) e "Indicadores". */
  descargarExcel(): void {
    const nombre = ((this.selectedLote as any)?.loteNombre || 'lote')
      .toString().trim().replace(/[\\/:*?"<>|]+/g, '-').slice(0, 100) || 'lote';
    const stamp = new Date().toISOString().slice(0, 10);
    const wb = XLSX.utils.book_new();

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
    if (seg.length) XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(seg), 'Seguimiento');

    const ind = (this.indicadoresSemanales || []).map((i: any) => ({
      Semana: i.semana,
      AvesInicio: i.avesInicioSemana,
      AvesFin: i.avesFinSemana,
      ConsumoDiaGrAve: i.consumoDiario,
      ConsumoGuiaGrAve: i.consumoTabla,
      PesoReal: i.pesoCierre,
      PesoGuia: i.pesoTabla,
      DifPesoPct: i.difPesoPct,
      GananciaSem: i.gananciaSemana,
      GananciaGuia: i.gananciaTabla,
      UnifReal: i.unifReal,
      UnifGuia: i.unifTabla,
      PorcMortSem: i.mortalidadSem,
      MortGuia: i.mortTabla,
      PorcSelSem: i.seleccionSem,
      PorcErrSexajeSem: i.errorSexajeSem,
      PorcRetiroSem: i.mortalidadMasSeleccion
    }));
    if (ind.length) XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(ind), 'Indicadores');

    if (!wb.SheetNames.length) return;
    XLSX.writeFile(wb, `levante-lote-${nombre}-seguimiento-indicadores-${stamp}.xlsx`);
  }

  ngOnInit(): void {
    this.calcularIndicadores().catch(error => {
      console.error('Error calculando indicadores:', error);
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      // Limpiar cache cuando cambie el lote
      if (changes['selectedLote']) {
        
        this.cacheGuiaRango.clear();
        this.guiaRangoKeyActual = null;
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
   * El front SOLO pinta: no recalcula desde los seguimientos crudos ni compara
   * contra la guía en el cliente. La función usa la guía Colombia correcta
   * (guia_genetica_sanmarino_colombia), no la Ecuador-mixto que el front usaba antes.
   *
   * Si no se puede resolver el loteId o el endpoint falla, cae al cálculo legacy
   * en cliente (fallback defensivo) para no romper la vista.
   */
  private async calcularIndicadores(): Promise<void> {
    if (!this.seguimientos || this.seguimientos.length === 0 || !this.selectedLote) {
      this.indicadoresSemanales = [];
      this.fuenteGuiaIndicadores = null;
      return;
    }

    const loteId = this.resolverLoteId();
    if (loteId != null) {
      try {
        const dto = await firstValueFrom(this.seguimientoSvc.getIndicadores(loteId));
        this.indicadoresSemanales = (dto || []).map(d => this.mapDtoAIndicador(d));
        this.fuenteGuiaIndicadores = 'clasica'; // guía Colombia real (BD)
        return;
      } catch (e) {
        console.warn('Indicadores desde BD no disponibles; se usa cálculo local (fallback):', e);
      }
    }

    // Fallback legacy (cálculo en cliente)
    const registrosPorSemana = this.agruparPorSemana(this.seguimientos);
    const semanas = Array.from(registrosPorSemana.keys());
    const minSemana = semanas.length ? Math.max(1, Math.min(...semanas)) : 1;
    const maxSemana = semanas.length ? Math.min(25, Math.max(...semanas)) : 25;
    await this.prefetchGuiaGeneticaRango(minSemana, maxSemana);
    this.indicadoresSemanales = await this.calcularIndicadoresSemanales(registrosPorSemana);
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
      conversionAlimenticia: d.conversionAlimenticia,
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

  private agruparPorSemana(registros: SeguimientoLoteLevanteDto[]): Map<number, SeguimientoLoteLevanteDto[]> {
    const grupos = new Map<number, SeguimientoLoteLevanteDto[]>();
    
    registros.forEach(registro => {
      const semana = this.calcularSemana(registro.fechaRegistro);
      // Levante normal: solo semanas 1..25 (no tiene sentido consultar guía fuera de rango)
      if (semana < 1 || semana > 25) return;
      if (!grupos.has(semana)) {
        grupos.set(semana, []);
      }
      grupos.get(semana)!.push(registro);
    });

    // Ordenar registros dentro de cada semana por fecha (calendario local, sin desfase UTC)
    grupos.forEach((registrosSem) => {
      registrosSem.sort((a, b) => {
        const ya = this.toYMD(a.fechaRegistro) ?? '';
        const yb = this.toYMD(b.fechaRegistro) ?? '';
        return ya.localeCompare(yb);
      });
    });

    return grupos;
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

  private async calcularIndicadoresSemanales(grupos: Map<number, SeguimientoLoteLevanteDto[]>): Promise<IndicadorSemanal[]> {
    const indicadores: IndicadorSemanal[] = [];
    const semanas = Array.from(grupos.keys()).sort((a, b) => a - b);
    
    let avesAcumuladas = this.selectedLote?.avesEncasetadas || 0;
    let mortalidadAcumulada = 0;
    let seleccionAcumulada = 0;
    let pesoAnterior = this.selectedLote?.pesoInicialH || 0;
    let pesoTablaAnterior = 0;

    
    
    for (let i = 0; i < semanas.length; i++) {
      const semana = semanas[i];
      const registros = grupos.get(semana) || [];
      
      
      
      const indicador = await this.calcularIndicadorSemana(
        semana,
        registros,
        avesAcumuladas,
        mortalidadAcumulada,
        seleccionAcumulada,
        pesoAnterior,
        pesoTablaAnterior
      );
      
      indicadores.push(indicador);
      
      // Actualizar acumulados para la siguiente semana
      avesAcumuladas = indicador.avesFinSemana;
      mortalidadAcumulada += indicador.mortalidadSem;
      seleccionAcumulada += indicador.seleccionSem;
      pesoAnterior = indicador.pesoCierre;
      pesoTablaAnterior = indicador.pesoTabla;
      
      
    }

    
    return indicadores;
  }

  private async calcularIndicadorSemana(
    semana: number, 
    registros: SeguimientoLoteLevanteDto[], 
    avesInicio: number,
    mortalidadAcum: number,
    seleccionAcum: number,
    pesoAnterior: number,
    pesoTablaAnterior: number
  ): Promise<IndicadorSemanal> {
    // Calcular totales de la semana
    const mortalidadTotal = registros.reduce((sum, r) => sum + (r.mortalidadHembras || 0) + (r.mortalidadMachos || 0), 0);
    const seleccionTotal = registros.reduce((sum, r) => sum + (r.selH || 0) + (r.selM || 0), 0);
    const consumoTotal = registros.reduce((sum, r) => sum + (r.consumoKgHembras || 0) + (r.consumoKgMachos || 0), 0);
    
    // Aves al final de la semana
    const avesFin = avesInicio - mortalidadTotal - seleccionTotal;
    
    // Peso/uniformidad del PESAJE de la semana: el pesaje se registra 1 vez por semana (no todos
    // los días), por lo que el ÚLTIMO día suele venir en 0. Se busca el último registro de la
    // semana que tenga peso > 0 y se promedian solo los sexos con dato (evita peso=0 y ganancia
    // negativa cuando el pesaje no cae el último día).
    const ultimoRegistro = registros[registros.length - 1];
    const regPesaje = [...registros].reverse().find(r => (r.pesoPromH || 0) > 0 || (r.pesoPromM || 0) > 0) || ultimoRegistro;
    const _pH = regPesaje?.pesoPromH || 0;
    const _pM = regPesaje?.pesoPromM || 0;
    let pesoPromedio = (_pH > 0 && _pM > 0) ? (_pH + _pM) / 2 : (_pH > 0 ? _pH : _pM);
    // Semana sin pesaje (hueco de datos): arrastrar el último peso conocido para no mostrar 0
    // (evita ganancia negativa y diferencia -100% engañosas). Estándar en pesajes semanales.
    if (pesoPromedio <= 0) pesoPromedio = pesoAnterior || 0;
    const _uH = regPesaje?.uniformidadH || 0;
    const _uM = regPesaje?.uniformidadM || 0;
    const unifReal = (_uH > 0 && _uM > 0) ? (_uH + _uM) / 2 : (_uH > 0 ? _uH : _uM);
    
    // 🔧 MEJORA: Consumo real en gramos (convertir de kg a gramos)
    const consumoTotalGramos = consumoTotal * 1000;
    
    // 🔧 MEJORA: Consumo por ave (más preciso)
    const avesPromedio = (avesInicio + avesFin) / 2;
    const diasConRegistro = registros.length; // Número de días con registro en la semana
    
    // 🔧 NUEVO: Consumo diario por ave (g/ave/día) - para comparar con tabla
    const consumoDiarioPorAve = (avesPromedio > 0 && diasConRegistro > 0) 
      ? consumoTotalGramos / (avesPromedio * diasConRegistro) 
      : 0;
    
    // Guía genética (levante normal: hasta semana 25, sin indicadores de huevos)
    const guia = await this.obtenerGuiaSemana(semana);

    // 🔧 MEJORA: Consumo tabla (valor de referencia de guía genética) - ya viene en g/ave/día
    const consumoTablaPorAve = guia
      ? (guia.consumoHembras + guia.consumoMachos) / 2
      : await this.obtenerConsumoTabla(semana);
    
    // 🔧 MEJORA: Conversión alimenticia (FCR) - Consumo total por ave / Ganancia por ave
    const gananciaSemana = pesoPromedio - pesoAnterior;
    const consumoTotalPorAve = avesPromedio > 0 ? consumoTotalGramos / avesPromedio : 0;
    const conversionAlimenticia = gananciaSemana > 0 ? consumoTotalPorAve / gananciaSemana : 0;
    
    // 🔧 MEJORA: Ganancia diaria acumulada
    const gananciaDiariaAcumulada = gananciaSemana / 7;

    // Tabla: peso esperado y ganancia esperada
    const pesoTabla = guia ? (guia.pesoHembras + guia.pesoMachos) / 2 : 0;
    const unifTabla = guia ? (guia.uniformidad ?? 0) : 0;
    const mortTabla = guia ? (guia.mortalidadHembras + guia.mortalidadMachos) / 2 : 0;
    const gananciaTabla = (pesoTabla > 0 && pesoTablaAnterior > 0) ? (pesoTabla - pesoTablaAnterior) : 0;
    
    // Error de sexaje total de la semana
    const errorSexajeTotal = registros.reduce((sum, r) => sum + (r.errorSexajeHembras || 0) + (r.errorSexajeMachos || 0), 0);
    
    // Porcentajes
    const mortalidadSem = avesInicio > 0 ? (mortalidadTotal / avesInicio) * 100 : 0;
    const seleccionSem = avesInicio > 0 ? (seleccionTotal / avesInicio) * 100 : 0;
    const errorSexajeSem = avesInicio > 0 ? (errorSexajeTotal / avesInicio) * 100 : 0;
    const mortalidadMasSeleccion = mortalidadSem + seleccionSem;
    
    // 🔧 MEJORA: Eficiencia (Ganancia por ave / Consumo total por ave)
    const eficiencia = consumoTotalPorAve > 0 ? gananciaSemana / consumoTotalPorAve : 0;
    
    // 🔧 MEJORA: IP (Índice de Productividad) - Combinación de eficiencia y supervivencia
    const supervivencia = avesInicio > 0 ? avesFin / avesInicio : 0;
    const ip = eficiencia * supervivencia;
    
    // 🔧 MEJORA: VPI (Índice de Vitalidad) - Relación entre supervivencia y rendimiento
    const vpi = supervivencia * eficiencia;
    
    // Saldo de aves semanal
    const saldoAvesSemanal = avesFin;
    
    // Acumulados
    const mortalidadAcumTotal = mortalidadAcum + mortalidadSem;
    const seleccionAcumTotal = seleccionAcum + seleccionSem;
    const mortalidadMasSeleccionAcumTotal = mortalidadAcumTotal + seleccionAcumTotal;
    
    // 🔧 MEJORA: Piso térmico desde guía genética
    const pisoTermicoVisible = guia ? !!guia.pisoTermicoRequerido : await this.obtenerPisoTermico(semana);
    
    // Información del lote (del selectedLote)
    const region = this.selectedLote?.regional || null;
    const granja = this.selectedLote?.farm?.name || null;
    const nave = this.selectedLote?.nucleo?.nucleoNombre || null;
    const sublote = this.extraerSublote(this.selectedLote?.loteNombre || '');
    
    // Observaciones del último registro de la semana
    const observaciones = ultimoRegistro?.observaciones || null;

    // Diferencias vs tabla
    const difPesoPct = pesoTabla > 0 ? ((pesoPromedio - pesoTabla) / pesoTabla) * 100 : 0;

    // Nota: pesoTablaAnterior / gananciaTabla se setean en el loop principal (semanal)
    return {
      semana,
      fechaInicio: this.obtenerFechaInicioSemana(semana),
      fechaFin: this.obtenerFechaFinSemana(semana),
      region,
      granja,
      nave,
      sublote,
      avesInicioSemana: avesInicio,
      avesFinSemana: avesFin,
      consumoDiario: consumoDiarioPorAve, // 🔧 NUEVO: Consumo diario por ave (g/ave/día)
      consumoTabla: consumoTablaPorAve, // Consumo esperado de tabla (g/ave/día)
      consumoTotalSemana: consumoTotalGramos, // 🔧 NUEVO: Consumo total de la semana en gramos
      conversionAlimenticia,
      pesoTabla,
      unifReal,
      unifTabla,
      mortTabla,
      difPesoPct,
      gananciaSemana,
      gananciaDiariaAcumulada,
      gananciaTabla,
      mortalidadSem,
      seleccionSem,
      errorSexajeSem, // 🔧 NUEVO: Error de sexaje semanal (%)
      mortalidadMasSeleccion,
      eficiencia,
      ip,
      vpi,
      saldoAvesSemanal,
      mortalidadAcum: mortalidadAcumTotal,
      seleccionAcum: seleccionAcumTotal,
      mortalidadMasSeleccionAcum: mortalidadMasSeleccionAcumTotal,
      pisoTermicoVisible,
      pesoInicial: pesoAnterior,
      pesoCierre: pesoPromedio,
      pesoAnterior,
      pesoTablaAnterior,
      observaciones
    };
  }

  // ================== VALIDACIÓN Y DEBUGGING ==================
  
  /**
   * Valida que el sistema esté usando correctamente la tabla genética
   */
  async validarUsoTablaGenetica(): Promise<void> {
    if (!this.selectedLote?.raza || !this.selectedLote?.anoTablaGenetica) {
      console.warn('⚠️ No se puede validar: Lote sin raza o año tabla genética');
      return;
    }

    
    
    
    
    

    let semanasValidadas = 0;
    let semanasConErrores = 0;
    let consumoPromedioDiferencia = 0;
    let pisoTermicoCorrecto = 0;

    // Validar cada semana
    for (const indicador of this.indicadoresSemanales) {
      const resultado = await this.validarSemanaIndicador(indicador);
      semanasValidadas++;
      
      if (resultado.tieneErrores) {
        semanasConErrores++;
      }
      
      consumoPromedioDiferencia += resultado.diferenciaConsumo;
      if (resultado.pisoTermicoCorrecto) {
        pisoTermicoCorrecto++;
      }
    }

    // Mostrar resumen
    
    
    
    
    
    
    if (semanasConErrores === 0) {
      
    } else if (semanasConErrores <= semanasValidadas * 0.2) {
      
    } else {
      
    }

    
  }

  /**
   * Valida los datos de una semana específica
   */
  private async validarSemanaIndicador(indicador: IndicadorSemanal): Promise<{
    tieneErrores: boolean;
    diferenciaConsumo: number;
    pisoTermicoCorrecto: boolean;
  }> {
    const semana = indicador.semana;
    

    let tieneErrores = false;
    let diferenciaConsumo = 0;
    let pisoTermicoCorrecto = false;

    try {
      // Obtener datos reales de la guía genética
      const datosGuia = await this.obtenerDatosCompletosGuia(semana);
      
      if (datosGuia) {
        
        
        
        
        
        
        
        
        

        // Validar consumo calculado vs tabla
        const consumoPromedioTabla = (datosGuia.consumoHembras + datosGuia.consumoMachos) / 2;
        diferenciaConsumo = Math.abs(indicador.consumoDiario - consumoPromedioTabla);
        const porcentajeDiferencia = (diferenciaConsumo / consumoPromedioTabla) * 100;

        
        
        
        

        if (porcentajeDiferencia <= 1) {
          
        } else if (porcentajeDiferencia <= 5) {
          
        } else if (porcentajeDiferencia <= 15) {
          
        } else {
          
          tieneErrores = true;
        }

        // Validar piso térmico
        
        
        
        
        pisoTermicoCorrecto = indicador.pisoTermicoVisible === datosGuia.pisoTermicoRequerido;
        
        if (pisoTermicoCorrecto) {
          
        } else {
          
          tieneErrores = true;
        }

        // Validar indicadores calculados
        
        
        
        
        
        
        
        

      } else {
        
        
        tieneErrores = true;
      }

    } catch (error) {
      console.error(`❌ Error validando semana ${semana}:`, error);
      tieneErrores = true;
    }

    return {
      tieneErrores,
      diferenciaConsumo,
      pisoTermicoCorrecto
    };
  }

  /**
   * Obtiene datos completos de la guía genética para una semana
   */
  private async obtenerDatosCompletosGuia(semana: number): Promise<any> {
    return await this.obtenerGuiaSemana(semana);
  }

  private async obtenerGuiaSemana(semana: number): Promise<GuiaGeneticaDto | null> {
    // Levante normal: semanas 1..25 (sin registro de huevos)
    if (semana < 1 || semana > 25) return null;

    if (!this.selectedLote?.raza || !this.selectedLote?.anoTablaGenetica) {
      return null;
    }

    const raza = this.selectedLote.raza;
    const ano = this.selectedLote.anoTablaGenetica;
    const key = `${raza}-${ano}`;
    const map = this.cacheGuiaRango.get(key);
    if (!map) return null;
    return map.get(semana) ?? null;
  }

  private async prefetchGuiaGeneticaRango(desde: number, hasta: number): Promise<void> {
    // Solo aplica a levante normal
    const desdeClamped = Math.max(1, Math.min(25, desde));
    const hastaClamped = Math.max(1, Math.min(25, hasta));
    if (hastaClamped < desdeClamped) return;

    if (!this.selectedLote?.raza || !this.selectedLote?.anoTablaGenetica) {
      this.fuenteGuiaIndicadores = null;
      return;
    }

    const raza = this.selectedLote.raza;
    const ano = this.selectedLote.anoTablaGenetica;
    const key = `${raza}-${ano}`;

    // Evitar pedir varias veces si ya tenemos el rango para ese lote
    if (this.guiaRangoKeyActual === key && this.cacheGuiaRango.has(key)) return;

    const map = new Map<number, GuiaGeneticaDto>();

    // 1) Prioridad: Guía genética Ecuador — curva mixto agregada por semanas de 7 días
    try {
      const rowsEc = await firstValueFrom(
        this.guiaGeneticaService.obtenerGuiaGeneticaRangoEcuadorMixto(raza, ano, desdeClamped, hastaClamped)
      );
      if (rowsEc && rowsEc.length > 0) {
        for (const r of rowsEc) {
          if (typeof r?.edad === 'number') map.set(r.edad, r);
        }
        this.cacheGuiaRango.set(key, map);
        this.guiaRangoKeyActual = key;
        this.fuenteGuiaIndicadores = 'ecuador-mixto';
      
        return;
      }
    } catch (e) {
      console.warn('Guía Ecuador mixto no disponible, se intentará guía clásica:', e);
    }

    // 2) Fallback: produccion_avicola_raw vía /guia-genetica/rango
    try {
      const rows = await firstValueFrom(
        this.guiaGeneticaService.obtenerGuiaGeneticaRango(raza, ano, desdeClamped, hastaClamped)
      );
      for (const r of rows || []) {
        if (typeof r?.edad === 'number') map.set(r.edad, r);
      }
      this.fuenteGuiaIndicadores = map.size > 0 ? 'clasica' : null;
      this.cacheGuiaRango.set(key, map);
      this.guiaRangoKeyActual = key;
      
    } catch (error) {
      console.error('❌ Error precargando guía genética por rango:', error);
      this.fuenteGuiaIndicadores = null;
      this.cacheGuiaRango.set(key, new Map());
      this.guiaRangoKeyActual = key;
    }
  }

  // ================== HELPERS DE CONSUMO TABLA ==================
  private async obtenerConsumoTabla(semana: number): Promise<number> {
    if (semana < 1 || semana > 25) {
      // Fuera de levante: no consultar guía genética
      return this.obtenerConsumoPorDefecto(Math.min(Math.max(semana, 1), 16));
    }

    const guia = await this.obtenerGuiaSemana(semana);
    if (guia) {
      return (guia.consumoHembras + guia.consumoMachos) / 2;
    }

    // fallback
    if (!this.selectedLote?.raza || !this.selectedLote?.anoTablaGenetica) {
      return this.obtenerConsumoPorDefecto(semana);
    }

    try {
      const consumoEsperado = await this.guiaGeneticaService.obtenerConsumoEsperado(
        this.selectedLote.raza,
        this.selectedLote.anoTablaGenetica,
        semana
      );
      
      return consumoEsperado > 0 ? consumoEsperado : this.obtenerConsumoPorDefecto(semana);
    } catch (error) {
      console.error(`❌ Error obteniendo consumo de guía genética para semana ${semana}:`, error);
      return this.obtenerConsumoPorDefecto(semana);
    }
  }

  private obtenerConsumoPorDefecto(semana: number): number {
    // Tabla de consumo por defecto (fallback)
    const tablaConsumo: { [key: number]: number } = {
      1: 15,   // Semana 1: 15g/ave/día
      2: 25,   // Semana 2: 25g/ave/día
      3: 35,   // Semana 3: 35g/ave/día
      4: 45,   // Semana 4: 45g/ave/día
      5: 55,   // Semana 5: 55g/ave/día
      6: 65,   // Semana 6: 65g/ave/día
      7: 75,   // Semana 7: 75g/ave/día
      8: 85,   // Semana 8: 85g/ave/día
      9: 95,   // Semana 9: 95g/ave/día
      10: 105, // Semana 10: 105g/ave/día
      11: 115, // Semana 11: 115g/ave/día
      12: 125, // Semana 12: 125g/ave/día
      13: 135, // Semana 13: 135g/ave/día
      14: 145, // Semana 14: 145g/ave/día
      15: 155, // Semana 15: 155g/ave/día
      16: 165, // Semana 16: 165g/ave/día
    };
    
    return tablaConsumo[semana] || 157; // Valor por defecto si no está en la tabla
  }

  private async obtenerPisoTermico(semana: number): Promise<boolean> {
    if (semana < 1 || semana > 25) {
      // Fuera de levante: no consultar guía genética
      return false;
    }

    const guia = await this.obtenerGuiaSemana(semana);
    if (guia) return !!guia.pisoTermicoRequerido;

    // fallback
    if (!this.selectedLote?.raza || !this.selectedLote?.anoTablaGenetica) {
      return semana <= 3; // Valor por defecto
    }

    try {
      const requierePisoTermico = await this.guiaGeneticaService.requierePisoTermico(
        this.selectedLote.raza,
        this.selectedLote.anoTablaGenetica,
        semana
      );

      return requierePisoTermico;
    } catch (error) {
      console.error(`❌ Error obteniendo piso térmico para semana ${semana}:`, error);
      return semana <= 3; // Valor por defecto
    }
  }

  // ================== VALIDACIONES Y ALERTAS ==================
  validarConsumo(consumoDiario: number, consumoTabla: number): { 
    esValido: boolean; 
    mensaje: string; 
    tipo: 'success' | 'warning' | 'error' 
  } {
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

  validarMortalidad(mortalidadSemana: number, semana: number, mortalidadTabla?: number): { 
    esValido: boolean; 
    mensaje: string; 
    tipo: 'success' | 'warning' | 'error' 
  } {
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
          },
          {
            nombre: 'Conversión Alimenticia (FCR)',
            formula: '(Consumo Total por Ave en g) / (Ganancia de Peso por Ave en g)'
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
        titulo: '⚡ Indicadores de Rendimiento',
        formulas: [
          {
            nombre: 'Eficiencia',
            formula: 'Ganancia de Peso por Ave (g) / Consumo Total por Ave (g)'
          },
          {
            nombre: 'IP (Índice de Productividad)',
            formula: 'Eficiencia × Supervivencia\nDonde: Supervivencia = Aves Fin / Aves Inicio'
          },
          {
            nombre: 'VPI (Índice de Vitalidad)',
            formula: 'Supervivencia × Eficiencia\nDonde: Supervivencia = Aves Fin / Aves Inicio'
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

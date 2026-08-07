import { Component, Input, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto, ProduccionService, IndicadorProduccionSemanalDto, IndicadoresProduccionResponse, IndicadoresProduccionRequest, InformacionLoteDto } from '../../services/produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { finalize } from 'rxjs/operators';
import { exportarObjetosMultiHojaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { causaEmptyStateIndicadores } from '../../funciones/empty-state-causa-indicadores.funcion';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { ClasificacionHuevoSemanaAgrupada } from '../../models/huevo-clasificacion.model';
import { agruparClasificacionHuevoPorSemana } from '../../funciones/agrupar-clasificacion-huevo-semana.funcion';

@Component({
  selector: 'app-tabla-lista-indicadores',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-lista-indicadores.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabla-lista-indicadores.component.scss']
})
export class TablaListaIndicadoresComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  /** ID del lote en fase Producción (mismo que listado y modal seguimiento diario). Flujo legacy. */
  @Input() produccionLoteId: number | null = null;
  /** ID del lote postura producción (flujo LPP). Si viene, se prioriza sobre produccionLoteId. */
  @Input() lotePosturaProduccionId: number | null = null;
  /** REQ-000c: información general del lote (fechaEncaset) para inferir la causa del empty-state. */
  @Input() informacionLote: InformacionLoteDto | null = null;
  @Input() loading: boolean = false;

  indicadoresSemanales: IndicadorProduccionSemanalDto[] = [];
  loadingIndicadores = false;
  tieneDatosGuiaGenetica = false;
  mensajeGuiaGenetica: string | null = null;
  error: string | null = null;
  expanded = new Set<number>(); // semanas expandidas para detalle clasificadora

  // ===== Clasificación de huevos por ÍTEMS (flag de empresa · Santa Reyes) =====
  /**
   * Flag `companies.clasificacion_huevo_por_items`: la empresa clasifica por ítem del catálogo
   * (Primera/Pnc) y las 11 columnas fijas quedan en 0 → el detalle expandible las reemplaza.
   * FAIL-CLOSED: si el GET de flags falla, sigue el detalle clásico de 11 columnas.
   */
  clasificacionHuevoPorItems = false;
  cargandoClasificacion = false;
  /** Error del endpoint de clasificación → línea discreta en el detalle (sin toast). */
  errorClasificacion = false;
  /** semana → desglose agrupado. MEMOIZADO: se recalcula solo al responder el endpoint. */
  private clasificacionPorSemana = new Map<number, ClasificacionHuevoSemanaAgrupada>();

  constructor(
    private produccionService: ProduccionService,
    private companyConfig: ActiveCompanyConfigService
  ) { }

  ngOnInit(): void {
    this.cargarIndicadores();
    this.cargarFlagsEmpresa();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedLote'] || changes['seguimientos'] || changes['produccionLoteId'] || changes['lotePosturaProduccionId']) {
      this.cargarIndicadores();
      if (this.clasificacionHuevoPorItems) this.cargarClasificacionHuevoItems();
    }
  }

  /**
   * Flags de la empresa activa (emite una vez y completa; usa caché por empresa).
   * El flag llega async: si ya hay lote seleccionado, dispara la carga del desglose por ítem.
   */
  private cargarFlagsEmpresa(): void {
    this.companyConfig.getFlags().subscribe(flags => {
      if (this.clasificacionHuevoPorItems === flags.clasificacionHuevoPorItems) return;
      this.clasificacionHuevoPorItems = flags.clasificacionHuevoPorItems;
      if (this.clasificacionHuevoPorItems) this.cargarClasificacionHuevoItems();
    });
  }

  /**
   * Request de indicadores (flujo LPP prioritario, legacy como fallback).
   * `null` cuando no hay lote resoluble. Misma firma que consume el endpoint hermano
   * `clasificacion-huevo-items`.
   */
  private construirRequestIndicadores(): IndicadoresProduccionRequest | null {
    // Flujo LPP: priorizar lotePosturaProduccionId. Flujo legacy: produccionLoteId o selectedLote.loteId
    const lppId = this.lotePosturaProduccionId ?? null;
    const loteIdLegacy = (this.produccionLoteId && this.produccionLoteId > 0) ? this.produccionLoteId : (this.selectedLote?.loteId ?? null);
    const hasLpp = lppId != null && lppId > 0;
    const hasLegacy = loteIdLegacy != null && loteIdLegacy > 0;
    if (!hasLpp && !hasLegacy) return null;

    // Request al backend: LPP o legacy (misma fuente que el tab Seguimiento).
    // semanaDesde: 26 = inicio de producción (semana de vida).
    return {
      loteId: hasLegacy ? loteIdLegacy! : 0,
      lotePosturaProduccionId: hasLpp ? lppId! : null,
      semanaDesde: 26,
      semanaHasta: null,
      fechaDesde: null,
      fechaHasta: null
    };
  }

  /**
   * Santa Reyes: desglose por ítem (Primera/Pnc) de TODAS las semanas del lote en UNA sola
   * petición; se agrupa en memoria por semana para que el template lea referencias estables.
   */
  private cargarClasificacionHuevoItems(): void {
    const request = this.construirRequestIndicadores();
    if (!request) {
      this.clasificacionPorSemana = new Map<number, ClasificacionHuevoSemanaAgrupada>();
      this.errorClasificacion = false;
      return;
    }

    this.cargandoClasificacion = true;
    this.errorClasificacion = false;

    this.produccionService.obtenerClasificacionHuevoItems(request)
      .pipe(finalize(() => { this.cargandoClasificacion = false; }))
      .subscribe({
        next: (filas) => {
          this.clasificacionPorSemana = agruparClasificacionHuevoPorSemana(filas);
          this.errorClasificacion = false;
        },
        error: () => {
          // Sin toast: el detalle muestra una línea discreta. No bloquea los indicadores.
          this.clasificacionPorSemana = new Map<number, ClasificacionHuevoSemanaAgrupada>();
          this.errorClasificacion = true;
        }
      });
  }

  /** Desglose por ítem de una semana (referencia ESTABLE del Map memoizado; null si no hay datos). */
  getClasificacionSemana(semana: number): ClasificacionHuevoSemanaAgrupada | null {
    return this.clasificacionPorSemana.get(semana) ?? null;
  }

  cargarIndicadores(): void {
    const request = this.construirRequestIndicadores();
    if (!request) {
      this.indicadoresSemanales = [];
      this.error = null;
      return;
    }

    this.loadingIndicadores = true;
    this.error = null;

    // Llamar al servicio que invoca el API
    // El backend procesará:
    // 1. Buscar todos los seguimientos de producción_diaria para este lote
    // 2. Agruparlos por semana
    // 3. Calcular métricas por semana
    // 4. Comparar con guía genética si está disponible
    // 5. Retornar todos los indicadores calculados
    this.produccionService.obtenerIndicadoresSemanales(request)
      .pipe(
        finalize(() => {
          this.loadingIndicadores = false;
        })
      )
      .subscribe({
        next: (response: IndicadoresProduccionResponse) => {
          // El backend ya calculó todos los indicadores semanales
          // Solo los asignamos para mostrar en la tabla
          this.indicadoresSemanales = response.indicadores || [];
          this.tieneDatosGuiaGenetica = response.tieneDatosGuiaGenetica || false;
          this.mensajeGuiaGenetica = response.mensajeGuiaGenetica ?? null;
          this.error = null;

          
        },
        error: (err) => {
          console.error('❌ Error al cargar indicadores semanales:', err);
          this.error = err.error?.message || 'Error al cargar indicadores semanales. Por favor, intenta de nuevo.';
          this.indicadoresSemanales = [];
          this.tieneDatosGuiaGenetica = false;
          this.mensajeGuiaGenetica = null;
        }
      });
  }

  /**
   * Descarga UN libro Excel con los datos de los tabs en HOJAS SEPARADAS:
   * "Seguimiento" (registros diarios) e "Indicadores" (métricas semanales vs guía).
   */
  descargarIndicadoresExcel(): void {
    const loteNombre = (this.selectedLote?.loteNombre || `Lote_${this.selectedLote?.loteId ?? ''}`)
      .trim()
      .replace(/[\\/:*?"<>|]+/g, '-')
      .replace(/\s+/g, ' ')
      .slice(0, 120) || 'lote';

    const maxSemana = this.indicadoresSemanales.length
      ? Math.max(...this.indicadoresSemanales.map(x => x.semana || 0))
      : 0;
    const stamp = new Date().toISOString().slice(0, 10);
    const filename = `produccion-lote-${loteNombre}-seguimiento-indicadores-semana-${maxSemana || 'NA'}-${stamp}.xlsx`;

    const segRows = this.buildSeguimientoRows();   // Hoja 1: Seguimiento (registros diarios)
    const indRows = this.buildIndicadoresRows();   // Hoja 2: Indicadores (métricas semanales vs guía)
    exportarObjetosMultiHojaExcel([
      ...(segRows.length ? [{ sheetName: 'Seguimiento', rows: segRows }] : []),
      ...(indRows.length ? [{ sheetName: 'Indicadores', rows: indRows }] : []),
    ], { filenameFull: filename });
  }

  /** Filas de la hoja "Seguimiento" (registros diarios de producción). */
  private buildSeguimientoRows(): any[] {
    return (this.seguimientos || []).map(s => ({
      Id: s.id,
      Fecha: this.formatearFecha(s.fechaRegistro),
      Etapa: s.etapa,
      MortalidadH: s.mortalidadH,
      MortalidadM: s.mortalidadM,
      SeleccionH: s.selH,
      SeleccionM: s.selM,
      ConsumoKgH: s.consKgH,
      ConsumoKgM: s.consKgM,
      TipoAlimento: s.tipoAlimento,
      HuevosTotales: s.huevosTotales,
      HuevosIncubables: s.huevosIncubables,
      HuevoLimpio: (s as any).huevoLimpio ?? 0,
      HuevoTratado: (s as any).huevoTratado ?? 0,
      HuevoSucio: (s as any).huevoSucio ?? 0,
      HuevoDeforme: (s as any).huevoDeforme ?? 0,
      HuevoBlanco: (s as any).huevoBlanco ?? 0,
      HuevoDobleYema: (s as any).huevoDobleYema ?? 0,
      HuevoPiso: (s as any).huevoPiso ?? 0,
      HuevoPequeno: (s as any).huevoPequeno ?? 0,
      HuevoRoto: (s as any).huevoRoto ?? 0,
      HuevoDesecho: (s as any).huevoDesecho ?? 0,
      HuevoOtro: (s as any).huevoOtro ?? 0,
      PesoHuevo: s.pesoHuevo,
      PesoH: (s as any).pesoH ?? null,
      PesoM: (s as any).pesoM ?? null,
      Uniformidad: (s as any).uniformidad ?? null,
      CoeficienteVariacion: (s as any).coeficienteVariacion ?? null,
      Observaciones: s.observaciones ?? null
    }));
  }

  /** Redondea a 2 decimales los valores numéricos de una fila (evita ruido de punto flotante en el Excel). */
  private redondearFila(row: Record<string, any>): Record<string, any> {
    const out: Record<string, any> = {};
    for (const k in row) {
      const v = row[k];
      out[k] = (typeof v === 'number' && Number.isFinite(v) && !Number.isInteger(v))
        ? Number(v.toFixed(2))
        : v;
    }
    return out;
  }

  /**
   * ¿La guía trae uniformidad para esta semana? La guía genética normalmente NO define uniformidad
   * para las edades de PRODUCCIÓN (solo la trae en las de levante), así que lo habitual es que
   * `uniformidadGuia` llegue null y la celda muestre «—».
   *
   * El chequeo contra 0 se conserva a propósito: `fn_indicadores_produccion_postura` emitía ese
   * campo como 0 en vez de NULL (paridad histórica con un `ParseDouble` viejo del C#) hasta la
   * migración `20260807140000_UniformidadGuiaProduccionNull`. Pintar ese 0 se lee como «la guía
   * exige 0 % de uniformidad», que es falso: es «sin dato». Un 0 real tampoco existe como
   * objetivo, así que tratarlo como ausencia sigue siendo correcto y cubre backends sin la migración.
   */
  hayGuiaUniformidad(ind: IndicadorProduccionSemanalDto): boolean {
    const g = ind?.uniformidadGuia;
    return g !== null && g !== undefined && Number(g) !== 0;
  }

  /** Filas de la hoja "Indicadores" (métricas semanales + comparación con guía). */
  private buildIndicadoresRows(): any[] {
    return (this.indicadoresSemanales || []).map(ind => this.redondearFila({
      Semana: ind.semana,
      FechaInicio: this.formatearFecha(ind.fechaInicioSemana),
      FechaFin: this.formatearFecha(ind.fechaFinSemana),
      Registros: ind.totalRegistros,

      MortalidadH: ind.mortalidadHembras,
      MortalidadM: ind.mortalidadMachos,
      PorcMortH: ind.porcentajeMortalidadHembras,
      PorcMortM: ind.porcentajeMortalidadMachos,
      GuiaMortH: ind.mortalidadGuiaHembras,
      GuiaMortM: ind.mortalidadGuiaMachos,
      DifMortH: ind.diferenciaMortalidadHembras,
      DifMortM: ind.diferenciaMortalidadMachos,

      SeleccionH: ind.seleccionHembras,
      PorcSelH: ind.porcentajeSeleccionHembras,
      SeleccionM: ind.seleccionMachos,
      PorcSelM: ind.porcentajeSeleccionMachos,

      RetiroSemH: ind.retiroSemanalHembrasReal,
      RetiroSemM: ind.retiroSemanalMachosReal,
      RetiroAcumH: ind.retiroAcumuladoHembrasReal,
      RetiroAcumHGuia: ind.retiroAcumuladoHembrasGuia,
      DifRetiroAcumH: this.diferenciaRetiroAcumuladoHembras(ind),
      RetiroAcumM: ind.retiroAcumuladoMachosReal,
      RetiroAcumMGuia: ind.retiroAcumuladoMachosGuia,
      DifRetiroAcumM: this.diferenciaRetiroAcumuladoMachos(ind),

      ConsumoKgH: ind.consumoKgHembras,
      ConsumoKgM: ind.consumoKgMachos,
      ConsumoKgTotal: ind.consumoTotalKg,
      ConsumoPromDiaKg: ind.consumoPromedioDiarioKg,
      ConsRealGrAveDiaH: this.consumoRealGrAveDiaH(ind),
      ConsGuiaGrAveDiaH: ind.consumoGuiaHembras,
      DifConsH: ind.diferenciaConsumoHembras,
      ConsRealGrAveDiaM: this.consumoRealGrAveDiaM(ind),
      ConsGuiaGrAveDiaM: ind.consumoGuiaMachos,
      DifConsM: ind.diferenciaConsumoMachos,

      HuevosTot: ind.huevosTotales,
      HuevosTotGuia: ind.huevosTotalesGuia,
      DifHuevosTot: ind.diferenciaHuevosTotales,
      HuevosInc: ind.huevosIncubables,
      HuevosIncGuia: ind.huevosIncubablesGuia,
      DifHuevosInc: ind.diferenciaHuevosIncubables,
      PromHuevosDia: ind.promedioHuevosPorDia,
      PorcProdReal: ind.eficienciaProduccion,
      PorcProdGuia: ind.porcentajeProduccionGuia,
      DifPorcProd: ind.diferenciaPorcentajeProduccion,
      PesoHuevoProm: ind.pesoHuevoPromedio,
      PesoHuevoGuia: ind.pesoHuevoGuia,
      DifPesoHuevo: ind.diferenciaPesoHuevo,
      HTAA: ind.htaaReal,
      HIAA: ind.hiaaReal,

      PesoH: ind.pesoPromedioHembras,
      PesoHGuia: ind.pesoGuiaHembras,
      DifPesoH: ind.diferenciaPesoHembras,
      PesoM: ind.pesoPromedioMachos,
      PesoMGuia: ind.pesoGuiaMachos,
      DifPesoM: ind.diferenciaPesoMachos,
      UnifReal: ind.uniformidadPromedio,
      UnifGuia: ind.uniformidadGuia,
      DifUnif: ind.diferenciaUniformidad,
      CvProm: ind.coeficienteVariacionPromedio,

      HIni: ind.avesHembrasInicioSemana,
      MIni: ind.avesMachosInicioSemana,
      HFin: ind.avesHembrasFinSemana,
      MFin: ind.avesMachosFinSemana,

      HuevoLimpio: ind.huevosLimpios,
      HuevoTratado: ind.huevosTratados,
      HuevoSucio: ind.huevosSucios,
      HuevoDeforme: ind.huevosDeformes,
      HuevoBlanco: ind.huevosBlancos,
      HuevoDobleYema: ind.huevosDobleYema,
      HuevoPiso: ind.huevosPiso,
      HuevoPequeno: ind.huevosPequenos,
      HuevoRoto: ind.huevosRotos,
      HuevoDesecho: ind.huevosDesecho,
      HuevoOtro: ind.huevosOtro
    }));
  }

  // ================== HELPERS ==================

  formatearPorcentaje(valor?: number | null): string {
    if (valor === null || valor === undefined) return '—';
    const signo = valor > 0 ? '+' : '';
    return `${signo}${valor.toFixed(2)}%`;
  }

  getDiferenciaClass(diferencia?: number | null): string {
    if (diferencia === null || diferencia === undefined) return '';
    const abs = Math.abs(diferencia);
    if (abs <= 5) return 'diferencia-ok';
    if (abs <= 15) return 'diferencia-warning';
    return 'diferencia-danger';
  }

  getEstadoCumplimiento(diferencia?: number | null): { clase: string, texto: string } {
    if (diferencia === null || diferencia === undefined) {
      return { clase: 'estado-info', texto: 'Sin datos' };
    }
    const absDiferencia = Math.abs(diferencia);
    if (absDiferencia <= 5) {
      return { clase: 'estado-ok', texto: 'Óptimo' };
    } else if (absDiferencia <= 15) {
      return { clase: 'estado-aceptable', texto: 'Aceptable' };
    } else {
      return { clase: 'estado-problema', texto: 'Requiere atención' };
    }
  }

  toggleExpanded(semana: number): void {
    if (this.expanded.has(semana)) this.expanded.delete(semana);
    else this.expanded.add(semana);
  }

  isExpanded(semana: number): boolean {
    return this.expanded.has(semana);
  }

  // Consumo real g/ave/día (para mostrar siempre, con fallback seguro)
  consumoRealGrAveDiaH(ind: IndicadorProduccionSemanalDto): number | null {
    const aves = ind.avesHembrasInicioSemana || 0;
    const dias = ind.totalRegistros || 0;
    if (!aves || !dias) return null;
    return Number((ind.consumoKgHembras * 1000) / (dias * aves));
  }

  consumoRealGrAveDiaM(ind: IndicadorProduccionSemanalDto): number | null {
    const aves = ind.avesMachosInicioSemana || 0;
    const dias = ind.totalRegistros || 0;
    if (!aves || !dias) return null;
    return Number((ind.consumoKgMachos * 1000) / (dias * aves));
  }

  calcularEtapa(semana: number): string {
    // "semana" viene del backend como SEMANA DE VIDA (edad). REQ-012c: rango 26-33 (no 25-33).
    if (semana >= 26 && semana <= 33) return 'Etapa 1';
    if (semana >= 34 && semana <= 50) return 'Etapa 2';
    if (semana > 50) return 'Etapa 3';
    return 'Inicial';
  }

  /** REQ-000c: causa probable del empty-state (encaset futuro / sin guía genética), o null → mensaje genérico. */
  getEmptyStateCausa(): string | null {
    return causaEmptyStateIndicadores({
      fechaEncaset: this.informacionLote?.fechaEncaset,
      tieneDatosGuiaGenetica: this.tieneDatosGuiaGenetica,
      mensajeGuiaGenetica: this.mensajeGuiaGenetica
    });
  }

  // REQ-004: %Retiro (mortalidad + selección) acumulado, diferencia Real vs Guía (cálculo client-side
  // a partir de los 2 campos declarados en el DTO; sin campo "Dif" propio en el backend).
  diferenciaRetiroAcumuladoHembras(ind: IndicadorProduccionSemanalDto): number | null {
    if (ind.retiroAcumuladoHembrasReal == null || ind.retiroAcumuladoHembrasGuia == null) return null;
    return ind.retiroAcumuladoHembrasReal - ind.retiroAcumuladoHembrasGuia;
  }

  diferenciaRetiroAcumuladoMachos(ind: IndicadorProduccionSemanalDto): number | null {
    if (ind.retiroAcumuladoMachosReal == null || ind.retiroAcumuladoMachosGuia == null) return null;
    return ind.retiroAcumuladoMachosReal - ind.retiroAcumuladoMachosGuia;
  }

  formatearFecha(fecha: string | Date | null | undefined): string {
    if (!fecha) return '—';

    try {
      const date = typeof fecha === 'string' ? new Date(fecha) : fecha;

      // Validar que la fecha sea válida
      if (isNaN(date.getTime())) {
        return '—';
      }

      // Formatear como DD/MM/YYYY
      return date.toLocaleDateString('es-ES', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
      });
    } catch (error) {
      console.warn('Error al formatear fecha:', fecha, error);
      return '—';
    }
  }
}

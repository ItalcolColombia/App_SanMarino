// frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts
import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { exportarAoaMultiHojaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import {
  IndicadorEcuadorService,
  IndicadorEcuadorDto,
  IndicadorEcuadorRequest,
  IndicadorEcuadorConsolidadoDto,
  LiquidacionPeriodoDto,
  LiquidacionPolloEngordeReporteDto,
  LiquidacionPolloEngordeItemDto,
  ReporteIndicadoresPanamaDto
} from '../../services/indicador-ecuador.service';
import { LiquidacionReporteComponent } from '../../components/liquidacion-reporte/liquidacion-reporte.component';
import { LiquidacionReportePanamaComponent } from '../../components/liquidacion-reporte-panama/liquidacion-reporte-panama.component';
import { AuditoriaLiquidacionModalComponent } from '../../components/auditoria-liquidacion-modal/auditoria-liquidacion-modal.component';
import { AuditoriaScopeInput } from '../../models/auditoria-liquidacion.model';
import {
  FilterDataResponse,
  FilterDataPolloEngordeResponse,
  GalponOption,
  GranjaOption,
  LoteOption,
  NucleoOption,
  PeLoteAveEngordeItem
} from '../../models/indicador-filtros.model';
import {
  aplicarFiltroCronologico,
  construirCodigoAnioCorrida,
  filtrarCascadaGeneral,
  filtrarCascadaPe,
  filtrarLotesPorFechaEncaset
} from '../../funciones/cascada-filtros.funcion';
import { parsearFilterDataPollo } from '../../funciones/parsear-filter-data-pollo.funcion';
import {
  etiquetaColumnaLiquidacion,
  etiquetaLoteFiltro,
  etiquetaTabLote
} from '../../funciones/etiquetas.funcion';
import {
  ajusteAvesDe,
  calcularLiquidacionTotales,
  porcentajeAjusteAvesDe
} from '../../funciones/liquidacion-totales.funcion';
import { construirHojasReporteTecnico } from '../../funciones/exportar-reporte-tecnico-excel.funcion';
import {
  formatearFechaLote,
  formatearNumero,
  formatearPorcentaje
} from '../../funciones/formato.funcion';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-indicador-ecuador-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LiquidacionReporteComponent, LiquidacionReportePanamaComponent, AuditoriaLiquidacionModalComponent],
  templateUrl: './indicador-ecuador-list.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./indicador-ecuador-list.component.scss']
})
export class IndicadorEcuadorListComponent implements OnInit {
  /** URL del servicio único de filtros (mismo que Seguimiento Diario Levante). */
  readonly filterDataUrl = `${environment.apiUrl}/SeguimientoLoteLevante/filter-data`;
  readonly filterDataPolloEngordeUrl = `${environment.apiUrl}/LoteReproductoraAveEngorde/filter-data`;

  /** Vista: indicadores generales o Pollo Engorde. En Ecuador por defecto Pollo Engorde y el select se deshabilita. */
  vistaIndicador: 'general' | 'polloEngorde' = 'general';
  /** En Ecuador true: select Vista deshabilitado y fijado en Pollo Engorde. */
  vistaSelectDisabled: boolean = false;

  // Filtros - Vista General
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;
  fechaDesde: string = '';
  fechaHasta: string = '';
  tipoFiltroLotes: 'cerrados' | 'aves_cero' | 'todos' = 'cerrados';
  tipoLote: string = 'Todos';

  /** Pollo Engorde – liquidación: un lote (cascada) o rango por fecha de cierre. */
  polloModo: 'unLote' | 'rango' = 'unLote';
  loadingFilterDataPollo = true;
  peFarms: GranjaOption[] = [];
  peNucleos: NucleoOption[] = [];
  peGalpones: GalponOption[] = [];
  peLotesAveEngorde: PeLoteAveEngordeItem[] = [];
  private peAllNucleos: NucleoOption[] = [];
  private peAllGalpones: GalponOption[] = [];
  private peAllLotesAveEngorde: PeLoteAveEngordeItem[] = [];
  /** Si true, genera liquidación de todos los lotes liquidados del alcance (granja / núcleo / galpón) sin elegir uno. */
  peTodosLotesLiquidados = false;

  peGranjaId: number | null = null;
  peNucleoId: string | null = null;
  peGalponId: string | null = null;
  peLoteAveEngordeId: number | null = null;

  /** Rango: franja por fecha de cierre del lote; alcance de granjas. */
  polloAlcance: 'TodasLasGranjas' | 'Granja' | 'Nucleo' = 'TodasLasGranjas';
  peFechaDesde: string = '';
  peFechaHasta: string = '';
  rangoGranjaId: number | null = null;
  rangoNucleoId: string | null = null;

  /** Filtro de tiempo por fecha de encaset en el selector de lotes (Un Lote). */
  tipoFiltroFechaPollo: 'todos' | 'rango' | 'anio' | 'meses' = 'todos';
  filtroAnioPollo: number | null = null;
  filtroMesesPollo: number[] = [];
  filtroEncDesde: string = '';
  filtroEncHasta: string = '';

  /** Filtros cronológicos (Año-Corrida) */
  selectedAnio: string | null = null;
  selectedCorrida: string | null = null;
  corridasDisponibles: string[] = [
    '01', '02', '03', '04', '05', '06',
    '07', '08', '09', '10', '11', '12'
  ];
  /** Código concatenado Año+Corrida — vacío si no hay año seleccionado */
  get loteConvertido(): string {
    return construirCodigoAnioCorrida(this.selectedAnio, this.selectedCorrida);
  }

  /** Tab activa en la planilla de resultados: 'consolidado' o loteAveEngordeId. */
  tabActivaLiquidacion: 'consolidado' | number = 'consolidado';

  resultadoLiquidacionPollo: LiquidacionPolloEngordeReporteDto | null = null;

  // Verificador de liquidación (modal de auditoría)
  mostrarAuditoria = false;
  scopeAuditoria: AuditoriaScopeInput | null = null;
  mostrarLiquidacionPollo = false;
  showReporte = false;

  /** Reporte de liquidación Panamá ("RESULTADOS DE LIQUIDACIÓN"). Solo cuando el país activo es Panamá. */
  reportePanama: ReporteIndicadoresPanamaDto | null = null;
  mostrarReportePanama = false;

  // Datos
  indicadores: IndicadorEcuadorDto[] = [];
  consolidado: IndicadorEcuadorConsolidadoDto | null = null;
  mostrarConsolidado: boolean = false;
  lotesCerrados: IndicadorEcuadorDto[] = [];
  mostrarLotesCerrados: boolean = false;
  liquidacionPeriodo: LiquidacionPeriodoDto | null = null;
  mostrarLiquidacion: boolean = false;
  liquidacionFechaInicio: string = '';
  liquidacionFechaFin: string = '';
  liquidacionTipo: 'Semanal' | 'Mensual' = 'Mensual';

  // Catálogos (cargados en una sola llamada desde filter-data)
  granjas: GranjaOption[] = [];
  nucleos: NucleoOption[] = [];
  galpones: GalponOption[] = [];
  lotes: LoteOption[] = [];
  private allNucleos: NucleoOption[] = [];
  private allGalpones: GalponOption[] = [];
  private allLotes: LoteOption[] = [];

  // UI
  loading: boolean = false;
  loadingFilterData: boolean = true;
  error: string | null = null;
  showFormulasModal = false;

  // Inyección moderna
  private indicadorService = inject(IndicadorEcuadorService);
  private http = inject(HttpClient);
  private countryFilter = inject(CountryFilterService);

  ngOnInit(): void {
    if (this.countryFilter.isEcuador()) {
      this.vistaIndicador = 'polloEngorde';
      this.vistaSelectDisabled = true;
    }
    this.cargarFilterData();
    this.cargarFilterDataPolloEngorde();
    this.establecerFechasPorDefecto();
    this.establecerFechasLiquidacionPorDefecto();
  }

  /** País activo = Panamá (cambia título, badge y el reporte de liquidación). */
  get esPanama(): boolean {
    return this.countryFilter.isPanama();
  }

  /** Título del módulo según el país activo de la sesión. */
  get tituloModulo(): string {
    if (this.esPanama) return 'Indicador Panamá';
    if (this.countryFilter.isEcuador()) return 'Indicador Ecuador';
    return 'Indicador';
  }

  /** Badge país (bandera + nombre) para el encabezado. */
  get paisBadge(): string | null {
    if (this.esPanama) return '🇵🇦 Panamá';
    if (this.countryFilter.isEcuador()) return '🇪🇨 Ecuador';
    return null;
  }

  /** Granjas, núcleos, galpones y lotes ave engorde en cascada (mismo endpoint que reproductoras). */
  cargarFilterDataPolloEngorde(): void {
    this.loadingFilterDataPollo = true;
    this.http.get<FilterDataPolloEngordeResponse>(this.filterDataPolloEngordeUrl).subscribe({
      next: (raw) => {
        const parsed = parsearFilterDataPollo(raw);
        this.peFarms = parsed.farms;
        this.peAllNucleos = parsed.nucleos;
        this.peAllGalpones = parsed.galpones;
        this.peAllLotesAveEngorde = parsed.lotesAveEngorde;
        this.applyPeCascade();
        this.loadingFilterDataPollo = false;
      },
      error: () => {
        this.peFarms = [];
        this.peAllNucleos = [];
        this.peAllGalpones = [];
        this.peAllLotesAveEngorde = [];
        this.applyPeCascade();
        this.loadingFilterDataPollo = false;
      }
    });
  }

  /** Filtra lotes cuyo nombre comienza con el código Año-Corrida */
  private aplicarFiltroCronologico(lotes: PeLoteAveEngordeItem[]): PeLoteAveEngordeItem[] {
    return aplicarFiltroCronologico(lotes, this.loteConvertido);
  }

  /** Cascada Granja → Núcleo → Galpón → Lote (ave engorde). */
  private applyPeCascade(): void {
    const r = filtrarCascadaPe({
      granjaId: this.peGranjaId,
      nucleoId: this.peNucleoId,
      galponId: this.peGalponId,
      allNucleos: this.peAllNucleos,
      allGalpones: this.peAllGalpones,
      allLotes: this.peAllLotesAveEngorde,
      codigoCronologico: this.loteConvertido
    });
    this.peNucleos = r.nucleos;
    this.peGalpones = r.galpones;
    this.peLotesAveEngorde = r.lotesAveEngorde;
  }

  /** Si solo hay un núcleo en la granja, se selecciona solo (flujo Ecuador: núcleo 1 implícito). */
  private aplicarNucleoUnicoPorDefecto(): void {
    if (this.peNucleos.length === 1 && !this.peNucleoId) {
      this.peNucleoId = this.peNucleos[0].nucleoId;
      this.applyPeCascade();
    }
  }

  onPeGranjaChange(): void {
    this.peNucleoId = null;
    this.peGalponId = null;
    this.peLoteAveEngordeId = null;
    this.peTodosLotesLiquidados = false;
    this.tipoFiltroFechaPollo = 'todos';
    this.filtroAnioPollo = null;
    this.filtroMesesPollo = [];
    this.filtroEncDesde = '';
    this.filtroEncHasta = '';
    this.selectedAnio = null;
    this.selectedCorrida = null;
    this.applyPeCascade();
    this.aplicarNucleoUnicoPorDefecto();
  }

  onPeNucleoChange(): void {
    this.peGalponId = null;
    this.peLoteAveEngordeId = null;
    this.applyPeCascade();
  }

  onPeGalponChange(): void {
    this.peLoteAveEngordeId = null;
    this.applyPeCascade();
  }

  onFiltroAnioChange(value: string | null): void {
    this.selectedAnio = value;
    this.selectedCorrida = null;
    this.peLoteAveEngordeId = null;
    this.applyPeCascade();
  }

  onFiltroCorreidaChange(value: string | null): void {
    this.selectedCorrida = value;
    this.peLoteAveEngordeId = null;
    this.applyPeCascade();
  }

  onPeTodosLotesChange(): void {
    if (this.peTodosLotesLiquidados) {
      this.peLoteAveEngordeId = null;
    } else {
      this.selectedAnio = null;
      this.selectedCorrida = null;
    }
  }

  onPolloModoChange(): void {
    this.error = null;
    this.selectedAnio = null;
    this.selectedCorrida = null;
  }

  onPolloAlcanceChange(): void {
    if (this.polloAlcance === 'TodasLasGranjas') {
      this.rangoGranjaId = null;
      this.rangoNucleoId = null;
    } else if (this.polloAlcance === 'Granja') {
      this.rangoNucleoId = null;
    }
  }

  /** Núcleos filtrados por granja seleccionada (modo rango). */
  get rangoNucleosLista(): NucleoOption[] {
    if (!this.rangoGranjaId) return [];
    return this.peAllNucleos.filter((n) => n.granjaId === this.rangoGranjaId);
  }

  /** Carga en una sola llamada granjas, núcleos, galpones y lotes (mismo servicio que Seguimiento Diario Levante). */
  cargarFilterData(): void {
    this.loadingFilterData = true;
    this.http.get<FilterDataResponse>(this.filterDataUrl).subscribe({
      next: (data) => {
        this.granjas = data.farms ?? [];
        this.allNucleos = data.nucleos ?? [];
        this.allGalpones = data.galpones ?? [];
        this.allLotes = data.lotes ?? [];
        this.applyFilterCascade();
        this.loadingFilterData = false;
      },
      error: () => {
        this.granjas = [];
        this.allNucleos = [];
        this.allGalpones = [];
        this.allLotes = [];
        this.nucleos = [];
        this.galpones = [];
        this.lotes = [];
        this.loadingFilterData = false;
      }
    });
  }

  /** Aplica cascada Granja → Núcleo → Galpón → Lote. */
  private applyFilterCascade(): void {
    const r = filtrarCascadaGeneral({
      granjaId: this.selectedGranjaId,
      nucleoId: this.selectedNucleoId,
      galponId: this.selectedGalponId,
      allNucleos: this.allNucleos,
      allGalpones: this.allGalpones,
      allLotes: this.allLotes
    });
    this.nucleos = r.nucleos;
    this.galpones = r.galpones;
    this.lotes = r.lotes;
  }

  establecerFechasPorDefecto(): void {
    const hoy = new Date();
    const primerDiaMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
    const ultimoDiaMes = new Date(hoy.getFullYear(), hoy.getMonth() + 1, 0);
    this.fechaDesde = primerDiaMes.toISOString().split('T')[0];
    this.fechaHasta = ultimoDiaMes.toISOString().split('T')[0];
    this.peFechaDesde = this.fechaDesde;
    this.peFechaHasta = this.fechaHasta;
  }

  establecerFechasLiquidacionPorDefecto(): void {
    const hoy = new Date();
    const primerDiaMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
    const ultimoDiaMes = new Date(hoy.getFullYear(), hoy.getMonth() + 1, 0);
    this.liquidacionFechaInicio = primerDiaMes.toISOString().split('T')[0];
    this.liquidacionFechaFin = ultimoDiaMes.toISOString().split('T')[0];
  }

  onGranjaChange(): void {
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.applyFilterCascade();
  }

  onNucleoChange(): void {
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.applyFilterCascade();
  }

  onGalponChange(): void {
    this.selectedLoteId = null;
    this.applyFilterCascade();
  }

  async calcularIndicadores(): Promise<void> {
    this.loading = true;
    this.error = null;

    try {
      const request: IndicadorEcuadorRequest = {
        granjaId: this.selectedGranjaId ? Number(this.selectedGranjaId) : null,
        nucleoId: this.selectedNucleoId || null,
        galponId: this.selectedGalponId || null,
        loteId: this.selectedLoteId ? Number(this.selectedLoteId) : null,
        fechaDesde: this.fechaDesde || null,
        fechaHasta: this.fechaHasta || null,
        tipoFiltroLotes: this.tipoFiltroLotes,
        tipoLote: this.tipoLote
      };

      this.indicadores = await firstValueFrom(this.indicadorService.calcularIndicadores(request));
      this.mostrarConsolidado = false;
      this.mostrarLotesCerrados = false;
      this.mostrarLiquidacion = false;
    } catch (error: any) {
      const errorMessage = error?.error?.message || error?.error?.error || error?.message || 'Error al calcular indicadores';
      this.error = errorMessage;
      console.error('Error al calcular indicadores:', error);
      if (error?.error) {
        console.error('Detalles del error:', error.error);
      }
    } finally {
      this.loading = false;
    }
  }

  async calcularConsolidado(): Promise<void> {
    this.loading = true;
    this.error = null;

    try {
      const request: IndicadorEcuadorRequest = {
        granjaId: this.selectedGranjaId ? Number(this.selectedGranjaId) : null,
        nucleoId: this.selectedNucleoId || null,
        galponId: this.selectedGalponId || null,
        loteId: this.selectedLoteId ? Number(this.selectedLoteId) : null,
        fechaDesde: this.fechaDesde || null,
        fechaHasta: this.fechaHasta || null,
        tipoFiltroLotes: this.tipoFiltroLotes,
        tipoLote: this.tipoLote
      };

      this.consolidado = await firstValueFrom(this.indicadorService.calcularConsolidado(request));
      this.mostrarConsolidado = true;
      this.mostrarLotesCerrados = false;
      this.mostrarLiquidacion = false;
    } catch (error: any) {
      const errorMessage = error?.error?.message || error?.error?.error || error?.message || 'Error al calcular consolidado';
      this.error = errorMessage;
      console.error('Error al calcular consolidado:', error);
      if (error?.error) {
        console.error('Detalles del error:', error.error);
      }
    } finally {
      this.loading = false;
    }
  }

  async verLotesCerrados(): Promise<void> {
    this.loading = true;
    this.error = null;
    this.mostrarConsolidado = false;
    this.mostrarLiquidacion = false;
    try {
      const desde = this.fechaDesde || new Date().toISOString().split('T')[0];
      const hasta = this.fechaHasta || new Date().toISOString().split('T')[0];
      this.lotesCerrados = await firstValueFrom(
        this.indicadorService.obtenerLotesCerrados(
          desde,
          hasta,
          this.selectedGranjaId ?? undefined,
          this.tipoFiltroLotes !== 'todos'
        )
      );
      this.mostrarLotesCerrados = true;
    } catch (err: any) {
      this.error = err?.error?.message || err?.error?.error || err?.message || 'Error al cargar lotes cerrados';
      this.lotesCerrados = [];
    } finally {
      this.loading = false;
    }
  }

  async calcularLiquidacionPeriodo(): Promise<void> {
    this.loading = true;
    this.error = null;
    this.mostrarConsolidado = false;
    this.mostrarLotesCerrados = false;
    try {
      const request = {
        fechaInicio: this.liquidacionFechaInicio,
        fechaFin: this.liquidacionFechaFin,
        tipoPeriodo: this.liquidacionTipo,
        granjaId: this.selectedGranjaId ?? null
      };
      this.liquidacionPeriodo = await firstValueFrom(this.indicadorService.calcularLiquidacionPeriodo(request));
      this.mostrarLiquidacion = true;
    } catch (err: any) {
      this.error = err?.error?.message || err?.error?.error || err?.message || 'Error al calcular liquidación por período';
      this.liquidacionPeriodo = null;
    } finally {
      this.loading = false;
    }
  }

  async generarLiquidacionPolloEngorde(): Promise<void> {
    this.loading = true;
    this.error = null;
    this.mostrarLiquidacionPollo = false;
    this.resultadoLiquidacionPollo = null;
    this.showReporte = false;
    this.mostrarReportePanama = false;
    this.reportePanama = null;

    // Panamá: el reporte de liquidación es el de "RESULTADOS DE LIQUIDACIÓN" (fn_reporte_indicadores_panama),
    // por lote. Requiere un lote específico seleccionado.
    if (this.esPanama) {
      if (!this.peLoteAveEngordeId) {
        this.error = 'Seleccione un lote para generar el reporte de liquidación (Panamá).';
        this.loading = false;
        return;
      }
      try {
        this.reportePanama = await firstValueFrom(
          this.indicadorService.getReporteIndicadoresPanama(this.peLoteAveEngordeId)
        );
        this.mostrarReportePanama = true;
      } catch (err: any) {
        if (err?.status === 404) {
          this.error = 'El lote aún no tiene liquidación registrada. Ciérrelo/liquídelo desde Seguimiento Pollo Engorde.';
        } else {
          this.error = err?.error?.error ?? err?.error?.message ?? err?.message ?? 'Error al generar el reporte de liquidación.';
        }
        this.reportePanama = null;
      } finally {
        this.loading = false;
      }
      return;
    }

    try {
      if (this.polloModo === 'unLote') {
        if (!this.peGranjaId) {
          this.error = 'Seleccione granja.';
          return;
        }
        if (this.peLoteAveEngordeId) {
          // Lote específico seleccionado → consulta individual
          const res = await firstValueFrom(
            this.indicadorService.liquidacionPolloEngordeReporte({
              modo: 'UnLote',
              loteAveEngordeId: this.peLoteAveEngordeId,
              fechaDesde: null,
              fechaHasta: null,
              alcance: 'TodasLasGranjas',
              granjaId: null,
              nucleoId: null,
              galponId: null,
              tipoFiltroLotes: this.tipoFiltroLotes
            })
          );
          this.resultadoLiquidacionPollo = res;
        } else {
          // Sin lote específico → consulta masiva por alcance (granja / núcleo / galpón)
          const res = await firstValueFrom(
            this.indicadorService.liquidacionPolloEngordeReporte({
              modo: 'TodosLiquidados',
              loteAveEngordeId: null,
              fechaDesde: null,
              fechaHasta: null,
              alcance: 'TodasLasGranjas',
              granjaId: this.peGranjaId,
              nucleoId: this.peNucleoId || null,
              galponId: this.peGalponId || null,
              loteCodigo: this.loteConvertido || null,
              tipoFiltroLotes: this.tipoFiltroLotes
            })
          );
          this.resultadoLiquidacionPollo = res;
        }
      } else {
        if (!this.peFechaDesde || !this.peFechaHasta) {
          this.error = 'Indique fecha inicial y fecha final (fecha de cierre del lote).';
          return;
        }
        const alcance = this.polloAlcance;
        if ((alcance === 'Granja' || alcance === 'Nucleo') && !this.rangoGranjaId) {
          this.error = 'Seleccione granja para el alcance elegido.';
          return;
        }
        if (alcance === 'Nucleo' && !this.rangoNucleoId) {
          this.error = 'Seleccione núcleo.';
          return;
        }
        const res = await firstValueFrom(
          this.indicadorService.liquidacionPolloEngordeReporte({
            modo: 'Rango',
            loteAveEngordeId: null,
            fechaDesde: this.peFechaDesde,
            fechaHasta: this.peFechaHasta,
            alcance,
            granjaId: alcance === 'TodasLasGranjas' ? null : this.rangoGranjaId,
            nucleoId: alcance === 'Nucleo' ? this.rangoNucleoId : null,
            tipoFiltroLotes: this.tipoFiltroLotes
          })
        );
        this.resultadoLiquidacionPollo = res;
      }
      this.mostrarLiquidacionPollo = true;
      this.tabActivaLiquidacion = 'consolidado';
    } catch (err: any) {
      this.error = err?.error?.error ?? err?.error?.message ?? err?.message ?? 'Error al generar la liquidación.';
      this.resultadoLiquidacionPollo = null;
    } finally {
      this.loading = false;
    }
  }

  /** Abre el verificador de liquidación con el alcance de la corrida cargada. */
  abrirAuditoria(): void {
    const items = this.resultadoLiquidacionPollo?.items ?? [];
    const granjaId = items[0]?.indicador?.granjaId ?? this.peGranjaId ?? this.rangoGranjaId ?? 0;
    if (!granjaId) {
      this.error = 'Genere primero la liquidación para poder verificarla.';
      return;
    }
    const nucleoId = (this.peNucleoId || this.rangoNucleoId || null) as string | null;
    const loteCodigo = (this.loteConvertido || items[0]?.loteNombre || null) as string | null;
    this.scopeAuditoria = { granjaId, nucleoId, loteCodigo };
    this.mostrarAuditoria = true;
  }

  cerrarAuditoria(): void {
    this.mostrarAuditoria = false;
  }

  /** Encabezado de columna en la planilla: galpón · lote · edad (días de ciclo). */
  etiquetaColumnaLiquidacion(item: LiquidacionPolloEngordeItemDto): string {
    return etiquetaColumnaLiquidacion(item);
  }

  /** Fila TOTAL: agrega cantidades y recalcula ratios como en consolidado. */
  liquidacionTotales(): IndicadorEcuadorDto | null {
    return calcularLiquidacionTotales(this.resultadoLiquidacionPollo?.items);
  }

  /** R1: valor o «—» — merma no registrada ⇒ campo vacío en el reporte. */
  fmtONada(v: number | null | undefined, decimales: number = 2): string {
    return v == null ? '—' : this.formatearNumero(v, decimales);
  }

  /** R1: porcentaje o «—» cuando la merma no está registrada. */
  fmtPctONada(v: number | null | undefined): string {
    return v == null ? '—' : this.formatearPorcentaje(v);
  }

  /**
   * Ajuste de aves = encasetadas − vendidas − (mortalidad + selección).
   * Se calcula SIEMPRE en el front (la mortalidad del DTO ya incluye selección y
   * avesSacrificadas = aves vendidas), de modo que aplica a TODOS los lotes, no solo
   * a los que tienen merma registrada. NO depende de la merma.
   */
  ajusteDe(ind: IndicadorEcuadorDto): number {
    return ajusteAvesDe(ind);
  }

  /** % de ajuste = (ajuste / encasetadas) × 100. Se calcula para todos los lotes. */
  porcentajeAjusteDe(ind: IndicadorEcuadorDto): number {
    return porcentajeAjusteAvesDe(ind);
  }

  etiquetaLoteFiltro(l: PeLoteAveEngordeItem): string {
    return etiquetaLoteFiltro(l, this.peAllGalpones);
  }

  limpiarFiltros(): void {
    this.selectedGranjaId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.establecerFechasPorDefecto();
    this.establecerFechasLiquidacionPorDefecto();
    this.tipoFiltroLotes = 'cerrados';
    this.tipoLote = 'Todos';
    this.applyFilterCascade();
    this.indicadores = [];
    this.consolidado = null;
    this.mostrarConsolidado = false;
    this.lotesCerrados = [];
    this.mostrarLotesCerrados = false;
    this.liquidacionPeriodo = null;
    this.mostrarLiquidacion = false;
    this.polloModo = 'unLote';
    this.peGranjaId = null;
    this.peNucleoId = null;
    this.peGalponId = null;
    this.peLoteAveEngordeId = null;
    this.peTodosLotesLiquidados = false;
    this.polloAlcance = 'TodasLasGranjas';
    this.rangoGranjaId = null;
    this.rangoNucleoId = null;
    this.tipoFiltroFechaPollo = 'todos';
    this.filtroAnioPollo = null;
    this.filtroMesesPollo = [];
    this.filtroEncDesde = '';
    this.filtroEncHasta = '';
    this.selectedAnio = null;
    this.selectedCorrida = null;
    this.tabActivaLiquidacion = 'consolidado';
    this.applyPeCascade();
    this.resultadoLiquidacionPollo = null;
    this.mostrarLiquidacionPollo = false;
  }

  /** Lotes del selector filtrados por fecha de encaset según el tipo de filtro activo. */
  get peLotesFiltradosPorFecha(): PeLoteAveEngordeItem[] {
    return filtrarLotesPorFechaEncaset({
      base: this.peLotesAveEngorde,
      tipoFiltroFecha: this.tipoFiltroFechaPollo,
      filtroAnio: this.filtroAnioPollo,
      filtroMeses: this.filtroMesesPollo,
      filtroDesde: this.filtroEncDesde,
      filtroHasta: this.filtroEncHasta
    });
  }

  get aniosDisponiblesPollo(): number[] {
    const s = new Set<number>();
    this.peLotesAveEngorde.forEach(l => { if (l.fechaEncaset) s.add(new Date(l.fechaEncaset).getFullYear()); });
    return Array.from(s).sort((a, b) => b - a);
  }

  /** Años disponibles extraídos de TODOS los lotes (sin filtro de cascada) para que el dropdown siempre esté poblado */
  get aniosDisponibles(): string[] {
    const s = new Set<string>();
    this.peAllLotesAveEngorde.forEach(l => {
      if (l.loteNombre && l.loteNombre.length >= 2) {
        const year = l.loteNombre.substring(0, 2);
        if (/^\d{2}$/.test(year)) s.add(year);
      }
    });
    return Array.from(s).sort((a, b) => parseInt(b) - parseInt(a));
  }

  get mesesNombres(): Array<{ num: number; nombre: string }> {
    return [
      { num: 1, nombre: 'Ene' }, { num: 2, nombre: 'Feb' }, { num: 3, nombre: 'Mar' },
      { num: 4, nombre: 'Abr' }, { num: 5, nombre: 'May' }, { num: 6, nombre: 'Jun' },
      { num: 7, nombre: 'Jul' }, { num: 8, nombre: 'Ago' }, { num: 9, nombre: 'Sep' },
      { num: 10, nombre: 'Oct' }, { num: 11, nombre: 'Nov' }, { num: 12, nombre: 'Dic' }
    ];
  }

  toggleMesFiltro(mes: number): void {
    const idx = this.filtroMesesPollo.indexOf(mes);
    if (idx === -1) this.filtroMesesPollo.push(mes);
    else this.filtroMesesPollo.splice(idx, 1);
    this.peLoteAveEngordeId = null;
  }

  onTipoFiltroFechaChange(): void {
    this.filtroAnioPollo = null;
    this.filtroMesesPollo = [];
    this.filtroEncDesde = '';
    this.filtroEncHasta = '';
    this.peLoteAveEngordeId = null;
  }

  seleccionarTab(tab: 'consolidado' | number): void {
    this.tabActivaLiquidacion = tab;
  }

  etiquetaTabLote(item: LiquidacionPolloEngordeItemDto): string {
    return etiquetaTabLote(item);
  }

  /**
   * Fecha guardada como timestamptz a medianoche UTC (fecha "pura", sin hora real).
   * OJO: NO usar `new Date(fecha).toLocaleDateString()` — convierte a la zona horaria
   * local del navegador y en Ecuador (UTC-5) corre la fecha un día hacia atrás.
   * Se extrae YYYY-MM-DD directo del ISO, sin pasar por conversión de zona horaria.
   */
  formatearFechaLote(fecha: string | null | undefined): string {
    return formatearFechaLote(fecha);
  }

  formatearNumero(valor: number | null | undefined, decimales: number = 2): string {
    return formatearNumero(valor, decimales);
  }

  formatearPorcentaje(valor: number | null | undefined): string {
    return formatearPorcentaje(valor);
  }

  exportarExcel(): void {
    const hojas = construirHojasReporteTecnico(this.resultadoLiquidacionPollo, this.liquidacionTotales());
    if (!hojas) return;

    const yyyymmdd = new Date().toISOString().slice(0, 10).replace(/-/g, '');
    exportarAoaMultiHojaExcel(hojas, { filenameFull: `Reporte_Tecnico_Ecuador_${yyyymmdd}.xlsx` });
  }
}

// src/app/features/reporte-contable/pages/reporte-contable-main/reporte-contable-main.component.ts
import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faFileExcel,
  faFileAlt,
  faCalendarWeek,
  faDownload,
  faSpinner,
  faSearch,
  faFilter,
  faDollarSign
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil, finalize } from 'rxjs';

import {
  ReporteContableService,
  ReporteContableCompletoDto,
  GenerarReporteContableRequestDto,
  ReporteMovimientosHuevosDto,
  FiltrosContablesDto,
  GranjaFiltroContableDto,
  NucleoFiltroContableDto,
  GalponFiltroContableDto,
  LoteBaseFiltroContableDto
} from '../../services/reporte-contable.service';
import { TablaDetalleDiarioContableComponent } from '../../components/tabla-detalle-diario-contable/tabla-detalle-diario-contable.component';
import { TablaAvesContableComponent } from '../../components/tabla-aves-contable/tabla-aves-contable.component';
import { TablaBultosContableComponent } from '../../components/tabla-bultos-contable/tabla-bultos-contable.component';
import { TablaMovimientosHuevosComponent } from '../../components/tabla-movimientos-huevos/tabla-movimientos-huevos.component';

@Component({
  selector: 'app-reporte-contable-main',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    FontAwesomeModule,
    TablaDetalleDiarioContableComponent,
    TablaAvesContableComponent,
    TablaBultosContableComponent,
    TablaMovimientosHuevosComponent
  ],
  templateUrl: './reporte-contable-main.component.html',
  styleUrls: ['./reporte-contable-main.component.scss']
})
export class ReporteContableMainComponent implements OnInit, OnDestroy {
  faFileExcel = faFileExcel;
  faFileAlt = faFileAlt;
  faCalendarWeek = faCalendarWeek;
  faDownload = faDownload;
  faSpinner = faSpinner;
  faSearch = faSearch;
  faFilter = faFilter;
  faDollarSign = faDollarSign;

  loading = signal(false);
  loadingFiltros = signal(false);
  reporte = signal<ReporteContableCompletoDto | null>(null);
  reporteMovimientosHuevos = signal<ReporteMovimientosHuevosDto | null>(null);
  loadingMovimientosHuevos = signal(false);

  // Jerarquía de filtros cargada desde el backend
  filtrosData: FiltrosContablesDto | null = null;

  // Selecciones en cascada
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteBaseId: number | null = null; // loteId del lote base seleccionado

  // Fase del lote — obligatoria
  faseDelLote: 'Levante' | 'Produccion' | null = null;

  // Filtros de reporte
  semanaContable: number | null = null;
  semanasContablesDisponibles: number[] = [];
  fechaInicio: string | null = null;
  fechaFin: string | null = null;
  usarRangoFechas: boolean = false;

  // UI
  error: string | null = null;
  activeTab: 'resumen' | 'movimientos-huevos' | number = 'resumen';

  private destroy$ = new Subject<void>();

  constructor(private reporteContableService: ReporteContableService) {}

  ngOnInit(): void {
    this.cargarFiltros();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private cargarFiltros(): void {
    this.loadingFiltros.set(true);
    this.reporteContableService.getFiltrosDisponibles()
      .pipe(takeUntil(this.destroy$), finalize(() => this.loadingFiltros.set(false)))
      .subscribe({
        next: (data) => { this.filtrosData = data; },
        error: () => { this.filtrosData = null; }
      });
  }

  // ================== CASCADA DE FILTROS ==================

  get granjas(): GranjaFiltroContableDto[] {
    return this.filtrosData?.granjas ?? [];
  }

  get nucleos(): NucleoFiltroContableDto[] {
    if (!this.selectedGranjaId || !this.filtrosData) return [];
    return this.filtrosData.granjas.find(g => g.granjaId === this.selectedGranjaId)?.nucleos ?? [];
  }

  get galpones(): GalponFiltroContableDto[] {
    if (!this.filtrosData) return [];
    const granja = this.filtrosData.granjas.find(g => g.granjaId === this.selectedGranjaId);
    if (!granja) return [];
    if (!this.selectedNucleoId) {
      return granja.nucleos.flatMap(n => n.galpones);
    }
    return granja.nucleos.find(n => n.nucleoId === this.selectedNucleoId)?.galpones ?? [];
  }

  get lotesBase(): LoteBaseFiltroContableDto[] {
    if (!this.filtrosData) return [];
    const granja = this.filtrosData.granjas.find(g => g.granjaId === this.selectedGranjaId);
    if (!granja) return [];

    const nucleosFiltro = this.selectedNucleoId
      ? granja.nucleos.filter(n => n.nucleoId === this.selectedNucleoId)
      : granja.nucleos;

    const galponesFiltro = this.selectedGalponId
      ? nucleosFiltro.flatMap(n => n.galpones).filter(g => g.galponId === this.selectedGalponId)
      : nucleosFiltro.flatMap(n => n.galpones);

    return galponesFiltro.flatMap(g => g.lotesBase);
  }

  get selectedLoteBase(): LoteBaseFiltroContableDto | null {
    return this.lotesBase.find(l => l.loteId === this.selectedLoteBaseId) ?? null;
  }

  get selectedGranjaNombre(): string {
    return this.granjas.find(g => g.granjaId === this.selectedGranjaId)?.granjaNombre ?? '';
  }

  get selectedNucleoNombre(): string {
    return this.nucleos.find(n => n.nucleoId === this.selectedNucleoId)?.nucleoNombre ?? '';
  }

  get selectedGalponNombre(): string {
    return this.galpones.find(g => g.galponId === this.selectedGalponId)?.galponNombre ?? '';
  }

  get selectedLoteBaseNombre(): string {
    return this.selectedLoteBase?.loteNombre ?? '';
  }

  // ================== EVENTOS DE FILTRO ==================

  onGranjaChange(granjaId: number | null): void {
    this.selectedGranjaId = granjaId ? Number(granjaId) : null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteBaseId = null;
    this.reporte.set(null);
    this.resetReporteConfig();
  }

  onNucleoChange(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId || null;
    this.selectedGalponId = null;
    this.selectedLoteBaseId = null;
    this.reporte.set(null);
    this.resetReporteConfig();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId || null;
    this.selectedLoteBaseId = null;
    this.reporte.set(null);
    this.resetReporteConfig();
  }

  onLoteBaseChange(loteId: number | null): void {
    this.selectedLoteBaseId = loteId ? Number(loteId) : null;
    this.faseDelLote = null;
    this.reporte.set(null);
    this.resetReporteConfig();
    this.error = null;
  }

  onFaseChange(fase: 'Levante' | 'Produccion' | null): void {
    this.faseDelLote = fase;
    this.reporte.set(null);
    this.resetReporteConfig();
    this.error = null;

    if (this.selectedLoteBaseId && this.faseDelLote) {
      this.cargarSemanasContables();
    }
  }

  private resetReporteConfig(): void {
    this.semanaContable = null;
    this.semanasContablesDisponibles = [];
    this.fechaInicio = null;
    this.fechaFin = null;
    this.usarRangoFechas = false;
  }

  private cargarSemanasContables(): void {
    if (!this.selectedLoteBaseId) return;
    this.reporteContableService.obtenerSemanasContables(this.selectedLoteBaseId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (semanas) => {
          this.semanasContablesDisponibles = semanas ?? [];
          if (semanas?.length > 0) {
            this.semanaContable = semanas[semanas.length - 1];
          }
        },
        error: () => { this.semanasContablesDisponibles = []; }
      });
  }

  // ================== GENERAR REPORTE ==================

  generarReporte(): void {
    if (!this.validarFiltros()) return;

    this.loading.set(true);
    this.error = null;

    const request: GenerarReporteContableRequestDto = {
      lotePadreId: this.selectedLoteBaseId!,
      faseDelLote: this.faseDelLote!,
      semanaContable: this.usarRangoFechas ? undefined : (this.semanaContable ?? undefined),
      fechaInicio: this.usarRangoFechas ? (this.fechaInicio ?? undefined) : undefined,
      fechaFin: this.usarRangoFechas ? (this.fechaFin ?? undefined) : undefined
    };

    this.reporteContableService.generarReporte(request)
      .pipe(finalize(() => this.loading.set(false)), takeUntil(this.destroy$))
      .subscribe({
        next: (reporte) => {
          this.reporte.set(reporte);
          this.error = null;
          this.cargarReporteMovimientosHuevos(request);
        },
        error: (err) => {
          this.error = err.error?.message || 'Error al generar el reporte contable';
          this.reporte.set(null);
        }
      });
  }

  private cargarReporteMovimientosHuevos(request: GenerarReporteContableRequestDto): void {
    this.loadingMovimientosHuevos.set(true);
    this.reporteContableService.obtenerReporteMovimientosHuevos(request)
      .pipe(finalize(() => this.loadingMovimientosHuevos.set(false)), takeUntil(this.destroy$))
      .subscribe({
        next: (reporte) => { this.reporteMovimientosHuevos.set(reporte); },
        error: () => { this.reporteMovimientosHuevos.set(null); }
      });
  }

  private validarFiltros(): boolean {
    if (!this.selectedLoteBaseId) {
      this.error = 'Debe seleccionar un lote base para generar el reporte contable';
      return false;
    }

    if (!this.faseDelLote) {
      this.error = 'Debe seleccionar la fase del lote (Levante o Producción)';
      return false;
    }

    if (this.usarRangoFechas || this.fechaInicio || this.fechaFin) {
      if (this.fechaInicio && this.fechaFin) {
        const inicio = new Date(this.fechaInicio);
        const fin = new Date(this.fechaFin);
        if (inicio > fin) {
          this.error = 'La fecha de inicio debe ser anterior a la fecha de fin';
          return false;
        }
        const dias = Math.ceil((fin.getTime() - inicio.getTime()) / (1000 * 60 * 60 * 24));
        if (dias > 365) {
          this.error = 'El rango de fechas no puede ser mayor a 365 días';
          return false;
        }
      } else if (this.fechaInicio && !this.fechaFin) {
        this.error = 'Debe seleccionar también la fecha de fin';
        return false;
      } else if (this.fechaFin && !this.fechaInicio) {
        this.error = 'Debe seleccionar también la fecha de inicio';
        return false;
      }
    }

    this.error = null;
    return true;
  }

  puedeGenerarReporte(): boolean {
    return this.validarFiltros();
  }

  // ================== EXPORTAR EXCEL ==================

  exportarExcel(): void {
    if (!this.validarFiltros()) return;

    this.loading.set(true);
    this.error = null;

    const request: GenerarReporteContableRequestDto = {
      lotePadreId: this.selectedLoteBaseId!,
      faseDelLote: this.faseDelLote!,
      semanaContable: this.usarRangoFechas ? undefined : (this.semanaContable ?? undefined),
      fechaInicio: this.usarRangoFechas ? (this.fechaInicio ?? undefined) : undefined,
      fechaFin: this.usarRangoFechas ? (this.fechaFin ?? undefined) : undefined
    };

    this.reporteContableService.exportarExcel(request)
      .pipe(finalize(() => this.loading.set(false)), takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          const reporte = this.reporte();
          link.download = reporte
            ? `Reporte_Contable_${reporte.lotePadreNombre}_Semana_${this.semanaContable || 'Actual'}.xlsx`
            : `Reporte_Contable_${this.selectedLoteBaseId}_${new Date().toISOString().split('T')[0]}.xlsx`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);
        },
        error: (err) => {
          this.error = err.error?.message || 'Error al exportar el reporte contable';
        }
      });
  }

  limpiarReporte(): void {
    this.reporte.set(null);
    this.error = null;
    this.activeTab = 'resumen';
  }

  // ================== TABS ==================

  setActiveTab(tab: 'resumen' | 'movimientos-huevos' | number): void {
    this.activeTab = tab;
  }

  isTabActive(tab: 'resumen' | 'movimientos-huevos' | number): boolean {
    return this.activeTab === tab;
  }

  getTabLabel(semana: number): string {
    const reporte = this.reporte();
    if (!reporte) return `Semana ${semana}`;
    const r = reporte.reportesSemanales.find(r => r.semanaContable === semana);
    if (!r) return `Semana ${semana}`;
    return `Sem ${semana} (${this.formatDateShort(r.fechaInicio)}-${this.formatDateShort(r.fechaFin)})`;
  }

  // ================== EVENTOS DE FECHAS / SEMANA ==================

  onSemanaChange(): void {
    if (this.semanaContable !== null) {
      this.usarRangoFechas = false;
      this.fechaInicio = null;
      this.fechaFin = null;
    } else if (this.fechaInicio || this.fechaFin) {
      this.usarRangoFechas = true;
    }
    this.limpiarReporte();
  }

  onFechaInicioChange(): void {
    if (this.fechaInicio) {
      this.usarRangoFechas = true;
      this.semanaContable = null;
    } else if (!this.fechaFin) {
      this.usarRangoFechas = false;
    }
    this.limpiarReporte();
  }

  onFechaFinChange(): void {
    if (this.fechaFin) {
      this.usarRangoFechas = true;
      this.semanaContable = null;
    } else if (!this.fechaInicio) {
      this.usarRangoFechas = false;
    }
    this.limpiarReporte();
  }

  // ================== TOTALES ==================

  getTotalMortalidad(): number {
    const r = this.reporte();
    if (!r) return 0;
    return r.reportesSemanales.reduce((t, s) => t + s.mortalidadHembrasSemanal + s.mortalidadMachosSemanal, 0);
  }

  getTotalTraslados(): number {
    const r = this.reporte();
    if (!r) return 0;
    return r.reportesSemanales.reduce((t, s) => t + s.trasladosHembrasSemanal + s.trasladosMachosSemanal, 0);
  }

  getTotalVentas(): number {
    const r = this.reporte();
    if (!r) return 0;
    return r.reportesSemanales.reduce((t, s) => t + s.ventasHembrasSemanal + s.ventasMachosSemanal, 0);
  }

  getTotalAlimento(): number {
    const r = this.reporte();
    if (!r) return 0;
    return r.reportesSemanales.reduce((t, s) => t + s.consumoTotalAlimento, 0);
  }

  getTotalAgua(): number {
    const r = this.reporte();
    if (!r) return 0;
    return r.reportesSemanales.reduce((t, s) => t + s.consumoTotalAgua, 0);
  }

  getTotalMedicamento(): number {
    const r = this.reporte();
    if (!r) return 0;
    return r.reportesSemanales.reduce((t, s) => t + s.consumoTotalMedicamento, 0);
  }

  getTotalVacuna(): number {
    const r = this.reporte();
    if (!r) return 0;
    return r.reportesSemanales.reduce((t, s) => t + s.consumoTotalVacuna, 0);
  }

  getTotalOtros(): number {
    const r = this.reporte();
    if (!r) return 0;
    return r.reportesSemanales.reduce((t, s) => t + s.otrosConsumos, 0);
  }

  getTotalGeneral(): number {
    const r = this.reporte();
    if (!r) return 0;
    return r.reportesSemanales.reduce((t, s) => t + s.totalGeneral, 0);
  }

  // ================== HELPERS ==================

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return 'N/A';
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }

  formatDateShort(date: string | Date | null | undefined): string {
    if (!date) return '';
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit' });
  }
}

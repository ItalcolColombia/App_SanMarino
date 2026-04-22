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
  ReporteContableSemanalDto,
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
  selectedLoteBaseId: number | null = null;

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

  // Sub-lote seleccionado (tab externo)
  selectedSublote: string | null = null;

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
    this.selectedSublote = null;
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

  // ================== SUBLOTES ==================

  get sublotes(): string[] {
    const r = this.reporte();
    if (!r) return [];
    const names = new Set<string>();
    r.reportesSemanales.forEach(s =>
      s.datosDiarios.forEach(d => { if (d.loteNombre) names.add(d.loteNombre); })
    );
    // Fallback: use sublotes string[] from first semana if datosDiarios is empty
    if (names.size === 0 && r.reportesSemanales.length > 0) {
      r.reportesSemanales[0].sublotes.forEach(s => names.add(s));
    }
    return Array.from(names).sort();
  }

  selectSublote(nombre: string): void {
    this.selectedSublote = nombre;
    this.activeTab = 'resumen';
  }

  // Proyecta un ReporteContableSemanalDto filtrado para un sub-lote específico
  private proyectarSemanaParaSublote(
    semana: ReporteContableSemanalDto,
    subloteNombre: string,
    saldoAnteriorH: number,
    saldoAnteriorM: number
  ): ReporteContableSemanalDto {
    const fd = semana.datosDiarios.filter(d => d.loteNombre === subloteNombre);
    const fc = semana.consumosDiarios.filter(d => d.loteNombre === subloteNombre);
    const last = fd[fd.length - 1];

    return {
      ...semana,
      datosDiarios: fd,
      consumosDiarios: fc,
      // AVES — Saldo anterior desde la semana previa
      saldoAnteriorHembras: saldoAnteriorH,
      saldoAnteriorMachos: saldoAnteriorM,
      // AVES — Entradas
      entradasHembras: fd.reduce((s, d) => s + d.entradasHembras, 0),
      entradasMachos: fd.reduce((s, d) => s + d.entradasMachos, 0),
      totalEntradas: fd.reduce((s, d) => s + d.entradasHembras + d.entradasMachos, 0),
      // AVES — Mortalidad
      mortalidadHembrasSemanal: fd.reduce((s, d) => s + d.mortalidadHembras, 0),
      mortalidadMachosSemanal: fd.reduce((s, d) => s + d.mortalidadMachos, 0),
      mortalidadTotalSemanal: fd.reduce((s, d) => s + d.mortalidadHembras + d.mortalidadMachos, 0),
      // AVES — Selección
      seleccionHembrasSemanal: fd.reduce((s, d) => s + d.seleccionHembras, 0),
      seleccionMachosSemanal: fd.reduce((s, d) => s + d.seleccionMachos, 0),
      totalSeleccionSemanal: fd.reduce((s, d) => s + d.seleccionHembras + d.seleccionMachos, 0),
      // AVES — Ventas
      ventasHembrasSemanal: fd.reduce((s, d) => s + d.ventasHembras, 0),
      ventasMachosSemanal: fd.reduce((s, d) => s + d.ventasMachos, 0),
      totalVentasSemanal: fd.reduce((s, d) => s + d.ventasHembras + d.ventasMachos, 0),
      // AVES — Traslados
      trasladosHembrasSemanal: fd.reduce((s, d) => s + d.trasladosHembras, 0),
      trasladosMachosSemanal: fd.reduce((s, d) => s + d.trasladosMachos, 0),
      totalTrasladosSemanal: fd.reduce((s, d) => s + d.trasladosHembras + d.trasladosMachos, 0),
      // AVES — Saldo final
      saldoFinHembras: last?.saldoHembras ?? 0,
      saldoFinMachos: last?.saldoMachos ?? 0,
      totalAvesVivas: (last?.saldoHembras ?? 0) + (last?.saldoMachos ?? 0),
      // BULTO
      saldoBultosAnterior: fd.length > 0 ? fd[0].saldoBultosAnterior : 0,
      trasladosBultosSemanal: fd.reduce((s, d) => s + d.trasladosBultos, 0),
      entradasBultosSemanal: fd.reduce((s, d) => s + d.entradasBultos, 0),
      retirosBultosSemanal: fd.reduce((s, d) => s + d.retirosBultos, 0),
      consumoBultosHembrasSemanal: fd.reduce((s, d) => s + d.consumoBultosHembras, 0),
      consumoBultosMachosSemanal: fd.reduce((s, d) => s + d.consumoBultosMachos, 0),
      saldoBultosFinal: last?.saldoBultos ?? 0,
      // CONSUMO
      consumoTotalAlimento: fc.reduce((s, d) => s + d.consumoAlimento, 0),
      consumoTotalAgua: fc.reduce((s, d) => s + d.consumoAgua, 0),
      consumoTotalMedicamento: fc.reduce((s, d) => s + d.consumoMedicamento, 0),
      consumoTotalVacuna: fc.reduce((s, d) => s + d.consumoVacuna, 0),
      otrosConsumos: fc.reduce((s, d) => s + d.otrosConsumos, 0),
      totalGeneral: fc.reduce((s, d) => s + d.totalConsumo, 0),
    };
  }

  // Semanas filtradas y proyectadas para el sub-lote activo
  get semanasParaSubloteActual(): ReporteContableSemanalDto[] {
    const r = this.reporte();
    if (!r) return [];
    if (!this.selectedSublote) return r.reportesSemanales;

    let prevSaldoH = 0, prevSaldoM = 0;
    return r.reportesSemanales.map(semana => {
      const projected = this.proyectarSemanaParaSublote(semana, this.selectedSublote!, prevSaldoH, prevSaldoM);
      const last = semana.datosDiarios.filter(d => d.loteNombre === this.selectedSublote).at(-1);
      prevSaldoH = last?.saldoHembras ?? prevSaldoH;
      prevSaldoM = last?.saldoMachos ?? prevSaldoM;
      return projected;
    });
  }

  // Movimientos de huevos filtrados para el sub-lote activo
  get movimientosHuevosParaSubloteActual(): ReporteMovimientosHuevosDto | null {
    const r = this.reporteMovimientosHuevos();
    if (!r) return null;
    if (!this.selectedSublote) return r;

    const filtered = r.movimientosDiarios.filter(m => m.loteNombre === this.selectedSublote);
    return {
      ...r,
      movimientosDiarios: filtered,
      totalPostura: filtered.reduce((s, m) => s + m.postura, 0),
      totalHvtoFertil: filtered.reduce((s, m) => s + m.hvtoFertil, 0),
      totalHvoComercial: filtered.reduce((s, m) => s + m.hvoComercial, 0),
      totalHuevoDesecho: filtered.reduce((s, m) => s + m.huevoDesecho, 0),
      totalEntrada: filtered.reduce((s, m) => s + m.entrada, 0),
      totalVenta: filtered.reduce((s, m) => s + m.venta, 0),
      totalSalida: filtered.reduce((s, m) => s + m.salida, 0),
      totalTrasladoAPlanta: filtered.reduce((s, m) => s + m.trasladoAPlanta, 0),
      totalDescarte: filtered.reduce((s, m) => s + m.descarte, 0),
    };
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
          // Auto-seleccionar el primer sublote disponible
          const names = new Set<string>();
          reporte.reportesSemanales.forEach(s =>
            s.datosDiarios.forEach(d => { if (d.loteNombre) names.add(d.loteNombre); })
          );
          const lista = Array.from(names).sort();
          this.selectedSublote = lista[0] ?? null;
          this.activeTab = 'resumen';
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
    this.selectedSublote = null;
  }

  // ================== TABS INTERNOS ==================

  setActiveTab(tab: 'resumen' | 'movimientos-huevos' | number): void {
    this.activeTab = tab;
  }

  isTabActive(tab: 'resumen' | 'movimientos-huevos' | number): boolean {
    return this.activeTab === tab;
  }

  getTabLabel(semana: number): string {
    const semanas = this.semanasParaSubloteActual;
    const r = semanas.find(r => r.semanaContable === semana);
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

  // ================== TOTALES (calculados sobre el sublote activo) ==================

  getTotalMortalidad(): number {
    return this.semanasParaSubloteActual.reduce((t, s) => t + s.mortalidadHembrasSemanal + s.mortalidadMachosSemanal, 0);
  }

  getTotalTraslados(): number {
    return this.semanasParaSubloteActual.reduce((t, s) => t + s.trasladosHembrasSemanal + s.trasladosMachosSemanal, 0);
  }

  getTotalVentas(): number {
    return this.semanasParaSubloteActual.reduce((t, s) => t + s.ventasHembrasSemanal + s.ventasMachosSemanal, 0);
  }

  getTotalAlimento(): number {
    return this.semanasParaSubloteActual.reduce((t, s) => t + s.consumoTotalAlimento, 0);
  }

  getTotalAgua(): number {
    return this.semanasParaSubloteActual.reduce((t, s) => t + s.consumoTotalAgua, 0);
  }

  getTotalMedicamento(): number {
    return this.semanasParaSubloteActual.reduce((t, s) => t + s.consumoTotalMedicamento, 0);
  }

  getTotalVacuna(): number {
    return this.semanasParaSubloteActual.reduce((t, s) => t + s.consumoTotalVacuna, 0);
  }

  getTotalOtros(): number {
    return this.semanasParaSubloteActual.reduce((t, s) => t + s.otrosConsumos, 0);
  }

  getTotalGeneral(): number {
    return this.semanasParaSubloteActual.reduce((t, s) => t + s.totalGeneral, 0);
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

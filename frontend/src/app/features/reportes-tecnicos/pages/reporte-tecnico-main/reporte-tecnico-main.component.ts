// src/app/features/reportes-tecnicos/pages/reporte-tecnico-main/reporte-tecnico-main.component.ts
import { Component, OnInit, OnDestroy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faFileExcel,
  faCalendarDay,
  faCalendarWeek,
  faDownload,
  faSpinner,
  faSearch,
  faFilter
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil, finalize } from 'rxjs';

import {
  ReporteTecnicoService,
  ReporteTecnicoLevanteCompletoDto,
  ObtenerReporteLevanteRequestDto,
  ReporteTecnicoProduccionCompletoDto,
  ObtenerReporteProduccionRequestDto,
  ReporteTecnicoProduccionTabsDto,
  ExportarExcelMetaDto
} from '../../services/reporte-tecnico.service';
import { ReporteTecnicoLevanteFilterService } from '../../services/reporte-tecnico-levante-filter.service';
import { TablaLevanteSemanalHembrasComponent } from '../../components/tabla-levante-semanal-hembras/tabla-levante-semanal-hembras.component';
import { TablaLevanteSemanalMachosComponent } from '../../components/tabla-levante-semanal-machos/tabla-levante-semanal-machos.component';
import { ReportesTabsComponent } from '../../components/reportes-tabs/reportes-tabs.component';

@Component({
  selector: 'app-reporte-tecnico-main',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    FontAwesomeModule,
    TablaLevanteSemanalHembrasComponent,
    TablaLevanteSemanalMachosComponent,
    ReportesTabsComponent
  ],
  templateUrl: './reporte-tecnico-main.component.html',
  styleUrls: ['./reporte-tecnico-main.component.scss']
})
export class ReporteTecnicoMainComponent implements OnInit, OnDestroy {
  faFileExcel    = faFileExcel;
  faCalendarDay  = faCalendarDay;
  faCalendarWeek = faCalendarWeek;
  faDownload     = faDownload;
  faSpinner      = faSpinner;
  faSearch       = faSearch;
  faFilter       = faFilter;

  readonly filterSvc        = inject(ReporteTecnicoLevanteFilterService);
  private readonly reporteService = inject(ReporteTecnicoService);

  loading               = signal(false);
  reporteLevante        = signal<ReporteTecnicoLevanteCompletoDto | null>(null);
  reporteProduccion     = signal<ReporteTecnicoProduccionCompletoDto | null>(null);
  reporteProduccionTabs = signal<ReporteTecnicoProduccionTabsDto | null>(null);

  // Paso 5: tipo de reporte
  tipoReporte: 'consolidado' | 'sublote' = 'consolidado';

  // Paso 8: fechas
  fechaInicio: string = '';
  fechaFin: string    = '';

  // Tabs activos
  tabLevanteActivo: 'semanal-hembras' | 'semanal-machos' | 'reporte-guia' | 'diario' = 'reporte-guia';
  tabProduccionActivo: 'semanal' | 'diario' = 'semanal';

  error: string | null     = null;
  mostrarFormulas: boolean = false;

  private destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.filterSvc.loadFilterData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── PASO 0: Etapa ────────────────────────────────────────────────────────────

  onEtapaChange(etapa: 'LEVANTE' | 'PRODUCCION'): void {
    this.filterSvc.setEtapa(etapa);
    this.tipoReporte = 'consolidado';
    this.limpiarReporte();
  }

  // ── PASOS 1-3: Ubicación ─────────────────────────────────────────────────────

  onGranjaChange(id: number | null): void {
    this.filterSvc.setGranja(id);
    this.limpiarReporte();
  }

  onNucleoChange(id: string | null): void {
    this.filterSvc.setNucleo(id);
    this.limpiarReporte();
  }

  onGalponChange(id: string | null): void {
    this.filterSvc.setGalpon(id);
    this.limpiarReporte();
  }

  // ── PASO 4: Lote Base ────────────────────────────────────────────────────────

  onLoteBaseChange(id: number | null): void {
    this.filterSvc.setLoteBase(id);
    this.limpiarReporte();
  }

  // ── PASOS 5-7: Configuración del reporte ─────────────────────────────────────

  onTipoReporteChange(): void {
    this.filterSvc.setSublote(null);
    this.limpiarReporte();
  }

  onSubloteChange(id: number | null): void {
    this.filterSvc.setSublote(id);
    this.limpiarReporte();
  }

  onPeriodicidadChange(p: 'Semanal' | 'Diario'): void {
    this.filterSvc.setPeriodicidad(p);
    this.limpiarReporte();
  }

  // ── Validación y generación ───────────────────────────────────────────────────

  puedeGenerarReporte(): boolean {
    if (!this.filterSvc.selectedLoteBaseId()) return false;
    if (this.tipoReporte === 'sublote' && !this.filterSvc.selectedSubloteId()) return false;
    return true;
  }

  generarReporte(): void {
    if (!this.validarFiltros()) return;
    if (this.filterSvc.selectedEtapa() === 'LEVANTE') {
      this._generarReporteLevante();
    } else {
      this._generarReporteProduccion();
    }
  }

  private _generarReporteLevante(): void {
    this.loading.set(true);
    this.error = null;

    const request: ObtenerReporteLevanteRequestDto = {
      lotePosturaBaseId: this.filterSvc.selectedLoteBaseId()!,
      loteLevanteId: this.tipoReporte === 'sublote'
        ? (this.filterSvc.selectedSubloteId() ?? null)
        : null,
      filtroPeriodicidad: this.filterSvc.selectedPeriodicidad(),
      fechaInicio: this.fechaInicio || null,
      fechaFin:    this.fechaFin    || null,
    };

    this.reporteService.obtenerReporteLevante(request)
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: (reporte) => {
          this.reporteLevante.set(reporte);
          this.error = null;
          this.tabLevanteActivo = this.filterSvc.selectedPeriodicidad() === 'Semanal'
            ? 'reporte-guia'
            : 'diario';
        },
        error: (err) => {
          this.error = err.error?.message || 'Error al generar el reporte de levante';
          this.reporteLevante.set(null);
        }
      });
  }

  private _generarReporteProduccion(): void {
    this.loading.set(true);
    this.error = null;

    const request: ObtenerReporteProduccionRequestDto = {
      lotePosturaBaseId: this.filterSvc.selectedLoteBaseId()!,
      lotePosturaProduccionId: this.tipoReporte === 'sublote'
        ? (this.filterSvc.selectedSubloteId() ?? null)
        : null,
      filtroPeriodicidad: this.filterSvc.selectedPeriodicidad(),
      fechaInicio: this.fechaInicio || null,
      fechaFin:    this.fechaFin    || null,
    };

    this.reporteService.obtenerReporteProduccionTabs(request)
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: (reporte) => {
          this.reporteProduccionTabs.set(reporte);
          this.error = null;
        },
        error: (err) => {
          this.error = err.error?.message || 'Error al generar el reporte de producción';
          this.reporteProduccionTabs.set(null);
        }
      });
  }

  puedeDescargarExcel(): boolean {
    const etapa = this.filterSvc.selectedEtapa();
    if (etapa === 'LEVANTE')    return !!this.reporteLevante();
    if (etapa === 'PRODUCCION') return !!this.reporteProduccionTabs();
    return false;
  }

  exportarExcel(): void {
    const etapa = this.filterSvc.selectedEtapa();
    if (etapa === 'LEVANTE')    { this._exportarLevante();    return; }
    if (etapa === 'PRODUCCION') { this._exportarProduccion(); return; }
  }

  private _buildMeta(): ExportarExcelMetaDto {
    const loteInfo = this.reporteLevante()?.informacionLote
                  ?? this.reporteProduccionTabs()?.loteInfo;
    return {
      etapa:             this.filterSvc.selectedEtapa(),
      loteBaseNombre:    this.filterSvc.selectedLoteNombre() || loteInfo?.loteNombre || 'Lote',
      loteSubloteNombre: this.tipoReporte === 'sublote'
        ? (this.filterSvc.selectedSubloteNombre() || null)
        : null,
      granjaNombre:      loteInfo?.granjaNombre ?? null,
      nucleoNombre:      loteInfo?.nucleoNombre ?? null,
      fechaInicio:       this.fechaInicio || null,
      fechaFin:          this.fechaFin    || null,
      totalAvesInicio:   (loteInfo as any)?.numeroHembras
                      ?? (loteInfo as any)?.numeroHembrasIniciales
                      ?? null,
      periodicidad:      this.filterSvc.selectedPeriodicidad(),
    };
  }

  private _exportarLevante(): void {
    if (!this.reporteLevante()) return;
    this.loading.set(true);
    this.error = null;

    this.reporteService.exportarExcelLevanteNuevo(this.reporteLevante()!, this._buildMeta())
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: (blob) => this._descargarBlob(blob, this._nombreArchivo()),
        error: (err) => { this.error = err.error?.message || 'Error al exportar Excel'; }
      });
  }

  private _exportarProduccion(): void {
    if (!this.reporteProduccionTabs()) return;
    this.loading.set(true);
    this.error = null;

    this.reporteService.exportarExcelProduccionTabs(this.reporteProduccionTabs()!, this._buildMeta())
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: (blob) => this._descargarBlob(blob, this._nombreArchivo()),
        error: (err) => { this.error = err.error?.message || 'Error al exportar Excel'; }
      });
  }

  private _nombreArchivo(): string {
    const etapa    = this.filterSvc.selectedEtapa();
    const base     = (this.filterSvc.selectedLoteNombre() || 'LOTE').replace(/\s+/g, '_');
    const sublote  = this.tipoReporte === 'sublote'
      ? (this.filterSvc.selectedSubloteNombre() || '').replace(/\s+/g, '_')
      : '';
    const ahora    = new Date();
    const fecha    = ahora.toISOString().slice(0, 10).replace(/-/g, '');
    const hora     = ahora.toTimeString().slice(0, 8).replace(/:/g, '');
    return sublote
      ? `${etapa}_${base}_${sublote}_${fecha}_${hora}.xlsx`
      : `${etapa}_${base}_${fecha}_${hora}.xlsx`;
  }

  private _descargarBlob(blob: Blob, filename: string): void {
    const url  = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href  = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
    this.error = null;
  }

  private validarFiltros(): boolean {
    const etapa = this.filterSvc.selectedEtapa();

    if (!this.filterSvc.selectedLoteBaseId()) {
      this.error = 'Debe seleccionar un Lote Base';
      return false;
    }
    if (this.tipoReporte === 'sublote' && !this.filterSvc.selectedSubloteId()) {
      this.error = etapa === 'LEVANTE'
        ? 'Debe seleccionar un sublote para el tipo "Por Sublote"'
        : 'Debe seleccionar un lote de producción para el tipo "Por Lote"';
      return false;
    }
    if (this.fechaInicio && this.fechaFin && new Date(this.fechaInicio) > new Date(this.fechaFin)) {
      this.error = 'La fecha de inicio debe ser anterior a la fecha de fin';
      return false;
    }
    return true;
  }

  limpiarReporte(): void {
    this.reporteLevante.set(null);
    this.reporteProduccion.set(null);
    this.reporteProduccionTabs.set(null);
    this.error = null;
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return 'N/A';
    return new Date(date).toLocaleDateString('es-ES', {
      day: '2-digit', month: '2-digit', year: 'numeric'
    });
  }

  calcularEdadDias(fechaEncaset: string | Date | null | undefined): number {
    if (!fechaEncaset) return 0;
    const encaset  = new Date(fechaEncaset);
    const diffTime = Date.now() - encaset.getTime();
    return Math.floor(diffTime / (1000 * 60 * 60 * 24));
  }

  semaforo(value: number | null | undefined, tipo: 'mort' | 'peso' | 'cons'): Record<string, boolean> {
    if (value === null || value === undefined || value === 0) return {};
    switch (tipo) {
      case 'mort': return { 'cell-danger': value > 0, 'cell-success': value < 0 };
      case 'peso': return { 'cell-success': value > 0, 'cell-danger': value < 0 };
      case 'cons': return { 'cell-warning': value > 0.5, 'cell-success': value <= 0 };
      default:     return {};
    }
  }

  get gruposFormulas() {
    return [
      {
        titulo: '📅 Información Básica',
        formulas: [
          { nombre: 'Edad en Días',    formula: 'Fecha Registro - Fecha Encaset' },
          { nombre: 'Edad en Semanas', formula: 'Edad en Días / 7 (redondeado hacia arriba)' },
          { nombre: 'Semana del Año',  formula: 'Semana del año calendario (ISO 8601)' }
        ]
      },
      {
        titulo: '🐔 Cálculos — HEMBRAS',
        formulas: [
          { nombre: '%MortH',    formula: '(MortH/HEMBRAINI)*100' },
          { nombre: 'DifMortH',  formula: '%MortH - %MortHGUIA' },
          { nombre: 'ConsAcGrH', formula: '(AcConsH*1000)/HEMBRAINI' },
          { nombre: 'GrAveDiaH', formula: 'SI(Hembra>0; (ConsKgH*1000)/Hembra/7; 0)' },
          { nombre: '%DifPesoH', formula: 'SI(PesoHGUIA>0; (PesoH-PesoHGUIA)/(PesoHGUIA*100); 0)' }
        ]
      },
      {
        titulo: '🐓 Cálculos — MACHOS',
        formulas: [
          { nombre: '%MortM',    formula: '(MortM/MACHOINI)*100' },
          { nombre: 'DifMortM',  formula: '%MortM - %MortMGUIA' },
          { nombre: 'ConsAcGrM', formula: '(AcConsM*1000)/MACHOINI' },
          { nombre: 'GrAveDiaM', formula: 'SI(SaldoMacho>0; (ConsKgM*1000)/SaldoMacho/7; 0)' },
          { nombre: 'DifConsM',  formula: 'ConsAcGrM - ConsAcGrMGUIA' }
        ]
      }
    ];
  }
}

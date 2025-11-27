// src/app/features/reportes-tecnicos/pages/reporte-tecnico-main/reporte-tecnico-main.component.ts
import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faFileExcel, 
  faFileAlt, 
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
  ReporteTecnicoCompletoDto,
  GenerarReporteTecnicoRequestDto 
} from '../../services/reporte-tecnico.service';
import {
  ReporteTecnicoProduccionService,
  ReporteTecnicoProduccionCompletoDto,
  GenerarReporteTecnicoProduccionRequestDto
} from '../../services/reporte-tecnico-produccion.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { TablaDatosDiariosComponent } from '../../components/tabla-datos-diarios/tabla-datos-diarios.component';
import { TablaDatosSemanalesComponent } from '../../components/tabla-datos-semanales/tabla-datos-semanales.component';
import { TablaDatosDiariosProduccionComponent } from '../../components/tabla-datos-diarios-produccion/tabla-datos-diarios-produccion.component';
import { TablaDatosSemanalesProduccionComponent } from '../../components/tabla-datos-semanales-produccion/tabla-datos-semanales-produccion.component';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FiltroSelectComponent } from '../../../lote-levante/pages/filtro-select/filtro-select.component';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../lote-levante/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';

@Component({
  selector: 'app-reporte-tecnico-main',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    FontAwesomeModule,
    DatePipe,
    SidebarComponent,
    FiltroSelectComponent,
    TablaDatosDiariosComponent,
    TablaDatosSemanalesComponent,
    TablaDatosDiariosProduccionComponent,
    TablaDatosSemanalesProduccionComponent
  ],
  templateUrl: './reporte-tecnico-main.component.html',
  styleUrls: ['./reporte-tecnico-main.component.scss']
})
export class ReporteTecnicoMainComponent implements OnInit, OnDestroy {
  // Iconos
  faFileExcel = faFileExcel;
  faFileAlt = faFileAlt;
  faCalendarDay = faCalendarDay;
  faCalendarWeek = faCalendarWeek;
  faDownload = faDownload;
  faSpinner = faSpinner;
  faSearch = faSearch;
  faFilter = faFilter;

  // Estado
  loading = signal(false);
  reporte = signal<ReporteTecnicoCompletoDto | null>(null);
  reporteProduccion = signal<ReporteTecnicoProduccionCompletoDto | null>(null);
  sublotes = signal<string[]>([]);
  
  // Filtros de selección (granja, núcleo, galpón, lote)
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;
  
  // Catálogos
  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  private allLotes: LoteDto[] = [];
  selectedLote: LoteDto | null = null;
  
  // Tab activo
  tabActivo: 'levante' | 'produccion' = 'levante';

  // Filtros de reporte
  tipoReporte: 'diario' | 'semanal' = 'diario';
  tipoConsolidacion: 'sublote' | 'consolidado' = 'sublote';
  loteNombreBase: string = '';
  subloteSeleccionado: string | null = null;
  fechaInicio: string = '';
  fechaFin: string = '';
  semana: number | null = null;
  
  // UI
  error: string | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private reporteService: ReporteTecnicoService,
    private reporteProduccionService: ReporteTecnicoProduccionService,
    private loteService: LoteService,
    private farmService: FarmService,
    private nucleoService: NucleoService,
    private galponService: GalponService
  ) {}

  ngOnInit(): void {
    this.cargarGranjas();
    this.establecerFechasPorDefecto();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private establecerFechasPorDefecto(): void {
    const hoy = new Date();
    const hace30Dias = new Date();
    hace30Dias.setDate(hoy.getDate() - 30);
    
    this.fechaFin = hoy.toISOString().split('T')[0];
    this.fechaInicio = hace30Dias.toISOString().split('T')[0];
  }

  private cargarGranjas(): void {
    this.farmService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (granjas) => {
          this.granjas = granjas || [];
        },
        error: (err) => {
          console.error('Error al cargar granjas:', err);
          this.granjas = [];
        }
      });
  }

  // ================== EVENTOS DE FILTRO ==================
  onGranjaChange(granjaId: number | null): void {
    this.selectedGranjaId = granjaId;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.selectedLote = null;
    this.reporte.set(null);
    this.nucleos = [];

    if (!this.selectedGranjaId) return;

    this.nucleoService.getByGranja(this.selectedGranjaId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows) => (this.nucleos = rows || []),
        error: () => (this.nucleos = [])
      });

    this.reloadLotes();
  }

  onNucleoChange(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.selectedLote = null;
    this.reporte.set(null);
    this.applyFiltersToLotes();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteId = null;
    this.selectedLote = null;
    this.reporte.set(null);
    this.applyFiltersToLotes();
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLoteId = loteId;
    this.reporte.set(null);
    this.reporteProduccion.set(null);

    if (!this.selectedLoteId) {
      this.selectedLote = null;
      return;
    }

    this.loteService.getById(this.selectedLoteId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (lote) => {
          this.selectedLote = lote || null;
          if (lote) {
            const nombreBase = this.extraerNombreBase(lote.loteNombre);
            this.loteNombreBase = nombreBase;
            this.cargarSublotes(nombreBase);
          }
        },
        error: () => (this.selectedLote = null)
      });
  }

  private reloadLotes(): void {
    if (!this.selectedGranjaId) {
      this.allLotes = [];
      return;
    }

    this.loteService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (all) => {
          this.allLotes = all || [];
          this.applyFiltersToLotes();
        },
        error: () => {
          this.allLotes = [];
        }
      });
  }

  private applyFiltersToLotes(): void {
    // Los lotes se filtran automáticamente por el componente filtro-select
    // Aquí solo necesitamos asegurarnos de que tenemos los datos
  }

  private extraerNombreBase(loteNombre: string): string {
    // Extraer el nombre base del lote (ej: "K326 A" -> "K326")
    const partes = loteNombre.trim().split(' ');
    if (partes.length > 1 && partes[partes.length - 1].length === 1) {
      // La última parte es probablemente el sublote
      return partes.slice(0, -1).join(' ');
    }
    return loteNombre;
  }

  cargarSublotes(loteNombreBase: string): void {
    if (this.tabActivo === 'produccion') {
      this.reporteProduccionService.obtenerSublotes(loteNombreBase)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (sublotes) => {
            this.sublotes.set(sublotes);
          },
          error: (err) => {
            console.error('Error al cargar sublotes:', err);
          }
        });
    } else {
      this.reporteService.obtenerSublotes(loteNombreBase)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (sublotes) => {
            this.sublotes.set(sublotes);
          },
          error: (err) => {
            console.error('Error al cargar sublotes:', err);
          }
        });
    }
  }

  puedeGenerarReporte(): boolean {
    if (this.tabActivo === 'produccion') {
      if (this.tipoConsolidacion === 'consolidado') {
        // Para consolidado, puede usar loteId (nueva lógica) o loteNombreBase (compatibilidad)
        if (!this.selectedLoteId && !this.loteNombreBase.trim()) return false;
      } else {
        if (!this.selectedLoteId) return false;
      }
      if (this.tipoReporte === 'diario' && (!this.fechaInicio || !this.fechaFin)) return false;
      return true;
    } else {
      if (!this.selectedLoteId) return false;
      // Para consolidado, puede usar loteId (nueva lógica) o loteNombreBase (compatibilidad)
      if (this.tipoConsolidacion === 'consolidado' && !this.selectedLoteId && !this.loteNombreBase.trim()) return false;
      if (this.tipoReporte === 'diario' && (!this.fechaInicio || !this.fechaFin)) return false;
      return true;
    }
  }

  generarReporte(): void {
    if (!this.validarFiltros()) {
      return;
    }

    this.loading.set(true);
    this.error = null;

    if (this.tabActivo === 'produccion') {
      this.generarReporteProduccion();
    } else {
      this.generarReporteLevante();
    }
  }

  private generarReporteLevante(): void {
    const request: GenerarReporteTecnicoRequestDto = {
      incluirSemanales: this.tipoReporte === 'semanal',
      consolidarSublotes: this.tipoConsolidacion === 'consolidado'
    };

    if (this.tipoConsolidacion === 'consolidado') {
      // Para consolidado, usar loteId si está disponible (nueva lógica de lote padre)
      // o loteNombre como fallback (compatibilidad hacia atrás)
      if (this.selectedLoteId) {
        request.loteId = this.selectedLoteId;
      } else {
        request.loteNombre = this.loteNombreBase;
      }
    } else {
      if (!this.selectedLoteId) {
        this.error = 'Debe seleccionar un lote';
        this.loading.set(false);
        return;
      }
      request.loteId = this.selectedLoteId;
      request.sublote = this.subloteSeleccionado || undefined;
    }

    if (this.tipoReporte === 'diario') {
      request.fechaInicio = this.fechaInicio || undefined;
      request.fechaFin = this.fechaFin || undefined;
    }

    this.reporteService.generarReporte(request)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (reporte) => {
          this.reporte.set(reporte);
          this.reporteProduccion.set(null);
          this.error = null;
        },
        error: (err) => {
          console.error('Error al generar reporte:', err);
          this.error = err.error?.message || 'Error al generar el reporte';
          this.reporte.set(null);
        }
      });
  }

  private generarReporteProduccion(): void {
    const request: GenerarReporteTecnicoProduccionRequestDto = {
      tipoReporte: this.tipoReporte,
      tipoConsolidacion: this.tipoConsolidacion
    };

    if (this.tipoConsolidacion === 'consolidado') {
      // Para consolidado, usar loteId si está disponible (nueva lógica de lote padre)
      // o loteNombreBase como fallback (compatibilidad hacia atrás)
      if (this.selectedLoteId) {
        request.loteId = this.selectedLoteId;
      } else {
        request.loteNombreBase = this.loteNombreBase;
      }
    } else {
      if (!this.selectedLoteId) {
        this.error = 'Debe seleccionar un lote';
        this.loading.set(false);
        return;
      }
      request.loteId = this.selectedLoteId;
    }

    if (this.tipoReporte === 'diario') {
      request.fechaInicio = this.fechaInicio || undefined;
      request.fechaFin = this.fechaFin || undefined;
    } else {
      request.semana = this.semana || undefined;
    }

    this.reporteProduccionService.generarReporte(request)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (reporte) => {
          this.reporteProduccion.set(reporte);
          this.reporte.set(null);
          this.error = null;
        },
        error: (err) => {
          console.error('Error al generar reporte de producción:', err);
          this.error = err.error?.message || 'Error al generar el reporte';
          this.reporteProduccion.set(null);
        }
      });
  }

  exportarExcel(): void {
    if (this.tabActivo === 'produccion') {
      if (!this.reporteProduccion()) {
        this.error = 'Debe generar un reporte primero';
        return;
      }
      // TODO: Implementar exportación Excel para producción
      this.error = 'Exportación a Excel para producción aún no implementada';
      return;
    }

    if (!this.reporte()) {
      this.error = 'Debe generar un reporte primero';
      return;
    }

    this.loading.set(true);
    this.error = null;

    const request: GenerarReporteTecnicoRequestDto = {
      incluirSemanales: this.tipoReporte === 'semanal',
      consolidarSublotes: this.tipoConsolidacion === 'consolidado'
    };

    if (this.tipoConsolidacion === 'consolidado') {
      // Para consolidado, usar loteId si está disponible (nueva lógica de lote padre)
      // o loteNombre como fallback (compatibilidad hacia atrás)
      if (this.selectedLoteId) {
        request.loteId = this.selectedLoteId;
      } else {
        request.loteNombre = this.loteNombreBase;
      }
    } else {
      if (this.selectedLoteId) {
        request.loteId = this.selectedLoteId;
        request.sublote = this.subloteSeleccionado || undefined;
      }
    }

    if (this.tipoReporte === 'diario') {
      request.fechaInicio = this.fechaInicio || undefined;
      request.fechaFin = this.fechaFin || undefined;
    }

    const exportService = this.tipoReporte === 'diario'
      ? this.reporteService.exportarExcelDiario(request)
      : this.reporteService.exportarExcelSemanal(request);

    exportService
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          
          const reporte = this.reporte();
          const info = reporte?.informacionLote;
          const nombreBase = info?.loteNombre?.replace(/\s+/g, '_') || 'reporte';
          const sublote = info?.sublote ? `_${info.sublote}` : '';
          const raza = info?.raza?.replace(/\s+/g, '_') || '';
          const tipo = this.tipoReporte;
          const fecha = new Date().toISOString().split('T')[0];
          
          const nombreArchivo = reporte?.esConsolidado
            ? `Lote_${nombreBase}_General_${raza}_${tipo}_${fecha}.xlsx`
            : `Lote_${nombreBase}${sublote}_${raza}_${tipo}_${fecha}.xlsx`;
          
          a.download = nombreArchivo;
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
          window.URL.revokeObjectURL(url);
        },
        error: (err) => {
          console.error('Error al exportar Excel:', err);
          this.error = err.error?.message || 'Error al exportar el reporte a Excel';
        }
      });
  }

  private validarFiltros(): boolean {
    if (this.tipoConsolidacion === 'consolidado') {
      // Para consolidado, puede usar loteId (nueva lógica) o loteNombreBase (compatibilidad)
      if (!this.selectedLoteId && !this.loteNombreBase.trim()) {
        this.error = 'Debe seleccionar un lote o ingresar el nombre del lote base';
        return false;
      }
    } else {
      if (!this.selectedLoteId) {
        this.error = 'Debe seleccionar un lote';
        return false;
      }
    }

    if (this.tipoReporte === 'diario') {
      if (!this.fechaInicio || !this.fechaFin) {
        this.error = 'Debe seleccionar fecha de inicio y fin';
        return false;
      }
      if (new Date(this.fechaInicio) > new Date(this.fechaFin)) {
        this.error = 'La fecha de inicio debe ser anterior a la fecha de fin';
        return false;
      }
    }

    return true;
  }

  limpiarReporte(): void {
    this.reporte.set(null);
    this.reporteProduccion.set(null);
    this.error = null;
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return 'N/A';
    return new Date(date).toLocaleDateString('es-ES', { 
      day: '2-digit', 
      month: '2-digit', 
      year: 'numeric' 
    });
  }

  calcularEdadDias(fechaEncaset: string | Date | null | undefined): number {
    if (!fechaEncaset) return 0;
    const encaset = new Date(fechaEncaset);
    const hoy = new Date();
    const diffTime = hoy.getTime() - encaset.getTime();
    return Math.floor(diffTime / (1000 * 60 * 60 * 24));
  }

  // Getters para nombres
  get selectedGranjaName(): string {
    const g = this.granjas.find(x => x.id === this.selectedGranjaId);
    return g?.name ?? '';
  }

  get selectedNucleoNombre(): string {
    const n = this.nucleos.find(x => x.nucleoId === this.selectedNucleoId);
    return n?.nucleoNombre ?? '';
  }

  get selectedGalponNombre(): string {
    // El componente filtro-select maneja esto
    return '';
  }

  get selectedLoteNombre(): string {
    return this.selectedLote?.loteNombre ?? '';
  }

  extraerSublote(loteNombre: string): string {
    const partes = loteNombre.trim().split(' ');
    if (partes.length > 1 && partes[partes.length - 1].length === 1) {
      return partes[partes.length - 1];
    }
    return '';
  }
}


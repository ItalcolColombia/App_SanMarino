// src/app/features/reporte-contable/pages/reporte-contable-main/reporte-contable-main.component.ts
import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
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
  GenerarReporteContableRequestDto 
} from '../../services/reporte-contable.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FiltroSelectComponent } from '../../../lote-levante/pages/filtro-select/filtro-select.component';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../lote-levante/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { TablaDetalleDiarioContableComponent } from '../../components/tabla-detalle-diario-contable/tabla-detalle-diario-contable.component';
import { TablaAvesContableComponent } from '../../components/tabla-aves-contable/tabla-aves-contable.component';
import { TablaBultosContableComponent } from '../../components/tabla-bultos-contable/tabla-bultos-contable.component';

@Component({
  selector: 'app-reporte-contable-main',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    FontAwesomeModule,
    SidebarComponent,
    FiltroSelectComponent,
    TablaDetalleDiarioContableComponent,
    TablaAvesContableComponent,
    TablaBultosContableComponent
  ],
  templateUrl: './reporte-contable-main.component.html',
  styleUrls: ['./reporte-contable-main.component.scss']
})
export class ReporteContableMainComponent implements OnInit, OnDestroy {
  // Iconos
  faFileExcel = faFileExcel;
  faFileAlt = faFileAlt;
  faCalendarWeek = faCalendarWeek;
  faDownload = faDownload;
  faSpinner = faSpinner;
  faSearch = faSearch;
  faFilter = faFilter;
  faDollarSign = faDollarSign;

  // Estado
  loading = signal(false);
  reporte = signal<ReporteContableCompletoDto | null>(null);
  
  // Filtros de selección (granja, núcleo, galpón, lote)
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;
  
  // Catálogos
  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  private allLotes: LoteDto[] = [];
  lotesPadres: LoteDto[] = []; // Solo lotes padres (sin lotePadreId)
  selectedLote: LoteDto | null = null;
  
  // Filtros de reporte
  semanaContable: number | null = null;
  semanasContablesDisponibles: number[] = [];
  
  // UI
  error: string | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private reporteContableService: ReporteContableService,
    private loteService: LoteService,
    private farmService: FarmService,
    private nucleoService: NucleoService,
    private galponService: GalponService
  ) {}

  ngOnInit(): void {
    this.cargarGranjas();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
    this.lotesPadres = [];

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
    this.semanaContable = null;
    this.semanasContablesDisponibles = [];

    if (!this.selectedLoteId) {
      this.selectedLote = null;
      return;
    }

    // Verificar que el lote seleccionado sea un lote padre
    const lote = this.lotesPadres.find(l => l.loteId === this.selectedLoteId);
    if (!lote) {
      this.error = 'Debe seleccionar un lote padre para generar el reporte contable';
      this.selectedLoteId = null;
      return;
    }

    this.selectedLote = lote;
    this.cargarSemanasContables();
  }

  private reloadLotes(): void {
    if (!this.selectedGranjaId) {
      this.allLotes = [];
      this.lotesPadres = [];
      return;
    }

    this.loteService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (lotes) => {
          this.allLotes = lotes || [];
          this.applyFiltersToLotes();
        },
        error: () => {
          this.allLotes = [];
          this.lotesPadres = [];
        }
      });
  }

  private applyFiltersToLotes(): void {
    let filtrados = [...this.allLotes];

    // Filtrar por granja
    if (this.selectedGranjaId) {
      filtrados = filtrados.filter(l => l.granjaId === this.selectedGranjaId);
    }

    // Filtrar por núcleo
    if (this.selectedNucleoId) {
      filtrados = filtrados.filter(l => l.nucleoId === this.selectedNucleoId);
    }

    // Filtrar por galpón
    if (this.selectedGalponId) {
      filtrados = filtrados.filter(l => l.galponId === this.selectedGalponId);
    }

    // Solo lotes padres (sin lotePadreId)
    this.lotesPadres = filtrados.filter(l => !l.lotePadreId);
  }

  private cargarSemanasContables(): void {
    if (!this.selectedLoteId) return;

    this.reporteContableService.obtenerSemanasContables(this.selectedLoteId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (semanas) => {
          this.semanasContablesDisponibles = semanas || [];
          if (semanas && semanas.length > 0) {
            // Seleccionar la última semana por defecto
            this.semanaContable = semanas[semanas.length - 1];
          }
        },
        error: (err) => {
          console.error('Error al cargar semanas contables:', err);
          this.semanasContablesDisponibles = [];
        }
      });
  }

  // ================== GENERAR REPORTE ==================
  generarReporte(): void {
    if (!this.validarFiltros()) {
      return;
    }

    this.loading.set(true);
    this.error = null;

    const request: GenerarReporteContableRequestDto = {
      lotePadreId: this.selectedLoteId!,
      semanaContable: this.semanaContable || undefined
    };

    this.reporteContableService.generarReporte(request)
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (reporte) => {
          this.reporte.set(reporte);
          this.error = null;
        },
        error: (err) => {
          console.error('Error al generar reporte contable:', err);
          this.error = err.error?.message || 'Error al generar el reporte contable';
          this.reporte.set(null);
        }
      });
  }

  private validarFiltros(): boolean {
    if (!this.selectedLoteId) {
      this.error = 'Debe seleccionar un lote padre';
      return false;
    }

    // Verificar que el lote seleccionado sea un lote padre
    const lote = this.lotesPadres.find(l => l.loteId === this.selectedLoteId);
    if (!lote) {
      this.error = 'Debe seleccionar un lote padre (no un sublote)';
      return false;
    }

    this.error = null;
    return true;
  }

  // ================== EXPORTAR EXCEL ==================
  exportarExcel(): void {
    if (!this.validarFiltros()) {
      return;
    }

    this.loading.set(true);
    this.error = null;

    const request: GenerarReporteContableRequestDto = {
      lotePadreId: this.selectedLoteId!,
      semanaContable: this.semanaContable || undefined
    };

    this.reporteContableService.exportarExcel(request)
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          
          const reporte = this.reporte();
          const nombreArchivo = reporte 
            ? `Reporte_Contable_${reporte.lotePadreNombre}_Semana_${this.semanaContable || 'Actual'}.xlsx`
            : `Reporte_Contable_${this.selectedLoteId}_${new Date().toISOString().split('T')[0]}.xlsx`;
          
          link.download = nombreArchivo;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);
        },
        error: (err) => {
          console.error('Error al exportar reporte contable:', err);
          this.error = err.error?.message || 'Error al exportar el reporte contable';
        }
      });
  }

  limpiarReporte(): void {
    this.reporte.set(null);
    this.error = null;
  }

  // ================== COMPUTED PROPERTIES ==================
  get selectedGranjaName(): string {
    if (!this.selectedGranjaId) return '';
    return this.granjas.find(g => g.id === this.selectedGranjaId)?.name || '';
  }

  get selectedNucleoNombre(): string {
    if (!this.selectedNucleoId) return '';
    return this.nucleos.find(n => n.nucleoId === this.selectedNucleoId)?.nucleoNombre || '';
  }

  get selectedGalponNombre(): string {
    // TODO: Implementar si es necesario
    return this.selectedGalponId || '';
  }

  get selectedLoteNombre(): string {
    return this.selectedLote?.loteNombre || '';
  }

  get isFormValid(): boolean {
    return this.validarFiltros();
  }

  puedeGenerarReporte(): boolean {
    return this.validarFiltros();
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return 'N/A';
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }

}


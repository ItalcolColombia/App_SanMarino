// src/app/features/reporte-tecnico-produccion/pages/reporte-tecnico-produccion-main/reporte-tecnico-produccion-main.component.ts
import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faFileExcel, 
  faDownload,
  faSpinner,
  faSearch
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil, finalize } from 'rxjs';

import { 
  ReporteTecnicoProduccionService, 
  ReporteTecnicoProduccionCompletoDto,
  ReporteTecnicoProduccionCuadroCompletoDto,
  ReporteClasificacionHuevoComercioCompletoDto
} from '../../services/reporte-tecnico-produccion.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FiltroSelectComponent } from '../../../lote-levante/pages/filtro-select/filtro-select.component';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../lote-levante/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { TablaReporteDiarioProduccionComponent } from '../../components/tabla-reporte-diario-produccion/tabla-reporte-diario-produccion.component';
import { TablaReporteCuadroProduccionComponent } from '../../components/tabla-reporte-cuadro-produccion/tabla-reporte-cuadro-produccion.component';
import { TablaClasificacionHuevoComercioComponent } from '../../components/tabla-clasificacion-huevo-comercio/tabla-clasificacion-huevo-comercio.component';

@Component({
  selector: 'app-reporte-tecnico-produccion-main',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    FontAwesomeModule,
    SidebarComponent,
    FiltroSelectComponent,
    TablaReporteDiarioProduccionComponent,
    TablaReporteCuadroProduccionComponent,
    TablaClasificacionHuevoComercioComponent
  ],
  templateUrl: './reporte-tecnico-produccion-main.component.html',
  styleUrls: ['./reporte-tecnico-produccion-main.component.scss']
})
export class ReporteTecnicoProduccionMainComponent implements OnInit, OnDestroy {
  // Iconos
  faFileExcel = faFileExcel;
  faDownload = faDownload;
  faSpinner = faSpinner;
  faSearch = faSearch;

  // Estado
  loading = signal(false);
  reporte = signal<ReporteTecnicoProduccionCompletoDto | null>(null);
  reporteCuadro = signal<ReporteTecnicoProduccionCuadroCompletoDto | null>(null);
  reporteClasificacion = signal<ReporteClasificacionHuevoComercioCompletoDto | null>(null);
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
  
  // Filtros de reporte
  tipoConsolidacion: 'sublote' | 'consolidado' = 'sublote';
  loteNombreBase: string = '';
  fechaInicio: string = '';
  fechaFin: string = '';
  
  // Tab activo interno
  tabActivo: 'diario' | 'cuadro' | 'clasificacion' = 'diario';
  
  // UI
  error: string | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private reporteService: ReporteTecnicoProduccionService,
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
  }

  private extraerNombreBase(loteNombre: string): string {
    const partes = loteNombre.trim().split(' ');
    if (partes.length > 1 && partes[partes.length - 1].length === 1) {
      return partes.slice(0, -1).join(' ');
    }
    return loteNombre;
  }

  cargarSublotes(loteNombreBase: string): void {
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

  puedeGenerarReporte(): boolean {
    if (!this.selectedLoteId) return false;
    if (this.tipoConsolidacion === 'consolidado' && !this.selectedLoteId && !this.loteNombreBase.trim()) return false;
    return true;
  }

  generarReporte(): void {
    if (!this.validarFiltros()) {
      return;
    }

    this.loading.set(true);
    this.error = null;

    if (!this.selectedLoteId) {
      this.error = 'Debe seleccionar un lote';
      this.loading.set(false);
      return;
    }

    // Generar reporte diario de producción
    const fechaInicio = this.fechaInicio ? new Date(this.fechaInicio).toISOString() : null;
    const fechaFin = this.fechaFin ? new Date(this.fechaFin).toISOString() : null;

    // Generar ambos reportes en paralelo
    const reporteDiario$ = this.reporteService.generarReporteDiario(
      this.selectedLoteId,
      fechaInicio,
      fechaFin,
      this.tipoConsolidacion === 'consolidado'
    );

    const reporteCuadro$ = this.reporteService.generarReporteCuadro(
      this.selectedLoteId,
      fechaInicio,
      fechaFin,
      this.tipoConsolidacion === 'consolidado'
    );

    reporteDiario$
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (reporte) => {
          this.reporte.set(reporte);
          this.error = null;
        },
        error: (err) => {
          console.error('Error al generar reporte de producción:', err);
          this.error = err.error?.message || 'Error al generar el reporte';
          this.reporte.set(null);
        }
      });

    reporteCuadro$
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (reporte) => {
          this.reporteCuadro.set(reporte);
        },
        error: (err) => {
          console.error('Error al generar reporte cuadro:', err);
          // No mostrar error si falla el cuadro, solo el diario es crítico
        }
      });

    // Generar reporte de clasificación de huevos
    const reporteClasificacion$ = this.reporteService.generarReporteClasificacionHuevoComercio(
      this.selectedLoteId,
      fechaInicio,
      fechaFin,
      this.tipoConsolidacion === 'consolidado'
    );

    reporteClasificacion$
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (reporte) => {
          this.reporteClasificacion.set(reporte);
        },
        error: (err) => {
          console.error('Error al generar reporte clasificación:', err);
          // No mostrar error si falla la clasificación, solo el diario es crítico
        }
      });
  }

  exportarExcel(): void {
    if (!this.selectedLoteId) {
      this.error = 'Debe seleccionar un lote primero';
      return;
    }

    this.loading.set(true);
    this.error = null;

    const fechaInicio = this.fechaInicio ? new Date(this.fechaInicio).toISOString() : null;
    const fechaFin = this.fechaFin ? new Date(this.fechaFin).toISOString() : null;

    this.reporteService.exportarExcelCompleto(
      this.selectedLoteId,
      fechaInicio,
      fechaFin,
      this.tipoConsolidacion === 'consolidado'
    )
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (blob) => {
          // Crear URL del blob y descargar
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          
          // Generar nombre de archivo
          const loteNombre = this.selectedLoteNombre.replace(/\s+/g, '_');
          const fecha = new Date().toISOString().slice(0, 19).replace(/:/g, '-');
          link.download = `Reporte_Tecnico_Produccion_${loteNombre}_${fecha}.xlsx`;
          
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);
          
          this.error = null;
        },
        error: (err) => {
          console.error('Error al exportar a Excel:', err);
          this.error = err.error?.message || 'Error al exportar el archivo Excel';
        }
      });
  }

  private validarFiltros(): boolean {
    if (this.tipoConsolidacion === 'consolidado') {
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

    return true;
  }

  limpiarReporte(): void {
    this.reporte.set(null);
    this.reporteCuadro.set(null);
    this.reporteClasificacion.set(null);
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

  calcularEdadDias(fechaInicio: string | Date | null | undefined): number {
    if (!fechaInicio) return 0;
    const inicio = new Date(fechaInicio);
    const hoy = new Date();
    const diffTime = hoy.getTime() - inicio.getTime();
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
    return '';
  }

  get selectedLoteNombre(): string {
    return this.selectedLote?.loteNombre ?? '';
  }
}

// src/app/features/reporte-tecnico-administrativo/pages/reporte-tecnico-administrativo-main/reporte-tecnico-administrativo-main.component.ts
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
  ReporteTecnicoService, 
  ReporteTecnicoLevanteCompletoDto
} from '../../../reportes-tecnicos/services/reporte-tecnico.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { TablaLevanteCompletaComponent } from '../../../reportes-tecnicos/components/tabla-levante-completa/tabla-levante-completa.component';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FiltroSelectComponent } from '../../../lote-levante/pages/filtro-select/filtro-select.component';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../lote-levante/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';

@Component({
  selector: 'app-reporte-tecnico-administrativo-main',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    FontAwesomeModule,
    SidebarComponent,
    FiltroSelectComponent,
    TablaLevanteCompletaComponent
  ],
  templateUrl: './reporte-tecnico-administrativo-main.component.html',
  styleUrls: ['./reporte-tecnico-administrativo-main.component.scss']
})
export class ReporteTecnicoAdministrativoMainComponent implements OnInit, OnDestroy {
  // Iconos
  faFileExcel = faFileExcel;
  faDownload = faDownload;
  faSpinner = faSpinner;
  faSearch = faSearch;

  // Estado
  loading = signal(false);
  reporte = signal<ReporteTecnicoLevanteCompletoDto | null>(null);
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
  
  // UI
  error: string | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private reporteService: ReporteTecnicoService,
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

    // Generar reporte completo de levante (solo datos semanales)
    this.reporteService.generarReporteLevanteCompleto(
      this.selectedLoteId,
      this.tipoConsolidacion === 'consolidado'
    )
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
          console.error('Error al generar reporte administrativo:', err);
          this.error = err.error?.message || 'Error al generar el reporte';
          this.reporte.set(null);
        }
      });
  }

  exportarExcel(): void {
    if (!this.reporte()) {
      this.error = 'Debe generar un reporte primero';
      return;
    }

    this.loading.set(true);
    this.error = null;

    // TODO: Implementar exportación Excel
    this.error = 'Exportación a Excel en desarrollo';
    this.loading.set(false);
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
    return '';
  }

  get selectedLoteNombre(): string {
    return this.selectedLote?.loteNombre ?? '';
  }
}

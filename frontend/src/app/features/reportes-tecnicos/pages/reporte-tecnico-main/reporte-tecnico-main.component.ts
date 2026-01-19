// src/app/features/reportes-tecnicos/pages/reporte-tecnico-main/reporte-tecnico-main.component.ts
import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
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
  ReporteTecnicoLevanteConTabsDto
} from '../../services/reporte-tecnico.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { TablaDatosDiariosHembrasComponent } from '../../components/tabla-datos-diarios-hembras/tabla-datos-diarios-hembras.component';
import { TablaDatosDiariosMachosComponent } from '../../components/tabla-datos-diarios-machos/tabla-datos-diarios-machos.component';
import { TablaLevanteSemanalHembrasComponent } from '../../components/tabla-levante-semanal-hembras/tabla-levante-semanal-hembras.component';
import { TablaLevanteSemanalMachosComponent } from '../../components/tabla-levante-semanal-machos/tabla-levante-semanal-machos.component';
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
    SidebarComponent,
    FiltroSelectComponent,
    TablaDatosDiariosHembrasComponent,
    TablaDatosDiariosMachosComponent,
    TablaLevanteSemanalHembrasComponent,
    TablaLevanteSemanalMachosComponent
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
  reporteLevanteConTabs = signal<ReporteTecnicoLevanteConTabsDto | null>(null);
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
  
  // Tab activo interno (Diario Hembras, Diario Machos, Registro Semana Hembras, Registro Semana Machos)
  tabLevanteActivo: 'hembras' | 'machos' | 'semanal-hembras' | 'semanal-machos' = 'hembras';

  // Filtros de reporte
  tipoConsolidacion: 'sublote' | 'consolidado' = 'sublote';
  loteNombreBase: string = '';
  subloteSeleccionado: string | null = null;
  fechaInicio: string = '';
  fechaFin: string = '';
  
  // UI
  error: string | null = null;
  mostrarFormulas: boolean = false;

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
    this.reporteLevanteConTabs.set(null);
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
    this.reporteLevanteConTabs.set(null);
    this.applyFiltersToLotes();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteId = null;
    this.selectedLote = null;
    this.reporteLevanteConTabs.set(null);
    this.applyFiltersToLotes();
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLoteId = loteId;
    this.reporteLevanteConTabs.set(null);

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
    // Para consolidado, puede usar loteId (nueva lógica) o loteNombreBase (compatibilidad)
    if (this.tipoConsolidacion === 'consolidado' && !this.selectedLoteId && !this.loteNombreBase.trim()) return false;
    if (!this.fechaInicio || !this.fechaFin) return false;
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

    // Generar reporte con tabs (diario hembras, diario machos, semanal)
    this.reporteService.generarReporteLevanteConTabs(
      this.selectedLoteId,
      this.fechaInicio || undefined,
      this.fechaFin || undefined,
      this.tipoConsolidacion === 'consolidado'
    )
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (reporte) => {
          this.reporteLevanteConTabs.set(reporte);
          this.error = null;
        },
        error: (err) => {
          console.error('Error al generar reporte con tabs:', err);
          this.error = err.error?.message || 'Error al generar el reporte';
          this.reporteLevanteConTabs.set(null);
        }
      });
  }


  exportarExcel(): void {
    if (!this.reporteLevanteConTabs()) {
      this.error = 'Debe generar un reporte primero';
      return;
    }

    this.loading.set(true);
    this.error = null;

    // TODO: Implementar exportación Excel para reporte con tabs
    // Por ahora, exportar según el tab activo
    const reporte = this.reporteLevanteConTabs();
    if (!reporte) {
      this.error = 'No hay datos para exportar';
      this.loading.set(false);
      return;
    }

    // Por ahora mostrar mensaje - se implementará después
    this.error = 'Exportación a Excel en desarrollo';
    this.loading.set(false);
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

    if (!this.fechaInicio || !this.fechaFin) {
      this.error = 'Debe seleccionar fecha de inicio y fin';
      return false;
    }
    if (new Date(this.fechaInicio) > new Date(this.fechaFin)) {
      this.error = 'La fecha de inicio debe ser anterior a la fecha de fin';
      return false;
    }

    return true;
  }

  limpiarReporte(): void {
    this.reporteLevanteConTabs.set(null);
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

  // ================== FÓRMULAS DE REPORTES TÉCNICOS (Estructura Excel) ==================
  get gruposFormulas() {
    return [
      {
        titulo: '📅 Información Básica',
        formulas: [
          {
            nombre: 'Edad en Días',
            formula: 'Fecha Registro - Fecha Encaset'
          },
          {
            nombre: 'Edad en Semanas',
            formula: 'Edad en Días / 7 (redondeado hacia arriba)'
          },
          {
            nombre: 'Semana del Año',
            formula: 'Semana del año calendario (ISO 8601)'
          }
        ]
      },
      {
        titulo: '🐔 Cálculos de Eficiencia y Rendimiento - HEMBRAS',
        formulas: [
          {
            nombre: 'KcalAveH',
            formula: 'SI(Hembra>0; (KcalAlH*ConsKgH)/(Hembra); 0)'
          },
          {
            nombre: 'ProtAveH',
            formula: 'SI(Hembra>0; (%ProtAlH*ConsKgH)/(Hembra); 0)'
          },
          {
            nombre: '%RelM/H',
            formula: 'SI(Hembra>0; (SaldoMacho/Hembra*100); "")'
          },
          {
            nombre: '%MortH',
            formula: '(MortH/HEMBRAINI)*100'
          },
          {
            nombre: 'DifMortH',
            formula: '%MortH - %MortHGUIA'
          },
          {
            nombre: '%SelH',
            formula: '(SelH/HEMBRAINI)*100'
          },
          {
            nombre: '%ErrH',
            formula: '(ErrorH/HEMBRAINI)*100'
          },
          {
            nombre: 'M+S+EH',
            formula: 'MortH + SelH + ErrorH'
          },
          {
            nombre: 'RetAcH',
            formula: 'ACMortH + ACSelH + ACErrH'
          },
          {
            nombre: '%RetiroH',
            formula: '(RetAcH/HEMBRAINI)*100'
          },
          {
            nombre: 'ConsAcGrH',
            formula: '(AcConsH*1000)/HEMBRAINI'
          },
          {
            nombre: 'GrAveDiaH',
            formula: 'SI(Hembra>0; (ConsKgH*1000)/Hembra/7; 0)'
          },
          {
            nombre: 'IncrConsH',
            formula: 'Diferencia con semana anterior (ConsAcGrH - ConsAcGrH anterior)'
          },
          {
            nombre: '%DifConsH',
            formula: 'SI(ConsAcGrHGUIA>0; (ConsAcGrH-ConsAcGrHGUIA)/(ConsAcGrHGUIA*100); 0)'
          },
          {
            nombre: '%DifPesoH',
            formula: 'SI(PesoHGUIA>0; (PesoH-PesoHGUIA)/(PesoHGUIA*100); 0)'
          }
        ]
      },
      {
        titulo: '🐓 Cálculos de Eficiencia y Rendimiento - MACHOS',
        formulas: [
          {
            nombre: 'KcalAveM',
            formula: 'SI(SaldoMacho>0; (KcalAlM*ConsKgM)/(SaldoMacho); 0)'
          },
          {
            nombre: 'ProtAveM',
            formula: 'SI(SaldoMacho>0; (%ProtAlM*ConsKgM)/(SaldoMacho); 0)'
          },
          {
            nombre: '%MortM',
            formula: '(MortM/MACHOINI)*100'
          },
          {
            nombre: 'DifMortM',
            formula: '%MortM - %MortMGUIA'
          },
          {
            nombre: '%SelM',
            formula: '(SelM/MACHOINI)*100'
          },
          {
            nombre: '%ErrM',
            formula: '(ErrorM/MACHOINI)*100'
          },
          {
            nombre: 'M+S+EM',
            formula: 'MortM + SelM + ErrorM'
          },
          {
            nombre: 'RetAcM',
            formula: 'ACMortM + ACSelM + ACErrM'
          },
          {
            nombre: '%RetAcM',
            formula: '(RetAcM/MACHOINI)*100'
          },
          {
            nombre: 'ConsAcGrM',
            formula: '(AcConsM*1000)/MACHOINI'
          },
          {
            nombre: 'GrAveDiaM',
            formula: 'SI(SaldoMacho>0; (ConsKgM*1000)/SaldoMacho/7; 0)'
          },
          {
            nombre: 'IncrConsM',
            formula: 'Diferencia con semana anterior (ConsAcGrM - ConsAcGrM anterior)'
          },
          {
            nombre: 'DifConsM',
            formula: 'ConsAcGrM - ConsAcGrMGUIA'
          },
          {
            nombre: '%DifPesoM',
            formula: 'SI(PesoMGUIA>0; (PesoM-PesoMGUIA)/(PesoMGUIA*100); 0)'
          }
        ]
      },
      {
        titulo: '📊 Valores Acumulados',
        formulas: [
          {
            nombre: 'ACMortH',
            formula: 'Acumulado de MortH desde inicio del lote'
          },
          {
            nombre: 'ACSelH',
            formula: 'Acumulado de SelH desde inicio del lote'
          },
          {
            nombre: 'ACErrH',
            formula: 'Acumulado de ErrorH desde inicio del lote'
          },
          {
            nombre: 'ACMortM',
            formula: 'Acumulado de MortM desde inicio del lote'
          },
          {
            nombre: 'ACSelM',
            formula: 'Acumulado de SelM desde inicio del lote'
          },
          {
            nombre: 'ACErrM',
            formula: 'Acumulado de ErrorM desde inicio del lote'
          },
          {
            nombre: 'AcConsH',
            formula: 'Acumulado de ConsKgH desde inicio del lote'
          },
          {
            nombre: 'AcConsM',
            formula: 'Acumulado de ConsKgM desde inicio del lote'
          },
          {
            nombre: 'ErrSexAcH',
            formula: 'Error de sexaje acumulado hembras (manual)'
          },
          {
            nombre: '%ErrSxAcH',
            formula: '(ErrSexAcH/HEMBRAINI)*100'
          },
          {
            nombre: 'ErrSexAcM',
            formula: 'Error de sexaje acumulado machos (manual)'
          },
          {
            nombre: '%ErrSxAcM',
            formula: '(ErrSexAcM/MACHOINI)*100'
          }
        ]
      },
      {
        titulo: '🍽️ Datos Nutricionales y Guía - HEMBRAS',
        formulas: [
          {
            nombre: 'KcalSemH',
            formula: 'KcalAlH * ConsKgH'
          },
          {
            nombre: 'KcalSemAcH',
            formula: 'Acumulado de KcalSemH desde inicio'
          },
          {
            nombre: 'ProtSemH',
            formula: '(%ProtAlH/100) * ConsKgH'
          },
          {
            nombre: 'ProtSemAcH',
            formula: 'Acumulado de ProtSemH desde inicio'
          },
          {
            nombre: 'DifConsAcH',
            formula: 'AcConsH - ConsAcGrHGUIA'
          }
        ]
      },
      {
        titulo: '🍽️ Datos Nutricionales y Guía - MACHOS',
        formulas: [
          {
            nombre: 'KcalSemM',
            formula: 'KcalAlM * ConsKgM'
          },
          {
            nombre: 'KcalSemAcM',
            formula: 'Acumulado de KcalSemM desde inicio'
          },
          {
            nombre: 'ProtSemM',
            formula: '(%ProtAlM/100) * ConsKgM'
          },
          {
            nombre: 'ProtSemAcM',
            formula: 'Acumulado de ProtSemM desde inicio'
          },
          {
            nombre: 'DifConsAcM',
            formula: 'AcConsM - ConsAcGrMGUIA'
          }
        ]
      },
      {
        titulo: '📋 Campos Manuales (GUIA)',
        formulas: [
          {
            nombre: 'Campos GUIA',
            formula: 'Valores de referencia manuales para comparación: %MortHGUIA, RetiroHGUIA, ConsAcGrHGUIA, GrAveDiaGUIAH, PesoHGUIA, UnifHGUIA, etc.'
          },
          {
            nombre: 'Campos Manuales Adicionales',
            formula: 'CODGUÍA, IDLoteRAP, TRASLADO, NÚCLEOL, AÑON, ALIMHGUÍA, ALIMMGUÍA, Observaciones'
          }
        ]
      },
      {
        titulo: '🔄 Cálculo de Saldos',
        formulas: [
          {
            nombre: 'Hembra (Saldo Actual)',
            formula: 'HEMBRAINI - ACMortH - ACSelH - ACErrH'
          },
          {
            nombre: 'SaldoMacho',
            formula: 'MACHOINI - ACMortM - ACSelM - ACErrM'
          },
          {
            nombre: 'Nota sobre Traslados',
            formula: 'Los traslados se registran como valores negativos en SelH/SelM y se restan del saldo'
          }
        ]
      }
    ];
  }
}


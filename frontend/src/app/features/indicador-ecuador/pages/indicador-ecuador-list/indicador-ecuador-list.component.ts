// frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { IndicadorEcuadorService, IndicadorEcuadorDto, IndicadorEcuadorRequest, IndicadorEcuadorConsolidadoDto, LiquidacionPeriodoDto, IndicadorPolloEngordePorLotePadreDto, IndicadorPolloEngordePorLotePadreRequest } from '../../services/indicador-ecuador.service';
import { environment } from '../../../../../environments/environment';

/** Misma estructura que devuelve SeguimientoLoteLevante/filter-data (granjas, núcleos, galpones, lotes en una sola llamada). */
interface FilterDataResponse {
  farms: Array<{ id: number; name: string }>;
  nucleos: Array<{ nucleoId: string; nucleoNombre?: string; granjaId: number }>;
  galpones: Array<{ galponId: string; galponNombre?: string; nucleoId: string; granjaId: number }>;
  lotes: Array<{ loteId: number; loteNombre: string; granjaId: number; nucleoId?: string | null; galponId?: string | null }>;
}

/** Filter-data para pollo engorde: lotes ave engorde (LoteReproductoraAveEngorde/filter-data). */
interface FilterDataPolloEngordeResponse {
  farms: Array<{ id: number; name: string }>;
  lotesAveEngorde: Array<{ loteAveEngordeId: number; loteNombre: string; granjaId: number; nucleoId?: string | null; galponId?: string | null }>;
}

@Component({
  selector: 'app-indicador-ecuador-list',
  standalone: true,
  imports: [CommonModule, FormsModule, SidebarComponent],
  templateUrl: './indicador-ecuador-list.component.html',
  styleUrls: ['./indicador-ecuador-list.component.scss']
})
export class IndicadorEcuadorListComponent implements OnInit {
  /** URL del servicio único de filtros (mismo que Seguimiento Diario Levante). */
  readonly filterDataUrl = `${environment.apiUrl}/SeguimientoLoteLevante/filter-data`;
  readonly filterDataPolloEngordeUrl = `${environment.apiUrl}/LoteReproductoraAveEngorde/filter-data`;

  /** Vista: indicadores generales o Pollo Engorde. En Ecuador por defecto Pollo Engorde y el select se deshabilita. */
  vistaIndicador: 'general' | 'polloEngorde' = 'general';
  /** En Ecuador true: select Vista deshabilitado y fijado en Pollo Engorde. */
  vistaSelectDisabled = false;

  // Filtros
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;
  fechaDesde: string = '';
  fechaHasta: string = '';
  soloLotesCerrados: boolean = false;
  tipoLote: string = 'Todos';

  // Pollo Engorde: lote padre (Lote Ave Engorde)
  selectedLoteAveEngordeId: number | null = null;
  lotesAveEngorde: Array<{ loteAveEngordeId: number; loteNombre: string; granjaId: number }> = [];
  /** Variables Conv. Ajustada (ocultas por ahora; backend usa 2,7 y 4,5 por defecto si no se envían) */
  pesoAjusteVariable: number | null = null;
  divisorAjusteVariable: number | null = null;
  resultadoPolloEngorde: IndicadorPolloEngordePorLotePadreDto | null = null;
  mostrarPolloEngorde = false;
  tabPolloEngordeActivo: 'padre' | number = 'padre'; // 'padre' o id del reproductor

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
  granjas: Array<{ id: number; name: string }> = [];
  nucleos: Array<{ nucleoId: string; nucleoNombre?: string; granjaId: number }> = [];
  galpones: Array<{ galponId: string; galponNombre?: string; nucleoId: string; granjaId: number }> = [];
  lotes: Array<{ loteId: number; loteNombre: string; granjaId: number; nucleoId?: string | null; galponId?: string | null }> = [];
  private allNucleos: Array<{ nucleoId: string; nucleoNombre?: string; granjaId: number }> = [];
  private allGalpones: Array<{ galponId: string; galponNombre?: string; nucleoId: string; granjaId: number }> = [];
  private allLotes: Array<{ loteId: number; loteNombre: string; granjaId: number; nucleoId?: string | null; galponId?: string | null }> = [];

  // UI
  loading = false;
  loadingFilterData = true;
  error: string | null = null;

  constructor(
    private indicadorService: IndicadorEcuadorService,
    private http: HttpClient,
    private countryFilter: CountryFilterService
  ) {}

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

  /** Carga lotes ave engorde para la vista Pollo Engorde (Lote padre + reproductores). */
  cargarFilterDataPolloEngorde(): void {
    this.http.get<FilterDataPolloEngordeResponse>(this.filterDataPolloEngordeUrl).subscribe({
      next: (data) => {
        const items = (data as any).lotesAveEngorde ?? (data as any).LotesAveEngorde ?? [];
        this.lotesAveEngorde = Array.isArray(items)
          ? items.map((x: any) => ({
              loteAveEngordeId: x.loteAveEngordeId ?? x.LoteAveEngordeId,
              loteNombre: x.loteNombre ?? x.LoteNombre ?? '',
              granjaId: x.granjaId ?? x.GranjaId ?? 0
            }))
          : [];
      },
      error: () => { this.lotesAveEngorde = []; }
    });
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
    if (!this.selectedGranjaId) {
      this.nucleos = [];
      this.galpones = [];
      this.lotes = [];
      return;
    }
    const gid = Number(this.selectedGranjaId);
    this.nucleos = this.allNucleos.filter(n => n.granjaId === gid);

    if (!this.selectedNucleoId) {
      this.galpones = this.allGalpones.filter(g => g.granjaId === gid);
      this.lotes = this.allLotes.filter(l => l.granjaId === gid);
      return;
    }
    const nid = String(this.selectedNucleoId).trim();
    this.galpones = this.allGalpones.filter(g => g.granjaId === gid && String(g.nucleoId).trim() === nid);
    this.lotes = this.allLotes.filter(l => l.granjaId === gid && String(l.nucleoId || '').trim() === nid);

    if (!this.selectedGalponId) return;
    const gpid = String(this.selectedGalponId).trim();
    this.lotes = this.lotes.filter(l => String(l.galponId || '').trim() === gpid);
  }

  establecerFechasPorDefecto(): void {
    const hoy = new Date();
    const primerDiaMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
    const ultimoDiaMes = new Date(hoy.getFullYear(), hoy.getMonth() + 1, 0);
    this.fechaDesde = primerDiaMes.toISOString().split('T')[0];
    this.fechaHasta = ultimoDiaMes.toISOString().split('T')[0];
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
        soloLotesCerrados: this.soloLotesCerrados,
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
        soloLotesCerrados: this.soloLotesCerrados,
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
        this.indicadorService.obtenerLotesCerrados(desde, hasta, this.selectedGranjaId ?? undefined)
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

  async verIndicadoresPolloEngordePorLotePadre(): Promise<void> {
    if (!this.selectedLoteAveEngordeId) {
      this.error = 'Seleccione un lote (Ave Engorde).';
      return;
    }
    this.loading = true;
    this.error = null;
    this.mostrarPolloEngorde = false;
    this.resultadoPolloEngorde = null;
    try {
      const request: IndicadorPolloEngordePorLotePadreRequest = {
        loteAveEngordeId: this.selectedLoteAveEngordeId,
        fechaDesde: this.fechaDesde || null,
        fechaHasta: this.fechaHasta || null,
        soloLotesCerrados: this.soloLotesCerrados
        // pesoAjusteVariable y divisorAjusteVariable no se envían; el backend usa 2,7 y 4,5 por defecto
      };
      this.resultadoPolloEngorde = await firstValueFrom(this.indicadorService.indicadoresPolloEngordePorLotePadre(request));
      this.mostrarPolloEngorde = true;
      this.tabPolloEngordeActivo = 'padre';
    } catch (err: any) {
      this.error = err?.error?.error ?? err?.error?.message ?? err?.message ?? 'Error al cargar indicadores por lote padre.';
      this.resultadoPolloEngorde = null;
    } finally {
      this.loading = false;
    }
  }

  setTabPolloEngorde(tab: 'padre' | number): void {
    this.tabPolloEngordeActivo = tab;
  }

  limpiarFiltros(): void {
    this.selectedGranjaId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.selectedLoteAveEngordeId = null;
    this.establecerFechasPorDefecto();
    this.establecerFechasLiquidacionPorDefecto();
    this.soloLotesCerrados = false;
    this.tipoLote = 'Todos';
    this.applyFilterCascade();
    this.indicadores = [];
    this.consolidado = null;
    this.mostrarConsolidado = false;
    this.lotesCerrados = [];
    this.mostrarLotesCerrados = false;
    this.liquidacionPeriodo = null;
    this.mostrarLiquidacion = false;
    this.resultadoPolloEngorde = null;
    this.mostrarPolloEngorde = false;
    this.tabPolloEngordeActivo = 'padre';
    this.pesoAjusteVariable = null;
    this.divisorAjusteVariable = null;
  }

  formatearNumero(valor: number | null | undefined, decimales: number = 2): string {
    if (valor == null) return '-';
    return valor.toFixed(decimales);
  }

  formatearPorcentaje(valor: number | null | undefined): string {
    if (valor == null) return '-';
    return `${valor.toFixed(2)}%`;
  }
}

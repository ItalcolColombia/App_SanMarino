// frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import {
  IndicadorEcuadorService,
  IndicadorEcuadorDto,
  IndicadorEcuadorRequest,
  IndicadorEcuadorConsolidadoDto,
  LiquidacionPeriodoDto,
  LiquidacionPolloEngordeReporteDto,
  LiquidacionPolloEngordeItemDto
} from '../../services/indicador-ecuador.service';
import { environment } from '../../../../../environments/environment';

/** Misma estructura que devuelve SeguimientoLoteLevante/filter-data (granjas, núcleos, galpones, lotes en una sola llamada). */
interface FilterDataResponse {
  farms: Array<{ id: number; name: string }>;
  nucleos: Array<{ nucleoId: string; nucleoNombre?: string; granjaId: number }>;
  galpones: Array<{ galponId: string; galponNombre?: string; nucleoId: string; granjaId: number }>;
  lotes: Array<{ loteId: number; loteNombre: string; granjaId: number; nucleoId?: string | null; galponId?: string | null }>;
}

/** Filter-data Pollo Engorde: granjas, núcleos, galpones y lotes ave engorde (LoteReproductoraAveEngorde/filter-data). */
interface FilterDataPolloEngordeResponse {
  farms?: Array<{ id: number; name: string }>;
  nucleos?: Array<{ nucleoId: string; nucleoNombre?: string; granjaId: number }>;
  galpones?: Array<{ galponId: string; galponNombre?: string; nucleoId: string; granjaId: number }>;
  lotesAveEngorde?: Array<{ loteAveEngordeId: number; loteNombre: string; granjaId: number; nucleoId?: string | null; galponId?: string | null }>;
}

@Component({
  selector: 'app-indicador-ecuador-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
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
  /** Vista general: GET lotes-cerrados + POST calcular/consolidado. true = cierre en rango (GET) o solo aves=0 (POST). */
  soloLotesCerrados: boolean = true;
  tipoLote: string = 'Todos';

  /** Pollo Engorde – liquidación: un lote (cascada) o rango por fecha de cierre. */
  polloModo: 'unLote' | 'rango' = 'unLote';
  loadingFilterDataPollo = true;
  peFarms: Array<{ id: number; name: string }> = [];
  peNucleos: Array<{ nucleoId: string; nucleoNombre?: string; granjaId: number }> = [];
  peGalpones: Array<{ galponId: string; galponNombre?: string; nucleoId: string; granjaId: number }> = [];
  peLotesAveEngorde: Array<{ loteAveEngordeId: number; loteNombre: string; granjaId: number; nucleoId?: string | null; galponId?: string | null }> = [];
  private peAllNucleos: Array<{ nucleoId: string; nucleoNombre?: string; granjaId: number }> = [];
  private peAllGalpones: Array<{ galponId: string; galponNombre?: string; nucleoId: string; granjaId: number }> = [];
  private peAllLotesAveEngorde: Array<{ loteAveEngordeId: number; loteNombre: string; granjaId: number; nucleoId?: string | null; galponId?: string | null }> = [];

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

  resultadoLiquidacionPollo: LiquidacionPolloEngordeReporteDto | null = null;
  mostrarLiquidacionPollo = false;
  tabLiquidacionIdx = 0;

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

  /** Granjas, núcleos, galpones y lotes ave engorde en cascada (mismo endpoint que reproductoras). */
  cargarFilterDataPolloEngorde(): void {
    this.loadingFilterDataPollo = true;
    this.http.get<FilterDataPolloEngordeResponse>(this.filterDataPolloEngordeUrl).subscribe({
      next: (raw) => {
        const data = raw as Record<string, unknown>;
        const farms = (data['farms'] ?? data['Farms'] ?? []) as Array<{ id?: number; Id?: number; name?: string; Name?: string }>;
        const nucleos = (data['nucleos'] ?? data['Nucleos'] ?? []) as Array<{ nucleoId?: string; NucleoId?: string; nucleoNombre?: string; NucleoNombre?: string; granjaId?: number; GranjaId?: number }>;
        const galpones = (data['galpones'] ?? data['Galpones'] ?? []) as Array<{ galponId?: string; GalponId?: string; galponNombre?: string; GalponNombre?: string; nucleoId?: string; NucleoId?: string; granjaId?: number; GranjaId?: number }>;
        const lotesRaw = (data['lotesAveEngorde'] ?? data['LotesAveEngorde'] ?? []) as unknown[];

        this.peFarms = Array.isArray(farms)
          ? farms.map((f) => ({ id: Number(f.id ?? f.Id ?? 0), name: String(f.name ?? f.Name ?? '') }))
          : [];
        this.peAllNucleos = Array.isArray(nucleos)
          ? nucleos.map((n) => ({
              nucleoId: String(n.nucleoId ?? n.NucleoId ?? '').trim(),
              nucleoNombre: n.nucleoNombre ?? n.NucleoNombre,
              granjaId: Number(n.granjaId ?? n.GranjaId ?? 0)
            }))
          : [];
        this.peAllGalpones = Array.isArray(galpones)
          ? galpones.map((g) => ({
              galponId: String(g.galponId ?? g.GalponId ?? '').trim(),
              galponNombre: g.galponNombre ?? g.GalponNombre,
              nucleoId: String(g.nucleoId ?? g.NucleoId ?? '').trim(),
              granjaId: Number(g.granjaId ?? g.GranjaId ?? 0)
            }))
          : [];
        this.peAllLotesAveEngorde = Array.isArray(lotesRaw)
          ? lotesRaw.map((x: any) => ({
              loteAveEngordeId: Number(x.loteAveEngordeId ?? x.LoteAveEngordeId ?? 0),
              loteNombre: String(x.loteNombre ?? x.LoteNombre ?? ''),
              granjaId: Number(x.granjaId ?? x.GranjaId ?? 0),
              nucleoId: x.nucleoId ?? x.NucleoId ?? null,
              galponId: x.galponId ?? x.GalponId ?? null
            }))
          : [];
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

  /** Cascada Granja → Núcleo → Galpón → Lote (ave engorde). */
  private applyPeCascade(): void {
    if (!this.peGranjaId) {
      this.peNucleos = [];
      this.peGalpones = [];
      this.peLotesAveEngorde = [];
      return;
    }
    const gid = Number(this.peGranjaId);
    this.peNucleos = this.peAllNucleos.filter((n) => n.granjaId === gid);

    if (!this.peNucleoId) {
      this.peGalpones = this.peAllGalpones.filter((g) => g.granjaId === gid);
      this.peLotesAveEngorde = this.peAllLotesAveEngorde.filter((l) => l.granjaId === gid);
      return;
    }
    const nid = String(this.peNucleoId).trim();
    this.peGalpones = this.peAllGalpones.filter((g) => g.granjaId === gid && String(g.nucleoId).trim() === nid);
    this.peLotesAveEngorde = this.peAllLotesAveEngorde.filter(
      (l) => l.granjaId === gid && String(l.nucleoId || '').trim() === nid
    );

    if (!this.peGalponId) return;
    const gpid = String(this.peGalponId).trim();
    this.peLotesAveEngorde = this.peLotesAveEngorde.filter((l) => String(l.galponId || '').trim() === gpid);
  }

  onPeGranjaChange(): void {
    this.peNucleoId = null;
    this.peGalponId = null;
    this.peLoteAveEngordeId = null;
    this.applyPeCascade();
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

  onPolloModoChange(): void {
    this.error = null;
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
  get rangoNucleosLista(): Array<{ nucleoId: string; nucleoNombre?: string; granjaId: number }> {
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
        this.indicadorService.obtenerLotesCerrados(
          desde,
          hasta,
          this.selectedGranjaId ?? undefined,
          this.soloLotesCerrados
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

    try {
      if (this.polloModo === 'unLote') {
        if (!this.peLoteAveEngordeId) {
          this.error = 'Seleccione granja, núcleo, galpón y lote liquidado.';
          return;
        }
        const res = await firstValueFrom(
          this.indicadorService.liquidacionPolloEngordeReporte({
            modo: 'UnLote',
            loteAveEngordeId: this.peLoteAveEngordeId,
            fechaDesde: null,
            fechaHasta: null,
            alcance: 'TodasLasGranjas',
            granjaId: null,
            nucleoId: null
          })
        );
        this.resultadoLiquidacionPollo = res;
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
            nucleoId: alcance === 'Nucleo' ? this.rangoNucleoId : null
          })
        );
        this.resultadoLiquidacionPollo = res;
      }
      this.tabLiquidacionIdx = 0;
      this.mostrarLiquidacionPollo = true;
    } catch (err: any) {
      this.error = err?.error?.error ?? err?.error?.message ?? err?.message ?? 'Error al generar la liquidación.';
      this.resultadoLiquidacionPollo = null;
    } finally {
      this.loading = false;
    }
  }

  setTabLiquidacion(idx: number): void {
    this.tabLiquidacionIdx = idx;
  }

  liquidacionItemActivo(): LiquidacionPolloEngordeItemDto | null {
    const items = this.resultadoLiquidacionPollo?.items;
    if (!items?.length) return null;
    const i = Math.min(this.tabLiquidacionIdx, items.length - 1);
    return items[i] ?? null;
  }

  limpiarFiltros(): void {
    this.selectedGranjaId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.establecerFechasPorDefecto();
    this.establecerFechasLiquidacionPorDefecto();
    this.soloLotesCerrados = true;
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
    this.polloAlcance = 'TodasLasGranjas';
    this.rangoGranjaId = null;
    this.rangoNucleoId = null;
    this.applyPeCascade();
    this.resultadoLiquidacionPollo = null;
    this.mostrarLiquidacionPollo = false;
    this.tabLiquidacionIdx = 0;
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

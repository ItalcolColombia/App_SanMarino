// frontend/src/app/features/reporte-diario-costos-postura/pages/reporte-diario-costos-postura-main/reporte-diario-costos-postura-main.component.ts
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { FarmDto, FarmService } from '../../../farm/services/farm.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { exportarAoaMultiHojaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import {
  formatearNumero as fmtNumero,
  fechaCortaSinTz as fmtFecha,
  dateStampCompact
} from '../../../../shared/utils/format';
import { ReporteDiarioCostosPosturaService } from '../../services/reporte-diario-costos-postura.service';
import { construirHojasCostosPostura } from '../../funciones/construir-aoa-costos-postura.funcion';
import { expandirFilasAlimento } from '../../funciones/expandir-filas-alimento.funcion';
import {
  FilaAlimentoView,
  LotePosturaBaseOpcion,
  ReporteDiarioCostosPosturaFila,
  ReporteDiarioCostosPosturaReporte,
  ReporteDiarioCostosPosturaRequest
} from '../../models/reporte-diario-costos-postura.model';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';

type Pestana = 'aves' | 'alimento' | 'huevos';

interface GranjaOpcion { id: number; name: string; regional: string; }

/** Fila de la pestaña Aves ya formateada (referencias estables — evita NG0103). */
interface FilaAvesView {
  fecha: string;
  fechaFmt: string;
  fase: string;
  granjaNombre: string;
  excluidoDelTotal: boolean;
  loteGalpon: string;
  edadDias: number | null;
  semana: number | null;
  mortalidadH: number; mortalidadM: number;
  seleccionH: number; seleccionM: number;
  errorSexajeH: number; errorSexajeM: number;
  ventaAvesH: number; ventaAvesM: number;
}

@Component({
  selector: 'app-reporte-diario-costos-postura-main',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './reporte-diario-costos-postura-main.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./reporte-diario-costos-postura-main.component.scss']
})
export class ReporteDiarioCostosPosturaMainComponent implements OnInit {
  private readonly service = inject(ReporteDiarioCostosPosturaService);
  private readonly farmService = inject(FarmService);
  private readonly toast = inject(ToastService);
  private readonly companyConfig = inject(ActiveCompanyConfigService);

  /** Empresas sin machos en postura: no se pintan ni se exportan (SR-DEF-1). */
  ocultaMachosEnPostura = false;

  // ── Catálogos de filtros ────────────────────────────────────────────────
  private granjasTodas: GranjaOpcion[] = [];
  private lotesBaseTodos: LotePosturaBaseOpcion[] = [];
  granjas: GranjaOpcion[] = [];
  lotesBase: LotePosturaBaseOpcion[] = [];
  regionales: string[] = [];
  loadingFilterData = true;

  // ── Filtros ─────────────────────────────────────────────────────────────
  regional: string | null = null;
  granjaId: number | null = null;
  lotePosturaBaseId: number | null = null;
  /** '' = ambas fases. */
  fase: '' | 'Levante' | 'Produccion' = '';
  fechaDesde = '';
  fechaHasta = '';

  // ── Estado / resultado ──────────────────────────────────────────────────
  loading = false;
  error: string | null = null;
  reporte: ReporteDiarioCostosPosturaReporte | null = null;
  pestana: Pestana = 'aves';

  // ── Vista precalculada (referencias estables) ───────────────────────────
  filasAves: FilaAvesView[] = [];
  filasAlimento: FilaAlimentoView[] = [];
  filasHuevos: ReporteDiarioCostosPosturaFila[] = [];
  /** Advertencia: filas cuya partición de huevo no cuadra con el total (defecto de datos). */
  huevosDescuadrados = 0;
  /** Advertencia: días registrados en levante Y producción; se muestran pero no suman dos veces. */
  diasDuplicados = 0;
  /** El lote base elegido vivía en más granjas que la pedida y el reporte lo siguió. */
  alcanceExpandido = false;

  ngOnInit(): void {
    this.companyConfig.getFlags().subscribe({
      next: f => (this.ocultaMachosEnPostura = !!f?.ocultaMachosEnPostura),
      error: () => (this.ocultaMachosEnPostura = false)
    });
    this.cargarFilterData();
  }

  private cargarFilterData(): void {
    this.loadingFilterData = true;

    this.farmService.getAll().subscribe({
      next: (farms: FarmDto[]) => {
        this.granjasTodas = (farms ?? [])
          .map(f => ({
            id: Number(f.id),
            name: String(f.name ?? ''),
            regional: (f.regionalNombre ?? f.regional ?? '').trim()
          }))
          .sort((a, b) => a.name.localeCompare(b.name));

        this.regionales = Array.from(
          new Set(this.granjasTodas.map(g => g.regional).filter(r => r !== ''))
        ).sort((a, b) => a.localeCompare(b));

        this.aplicarCascada();
        this.loadingFilterData = false;
      },
      error: () => {
        this.granjasTodas = [];
        this.granjas = [];
        this.regionales = [];
        this.loadingFilterData = false;
        this.toast.error('No se pudieron cargar las granjas para los filtros.');
      }
    });

    this.service.lotesBase().subscribe({
      next: (bases) => {
        this.lotesBaseTodos = bases ?? [];
        this.aplicarCascada();
      },
      error: () => { this.lotesBaseTodos = []; this.lotesBase = []; }
    });
  }

  /**
   * Regional → granja → lote base. Se recalcula entero (referencias nuevas solo al cambiar filtros).
   *
   * El lote base se acota por DÓNDE ESTÁN SUS LOTES (`granjaIds`), no por el `farm_id` del catálogo:
   * si el lote se traslada a otra granja, la base tiene que seguir apareciendo bajo la granja donde
   * se hizo el levante.
   */
  private aplicarCascada(): void {
    this.granjas = this.regional
      ? this.granjasTodas.filter(g => g.regional === this.regional)
      : this.granjasTodas;

    if (this.granjaId != null && !this.granjas.some(g => g.id === this.granjaId)) {
      this.granjaId = null;
    }

    const granjaSel = this.granjaId;
    this.lotesBase = granjaSel != null
      ? this.lotesBaseTodos.filter(b => (b.granjaIds ?? []).includes(granjaSel))
      : this.lotesBaseTodos;

    if (this.lotePosturaBaseId != null && !this.lotesBase.some(b => b.lotePosturaBaseId === this.lotePosturaBaseId)) {
      this.lotePosturaBaseId = null;
    }
  }

  /** Etiqueta del desplegable: la base y, si vive en más de una granja, cuáles. */
  etiquetaLoteBase(b: LotePosturaBaseOpcion): string {
    const granjas = b.granjaNombres ?? [];
    return granjas.length > 1 ? `${b.loteNombre} (${granjas.join(' + ')})` : b.loteNombre;
  }

  onRegionalChange(): void { this.aplicarCascada(); }
  onGranjaChange(): void { this.aplicarCascada(); }

  async generar(): Promise<void> {
    this.loading = true;
    this.error = null;
    try {
      const request: ReporteDiarioCostosPosturaRequest = {
        granjaId: this.granjaId != null ? Number(this.granjaId) : null,
        regional: this.regional || null,
        lotePosturaBaseId: this.lotePosturaBaseId != null ? Number(this.lotePosturaBaseId) : null,
        fase: this.fase || null,
        fechaDesde: this.fechaDesde || null,
        fechaHasta: this.fechaHasta || null
      };
      const rep = await firstValueFrom(this.service.generar(request));
      this.aplicarReporte(rep);

      if (rep.filas.length === 0) {
        this.toast.info('El reporte no devolvió registros para los filtros seleccionados.');
      }
    } catch (err: any) {
      this.error = err?.error?.message || err?.error?.error || err?.message
        || 'Error al generar el reporte diario de costos de postura';
      this.limpiarResultado();
      this.toast.error(this.error!);
    } finally {
      this.loading = false;
    }
  }

  /** Precalcula TODA la vista una sola vez por generación (referencias estables). */
  private aplicarReporte(rep: ReporteDiarioCostosPosturaReporte): void {
    this.reporte = rep;

    this.filasAves = rep.filas.map((f): FilaAvesView => ({
      fecha: f.fecha,
      fechaFmt: fmtFecha(f.fecha),
      fase: f.fase,
      granjaNombre: f.granjaNombre,
      excluidoDelTotal: f.excluidoDelTotal === true,
      loteGalpon: f.loteGalpon,
      edadDias: f.edadDias,
      semana: f.semana,
      mortalidadH: f.mortalidadH, mortalidadM: f.mortalidadM,
      seleccionH: f.seleccionH, seleccionM: f.seleccionM,
      errorSexajeH: f.errorSexajeH, errorSexajeM: f.errorSexajeM,
      ventaAvesH: f.ventaAvesH, ventaAvesM: f.ventaAvesM
    }));

    this.filasAlimento = expandirFilasAlimento(rep.filas);
    this.filasHuevos = rep.filas.filter(f => f.fase === 'Produccion');
    this.huevosDescuadrados = this.filasHuevos.filter(f => !f.huevo.particionCuadra).length;
    this.diasDuplicados = rep.diasDuplicados ?? 0;
    this.alcanceExpandido = rep.alcanceExpandidoPorLoteBase === true;

    // La pestaña Huevos no existe sin producción: si estaba activa, se vuelve a Aves.
    if (this.pestana === 'huevos' && this.filasHuevos.length === 0) this.pestana = 'aves';
  }

  limpiar(): void {
    this.regional = null;
    this.granjaId = null;
    this.lotePosturaBaseId = null;
    this.fase = '';
    this.fechaDesde = '';
    this.fechaHasta = '';
    this.error = null;
    this.aplicarCascada();
    this.limpiarResultado();
  }

  private limpiarResultado(): void {
    this.reporte = null;
    this.filasAves = [];
    this.filasAlimento = [];
    this.filasHuevos = [];
    this.huevosDescuadrados = 0;
    this.diasDuplicados = 0;
    this.alcanceExpandido = false;
    this.pestana = 'aves';
  }

  get hayHuevos(): boolean { return this.filasHuevos.length > 0; }

  irA(p: Pestana): void { this.pestana = p; }

  exportarExcel(): void {
    if (!this.reporte || this.reporte.filas.length === 0) return;
    const hojas = construirHojasCostosPostura(this.reporte, this.ocultaMachosEnPostura);
    exportarAoaMultiHojaExcel(hojas, {
      filenameFull: `Reporte_Diario_Costos_Postura_${dateStampCompact()}.xlsx`
    });
    this.toast.success(`Excel generado con ${hojas.length} hoja(s) y ${this.reporte.filas.length} registro(s).`);
  }

  // ── Formato (delegan en shared/utils/format) ────────────────────────────
  ent(v: number | null | undefined): string {
    if (v == null) return '—';
    return fmtNumero(Math.round(v));
  }
  kg(v: number | null | undefined): string {
    if (v == null) return '—';
    return fmtNumero(Math.round(v * 1000) / 1000);
  }
  fechaFmt(iso: string | null): string { return fmtFecha(iso); }

  // El lote base puede vivir en dos granjas con el mismo «lote : galpón», así que la granja
  // entra en la clave.
  trackAves = (_: number, f: FilaAvesView) => `${f.fecha}|${f.granjaNombre}|${f.loteGalpon}|${f.fase}`;
  trackAlimento = (i: number, f: FilaAlimentoView) => `${f.fecha}|${f.granjaNombre}|${f.loteGalpon}|${i}`;
  trackHuevo = (_: number, f: ReporteDiarioCostosPosturaFila) => `${f.fecha}|${f.loteId}`;
}

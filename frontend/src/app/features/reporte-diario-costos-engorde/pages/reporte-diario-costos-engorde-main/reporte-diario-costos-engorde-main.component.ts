// frontend/src/app/features/reporte-diario-costos-engorde/pages/reporte-diario-costos-engorde-main/reporte-diario-costos-engorde-main.component.ts
import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { exportarAoaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { formatearNumero as fmtNumero, fechaCorta as fmtFechaCorta, dateStampCompact } from '../../../../shared/utils/format';
import { LoteBaseEngordeApi, LoteBaseEngordeDto } from '../../../engorde-comun/services/lote-base-engorde.api';
import { LoteEngordeService } from '../../../lote-engorde/services/lote-engorde.service';
import { ReporteDiarioCostosEngordeService } from '../../services/reporte-diario-costos-engorde.service';
import { construirAoaReporteCostos } from '../../funciones/construir-aoa-reporte-costos.funcion';
import {
  ReporteDiarioCostosFila,
  ReporteDiarioCostosGalponDia,
  ReporteDiarioCostosGalponHeader,
  ReporteDiarioCostosGalponTotal,
  ReporteDiarioCostosReporte,
  ReporteDiarioCostosRequest
} from '../../models/reporte-diario-costos.model';

interface GranjaOpcion { id: number; name: string; }

/** Fila de vista precalculada (referencias estables para el template — evita NG0103). */
interface FilaView {
  fecha: string;
  fechaFmt: string;
  consumoTotalKg: number;
  mortSelTotal: number;
  avesVivasTotal: number;
  /** >= 1 filas de alimento (placeholder si el día no tiene desglose). */
  alimentoRows: { nombreAlimento: string; stockKg: number | null; consumoKg: number | null }[];
  /** Galpones alineados 1:1 con `galponesCols` (por id, no por posición). */
  galpones: ReporteDiarioCostosGalponDia[];
}

const GALPON_VACIO = (g: ReporteDiarioCostosGalponHeader): ReporteDiarioCostosGalponDia => ({
  galponId: g.galponId,
  galponNombre: g.galponNombre,
  mortalidad: 0,
  seleccion: 0,
  errSexaje: 0,
  mortSel: 0,
  consumoKg: 0,
  avesVivas: 0
});

@Component({
  selector: 'app-reporte-diario-costos-engorde-main',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './reporte-diario-costos-engorde-main.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./reporte-diario-costos-engorde-main.component.scss']
})
export class ReporteDiarioCostosEngordeMainComponent implements OnInit {
  private readonly service = inject(ReporteDiarioCostosEngordeService);
  private readonly loteBaseApi = inject(LoteBaseEngordeApi);
  private readonly loteEngordeSvc = inject(LoteEngordeService);

  // Catálogos de filtros
  granjas: GranjaOpcion[] = [];
  lotesBase: LoteBaseEngordeDto[] = [];
  loadingFilterData = true;

  // Filtros
  granjaId: number | null = null;
  loteBaseEngordeId: number | null = null;
  fechaInicio = '';
  fechaFin = '';

  // Estado / resultado
  loading = false;
  error: string | null = null;
  reporte: ReporteDiarioCostosReporte | null = null;

  // Vista precalculada (referencias estables)
  galponesCols: ReporteDiarioCostosGalponHeader[] = [];
  filasView: FilaView[] = [];
  totalesPorGalpon: ReporteDiarioCostosGalponTotal[] = [];
  avesActualesPorGalpon: number[] = [];

  ngOnInit(): void {
    this.cargarFilterData();
  }

  private cargarFilterData(): void {
    this.loadingFilterData = true;
    this.loteEngordeSvc.getFormData().subscribe({
      next: (data) => {
        this.granjas = (data.farms ?? [])
          .map(f => ({ id: Number(f.id), name: String(f.name ?? '') }))
          .sort((a, b) => a.name.localeCompare(b.name));
        this.loadingFilterData = false;
      },
      error: () => { this.granjas = []; this.loadingFilterData = false; }
    });
    this.loteBaseApi.getAll().subscribe({
      next: (bases) => { this.lotesBase = bases ?? []; },
      error: () => { this.lotesBase = []; }
    });
  }

  async generar(): Promise<void> {
    if (!this.granjaId) {
      this.error = 'Seleccioná una granja para generar el reporte.';
      return;
    }
    this.loading = true;
    this.error = null;
    this.reporte = null;
    try {
      const request: ReporteDiarioCostosRequest = {
        granjaId: Number(this.granjaId),
        loteBaseEngordeId: this.loteBaseEngordeId != null ? Number(this.loteBaseEngordeId) : null,
        fechaInicio: this.fechaInicio || null,
        fechaFin: this.fechaFin || null
      };
      const rep = await firstValueFrom(this.service.generar(request));
      this.aplicarReporte(rep);
    } catch (err: any) {
      this.error = err?.error?.message || err?.error?.error || err?.message || 'Error al generar el reporte diario de costos';
      this.reporte = null;
      this.galponesCols = [];
      this.filasView = [];
      this.totalesPorGalpon = [];
      this.avesActualesPorGalpon = [];
    } finally {
      this.loading = false;
    }
  }

  /** Precalcula TODA la vista una sola vez por generación (referencias estables). */
  private aplicarReporte(rep: ReporteDiarioCostosReporte): void {
    this.reporte = rep;
    this.galponesCols = rep.galpones;

    this.filasView = rep.filas.map((f: ReporteDiarioCostosFila): FilaView => {
      const porId = new Map(f.galpones.map(g => [g.galponId, g]));
      return {
        fecha: f.fecha,
        fechaFmt: fmtFechaCorta(f.fecha),
        consumoTotalKg: f.consumoTotalKg,
        mortSelTotal: f.mortSelTotal,
        avesVivasTotal: f.avesVivasTotal,
        alimentoRows: f.alimentos.length > 0
          ? f.alimentos
          : [{ nombreAlimento: '—', stockKg: null, consumoKg: null }],
        galpones: rep.galpones.map(g => porId.get(g.galponId) ?? GALPON_VACIO(g))
      };
    });

    const totPorId = new Map(rep.totales.porGalpon.map(t => [t.galponId, t]));
    this.totalesPorGalpon = rep.galpones.map(g => totPorId.get(g.galponId) ?? {
      galponId: g.galponId, galponNombre: g.galponNombre, mortalidad: 0, seleccion: 0, errSexaje: 0, mortSel: 0
    });

    const avesPorId = new Map(rep.avesVivasActuales.map(a => [a.galponId, a.avesVivas]));
    this.avesActualesPorGalpon = rep.galpones.map(g => avesPorId.get(g.galponId) ?? 0);
  }

  limpiar(): void {
    this.granjaId = null;
    this.loteBaseEngordeId = null;
    this.fechaInicio = '';
    this.fechaFin = '';
    this.reporte = null;
    this.error = null;
    this.galponesCols = [];
    this.filasView = [];
    this.totalesPorGalpon = [];
    this.avesActualesPorGalpon = [];
  }

  // ── Formato (delegan en shared/utils/format) ────────────────────────────
  kg(v: number | null | undefined): string {
    if (v == null) return '—';
    return fmtNumero(Math.round(v * 100) / 100);
  }
  ent(v: number | null | undefined): string {
    if (v == null) return '—';
    return fmtNumero(Math.round(v));
  }
  fecha(iso: string | null): string {
    return fmtFechaCorta(iso);
  }

  trackFila = (_: number, f: FilaView) => f.fecha;
  trackGalpon = (_: number, g: ReporteDiarioCostosGalponHeader) => g.galponId;

  exportarExcel(): void {
    if (!this.reporte || this.reporte.filas.length === 0) return;
    const { aoa, colWidths } = construirAoaReporteCostos(this.reporte);
    exportarAoaExcel(aoa, 'Reporte Diario Costos', {
      filenameFull: `Reporte_Diario_Costos_Engorde_${dateStampCompact()}.xlsx`,
      colWidths
    });
  }
}

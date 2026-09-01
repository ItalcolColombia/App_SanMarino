import { Component, inject, Input, OnChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteGeneralSemanalDto } from '../../services/reporte-tecnico.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { GuiaMetricasDisponibles, GUIA_TODAS_DISPONIBLES } from '../../models/reporte-tecnico-guia.model';
import { columnasHuevoReporte, ColumnasHuevoReporte } from '../../funciones/columnas-huevo-reporte.funcion';

@Component({
  selector: 'app-reporte-general-semanal',
  standalone: true,
  imports: [CommonModule],
  // Eager (no OnPush): el flag de machos se asigna desde un `subscribe`, y con OnPush esa
  // asignación no marca la vista sucia. Ver CLAUDE.md §Change detection en Angular 22.
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './reporte-general-semanal.component.html',
  styleUrls: ['./reporte-general-semanal.component.scss']
})
export class ReporteGeneralSemanalComponent implements OnChanges {
  /** Empresas sin machos en postura: sus columnas no se pintan (SR-DEF-1). */
  ocultaMachosEnPostura = false;

  private readonly companyConfigMachos = inject(ActiveCompanyConfigService);

  constructor() {
    this.companyConfigMachos.getFlags().subscribe({
      next: f => (this.ocultaMachosEnPostura = !!f?.ocultaMachosEnPostura),
      error: () => (this.ocultaMachosEnPostura = false)
    });
  }

  @Input() datos: ReporteGeneralSemanalDto[] = [];

  /** `companies.clasificacion_huevo_por_items` de la empresa del reporte (lo informa el backend). */
  @Input() clasificacionHuevoPorItems = false;

  /** Qué columnas de comparación contra la guía tienen dato. Por defecto, todas. */
  @Input() guiaDisponible: GuiaMetricasDisponibles = GUIA_TODAS_DISPONIBLES;

  /** Columnas del grupo «Huevos». Campo, no getter: un objeto nuevo por ciclo rompe el CD. */
  colHuevos: ColumnasHuevoReporte = columnasHuevoReporte(false);

  ngOnChanges(): void {
    const hayOtros = this.datos.some(d => (d.huevoOtros ?? 0) > 0);
    this.colHuevos = columnasHuevoReporte(this.clasificacionHuevoPorItems, hayOtros);
  }

  /** «%Postura»: Real + Guía + Dif, o sólo Real. */
  get colspanPostura(): number { return this.guiaDisponible.prodPorcentaje ? 3 : 1; }

  /** «Peso Huevo»: Real + Guía + Dif, o sólo Real. */
  get colspanPesoHuevo(): number { return this.guiaDisponible.pesoHuevo ? 3 : 1; }

  /** «Calidad»: Unif [+ G] + HTAA [+ G]. */
  get colspanCalidad(): number {
    return 2 + (this.guiaDisponible.uniformidad ? 1 : 0) + (this.guiaDisponible.hTotalAa ? 1 : 0);
  }

  semaforo(real: number | null | undefined, guia: number | null | undefined, umbral = 5): string {
    if (real == null || guia == null) return '';
    const pct = guia !== 0 ? (Math.abs(real - guia) / Math.abs(guia)) * 100 : 0;
    if (pct <= umbral)      return 'cell-success';
    if (pct <= umbral * 3)  return 'cell-warning';
    return 'cell-danger';
  }

  fmt(v: number | null | undefined, dec = 1): string {
    if (v == null) return '—';
    return v.toFixed(dec);
  }

  classDif(v: number | null | undefined): string {
    if (v == null) return '';
    return v > 0.01 ? 'cell-success' : v < -0.01 ? 'cell-danger' : '';
  }
}

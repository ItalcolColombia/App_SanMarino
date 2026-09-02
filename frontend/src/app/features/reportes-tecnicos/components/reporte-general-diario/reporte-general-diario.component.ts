import { Component, inject, Input, OnChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteGeneralDiarioDto } from '../../services/reporte-tecnico.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { GuiaMetricasDisponibles, GUIA_TODAS_DISPONIBLES } from '../../models/reporte-tecnico-guia.model';
import { columnasHuevoReporte, ColumnasHuevoReporte } from '../../funciones/columnas-huevo-reporte.funcion';

@Component({
  selector: 'app-reporte-general-diario',
  standalone: true,
  imports: [CommonModule],
  // Eager (no OnPush): el flag de machos se asigna desde un `subscribe`, y con OnPush esa
  // asignación no marca la vista sucia. Ver CLAUDE.md §Change detection en Angular 22.
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './reporte-general-diario.component.html',
  styleUrls: ['./reporte-general-diario.component.scss']
})
export class ReporteGeneralDiarioComponent implements OnChanges {
  /** Empresas sin machos en postura: sus columnas no se pintan (SR-DEF-1). */
  ocultaMachosEnPostura = false;

  private readonly companyConfigMachos = inject(ActiveCompanyConfigService);

  constructor() {
    this.companyConfigMachos.getFlags().subscribe({
      next: f => (this.ocultaMachosEnPostura = !!f?.ocultaMachosEnPostura),
      error: () => (this.ocultaMachosEnPostura = false)
    });
  }

  @Input() datos: ReporteGeneralDiarioDto[] = [];

  /**
   * `companies.clasificacion_huevo_por_items` de la empresa del reporte. Lo informa el backend en
   * el DTO (no se vuelve a preguntar por el flag): con clasificación por ítems, `huevo_inc` se
   * escribe en 0 a propósito y el desglose real es Primera/Pnc.
   */
  @Input() clasificacionHuevoPorItems = false;

  /** Qué columnas de comparación contra la guía tienen dato. Por defecto, todas. */
  @Input() guiaDisponible: GuiaMetricasDisponibles = GUIA_TODAS_DISPONIBLES;

  /**
   * Columnas del grupo «Huevos». Se recalcula en `ngOnChanges` y NO se expone como getter: un
   * getter que devuelve un objeto nuevo por ciclo rompe la comparación de referencias del template
   * (CLAUDE.md §🧩).
   */
  colHuevos: ColumnasHuevoReporte = columnasHuevoReporte(false);

  ngOnChanges(): void {
    const hayOtros = this.datos.some(d => (d.huevoOtros ?? 0) > 0);
    this.colHuevos = columnasHuevoReporte(this.clasificacionHuevoPorItems, hayOtros);
  }

  /** `colspan` del grupo «%Postura»: Real + Guía + Dif, o sólo Real si la guía no trae postura. */
  get colspanPostura(): number {
    return this.guiaDisponible.prodPorcentaje ? 3 : 1;
  }

  /** `colspan` del grupo «Peso Huevo»: Real + Guía, o sólo Real. */
  get colspanPesoHuevo(): number {
    return this.guiaDisponible.pesoHuevo ? 2 : 1;
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

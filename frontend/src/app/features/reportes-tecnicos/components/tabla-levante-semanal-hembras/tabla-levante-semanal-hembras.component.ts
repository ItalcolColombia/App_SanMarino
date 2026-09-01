// src/app/features/reportes-tecnicos/components/tabla-levante-semanal-hembras/tabla-levante-semanal-hembras.component.ts
import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

import { ReporteTecnicoLevanteSemanalDto, ReporteTecnicoLoteInfoDto } from '../../services/reporte-tecnico.service';
import { GuiaMetricasDisponibles, GUIA_TODAS_DISPONIBLES } from '../../models/reporte-tecnico-guia.model';

@Component({
  selector: 'app-tabla-levante-semanal-hembras',
  standalone: true,
  imports: [],
  templateUrl: './tabla-levante-semanal-hembras.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabla-levante-semanal-hembras.component.scss']
})
export class TablaLevanteSemanalHembrasComponent {

  /**
   * Qué columnas de comparación contra la guía tienen dato. Lo informa el backend en el reporte;
   * por defecto todas, para que un DTO sin el campo pinte lo de siempre.
   */
  @Input() guiaDisponible: GuiaMetricasDisponibles = GUIA_TODAS_DISPONIBLES;

  // ── Ancho de los grupos ─────────────────────────────────────────────────────────────────────
  // Cada grupo pierde su columna «Tabla»/«std» cuando la guía de la empresa no trae esa métrica.
  //
  // 🔴 TOSCANA arranca en 5, no en 4: hasta este pase declaraba `colspan="4"` con **cinco**
  // columnas debajo (medido: 5 `<th>` y 5 `<td>`), así que el rótulo de ese grupo y los de todos
  // los grupos siguientes se pintaban corridos una columna a la izquierda. Los datos siempre
  // estuvieron bien; mentía el encabezado.

  /** % SEM. MORT.: M, [M std], S, E.S. */
  get colspanSemMort(): number { return 3 + (this.guiaDisponible.mortSemH ? 1 : 0); }

  /** % MORTALIDAD ACUMULADA: M, S, E.S., LOTE, [PTO. Dif.] */
  get colspanMortAcum(): number { return 4 + (this.guiaDisponible.retiroAcH ? 1 : 0); }

  /** cons.acumulado: REAL, [TABLA], % DIF */
  get colspanConsAcum(): number { return 2 + (this.guiaDisponible.consAcH ? 1 : 0); }

  /** TOSCANA: gr/AVE/DIA, [Tabla], CON LOTE, Incrementos, [Tabla] */
  get colspanToscana(): number {
    return 3 + (this.guiaDisponible.grAveDiaH ? 1 : 0) + (this.guiaDisponible.consAcH ? 1 : 0);
  }

  /** PESO CORPORAL: [Tabla], PESO LOTE, DIF LOTE */
  get colspanPesoCorporal(): number { return 2 + (this.guiaDisponible.pesoH ? 1 : 0); }

  /** GANANCIA: [TABLA], REAL */
  get colspanGanancia(): number { return 1 + (this.guiaDisponible.consAcH ? 1 : 0); }

  @Input() datos: ReporteTecnicoLevanteSemanalDto[] = [];
  @Input() informacionLote?: ReporteTecnicoLoteInfoDto | null;

  formatNumber(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return value.toFixed(decimals);
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const d = typeof date === 'string' ? new Date(date) : date;
    const day = d.getDate().toString().padStart(2, '0');
    const month = d.toLocaleDateString('es-ES', { month: 'short' });
    const year = d.getFullYear().toString().slice(-2);
    return `${day}-${month}-${year}`;
  }

  formatPercentage(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return `${value.toFixed(decimals)}%`;
  }

  // ================== GUÍA GENÉTICA (desde BD: guia_genetica_sanmarino_colombia) ==================
  // Los valores de guía llegan poblados por el backend (ReporteTecnicoService) a partir de la guía
  // genética real del lote (raza + año). Si no hay fila de guía para la semana, se muestra "-" en
  // lugar de datos hardcodeados que "no corresponden" a la guía actual (REQ-002 g/i, REQ-010 d).
  // Los métodos conservan la firma (semana, valorDto) para no romper el template.

  getConsumoAcumuladoTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }

  getGrAveDiaTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }

  getIncrementoTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }

  /**
   * Peso de la guía. Llega en KILOS (misma unidad que `pesoH`, ver PesoLevanteCalculos en el
   * backend) y esta hoja muestra el bloque «PESO CORPORAL Grs.», así que se pasa a gramos —
   * igual que la celda del peso real, que ya multiplicaba por 1000.
   */
  getPesoTabla(_semana: number, valorKg?: number | null): number | null {
    return valorKg != null ? valorKg * 1000 : null;
  }

  getGananciaTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }
}

import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { SeguimientoDiarioTablaFilaDto } from '../../services/seguimiento-aves-engorde.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { ProductividadEngordeComputeService } from '../../services/productividad-engorde-compute.service';
import {
  ProductividadDiariaFila,
  ProductividadSemanalFila
} from '../../services/productividad-engorde.models';

/**
 * Gráficas de PRODUCTIVIDAD pollo engorde (Panamá). Solo datos reales del seguimiento diario.
 * Toggle Diaria / Semanal, dos gráficas por modo (sin filtros: se grafica el lote actual).
 */
@Component({
  selector: 'app-graficas-productividad-engorde',
  standalone: true,
  imports: [CommonModule, NgChartsModule],
  templateUrl: './graficas-productividad-engorde.component.html',
  styleUrls: ['./graficas-productividad-engorde.component.scss']
})
export class GraficasProductividadEngordeComponent implements OnChanges {
  @Input() tablaFilas: SeguimientoDiarioTablaFilaDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() loading = false;

  modo: 'diaria' | 'semanal' = 'diaria';

  diaria: ProductividadDiariaFila[] = [];
  semanal: ProductividadSemanalFila[] = [];

  // Desempeño diario (Gramos + Total QQ)
  chartDesempenoDiarioData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  chartDesempenoDiarioOptions: ChartConfiguration['options'] = {};
  // Mortalidad diaria (% + cantidades)
  chartMortDiariaData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  chartMortDiariaOptions: ChartConfiguration['options'] = {};

  // Desempeño productivo semanal (GRS + QQ + CA)
  chartDesempenoSemanalData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  chartDesempenoSemanalOptions: ChartConfiguration['options'] = {};
  // Mortalidad y selección acumulada semanal
  chartMortSemanalData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  chartMortSemanalOptions: ChartConfiguration['options'] = {};

  // Una gráfica por cada semana (DESEMPEÑO ACTUAL VS HISTORICOS - Semana N)
  chartsSemana: { semana: number; data: ChartData<'bar' | 'line'>; options: ChartConfiguration['options'] }[] = [];

  private readonly cBlue = 'rgba(99, 169, 232, 0.85)';
  private readonly cOrange = 'rgba(247, 165, 90, 0.85)';
  private readonly cTeal = 'rgba(80, 196, 178, 0.95)';
  private readonly cPurple = 'rgba(150, 130, 220, 0.85)';
  private readonly cPink = 'rgba(240, 110, 140, 0.95)';
  private readonly cOrangeBar = 'rgba(247, 180, 110, 0.6)';

  constructor(private compute: ProductividadEngordeComputeService) {}

  get hayDatos(): boolean {
    return this.diaria.length > 0;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['tablaFilas']) {
      this.rebuild();
    }
  }

  setModo(modo: 'diaria' | 'semanal'): void {
    this.modo = modo;
  }

  private rebuild(): void {
    const res = this.compute.compute(this.tablaFilas ?? []);
    this.diaria = res.diaria;
    this.semanal = res.semanal;
    this.chartsSemana = [];
    if (this.diaria.length) {
      this.buildDiaria(this.diaria);
    }
    if (this.semanal.length) {
      this.buildSemanal(this.semanal);
    }
  }

  // ─── Diaria ──────────────────────────────────────────────────────────────────

  private buildDiaria(filas: ProductividadDiariaFila[]): void {
    const labels = filas.map(f => `${f.edadDia}`);

    this.chartDesempenoDiarioData = {
      labels,
      datasets: [
        {
          type: 'bar',
          label: 'Gramos (g)',
          data: filas.map(f => (f.gramos > 0 ? f.gramos : null)),
          backgroundColor: this.cBlue,
          yAxisID: 'y'
        },
        {
          type: 'line',
          label: 'Total QQ',
          data: filas.map(f => (f.qq > 0 ? f.qq : null)),
          borderColor: this.cPink,
          backgroundColor: this.cPink,
          yAxisID: 'y1',
          tension: 0.2,
          pointRadius: 2,
          borderWidth: 2
        }
      ]
    };
    this.chartDesempenoDiarioOptions = this.comboOptions(
      'Desempeño diario',
      'Edad (día)',
      'Gramos (grs)',
      'Total QQ'
    );

    this.chartMortDiariaData = {
      labels,
      datasets: [
        { type: 'bar', label: '% Mortalidad', data: filas.map(f => f.pctMortalidad), backgroundColor: this.cTeal, yAxisID: 'y' },
        { type: 'bar', label: '% Selección', data: filas.map(f => f.pctSeleccion), backgroundColor: this.cPurple, yAxisID: 'y' },
        { type: 'bar', label: '% Mortalidad + Selección', data: filas.map(f => f.pctMortSel), backgroundColor: this.cOrangeBar, yAxisID: 'y' },
        {
          type: 'line', label: 'Mortalidad Total',
          data: filas.map(f => f.mortalidadTotalAcum),
          borderColor: this.cPink, backgroundColor: this.cPink, yAxisID: 'y1',
          tension: 0.2, pointRadius: 2, borderWidth: 2
        },
        {
          type: 'line', label: 'Selección Total',
          data: filas.map(f => f.seleccionTotalAcum),
          borderColor: this.cBlue, backgroundColor: this.cBlue, yAxisID: 'y1',
          tension: 0.2, pointRadius: 2, borderWidth: 2
        }
      ]
    };
    this.chartMortDiariaOptions = this.comboOptions(
      'Mortalidad diaria',
      'Edad (día)',
      'Porcentajes (%)',
      'Cantidades Totales'
    );
  }

  // ─── Semanal ─────────────────────────────────────────────────────────────────

  private buildSemanal(filas: ProductividadSemanalFila[]): void {
    const labels = filas.map(f => `Sem ${f.semana}`);

    this.chartDesempenoSemanalData = {
      labels,
      datasets: [
        { type: 'bar', label: 'GRS', data: filas.map(f => (f.grs > 0 ? f.grs : null)), backgroundColor: this.cBlue, yAxisID: 'y' },
        { type: 'bar', label: 'QQ', data: filas.map(f => (f.qq > 0 ? f.qq : null)), backgroundColor: this.cOrange, yAxisID: 'y' },
        {
          type: 'line', label: 'CA',
          data: filas.map(f => (f.ca > 0 ? f.ca : null)),
          borderColor: this.cTeal, backgroundColor: this.cTeal, yAxisID: 'y1',
          tension: 0.2, pointRadius: 3, borderWidth: 2
        }
      ]
    };
    this.chartDesempenoSemanalOptions = this.comboOptions(
      'Desempeño productivo',
      'Semana',
      'grs / QQ',
      'Conversión Alimenticia (CA)'
    );

    this.chartMortSemanalData = {
      labels,
      datasets: [
        { type: 'bar', label: '% Mortalidad', data: filas.map(f => f.pctMortalidad), backgroundColor: this.cTeal, yAxisID: 'y' },
        { type: 'bar', label: '% Selección', data: filas.map(f => f.pctSeleccion), backgroundColor: this.cPurple, yAxisID: 'y' },
        { type: 'bar', label: '% Mortalidad + Selección', data: filas.map(f => f.pctMortSel), backgroundColor: this.cOrangeBar, yAxisID: 'y' },
        {
          type: 'line', label: 'Mortalidad Total',
          data: filas.map(f => f.mortalidadTotalAcum),
          borderColor: this.cPink, backgroundColor: this.cPink, yAxisID: 'y1',
          tension: 0.2, pointRadius: 2, borderWidth: 2
        },
        {
          type: 'line', label: 'Selección Total',
          data: filas.map(f => f.seleccionTotalAcum),
          borderColor: this.cBlue, backgroundColor: this.cBlue, yAxisID: 'y1',
          tension: 0.2, pointRadius: 2, borderWidth: 2
        }
      ]
    };
    this.chartMortSemanalOptions = this.comboOptions(
      'Mortalidad y selección acumulada',
      'Semana',
      'Porcentajes (%)',
      'Cantidades Totales'
    );

    this.buildChartsPorSemana(filas);
  }

  /** Una gráfica por semana: GRS y QQ (barras) + CA (punto), con el lote en el eje X. */
  private buildChartsPorSemana(filas: ProductividadSemanalFila[]): void {
    const loteLabel = this.loteLabel();
    this.chartsSemana = filas.map(f => ({
      semana: f.semana,
      data: {
        labels: [loteLabel],
        datasets: [
          { type: 'bar', label: 'GRS', data: [f.grs > 0 ? f.grs : null], backgroundColor: this.cBlue, yAxisID: 'y' },
          { type: 'bar', label: 'QQ', data: [f.qq > 0 ? f.qq : null], backgroundColor: this.cOrange, yAxisID: 'y' },
          {
            type: 'line', label: 'CA',
            data: [f.ca > 0 ? f.ca : null],
            borderColor: this.cTeal, backgroundColor: this.cTeal, yAxisID: 'y1',
            pointRadius: 5, pointHoverRadius: 6, showLine: false
          }
        ]
      } as ChartData<'bar' | 'line'>,
      options: this.comboOptions(
        `DESEMPEÑO ACTUAL VS HISTÓRICOS - Semana ${f.semana}`,
        'Lote',
        'GRS / QQ',
        'Conversión Alimenticia (CA)'
      )
    }));
  }

  private loteLabel(): string {
    if (!this.selectedLote) return 'Lote';
    return String(this.selectedLote.loteNombre || `#${this.selectedLote.loteId}`);
  }

  // ─── Opciones ────────────────────────────────────────────────────────────────

  private comboOptions(
    title: string,
    xLabel: string,
    yLeft: string,
    yRight: string
  ): ChartConfiguration['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        title: { display: true, text: title, font: { size: 13, weight: 'bold' } },
        legend: { position: 'top' }
      },
      scales: {
        x: { title: { display: true, text: xLabel }, ticks: { maxRotation: 45, minRotation: 0 } },
        y: {
          type: 'linear',
          position: 'left',
          beginAtZero: true,
          title: { display: true, text: yLeft },
          grid: { color: 'rgba(0,0,0,0.06)' }
        },
        y1: {
          type: 'linear',
          position: 'right',
          beginAtZero: true,
          title: { display: true, text: yRight },
          grid: { drawOnChartArea: false }
        }
      }
    };
  }
}

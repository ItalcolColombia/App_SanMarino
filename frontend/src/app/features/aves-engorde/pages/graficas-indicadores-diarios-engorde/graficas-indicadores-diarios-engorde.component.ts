import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { SeguimientoLoteLevanteDto } from '../../../lote-levante/services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';
import { IndicadoresDiariosEngordeComputeService } from '../../services/indicadores-diarios-engorde-compute.service';
import { IndicadorDiarioFila } from '../../../lote-levante/services/indicadores-diarios.models';

/** Cuatro gráficas fijas (datos del lote vs guía genética), mismo criterio que la tabla de indicadores diarios. */
@Component({
  selector: 'app-graficas-indicadores-diarios-engorde',
  standalone: true,
  imports: [CommonModule, NgChartsModule],
  templateUrl: './graficas-indicadores-diarios-engorde.component.html',
  styleUrls: ['./graficas-indicadores-diarios-engorde.component.scss']
})
export class GraficasIndicadoresDiariosEngordeComponent implements OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  @Input() selectedLote: LoteDto | LotePosturaLevanteDto | null = null;
  @Input() loading = false;

  cargando = false;
  errorGuia: string | null = null;
  etiquetaGuia = '';
  filas: IndicadorDiarioFila[] = [];

  chart1Data: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  chart1Options: ChartConfiguration['options'] = {};

  chart2Data: ChartData<'bar'> = { labels: [], datasets: [] };
  chart2Options: ChartConfiguration['options'] = {};

  chart3Data: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  chart3Options: ChartConfiguration['options'] = {};

  chart4Data: ChartData<'bar'> = { labels: [], datasets: [] };
  chart4Options: ChartConfiguration['options'] = {};

  private readonly cPeach = 'rgba(255, 183, 143, 0.9)';
  private readonly cYellow = 'rgba(255, 214, 102, 0.9)';
  private readonly cGreen = 'rgba(130, 200, 150, 0.9)';
  private readonly cBrown = 'rgba(160, 120, 90, 0.9)';

  constructor(private compute: IndicadoresDiariosEngordeComputeService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      void this.rebuild();
    }
  }

  private async rebuild(): Promise<void> {
    this.errorGuia = null;
    this.filas = [];
    this.clearCharts();

    if (!this.seguimientos?.length || !this.selectedLote) {
      return;
    }

    this.cargando = true;
    try {
      const res = await this.compute.compute(this.seguimientos, this.selectedLote);
      this.errorGuia = res.errorGuia;
      this.etiquetaGuia = res.etiquetaGuiaCargada;
      this.filas = res.filas;
      if (res.filas.length && !res.errorGuia) {
        this.buildAllCharts(res.filas);
      }
    } finally {
      this.cargando = false;
    }
  }

  private clearCharts(): void {
    const empty = { labels: [] as string[], datasets: [] };
    this.chart1Data = empty as ChartData<'bar' | 'line'>;
    this.chart2Data = empty as ChartData<'bar'>;
    this.chart3Data = empty as ChartData<'bar' | 'line'>;
    this.chart4Data = empty as ChartData<'bar'>;
  }

  private labels(filas: IndicadorDiarioFila[]): string[] {
    return filas.map(f => `Día ${f.dia}`);
  }

  private buildAllCharts(filas: IndicadorDiarioFila[]): void {
    if (!this.selectedLote) {
      return;
    }
    const lote = this.selectedLote;
    const labels = this.labels(filas);

    // 1) Peso (g) + CA reg. + CA guía + línea edad (día)
    this.chart1Data = {
      labels,
      datasets: [
        {
          type: 'bar',
          label: 'Peso (g) reg.',
          data: filas.map(f => (f.pesoRealG > 0 ? f.pesoRealG : null)),
          backgroundColor: this.cPeach,
          yAxisID: 'y'
        },
        {
          type: 'bar',
          label: 'Peso (g) guía',
          data: filas.map(f => (f.pesoTablaG > 0 ? f.pesoTablaG : null)),
          backgroundColor: 'rgba(255, 140, 120, 0.55)',
          yAxisID: 'y'
        },
        {
          type: 'bar',
          label: 'CA reg.',
          data: filas.map(f => (f.caReal != null && f.caReal > 0 ? f.caReal : null)),
          backgroundColor: this.cYellow,
          yAxisID: 'y1'
        },
        {
          type: 'bar',
          label: 'CA guía',
          data: filas.map(f => (f.caTabla > 0 ? f.caTabla : null)),
          backgroundColor: this.cGreen,
          yAxisID: 'y1'
        },
        {
          type: 'line',
          label: 'Día de vida',
          data: filas.map(f => f.dia),
          borderColor: this.cBrown,
          backgroundColor: this.cBrown,
          yAxisID: 'y2',
          tension: 0.2,
          pointRadius: 3,
          borderWidth: 2,
          order: 0
        }
      ]
    };
    this.chart1Options = this.chartOptionsCombo(
      'Peso corporal (g), CA y día de vida (línea)',
      'Peso (g)',
      'CA',
      'Día'
    );

    // 2) Ganancia diaria (g) reg. vs guía — equivalente a comparar productividad vs estándar
    this.chart2Data = {
      labels,
      datasets: [
        {
          label: 'Ganancia (g) reg.',
          data: filas.map(f => (f.gananciaDiariaRealG != null ? f.gananciaDiariaRealG : null)),
          backgroundColor: this.cPeach
        },
        {
          label: 'Ganancia (g) guía',
          data: filas.map(f => (f.gananciaDiariaTablaG > 0 ? f.gananciaDiariaTablaG : null)),
          backgroundColor: this.cYellow
        }
      ]
    };
    this.chart2Options = this.chartOptionsSimple('Ganancia diaria (g): registro vs guía', 'g');

    // 3) % mort.+sel. día (reg), % guía, línea: mortalidad % solo 1ª semana (días vida 0–7, solo mort. H+M)
    const mortPctSemana1 = this.mortalidadSoloPrimeraSemanaPct(this.seguimientos, lote);
    const lineaSemana1 = filas.map(() => mortPctSemana1);

    this.chart3Data = {
      labels,
      datasets: [
        {
          type: 'bar',
          label: '% Mort.+sel. día (reg.)',
          data: filas.map(f => f.mortSelRealPct),
          backgroundColor: this.cPeach
        },
        {
          type: 'bar',
          label: '% Mort.+sel. día (guía)',
          data: filas.map(f => (f.mortSelTablaPct > 0 ? f.mortSelTablaPct : null)),
          backgroundColor: this.cYellow
        },
        {
          type: 'line',
          label: '% Mortalidad 1ª semana (días 0–7, solo mort.)',
          data: lineaSemana1,
          borderColor: this.cGreen,
          backgroundColor: 'rgba(130, 200, 150, 0.25)',
          borderWidth: 2,
          pointRadius: 0,
          pointHoverRadius: 4,
          fill: false,
          tension: 0,
          yAxisID: 'y'
        }
      ]
    };
    this.chart3Options = this.chartOptionsSimple(
      'Mortalidad y selección (% día) · línea verde = % mortalidad acumulada solo en días de vida 0–7 (sin selección)',
      '%'
    );

    // 4) GAD y CADA (g/g/ave) reg. vs guía
    this.chart4Data = {
      labels,
      datasets: [
        {
          label: 'GAD (g) reg.',
          data: filas.map(f => (f.gananciaDiariaRealG != null ? f.gananciaDiariaRealG : null)),
          backgroundColor: this.cPeach
        },
        {
          label: 'GAD (g) guía',
          data: filas.map(f => (f.gananciaDiariaTablaG > 0 ? f.gananciaDiariaTablaG : null)),
          backgroundColor: this.cYellow
        },
        {
          label: 'CADA (g/ave) reg.',
          data: filas.map(f => (f.consumoDiarioRealG > 0 ? f.consumoDiarioRealG : null)),
          backgroundColor: this.cGreen
        },
        {
          label: 'CADA (g/ave) guía',
          data: filas.map(f => (f.consumoDiarioTablaG > 0 ? f.consumoDiarioTablaG : null)),
          backgroundColor: this.cBrown
        }
      ]
    };
    this.chart4Options = this.chartOptionsSimple(
      'Ganancia diaria y consumo alimentario diario (g/g/ave)',
      'g'
    );
  }

  private chartOptionsSimple(
    title: string,
    unit: string,
    skipBeginZero = false
  ): ChartConfiguration['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        title: { display: true, text: title, font: { size: 13, weight: 'bold' } },
        legend: { position: 'top' }
      },
      scales: {
        x: { ticks: { maxRotation: 45, minRotation: 0 } },
        y: {
          beginAtZero: !skipBeginZero,
          title: { display: true, text: unit }
        }
      }
    };
  }

  /**
   * % sobre aves iniciales del lote: suma solo mortalidad H+M en registros cuyo día de vida está en [0,7].
   * No incluye selección ni error de sexaje.
   */
  private mortalidadSoloPrimeraSemanaPct(
    seguimientos: SeguimientoLoteLevanteDto[],
    lote: LoteDto | LotePosturaLevanteDto
  ): number {
    const inicial = this.avesInicialesLote(lote);
    if (inicial <= 0 || !seguimientos?.length) {
      return 0;
    }
    let mortSemana1 = 0;
    for (const r of seguimientos) {
      const dia = this.calcularDiaVida(lote, r.fechaRegistro);
      if (dia >= 0 && dia <= 7) {
        mortSemana1 += (r.mortalidadHembras ?? 0) + (r.mortalidadMachos ?? 0);
      }
    }
    return (mortSemana1 / inicial) * 100;
  }

  private avesInicialesLote(l: LoteDto | LotePosturaLevanteDto): number {
    const n = l.avesEncasetadas;
    if (n != null && n > 0) {
      return n;
    }
    const ld = l as LoteDto;
    const h = ld?.hembrasL ?? 0;
    const m = ld?.machosL ?? 0;
    const x = ld?.mixtas ?? 0;
    const t = h + m + x;
    return t > 0 ? t : 0;
  }

  private calcularDiaVida(
    selectedLote: LoteDto | LotePosturaLevanteDto,
    fechaRegistro: string | Date
  ): number {
    const encYmd = this.toYMD(selectedLote?.fechaEncaset);
    const regYmd = this.toYMD(fechaRegistro);
    if (!encYmd || !regYmd) {
      return -1;
    }
    const MS_DAY = 24 * 60 * 60 * 1000;
    const enc = this.ymdToLocalNoonDate(encYmd);
    const reg = this.ymdToLocalNoonDate(regYmd);
    if (!enc || !reg) {
      return -1;
    }
    return Math.max(0, Math.floor((reg.getTime() - enc.getTime()) / MS_DAY));
  }

  private toYMD(value: string | Date | null | undefined): string | null {
    if (value == null || value === '') {
      return null;
    }
    if (typeof value === 'string') {
      const m = value.match(/^(\d{4}-\d{2}-\d{2})/);
      if (m) {
        return m[1];
      }
      const d = new Date(value);
      if (isNaN(d.getTime())) {
        return null;
      }
      const y = d.getFullYear();
      const mo = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${y}-${mo}-${day}`;
    }
    const d = value;
    const y = d.getFullYear();
    const mo = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${mo}-${day}`;
  }

  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) {
      return null;
    }
    const m = ymd.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (!m) {
      return null;
    }
    const y = Number(m[1]);
    const mo = Number(m[2]) - 1;
    const day = Number(m[3]);
    return new Date(y, mo, day, 12, 0, 0, 0);
  }

  private chartOptionsCombo(
    title: string,
    yLeft: string,
    yRight1: string,
    yRight2: string
  ): ChartConfiguration['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        title: { display: true, text: title, font: { size: 13, weight: 'bold' } },
        legend: { position: 'top' }
      },
      scales: {
        x: { ticks: { maxRotation: 45, minRotation: 0 } },
        y: {
          type: 'linear',
          position: 'left',
          title: { display: true, text: yLeft },
          grid: { color: 'rgba(0,0,0,0.06)' }
        },
        y1: {
          type: 'linear',
          position: 'right',
          title: { display: true, text: yRight1 },
          grid: { drawOnChartArea: false }
        },
        y2: {
          type: 'linear',
          position: 'right',
          offset: true,
          title: { display: true, text: yRight2 },
          grid: { drawOnChartArea: false }
        }
      }
    };
  }
}

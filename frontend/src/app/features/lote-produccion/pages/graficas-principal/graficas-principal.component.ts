import { Component, Input, OnInit, OnChanges, SimpleChanges, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { SeguimientoItemDto, ProduccionService, IndicadorProduccionSemanalDto, IndicadoresProduccionRequest } from '../../services/produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';

interface IndicadorSemanal {
  semana: number;
  fechaInicio: string;
  avesInicioSemana: number;
  avesFinSemana: number;
  consumoReal: number;
  consumoTabla: number;
  conversionAlimenticia: number;
  huevosTotales: number;
  huevosIncubables: number;
  mortalidadHembras: number;
  mortalidadMachos: number;
  mortalidadTotal: number;
  eficiencia: number;
  ip: number;
  vpi: number;
  mortalidadAcum: number;
  huevosTotalesAcum: number;
  huevosIncubablesAcum: number;
  porcentajeIncubables: number;
  pesoHuevoPromedio: number;
}

@Component({
  selector: 'app-graficas-principal',
  standalone: true,
  imports: [CommonModule, FormsModule, NgChartsModule],
  templateUrl: './graficas-principal.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./graficas-principal.component.scss']
})
export class GraficasPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  /** Fecha de encaset (fuente confiable para calcular semanas de edad). */
  @Input() fechaEncaset: string | Date | null = null;
  @Input() loading: boolean = false;
  /** IDs para traer los indicadores del API (mismos que usa la tabla): comparación REAL vs GUÍA. */
  @Input() lotePosturaProduccionId: number | null = null;
  @Input() produccionLoteId: number | null = null;

  private readonly produccionSvc = inject(ProduccionService);

  // ===== Comparativo Real vs Guía (alimentado por el API de indicadores, ya corregido) =====
  indicadoresApi: IndicadorProduccionSemanalDto[] = [];
  cargandoComparativo = false;
  metricaComparativa: 'consumo' | 'peso' | 'produccion' | 'mortalidad' | 'htaa' = 'consumo';
  sexoComparativo: 'H' | 'M' = 'H';
  comparativoGuiaChartData: ChartData<'line'> = { labels: [], datasets: [] };
  comparativoGuiaChartOptions: ChartConfiguration['options'] = {};

  // Constantes para producción
  readonly SEMANA_INICIO_PRODUCCION = 26;
  readonly SEMANA_MAX_PRODUCCION = 75;

  // Datos para gráficas
  indicadoresSemanales: IndicadorSemanal[] = [];

  // ========== SELECTORES DE GRÁFICAS ==========
  tipoGraficaSeleccionada: 'huevos' | 'mortalidad' | 'consumo' | 'eficiencia' | 'aves' = 'huevos';
  tipoVisualizacion: 'linea' | 'barra' | 'torta' = 'barra';

  // ========== SELECTOR COMPARATIVO ==========
  semanasDisponibles: number[] = [];
  semanaComparacion1: number | null = null;
  semanaComparacion2: number | null = null;
  mostrarComparativo: boolean = false;

  // ========== DATOS DE GRÁFICAS CHART.JS ==========
  // Gráfica de Huevos
  huevosChartData: ChartData<'bar' | 'line' | 'pie'> = { labels: [], datasets: [] };
  huevosChartOptions: ChartConfiguration['options'] = {};

  // Gráfica de Mortalidad
  mortalidadChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  mortalidadChartOptions: ChartConfiguration['options'] = {};

  // Gráfica de Consumo
  consumoChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  consumoChartOptions: ChartConfiguration['options'] = {};

  // Gráfica de Eficiencia
  eficienciaChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  eficienciaChartOptions: ChartConfiguration['options'] = {};

  // Gráfica de Aves
  avesChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  avesChartOptions: ChartConfiguration['options'] = {};

  // Gráfica Comparativa (2 semanas)
  comparativoChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  comparativoChartOptions: ChartConfiguration['options'] = {};

  // Gráfica de Torta (distribución)
  tortaChartData: ChartData<'pie' | 'doughnut'> = { labels: [], datasets: [] };
  tortaChartOptions: ChartConfiguration['options'] = {};

  // Tipos de gráficas
  barChartType: ChartType = 'bar';
  lineChartType: ChartType = 'line';
  pieChartType: ChartType = 'pie';
  doughnutChartType: ChartType = 'doughnut';

  constructor() {
    this.initChartOptions();
  }

  ngOnInit(): void {
    this.prepararDatosGraficas();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      this.prepararDatosGraficas();
    }
    if (changes['lotePosturaProduccionId'] || changes['produccionLoteId']) {
      this.cargarIndicadoresApi();
    }
  }

  // ================== COMPARATIVO REAL vs GUÍA (desde el API de indicadores) ==================
  /** Trae los indicadores semanales del API (mismos que la tabla: calcs corregidos + guía por semana). */
  private cargarIndicadoresApi(): void {
    const hasLpp = this.lotePosturaProduccionId != null && this.lotePosturaProduccionId > 0;
    const hasLegacy = this.produccionLoteId != null && this.produccionLoteId > 0;
    if (!hasLpp && !hasLegacy) { this.indicadoresApi = []; this.construirComparativoGuia(); return; }

    const req: IndicadoresProduccionRequest = {
      loteId: hasLegacy ? this.produccionLoteId! : 0,
      lotePosturaProduccionId: hasLpp ? this.lotePosturaProduccionId! : null,
      semanaDesde: 26,
      semanaHasta: null,
      fechaDesde: null,
      fechaHasta: null
    };
    this.cargandoComparativo = true;
    this.produccionSvc.obtenerIndicadoresSemanales(req).subscribe({
      next: resp => {
        this.indicadoresApi = resp.indicadores || [];
        this.construirComparativoGuia();
        this.prepararDatosGraficas(); // reconstruye TODAS las gráficas desde la fuente correcta
        this.cargandoComparativo = false;
      },
      error: () => { this.indicadoresApi = []; this.construirComparativoGuia(); this.cargandoComparativo = false; }
    });
  }

  /**
   * Mapea los indicadores del API (IndicadorProduccionSemanalDto) al modelo IndicadorSemanal que
   * usan las gráficas. Reemplaza el cálculo client-side (que tenía consumoTabla=157 hardcodeado y
   * conversión alimenticia, que no aplica a reproductoras). El "consumo tabla" pasa a ser la GUÍA
   * real convertida a kg/semana; la conversión alimenticia se anula.
   */
  private mapApiToIndicadorSemanal(api: IndicadorProduccionSemanalDto[]): IndicadorSemanal[] {
    let mortAcum = 0, huevTotAcum = 0, huevIncAcum = 0;
    return (api || []).map(d => {
      const avesIni = (d.avesHembrasInicioSemana || 0) + (d.avesMachosInicioSemana || 0);
      const avesFin = (d.avesHembrasFinSemana || 0) + (d.avesMachosFinSemana || 0);
      const mortH = d.porcentajeMortalidadHembras || 0;
      const mortM = d.porcentajeMortalidadMachos || 0;
      const huevTot = d.huevosTotales || 0;
      const huevInc = d.huevosIncubables || 0;
      // Guía de consumo (g/ave/día) → kg/semana comparable: g/ave/día × aves × 7 / 1000
      const consumoGuiaKgSemana =
        (((d.consumoGuiaHembras ?? 0) * (d.avesHembrasInicioSemana || 0)) +
         ((d.consumoGuiaMachos ?? 0) * (d.avesMachosInicioSemana || 0))) * 7 / 1000;
      mortAcum += (mortH + mortM);
      huevTotAcum += huevTot;
      huevIncAcum += huevInc;
      return {
        semana: d.semana,
        fechaInicio: String(d.fechaInicioSemana ?? ''),
        avesInicioSemana: avesIni,
        avesFinSemana: avesFin,
        consumoReal: Number(d.consumoTotalKg ?? 0),
        consumoTabla: Number(consumoGuiaKgSemana.toFixed(2)),
        conversionAlimenticia: 0, // no aplica a reproductoras (REQ-002h)
        huevosTotales: huevTot,
        huevosIncubables: huevInc,
        mortalidadHembras: mortH,
        mortalidadMachos: mortM,
        mortalidadTotal: mortH + mortM,
        eficiencia: d.eficienciaProduccion || 0,
        ip: 0,
        vpi: 0,
        mortalidadAcum: mortAcum,
        huevosTotalesAcum: huevTotAcum,
        huevosIncubablesAcum: huevIncAcum,
        porcentajeIncubables: huevTot > 0 ? (huevInc / huevTot) * 100 : 0,
        pesoHuevoPromedio: d.pesoHuevoPromedio ?? 0
      } as IndicadorSemanal;
    });
  }

  /** Reconstruye la gráfica comparativa Real vs Guía según la métrica y sexo seleccionados. */
  construirComparativoGuia(): void {
    const data = this.indicadoresApi || [];
    const labels = data.map(d => `S${d.semana}`);
    const sexo = this.sexoComparativo;

    const real: (number | null)[] = [];
    const guia: (number | null)[] = [];
    let etiqueta = '';
    let unidad = '';

    for (const d of data) {
      switch (this.metricaComparativa) {
        case 'consumo': {
          etiqueta = `Consumo ${sexo}`; unidad = 'g/ave/día';
          const kg = sexo === 'H' ? d.consumoKgHembras : d.consumoKgMachos;
          const aves = sexo === 'H' ? d.avesHembrasInicioSemana : d.avesMachosInicioSemana;
          const dias = d.totalRegistros || 0;
          real.push(aves > 0 && dias > 0 ? Number(((kg * 1000) / (dias * aves)).toFixed(1)) : null);
          guia.push(sexo === 'H' ? (d.consumoGuiaHembras ?? null) : (d.consumoGuiaMachos ?? null));
          break;
        }
        case 'peso': {
          etiqueta = `Peso ${sexo}`; unidad = 'kg';
          real.push(sexo === 'H' ? (d.pesoPromedioHembras ?? null) : (d.pesoPromedioMachos ?? null));
          guia.push(sexo === 'H' ? (d.pesoGuiaHembras ?? null) : (d.pesoGuiaMachos ?? null));
          break;
        }
        case 'mortalidad': {
          etiqueta = `% Mortalidad ${sexo}`; unidad = '%';
          real.push(sexo === 'H' ? d.porcentajeMortalidadHembras : d.porcentajeMortalidadMachos);
          guia.push(sexo === 'H' ? (d.mortalidadGuiaHembras ?? null) : (d.mortalidadGuiaMachos ?? null));
          break;
        }
        case 'produccion': {
          etiqueta = '% Producción'; unidad = '%';
          real.push(d.eficienciaProduccion ?? null);
          guia.push(d.porcentajeProduccionGuia ?? null);
          break;
        }
        case 'htaa': {
          etiqueta = 'H.T.A.A (acum/ave)'; unidad = 'huevos/ave';
          real.push(d.htaaReal ?? null);
          guia.push(d.huevosTotalesGuia ?? null);
          break;
        }
      }
    }

    this.comparativoGuiaChartData = {
      labels,
      datasets: [
        { data: real, label: `${etiqueta} — Real`, borderColor: '#F5821F', backgroundColor: 'rgba(245,130,31,0.15)', tension: 0.3, spanGaps: true, pointRadius: 2 },
        { data: guia, label: `${etiqueta} — Guía`, borderColor: '#FBB040', backgroundColor: 'rgba(251,176,64,0.10)', borderDash: [6, 4], tension: 0.3, spanGaps: true, pointRadius: 2 }
      ]
    };
    this.comparativoGuiaChartOptions = {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: true, position: 'top' },
        title: { display: true, text: `${etiqueta} — Real vs Guía (${unidad})` }
      },
      scales: { y: { beginAtZero: false, title: { display: true, text: unidad } } }
    };
  }

  // ========== INICIALIZACIÓN DE OPCIONES DE GRÁFICAS ==========
  private initChartOptions(): void {
    const baseOptions: ChartConfiguration['options'] = {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: 'top',
          labels: {
            color: '#374151',
            font: {
              family: 'Inter, sans-serif',
              size: 12,
              weight: 'normal'
            },
            padding: 15,
            usePointStyle: true
          }
        },
        tooltip: {
          backgroundColor: 'rgba(0, 0, 0, 0.8)',
          titleColor: '#fff',
          bodyColor: '#fff',
          borderColor: '#d32f2f',
          borderWidth: 2,
          cornerRadius: 8,
          displayColors: true,
          padding: 12
        }
      },
      scales: {
        y: {
          beginAtZero: true,
          grid: {
            color: 'rgba(107, 114, 128, 0.1)'
          },
          ticks: {
            color: '#374151',
            font: {
              family: 'Inter, sans-serif',
              size: 11
            }
          }
        },
        x: {
          grid: {
            color: 'rgba(107, 114, 128, 0.1)'
          },
          ticks: {
            color: '#374151',
            font: {
              family: 'Inter, sans-serif',
              size: 11
            }
          }
        }
      }
    };

    // Opciones para barras
    this.huevosChartOptions = { ...baseOptions };
    this.mortalidadChartOptions = { ...baseOptions };
    this.consumoChartOptions = { ...baseOptions };
    this.eficienciaChartOptions = { ...baseOptions };
    this.avesChartOptions = { ...baseOptions };
    this.comparativoChartOptions = { ...baseOptions };

    // Opciones para torta
    this.tortaChartOptions = {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: 'right',
          labels: {
            color: '#374151',
            font: {
              family: 'Inter, sans-serif',
              size: 12,
              weight: 'normal'
            },
            padding: 15,
            usePointStyle: true
          }
        },
        tooltip: {
          backgroundColor: 'rgba(0, 0, 0, 0.8)',
          titleColor: '#fff',
          bodyColor: '#fff',
          borderColor: '#d32f2f',
          borderWidth: 2,
          cornerRadius: 8,
          displayColors: true,
          padding: 12,
          callbacks: {
            label: (context: any) => {
              const label = context.label || '';
              const value = context.parsed || 0;
              const total = context.dataset.data.reduce((a: number, b: number) => a + b, 0);
              const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : 0;
              return `${label}: ${value} (${percentage}%)`;
            }
          }
        }
      }
    };
  }

  // ================== PREPARACIÓN DE DATOS ==================
  private prepararDatosGraficas(): void {
    // FUENTE ÚNICA: los indicadores del API (calculados en la BD, con guía por semana).
    // El front ya NO calcula: sin el fallback client-side legacy (que tenía consumoTabla=157
    // hardcodeado y conversión alimenticia, que no aplica a reproductoras).
    if (this.indicadoresApi && this.indicadoresApi.length > 0) {
      this.indicadoresSemanales = this.mapApiToIndicadorSemanal(this.indicadoresApi);
    } else {
      this.indicadoresSemanales = [];
      this.semanasDisponibles = [];
      return;
    }

    // Actualizar semanas disponibles (solo semanas de producción: 25-75)
    this.semanasDisponibles = this.indicadoresSemanales
      .map(ind => ind.semana)
      .filter(s => s >= this.SEMANA_INICIO_PRODUCCION && s <= this.SEMANA_MAX_PRODUCCION)
      .sort((a, b) => a - b);

    // Si no hay semanas seleccionadas para comparación, seleccionar las primeras dos si existen
    if (!this.semanaComparacion1 && this.semanasDisponibles.length > 0) {
      this.semanaComparacion1 = this.semanasDisponibles[0];
    }
    if (!this.semanaComparacion2 && this.semanasDisponibles.length > 1) {
      this.semanaComparacion2 = this.semanasDisponibles[1];
    }

    // Preparar datos de Chart.js
    this.prepararChartData();
  }

  // ========== PREPARACIÓN DE DATOS PARA CHART.JS ==========
  private prepararChartData(): void {
    if (this.indicadoresSemanales.length === 0) return;

    const labels = this.indicadoresSemanales.map(ind => `Semana ${ind.semana}`);

    // Gráfica de Huevos
    this.huevosChartData = {
      labels,
      datasets: [
        {
          label: 'Huevos Totales',
          data: this.indicadoresSemanales.map(ind => ind.huevosTotales || 0),
          backgroundColor: 'rgba(34, 197, 94, 0.7)',
          borderColor: 'rgba(34, 197, 94, 1)',
          borderWidth: 2
        },
        {
          label: 'Huevos Incubables',
          data: this.indicadoresSemanales.map(ind => ind.huevosIncubables || 0),
          backgroundColor: 'rgba(22, 163, 74, 0.7)',
          borderColor: 'rgba(22, 163, 74, 1)',
          borderWidth: 2
        }
      ]
    };

    // Gráfica de Mortalidad
    this.mortalidadChartData = {
      labels,
      datasets: [
        {
          label: 'Mortalidad Hembras (%)',
          data: this.indicadoresSemanales.map(ind => ind.mortalidadHembras || 0),
          backgroundColor: 'rgba(239, 68, 68, 0.7)',
          borderColor: 'rgba(239, 68, 68, 1)',
          borderWidth: 2
        },
        {
          label: 'Mortalidad Machos (%)',
          data: this.indicadoresSemanales.map(ind => ind.mortalidadMachos || 0),
          backgroundColor: 'rgba(220, 38, 38, 0.7)',
          borderColor: 'rgba(220, 38, 38, 1)',
          borderWidth: 2
        }
      ]
    };

    // Gráfica de Consumo
    this.consumoChartData = {
      labels,
      datasets: [
        {
          label: 'Consumo Real (kg/sem)',
          data: this.indicadoresSemanales.map(ind => ind.consumoReal || 0),
          backgroundColor: 'rgba(211, 47, 47, 0.7)',
          borderColor: 'rgba(211, 47, 47, 1)',
          borderWidth: 2
        },
        {
          label: 'Consumo Guía (kg/sem)',
          data: this.indicadoresSemanales.map(ind => ind.consumoTabla || 0),
          backgroundColor: 'rgba(25, 118, 210, 0.7)',
          borderColor: 'rgba(25, 118, 210, 1)',
          borderWidth: 2
        }
      ]
    };

    // Gráfica de Eficiencia
    this.eficienciaChartData = {
      labels,
      datasets: [{
        label: 'Eficiencia (%)',
        data: this.indicadoresSemanales.map(ind => ind.porcentajeIncubables || 0),
        backgroundColor: 'rgba(33, 150, 243, 0.7)',
        borderColor: 'rgba(33, 150, 243, 1)',
        borderWidth: 2,
        fill: false,
        tension: 0.4
      }]
    };

    // Gráfica de Aves
    this.avesChartData = {
      labels,
      datasets: [{
        label: 'Aves Vivas',
        data: this.indicadoresSemanales.map(ind => ind.avesFinSemana || 0),
        backgroundColor: 'rgba(123, 31, 162, 0.7)',
        borderColor: 'rgba(123, 31, 162, 1)',
        borderWidth: 2
      }]
    };

    // Gráfica de Torta (distribución huevos totales vs incubables en última semana)
    const ultimaSemana = this.indicadoresSemanales[this.indicadoresSemanales.length - 1];
    if (ultimaSemana && ultimaSemana.huevosTotales > 0) {
      const noIncubables = ultimaSemana.huevosTotales - ultimaSemana.huevosIncubables;
      this.tortaChartData = {
        labels: ['Huevos Incubables', 'Huevos No Incubables'],
        datasets: [{
          data: [
            ultimaSemana.huevosIncubables || 0,
            noIncubables
          ],
          backgroundColor: [
            'rgba(34, 197, 94, 0.8)',
            'rgba(239, 68, 68, 0.8)'
          ],
          borderColor: [
            'rgba(34, 197, 94, 1)',
            'rgba(239, 68, 68, 1)'
          ],
          borderWidth: 2
        }]
      };
    }

    // Actualizar gráfica comparativa si está activa
    if (this.mostrarComparativo) {
      this.actualizarGraficaComparativa();
    }
  }

  // ========== ACTUALIZAR GRÁFICA COMPARATIVA ==========
  actualizarGraficaComparativa(): void {
    if (!this.semanaComparacion1 || !this.semanaComparacion2) {
      this.comparativoChartData = { labels: [], datasets: [] };
      return;
    }

    const semana1 = this.indicadoresSemanales.find(ind => ind.semana === this.semanaComparacion1);
    const semana2 = this.indicadoresSemanales.find(ind => ind.semana === this.semanaComparacion2);

    if (!semana1 || !semana2) {
      this.comparativoChartData = { labels: [], datasets: [] };
      return;
    }

    const metricas = this.getMetricasParaComparacion();

    this.comparativoChartData = {
      labels: metricas.map(m => m.nombre),
      datasets: [
        {
          label: `Semana ${this.semanaComparacion1}`,
          data: metricas.map(m => m.semana1),
          backgroundColor: 'rgba(211, 47, 47, 0.7)',
          borderColor: 'rgba(211, 47, 47, 1)',
          borderWidth: 2
        },
        {
          label: `Semana ${this.semanaComparacion2}`,
          data: metricas.map(m => m.semana2),
          backgroundColor: 'rgba(25, 118, 210, 0.7)',
          borderColor: 'rgba(25, 118, 210, 1)',
          borderWidth: 2
        }
      ]
    };
  }

  private getMetricasParaComparacion(): Array<{ nombre: string; semana1: number; semana2: number }> {
    const semana1 = this.indicadoresSemanales.find(ind => ind.semana === this.semanaComparacion1);
    const semana2 = this.indicadoresSemanales.find(ind => ind.semana === this.semanaComparacion2);

    if (!semana1 || !semana2) return [];

    return [
      { nombre: 'Huevos Totales', semana1: semana1.huevosTotales || 0, semana2: semana2.huevosTotales || 0 },
      { nombre: 'Huevos Incubables', semana1: semana1.huevosIncubables || 0, semana2: semana2.huevosIncubables || 0 },
      { nombre: 'Eficiencia (%)', semana1: semana1.porcentajeIncubables || 0, semana2: semana2.porcentajeIncubables || 0 },
      { nombre: 'Mortalidad Total (%)', semana1: semana1.mortalidadTotal || 0, semana2: semana2.mortalidadTotal || 0 },
      { nombre: 'Consumo (kg)', semana1: semana1.consumoReal || 0, semana2: semana2.consumoReal || 0 },
      { nombre: 'Aves Vivas', semana1: semana1.avesFinSemana || 0, semana2: semana2.avesFinSemana || 0 }
    ];
  }

  // ========== MÉTODOS PÚBLICOS PARA CAMBIOS ==========
  onTipoGraficaChange(): void {
    // Los datos ya están preparados, solo cambia el canvas mostrado
  }

  onVisualizacionChange(): void {
    // Los datos ya están preparados, solo cambia el canvas mostrado
  }

  onSemanaComparacionChange(): void {
    if (this.semanaComparacion1 && this.semanaComparacion2) {
      this.actualizarGraficaComparativa();
    }
  }

  toggleComparativo(): void {
    this.mostrarComparativo = !this.mostrarComparativo;
    if (this.mostrarComparativo) {
      this.actualizarGraficaComparativa();
    }
  }

  getTituloGrafica(): string {
    const titulos: { [key: string]: string } = {
      'huevos': '🥚 Producción de Huevos por Semana',
      'mortalidad': '💀 Mortalidad por Semana',
      'consumo': '🍽️ Consumo de Alimento',
      'eficiencia': '📊 Eficiencia de Producción',
      'aves': '🐔 Aves Vivas por Semana'
    };
    return titulos[this.tipoGraficaSeleccionada] || '📊 Gráfica';
  }

  // ========== HELPERS ==========
  calcularPromedioIndicadores(propiedad: keyof IndicadorSemanal): number {
    if (this.indicadoresSemanales.length === 0) return 0;
    const suma = this.indicadoresSemanales.reduce((acc, ind) => acc + ((ind[propiedad] as number) || 0), 0);
    return suma / this.indicadoresSemanales.length;
  }
}

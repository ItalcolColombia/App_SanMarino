import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { SeguimientoItemDto } from '../../services/produccion.service';
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
  styleUrls: ['./graficas-principal.component.scss']
})
export class GraficasPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() loading: boolean = false;

  // Constantes para producción
  readonly SEMANA_INICIO_PRODUCCION = 25;
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
    if (!this.seguimientos || this.seguimientos.length === 0 || !this.selectedLote) {
      this.indicadoresSemanales = [];
      this.semanasDisponibles = [];
      return;
    }

    // Calcular indicadores semanales
    this.indicadoresSemanales = this.calcularIndicadoresSemanales();

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

  // ========== CÁLCULO DE INDICADORES SEMANALES ==========
  private calcularIndicadoresSemanales(): IndicadorSemanal[] {
    const registrosPorSemana = this.agruparPorSemana(this.seguimientos);
    const semanas = Array.from(registrosPorSemana.keys()).sort((a, b) => a - b);

    let avesAcumuladas = (this.selectedLote as any)?.avesInicialesH + (this.selectedLote as any)?.avesInicialesM || 0;
    let mortalidadAcumulada = 0;
    let huevosTotalesAcumulados = 0;
    let huevosIncubablesAcumulados = 0;

    return semanas.map(semana => {
      const registros = registrosPorSemana.get(semana) || [];

      // Solo procesar semanas de producción (25+)
      if (semana < this.SEMANA_INICIO_PRODUCCION) {
        return null;
      }

      const mortalidadHembrasTotal = registros.reduce((sum, r) => sum + (r.mortalidadH || 0), 0);
      const mortalidadMachosTotal = registros.reduce((sum, r) => sum + (r.mortalidadM || 0), 0);
      const mortalidadTotal = mortalidadHembrasTotal + mortalidadMachosTotal;
      const consumoTotal = registros.reduce((sum, r) => sum + (r.consumoKg || 0), 0);
      const huevosTotales = registros.reduce((sum, r) => sum + (r.huevosTotales || 0), 0);
      const huevosIncubables = registros.reduce((sum, r) => sum + (r.huevosIncubables || 0), 0);
      const pesoHuevoPromedio = registros.length > 0
        ? registros.reduce((sum, r) => sum + (r.pesoHuevo || 0), 0) / registros.length
        : 0;

      const avesFin = avesAcumuladas - mortalidadTotal;
      const consumoReal = consumoTotal;
      const consumoTabla = 157; // kg por semana (puede venir de tabla genética)
      const conversionAlimenticia = avesFin > 0 ? consumoReal / avesFin : 0;

      const mortalidadHembras = avesAcumuladas > 0 ? (mortalidadHembrasTotal / avesAcumuladas) * 100 : 0;
      const mortalidadMachos = avesAcumuladas > 0 ? (mortalidadMachosTotal / avesAcumuladas) * 100 : 0;
      const mortalidadTotalPorcentaje = mortalidadHembras + mortalidadMachos;

      const eficiencia = conversionAlimenticia > 0 ? huevosTotales / conversionAlimenticia : 0;
      const ip = conversionAlimenticia > 0 ? (huevosTotales / conversionAlimenticia) / 10 : 0;
      const vpi = huevosTotalesAcumulados > 0 ? huevosTotales / huevosTotalesAcumulados : 0;
      const porcentajeIncubables = huevosTotales > 0 ? (huevosIncubables / huevosTotales) * 100 : 0;

      mortalidadAcumulada += mortalidadTotalPorcentaje;
      huevosTotalesAcumulados += huevosTotales;
      huevosIncubablesAcumulados += huevosIncubables;

      const indicador: IndicadorSemanal = {
        semana,
        fechaInicio: this.obtenerFechaInicioSemana(semana),
        avesInicioSemana: avesAcumuladas,
        avesFinSemana: avesFin,
        consumoReal,
        consumoTabla,
        conversionAlimenticia,
        huevosTotales,
        huevosIncubables,
        mortalidadHembras,
        mortalidadMachos,
        mortalidadTotal: mortalidadTotalPorcentaje,
        eficiencia,
        ip,
        vpi,
        mortalidadAcum: mortalidadAcumulada,
        huevosTotalesAcum: huevosTotalesAcumulados,
        huevosIncubablesAcum: huevosIncubablesAcumulados,
        porcentajeIncubables,
        pesoHuevoPromedio
      };

      avesAcumuladas = avesFin;

      return indicador;
    }).filter((ind): ind is IndicadorSemanal => ind !== null);
  }

  private agruparPorSemana(registros: SeguimientoItemDto[]): Map<number, SeguimientoItemDto[]> {
    const grupos = new Map<number, SeguimientoItemDto[]>();

    registros.forEach(registro => {
      const semana = this.calcularSemana(registro.fechaRegistro);
      // Ajustar a semana mínima de producción (25)
      const semanaProduccion = Math.max(this.SEMANA_INICIO_PRODUCCION, semana);

      if (!grupos.has(semanaProduccion)) {
        grupos.set(semanaProduccion, []);
      }
      grupos.get(semanaProduccion)!.push(registro);
    });

    grupos.forEach((registros, semana) => {
      registros.sort((a, b) => new Date(a.fechaRegistro).getTime() - new Date(b.fechaRegistro).getTime());
    });

    return grupos;
  }

  private calcularSemana(fechaRegistro: string | Date): number {
    if (!this.selectedLote?.fechaEncaset) return this.SEMANA_INICIO_PRODUCCION;

    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaEncaset.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    // En producción, las semanas comienzan desde la 25
    const semanaCalculada = Math.ceil(diffDays / 7);
    return Math.max(this.SEMANA_INICIO_PRODUCCION, Math.min(semanaCalculada, this.SEMANA_MAX_PRODUCCION));
  }

  private obtenerFechaInicioSemana(semana: number): string {
    if (!this.selectedLote?.fechaEncaset) return '';

    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const diasASumar = (semana - 1) * 7;
    const fechaInicio = new Date(fechaEncaset.getTime() + (diasASumar * 24 * 60 * 60 * 1000));

    return fechaInicio.toISOString().split('T')[0];
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
          label: 'Consumo Real (kg)',
          data: this.indicadoresSemanales.map(ind => ind.consumoReal || 0),
          backgroundColor: 'rgba(211, 47, 47, 0.7)',
          borderColor: 'rgba(211, 47, 47, 1)',
          borderWidth: 2
        },
        {
          label: 'Consumo Tabla (kg)',
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

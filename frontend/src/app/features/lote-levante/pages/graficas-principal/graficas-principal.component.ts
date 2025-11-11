import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';

interface PuntoGrafica {
  semana: number;
  fecha: string;
  valor: number;
  etiqueta: string;
}

interface SerieGrafica {
  nombre: string;
  datos: PuntoGrafica[];
  color: string;
  tipo: 'linea' | 'barra' | 'area';
}

@Component({
  selector: 'app-graficas-principal',
  standalone: true,
  imports: [CommonModule, FormsModule, NgChartsModule],
  templateUrl: './graficas-principal.component.html',
  styleUrls: ['./graficas-principal.component.scss']
})
export class GraficasPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() loading: boolean = false;

  // Datos para gráficas
  seriesGraficas: SerieGrafica[] = [];
  indicadoresSemanales: any[] = [];

  // ========== SELECTORES DE GRÁFICAS ==========
  tipoGraficaSeleccionada: 'mortalidad' | 'consumo' | 'peso' | 'conversion' | 'seleccion' | 'aves' | 'comparacion' = 'mortalidad';
  tiposGraficaSeleccionados: string[] = ['mortalidad']; // Lista de tipos seleccionados para gráfica combinada
  tipoVisualizacion: 'linea' | 'barra' | 'torta' = 'barra';
  mostrarGraficaCombinada: boolean = false; // Activar gráfica combinada cuando hay múltiples tipos seleccionados

  // ========== SELECTOR COMPARATIVO ==========
  semanasDisponibles: number[] = [];
  semanaComparacion1: number | null = null;
  semanaComparacion2: number | null = null;
  mostrarComparativo: boolean = false;

  // ========== SELECTOR MÚLTIPLE DE MÉTRICAS ==========
  metricasDisponibles = [
    { id: 'mortalidad', nombre: 'Mortalidad (%)', icono: '💀', color: 'rgba(245, 124, 0, 1)' },
    { id: 'consumoReal', nombre: 'Consumo Real (g)', icono: '🍽️', color: 'rgba(211, 47, 47, 1)' },
    { id: 'consumoTabla', nombre: 'Consumo Tabla (g)', icono: '📊', color: 'rgba(25, 118, 210, 1)' },
    { id: 'peso', nombre: 'Peso Promedio (g)', icono: '⚖️', color: 'rgba(56, 142, 60, 1)' },
    { id: 'conversion', nombre: 'Conversión Alimenticia', icono: '🔄', color: 'rgba(33, 150, 243, 1)' },
    { id: 'seleccion', nombre: 'Selección (%)', icono: '📋', color: 'rgba(156, 39, 176, 1)' },
    { id: 'aves', nombre: 'Aves Vivas', icono: '🐔', color: 'rgba(123, 31, 162, 1)' }
  ];
  metricasSeleccionadas: string[] = [];
  mostrarComparacionPersonalizada: boolean = false;

  // Gráfica combinada personalizada
  comparacionPersonalizadaChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  comparacionPersonalizadaChartOptions: ChartConfiguration['options'] = {};

  // Gráfica combinada de tipos de indicadores
  tiposCombinadosChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  tiposCombinadosChartOptions: ChartConfiguration['options'] = {};

  // ========== DATOS DE GRÁFICAS CHART.JS ==========
  // Gráfica de Mortalidad
  mortalidadChartData: ChartData<'bar' | 'line' | 'pie'> = { labels: [], datasets: [] };
  mortalidadChartOptions: ChartConfiguration['options'] = {};

  // Gráfica de Consumo
  consumoChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  consumoChartOptions: ChartConfiguration['options'] = {};

  // Gráfica de Peso
  pesoChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  pesoChartOptions: ChartConfiguration['options'] = {};

  // Gráfica de Conversión
  conversionChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  conversionChartOptions: ChartConfiguration['options'] = {};

  // Gráfica de Selección
  seleccionChartData: ChartData<'bar' | 'line'> = { labels: [], datasets: [] };
  seleccionChartOptions: ChartConfiguration['options'] = {};

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
    this.mortalidadChartOptions = { ...baseOptions };
    this.consumoChartOptions = { ...baseOptions };
    this.pesoChartOptions = { ...baseOptions };
    this.conversionChartOptions = { ...baseOptions };
    this.seleccionChartOptions = { ...baseOptions };
    this.avesChartOptions = { ...baseOptions };
    this.comparativoChartOptions = { ...baseOptions };
    this.comparacionPersonalizadaChartOptions = { ...baseOptions };

    // Opciones especiales para gráfica combinada de tipos
    this.tiposCombinadosChartOptions = {
      ...baseOptions,
      interaction: {
        mode: 'index',
        intersect: false
      },
      plugins: {
        ...baseOptions.plugins,
        tooltip: {
          ...baseOptions.plugins?.tooltip,
          mode: 'index',
          intersect: false
        },
        legend: {
          ...baseOptions.plugins?.legend,
          display: true // Asegurar que la leyenda se muestre
        }
      },
      scales: {
        ...baseOptions.scales,
        y: {
          ...(baseOptions.scales?.['y'] || {}),
          beginAtZero: true
        } as any,
        x: {
          ...(baseOptions.scales?.['x'] || {})
        } as any
      },
      // Configuración para barras agrupadas (Chart.js maneja esto automáticamente)
      datasets: {
        bar: {
          barPercentage: 0.8,
          categoryPercentage: 0.9
        } as any
      }
    };

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
      this.seriesGraficas = [];
      this.indicadoresSemanales = [];
      this.semanasDisponibles = [];
      return;
    }

    // Calcular indicadores semanales (reutilizar lógica del componente de indicadores)
    this.indicadoresSemanales = this.calcularIndicadoresSemanales();

    // Actualizar semanas disponibles
    this.semanasDisponibles = this.indicadoresSemanales.map(ind => ind.semana).sort((a, b) => a - b);

    // Si no hay semanas seleccionadas para comparación, seleccionar las primeras dos si existen
    if (!this.semanaComparacion1 && this.semanasDisponibles.length > 0) {
      this.semanaComparacion1 = this.semanasDisponibles[0];
    }
    if (!this.semanaComparacion2 && this.semanasDisponibles.length > 1) {
      this.semanaComparacion2 = this.semanasDisponibles[1];
    }

    // Preparar series de datos para gráficas (para compatibilidad)
    this.seriesGraficas = this.prepararSeriesGraficas();

    // Preparar datos de Chart.js
    this.prepararChartData();
  }

  // ========== PREPARACIÓN DE DATOS PARA CHART.JS ==========
  private prepararChartData(): void {
    if (this.indicadoresSemanales.length === 0) return;

    const labels = this.indicadoresSemanales.map(ind => `Semana ${ind.semana}`);

    // Gráfica de Mortalidad
    this.mortalidadChartData = {
      labels,
      datasets: [{
        label: 'Mortalidad (%)',
        data: this.indicadoresSemanales.map(ind => ind.mortalidadSem || 0),
        backgroundColor: 'rgba(245, 124, 0, 0.7)',
        borderColor: 'rgba(245, 124, 0, 1)',
        borderWidth: 2
      }]
    };

    // Gráfica de Consumo
    this.consumoChartData = {
      labels,
      datasets: [
        {
          label: 'Consumo Real (g)',
          data: this.indicadoresSemanales.map(ind => ind.consumoReal || 0),
          backgroundColor: 'rgba(211, 47, 47, 0.7)',
          borderColor: 'rgba(211, 47, 47, 1)',
          borderWidth: 2
        },
        {
          label: 'Consumo Tabla (g)',
          data: this.indicadoresSemanales.map(ind => ind.consumoTabla || 0),
          backgroundColor: 'rgba(25, 118, 210, 0.7)',
          borderColor: 'rgba(25, 118, 210, 1)',
          borderWidth: 2
        }
      ]
    };

    // Gráfica de Peso
    this.pesoChartData = {
      labels,
      datasets: [{
        label: 'Peso Promedio (g)',
        data: this.indicadoresSemanales.map(ind => ind.pesoCierre || 0),
        backgroundColor: 'rgba(56, 142, 60, 0.7)',
        borderColor: 'rgba(56, 142, 60, 1)',
        borderWidth: 2,
        fill: false,
        tension: 0.4
      }]
    };

    // Gráfica de Conversión
    this.conversionChartData = {
      labels,
      datasets: [{
        label: 'Conversión Alimenticia',
        data: this.indicadoresSemanales.map(ind => ind.conversionAlimenticia || 0),
        backgroundColor: 'rgba(33, 150, 243, 0.7)',
        borderColor: 'rgba(33, 150, 243, 1)',
        borderWidth: 2,
        fill: false,
        tension: 0.4
      }]
    };

    // Gráfica de Selección
    this.seleccionChartData = {
      labels,
      datasets: [{
        label: 'Selección (%)',
        data: this.indicadoresSemanales.map(ind => ind.seleccionSem || 0),
        backgroundColor: 'rgba(156, 39, 176, 0.7)',
        borderColor: 'rgba(156, 39, 176, 1)',
        borderWidth: 2
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

    // Gráfica de Torta (distribución de mortalidad vs selección en última semana)
    const ultimaSemana = this.indicadoresSemanales[this.indicadoresSemanales.length - 1];
    if (ultimaSemana) {
      const total = (ultimaSemana.mortalidadSem || 0) + (ultimaSemana.seleccionSem || 0);
      if (total > 0) {
        this.tortaChartData = {
          labels: ['Mortalidad', 'Selección'],
          datasets: [{
            data: [
              ultimaSemana.mortalidadSem || 0,
              ultimaSemana.seleccionSem || 0
            ],
            backgroundColor: [
              'rgba(245, 124, 0, 0.8)',
              'rgba(156, 39, 176, 0.8)'
            ],
            borderColor: [
              'rgba(245, 124, 0, 1)',
              'rgba(156, 39, 176, 1)'
            ],
            borderWidth: 2
          }]
        };
      }
    }

    // Actualizar gráfica comparativa si está activa
    if (this.mostrarComparativo) {
      this.actualizarGraficaComparativa();
    }

    // Actualizar gráfica de comparación personalizada si está activa
    if (this.mostrarComparacionPersonalizada && this.metricasSeleccionadas.length > 0) {
      this.actualizarGraficaComparacionPersonalizada();
    }

    // Actualizar gráfica combinada de tipos de indicadores
    if (this.tiposGraficaSeleccionados.length > 0) {
      this.actualizarGraficaTiposCombinados();
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

    // Preparar datos comparativos según el tipo seleccionado
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
      { nombre: 'Mortalidad (%)', semana1: semana1.mortalidadSem || 0, semana2: semana2.mortalidadSem || 0 },
      { nombre: 'Selección (%)', semana1: semana1.seleccionSem || 0, semana2: semana2.seleccionSem || 0 },
      { nombre: 'Consumo Real (g)', semana1: semana1.consumoReal || 0, semana2: semana2.consumoReal || 0 },
      { nombre: 'Peso (g)', semana1: semana1.pesoCierre || 0, semana2: semana2.pesoCierre || 0 },
      { nombre: 'Conversión', semana1: semana1.conversionAlimenticia || 0, semana2: semana2.conversionAlimenticia || 0 },
      { nombre: 'Aves Vivas', semana1: semana1.avesFinSemana || 0, semana2: semana2.avesFinSemana || 0 }
    ];
  }

  // ========== MÉTODOS PÚBLICOS PARA CAMBIOS ==========
  onTipoGraficaChange(): void {
    // Si solo hay un tipo seleccionado, actualizar selección única
    if (this.tiposGraficaSeleccionados.length === 1) {
      this.tipoGraficaSeleccionada = this.tiposGraficaSeleccionados[0] as any;
      this.mostrarGraficaCombinada = false;
    } else if (this.tiposGraficaSeleccionados.length > 1) {
      // Si hay múltiples tipos, mostrar gráfica combinada
      this.mostrarGraficaCombinada = true;
      this.actualizarGraficaTiposCombinados();
    }
  }

  toggleTipoGrafica(tipo: string): void {
    const index = this.tiposGraficaSeleccionados.indexOf(tipo);
    if (index > -1) {
      // Si es el último tipo, mantener al menos uno
      if (this.tiposGraficaSeleccionados.length === 1) {
        return;
      }
      this.tiposGraficaSeleccionados.splice(index, 1);
    } else {
      this.tiposGraficaSeleccionados.push(tipo);
    }

    // Si solo queda uno, actualizar el selector único
    if (this.tiposGraficaSeleccionados.length === 1) {
      this.tipoGraficaSeleccionada = this.tiposGraficaSeleccionados[0] as any;
      this.mostrarGraficaCombinada = false;
    } else {
      this.mostrarGraficaCombinada = true;
      this.actualizarGraficaTiposCombinados();
    }
  }

  isTipoGraficaSeleccionado(tipo: string): boolean {
    return this.tiposGraficaSeleccionados.includes(tipo);
  }

  onVisualizacionChange(): void {
    // Cuando cambia la visualización (línea/barra/torta)
    // Los datos ya están preparados, solo cambia el canvas mostrado
    // Si hay comparación personalizada activa, actualizar la gráfica
    if (this.mostrarComparacionPersonalizada && this.metricasSeleccionadas.length > 0) {
      this.actualizarGraficaComparacionPersonalizada();
    }
    // Si hay gráfica combinada de tipos, actualizar (esto regenera los datasets con las nuevas configuraciones)
    if (this.mostrarGraficaCombinada && this.tiposGraficaSeleccionados.length > 1) {
      this.actualizarGraficaTiposCombinados();
    }
  }

  onSemanaComparacionChange(): void {
    if (this.semanaComparacion1 && this.semanaComparacion2) {
      this.actualizarGraficaComparativa();
    }
  }

  toggleComparativo(): void {
    this.mostrarComparativo = !this.mostrarComparativo;
    if (this.mostrarComparativo) {
      // Desactivar comparación personalizada si se activa comparación de semanas
      this.mostrarComparacionPersonalizada = false;
      // No desactivar gráfica combinada de tipos, solo ocultarla temporalmente
      this.actualizarGraficaComparativa();
    } else {
      // Al desactivar comparación de semanas, verificar si hay tipos combinados
      if (this.tiposGraficaSeleccionados.length > 1) {
        this.mostrarGraficaCombinada = true;
        this.actualizarGraficaTiposCombinados();
      }
    }
  }

  getTituloGrafica(): string {
    if (this.mostrarComparacionPersonalizada && this.metricasSeleccionadas.length > 0) {
      return this.getTituloComparacionPersonalizada();
    }

    // Si hay múltiples tipos seleccionados, mostrar título combinado
    if (this.mostrarGraficaCombinada && this.tiposGraficaSeleccionados.length > 1) {
      const iconos: { [key: string]: string } = {
        'mortalidad': '💀',
        'consumo': '🍽️',
        'consumoTabla': '📊',
        'peso': '⚖️',
        'conversion': '🔄',
        'seleccion': '📋',
        'retiro': '📉',
        'uniformidad': '📐',
        'cv': '📈',
        'difConsumo': '🔀',
        'incrConsumo': '📊',
        'aves': '🐔'
      };
      const iconosSeleccionados = this.tiposGraficaSeleccionados.map(t => iconos[t] || '').filter(Boolean);
      return `📊 Comparación: ${iconosSeleccionados.join(' vs ')}`;
    }

    const titulos: { [key: string]: string } = {
      'mortalidad': '💀 Mortalidad por Semana',
      'consumo': '🍽️ Consumo de Alimento',
      'peso': '⚖️ Evolución de Peso',
      'conversion': '🔄 Conversión Alimenticia',
      'seleccion': '📋 Selección por Semana',
      'aves': '🐔 Aves Vivas por Semana',
      'comparacion': '📊 Comparación Personalizada'
    };
    return titulos[this.tipoGraficaSeleccionada] || '📊 Gráfica';
  }

  private calcularIndicadoresSemanales(): any[] {
    // Agrupar registros por semana
    const registrosPorSemana = this.agruparPorSemana(this.seguimientos);

    // Calcular indicadores para cada semana
    return this.calcularIndicadoresSemanalesFromGrupos(registrosPorSemana);
  }

  private agruparPorSemana(registros: SeguimientoLoteLevanteDto[]): Map<number, SeguimientoLoteLevanteDto[]> {
    const grupos = new Map<number, SeguimientoLoteLevanteDto[]>();

    registros.forEach(registro => {
      const semana = this.calcularSemana(registro.fechaRegistro);
      if (!grupos.has(semana)) {
        grupos.set(semana, []);
      }
      grupos.get(semana)!.push(registro);
    });

    // Ordenar registros dentro de cada semana por fecha
    grupos.forEach((registros, semana) => {
      registros.sort((a, b) => new Date(a.fechaRegistro).getTime() - new Date(b.fechaRegistro).getTime());
    });

    return grupos;
  }

  private calcularSemana(fechaRegistro: string | Date): number {
    if (!this.selectedLote?.fechaEncaset) return 1;

    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaEncaset.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    return Math.max(1, Math.ceil(diffDays / 7));
  }

  private calcularIndicadoresSemanalesFromGrupos(grupos: Map<number, SeguimientoLoteLevanteDto[]>): any[] {
    const indicadores: any[] = [];
    const semanas = Array.from(grupos.keys()).sort((a, b) => a - b);

    let avesAcumuladas = this.selectedLote?.avesEncasetadas || 0;
    let mortalidadAcumulada = 0;
    let seleccionAcumulada = 0;
    let pesoAnterior = this.selectedLote?.pesoInicialH || 0;
    let consumoAnterior = 0;
    let consumoTablaAnterior = 0;

    semanas.forEach((semana, index) => {
      const registros = grupos.get(semana) || [];
      const indicador = this.calcularIndicadorSemana(semana, registros, avesAcumuladas, mortalidadAcumulada, seleccionAcumulada, pesoAnterior);

      // Calcular incrementos de consumo (comparar con semana anterior)
      if (index > 0 && consumoAnterior > 0) {
        indicador.incrConsumoReal = indicador.consumoReal - consumoAnterior;
        indicador.incrConsumoTabla = indicador.consumoTabla - consumoTablaAnterior;
      } else {
        indicador.incrConsumoReal = 0;
        indicador.incrConsumoTabla = 0;
      }

      indicadores.push(indicador);

      // Actualizar acumulados para la siguiente semana
      avesAcumuladas = indicador.avesFinSemana;
      mortalidadAcumulada += indicador.mortalidadSem;
      seleccionAcumulada += indicador.seleccionSem;
      pesoAnterior = indicador.pesoCierre;
      consumoAnterior = indicador.consumoReal;
      consumoTablaAnterior = indicador.consumoTabla;
    });

    return indicadores;
  }

  private calcularIndicadorSemana(
    semana: number,
    registros: SeguimientoLoteLevanteDto[],
    avesInicio: number,
    mortalidadAcum: number,
    seleccionAcum: number,
    pesoAnterior: number
  ): any {
    // Calcular totales de la semana
    const mortalidadTotal = registros.reduce((sum, r) => sum + (r.mortalidadHembras || 0) + (r.mortalidadMachos || 0), 0);
    const seleccionTotal = registros.reduce((sum, r) => sum + (r.selH || 0) + (r.selM || 0), 0);
    const errorSexajeTotal = registros.reduce((sum, r) => sum + (r.errorSexajeHembras || 0) + (r.errorSexajeMachos || 0), 0);
    const consumoTotal = registros.reduce((sum, r) => sum + (r.consumoKgHembras || 0) + (r.consumoKgMachos || 0), 0);

    // Aves al final de la semana
    const avesFin = avesInicio - mortalidadTotal - seleccionTotal - errorSexajeTotal;

    // Peso promedio de la semana (usar el último registro de la semana)
    const ultimoRegistro = registros[registros.length - 1];
    const pesoPromedio = ((ultimoRegistro?.pesoPromH || 0) + (ultimoRegistro?.pesoPromM || 0)) / 2;

    // Uniformidad y CV (promedio de hembras y machos)
    const uniformidadPromedio = ((ultimoRegistro?.uniformidadH || 0) + (ultimoRegistro?.uniformidadM || 0)) / 2;
    const cvPromedio = ((ultimoRegistro?.cvH || 0) + (ultimoRegistro?.cvM || 0)) / 2;

    // Consumo real en gramos por ave (convertir de kg a gramos)
    const avesPromedio = (avesInicio + avesFin) / 2;
    const consumoRealTotal = consumoTotal * 1000;
    const consumoRealPorAve = avesPromedio > 0 ? consumoRealTotal / avesPromedio : 0;

    // Consumo tabla (valor fijo por ahora, debería venir de la tabla genética)
    const consumoTabla = 157;

    // Conversión alimenticia
    const conversionAlimenticia = avesFin > 0 ? consumoRealTotal / avesFin : 0;

    // Porcentajes
    const mortalidadSem = avesInicio > 0 ? (mortalidadTotal / avesInicio) * 100 : 0;
    const seleccionSem = avesInicio > 0 ? (seleccionTotal / avesInicio) * 100 : 0;
    const errorSexajeSem = avesInicio > 0 ? (errorSexajeTotal / avesInicio) * 100 : 0;
    const retiroSem = mortalidadSem + seleccionSem + errorSexajeSem;

    // Diferencias porcentuales (vs guía)
    const difConsumoPorc = consumoTabla > 0 ? ((consumoRealPorAve - consumoTabla) / consumoTabla) * 100 : 0;
    // Nota: difPesoPorc requiere peso de guía genética (se calculará cuando esté disponible)

    // Incrementos de consumo (comparar con semana anterior si existe)
    // Esto se calculará después cuando tengamos todas las semanas

    // Eficiencia
    const eficiencia = conversionAlimenticia > 0 ? pesoPromedio / conversionAlimenticia / 10 : 0;

    // IP (Índice de Productividad)
    const ip = conversionAlimenticia > 0 ? ((pesoPromedio / conversionAlimenticia) / 10) / conversionAlimenticia : 0;

    return {
      semana,
      fechaInicio: this.obtenerFechaInicioSemana(semana),
      avesInicioSemana: avesInicio,
      avesFinSemana: avesFin,
      consumoReal: consumoRealPorAve, // Ahora es por ave en gramos
      consumoTabla,
      conversionAlimenticia,
      mortalidadSem,
      seleccionSem,
      errorSexajeSem,
      retiroSem,
      uniformidad: uniformidadPromedio,
      cv: cvPromedio,
      difConsumoPorc,
      eficiencia,
      ip,
      pesoCierre: pesoPromedio,
      pesoInicial: pesoAnterior,
      gananciaSemana: pesoPromedio - pesoAnterior
    };
  }

  private prepararSeriesGraficas(): SerieGrafica[] {
    if (this.indicadoresSemanales.length === 0) return [];

    return [
      {
        nombre: 'Consumo Real (g)',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.consumoReal,
          etiqueta: `Semana ${ind.semana}: ${ind.consumoReal.toFixed(0)}g`
        })),
        color: '#d32f2f',
        tipo: 'barra'
      },
      {
        nombre: 'Consumo Tabla (g)',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.consumoTabla,
          etiqueta: `Semana ${ind.semana}: ${ind.consumoTabla.toFixed(0)}g`
        })),
        color: '#1976d2',
        tipo: 'barra'
      },
      {
        nombre: 'Peso Promedio (g)',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.pesoCierre,
          etiqueta: `Semana ${ind.semana}: ${ind.pesoCierre.toFixed(2)}g`
        })),
        color: '#388e3c',
        tipo: 'linea'
      },
      {
        nombre: 'Mortalidad (%)',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.mortalidadSem,
          etiqueta: `Semana ${ind.semana}: ${ind.mortalidadSem.toFixed(2)}%`
        })),
        color: '#f57c00',
        tipo: 'barra'
      },
      {
        nombre: 'Selección (%)',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.seleccionSem,
          etiqueta: `Semana ${ind.semana}: ${ind.seleccionSem.toFixed(2)}%`
        })),
        color: '#9c27b0',
        tipo: 'barra'
      },
      {
        nombre: 'Conversión Alimenticia',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.conversionAlimenticia,
          etiqueta: `Semana ${ind.semana}: ${ind.conversionAlimenticia.toFixed(2)}`
        })),
        color: '#2196f3',
        tipo: 'linea'
      },
      {
        nombre: 'Eficiencia',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.eficiencia,
          etiqueta: `Semana ${ind.semana}: ${ind.eficiencia.toFixed(2)}`
        })),
        color: '#4caf50',
        tipo: 'linea'
      },
      {
        nombre: 'Aves Vivas',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.avesFinSemana,
          etiqueta: `Semana ${ind.semana}: ${ind.avesFinSemana} aves`
        })),
        color: '#7b1fa2',
        tipo: 'barra'
      }
    ];
  }

  // ================== HELPERS DE FECHA ==================
  private obtenerFechaInicioSemana(semana: number): string {
    if (!this.selectedLote?.fechaEncaset) return '';

    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const diasASumar = (semana - 1) * 7;
    const fechaInicio = new Date(fechaEncaset.getTime() + (diasASumar * 24 * 60 * 60 * 1000));

    return fechaInicio.toISOString().split('T')[0];
  }

  // ================== MÉTODOS PÚBLICOS ==================
  getSeriesDisponibles(): string[] {
    return this.seriesGraficas.map(serie => serie.nombre);
  }

  getDatosSerie(nombreSerie: string): PuntoGrafica[] {
    const serie = this.seriesGraficas.find(s => s.nombre === nombreSerie);
    return serie ? serie.datos : [];
  }

  getColorSerie(nombreSerie: string): string {
    const serie = this.seriesGraficas.find(s => s.nombre === nombreSerie);
    return serie ? serie.color : '#000000';
  }

  getTipoSerie(nombreSerie: string): string {
    const serie = this.seriesGraficas.find(s => s.nombre === nombreSerie);
    return serie ? serie.tipo : 'linea';
  }

  // ================== FORMATO ==================
  formatNumber = (value: number, decimals: number = 2): string => {
    return value.toFixed(decimals);
  };

  formatDate = (date: string): string => {
    return new Date(date).toLocaleDateString('es-ES');
  };

  // ================== MÉTODOS DE CÁLCULO ==================
  calcularPromedio(datos: PuntoGrafica[]): number {
    if (datos.length === 0) return 0;
    const suma = datos.reduce((acc, punto) => acc + punto.valor, 0);
    return suma / datos.length;
  }

  calcularTotal(datos: PuntoGrafica[]): number {
    return datos.reduce((acc, punto) => acc + punto.valor, 0);
  }

  calcularGananciaTotal(): number {
    if (this.indicadoresSemanales.length === 0) return 0;
    const primerPeso = this.indicadoresSemanales[0].pesoInicial;
    const ultimoPeso = this.indicadoresSemanales[this.indicadoresSemanales.length - 1].pesoCierre;
    return ultimoPeso - primerPeso;
  }

  getMejorSemana(serieNombre: string): string {
    const datos = this.getDatosSerie(serieNombre);
    if (datos.length === 0) return 'N/A';

    const mejorPunto = datos.reduce((mejor, actual) =>
      actual.valor < mejor.valor ? actual : mejor
    );

    return `Semana ${mejorPunto.semana} (${mejorPunto.valor.toFixed(2)})`;
  }

  calcularPromedioIndicadores(propiedad: string): number {
    if (this.indicadoresSemanales.length === 0) return 0;
    const suma = this.indicadoresSemanales.reduce((acc, ind) => acc + (ind[propiedad] || 0), 0);
    return suma / this.indicadoresSemanales.length;
  }

  getMejorSemanaConversion(): string {
    if (this.indicadoresSemanales.length === 0) return 'N/A';

    const mejorSemana = this.indicadoresSemanales.reduce((mejor, actual) =>
      actual.conversionAlimenticia < mejor.conversionAlimenticia ? actual : mejor
    );

    return `Semana ${mejorSemana.semana} (${mejorSemana.conversionAlimenticia.toFixed(2)})`;
  }

  // ========== MÉTODOS PARA COMPARACIÓN PERSONALIZADA ==========
  toggleComparacionPersonalizada(): void {
    this.mostrarComparacionPersonalizada = !this.mostrarComparacionPersonalizada;
    if (this.mostrarComparacionPersonalizada) {
      // Desactivar comparación de semanas si se activa comparación personalizada
      this.mostrarComparativo = false;
      // No desactivar gráfica combinada de tipos, solo ocultarla temporalmente

      // Por defecto, seleccionar Consumo Real y Consumo Tabla si no hay nada seleccionado
      if (this.metricasSeleccionadas.length === 0) {
        this.metricasSeleccionadas = ['consumoReal', 'consumoTabla'];
      }

      if (this.metricasSeleccionadas.length > 0) {
        this.actualizarGraficaComparacionPersonalizada();
      }
    } else {
      // Al desactivar comparación personalizada, verificar si hay tipos combinados
      if (this.tiposGraficaSeleccionados.length > 1) {
        this.mostrarGraficaCombinada = true;
        this.actualizarGraficaTiposCombinados();
      }
    }
  }

  toggleMetrica(metricaId: string): void {
    const index = this.metricasSeleccionadas.indexOf(metricaId);
    if (index > -1) {
      this.metricasSeleccionadas.splice(index, 1);
    } else {
      this.metricasSeleccionadas.push(metricaId);
    }

    if (this.mostrarComparacionPersonalizada && this.metricasSeleccionadas.length > 0) {
      this.actualizarGraficaComparacionPersonalizada();
    }
  }

  isMetricaSeleccionada(metricaId: string): boolean {
    return this.metricasSeleccionadas.includes(metricaId);
  }

  actualizarGraficaComparacionPersonalizada(): void {
    if (this.metricasSeleccionadas.length === 0 || this.indicadoresSemanales.length === 0) {
      this.comparacionPersonalizadaChartData = { labels: [], datasets: [] };
      return;
    }

    const labels = this.indicadoresSemanales.map(ind => `Semana ${ind.semana}`);
    const datasets = this.metricasSeleccionadas.map(metricaId => {
      const metrica = this.metricasDisponibles.find(m => m.id === metricaId);
      if (!metrica) return null;

      let data: number[] = [];
      let label = metrica.nombre;

      switch (metricaId) {
        case 'mortalidad':
          data = this.indicadoresSemanales.map(ind => ind.mortalidadSem || 0);
          break;
        case 'consumoReal':
          data = this.indicadoresSemanales.map(ind => ind.consumoReal || 0);
          break;
        case 'consumoTabla':
          data = this.indicadoresSemanales.map(ind => ind.consumoTabla || 0);
          break;
        case 'peso':
          data = this.indicadoresSemanales.map(ind => ind.pesoCierre || 0);
          break;
        case 'conversion':
          data = this.indicadoresSemanales.map(ind => ind.conversionAlimenticia || 0);
          break;
        case 'seleccion':
          data = this.indicadoresSemanales.map(ind => ind.seleccionSem || 0);
          break;
        case 'aves':
          data = this.indicadoresSemanales.map(ind => ind.avesFinSemana || 0);
          break;
        default:
          return null;
      }

      return {
        label,
        data,
        backgroundColor: metrica.color.replace('1)', '0.7)'),
        borderColor: metrica.color,
        borderWidth: 2,
        fill: false,
        tension: 0.4
      };
    }).filter(d => d !== null) as any[];

    this.comparacionPersonalizadaChartData = {
      labels,
      datasets
    };
  }

  getTituloComparacionPersonalizada(): string {
    if (this.metricasSeleccionadas.length === 0) {
      return '📊 Comparación Personalizada - Selecciona métricas';
    }
    const nombres = this.metricasSeleccionadas.map(id => {
      const metrica = this.metricasDisponibles.find(m => m.id === id);
      return metrica?.icono || '';
    });
    return `📊 Comparación: ${nombres.join(' vs ')}`;
  }

  getMetricaInfo(metricaId: string): { icono: string; nombre: string } | null {
    const metrica = this.metricasDisponibles.find(m => m.id === metricaId);
    return metrica ? { icono: metrica.icono, nombre: metrica.nombre } : null;
  }

  // ========== MÉTODOS PARA GRÁFICA COMBINADA DE TIPOS ==========
  actualizarGraficaTiposCombinados(): void {
    if (this.tiposGraficaSeleccionados.length === 0 || this.indicadoresSemanales.length === 0) {
      this.tiposCombinadosChartData = { labels: [], datasets: [] };
      return;
    }

    const labels = this.indicadoresSemanales.map(ind => `Semana ${ind.semana}`);
    const datasets: any[] = [];

    // Actualizar opciones según el tipo de visualización
    const isBarChart = this.tipoVisualizacion === 'barra';

    // Colores para cada tipo de gráfica
    const coloresPorTipo: { [key: string]: string } = {
      'mortalidad': 'rgba(245, 124, 0, 1)',
      'consumo': 'rgba(211, 47, 47, 1)',
      'consumoTabla': 'rgba(25, 118, 210, 1)',
      'peso': 'rgba(56, 142, 60, 1)',
      'conversion': 'rgba(33, 150, 243, 1)',
      'seleccion': 'rgba(156, 39, 176, 1)',
      'retiro': 'rgba(244, 67, 54, 1)',
      'uniformidad': 'rgba(156, 39, 176, 1)',
      'cv': 'rgba(63, 81, 181, 1)',
      'difConsumo': 'rgba(233, 30, 99, 1)',
      'incrConsumo': 'rgba(255, 152, 0, 1)',
      'aves': 'rgba(123, 31, 162, 1)'
    };

    const nombresPorTipo: { [key: string]: string } = {
      'mortalidad': 'Mortalidad (%)',
      'consumo': 'Consumo Real (g)',
      'consumoTabla': 'Consumo Tabla (g)',
      'peso': 'Peso Promedio (g)',
      'conversion': 'Conversión Alimenticia',
      'seleccion': 'Selección (%)',
      'retiro': 'Retiro (%)',
      'uniformidad': 'Uniformidad (%)',
      'cv': 'CV (%)',
      'difConsumo': 'Dif. Consumo (%)',
      'incrConsumo': 'Incr. Consumo Real',
      'aves': 'Aves Vivas'
    };

    this.tiposGraficaSeleccionados.forEach(tipo => {
      let data: number[] = [];

      switch (tipo) {
        case 'mortalidad':
          data = this.indicadoresSemanales.map(ind => ind.mortalidadSem || 0);
          break;
        case 'consumo':
          // Para consumo, mostrar consumo real
          data = this.indicadoresSemanales.map(ind => ind.consumoReal || 0);
          break;
        case 'consumoTabla':
          data = this.indicadoresSemanales.map(ind => ind.consumoTabla || 0);
          break;
        case 'peso':
          data = this.indicadoresSemanales.map(ind => ind.pesoCierre || 0);
          break;
        case 'conversion':
          data = this.indicadoresSemanales.map(ind => ind.conversionAlimenticia || 0);
          break;
        case 'seleccion':
          data = this.indicadoresSemanales.map(ind => ind.seleccionSem || 0);
          break;
        case 'retiro':
          data = this.indicadoresSemanales.map(ind => ind.retiroSem || 0);
          break;
        case 'uniformidad':
          data = this.indicadoresSemanales.map(ind => ind.uniformidad || 0);
          break;
        case 'cv':
          data = this.indicadoresSemanales.map(ind => ind.cv || 0);
          break;
        case 'difConsumo':
          data = this.indicadoresSemanales.map(ind => ind.difConsumoPorc || 0);
          break;
        case 'incrConsumo':
          data = this.indicadoresSemanales.map(ind => ind.incrConsumoReal || 0);
          break;
        case 'aves':
          data = this.indicadoresSemanales.map(ind => ind.avesFinSemana || 0);
          break;
        default:
          return;
      }

      const color = coloresPorTipo[tipo] || 'rgba(0, 0, 0, 1)';

      // Determinar si debe usar barras o líneas según el tipo de visualización
      const isBarChart = this.tipoVisualizacion === 'barra';

      const dataset: any = {
        label: nombresPorTipo[tipo] || tipo,
        data,
        backgroundColor: isBarChart ? color.replace('1)', '0.7)') : color.replace('1)', '0.1)'),
        borderColor: color,
        borderWidth: 2,
        fill: !isBarChart, // Llenar solo para líneas
        tension: isBarChart ? 0 : 0.4, // Sin curvatura para barras
        yAxisID: 'y', // Asegurar que todos usen el mismo eje Y
        order: datasets.length // Mantener el orden
      };

      datasets.push(dataset);
    });

    this.tiposCombinadosChartData = {
      labels,
      datasets
    };

    // Forzar actualización del componente
    if (this.tiposCombinadosChartData.datasets.length > 0) {
      // Log para debugging
      console.log('Gráfica combinada actualizada:', {
        tipos: this.tiposGraficaSeleccionados,
        datasetsCount: this.tiposCombinadosChartData.datasets.length,
        datasets: this.tiposCombinadosChartData.datasets.map(d => ({ label: d.label, dataLength: d.data.length }))
      });
    }
  }
}

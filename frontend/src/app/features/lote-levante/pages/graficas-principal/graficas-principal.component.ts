import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';

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
  /** LoteDto (aves-engorde) o LotePosturaLevanteDto (seguimiento levante). */
  @Input() selectedLote: LoteDto | LotePosturaLevanteDto | null = null;
  @Input() loading: boolean = false;

  // Datos para gráficas
  seriesGraficas: SerieGrafica[] = [];
  indicadoresSemanales: any[] = [];

  // ========== SELECTORES DE GRÁFICAS ==========
  tipoGraficaSeleccionada: 'mortalidad' | 'consumo' | 'peso' | 'conversion' | 'seleccion' | 'aves' | 'comparacion' = 'mortalidad';
  tiposGraficaSeleccionados: string[] = ['mortalidad']; // Lista de tipos seleccionados para gráfica combinada
  tipoVisualizacion: 'linea' | 'barra' | 'torta' = 'barra';
  mostrarGraficaCombinada: boolean = false; // Activar gráfica combinada cuando hay múltiples tipos seleccionados

  // ========== FILTRO POR RANGO ==========
  modoFiltro: 'todos' | 'fechas' | 'edad' = 'todos';
  fechaDesde: string = '';
  fechaHasta: string = '';
  semanaDesde: number | null = null;
  semanaHasta: number | null = null;

  /** Indicadores filtrados según modoFiltro (fechas o edad). Usados en todas las gráficas. */
  get indicadoresFiltrados(): any[] {
    if (!this.indicadoresSemanales.length) return [];
    if (this.modoFiltro === 'todos') return this.indicadoresSemanales;
    if (this.modoFiltro === 'fechas') {
      const d = this.fechaDesde && this.fechaHasta;
      if (!d) return this.indicadoresSemanales;
      return this.indicadoresSemanales.filter((ind: any) => {
        const f = ind.fechaInicio || '';
        return f >= this.fechaDesde && f <= this.fechaHasta;
      });
    }
    if (this.modoFiltro === 'edad' && this.semanaDesde != null && this.semanaHasta != null) {
      return this.indicadoresSemanales.filter(
        (ind: any) => ind.semana >= this.semanaDesde! && ind.semana <= this.semanaHasta!
      );
    }
    return this.indicadoresSemanales;
  }

  // ========== SELECTOR COMPARATIVO ==========
  /** Semanas disponibles según indicadores filtrados (para selector de comparación). */
  get semanasDisponibles(): number[] {
    return [...new Set(this.indicadoresFiltrados.map((ind: any) => ind.semana))].sort((a, b) => a - b);
  }
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
    this.comparativoChartOptions = {
      ...baseOptions,
      plugins: {
        ...baseOptions.plugins,
        tooltip: {
          ...baseOptions.plugins?.tooltip,
          callbacks: {
            label: (context: any) => {
              const label = context.dataset.label || '';
              const value = context.parsed.y;
              const metrica = context.label;
              return `${label}: ${typeof value === 'number' ? value.toFixed(2) : value} ${metrica.includes('%') ? '' : ''}`;
            }
          }
        }
      },
      datasets: {
        bar: {
          barPercentage: 0.75,
          categoryPercentage: 0.8
        } as any
      }
    };
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
      return;
    }

    // Calcular indicadores semanales (reutilizar lógica del componente de indicadores)
    this.indicadoresSemanales = this.calcularIndicadoresSemanales();

    // Inicializar rangos de filtro con el total de datos disponibles
    if (this.indicadoresSemanales.length > 0) {
      const fechas = this.indicadoresSemanales.map((ind: any) => ind.fechaInicio || '').filter(Boolean);
      if (fechas.length > 0) {
        this.fechaDesde = fechas.reduce((a, b) => (a <= b ? a : b));
        this.fechaHasta = fechas.reduce((a, b) => (a >= b ? a : b));
      }
      const semanas = this.indicadoresSemanales.map((ind: any) => ind.semana);
      this.semanaDesde = Math.min(...semanas);
      this.semanaHasta = Math.max(...semanas);
    }

    // Validar y asignar semanas para comparación
    const semanas = this.semanasDisponibles;
    if (semanas.length > 0) {
      if (!this.semanaComparacion1 || !semanas.includes(this.semanaComparacion1)) {
        this.semanaComparacion1 = semanas[0];
      }
      if (!this.semanaComparacion2 || !semanas.includes(this.semanaComparacion2) || this.semanaComparacion2 === this.semanaComparacion1) {
        this.semanaComparacion2 = semanas.length > 1 ? semanas[1] : semanas[0];
      }
    }

    // Preparar series de datos para gráficas (para compatibilidad)
    this.seriesGraficas = this.prepararSeriesGraficas();

    // Preparar datos de Chart.js
    this.prepararChartData();
  }

  /** Aplicar filtro y refrescar gráficas. */
  aplicarFiltro(): void {
    this.prepararChartData();
  }

  /** Limpiar filtro y mostrar todos los datos. */
  limpiarFiltro(): void {
    this.modoFiltro = 'todos';
    this.prepararChartData();
  }

  // ========== PREPARACIÓN DE DATOS PARA CHART.JS ==========
  private prepararChartData(): void {
    const ind = this.indicadoresFiltrados;
    if (ind.length === 0) return;

    const labels = ind.map((x: any) => `Semana ${x.semana}`);

    // Gráfica de Mortalidad
    this.mortalidadChartData = {
      labels,
      datasets: [{
        label: 'Mortalidad (%)',
        data: ind.map((x: any) => x.mortalidadSem || 0),
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
          data: ind.map((x: any) => x.consumoReal || 0),
          backgroundColor: 'rgba(211, 47, 47, 0.7)',
          borderColor: 'rgba(211, 47, 47, 1)',
          borderWidth: 2
        },
        {
          label: 'Consumo Tabla (g)',
          data: ind.map((x: any) => x.consumoTabla || 0),
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
        data: ind.map((x: any) => x.pesoCierre || 0),
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
        data: ind.map((x: any) => x.conversionAlimenticia || 0),
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
        data: ind.map((x: any) => x.seleccionSem || 0),
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
        data: ind.map((x: any) => x.avesFinSemana || 0),
        backgroundColor: 'rgba(123, 31, 162, 0.7)',
        borderColor: 'rgba(123, 31, 162, 1)',
        borderWidth: 2
      }]
    };

    // Gráfica de Torta (distribución de mortalidad vs selección en última semana)
    const ultimaSemana = ind[ind.length - 1];
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

    const semana1 = this.indicadoresFiltrados.find((ind: any) => ind.semana === this.semanaComparacion1);
    const semana2 = this.indicadoresFiltrados.find((ind: any) => ind.semana === this.semanaComparacion2);

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
    const semana1 = this.indicadoresFiltrados.find((ind: any) => ind.semana === this.semanaComparacion1);
    const semana2 = this.indicadoresFiltrados.find((ind: any) => ind.semana === this.semanaComparacion2);

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
    const ind = this.indicadoresFiltrados;
    if (ind.length === 0) return [];

    return [
      {
        nombre: 'Consumo Real (g)',
        datos: ind.map((x: any) => ({
          semana: x.semana,
          fecha: x.fechaInicio,
          valor: x.consumoReal,
          etiqueta: `Semana ${x.semana}: ${x.consumoReal.toFixed(0)}g`
        })),
        color: '#d32f2f',
        tipo: 'barra'
      },
      {
        nombre: 'Consumo Tabla (g)',
        datos: ind.map((x: any) => ({
          semana: x.semana,
          fecha: x.fechaInicio,
          valor: x.consumoTabla,
          etiqueta: `Semana ${x.semana}: ${x.consumoTabla.toFixed(0)}g`
        })),
        color: '#1976d2',
        tipo: 'barra'
      },
      {
        nombre: 'Peso Promedio (g)',
        datos: ind.map((x: any) => ({
          semana: x.semana,
          fecha: x.fechaInicio,
          valor: x.pesoCierre,
          etiqueta: `Semana ${x.semana}: ${x.pesoCierre.toFixed(2)}g`
        })),
        color: '#388e3c',
        tipo: 'linea'
      },
      {
        nombre: 'Mortalidad (%)',
        datos: ind.map((x: any) => ({
          semana: x.semana,
          fecha: x.fechaInicio,
          valor: x.mortalidadSem,
          etiqueta: `Semana ${x.semana}: ${x.mortalidadSem.toFixed(2)}%`
        })),
        color: '#f57c00',
        tipo: 'barra'
      },
      {
        nombre: 'Selección (%)',
        datos: ind.map((x: any) => ({
          semana: x.semana,
          fecha: x.fechaInicio,
          valor: x.seleccionSem,
          etiqueta: `Semana ${x.semana}: ${x.seleccionSem.toFixed(2)}%`
        })),
        color: '#9c27b0',
        tipo: 'barra'
      },
      {
        nombre: 'Conversión Alimenticia',
        datos: ind.map((x: any) => ({
          semana: x.semana,
          fecha: x.fechaInicio,
          valor: x.conversionAlimenticia,
          etiqueta: `Semana ${x.semana}: ${x.conversionAlimenticia.toFixed(2)}`
        })),
        color: '#2196f3',
        tipo: 'linea'
      },
      {
        nombre: 'Eficiencia',
        datos: ind.map((x: any) => ({
          semana: x.semana,
          fecha: x.fechaInicio,
          valor: x.eficiencia,
          etiqueta: `Semana ${x.semana}: ${(x.eficiencia || 0).toFixed(2)}`
        })),
        color: '#4caf50',
        tipo: 'linea'
      },
      {
        nombre: 'Aves Vivas',
        datos: ind.map((x: any) => ({
          semana: x.semana,
          fecha: x.fechaInicio,
          valor: x.avesFinSemana,
          etiqueta: `Semana ${x.semana}: ${x.avesFinSemana} aves`
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
    const ind = this.indicadoresFiltrados;
    if (ind.length === 0) return 0;
    const primerPeso = ind[0].pesoInicial;
    const ultimoPeso = ind[ind.length - 1].pesoCierre;
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
    const ind = this.indicadoresFiltrados;
    if (ind.length === 0) return 0;
    const suma = ind.reduce((acc: number, x: any) => acc + (x[propiedad] || 0), 0);
    return suma / ind.length;
  }

  getMejorSemanaConversion(): string {
    const ind = this.indicadoresFiltrados;
    if (ind.length === 0) return 'N/A';

    const mejorSemana = ind.reduce((mejor: any, actual: any) =>
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
    const ind = this.indicadoresFiltrados;
    if (this.metricasSeleccionadas.length === 0 || ind.length === 0) {
      this.comparacionPersonalizadaChartData = { labels: [], datasets: [] };
      return;
    }

    const labels = ind.map((x: any) => `Semana ${x.semana}`);
    const datasets = this.metricasSeleccionadas.map(metricaId => {
      const metrica = this.metricasDisponibles.find(m => m.id === metricaId);
      if (!metrica) return null;

      let data: number[] = [];
      let label = metrica.nombre;

      switch (metricaId) {
        case 'mortalidad':
          data = ind.map((x: any) => x.mortalidadSem || 0);
          break;
        case 'consumoReal':
          data = ind.map((x: any) => x.consumoReal || 0);
          break;
        case 'consumoTabla':
          data = ind.map((x: any) => x.consumoTabla || 0);
          break;
        case 'peso':
          data = ind.map((x: any) => x.pesoCierre || 0);
          break;
        case 'conversion':
          data = ind.map((x: any) => x.conversionAlimenticia || 0);
          break;
        case 'seleccion':
          data = ind.map((x: any) => x.seleccionSem || 0);
          break;
        case 'aves':
          data = ind.map((x: any) => x.avesFinSemana || 0);
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
    const ind = this.indicadoresFiltrados;
    if (this.tiposGraficaSeleccionados.length === 0 || ind.length === 0) {
      this.tiposCombinadosChartData = { labels: [], datasets: [] };
      return;
    }

    const labels = ind.map((x: any) => `Semana ${x.semana}`);
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
          data = ind.map((x: any) => x.mortalidadSem || 0);
          break;
        case 'consumo':
          data = ind.map((x: any) => x.consumoReal || 0);
          break;
        case 'consumoTabla':
          data = ind.map((x: any) => x.consumoTabla || 0);
          break;
        case 'peso':
          data = ind.map((x: any) => x.pesoCierre || 0);
          break;
        case 'conversion':
          data = ind.map((x: any) => x.conversionAlimenticia || 0);
          break;
        case 'seleccion':
          data = ind.map((x: any) => x.seleccionSem || 0);
          break;
        case 'retiro':
          data = ind.map((x: any) => x.retiroSem || 0);
          break;
        case 'uniformidad':
          data = ind.map((x: any) => x.uniformidad || 0);
          break;
        case 'cv':
          data = ind.map((x: any) => x.cv || 0);
          break;
        case 'difConsumo':
          data = ind.map((x: any) => x.difConsumoPorc || 0);
          break;
        case 'incrConsumo':
          data = ind.map((x: any) => x.incrConsumoReal || 0);
          break;
        case 'aves':
          data = ind.map((x: any) => x.avesFinSemana || 0);
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

import { Component, Input, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { SeguimientoLoteLevanteDto, SeguimientoLoteLevanteService, IndicadorSemanalLevanteDto } from '../../services/seguimiento-lote-levante.service';
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
  changeDetection: ChangeDetectionStrategy.Eager,
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
  tipoGraficaSeleccionada: 'mortalidad' | 'consumo' | 'peso' | 'seleccion' | 'aves' | 'comparacion' = 'mortalidad';
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
    { id: 'consumoReal', nombre: 'Consumo (g/ave/día)', icono: '🍽️', color: 'rgba(211, 47, 47, 1)' },
    { id: 'consumoTabla', nombre: 'Consumo Tabla (g)', icono: '📊', color: 'rgba(25, 118, 210, 1)' },
    { id: 'peso', nombre: 'Peso Promedio (g)', icono: '⚖️', color: 'rgba(56, 142, 60, 1)' },
    // Conversión Alimenticia removida: es parámetro de pollo de engorde, no aplica a reproductoras (REQ-002h).
    { id: 'seleccion', nombre: 'Selección (%)', icono: '📋', color: 'rgba(156, 39, 176, 1)' },
    { id: 'aves', nombre: 'Aves Vivas', icono: '🐔', color: 'rgba(123, 31, 162, 1)' }
  ];
  metricasSeleccionadas: string[] = [];
  mostrarComparacionPersonalizada: boolean = false;

  // ========== SELECTOR DE SEXO (REQ-010b) ==========
  /**
   * Selector Hembras/Machos/Ambos. El backend (`fn_indicadores_levante_postura` vía
   * `IndicadorSemanalLevanteDto`) ya expone series POR SEXO (consumoDiario/consumoTabla/peso/
   * pesoTabla/mortPct/mortTabla/retiroPct por Hembras y Machos) además de las mixtas. Con el grupo:
   *   - 'ambos'   → usa las series MIXTAS (comportamiento previo, intacto).
   *   - 'hembras' → usa las series *Hembras.
   *   - 'machos'  → usa las series *Machos.
   * `grupoSexoSeleccionado`/`grupoSexoLabel` etiquetan los títulos; `prepararChartData` y la tarjeta
   * "Comparativo con Guía" (`construirComparativoGuia`) recomputan las series al cambiar el grupo.
   */
  grupoSexoSeleccionado: 'ambos' | 'hembras' | 'machos' = 'ambos';
  readonly grupoSexoDisponible = true;

  get grupoSexoLabel(): string {
    switch (this.grupoSexoSeleccionado) {
      case 'hembras': return 'Hembras';
      case 'machos': return 'Machos';
      default: return 'Hembras + Machos';
    }
  }

  /** Recalcula las series de todas las gráficas y del comparativo con guía según el grupo activo. */
  onGrupoSexoChange(): void {
    this.prepararChartData();
    this.construirComparativoGuia();
  }

  /**
   * Valor Real de una métrica para un punto según el grupo activo (REQ-010b).
   * 'ambos' devuelve EXACTAMENTE la expresión mixta previa (`x[mixto] || 0`); 'hembras'/'machos'
   * toman la serie por sexo (null/undefined → 0 para las gráficas de barras/líneas).
   */
  private serieValor(x: any, mixto: string, hembras: string, machos: string): number {
    if (this.grupoSexoSeleccionado === 'hembras') return x[hembras] ?? 0;
    if (this.grupoSexoSeleccionado === 'machos')  return x[machos] ?? 0;
    return x[mixto] || 0;
  }

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

  // ===== Comparativo Real vs Guía (levante) =====
  metricaComparativa: 'consumo' | 'peso' | 'mortalidad' | 'retiro' = 'consumo';
  comparativoGuiaChartData: ChartData<'line'> = { labels: [], datasets: [] };
  comparativoGuiaChartOptions: ChartConfiguration['options'] = {};

  /** Construye la gráfica comparativa Real vs Guía (semanas 1-25) según la métrica seleccionada. */
  construirComparativoGuia(): void {
    const data = (this.indicadoresSemanales || []) as any[];
    const labels = data.map(d => `S${d.semana}`);
    const real: (number | null)[] = [];
    const guia: (number | null)[] = [];
    let etiqueta = '', unidad = '';

    // Los valores "tabla" (guía) vienen del DTO calculado en la BD (fn_indicadores_levante_postura):
    // consumoTabla, pesoTabla, mortTabla (mixtos) + *Hembras/*Machos por sexo. El front NO recalcula.
    // Según el grupo activo (REQ-010b): 'ambos' usa la serie mixta (comportamiento previo intacto),
    // 'hembras'/'machos' usan la serie por sexo. Real preserva null (spanGaps); Guía convierte 0/null
    // a null para que el chart no dibuje puntos falsos.
    for (const d of data) {
      switch (this.metricaComparativa) {
        case 'consumo':
          etiqueta = 'Consumo'; unidad = 'g/ave/día';
          real.push(this.realPorGrupo(d, 'consumoReal', 'consumoRealHembras', 'consumoRealMachos'));
          guia.push(this.guiaPorGrupo(d, 'consumoTabla', 'consumoTablaHembras', 'consumoTablaMachos'));
          break;
        case 'peso':
          etiqueta = 'Peso'; unidad = 'g';
          real.push(this.realPorGrupo(d, 'pesoCierre', 'pesoHembras', 'pesoMachos'));
          guia.push(this.guiaPorGrupo(d, 'pesoTabla', 'pesoTablaHembras', 'pesoTablaMachos'));
          break;
        case 'mortalidad':
          etiqueta = '% Mortalidad semana'; unidad = '%';
          real.push(this.realPorGrupo(d, 'mortalidadSem', 'mortPctHembras', 'mortPctMachos'));
          guia.push(this.guiaPorGrupo(d, 'mortTabla', 'mortTablaHembras', 'mortTablaMachos'));
          break;
        case 'retiro':
          // La BD no expone guía de retiro (ni mixta ni por sexo); se muestra solo la serie real.
          etiqueta = '% Retiro (Mort+Sel+ErrSex)'; unidad = '%';
          real.push(this.realPorGrupo(d, 'retiroSem', 'retiroPctHembras', 'retiroPctMachos'));
          guia.push(null);
          break;
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
      responsive: true, maintainAspectRatio: false,
      plugins: { legend: { display: true, position: 'top' }, title: { display: true, text: `${etiqueta} — Real vs Guía (${unidad}) · ${this.grupoSexoLabel}` } },
      scales: { y: { beginAtZero: false, title: { display: true, text: unidad } } }
    };
  }

  /**
   * Serie Real por grupo (comparativo): 'ambos' usa la mixta (comportamiento previo, `x[mixto] ?? null`),
   * 'hembras'/'machos' la serie por sexo. Preserva null para que el chart use spanGaps.
   */
  private realPorGrupo(x: any, mixto: string, hembras: string, machos: string): number | null {
    const v = this.grupoSexoSeleccionado === 'hembras' ? x[hembras]
            : this.grupoSexoSeleccionado === 'machos'  ? x[machos]
            : x[mixto];
    return v ?? null;
  }

  /**
   * Serie Guía por grupo (comparativo): 'ambos' usa la mixta; convierte 0/null a null para no dibujar
   * puntos falsos (idéntico al criterio previo `(v ?? 0) > 0 ? v : null`). H/M usan la guía por sexo
   * (peso_h/_m, mort_sem_h/_m); si la guía no trae el dato del sexo, degrada a null (spanGaps).
   */
  private guiaPorGrupo(x: any, mixto: string, hembras: string, machos: string): number | null {
    const v = this.grupoSexoSeleccionado === 'hembras' ? x[hembras]
            : this.grupoSexoSeleccionado === 'machos'  ? x[machos]
            : x[mixto];
    return (v ?? 0) > 0 ? (v as number) : null;
  }

  constructor(private seguimientoSvc: SeguimientoLoteLevanteService) {
    this.initChartOptions();
  }

  ngOnInit(): void {
    void this.prepararDatosGraficas();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      void this.prepararDatosGraficas();
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
  /**
   * Los indicadores semanales de levante se calculan en la BD
   * (fn_indicadores_levante_postura, endpoint …/por-lote/{id}/indicadores).
   * El front SOLO pinta: no recalcula desde los seguimientos crudos ni consulta
   * la guía genética en el cliente. Los valores "tabla" (consumo/peso/mortalidad
   * de guía) llegan dentro del mismo DTO, garantizando consistencia con la tabla
   * de indicadores.
   */
  private async prepararDatosGraficas(): Promise<void> {
    if (!this.seguimientos || this.seguimientos.length === 0 || !this.selectedLote) {
      this.seriesGraficas = [];
      this.indicadoresSemanales = [];
      return;
    }

    // Traer indicadores calculados en la BD (misma fuente que la tabla).
    // Levante = semanas 1..25: acotar por si la fn devolviera algo fuera de rango (REQ-008).
    const loteId = this.resolverLoteId();
    if (loteId == null) {
      this.seriesGraficas = [];
      this.indicadoresSemanales = [];
      return;
    }
    let dtos: IndicadorSemanalLevanteDto[] = [];
    try {
      dtos = await firstValueFrom(this.seguimientoSvc.getIndicadores(loteId)) || [];
    } catch (e) {
      console.warn('Indicadores desde BD no disponibles para gráficas:', e);
      this.seriesGraficas = [];
      this.indicadoresSemanales = [];
      return;
    }
    this.indicadoresSemanales = dtos
      .filter(d => d.semana >= 1 && d.semana <= 25)
      .sort((a, b) => a.semana - b.semana)
      .map(d => this.mapDtoAIndicador(d));
    this.calcularIncrementosConsumo(this.indicadoresSemanales);

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

    // Comparativo Real vs Guía
    this.construirComparativoGuia();
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

    // Gráfica de Mortalidad — Real por grupo (REQ-010b: 'ambos' mixta, H/M por sexo).
    this.mortalidadChartData = {
      labels,
      datasets: [{
        label: 'Mortalidad (%)',
        data: ind.map((x: any) => this.serieValor(x, 'mortalidadSem', 'mortPctHembras', 'mortPctMachos')),
        backgroundColor: 'rgba(245, 124, 0, 0.7)',
        borderColor: 'rgba(245, 124, 0, 1)',
        borderWidth: 2
      }]
    };

    // Gráfica de Consumo — Real + Tabla por grupo (REQ-010b).
    this.consumoChartData = {
      labels,
      datasets: [
        {
          label: 'Consumo (g/ave/día)',
          data: ind.map((x: any) => this.serieValor(x, 'consumoReal', 'consumoRealHembras', 'consumoRealMachos')),
          backgroundColor: 'rgba(211, 47, 47, 0.7)',
          borderColor: 'rgba(211, 47, 47, 1)',
          borderWidth: 2
        },
        {
          label: 'Consumo Tabla (g)',
          data: ind.map((x: any) => this.serieValor(x, 'consumoTabla', 'consumoTablaHembras', 'consumoTablaMachos')),
          backgroundColor: 'rgba(25, 118, 210, 0.7)',
          borderColor: 'rgba(25, 118, 210, 1)',
          borderWidth: 2
        }
      ]
    };

    // Gráfica de Peso — Real por grupo (REQ-010b).
    this.pesoChartData = {
      labels,
      datasets: [{
        label: 'Peso Promedio (g)',
        data: ind.map((x: any) => this.serieValor(x, 'pesoCierre', 'pesoHembras', 'pesoMachos')),
        backgroundColor: 'rgba(56, 142, 60, 0.7)',
        borderColor: 'rgba(56, 142, 60, 1)',
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
      { nombre: 'Consumo (g/ave/día)', semana1: semana1.consumoReal || 0, semana2: semana2.consumoReal || 0 },
      { nombre: 'Peso (g)', semana1: semana1.pesoCierre || 0, semana2: semana2.pesoCierre || 0 },
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

  /** Título de la gráfica activa, etiquetado con el grupo H/M/Ambos activo (REQ-010b). */
  getTituloGrafica(): string {
    return `${this.getTituloGraficaBase()} · ${this.grupoSexoLabel}`;
  }

  private getTituloGraficaBase(): string {
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
        'seleccion': '📋',
        'retiro': '📉',
        'uniformidad': '📐',
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
      'seleccion': '📋 Selección por Semana',
      'aves': '🐔 Aves Vivas por Semana',
      'comparacion': '📊 Comparación Personalizada'
    };
    return titulos[this.tipoGraficaSeleccionada] || '📊 Gráfica';
  }

  /** loteId numérico (lotes.lote_id) para pedir los indicadores a la BD. */
  private resolverLoteId(): number | null {
    const candidatos = [
      (this.selectedLote as any)?.loteId,
      (this.seguimientos?.[0] as any)?.loteId
    ];
    for (const c of candidatos) {
      const n = Number(c);
      if (Number.isFinite(n) && n > 0) return n;
    }
    return null;
  }

  /**
   * Mapea el DTO de la BD al modelo que consumen las gráficas. Los campos derivados
   * (retiroSem, difConsumoPorc) se obtienen SOLO de los valores ya calculados en la BD,
   * el front no recalcula. `fechaInicio` es contexto (no cálculo) para el filtro por fecha.
   */
  private mapDtoAIndicador(d: IndicadorSemanalLevanteDto): any {
    const consumoReal = d.consumoDiario;
    const consumoTabla = d.consumoTabla;
    return {
      semana: d.semana,
      fechaInicio: this.obtenerFechaInicioSemana(d.semana),
      avesInicioSemana: d.avesInicioSemana,
      avesFinSemana: d.avesFinSemana,
      consumoReal,
      consumoTabla,
      conversionAlimenticia: d.conversionAlimenticia,
      mortalidadSem: d.mortalidadSem,
      seleccionSem: d.seleccionSem,
      errorSexajeSem: d.errorSexajeSem,
      retiroSem: d.mortalidadSem + d.seleccionSem + d.errorSexajeSem,
      uniformidad: d.unifReal,
      difConsumoPorc: consumoTabla > 0 ? ((consumoReal - consumoTabla) / consumoTabla) * 100 : 0,
      eficiencia: d.eficiencia,
      ip: d.ip,
      pesoCierre: d.pesoCierre,
      pesoInicial: d.pesoInicial,
      pesoTabla: d.pesoTabla,
      mortTabla: d.mortTabla,
      gananciaSemana: d.gananciaSemana,
      // ── Series POR SEXO (REQ-010b): se copian tal cual de la BD (ya calculadas), el front NO
      //    recalcula. Alimentan el selector Hembras/Machos; 'ambos' sigue usando las mixtas. ──
      consumoRealHembras: d.consumoDiarioHembras ?? null,
      consumoRealMachos: d.consumoDiarioMachos ?? null,
      consumoTablaHembras: d.consumoTablaHembras ?? null,
      consumoTablaMachos: d.consumoTablaMachos ?? null,
      pesoHembras: d.pesoHembras ?? null,
      pesoMachos: d.pesoMachos ?? null,
      pesoTablaHembras: d.pesoTablaHembras ?? null,
      pesoTablaMachos: d.pesoTablaMachos ?? null,
      mortPctHembras: d.mortPctHembras ?? null,
      mortPctMachos: d.mortPctMachos ?? null,
      mortTablaHembras: d.mortTablaHembras ?? null,
      mortTablaMachos: d.mortTablaMachos ?? null,
      retiroPctHembras: d.retiroPctHembras ?? null,
      retiroPctMachos: d.retiroPctMachos ?? null,
      // Se completan en calcularIncrementosConsumo (diferencia vs semana anterior).
      incrConsumoReal: 0,
      incrConsumoTabla: 0
    };
  }

  /** Incrementos de consumo (real/tabla) respecto a la semana anterior. No es un cálculo de negocio; es un delta de presentación sobre los valores ya calculados en la BD. */
  private calcularIncrementosConsumo(indicadores: any[]): void {
    for (let i = 1; i < indicadores.length; i++) {
      const prev = indicadores[i - 1];
      const cur = indicadores[i];
      cur.incrConsumoReal = (cur.consumoReal || 0) - (prev.consumoReal || 0);
      cur.incrConsumoTabla = (cur.consumoTabla || 0) - (prev.consumoTabla || 0);
    }
  }

  private prepararSeriesGraficas(): SerieGrafica[] {
    const ind = this.indicadoresFiltrados;
    if (ind.length === 0) return [];

    return [
      {
        nombre: 'Consumo (g/ave/día)',
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
      'seleccion': 'rgba(156, 39, 176, 1)',
      'retiro': 'rgba(244, 67, 54, 1)',
      'uniformidad': 'rgba(156, 39, 176, 1)',
      'difConsumo': 'rgba(233, 30, 99, 1)',
      'incrConsumo': 'rgba(255, 152, 0, 1)',
      'aves': 'rgba(123, 31, 162, 1)'
    };

    const nombresPorTipo: { [key: string]: string } = {
      'mortalidad': 'Mortalidad (%)',
      'consumo': 'Consumo (g/ave/día)',
      'consumoTabla': 'Consumo Tabla (g)',
      'peso': 'Peso Promedio (g)',
      'seleccion': 'Selección (%)',
      'retiro': 'Retiro (%)',
      'uniformidad': 'Uniformidad (%)',
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
        case 'seleccion':
          data = ind.map((x: any) => x.seleccionSem || 0);
          break;
        case 'retiro':
          data = ind.map((x: any) => x.retiroSem || 0);
          break;
        case 'uniformidad':
          data = ind.map((x: any) => x.uniformidad || 0);
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

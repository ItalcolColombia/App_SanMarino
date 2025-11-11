import { Component, Input, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy, signal, ChangeDetectorRef } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';

import {
  ProduccionService,
  LiquidacionTecnicaProduccionDto,
  EtapaLiquidacionDto,
  MetricasAcumuladasProduccionDto,
  ComparacionGuiaProduccionDto
} from '../../services/produccion.service';

@Component({
  selector: 'app-liquidacion-tecnica',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, NgChartsModule, DatePipe, DecimalPipe],
  templateUrl: './liquidacion-tecnica.component.html',
  styleUrls: ['./liquidacion-tecnica.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LiquidacionTecnicaComponent implements OnInit, OnChanges {
  @Input() loteId: string | null = null;
  @Input() loteNombre: string | null = null;

  // Señales reactivas
  loading = signal(false);
  liquidacion = signal<LiquidacionTecnicaProduccionDto | null>(null);
  error = signal<string | null>(null);

  // Formulario para filtros
  form: FormGroup;

  // Vista activa y etapa seleccionada
  vistaActiva: 'resumen' | 'etapas' | 'comparacion' = 'resumen';
  etapaSeleccionada: 1 | 2 | 3 | null = null;

  // Configuraciones de gráficos
  public barChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: true, position: 'top' },
      title: { display: false }
    },
    scales: {
      x: { display: true, grid: { display: false } },
      y: { display: true, beginAtZero: true, grid: { color: 'rgba(0,0,0,0.1)' } }
    }
  };

  constructor(
    private fb: FormBuilder,
    private produccionService: ProduccionService,
    private cdr: ChangeDetectorRef
  ) {
    this.form = this.fb.group({
      fechaHasta: [new Date()]
    });
  }

  ngOnInit(): void {
    // NO cargar datos aquí, esperar a ngOnChanges
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['loteId'] && this.loteId) {
      this.cargarLiquidacion();
    }
  }

  /**
   * Cargar liquidación técnica de producción
   */
  cargarLiquidacion(): void {
    if (!this.loteId) {
      this.liquidacion.set(null);
      return;
    }

    const loteIdNum = parseInt(this.loteId, 10);
    if (isNaN(loteIdNum)) {
      this.error.set('ID de lote inválido');
      return;
    }

    const fechaHasta = this.form.value.fechaHasta || new Date();
    const fechaHastaStr = fechaHasta instanceof Date ? fechaHasta.toISOString() : fechaHasta;

    this.loading.set(true);
    this.error.set(null);

    this.produccionService.calcularLiquidacionProduccion(loteIdNum, fechaHastaStr).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: (data) => {
        this.liquidacion.set(data);
        this.cdr.markForCheck();
      },
      error: (error: any) => {
        console.error('Error al cargar liquidación técnica:', error);
        this.error.set(this.getErrorMessage(error));
        this.liquidacion.set(null);
        this.cdr.markForCheck();
      }
    });
  }

  /**
   * Cambiar vista activa
   */
  cambiarVista(vista: 'resumen' | 'etapas' | 'comparacion'): void {
    this.vistaActiva = vista;
    this.etapaSeleccionada = null;
  }

  /**
   * Seleccionar etapa para ver detalle
   */
  seleccionarEtapa(etapa: number): void {
    if (etapa === 1 || etapa === 2 || etapa === 3) {
      this.etapaSeleccionada = etapa as 1 | 2 | 3;
      this.vistaActiva = 'etapas';
    }
  }

  /**
   * Actualizar datos
   */
  actualizar(): void {
    this.cargarLiquidacion();
  }

  /**
   * Obtener mensaje de error
   */
  private getErrorMessage(error: any): string {
    if (error?.error?.message) {
      return error.error.message;
    }
    if (error?.message) {
      return error.message;
    }
    return 'Error al cargar la liquidación técnica. Por favor, intenta de nuevo.';
  }

  /**
   * Obtener etapa por número
   */
  getEtapa(num: number): EtapaLiquidacionDto | null {
    const liq = this.liquidacion();
    if (!liq) return null;
    if (num === 1) return liq.etapa1;
    if (num === 2) return liq.etapa2;
    if (num === 3) return liq.etapa3;
    return null;
  }

  /**
   * Formatear porcentaje con signo
   */
  formatearPorcentaje(valor?: number): string {
    if (valor === null || valor === undefined) return '—';
    const signo = valor >= 0 ? '+' : '';
    return `${signo}${valor.toFixed(2)}%`;
  }

  /**
   * Obtener clase CSS según diferencia
   */
  getDiferenciaClass(valor?: number): string {
    if (valor === null || valor === undefined) return '';
    if (Math.abs(valor) <= 5) return 'diferencia-ok';
    if (Math.abs(valor) <= 10) return 'diferencia-warning';
    return 'diferencia-danger';
  }

  /**
   * Obtener estado de cumplimiento
   */
  getEstadoCumplimiento(valor?: number): { texto: string; clase: string } {
    if (valor === null || valor === undefined) {
      return { texto: 'Sin datos', clase: 'estado-neutral' };
    }
    if (Math.abs(valor) <= 5) {
      return { texto: 'Óptimo', clase: 'estado-ok' };
    }
    if (Math.abs(valor) <= 10) {
      return { texto: 'Aceptable', clase: 'estado-warning' };
    }
    return { texto: 'Requiere atención', clase: 'estado-danger' };
  }

  /**
   * Preparar datos para gráfico de etapas
   */
  getEtapasChartData(): ChartData<'bar'> {
    const liq = this.liquidacion();
    if (!liq) return { labels: [], datasets: [] };

    return {
      labels: ['Etapa 1 (25-33)', 'Etapa 2 (34-50)', 'Etapa 3 (>50)'],
      datasets: [
        {
          label: 'Huevos Totales',
          data: [liq.etapa1.huevosTotales, liq.etapa2.huevosTotales, liq.etapa3.huevosTotales],
          backgroundColor: 'rgba(54, 162, 235, 0.6)'
        },
        {
          label: 'Huevos Incubables',
          data: [liq.etapa1.huevosIncubables, liq.etapa2.huevosIncubables, liq.etapa3.huevosIncubables],
          backgroundColor: 'rgba(75, 192, 192, 0.6)'
        },
        {
          label: 'Mortalidad Hembras',
          data: [liq.etapa1.mortalidadHembras, liq.etapa2.mortalidadHembras, liq.etapa3.mortalidadHembras],
          backgroundColor: 'rgba(255, 99, 132, 0.6)'
        }
      ]
    };
  }

  /**
   * Preparar datos para gráfico de comparación
   */
  getComparacionChartData(): ChartData<'bar'> | null {
    const liq = this.liquidacion();
    if (!liq?.comparacionGuia) return null;

    const comp = liq.comparacionGuia;
    return {
      labels: ['Consumo H', 'Consumo M', 'Peso H', 'Peso M', 'Mortalidad H', 'Mortalidad M'],
      datasets: [
        {
          label: 'Real',
          data: [
            liq.totales.consumoPromedioDiarioKg * 1000 / liq.totales.totalAvesActuales || 0,
            liq.totales.consumoPromedioDiarioKg * 1000 / liq.totales.totalAvesActuales || 0,
            comp.pesoGuiaHembras || 0,
            comp.pesoGuiaMachos || 0,
            liq.totales.porcentajeMortalidadAcumuladaHembras || 0,
            liq.totales.porcentajeMortalidadAcumuladaMachos || 0
          ],
          backgroundColor: 'rgba(54, 162, 235, 0.6)'
        },
        {
          label: 'Guía Genética',
          data: [
            comp.consumoGuiaHembras || 0,
            comp.consumoGuiaMachos || 0,
            comp.pesoGuiaHembras || 0,
            comp.pesoGuiaMachos || 0,
            comp.mortalidadGuiaHembras || 0,
            comp.mortalidadGuiaMachos || 0
          ],
          backgroundColor: 'rgba(255, 206, 86, 0.6)'
        }
      ]
    };
  }
}

import { Component, Input, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy, signal, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';

import {
  LiquidacionTecnicaService,
  LiquidacionTecnicaDto,
  LiquidacionTecnicaCompletaDto
} from '../../services/liquidacion-tecnica.service';
import {
  LiquidacionComparacionService,
  LiquidacionTecnicaComparacionDto
} from '../../services/liquidacion-comparacion.service';
import { GuiaGeneticaService } from '../../../../services/guia-genetica.service';
import { LoteDto } from '../../../lote/services/lote.service';
import {
  getEstadoClase,
  getDiferenciaClass,
  getEstadoClass,
  getEstadoTexto,
  getCumplimientoClass,
  formatDate,
  getErrorMessageLiquidacion,
  calcularEdadSemanas,
  calcularEdadDias
} from '../../funciones/liquidacion-tecnica-estados.funcion';
import {
  construirIndicadores,
  calcularCumplimientoGeneral
} from '../../funciones/liquidacion-tecnica-indicadores.funcion';
import {
  construirIndicadoresChartData,
  construirRetirosChartData,
  construirEvolucionChartData,
  construirConsumoPesoChartData
} from '../../funciones/liquidacion-tecnica-graficos.funcion';

@Component({
  selector: 'app-liquidacion-tecnica',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, NgChartsModule],
  templateUrl: './liquidacion-tecnica.component.html',
  styleUrls: ['./liquidacion-tecnica.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LiquidacionTecnicaComponent implements OnInit, OnChanges {
  @Input() loteId: string | null = null;
  @Input() loteNombre: string | null = null;
  /** true cuando el lote es LoteAveEngordeId (módulo aves de engorde / Ecuador). */
  @Input() esLoteAveEngorde: boolean = false;

  // Señales reactivas
  loading = signal(false);
  liquidacion = signal<LiquidacionTecnicaDto | null>(null);
  liquidacionCompleta = signal<LiquidacionTecnicaCompletaDto | null>(null);
  comparacion = signal<LiquidacionTecnicaComparacionDto | null>(null);
  error = signal<string | null>(null);

  // Datos completos del lote
  datosLote = signal<LoteDto | null>(null);

  // Formulario para filtros
  form: FormGroup;

  // Vista activa
  vistaActiva: 'resumen' | 'detalle' | 'graficos' = 'resumen';

  // Flag para indicar si los datos de la guía genética se han cargado
  guiaGeneticaCargada: boolean = false;

  // Propiedades para comparación con Guía Genética
  pesoEsperadoGuia: number = 0;
  pesoEsperadoHembrasGuia: number = 0;
  pesoEsperadoMachosGuia: number = 0;
  consumoEsperadoGuia: number = 0;
  mortalidadEsperadaGuia: number = 0;
  mortalidadEsperadaHembrasGuia: number = 0;
  mortalidadEsperadaMachosGuia: number = 0;
  uniformidadEsperadaGuia: number = 0;
  uniformidadEsperadaHembrasGuia: number = 0;
  uniformidadEsperadaMachosGuia: number = 0;
  porcentajeCumplimientoGeneral: number = 0;
  parametrosOptimos: number = 0;

  // Configuraciones de gráficos
  public barChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'top',
      },
      title: {
        display: false
      }
    },
    scales: {
      x: {
        display: true,
        grid: {
          display: false
        }
      },
      y: {
        display: true,
        beginAtZero: true,
        grid: {
          color: 'rgba(0,0,0,0.1)'
        }
      }
    }
  };

  public pieChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'right',
      },
      title: {
        display: false
      }
    }
  };

  public lineChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'top',
      },
      title: {
        display: false
      }
    },
    scales: {
      x: {
        display: true,
        grid: {
          display: false
        }
      },
      y: {
        display: true,
        beginAtZero: true,
        grid: {
          color: 'rgba(0,0,0,0.1)'
        }
      }
    },
    elements: {
      line: {
        tension: 0.4
      },
      point: {
        radius: 4,
        hoverRadius: 6
      }
    }
  };

  constructor(
    private fb: FormBuilder,
    private liquidacionService: LiquidacionTecnicaService,
    private comparacionService: LiquidacionComparacionService,
    private guiaGeneticaService: GuiaGeneticaService,
    private cdr: ChangeDetectorRef
  ) {
    this.form = this.fb.group({
      fechaHasta: [new Date()],
      tipoVista: ['resumen'] // 'resumen' | 'completa'
    });
  }

  ngOnInit(): void {
    // NO cargar datos aquí, esperar a ngOnChanges
    // El componente se inicializa sin loteId, y ngOnChanges lo manejará
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['loteId'] && this.loteId) {
      // Solo cargar liquidación, no hacer petición separada para datos del lote
      this.cargarLiquidacion();
    }
  }

  /**
   * Cargar datos completos del lote
   */
  cargarDatosLote(): void {
    // Ya no se usa - los datos vienen de LiquidacionTecnicaDto
    
    
  }

  /**
   * Cargar liquidación técnica según el tipo seleccionado
   */
  cargarLiquidacion(): void {
    if (!this.loteId) {
      this.liquidacion.set(null);
      this.liquidacionCompleta.set(null);
      this.comparacion.set(null);
      return;
    }

    const fechaHasta = this.form.value.fechaHasta || new Date();
    const tipoVista = this.form.value.tipoVista || 'resumen';

    this.loading.set(true);
    this.error.set(null);

    let request$: Observable<LiquidacionTecnicaDto | LiquidacionTecnicaCompletaDto>;

    if (tipoVista === 'completa') {
      request$ = this.liquidacionService.getLiquidacionCompleta(this.loteId, fechaHasta, this.esLoteAveEngorde);
    } else {
      request$ = this.liquidacionService.getLiquidacionTecnica(this.loteId, fechaHasta, this.esLoteAveEngorde);
    }

    // Cargar liquidación técnica básica
    request$.pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: (data: LiquidacionTecnicaDto | LiquidacionTecnicaCompletaDto) => {
        if (tipoVista === 'completa') {
          const completaData = data as LiquidacionTecnicaCompletaDto & { liquidacion?: LiquidacionTecnicaDto };
          this.liquidacionCompleta.set(completaData);
          this.liquidacion.set(completaData.resumen ?? completaData.liquidacion ?? null);
        } else {
          const simpleData = data as LiquidacionTecnicaDto;
          this.liquidacion.set(simpleData);
          this.liquidacionCompleta.set(null);
        }

        // Cargar datos de comparación con guía genética
        this.cargarComparacion();
      },
      error: (error: any) => {
        console.error('Error al cargar liquidación técnica:', error);
        this.error.set(this.getErrorMessage(error));
        this.liquidacion.set(null);
        this.liquidacionCompleta.set(null);
        this.comparacion.set(null);
      }
    });
  }


  /**
   * Cambiar vista activa
   */
  cambiarVista(vista: 'resumen' | 'detalle' | 'graficos'): void {
    this.vistaActiva = vista;
  }

  /**
   * Actualizar datos
   */
  actualizar(): void {
    this.cargarLiquidacion();
  }

  /**
   * Obtener indicadores para la tabla comparativa
   */
  get indicadores() {
    return construirIndicadores(this.liquidacion(), this.comparacion(), {
      pesoEsperadoHembrasGuia: this.pesoEsperadoHembrasGuia,
      pesoEsperadoMachosGuia: this.pesoEsperadoMachosGuia,
      mortalidadEsperadaHembrasGuia: this.mortalidadEsperadaHembrasGuia,
      mortalidadEsperadaMachosGuia: this.mortalidadEsperadaMachosGuia,
      uniformidadEsperadaHembrasGuia: this.uniformidadEsperadaHembrasGuia,
      uniformidadEsperadaMachosGuia: this.uniformidadEsperadaMachosGuia
    });
  }

  /**
   * Obtener clase CSS para el estado del indicador
   */
  getEstadoClase(diferencia: number | null | undefined, tipo: string, cumple?: boolean): string {
    return getEstadoClase(diferencia, tipo, cumple);
  }


  /**
   * Obtener información de la guía genética
   */
  get guiaGenetica() {
    const comparacion = this.comparacion();
    if (!comparacion) return null;

    return {
      nombre: comparacion.nombreGuiaGenetica || 'Sin guía genética',
      raza: comparacion.raza,
      anoTabla: comparacion.anoTablaGenetica,
      estadoGeneral: comparacion.estadoGeneral,
      porcentajeCumplimiento: comparacion.porcentajeCumplimiento,
      parametrosEvaluados: comparacion.totalParametrosEvaluados,
      parametrosCumplidos: comparacion.parametrosCumplidos
    };
  }

  /**
   * Formatear fecha para mostrar
   */
  formatDate(date: Date | string | null | undefined): string {
    return formatDate(date);
  }

  /**
   * Obtener mensaje de error amigable
   */
  private getErrorMessage(error: any): string {
    return getErrorMessageLiquidacion(error);
  }

  // ==================== MÉTODOS PARA GRÁFICOS ====================

  /**
   * Datos para gráfico de barras: Indicadores Real vs Guía
   */
  get indicadoresChartData(): ChartData<'bar'> {
    return construirIndicadoresChartData(this.liquidacion());
  }

  /**
   * Datos para gráfico de torta: Distribución de retiros
   */
  get retirosChartData(): ChartData<'pie'> {
    return construirRetirosChartData(this.liquidacion());
  }

  /**
   * Datos para gráfico de líneas: Evolución semanal
   */
  get evolucionChartData(): ChartData<'line'> {
    const liquidacionCompleta = this.liquidacionCompleta();
    if (!liquidacionCompleta?.detallesSeguimiento?.length) {
      return { labels: [], datasets: [] };
    }

    const seguimientos = liquidacionCompleta.detallesSeguimiento
      .sort((a, b) => a.semana - b.semana);

    return {
      labels: seguimientos.map(s => `Sem ${s.semana}`),
      datasets: [
        {
          label: 'Mortalidad Hembras',
          data: seguimientos.map(s => s.mortalidadHembras || 0),
          borderColor: 'rgba(239, 68, 68, 1)',
          backgroundColor: 'rgba(239, 68, 68, 0.1)',
          tension: 0.4,
          fill: false
        },
        {
          label: 'Mortalidad Machos',
          data: seguimientos.map(s => s.mortalidadMachos || 0),
          borderColor: 'rgba(185, 28, 28, 1)',
          backgroundColor: 'rgba(185, 28, 28, 0.1)',
          tension: 0.4,
          fill: false
        },
        {
          label: 'Selección Hembras',
          data: seguimientos.map(s => s.seleccionHembras || 0),
          borderColor: 'rgba(245, 158, 11, 1)',
          backgroundColor: 'rgba(245, 158, 11, 0.1)',
          tension: 0.4,
          fill: false
        },
        {
          label: 'Selección Machos',
          data: seguimientos.map(s => s.seleccionMachos || 0),
          borderColor: 'rgba(217, 119, 6, 1)',
          backgroundColor: 'rgba(217, 119, 6, 0.1)',
          tension: 0.4,
          fill: false
        }
      ]
    };
  }

  /**
   * Datos para gráfico de líneas: Consumo y Peso
   */
  get consumoPesoChartData(): ChartData<'line'> {
    const liquidacionCompleta = this.liquidacionCompleta();
    if (!liquidacionCompleta?.detallesSeguimiento?.length) {
      return { labels: [], datasets: [] };
    }

    const seguimientos = liquidacionCompleta.detallesSeguimiento
      .sort((a, b) => a.semana - b.semana);

    return {
      labels: seguimientos.map(s => `Sem ${s.semana}`),
      datasets: [
        {
          label: 'Consumo Alimento (kg)',
          data: seguimientos.map(s => s.consumoAlimento || 0),
          borderColor: 'rgba(59, 130, 246, 1)',
          backgroundColor: 'rgba(59, 130, 246, 0.1)',
          tension: 0.4,
          fill: false,
          yAxisID: 'y'
        },
        {
          label: 'Peso Hembras (g)',
          data: seguimientos.map(s => (s.pesoPromedioHembras || 0) / 1000), // Convertir a kg para mejor escala
          borderColor: 'rgba(16, 185, 129, 1)',
          backgroundColor: 'rgba(16, 185, 129, 0.1)',
          tension: 0.4,
          fill: false,
          yAxisID: 'y1'
        },
        {
          label: 'Uniformidad Hembras (%)',
          data: seguimientos.map(s => s.uniformidadHembras || 0),
          borderColor: 'rgba(168, 162, 158, 1)',
          backgroundColor: 'rgba(168, 162, 158, 0.1)',
          tension: 0.4,
          fill: false,
          yAxisID: 'y2'
        }
      ]
    };
  }

  /**
   * Opciones específicas para el gráfico de consumo y peso (múltiples ejes Y)
   */
  get consumoPesoChartOptions(): ChartConfiguration['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: 'top',
        },
        title: {
          display: false
        }
      },
      scales: {
        x: {
          display: true,
          grid: {
            display: false
          }
        },
        y: {
          type: 'linear',
          display: true,
          position: 'left',
          title: {
            display: true,
            text: 'Consumo (kg)'
          },
          grid: {
            color: 'rgba(59, 130, 246, 0.1)'
          }
        },
        y1: {
          type: 'linear',
          display: true,
          position: 'right',
          title: {
            display: true,
            text: 'Peso (kg)'
          },
          grid: {
            drawOnChartArea: false,
          },
        },
        y2: {
          type: 'linear',
          display: false,
          min: 0,
          max: 100
        }
      },
      elements: {
        line: {
          tension: 0.4
        },
        point: {
          radius: 4,
          hoverRadius: 6
        }
      }
    };
  }

  // Métodos para comparación con Guía Genética
  cargarComparacion(): void {
    const liquidacionData = this.liquidacion();

    
    
    
    
    

    if (!liquidacionData || !liquidacionData.raza || !liquidacionData.anoTablaGenetica) {
      console.warn('No se puede cargar comparación: faltan datos de raza o año tabla genética');
      console.warn('Raza disponible:', liquidacionData?.raza);
      console.warn('Año disponible:', liquidacionData?.anoTablaGenetica);
      return;
    }

    // Usar semana 25 (175 días) para liquidación técnica, no la edad actual
    const semanaLiquidacion = 25;
    

    // Cargar datos de la guía genética para semana 25
    this.guiaGeneticaService.obtenerGuiaGenetica(
      liquidacionData.raza,
      liquidacionData.anoTablaGenetica,
      semanaLiquidacion
    ).subscribe({
      next: (guiaData) => {
        
        
        

        if (guiaData?.datos) {
          
          
          
          
          

          // Guardar pesos por sexo
          this.pesoEsperadoHembrasGuia = guiaData.datos.pesoHembras || 0;
          this.pesoEsperadoMachosGuia = guiaData.datos.pesoMachos || 0;
          this.pesoEsperadoGuia = (this.pesoEsperadoHembrasGuia + this.pesoEsperadoMachosGuia) / 2;
          this.consumoEsperadoGuia = (guiaData.datos.consumoHembras + guiaData.datos.consumoMachos) / 2;

          // Guardar mortalidades por sexo de la guía
          this.mortalidadEsperadaHembrasGuia = guiaData.datos.mortalidadHembras || 0;
          this.mortalidadEsperadaMachosGuia = guiaData.datos.mortalidadMachos || 0;

          // Promedio general para otros usos
          this.mortalidadEsperadaGuia = (this.mortalidadEsperadaHembrasGuia + this.mortalidadEsperadaMachosGuia) / 2;

          // Uniformidad (única en guía - usaremos el mismo valor para ambos sexos)
          this.uniformidadEsperadaGuia = guiaData.datos.uniformidad || 0;
          this.uniformidadEsperadaHembrasGuia = guiaData.datos.uniformidad || 0;
          this.uniformidadEsperadaMachosGuia = guiaData.datos.uniformidad || 0;

          
          
          
          
          
          

          // Calcular cumplimiento general
          this.calcularCumplimientoGeneral();

          // Marcar que la guía genética está cargada
          this.guiaGeneticaCargada = true;

          // Forzar detección de cambios
          this.cdr.detectChanges();
        } else {
          console.warn('No se recibieron datos de guía genética');
          this.guiaGeneticaCargada = true; // Marcar como cargada aunque no haya datos
        }
      },
      error: (error) => {
        console.error('Error cargando datos de guía genética:', error);
        // Usar valores por defecto
        this.pesoEsperadoGuia = 2500;
        this.consumoEsperadoGuia = 180;
        this.mortalidadEsperadaGuia = 4.0;
        this.mortalidadEsperadaHembrasGuia = 0;
        this.mortalidadEsperadaMachosGuia = 0;
        this.uniformidadEsperadaGuia = 0;
        this.calcularCumplimientoGeneral();
      }
    });
  }

  // Métodos de cálculo para usar en el template
  calcularConsumoPorAve(): number {
    const liquidacionData = this.liquidacion();
    if (!liquidacionData || !liquidacionData.totalAvesEncasetadas) return 0;

    // El backend envía el consumo total del lote, necesitamos dividir por aves vivas
    const avesVivas = this.calcularAvesVivas();
    if (avesVivas === 0) return 0;

    // Conversión: gramos totales / aves vivas = gramos por ave
    return liquidacionData.consumoAlimentoRealGramos / avesVivas;
  }

  calcularAvesVivas(): number {
    const liquidacionData = this.liquidacion();
    if (!liquidacionData || !liquidacionData.totalAvesEncasetadas) return 0;

    // Calcular aves vivas: total inicial - total retiradas
    const retiroTotal = (liquidacionData.porcentajeRetiroTotalGeneral / 100) * liquidacionData.totalAvesEncasetadas;
    return liquidacionData.totalAvesEncasetadas - retiroTotal;
  }

  calcularPesoPromedio(): number {
    const liquidacionData = this.liquidacion();
    if (!liquidacionData) return 0;

    const pesoHembras = liquidacionData.pesoSemana25RealHembras || 0;
    const pesoMachos = liquidacionData.pesoSemana25RealMachos || 0;

    if (pesoHembras > 0 && pesoMachos > 0) {
      return (pesoHembras + pesoMachos) / 2;
    } else if (pesoHembras > 0) {
      return pesoHembras;
    } else if (pesoMachos > 0) {
      return pesoMachos;
    }

    return 0;
  }

  calcularMortalidadPromedio(): number {
    const liquidacionData = this.liquidacion();
    if (!liquidacionData) return 0;

    return (liquidacionData.porcentajeMortalidadHembras + liquidacionData.porcentajeMortalidadMachos) / 2;
  }

  calcularEdadSemanas(fechaEncaset: string | Date): number {
    const fechaInicio = new Date(fechaEncaset);
    const fechaActual = new Date();
    const diferenciaDias = Math.floor((fechaActual.getTime() - fechaInicio.getTime()) / (1000 * 60 * 60 * 24));
    return Math.floor(diferenciaDias / 7);
  }

  /**
   * % de cumplimiento general vs. guía genética (peso, consumo, mortalidad).
   *
   * REQ-010f/REQ-002h: se retiró "Conversión Alimenticia" del cálculo (es KPI de pollo de engorde,
   * no aplica a reproductoras) — el promedio pasa de 4 a 3 parámetros. Cambio de comportamiento
   * INTENCIONAL pedido por el REQ: el % de cumplimiento general ahora puede diferir del valor que
   * mostraba esta pantalla antes del cambio (antes promediaba peso+consumo+mortalidad+conversión).
   */
  calcularCumplimientoGeneral(): void {
    const liquidacionData = this.liquidacion();
    if (!liquidacionData) return;

    let cumplimientos: number[] = [];
    this.parametrosOptimos = 0;

    // Peso promedio
    const pesoReal = this.calcularPesoPromedio();
    const diferenciaPeso = Math.abs(this.pesoEsperadoGuia - pesoReal);
    const porcentajePeso = this.pesoEsperadoGuia > 0 ? (diferenciaPeso / this.pesoEsperadoGuia) * 100 : 100;
    const cumplimientoPeso = Math.max(0, 100 - porcentajePeso);
    cumplimientos.push(cumplimientoPeso);
    if (porcentajePeso <= 5) this.parametrosOptimos++;

    // Consumo
    const diferenciaConsumo = Math.abs(this.consumoEsperadoGuia - liquidacionData.consumoAlimentoRealGramos);
    const porcentajeConsumo = this.consumoEsperadoGuia > 0 ? (diferenciaConsumo / this.consumoEsperadoGuia) * 100 : 100;
    const cumplimientoConsumo = Math.max(0, 100 - porcentajeConsumo);
    cumplimientos.push(cumplimientoConsumo);
    if (porcentajeConsumo <= 10) this.parametrosOptimos++;

    // Mortalidad
    const mortalidadReal = this.calcularMortalidadPromedio();
    const diferenciaMortalidad = Math.abs(this.mortalidadEsperadaGuia - mortalidadReal);
    const porcentajeMortalidad = this.mortalidadEsperadaGuia > 0 ? (diferenciaMortalidad / this.mortalidadEsperadaGuia) * 100 : 100;
    const cumplimientoMortalidad = Math.max(0, 100 - porcentajeMortalidad);
    cumplimientos.push(cumplimientoMortalidad);
    if (porcentajeMortalidad <= 20) this.parametrosOptimos++;

    // Promedio general
    this.porcentajeCumplimientoGeneral = cumplimientos.reduce((sum, val) => sum + val, 0) / cumplimientos.length;
  }

  // Métodos para clases CSS
  getDiferenciaClass(diferencia: number): string {
    const absDiferencia = Math.abs(diferencia);
    if (absDiferencia <= 5) return 'diferencia-optima';
    if (absDiferencia <= 15) return 'diferencia-aceptable';
    return 'diferencia-problema';
  }

  getEstadoClass(tipo: string, esperado: number, real: number): string {
    const diferencia = Math.abs(esperado - real);
    const porcentaje = esperado > 0 ? (diferencia / esperado) * 100 : 100;

    switch (tipo) {
      case 'peso':
        return porcentaje <= 5 ? 'estado-optimo' : porcentaje <= 10 ? 'estado-aceptable' : 'estado-problema';
      case 'consumo':
        return porcentaje <= 10 ? 'estado-optimo' : porcentaje <= 20 ? 'estado-aceptable' : 'estado-problema';
      case 'mortalidad':
        return porcentaje <= 20 ? 'estado-optimo' : porcentaje <= 40 ? 'estado-aceptable' : 'estado-problema';
      default:
        return 'estado-aceptable';
    }
  }

  getEstadoTexto(tipo: string, esperado: number, real: number): string {
    const diferencia = Math.abs(esperado - real);
    const porcentaje = esperado > 0 ? (diferencia / esperado) * 100 : 100;

    switch (tipo) {
      case 'peso':
        if (porcentaje <= 5) return 'Óptimo';
        if (porcentaje <= 10) return 'Aceptable';
        return real < esperado ? 'Bajo' : 'Alto';
      case 'consumo':
        if (porcentaje <= 10) return 'Óptimo';
        if (porcentaje <= 20) return 'Aceptable';
        return real < esperado ? 'Bajo' : 'Alto';
      case 'mortalidad':
        if (porcentaje <= 20) return 'Normal';
        if (porcentaje <= 40) return 'Aceptable';
        return real < esperado ? 'Baja' : 'Alta';
      default:
        return 'Aceptable';
    }
  }

  getCumplimientoClass(): string {
    if (this.porcentajeCumplimientoGeneral >= 90) return 'cumplimiento-excelente';
    if (this.porcentajeCumplimientoGeneral >= 75) return 'cumplimiento-bueno';
    if (this.porcentajeCumplimientoGeneral >= 60) return 'cumplimiento-aceptable';
    return 'cumplimiento-problema';
  }

  calcularEdadDias(fechaEncaset: string | Date | undefined): number {
    if (!fechaEncaset) return 0;
    const fechaInicio = new Date(fechaEncaset);
    const fechaActual = new Date();
    return Math.floor((fechaActual.getTime() - fechaInicio.getTime()) / (1000 * 60 * 60 * 24));
  }

  // Métodos auxiliares para obtener datos del lote - AHORA DESDE LIQUIDACION
  obtenerNombreLote(): string {
    const liquidacion = this.liquidacion();
    return liquidacion?.loteNombre || this.loteNombre || '—';
  }

  obtenerRaza(): string {
    const liquidacion = this.liquidacion();
    return liquidacion?.raza || '—';
  }

  obtenerAnoTablaGenetica(): string {
    const liquidacion = this.liquidacion();
    return liquidacion?.anoTablaGenetica?.toString() || '—';
  }

  obtenerGranja(): string {
    // No disponible en LiquidacionTecnicaDto
    return '—';
  }

  obtenerNucleo(): string {
    // No disponible en LiquidacionTecnicaDto
    return '—';
  }

  obtenerGalpon(): string {
    // No disponible en LiquidacionTecnicaDto
    return '—';
  }

  obtenerFechaEncaset(): string {
    const liquidacion = this.liquidacion();
    if (!liquidacion?.fechaEncaset) return '—';
    return this.formatDate(liquidacion.fechaEncaset);
  }

  obtenerEdadActual(): number {
    const liquidacion = this.liquidacion();
    if (!liquidacion?.fechaEncaset) return 0;
    return this.calcularEdadDias(liquidacion.fechaEncaset);
  }

  obtenerTotalAvesIniciales(): number {
    const liquidacion = this.liquidacion();
    return liquidacion?.totalAvesEncasetadas || 0;
  }

  /**
   * Obtiene la uniformidad real única (promedio de H y M si existen ambas)
   */
  private getUniformidadRealUnica(): number {
    const liq = this.liquidacion();
    if (!liq) return 0;
    const vals: number[] = [];
    if (typeof liq.uniformidadRealHembras === 'number') vals.push(liq.uniformidadRealHembras);
    if (typeof liq.uniformidadRealMachos === 'number') vals.push(liq.uniformidadRealMachos);
    if (vals.length === 0) return 0;
    return vals.reduce((a, b) => a + b, 0) / vals.length;
  }
}

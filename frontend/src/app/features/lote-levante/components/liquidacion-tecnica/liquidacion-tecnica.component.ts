import { Component, Input, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy, signal } from '@angular/core';
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

  // Propiedades para comparación con Guía Genética
  pesoEsperadoGuia: number = 0;
  consumoEsperadoGuia: number = 0;
  mortalidadEsperadaGuia: number = 0;
  conversionEsperadaGuia: number = 0;
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
    private guiaGeneticaService: GuiaGeneticaService
  ) {
    this.form = this.fb.group({
      fechaHasta: [new Date()],
      tipoVista: ['resumen'] // 'resumen' | 'completa'
    });
  }

  ngOnInit(): void {
    // Escuchar cambios en el formulario
    this.form.valueChanges.subscribe(() => {
      if (this.loteId) {
        this.cargarLiquidacion();
      }
    });
    
    // Cargar datos del lote si ya tenemos el ID
    if (this.loteId) {
      this.cargarDatosLote();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['loteId'] && this.loteId) {
      this.cargarDatosLote();
      this.cargarLiquidacion();
    }
  }

  /**
   * Cargar datos completos del lote
   */
  cargarDatosLote(): void {
    if (!this.loteId) return;
    
    console.log('=== DEBUG: cargarDatosLote() ===');
    console.log('LoteId:', this.loteId);
    
    this.liquidacionService.obtenerDatosCompletosLote(this.loteId).subscribe({
      next: (lote: LoteDto) => {
        console.log('✅ Datos del lote cargados:', lote);
        this.datosLote.set(lote);
      },
      error: (error: any) => {
        console.error('❌ Error cargando datos del lote:', error);
        this.error.set('Error al cargar los datos del lote');
      }
    });
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
      request$ = this.liquidacionService.getLiquidacionCompleta(this.loteId, fechaHasta);
    } else {
      request$ = this.liquidacionService.getLiquidacionTecnica(this.loteId, fechaHasta);
    }

    // Cargar liquidación técnica básica
    request$.pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: (data: LiquidacionTecnicaDto | LiquidacionTecnicaCompletaDto) => {
        if (tipoVista === 'completa') {
          const completaData = data as LiquidacionTecnicaCompletaDto;
          this.liquidacionCompleta.set(completaData);
          this.liquidacion.set(completaData.resumen);
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
    const liquidacion = this.liquidacion();
    const comparacion = this.comparacion();
    if (!liquidacion) return [];

    return [
      {
        concepto: 'Mortalidad Hembras',
        real: liquidacion.porcentajeMortalidadHembras,
        guia: comparacion?.mortalidadEsperadaHembras || null,
        diferencia: comparacion?.diferenciaMortalidadHembras || null,
        unidad: '%',
        tipo: 'porcentaje',
        cumple: comparacion?.cumpleMortalidadHembras || false
      },
      {
        concepto: 'Mortalidad Machos',
        real: liquidacion.porcentajeMortalidadMachos,
        guia: comparacion?.mortalidadEsperadaMachos || null,
        diferencia: comparacion?.diferenciaMortalidadMachos || null,
        unidad: '%',
        tipo: 'porcentaje',
        cumple: comparacion?.cumpleMortalidadMachos || false
      },
      {
        concepto: 'Selección Hembras',
        real: liquidacion.porcentajeSeleccionHembras,
        guia: null,
        diferencia: null,
        unidad: '%',
        tipo: 'porcentaje',
        cumple: true // No hay guía para selección
      },
      {
        concepto: 'Selección Machos',
        real: liquidacion.porcentajeSeleccionMachos,
        guia: null,
        diferencia: null,
        unidad: '%',
        tipo: 'porcentaje',
        cumple: true // No hay guía para selección
      },
      {
        concepto: 'Retiro Total Hembras',
        real: liquidacion.porcentajeRetiroTotalHembras,
        guia: liquidacion.porcentajeRetiroGuia,
        diferencia: liquidacion.porcentajeRetiroGuia ? 
          liquidacion.porcentajeRetiroTotalHembras - liquidacion.porcentajeRetiroGuia : null,
        unidad: '%',
        tipo: 'porcentaje',
        cumple: true // Usar lógica existente
      },
      {
        concepto: 'Retiro Total Machos',
        real: liquidacion.porcentajeRetiroTotalMachos,
        guia: liquidacion.porcentajeRetiroGuia,
        diferencia: liquidacion.porcentajeRetiroGuia ? 
          liquidacion.porcentajeRetiroTotalMachos - liquidacion.porcentajeRetiroGuia : null,
        unidad: '%',
        tipo: 'porcentaje',
        cumple: true // Usar lógica existente
      },
      {
        concepto: 'Consumo Alimento',
        real: liquidacion.consumoAlimentoRealGramos,
        guia: comparacion?.consumoAcumuladoEsperadoHembras || liquidacion.consumoAlimentoGuiaGramos,
        diferencia: comparacion?.diferenciaConsumoHembras || liquidacion.porcentajeDiferenciaConsumo,
        unidad: 'gr',
        tipo: 'peso',
        cumple: comparacion?.cumpleConsumoHembras || false
      },
      {
        concepto: 'Peso Semana 25 (Hembras)',
        real: liquidacion.pesoSemana25RealHembras,
        guia: comparacion?.pesoEsperadoHembras || liquidacion.pesoSemana25GuiaHembras,
        diferencia: comparacion?.diferenciaPesoHembras || liquidacion.porcentajeDiferenciaPesoHembras,
        unidad: 'gr',
        tipo: 'peso',
        cumple: comparacion?.cumplePesoHembras || false
      },
      {
        concepto: 'Peso Semana 25 (Machos)',
        real: liquidacion.pesoSemana25RealMachos,
        guia: comparacion?.pesoEsperadoMachos || null,
        diferencia: comparacion?.diferenciaPesoMachos || null,
        unidad: 'gr',
        tipo: 'peso',
        cumple: comparacion?.cumplePesoMachos || false
      },
      {
        concepto: 'Uniformidad (Hembras)',
        real: liquidacion.uniformidadRealHembras,
        guia: comparacion?.uniformidadEsperadaHembras || liquidacion.uniformidadGuiaHembras,
        diferencia: comparacion?.diferenciaUniformidadHembras || liquidacion.porcentajeDiferenciaUniformidadHembras,
        unidad: '%',
        tipo: 'porcentaje',
        cumple: comparacion?.cumpleUniformidadHembras || false
      },
      {
        concepto: 'Uniformidad (Machos)',
        real: liquidacion.uniformidadRealMachos,
        guia: comparacion?.uniformidadEsperadaMachos || null,
        diferencia: comparacion?.diferenciaUniformidadMachos || null,
        unidad: '%',
        tipo: 'porcentaje',
        cumple: comparacion?.cumpleUniformidadMachos || false
      }
    ].filter(ind => ind.real != null);
  }

  /**
   * Obtener clase CSS para el estado del indicador
   */
  getEstadoClase(diferencia: number | null | undefined, tipo: string, cumple?: boolean): string {
    if (cumple !== undefined) {
      return cumple ? 'estado-bueno' : 'estado-critico';
    }
    
    if (diferencia === null || diferencia === undefined) return 'estado-neutral';
    
    const umbral = tipo === 'porcentaje' ? 2 : 5; // 2% para porcentajes, 5% para otros
    
    if (Math.abs(diferencia) <= umbral) return 'estado-bueno';
    if (Math.abs(diferencia) <= umbral * 2) return 'estado-alerta';
    return 'estado-critico';
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
    if (!date) return '—';
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleDateString('es-ES');
  }

  /**
   * Obtener mensaje de error amigable
   */
  private getErrorMessage(error: any): string {
    if (error.status === 404) {
      return 'Lote no encontrado o sin datos para liquidación técnica';
    }
    if (error.status === 400) {
      return 'Parámetros inválidos para el cálculo';
    }
    if (error.status === 500) {
      return 'Error interno del servidor';
    }
    return 'Error desconocido al calcular liquidación técnica';
  }

  // ==================== MÉTODOS PARA GRÁFICOS ====================

  /**
   * Datos para gráfico de barras: Indicadores Real vs Guía
   */
  get indicadoresChartData(): ChartData<'bar'> {
    const liquidacion = this.liquidacion();
    if (!liquidacion) {
      return { labels: [], datasets: [] };
    }

    const indicadores = [
      { label: 'Mort. H (%)', real: liquidacion.porcentajeMortalidadHembras, guia: null },
      { label: 'Mort. M (%)', real: liquidacion.porcentajeMortalidadMachos, guia: null },
      { label: 'Retiro H (%)', real: liquidacion.porcentajeRetiroTotalHembras, guia: liquidacion.porcentajeRetiroGuia },
      { label: 'Retiro M (%)', real: liquidacion.porcentajeRetiroTotalMachos, guia: liquidacion.porcentajeRetiroGuia },
      { label: 'Consumo (g)', real: liquidacion.consumoAlimentoRealGramos / 1000, guia: liquidacion.consumoAlimentoGuiaGramos ? liquidacion.consumoAlimentoGuiaGramos / 1000 : null },
      { label: 'Peso H (g)', real: liquidacion.pesoSemana25RealHembras ? liquidacion.pesoSemana25RealHembras / 1000 : null, guia: liquidacion.pesoSemana25GuiaHembras ? liquidacion.pesoSemana25GuiaHembras / 1000 : null },
      { label: 'Unif. H (%)', real: liquidacion.uniformidadRealHembras, guia: liquidacion.uniformidadGuiaHembras }
    ].filter(ind => ind.real != null);

    return {
      labels: indicadores.map(ind => ind.label),
      datasets: [
        {
          label: 'Real',
          data: indicadores.map(ind => ind.real || 0),
          backgroundColor: 'rgba(211, 47, 47, 0.8)',
          borderColor: 'rgba(211, 47, 47, 1)',
          borderWidth: 1
        },
        {
          label: 'Guía',
          data: indicadores.map(ind => ind.guia || 0),
          backgroundColor: 'rgba(120, 113, 108, 0.8)',
          borderColor: 'rgba(120, 113, 108, 1)',
          borderWidth: 1
        }
      ]
    };
  }

  /**
   * Datos para gráfico de torta: Distribución de retiros
   */
  get retirosChartData(): ChartData<'pie'> {
    const liquidacion = this.liquidacion();
    if (!liquidacion) {
      return { labels: [], datasets: [] };
    }

    const totalHembras = liquidacion.hembrasEncasetadas;
    const totalMachos = liquidacion.machosEncasetados;
    const total = totalHembras + totalMachos;

    const mortHembras = (liquidacion.porcentajeMortalidadHembras / 100) * totalHembras;
    const mortMachos = (liquidacion.porcentajeMortalidadMachos / 100) * totalMachos;
    const selHembras = (liquidacion.porcentajeSeleccionHembras / 100) * totalHembras;
    const selMachos = (liquidacion.porcentajeSeleccionMachos / 100) * totalMachos;
    const errHembras = (liquidacion.porcentajeErrorSexajeHembras / 100) * totalHembras;
    const errMachos = (liquidacion.porcentajeErrorSexajeMachos / 100) * totalMachos;
    const vivas = total - (mortHembras + mortMachos + selHembras + selMachos + errHembras + errMachos);

    return {
      labels: ['Vivas', 'Mort. Hembras', 'Mort. Machos', 'Sel. Hembras', 'Sel. Machos', 'Error Sexaje'],
      datasets: [{
        data: [vivas, mortHembras, mortMachos, selHembras, selMachos, errHembras + errMachos],
        backgroundColor: [
          'rgba(16, 185, 129, 0.8)', // Verde - Vivas
          'rgba(239, 68, 68, 0.8)',  // Rojo - Mort Hembras
          'rgba(185, 28, 28, 0.8)',  // Rojo oscuro - Mort Machos
          'rgba(245, 158, 11, 0.8)', // Amarillo - Sel Hembras
          'rgba(217, 119, 6, 0.8)',  // Amarillo oscuro - Sel Machos
          'rgba(107, 114, 128, 0.8)' // Gris - Error Sexaje
        ],
        borderColor: [
          'rgba(16, 185, 129, 1)',
          'rgba(239, 68, 68, 1)',
          'rgba(185, 28, 28, 1)',
          'rgba(245, 158, 11, 1)',
          'rgba(217, 119, 6, 1)',
          'rgba(107, 114, 128, 1)'
        ],
        borderWidth: 2
      }]
    };
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
    
    console.log('=== DEBUG: cargarComparacion() ===');
    console.log('LiquidacionData:', liquidacionData);
    console.log('Raza:', liquidacionData?.raza);
    console.log('Año Tabla Genética:', liquidacionData?.anoTablaGenetica);
    console.log('Fecha Encaset:', liquidacionData?.fechaEncaset);
    
    if (!liquidacionData || !liquidacionData.raza || !liquidacionData.anoTablaGenetica) {
      console.warn('No se puede cargar comparación: faltan datos de raza o año tabla genética');
      console.warn('Raza disponible:', liquidacionData?.raza);
      console.warn('Año disponible:', liquidacionData?.anoTablaGenetica);
      return;
    }

    // Calcular edad en semanas
    const edadSemanas = this.calcularEdadSemanas(liquidacionData.fechaEncaset || new Date());
    console.log('Edad en semanas calculada:', edadSemanas);
    
    // Cargar datos de la guía genética
    this.guiaGeneticaService.obtenerGuiaGenetica(
      liquidacionData.raza,
      liquidacionData.anoTablaGenetica,
      edadSemanas
    ).subscribe({
      next: (guiaData) => {
        if (guiaData?.datos) {
          this.pesoEsperadoGuia = (guiaData.datos.pesoHembras + guiaData.datos.pesoMachos) / 2;
          this.consumoEsperadoGuia = (guiaData.datos.consumoHembras + guiaData.datos.consumoMachos) / 2;
          this.mortalidadEsperadaGuia = (guiaData.datos.mortalidadHembras + guiaData.datos.mortalidadMachos) / 2;
          this.conversionEsperadaGuia = this.calcularConversionEsperada();
          
          // Calcular cumplimiento general
          this.calcularCumplimientoGeneral();
        }
      },
      error: (error) => {
        console.error('Error cargando datos de guía genética:', error);
        // Usar valores por defecto
        this.pesoEsperadoGuia = 2500;
        this.consumoEsperadoGuia = 180;
        this.mortalidadEsperadaGuia = 4.0;
        this.conversionEsperadaGuia = 1.8;
        this.calcularCumplimientoGeneral();
      }
    });
  }

  // Métodos de cálculo para usar en el template
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

  calcularConversionAlimenticia(): number {
    const liquidacionData = this.liquidacion();
    if (!liquidacionData) return 0;
    
    const pesoPromedio = this.calcularPesoPromedio();
    const consumoTotal = liquidacionData.consumoAlimentoRealGramos;
    
    if (pesoPromedio > 0 && consumoTotal > 0) {
      return consumoTotal / pesoPromedio;
    }
    
    return 0;
  }

  calcularEdadSemanas(fechaEncaset: string | Date): number {
    const fechaInicio = new Date(fechaEncaset);
    const fechaActual = new Date();
    const diferenciaDias = Math.floor((fechaActual.getTime() - fechaInicio.getTime()) / (1000 * 60 * 60 * 24));
    return Math.floor(diferenciaDias / 7);
  }

  calcularConversionEsperada(): number {
    // Conversión esperada basada en peso y consumo
    if (this.pesoEsperadoGuia > 0 && this.consumoEsperadoGuia > 0) {
      return this.consumoEsperadoGuia / this.pesoEsperadoGuia;
    }
    return 1.8; // Valor por defecto
  }

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

    // Conversión alimenticia
    const conversionReal = this.calcularConversionAlimenticia();
    const diferenciaConversion = Math.abs(this.conversionEsperadaGuia - conversionReal);
    const porcentajeConversion = this.conversionEsperadaGuia > 0 ? (diferenciaConversion / this.conversionEsperadaGuia) * 100 : 100;
    const cumplimientoConversion = Math.max(0, 100 - porcentajeConversion);
    cumplimientos.push(cumplimientoConversion);
    if (porcentajeConversion <= 10) this.parametrosOptimos++;

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
      case 'conversion':
        return porcentaje <= 10 ? 'estado-optimo' : porcentaje <= 20 ? 'estado-aceptable' : 'estado-problema';
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
      case 'conversion':
        if (porcentaje <= 10) return 'Óptima';
        if (porcentaje <= 20) return 'Aceptable';
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

  // Métodos auxiliares para obtener datos del lote
  obtenerNombreLote(): string {
    const lote = this.datosLote();
    return lote?.loteNombre || this.loteNombre || '—';
  }

  obtenerRaza(): string {
    const lote = this.datosLote();
    return lote?.raza || '—';
  }

  obtenerAnoTablaGenetica(): string {
    const lote = this.datosLote();
    return lote?.anoTablaGenetica?.toString() || '—';
  }

  obtenerGranja(): string {
    const lote = this.datosLote();
    return lote?.farm?.name || '—';
  }

  obtenerNucleo(): string {
    const lote = this.datosLote();
    return lote?.nucleo?.nucleoNombre || '—';
  }

  obtenerGalpon(): string {
    const lote = this.datosLote();
    return lote?.galpon?.galponNombre || '—';
  }

  obtenerFechaEncaset(): string {
    const lote = this.datosLote();
    if (!lote?.fechaEncaset) return '—';
    return this.formatDate(lote.fechaEncaset);
  }

  obtenerEdadActual(): number {
    const lote = this.datosLote();
    if (!lote?.fechaEncaset) return 0;
    return this.calcularEdadDias(lote.fechaEncaset);
  }

  obtenerTotalAvesIniciales(): number {
    const lote = this.datosLote();
    return (lote?.hembrasL || 0) + (lote?.machosL || 0);
  }
}

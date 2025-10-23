import { Component, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto } from '../../services/produccion.service';

export interface AnalisisData {
  totalRegistros: number;
  totalHuevosTotales: number;
  totalMortalidad: number;
  eficienciaPromedio: number;
  resumenPorPeriodo: Array<{
    periodo: string;
    huevosTotales: number;
    huevosIncubables: number;
    eficiencia: number;
    mortalidad: number;
  }>;
  correlaciones: {
    mortalidadProduccion: string;
    consumoProduccion: string;
    pesoProduccion: string;
  };
  comparaciones: Array<{
    metrica: string;
    actual: number;
    anterior: number;
    cambio: number;
    tendencia: 'up' | 'down' | 'stable';
  }>;
  benchmark: {
    eficiencia: number;
    produccion: number;
  };
  recomendaciones: Array<{
    tipo: 'warning' | 'success' | 'info';
    texto: string;
    prioridad: string;
  }>;
}

@Component({
  selector: 'app-modal-analisis',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './modal-analisis.component.html',
  styleUrls: ['./modal-analisis.component.scss']
})
export class ModalAnalisisComponent implements OnInit {
  @Input() isOpen: boolean = false;
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() loading: boolean = false;
  
  @Output() close = new EventEmitter<void>();
  @Output() export = new EventEmitter<void>();

  analisisData: AnalisisData = {
    totalRegistros: 0,
    totalHuevosTotales: 0,
    totalMortalidad: 0,
    eficienciaPromedio: 0,
    resumenPorPeriodo: [],
    correlaciones: {
      mortalidadProduccion: 'N/A',
      consumoProduccion: 'N/A',
      pesoProduccion: 'N/A'
    },
    comparaciones: [],
    benchmark: {
      eficiencia: 0,
      produccion: 0
    },
    recomendaciones: []
  };

  constructor() { }

  ngOnInit(): void {
    if (this.seguimientos.length > 0) {
      this.calcularAnalisis();
    }
  }

  ngOnChanges(): void {
    if (this.seguimientos.length > 0) {
      this.calcularAnalisis();
    }
  }

  // ================== EVENTOS ==================
  onClose(): void {
    this.close.emit();
  }

  exportarAnalisis(): void {
    this.export.emit();
  }

  // ================== ANÁLISIS ==================
  private calcularAnalisis(): void {
    if (this.seguimientos.length === 0) return;

    // Métricas básicas
    this.analisisData.totalRegistros = this.seguimientos.length;
    this.analisisData.totalHuevosTotales = this.seguimientos.reduce((sum, s) => sum + s.huevosTotales, 0);
    this.analisisData.totalMortalidad = this.seguimientos.reduce((sum, s) => sum + s.mortalidadH + s.mortalidadM, 0);

    // Eficiencia promedio
    const totalHuevosIncubables = this.seguimientos.reduce((sum, s) => sum + s.huevosIncubables, 0);
    this.analisisData.eficienciaPromedio = this.analisisData.totalHuevosTotales > 0 
      ? Math.round((totalHuevosIncubables / this.analisisData.totalHuevosTotales) * 100)
      : 0;

    // Resumen por período (semanal)
    this.calcularResumenPorPeriodo();

    // Correlaciones
    this.calcularCorrelaciones();

    // Comparaciones
    this.calcularComparaciones();

    // Benchmark
    this.calcularBenchmark();

    // Recomendaciones
    this.generarRecomendaciones();
  }

  private calcularResumenPorPeriodo(): void {
    // Agrupar por semanas
    const semanas = new Map<string, SeguimientoItemDto[]>();
    
    this.seguimientos.forEach(s => {
      const fecha = new Date(s.fechaRegistro);
      const semana = this.getSemana(fecha);
      if (!semanas.has(semana)) {
        semanas.set(semana, []);
      }
      semanas.get(semana)!.push(s);
    });

    this.analisisData.resumenPorPeriodo = Array.from(semanas.entries()).map(([semana, registros]) => {
      const huevosTotales = registros.reduce((sum, r) => sum + r.huevosTotales, 0);
      const huevosIncubables = registros.reduce((sum, r) => sum + r.huevosIncubables, 0);
      const mortalidad = registros.reduce((sum, r) => sum + r.mortalidadH + r.mortalidadM, 0);
      const eficiencia = huevosTotales > 0 ? Math.round((huevosIncubables / huevosTotales) * 100) : 0;

      return {
        periodo: semana,
        huevosTotales,
        huevosIncubables,
        eficiencia,
        mortalidad
      };
    }).sort((a, b) => a.periodo.localeCompare(b.periodo));
  }

  private calcularCorrelaciones(): void {
    // Simulación de correlaciones (en un caso real se calcularían estadísticamente)
    const mortalidadPromedio = this.seguimientos.reduce((sum, s) => sum + s.mortalidadH + s.mortalidadM, 0) / this.seguimientos.length;
    const produccionPromedio = this.seguimientos.reduce((sum, s) => sum + s.huevosTotales, 0) / this.seguimientos.length;

    this.analisisData.correlaciones = {
      mortalidadProduccion: mortalidadPromedio > 5 ? 'Alta (-0.7)' : 'Baja (-0.2)',
      consumoProduccion: 'Media (+0.4)',
      pesoProduccion: 'Alta (+0.8)'
    };
  }

  private calcularComparaciones(): void {
    // Simulación de comparaciones con períodos anteriores
    const eficienciaActual = this.analisisData.eficienciaPromedio;
    const produccionActual = this.analisisData.totalHuevosTotales;

    this.analisisData.comparaciones = [
      {
        metrica: 'Eficiencia de Producción',
        actual: eficienciaActual,
        anterior: eficienciaActual - 5,
        cambio: 5,
        tendencia: 'up' as const
      },
      {
        metrica: 'Huevos Totales',
        actual: produccionActual,
        anterior: produccionActual - 100,
        cambio: 8,
        tendencia: 'up' as const
      },
      {
        metrica: 'Mortalidad',
        actual: this.analisisData.totalMortalidad,
        anterior: this.analisisData.totalMortalidad + 2,
        cambio: -10,
        tendencia: 'down' as const
      }
    ];
  }

  private calcularBenchmark(): void {
    // Simulación de benchmarking
    this.analisisData.benchmark = {
      eficiencia: Math.min(this.analisisData.eficienciaPromedio, 100),
      produccion: Math.min((this.analisisData.totalHuevosTotales / 1000) * 100, 100)
    };
  }

  private generarRecomendaciones(): void {
    const recomendaciones = [];

    // Recomendación basada en mortalidad
    if (this.analisisData.totalMortalidad > this.seguimientos.length * 2) {
      recomendaciones.push({
        tipo: 'warning' as const,
        texto: 'La mortalidad está por encima del promedio esperado. Revisar condiciones ambientales.',
        prioridad: 'Alta'
      });
    }

    // Recomendación basada en eficiencia
    if (this.analisisData.eficienciaPromedio < 80) {
      recomendaciones.push({
        tipo: 'warning' as const,
        texto: 'La eficiencia de producción está por debajo del objetivo. Optimizar manejo de huevos.',
        prioridad: 'Media'
      });
    } else {
      recomendaciones.push({
        tipo: 'success' as const,
        texto: 'Excelente eficiencia de producción. Mantener las prácticas actuales.',
        prioridad: 'Baja'
      });
    }

    // Recomendación general
    recomendaciones.push({
      tipo: 'info' as const,
      texto: 'Considerar implementar análisis de tendencias más detallado para optimizar la producción.',
      prioridad: 'Baja'
    });

    this.analisisData.recomendaciones = recomendaciones;
  }

  // ================== HELPERS ==================
  private getSemana(fecha: Date): string {
    const inicioSemana = new Date(fecha);
    inicioSemana.setDate(fecha.getDate() - fecha.getDay());
    const finSemana = new Date(inicioSemana);
    finSemana.setDate(inicioSemana.getDate() + 6);
    
    return `${this.formatearFecha(inicioSemana)} - ${this.formatearFecha(finSemana)}`;
  }

  private formatearFecha(fecha: Date): string {
    return fecha.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit' });
  }
}

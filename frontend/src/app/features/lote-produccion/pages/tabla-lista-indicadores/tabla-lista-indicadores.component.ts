import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto } from '../../services/produccion.service';

interface IndicadorSemanal {
  semana: number;
  fechaInicio: string;
  fechaFin: string;
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
}

@Component({
  selector: 'app-tabla-lista-indicadores',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-lista-indicadores.component.html',
  styleUrls: ['./tabla-lista-indicadores.component.scss']
})
export class TablaListaIndicadoresComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() selectedLote: any = null;
  @Input() loading: boolean = false;

  // Datos calculados
  indicadoresSemanales: IndicadorSemanal[] = [];

  constructor() { }

  ngOnInit(): void {
    this.calcularIndicadores();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      this.calcularIndicadores();
    }
  }

  // ================== CÁLCULOS DE INDICADORES ==================
  private calcularIndicadores(): void {
    if (!this.seguimientos || this.seguimientos.length === 0 || !this.selectedLote) {
      this.indicadoresSemanales = [];
      return;
    }

    // Agrupar registros por semana
    const registrosPorSemana = this.agruparPorSemana(this.seguimientos);
    
    // Calcular indicadores para cada semana
    this.indicadoresSemanales = this.calcularIndicadoresSemanales(registrosPorSemana);
  }

  private agruparPorSemana(registros: SeguimientoItemDto[]): Map<number, SeguimientoItemDto[]> {
    const grupos = new Map<number, SeguimientoItemDto[]>();
    
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
    if (!this.selectedLote?.fechaInicio) return 1;
    
    const fechaInicio = new Date(this.selectedLote.fechaInicio);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaInicio.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    return Math.max(1, Math.ceil(diffDays / 7));
  }

  private calcularIndicadoresSemanales(grupos: Map<number, SeguimientoItemDto[]>): IndicadorSemanal[] {
    const indicadores: IndicadorSemanal[] = [];
    const semanas = Array.from(grupos.keys()).sort((a, b) => a - b);
    
    let avesAcumuladas = this.selectedLote?.avesInicialesH + this.selectedLote?.avesInicialesM || 0;
    let mortalidadAcumulada = 0;
    let huevosTotalesAcumulados = 0;
    let huevosIncubablesAcumulados = 0;

    semanas.forEach(semana => {
      const registros = grupos.get(semana) || [];
      const indicador = this.calcularIndicadorSemana(semana, registros, avesAcumuladas, mortalidadAcumulada, huevosTotalesAcumulados, huevosIncubablesAcumulados);
      
      indicadores.push(indicador);
      
      // Actualizar acumulados para la siguiente semana
      avesAcumuladas = indicador.avesFinSemana;
      mortalidadAcumulada += indicador.mortalidadTotal;
      huevosTotalesAcumulados += indicador.huevosTotales;
      huevosIncubablesAcumulados += indicador.huevosIncubables;
    });

    return indicadores;
  }

  private calcularIndicadorSemana(
    semana: number, 
    registros: SeguimientoItemDto[], 
    avesInicio: number,
    mortalidadAcum: number,
    huevosTotalesAcum: number,
    huevosIncubablesAcum: number
  ): IndicadorSemanal {
    // Calcular totales de la semana
    const mortalidadHembrasTotal = registros.reduce((sum, r) => sum + (r.mortalidadH || 0), 0);
    const mortalidadMachosTotal = registros.reduce((sum, r) => sum + (r.mortalidadM || 0), 0);
    const mortalidadTotal = mortalidadHembrasTotal + mortalidadMachosTotal;
    const consumoTotal = registros.reduce((sum, r) => sum + (r.consumoKg || 0), 0);
    const huevosTotales = registros.reduce((sum, r) => sum + (r.huevosTotales || 0), 0);
    const huevosIncubables = registros.reduce((sum, r) => sum + (r.huevosIncubables || 0), 0);
    
    // Aves al final de la semana
    const avesFin = avesInicio - mortalidadTotal;
    
    // Consumo real en kg
    const consumoReal = consumoTotal;
    
    // Consumo tabla (valor fijo por ahora, debería venir de la tabla genética)
    const consumoTabla = 157; // kg por semana
    
    // Conversión alimenticia
    const conversionAlimenticia = avesFin > 0 ? consumoReal / avesFin : 0;
    
    // Porcentajes de mortalidad
    const mortalidadHembras = avesInicio > 0 ? (mortalidadHembrasTotal / avesInicio) * 100 : 0;
    const mortalidadMachos = avesInicio > 0 ? (mortalidadMachosTotal / avesInicio) * 100 : 0;
    const mortalidadTotalPorcentaje = mortalidadHembras + mortalidadMachos;
    
    // Eficiencia (simplificada para producción)
    const eficiencia = conversionAlimenticia > 0 ? huevosTotales / conversionAlimenticia : 0;
    
    // IP (Índice de Productividad) - simplificado para producción
    const ip = conversionAlimenticia > 0 ? (huevosTotales / conversionAlimenticia) / 10 : 0;
    
    // VPI (Variación de Producción) - simplificado
    const vpi = huevosTotalesAcum > 0 ? huevosTotales / huevosTotalesAcum : 0;
    
    // Acumulados
    const mortalidadAcumTotal = mortalidadAcum + mortalidadTotalPorcentaje;
    const huevosTotalesAcumTotal = huevosTotalesAcum + huevosTotales;
    const huevosIncubablesAcumTotal = huevosIncubablesAcum + huevosIncubables;

    return {
      semana,
      fechaInicio: this.obtenerFechaInicioSemana(semana),
      fechaFin: this.obtenerFechaFinSemana(semana),
      avesInicioSemana: avesInicio,
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
      mortalidadAcum: mortalidadAcumTotal,
      huevosTotalesAcum: huevosTotalesAcumTotal,
      huevosIncubablesAcum: huevosIncubablesAcumTotal
    };
  }

  // ================== HELPERS DE FECHA ==================
  private obtenerFechaInicioSemana(semana: number): string {
    if (!this.selectedLote?.fechaInicio) return '';
    
    const fechaInicio = new Date(this.selectedLote.fechaInicio);
    const diasASumar = (semana - 1) * 7;
    const fechaInicioSemana = new Date(fechaInicio.getTime() + (diasASumar * 24 * 60 * 60 * 1000));
    
    return fechaInicioSemana.toISOString().split('T')[0];
  }

  private obtenerFechaFinSemana(semana: number): string {
    if (!this.selectedLote?.fechaInicio) return '';
    
    const fechaInicio = new Date(this.selectedLote.fechaInicio);
    const diasASumar = (semana * 7) - 1;
    const fechaFinSemana = new Date(fechaInicio.getTime() + (diasASumar * 24 * 60 * 60 * 1000));
    
    return fechaFinSemana.toISOString().split('T')[0];
  }

  // ================== FORMATO ==================
  formatNumber = (value: number, decimals: number = 2): string => {
    return value.toFixed(decimals);
  };

  formatPercentage = (value: number, decimals: number = 2): string => {
    return `${value.toFixed(decimals)}%`;
  };

  formatDate = (date: string): string => {
    return new Date(date).toLocaleDateString('es-ES');
  };
}

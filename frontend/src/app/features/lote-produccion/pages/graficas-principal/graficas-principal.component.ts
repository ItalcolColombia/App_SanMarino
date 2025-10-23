import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto } from '../../services/produccion.service';

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
  imports: [CommonModule],
  templateUrl: './graficas-principal.component.html',
  styleUrls: ['./graficas-principal.component.scss']
})
export class GraficasPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() selectedLote: any = null;
  @Input() loading: boolean = false;

  // Datos para gráficas
  seriesGraficas: SerieGrafica[] = [];
  indicadoresSemanales: any[] = [];

  constructor() { }

  ngOnInit(): void {
    this.prepararDatosGraficas();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      this.prepararDatosGraficas();
    }
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
    
    // Debug: verificar datos
    console.log('Datos para gráficas:', {
      seguimientos: this.seguimientos.length,
      lote: this.selectedLote?.loteNombre,
      indicadores: this.indicadoresSemanales.length,
      primerIndicador: this.indicadoresSemanales[0]
    });
    
    // Preparar series de datos para gráficas
    this.seriesGraficas = this.prepararSeriesGraficas();
    
    console.log('Series preparadas:', this.seriesGraficas.length);
  }

  private calcularIndicadoresSemanales(): any[] {
    // Agrupar registros por semana
    const registrosPorSemana = this.agruparPorSemana(this.seguimientos);
    
    // Calcular indicadores para cada semana
    return this.calcularIndicadoresSemanalesFromGrupos(registrosPorSemana);
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

  private calcularIndicadoresSemanalesFromGrupos(grupos: Map<number, SeguimientoItemDto[]>): any[] {
    const indicadores: any[] = [];
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
  ): any {
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

    return {
      semana,
      fechaInicio: this.obtenerFechaInicioSemana(semana),
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
      vpi
    };
  }

  private prepararSeriesGraficas(): SerieGrafica[] {
    if (this.indicadoresSemanales.length === 0) return [];

    return [
      {
        nombre: 'Consumo Real (kg)',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.consumoReal,
          etiqueta: `Semana ${ind.semana}: ${ind.consumoReal.toFixed(2)}kg`
        })),
        color: '#d32f2f',
        tipo: 'barra'
      },
      {
        nombre: 'Consumo Tabla (kg)',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.consumoTabla,
          etiqueta: `Semana ${ind.semana}: ${ind.consumoTabla.toFixed(2)}kg`
        })),
        color: '#1976d2',
        tipo: 'barra'
      },
      {
        nombre: 'Huevos Totales',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.huevosTotales,
          etiqueta: `Semana ${ind.semana}: ${ind.huevosTotales} huevos`
        })),
        color: '#388e3c',
        tipo: 'barra'
      },
      {
        nombre: 'Huevos Incubables',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.huevosIncubables,
          etiqueta: `Semana ${ind.semana}: ${ind.huevosIncubables} huevos`
        })),
        color: '#4caf50',
        tipo: 'barra'
      },
      {
        nombre: 'Mortalidad Hembras (%)',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.mortalidadHembras,
          etiqueta: `Semana ${ind.semana}: ${ind.mortalidadHembras.toFixed(2)}%`
        })),
        color: '#f57c00',
        tipo: 'barra'
      },
      {
        nombre: 'Mortalidad Machos (%)',
        datos: this.indicadoresSemanales.map(ind => ({
          semana: ind.semana,
          fecha: ind.fechaInicio,
          valor: ind.mortalidadMachos,
          etiqueta: `Semana ${ind.semana}: ${ind.mortalidadMachos.toFixed(2)}%`
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
    if (!this.selectedLote?.fechaInicio) return '';
    
    const fechaInicio = new Date(this.selectedLote.fechaInicio);
    const diasASumar = (semana - 1) * 7;
    const fechaInicioSemana = new Date(fechaInicio.getTime() + (diasASumar * 24 * 60 * 60 * 1000));
    
    return fechaInicioSemana.toISOString().split('T')[0];
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

  calcularPromedioIndicadores(propiedad: string): number {
    if (this.indicadoresSemanales.length === 0) return 0;
    const suma = this.indicadoresSemanales.reduce((acc, ind) => acc + (ind[propiedad] || 0), 0);
    return suma / this.indicadoresSemanales.length;
  }

  calcularTotalIndicadores(propiedad: string): number {
    if (this.indicadoresSemanales.length === 0) return 0;
    return this.indicadoresSemanales.reduce((acc, ind) => acc + (ind[propiedad] || 0), 0);
  }

  getMejorSemanaConversion(): string {
    if (this.indicadoresSemanales.length === 0) return 'N/A';
    
    const mejorSemana = this.indicadoresSemanales.reduce((mejor, actual) => 
      actual.conversionAlimenticia < mejor.conversionAlimenticia ? actual : mejor
    );
    
    return `Semana ${mejorSemana.semana} (${mejorSemana.conversionAlimenticia.toFixed(2)})`;
  }
}

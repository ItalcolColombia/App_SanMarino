import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
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
  imports: [CommonModule],
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

    semanas.forEach(semana => {
      const registros = grupos.get(semana) || [];
      const indicador = this.calcularIndicadorSemana(semana, registros, avesAcumuladas, mortalidadAcumulada, seleccionAcumulada, pesoAnterior);
      
      indicadores.push(indicador);
      
      // Actualizar acumulados para la siguiente semana
      avesAcumuladas = indicador.avesFinSemana;
      mortalidadAcumulada += indicador.mortalidadSem;
      seleccionAcumulada += indicador.seleccionSem;
      pesoAnterior = indicador.pesoCierre;
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
    const consumoTotal = registros.reduce((sum, r) => sum + (r.consumoKgHembras || 0) + (r.consumoKgMachos || 0), 0);
    
    // Aves al final de la semana
    const avesFin = avesInicio - mortalidadTotal - seleccionTotal;
    
    // Peso promedio de la semana (usar el último registro de la semana)
    const ultimoRegistro = registros[registros.length - 1];
    const pesoPromedio = ((ultimoRegistro?.pesoPromH || 0) + (ultimoRegistro?.pesoPromM || 0)) / 2;
    
    // Consumo real en gramos (convertir de kg a gramos)
    const consumoReal = consumoTotal * 1000;
    
    // Consumo tabla (valor fijo por ahora, debería venir de la tabla genética)
    const consumoTabla = 157;
    
    // Conversión alimenticia
    const conversionAlimenticia = avesFin > 0 ? consumoReal / avesFin : 0;
    
    // Porcentajes
    const mortalidadSem = avesInicio > 0 ? (mortalidadTotal / avesInicio) * 100 : 0;
    const seleccionSem = avesInicio > 0 ? (seleccionTotal / avesInicio) * 100 : 0;
    
    // Eficiencia
    const eficiencia = conversionAlimenticia > 0 ? pesoPromedio / conversionAlimenticia / 10 : 0;
    
    // IP (Índice de Productividad)
    const ip = conversionAlimenticia > 0 ? ((pesoPromedio / conversionAlimenticia) / 10) / conversionAlimenticia : 0;

    return {
      semana,
      fechaInicio: this.obtenerFechaInicioSemana(semana),
      avesInicioSemana: avesInicio,
      avesFinSemana: avesFin,
      consumoReal,
      consumoTabla,
      conversionAlimenticia,
      mortalidadSem,
      seleccionSem,
      eficiencia,
      ip,
      pesoCierre: pesoPromedio,
      pesoInicial: pesoAnterior
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
}

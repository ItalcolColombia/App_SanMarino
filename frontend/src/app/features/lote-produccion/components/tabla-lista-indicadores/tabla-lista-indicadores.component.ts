import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoteProduccionDto } from '../../services/lote-produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';

@Component({
  selector: 'app-tabla-lista-indicadores',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-lista-indicadores.component.html',
  styleUrls: ['./tabla-lista-indicadores.component.scss']
})
export class TablaListaIndicadoresComponent implements OnInit, OnChanges {
  @Input() registros: LoteProduccionDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() loading: boolean = false;

  indicadoresSemanales: any[] = [];

  constructor() { }

  ngOnInit(): void {
    this.calcularIndicadoresSemanales();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['registros'] || changes['selectedLote']) {
      this.calcularIndicadoresSemanales();
    }
  }

  calcularIndicadoresSemanales(): void {
    if (!this.registros.length || !this.selectedLote) {
      this.indicadoresSemanales = [];
      return;
    }

    // Agrupar registros por semana
    const registrosPorSemana = new Map<number, LoteProduccionDto[]>();
    
    this.registros.forEach(registro => {
      const edadDias = this.calcularEdadDias(registro.fecha);
      const semana = Math.ceil(edadDias / 7);
      
      if (!registrosPorSemana.has(semana)) {
        registrosPorSemana.set(semana, []);
      }
      registrosPorSemana.get(semana)!.push(registro);
    });

    // Calcular indicadores por semana
    this.indicadoresSemanales = Array.from(registrosPorSemana.entries())
      .map(([semana, registrosSemana]) => {
        const totalRegistros = registrosSemana.length;
        
        // Sumar valores de la semana
        const totalMortalidadH = registrosSemana.reduce((sum, r) => sum + r.mortalidadH, 0);
        const totalMortalidadM = registrosSemana.reduce((sum, r) => sum + r.mortalidadM, 0);
        const totalSelH = registrosSemana.reduce((sum, r) => sum + r.selH, 0);
        const totalConsKgH = registrosSemana.reduce((sum, r) => sum + r.consKgH, 0);
        const totalConsKgM = registrosSemana.reduce((sum, r) => sum + r.consKgM, 0);
        const totalHuevoTot = registrosSemana.reduce((sum, r) => sum + r.huevoTot, 0);
        const totalHuevoInc = registrosSemana.reduce((sum, r) => sum + r.huevoInc, 0);
        const pesoPromedioHuevo = registrosSemana.reduce((sum, r) => sum + r.pesoHuevo, 0) / totalRegistros;

        // Calcular porcentajes
        const porcentajeIncubable = totalHuevoTot > 0 ? (totalHuevoInc / totalHuevoTot) * 100 : 0;
        const mortalidadTotal = totalMortalidadH + totalMortalidadM;
        const mortalidadPorcentaje = this.calcularMortalidadPorcentaje(mortalidadTotal, semana);

        return {
          semana,
          edadInicio: (semana - 1) * 7 + 1,
          edadFin: semana * 7,
          totalRegistros,
          mortalidadH: totalMortalidadH,
          mortalidadM: totalMortalidadM,
          mortalidadTotal,
          mortalidadPorcentaje,
          selH: totalSelH,
          consKgH: totalConsKgH,
          consKgM: totalConsKgM,
          huevoTot: totalHuevoTot,
          huevoInc: totalHuevoInc,
          porcentajeIncubable,
          pesoPromedioHuevo,
          etapa: this.determinarEtapa(semana)
        };
      })
      .sort((a, b) => a.semana - b.semana);
  }

  calcularEdadDias(fechaRegistro: string | Date): number {
    if (!this.selectedLote?.fechaEncaset) return 0;
    
    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaEncaset.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    return Math.max(1, diffDays);
  }

  calcularMortalidadPorcentaje(mortalidadTotal: number, semana: number): number {
    // Estimación basada en población inicial y semana
    if (!this.selectedLote) return 0;
    
    const poblacionInicial = (this.selectedLote.hembrasL || 0) + (this.selectedLote.machosL || 0);
    if (poblacionInicial === 0) return 0;
    
    // Factor de mortalidad esperada por semana (ajustar según experiencia)
    const factorMortalidad = Math.max(0.1, 0.5 - (semana * 0.01));
    const mortalidadEsperada = poblacionInicial * factorMortalidad;
    
    return mortalidadEsperada > 0 ? (mortalidadTotal / mortalidadEsperada) * 100 : 0;
  }

  determinarEtapa(semana: number): string {
    if (semana >= 25 && semana <= 33) return 'Etapa 1';
    if (semana >= 34 && semana <= 50) return 'Etapa 2';
    if (semana > 50) return 'Etapa 3';
    return 'Pre-producción';
  }

  getEficienciaColor(porcentaje: number): string {
    if (porcentaje >= 90) return 'success';
    if (porcentaje >= 70) return 'warning';
    return 'danger';
  }

  getMortalidadColor(porcentaje: number): string {
    if (porcentaje <= 5) return 'success';
    if (porcentaje <= 10) return 'warning';
    return 'danger';
  }
}

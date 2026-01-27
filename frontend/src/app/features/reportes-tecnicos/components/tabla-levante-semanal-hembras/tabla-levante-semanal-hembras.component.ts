// src/app/features/reportes-tecnicos/components/tabla-levante-semanal-hembras/tabla-levante-semanal-hembras.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoLevanteSemanalDto, ReporteTecnicoLoteInfoDto } from '../../services/reporte-tecnico.service';

@Component({
  selector: 'app-tabla-levante-semanal-hembras',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-levante-semanal-hembras.component.html',
  styleUrls: ['./tabla-levante-semanal-hembras.component.scss']
})
export class TablaLevanteSemanalHembrasComponent {
  @Input() datos: ReporteTecnicoLevanteSemanalDto[] = [];
  @Input() informacionLote?: ReporteTecnicoLoteInfoDto | null;

  formatNumber(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return value.toFixed(decimals);
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const d = typeof date === 'string' ? new Date(date) : date;
    const day = d.getDate().toString().padStart(2, '0');
    const month = d.toLocaleDateString('es-ES', { month: 'short' });
    const year = d.getFullYear().toString().slice(-2);
    return `${day}-${month}-${year}`;
  }

  formatPercentage(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return `${value.toFixed(decimals)}%`;
  }

  // ================== DATOS ESTÁTICOS DE GUÍA GENÉTICA (Hasta conectar con BD) ==================
  /**
   * Datos estáticos de la guía genética por semana (semanas 1-25)
   * Estos datos se mostrarán en amarillo hasta que se conecten con la base de datos
   */
  private datosGuiaGeneticaEstaticos: Map<number, {
    consumoAcumuladoTabla: number;  // consAcGrHGUIA (en gramos)
    grAveDiaTabla: number;          // grAveDiaGUIAH (gramos/ave/día)
    incrementoTabla: number;        // incrConsHGUIA (incremento de consumo)
    pesoTabla: number;              // pesoHGUIA (en gramos)
    gananciaTabla: number;          // ganancia_tabla (en gramos)
  }> = new Map([
    [1, { consumoAcumuladoTabla: 147, grAveDiaTabla: 21.0, incrementoTabla: 21.0, pesoTabla: 175, gananciaTabla: 80 }],
    [2, { consumoAcumuladoTabla: 329, grAveDiaTabla: 26.0, incrementoTabla: 5.0, pesoTabla: 270, gananciaTabla: 115 }],
    [3, { consumoAcumuladoTabla: 539, grAveDiaTabla: 30.0, incrementoTabla: 4.0, pesoTabla: 386, gananciaTabla: 120 }],
    [4, { consumoAcumuladoTabla: 777, grAveDiaTabla: 34.0, incrementoTabla: 4.0, pesoTabla: 522, gananciaTabla: 110 }],
    [5, { consumoAcumuladoTabla: 1022, grAveDiaTabla: 35.0, incrementoTabla: 1.0, pesoTabla: 598, gananciaTabla: 100 }],
    [6, { consumoAcumuladoTabla: 1281, grAveDiaTabla: 37.0, incrementoTabla: 2.0, pesoTabla: 701, gananciaTabla: 90 }],
    [7, { consumoAcumuladoTabla: 1554, grAveDiaTabla: 39.0, incrementoTabla: 2.0, pesoTabla: 770, gananciaTabla: 90 }],
    [8, { consumoAcumuladoTabla: 1848, grAveDiaTabla: 42.0, incrementoTabla: 3.0, pesoTabla: 860, gananciaTabla: 90 }],
    [9, { consumoAcumuladoTabla: 2156, grAveDiaTabla: 44.0, incrementoTabla: 2.0, pesoTabla: 950, gananciaTabla: 105 }],
    [10, { consumoAcumuladoTabla: 2478, grAveDiaTabla: 46.0, incrementoTabla: 2.0, pesoTabla: 1040, gananciaTabla: 105 }],
    [11, { consumoAcumuladoTabla: 2814, grAveDiaTabla: 48.0, incrementoTabla: 2.0, pesoTabla: 1130, gananciaTabla: 90 }],
    [12, { consumoAcumuladoTabla: 3171, grAveDiaTabla: 51.0, incrementoTabla: 3.0, pesoTabla: 1220, gananciaTabla: 90 }],
    [13, { consumoAcumuladoTabla: 3556, grAveDiaTabla: 55.0, incrementoTabla: 4.0, pesoTabla: 1315, gananciaTabla: 95 }],
    [14, { consumoAcumuladoTabla: 3969, grAveDiaTabla: 59.0, incrementoTabla: 4.0, pesoTabla: 1425, gananciaTabla: 110 }],
    [15, { consumoAcumuladoTabla: 4417, grAveDiaTabla: 64.0, incrementoTabla: 5.0, pesoTabla: 1535, gananciaTabla: 110 }],
    [16, { consumoAcumuladoTabla: 4900, grAveDiaTabla: 69.0, incrementoTabla: 5.0, pesoTabla: 1655, gananciaTabla: 120 }],
    [17, { consumoAcumuladoTabla: 5432, grAveDiaTabla: 76.0, incrementoTabla: 7.0, pesoTabla: 1785, gananciaTabla: 130 }],
    [18, { consumoAcumuladoTabla: 6013, grAveDiaTabla: 83.0, incrementoTabla: 7.0, pesoTabla: 1915, gananciaTabla: 130 }],
    [19, { consumoAcumuladoTabla: 6643, grAveDiaTabla: 90.0, incrementoTabla: 7.0, pesoTabla: 2060, gananciaTabla: 145 }],
    [20, { consumoAcumuladoTabla: 7329, grAveDiaTabla: 98.0, incrementoTabla: 8.0, pesoTabla: 2215, gananciaTabla: 155 }],
    [21, { consumoAcumuladoTabla: 8078, grAveDiaTabla: 107.0, incrementoTabla: 9.0, pesoTabla: 2400, gananciaTabla: 185 }],
    [22, { consumoAcumuladoTabla: 8862, grAveDiaTabla: 112.0, incrementoTabla: 5.0, pesoTabla: 2575, gananciaTabla: 175 }],
    [23, { consumoAcumuladoTabla: 9667, grAveDiaTabla: 115.0, incrementoTabla: 3.0, pesoTabla: 2745, gananciaTabla: 170 }],
    [24, { consumoAcumuladoTabla: 10493, grAveDiaTabla: 118.0, incrementoTabla: 3.0, pesoTabla: 2915, gananciaTabla: 170 }],
    [25, { consumoAcumuladoTabla: 11340, grAveDiaTabla: 121.0, incrementoTabla: 3.0, pesoTabla: 3080, gananciaTabla: 165 }]
  ]);

  /**
   * Obtiene el valor de consumo acumulado tabla para una semana
   * Si el DTO tiene el valor, lo usa; si no, usa el valor estático
   */
  getConsumoAcumuladoTabla(semana: number, valorDto?: number | null): number | null {
    if (valorDto !== null && valorDto !== undefined) return valorDto;
    const datos = this.datosGuiaGeneticaEstaticos.get(semana);
    return datos?.consumoAcumuladoTabla ?? null;
  }

  /**
   * Obtiene el valor de gramos ave/día tabla para una semana
   */
  getGrAveDiaTabla(semana: number, valorDto?: number | null): number | null {
    if (valorDto !== null && valorDto !== undefined) return valorDto;
    const datos = this.datosGuiaGeneticaEstaticos.get(semana);
    return datos?.grAveDiaTabla ?? null;
  }

  /**
   * Obtiene el valor de incremento tabla para una semana
   */
  getIncrementoTabla(semana: number, valorDto?: number | null): number | null {
    if (valorDto !== null && valorDto !== undefined) return valorDto;
    const datos = this.datosGuiaGeneticaEstaticos.get(semana);
    return datos?.incrementoTabla ?? null;
  }

  /**
   * Obtiene el valor de peso tabla para una semana (en gramos)
   */
  getPesoTabla(semana: number, valorDto?: number | null): number | null {
    if (valorDto !== null && valorDto !== undefined) return valorDto;
    const datos = this.datosGuiaGeneticaEstaticos.get(semana);
    return datos?.pesoTabla ?? null;
  }

  /**
   * Obtiene el valor de ganancia tabla para una semana (en gramos)
   */
  getGananciaTabla(semana: number, valorDto?: number | null): number | null {
    if (valorDto !== null && valorDto !== undefined) return valorDto;
    const datos = this.datosGuiaGeneticaEstaticos.get(semana);
    return datos?.gananciaTabla ?? null;
  }
}

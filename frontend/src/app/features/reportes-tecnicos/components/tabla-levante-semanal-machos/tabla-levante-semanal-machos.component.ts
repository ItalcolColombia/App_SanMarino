// src/app/features/reportes-tecnicos/components/tabla-levante-semanal-machos/tabla-levante-semanal-machos.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoLevanteSemanalDto, ReporteTecnicoLoteInfoDto } from '../../services/reporte-tecnico.service';

@Component({
  selector: 'app-tabla-levante-semanal-machos',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-levante-semanal-machos.component.html',
  styleUrls: ['./tabla-levante-semanal-machos.component.scss']
})
export class TablaLevanteSemanalMachosComponent {
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
   * Datos estáticos de la guía genética por semana (semanas 1-25) para MACHOS
   * Estos datos se mostrarán en amarillo hasta que se conecten con la base de datos
   */
  private datosGuiaGeneticaEstaticos: Map<number, {
    consumoAcumuladoTabla: number;  // consAcGrMGUIA (en gramos)
    grAveDiaTabla: number;          // grAveDiaMGUIA (gramos/ave/día)
    incrementoTabla: number;        // incrConsMGUIA (incremento de consumo)
    pesoTabla: number;              // pesoMGUIA (en gramos)
    gananciaTabla: number;          // ganancia_tabla (en gramos)
    uniformidadTabla: number;       // uniformidad_tabla (porcentaje)
  }> = new Map([
    [1, { consumoAcumuladoTabla: 169, grAveDiaTabla: 24.0, incrementoTabla: 24.0, pesoTabla: 175, gananciaTabla: 110, uniformidadTabla: 70 }],
    [2, { consumoAcumuladoTabla: 531, grAveDiaTabla: 52.0, incrementoTabla: 28.0, pesoTabla: 401, gananciaTabla: 175, uniformidadTabla: 70 }],
    [3, { consumoAcumuladoTabla: 916, grAveDiaTabla: 55.0, incrementoTabla: 3.0, pesoTabla: 624, gananciaTabla: 220, uniformidadTabla: 70 }],
    [4, { consumoAcumuladoTabla: 1322, grAveDiaTabla: 58.0, incrementoTabla: 3.0, pesoTabla: 822, gananciaTabla: 250, uniformidadTabla: 70 }],
    [5, { consumoAcumuladoTabla: 1737, grAveDiaTabla: 59.0, incrementoTabla: 1.0, pesoTabla: 1036, gananciaTabla: 185, uniformidadTabla: 70 }],
    [6, { consumoAcumuladoTabla: 2162, grAveDiaTabla: 61.0, incrementoTabla: 2.0, pesoTabla: 1238, gananciaTabla: 180, uniformidadTabla: 75 }],
    [7, { consumoAcumuladoTabla: 2613, grAveDiaTabla: 64.0, incrementoTabla: 3.0, pesoTabla: 1420, gananciaTabla: 135, uniformidadTabla: 75 }],
    [8, { consumoAcumuladoTabla: 3078, grAveDiaTabla: 66.0, incrementoTabla: 2.0, pesoTabla: 1545, gananciaTabla: 120, uniformidadTabla: 75 }],
    [9, { consumoAcumuladoTabla: 3557, grAveDiaTabla: 68.0, incrementoTabla: 2.0, pesoTabla: 1670, gananciaTabla: 125, uniformidadTabla: 75 }],
    [10, { consumoAcumuladoTabla: 4051, grAveDiaTabla: 70.0, incrementoTabla: 2.0, pesoTabla: 1795, gananciaTabla: 125, uniformidadTabla: 80 }],
    [11, { consumoAcumuladoTabla: 4560, grAveDiaTabla: 72.0, incrementoTabla: 2.0, pesoTabla: 1920, gananciaTabla: 125, uniformidadTabla: 80 }],
    [12, { consumoAcumuladoTabla: 5089, grAveDiaTabla: 76.0, incrementoTabla: 4.0, pesoTabla: 2045, gananciaTabla: 125, uniformidadTabla: 82.5 }],
    [13, { consumoAcumuladoTabla: 5639, grAveDiaTabla: 78.0, incrementoTabla: 2.0, pesoTabla: 2170, gananciaTabla: 125, uniformidadTabla: 82.5 }],
    [14, { consumoAcumuladoTabla: 6209, grAveDiaTabla: 81.0, incrementoTabla: 3.0, pesoTabla: 2295, gananciaTabla: 125, uniformidadTabla: 82.5 }],
    [15, { consumoAcumuladoTabla: 6802, grAveDiaTabla: 85.0, incrementoTabla: 4.0, pesoTabla: 2420, gananciaTabla: 125, uniformidadTabla: 85 }],
    [16, { consumoAcumuladoTabla: 7421, grAveDiaTabla: 88.0, incrementoTabla: 3.0, pesoTabla: 2560, gananciaTabla: 125, uniformidadTabla: 85 }],
    [17, { consumoAcumuladoTabla: 8062, grAveDiaTabla: 91.0, incrementoTabla: 3.0, pesoTabla: 2715, gananciaTabla: 140, uniformidadTabla: 87.5 }],
    [18, { consumoAcumuladoTabla: 8729, grAveDiaTabla: 95.0, incrementoTabla: 4.0, pesoTabla: 2875, gananciaTabla: 155, uniformidadTabla: 87.5 }],
    [19, { consumoAcumuladoTabla: 9424, grAveDiaTabla: 99.0, incrementoTabla: 4.0, pesoTabla: 3035, gananciaTabla: 160, uniformidadTabla: 87.5 }],
    [20, { consumoAcumuladoTabla: 10152, grAveDiaTabla: 104.0, incrementoTabla: 5.0, pesoTabla: 3195, gananciaTabla: 160, uniformidadTabla: 90 }],
    [21, { consumoAcumuladoTabla: 10913, grAveDiaTabla: 109.0, incrementoTabla: 5.0, pesoTabla: 3355, gananciaTabla: 160, uniformidadTabla: 90 }],
    [22, { consumoAcumuladoTabla: 11710, grAveDiaTabla: 114.0, incrementoTabla: 5.0, pesoTabla: 3515, gananciaTabla: 160, uniformidadTabla: 90 }],
    [23, { consumoAcumuladoTabla: 12550, grAveDiaTabla: 120.0, incrementoTabla: 6.0, pesoTabla: 3675, gananciaTabla: 160, uniformidadTabla: 90 }],
    [24, { consumoAcumuladoTabla: 13415, grAveDiaTabla: 124.0, incrementoTabla: 4.0, pesoTabla: 3825, gananciaTabla: 160, uniformidadTabla: 90 }],
    [25, { consumoAcumuladoTabla: 14283, grAveDiaTabla: 124.0, incrementoTabla: 0.0, pesoTabla: 3825, gananciaTabla: 150, uniformidadTabla: 90 }]
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

  /**
   * Obtiene el valor de uniformidad tabla para una semana (porcentaje)
   */
  getUniformidadTabla(semana: number, valorDto?: number | null): number | null {
    if (valorDto !== null && valorDto !== undefined) return valorDto;
    const datos = this.datosGuiaGeneticaEstaticos.get(semana);
    return datos?.uniformidadTabla ?? null;
  }
}

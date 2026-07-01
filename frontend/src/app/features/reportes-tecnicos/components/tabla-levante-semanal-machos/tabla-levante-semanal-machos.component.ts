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

  // ================== GUÍA GENÉTICA (desde BD: guia_genetica_sanmarino_colombia) ==================
  // Los valores de guía llegan poblados por el backend (ReporteTecnicoService) a partir de la guía
  // genética real del lote (raza + año). Si no hay fila de guía para la semana, se muestra "-" en
  // lugar de datos hardcodeados que "no corresponden" a la guía actual (REQ-002 g/i, REQ-010 d).
  // Los métodos conservan la firma (semana, valorDto) para no romper el template.

  getConsumoAcumuladoTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }

  getGrAveDiaTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }

  getIncrementoTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }

  getPesoTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }

  getGananciaTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }

  getUniformidadTabla(_semana: number, valorDto?: number | null): number | null {
    return valorDto ?? null;
  }
}

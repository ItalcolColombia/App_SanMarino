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
}

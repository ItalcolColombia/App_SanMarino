// src/app/features/reportes-tecnicos/components/tabla-levante-completa/tabla-levante-completa.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoLevanteSemanalDto } from '../../services/reporte-tecnico.service';

@Component({
  selector: 'app-tabla-levante-completa',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-levante-completa.component.html',
  styleUrls: ['./tabla-levante-completa.component.scss']
})
export class TablaLevanteCompletaComponent {
  @Input() datos: ReporteTecnicoLevanteSemanalDto[] = [];

  formatNumber(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return value.toFixed(decimals);
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('es-ES', { 
      day: '2-digit', 
      month: '2-digit', 
      year: 'numeric' 
    });
  }

  formatPercentage(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return `${value.toFixed(decimals)}%`;
  }
}


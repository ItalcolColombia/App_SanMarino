// src/app/features/reportes-tecnicos/components/tabla-datos-semanales/tabla-datos-semanales.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoSemanalDto } from '../../services/reporte-tecnico.service';

@Component({
  selector: 'app-tabla-datos-semanales',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-datos-semanales.component.html',
  styleUrls: ['./tabla-datos-semanales.component.scss']
})
export class TablaDatosSemanalesComponent {
  @Input() datos: ReporteTecnicoSemanalDto[] = [];

  formatNumber(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return value.toFixed(decimals);
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('es-ES', { 
      day: '2-digit', 
      month: 'short', 
      year: '2-digit' 
    });
  }
}



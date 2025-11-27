// src/app/features/reportes-tecnicos/components/tabla-datos-diarios/tabla-datos-diarios.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoDiarioDto } from '../../services/reporte-tecnico.service';

@Component({
  selector: 'app-tabla-datos-diarios',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-datos-diarios.component.html',
  styleUrls: ['./tabla-datos-diarios.component.scss']
})
export class TablaDatosDiariosComponent {
  @Input() datos: ReporteTecnicoDiarioDto[] = [];

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

  getMortalidadClass(mortalidad: number): string {
    if (mortalidad > 50) return 'highlight-yellow';
    if (mortalidad > 20) return 'highlight-orange';
    return '';
  }

  getDescarteClass(descarte: number): string {
    if (descarte > 0) return 'highlight-red';
    return '';
  }
}



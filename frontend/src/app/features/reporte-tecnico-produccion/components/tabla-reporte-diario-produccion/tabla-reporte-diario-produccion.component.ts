// src/app/features/reporte-tecnico-produccion/components/tabla-reporte-diario-produccion/tabla-reporte-diario-produccion.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoProduccionDiarioDto, ReporteTecnicoProduccionLoteInfoDto } from '../../services/reporte-tecnico-produccion.service';

@Component({
  selector: 'app-tabla-reporte-diario-produccion',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-reporte-diario-produccion.component.html',
  styleUrls: ['./tabla-reporte-diario-produccion.component.scss']
})
export class TablaReporteDiarioProduccionComponent {
  @Input() datos: ReporteTecnicoProduccionDiarioDto[] = [];
  @Input() informacionLote?: ReporteTecnicoProduccionLoteInfoDto | null;

  formatNumber(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return value.toFixed(decimals);
  }

  formatDate(date: string | Date): string {
    if (!date) return 'N/A';
    const d = new Date(date);
    return d.toLocaleDateString('es-ES', { 
      day: '2-digit', 
      month: '2-digit', 
      year: 'numeric' 
    });
  }

  formatPercentage(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return `${value.toFixed(decimals)}%`;
  }

  trackByFecha = (_: number, item: ReporteTecnicoProduccionDiarioDto) => item.fecha;
}

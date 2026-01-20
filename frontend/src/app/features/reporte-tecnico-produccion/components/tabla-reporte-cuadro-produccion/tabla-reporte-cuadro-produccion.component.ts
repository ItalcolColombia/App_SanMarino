// src/app/features/reporte-tecnico-produccion/components/tabla-reporte-cuadro-produccion/tabla-reporte-cuadro-produccion.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { 
  ReporteTecnicoProduccionCuadroDto, 
  ReporteTecnicoProduccionLoteInfoDto 
} from '../../services/reporte-tecnico-produccion.service';

@Component({
  selector: 'app-tabla-reporte-cuadro-produccion',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-reporte-cuadro-produccion.component.html',
  styleUrls: ['./tabla-reporte-cuadro-produccion.component.scss']
})
export class TablaReporteCuadroProduccionComponent {
  @Input() datos: ReporteTecnicoProduccionCuadroDto[] = [];
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

  trackBySemana = (_: number, item: ReporteTecnicoProduccionCuadroDto) => item.semana;
}

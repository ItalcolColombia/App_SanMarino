// src/app/features/reporte-tecnico-produccion/components/tabla-clasificacion-huevo-comercio/tabla-clasificacion-huevo-comercio.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { 
  ReporteClasificacionHuevoComercioDto, 
  ReporteTecnicoProduccionLoteInfoDto 
} from '../../services/reporte-tecnico-produccion.service';

@Component({
  selector: 'app-tabla-clasificacion-huevo-comercio',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-clasificacion-huevo-comercio.component.html',
  styleUrls: ['./tabla-clasificacion-huevo-comercio.component.scss']
})
export class TablaClasificacionHuevoComercioComponent {
  @Input() datos: ReporteClasificacionHuevoComercioDto[] = [];
  @Input() informacionLote?: ReporteTecnicoProduccionLoteInfoDto | null;

  formatNumber(value: number | null | undefined, decimals: number = 0): string {
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

  formatDateRange(fechaInicio: string | Date, fechaFin: string | Date): string {
    const inicio = this.formatDate(fechaInicio);
    const fin = this.formatDate(fechaFin);
    return `${inicio} - ${fin}`;
  }

  formatPercentage(value: number | null | undefined, decimals: number = 1): string {
    if (value === null || value === undefined) return '-';
    return `${value.toFixed(decimals)}%`;
  }

  trackBySemana = (_: number, item: ReporteClasificacionHuevoComercioDto) => item.semana;
}

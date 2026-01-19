// src/app/features/reportes-tecnicos/components/tabla-datos-diarios-machos/tabla-datos-diarios-machos.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoDiarioMachosDto, ReporteTecnicoLoteInfoDto } from '../../services/reporte-tecnico.service';

@Component({
  selector: 'app-tabla-datos-diarios-machos',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-datos-diarios-machos.component.html',
  styleUrls: ['./tabla-datos-diarios-machos.component.scss']
})
export class TablaDatosDiariosMachosComponent {
  @Input() datos: ReporteTecnicoDiarioMachosDto[] = [];
  @Input() informacionLote?: ReporteTecnicoLoteInfoDto | null;

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
    if (mortalidad > 10) return 'highlight-red';
    if (mortalidad > 5) return 'highlight-orange';
    if (mortalidad > 0) return 'highlight-yellow';
    return '';
  }

  getPorcentajeClass(porcentaje: number): string {
    if (porcentaje > 1) return 'highlight-red';
    if (porcentaje > 0.5) return 'highlight-orange';
    return '';
  }

  calcularConsumoBultos(kilos: number): number {
    // Asumiendo 40kg por bulto estándar
    return kilos / 40;
  }

  trackByFecha = (_: number, item: ReporteTecnicoDiarioMachosDto) => item.fecha;
}

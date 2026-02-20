// src/app/features/reporte-contable/components/tabla-movimientos-huevos/tabla-movimientos-huevos.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteMovimientosHuevosDto, MovimientoHuevoDiarioDto } from '../../services/reporte-contable.service';

@Component({
  selector: 'app-tabla-movimientos-huevos',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-movimientos-huevos.component.html',
  styleUrls: ['./tabla-movimientos-huevos.component.scss']
})
export class TablaMovimientosHuevosComponent {
  @Input() reporte: ReporteMovimientosHuevosDto | null = null;

  get movimientosDiarios(): MovimientoHuevoDiarioDto[] {
    return this.reporte?.movimientosDiarios || [];
  }

  getDiaSemana(fecha: string): string {
    const date = new Date(fecha);
    const dias = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
    return dias[date.getDay()];
  }

  getNumeroDia(fecha: string): number {
    return new Date(fecha).getDate();
  }

  formatFecha(fecha: string): string {
    const date = new Date(fecha);
    return date.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit' });
  }
}

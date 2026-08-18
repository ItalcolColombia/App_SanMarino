// src/app/features/reporte-contable/components/tabla-bultos-contable/tabla-bultos-contable.component.ts
import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteContableSemanalDto, DatoDiarioContableDto } from '../../services/reporte-contable.service';

@Component({
  selector: 'app-tabla-bultos-contable',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-bultos-contable.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabla-bultos-contable.component.scss']
})
export class TablaBultosContableComponent {
  @Input() reporteSemanal: ReporteContableSemanalDto | null = null;

  /**
   * Aviso de alcance del kardex de bultos. Los movimientos de alimento son de la GRANJA: cuando la
   * granja tiene varios lotes padres, todos sus reportes muestran los mismos kilos y sumarlos
   * multiplica el alimento (§2.4 de la auditoría de ago-2026). Null = el padre es el único.
   */
  @Input() advertenciaBultos: string | null = null;

  get datosDiarios(): DatoDiarioContableDto[] {
    return this.reporteSemanal?.datosDiarios || [];
  }

  getDiaSemana(fecha: string): string {
    const date = new Date(fecha);
    const dias = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
    return dias[date.getDay()];
  }

  getNumeroDia(fecha: string): number {
    return new Date(fecha).getDate();
  }
}











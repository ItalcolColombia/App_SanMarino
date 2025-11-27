// src/app/features/reporte-contable/components/tabla-detalle-diario-contable/tabla-detalle-diario-contable.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ConsumoDiarioContableDto } from '../../services/reporte-contable.service';

@Component({
  selector: 'app-tabla-detalle-diario-contable',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './tabla-detalle-diario-contable.component.html',
  styleUrls: ['./tabla-detalle-diario-contable.component.scss']
})
export class TablaDetalleDiarioContableComponent {
  @Input() consumosDiarios: ConsumoDiarioContableDto[] = [];

  getSubtotalAlimento(): number {
    return this.consumosDiarios.reduce((sum, c) => sum + c.consumoAlimento, 0);
  }

  getSubtotalAgua(): number {
    return this.consumosDiarios.reduce((sum, c) => sum + c.consumoAgua, 0);
  }

  getSubtotalMedicamento(): number {
    return this.consumosDiarios.reduce((sum, c) => sum + c.consumoMedicamento, 0);
  }

  getSubtotalVacuna(): number {
    return this.consumosDiarios.reduce((sum, c) => sum + c.consumoVacuna, 0);
  }

  getSubtotalOtros(): number {
    return this.consumosDiarios.reduce((sum, c) => sum + c.otrosConsumos, 0);
  }

  getSubtotalGeneral(): number {
    return this.consumosDiarios.reduce((sum, c) => sum + c.totalConsumo, 0);
  }
}


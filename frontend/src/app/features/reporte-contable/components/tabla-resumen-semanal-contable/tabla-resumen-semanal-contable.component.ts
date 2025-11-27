// src/app/features/reporte-contable/components/tabla-resumen-semanal-contable/tabla-resumen-semanal-contable.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ReporteContableSemanalDto } from '../../services/reporte-contable.service';

@Component({
  selector: 'app-tabla-resumen-semanal-contable',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './tabla-resumen-semanal-contable.component.html',
  styleUrls: ['./tabla-resumen-semanal-contable.component.scss']
})
export class TablaResumenSemanalContableComponent {
  @Input() reportesSemanales: ReporteContableSemanalDto[] = [];

  getTotalAlimento(): number {
    return this.reportesSemanales.reduce((sum, r) => sum + (r.consumoTotalAlimento || 0), 0);
  }

  getTotalAgua(): number {
    return this.reportesSemanales.reduce((sum, r) => sum + (r.consumoTotalAgua || 0), 0);
  }

  getTotalMedicamento(): number {
    return this.reportesSemanales.reduce((sum, r) => sum + (r.consumoTotalMedicamento || 0), 0);
  }

  getTotalVacuna(): number {
    return this.reportesSemanales.reduce((sum, r) => sum + (r.consumoTotalVacuna || 0), 0);
  }

  getTotalOtros(): number {
    return this.reportesSemanales.reduce((sum, r) => sum + (r.otrosConsumos || 0), 0);
  }

  getTotalGeneral(): number {
    return this.reportesSemanales.reduce((sum, r) => sum + (r.totalGeneral || 0), 0);
  }
}


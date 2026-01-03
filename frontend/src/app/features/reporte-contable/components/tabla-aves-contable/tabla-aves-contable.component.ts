// src/app/features/reporte-contable/components/tabla-aves-contable/tabla-aves-contable.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteContableSemanalDto, DatoDiarioContableDto } from '../../services/reporte-contable.service';

@Component({
  selector: 'app-tabla-aves-contable',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-aves-contable.component.html',
  styleUrls: ['./tabla-aves-contable.component.scss']
})
export class TablaAvesContableComponent {
  @Input() reporteSemanal: ReporteContableSemanalDto | null = null;

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











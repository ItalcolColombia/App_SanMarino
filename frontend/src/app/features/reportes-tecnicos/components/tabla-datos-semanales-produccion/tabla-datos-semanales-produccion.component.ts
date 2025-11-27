// src/app/features/reportes-tecnicos/components/tabla-datos-semanales-produccion/tabla-datos-semanales-produccion.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ReporteTecnicoProduccionSemanalDto } from '../../services/reporte-tecnico-produccion.service';

@Component({
  selector: 'app-tabla-datos-semanales-produccion',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './tabla-datos-semanales-produccion.component.html',
  styleUrls: ['./tabla-datos-semanales-produccion.component.scss']
})
export class TablaDatosSemanalesProduccionComponent {
  @Input() datos: ReporteTecnicoProduccionSemanalDto[] = [];

  trackBySemana = (_: number, item: ReporteTecnicoProduccionSemanalDto) => item.semana;
}


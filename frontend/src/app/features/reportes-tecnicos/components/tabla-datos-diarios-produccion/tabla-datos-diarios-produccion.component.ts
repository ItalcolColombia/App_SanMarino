// src/app/features/reportes-tecnicos/components/tabla-datos-diarios-produccion/tabla-datos-diarios-produccion.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ReporteTecnicoProduccionDiarioDto } from '../../services/reporte-tecnico-produccion.service';

@Component({
  selector: 'app-tabla-datos-diarios-produccion',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './tabla-datos-diarios-produccion.component.html',
  styleUrls: ['./tabla-datos-diarios-produccion.component.scss']
})
export class TablaDatosDiariosProduccionComponent {
  @Input() datos: ReporteTecnicoProduccionDiarioDto[] = [];

  trackByFecha = (_: number, item: ReporteTecnicoProduccionDiarioDto) => item.fecha;
}


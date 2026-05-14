import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteGeneralSemanalDto } from '../../services/reporte-tecnico.service';

@Component({
  selector: 'app-reporte-general-semanal',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reporte-general-semanal.component.html',
  styleUrls: ['./reporte-general-semanal.component.scss']
})
export class ReporteGeneralSemanalComponent {
  @Input() datos: ReporteGeneralSemanalDto[] = [];

  semaforo(real: number | null | undefined, guia: number | null | undefined, umbral = 5): string {
    if (real == null || guia == null) return '';
    const pct = guia !== 0 ? (Math.abs(real - guia) / Math.abs(guia)) * 100 : 0;
    if (pct <= umbral)      return 'cell-success';
    if (pct <= umbral * 3)  return 'cell-warning';
    return 'cell-danger';
  }

  fmt(v: number | null | undefined, dec = 1): string {
    if (v == null) return '—';
    return v.toFixed(dec);
  }

  classDif(v: number | null | undefined): string {
    if (v == null) return '';
    return v > 0.01 ? 'cell-success' : v < -0.01 ? 'cell-danger' : '';
  }
}

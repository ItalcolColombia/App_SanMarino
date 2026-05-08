import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteDiarioGalponDto } from '../../services/reporte-tecnico.service';

@Component({
  selector: 'app-reporte-diario-galpon',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reporte-diario-galpon.component.html',
  styleUrls: ['./reporte-diario-galpon.component.scss']
})
export class ReporteDiarioGalponComponent {
  @Input() datos: ReporteDiarioGalponDto[] = [];
  @Input() galponNombre = '';

  semaforo(real: number | null | undefined, guia: number | null | undefined, umbral = 5): string {
    if (real == null || guia == null) return '';
    const dif = Math.abs(real - guia);
    const pct = guia !== 0 ? (dif / Math.abs(guia)) * 100 : 0;
    if (pct <= umbral)        return 'cell-success';
    if (pct <= umbral * 3)    return 'cell-warning';
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

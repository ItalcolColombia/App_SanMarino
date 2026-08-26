import { Component, inject, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteGeneralDiarioDto } from '../../services/reporte-tecnico.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';

@Component({
  selector: 'app-reporte-general-diario',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reporte-general-diario.component.html',
  styleUrls: ['./reporte-general-diario.component.scss']
})
export class ReporteGeneralDiarioComponent {
  /** Empresas sin machos en postura: sus columnas no se pintan (SR-DEF-1). */
  ocultaMachosEnPostura = false;

  private readonly companyConfigMachos = inject(ActiveCompanyConfigService);

  constructor() {
    this.companyConfigMachos.getFlags().subscribe({
      next: f => (this.ocultaMachosEnPostura = !!f?.ocultaMachosEnPostura),
      error: () => (this.ocultaMachosEnPostura = false)
    });
  }

  @Input() datos: ReporteGeneralDiarioDto[] = [];

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

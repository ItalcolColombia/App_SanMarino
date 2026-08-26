// src/app/features/reportes-tecnicos/components/tabla-levante-completa/tabla-levante-completa.component.ts
import { Component, inject, Input, ChangeDetectionStrategy } from '@angular/core';

import { ReporteTecnicoLevanteSemanalDto } from '../../services/reporte-tecnico.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';

@Component({
  selector: 'app-tabla-levante-completa',
  standalone: true,
  imports: [],
  templateUrl: './tabla-levante-completa.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabla-levante-completa.component.scss']
})
export class TablaLevanteCompletaComponent {
  /** Empresas sin machos en postura: sus columnas no se pintan (SR-DEF-1). */
  ocultaMachosEnPostura = false;

  private readonly companyConfigMachos = inject(ActiveCompanyConfigService);

  constructor() {
    this.companyConfigMachos.getFlags().subscribe({
      next: f => (this.ocultaMachosEnPostura = !!f?.ocultaMachosEnPostura),
      error: () => (this.ocultaMachosEnPostura = false)
    });
  }

  @Input() datos: ReporteTecnicoLevanteSemanalDto[] = [];

  formatNumber(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return value.toFixed(decimals);
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('es-ES', { 
      day: '2-digit', 
      month: '2-digit', 
      year: 'numeric' 
    });
  }

  formatPercentage(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return `${value.toFixed(decimals)}%`;
  }
}


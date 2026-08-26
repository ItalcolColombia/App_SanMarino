// src/app/features/reporte-contable/components/tabla-aves-contable/tabla-aves-contable.component.ts
import { Component, Input, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteContableSemanalDto, DatoDiarioContableDto } from '../../services/reporte-contable.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';

@Component({
  selector: 'app-tabla-aves-contable',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-aves-contable.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabla-aves-contable.component.scss']
})
export class TablaAvesContableComponent implements OnInit {
  @Input() reporteSemanal: ReporteContableSemanalDto | null = null;

  /** Empresas sin machos en postura: sus columnas no se pintan (SR-DEF-1). */
  ocultaMachosEnPostura = false;

  constructor(private companyConfig: ActiveCompanyConfigService) {}

  ngOnInit(): void {
    this.companyConfig.getFlags().subscribe({
      next: f => (this.ocultaMachosEnPostura = !!f?.ocultaMachosEnPostura),
      error: () => (this.ocultaMachosEnPostura = false)
    });
  }

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











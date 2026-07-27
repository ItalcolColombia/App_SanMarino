// src/app/features/reporte-contable/components/tabla-movimientos-huevos/tabla-movimientos-huevos.component.ts
import { Component, Input, OnInit, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteMovimientosHuevosDto, MovimientoHuevoDiarioDto } from '../../services/reporte-contable.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';

@Component({
  selector: 'app-tabla-movimientos-huevos',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-movimientos-huevos.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabla-movimientos-huevos.component.scss']
})
export class TablaMovimientosHuevosComponent implements OnInit {
  @Input() reporte: ReporteMovimientosHuevosDto | null = null;

  private readonly companyConfig = inject(ActiveCompanyConfigService);
  /**
   * Flag `companies.clasificacion_huevo_por_items` de la EMPRESA ACTIVA (el reporte es
   * multi-lote/multi-granja de esa misma empresa): con clasificación por ítem, `HVO COMERCIAL` y
   * `HUEVO DESECHO` salen de las 11 columnas fijas y quedan siempre en 0 → se ocultan.
   * `POSTURA` (total) y `HVTO FERTIL` se mantienen. FAIL-CLOSED: sin flag, tabla intacta.
   */
  clasificacionHuevoPorItems = false;

  ngOnInit(): void {
    this.companyConfig.getFlags().subscribe(flags => {
      this.clasificacionHuevoPorItems = flags.clasificacionHuevoPorItems;
    });
  }

  get movimientosDiarios(): MovimientoHuevoDiarioDto[] {
    return this.reporte?.movimientosDiarios || [];
  }

  getDiaSemana(fecha: string): string {
    const date = new Date(fecha);
    const dias = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
    return dias[date.getDay()];
  }

  getNumeroDia(fecha: string): number {
    return new Date(fecha).getDate();
  }

  formatFecha(fecha: string): string {
    const date = new Date(fecha);
    return date.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit' });
  }
}

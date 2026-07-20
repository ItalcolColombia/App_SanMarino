// frontend/src/app/features/indicador-ecuador/components/liquidacion-reporte-corrida-panama/liquidacion-reporte-corrida-panama.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReporteCorridaPanamaDto,
  ReporteCorridaPanamaItemDto,
  ReporteIndicadoresPanamaDto
} from '../../services/indicador-ecuador.service';
import { LiquidacionReportePanamaComponent } from '../liquidacion-reporte-panama/liquidacion-reporte-panama.component';
import { formatearFechaLote } from '../../funciones/formato.funcion';

/**
 * Reporte de liquidación de una CORRIDA Panamá: tabs Consolidado + un tab por galpón,
 * reutilizando el reporte individual (`app-liquidacion-reporte-panama`) por ítem.
 * Lista además los galpones de la corrida que aún no tienen liquidación registrada.
 */
@Component({
  selector: 'app-liquidacion-reporte-corrida-panama',
  standalone: true,
  imports: [CommonModule, LiquidacionReportePanamaComponent],
  templateUrl: './liquidacion-reporte-corrida-panama.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./liquidacion-reporte-corrida-panama.component.scss']
})
export class LiquidacionReporteCorridaPanamaComponent implements OnChanges {
  @Input({ required: true }) data!: ReporteCorridaPanamaDto;
  @Input() granjaNombre: string | null = null;

  @Output() cerrar = new EventEmitter<void>();

  /** Tab activa: consolidado de la corrida o el loteAveEngordeId de un galpón. */
  tabActiva: 'consolidado' | number = 'consolidado';

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data']) {
      // Nueva corrida generada ⇒ volver al consolidado (o al primer galpón si no hay).
      this.tabActiva = this.data?.consolidado ? 'consolidado' : (this.data?.items[0]?.loteAveEngordeId ?? 'consolidado');
    }
  }

  seleccionarTab(tab: 'consolidado' | number): void {
    this.tabActiva = tab;
  }

  /** Reporte mostrado según la tab activa (consolidado o el del galpón). */
  get reporteActivo(): ReporteIndicadoresPanamaDto | null {
    if (this.tabActiva === 'consolidado') return this.data.consolidado;
    const item = this.data.items.find(i => i.loteAveEngordeId === this.tabActiva);
    return item?.reporte ?? null;
  }

  /** Subtítulo del reporte embebido según la tab activa. */
  get subtituloActivo(): string {
    const granja = this.granjaNombre ? ` · ${this.granjaNombre}` : '';
    if (this.tabActiva === 'consolidado') {
      return `Consolidado corrida ${this.data.corrida}${granja} · ${this.data.items.length} galpón(es)`;
    }
    const item = this.data.items.find(i => i.loteAveEngordeId === this.tabActiva);
    if (!item) return `Corrida ${this.data.corrida}${granja}`;
    return `Corrida ${this.data.corrida}${granja} · Galpón ${item.galponId ?? '—'} · enc. ${formatearFechaLote(item.fechaEncaset)}`;
  }

  etiquetaTab(item: ReporteCorridaPanamaItemDto): string {
    return `Galpón ${item.galponId ?? item.loteAveEngordeId}`;
  }

  etiquetaSinLiquidacion(l: { galponId: string | null; fechaEncaset: string | null }): string {
    return `Galpón ${l.galponId ?? '—'} (enc. ${formatearFechaLote(l.fechaEncaset)})`;
  }

  print(): void {
    window.print();
  }
}

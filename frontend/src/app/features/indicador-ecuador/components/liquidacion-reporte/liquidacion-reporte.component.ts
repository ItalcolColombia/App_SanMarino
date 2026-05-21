// frontend/src/app/features/indicador-ecuador/components/liquidacion-reporte/liquidacion-reporte.component.ts
import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import {
  LiquidacionPolloEngordeItemDto,
  IndicadorEcuadorDto
} from '../../services/indicador-ecuador.service';

@Component({
  selector: 'app-liquidacion-reporte',
  standalone: true,
  imports: [CommonModule],
  providers: [DatePipe, DecimalPipe],
  templateUrl: './liquidacion-reporte.component.html',
  styleUrls: ['./liquidacion-reporte.component.scss']
})
export class LiquidacionReporteComponent {
  @Input() items: LiquidacionPolloEngordeItemDto[] = [];
  @Input() empresa: string = 'ECU - ITALCOL S.A.';

  print(): void {
    window.print();
  }

  /** Días calendario entre encasetamiento y cierre */
  diasEngorde(ind: IndicadorEcuadorDto): number {
    if (!ind.fechaInicioLote || !ind.fechaCierreLote) return 0;
    const ms = new Date(ind.fechaCierreLote).getTime() - new Date(ind.fechaInicioLote).getTime();
    return Math.round(ms / 86_400_000);
  }

  /** Consumo ave en Kg (el DTO lo trae en gramos) */
  consumoAveKg(ind: IndicadorEcuadorDto): number {
    return ind.consumoAveGramos / 1000;
  }

  /** Residual de aves (puede ser negativo si hay ajustes) */
  ajusteAves(ind: IndicadorEcuadorDto): number {
    return ind.avesEncasetadas - ind.mortalidad - ind.avesSacrificadas;
  }

  pct(ajuste: number, encasetadas: number): number {
    return encasetadas > 0 ? (ajuste / encasetadas) * 100 : 0;
  }

  fmt(val: number | null | undefined, decimals = 2): string {
    if (val == null) return '0.00';
    return val.toFixed(decimals);
  }

  fmtDate(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    return `${d.getMonth() + 1}/${d.getDate()}/${d.getFullYear()}`;
  }

  /** Identificador legible del lote: Galpón + nombre lote */
  loteLabel(item: LiquidacionPolloEngordeItemDto): string {
    const galpon = item.indicador.galponNombre ?? '';
    const lote   = item.loteNombre ?? '';
    return galpon ? `${galpon} — ${lote}` : lote;
  }
}

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

  /** Días de engorde: usa el cálculo del backend; fallback a días calendario encaset→cierre. */
  diasEngorde(ind: IndicadorEcuadorDto): number {
    if (ind.diasEngorde != null) return ind.diasEngorde;
    if (!ind.fechaInicioLote || !ind.fechaCierreLote) return 0;
    const ms = new Date(ind.fechaCierreLote).getTime() - new Date(ind.fechaInicioLote).getTime();
    return Math.round(ms / 86_400_000);
  }

  /** Consumo ave en Kg (el DTO lo trae en gramos) */
  consumoAveKg(ind: IndicadorEcuadorDto): number {
    return ind.consumoAveGramos / 1000;
  }

  /** R1: true si Costos registró merma en el lote (si no, el bloque va vacío «—»). */
  mermaRegistrada(ind: IndicadorEcuadorDto): boolean {
    return ind.mermaUnidades != null || ind.mermaKilos != null;
  }

  /** Ajuste de aves: backend (incluye merma); null = sin merma registrada ⇒ campo vacío. */
  ajusteAves(ind: IndicadorEcuadorDto): number | null {
    if (ind.ajusteAves != null) return ind.ajusteAves;
    if (!this.mermaRegistrada(ind)) return null;
    return ind.avesEncasetadas - ind.mortalidad - ind.avesSacrificadas - (ind.mermaUnidades ?? 0);
  }

  /** % de ajuste: backend; null = sin merma registrada ⇒ campo vacío. */
  porcentajeAjuste(ind: IndicadorEcuadorDto): number | null {
    if (ind.porcentajeAjuste != null) return ind.porcentajeAjuste;
    const ajuste = this.ajusteAves(ind);
    if (ajuste == null) return null;
    return ind.avesEncasetadas > 0 ? (ajuste / ind.avesEncasetadas) * 100 : 0;
  }

  /** Producción kilo en pie (kg que salen de granja). */
  produccionKiloEnPie(ind: IndicadorEcuadorDto): number {
    return ind.produccionKiloEnPie ?? ind.kgCarnePollos;
  }

  /** Total kilos a cliente = producción − merma kilos; null = sin merma registrada ⇒ vacío. */
  totalKilosCliente(ind: IndicadorEcuadorDto): number | null {
    if (ind.totalKilosDespachadosCliente != null) return ind.totalKilosDespachadosCliente;
    if (!this.mermaRegistrada(ind)) return null;
    return this.produccionKiloEnPie(ind) - (ind.mermaKilos ?? 0);
  }

  fmt(val: number | null | undefined, decimals = 2): string {
    if (val == null) return '0.00';
    return val.toFixed(decimals);
  }

  /** R1: valor o «—» — merma no registrada ⇒ campo vacío en la ficha. */
  fmtONada(val: number | null | undefined, decimals = 2): string {
    if (val == null) return '—';
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

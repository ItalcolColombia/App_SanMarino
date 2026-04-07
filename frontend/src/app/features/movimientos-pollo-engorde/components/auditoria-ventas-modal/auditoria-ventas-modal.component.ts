import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuditoriaVentasEngordeResponse, AuditoriaVentasLoteDetalle } from '../../services/movimiento-pollo-engorde.service';

@Component({
  selector: 'app-auditoria-ventas-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './auditoria-ventas-modal.component.html',
  styleUrls: ['./auditoria-ventas-modal.component.scss']
})
export class AuditoriaVentasModalComponent {
  @Input() isOpen = false;
  @Input() data: AuditoriaVentasEngordeResponse | null = null;
  @Input() loteMetaById: Record<number, { galponLabel: string; loteLabel: string }> = {};
  @Output() close = new EventEmitter<void>();
  @Output() corregirCompletados = new EventEmitter<void>();

  expanded: Record<number, boolean> = {};

  onClose(): void {
    this.expanded = {};
    this.close.emit();
  }

  onCorregirCompletados(): void {
    this.corregirCompletados.emit();
  }

  toggle(loteId: number): void {
    this.expanded = { ...this.expanded, [loteId]: !this.expanded[loteId] };
  }

  isExpanded(loteId: number): boolean {
    return !!this.expanded[loteId];
  }

  lotesConExceso(): AuditoriaVentasLoteDetalle[] {
    const lotes = this.data?.lotes ?? [];
    return lotes
      .filter((l) => (l.excesoH + l.excesoM + l.excesoX) > 0)
      .sort((a, b) => (b.excesoH + b.excesoM + b.excesoX) - (a.excesoH + a.excesoM + a.excesoX));
  }

  fmt(n: number): string {
    return new Intl.NumberFormat('es-CO').format(n ?? 0);
  }

  galponLabel(loteId: number): string {
    return this.loteMetaById?.[loteId]?.galponLabel || '—';
  }

  modoLabel(): string {
    const d = this.data;
    if (!d) return '';
    if (d.aplicarCorreccion) return 'CORRIGIÓ (solo Pendiente)';
    if (d.dryRun) return 'VALIDACIÓN (sin cambios)';
    return 'VALIDACIÓN';
  }

  estadoBadgeClass(l: AuditoriaVentasLoteDetalle): string {
    const exceso = (l.excesoH + l.excesoM + l.excesoX) > 0;
    if (!exceso) return 'badge badge--success';
    return l.autoCorregible ? 'badge badge--warning' : 'badge badge--danger';
  }
}


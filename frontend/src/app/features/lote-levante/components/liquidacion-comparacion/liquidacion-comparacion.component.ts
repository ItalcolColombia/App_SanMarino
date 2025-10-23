// src/app/features/lote-levante/components/liquidacion-comparacion/liquidacion-comparacion.component.ts
import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LiquidacionComparacionService, LiquidacionTecnicaComparacionDto, LiquidacionTecnicaComparacionCompletaDto } from '../../services/liquidacion-comparacion.service';

@Component({
  selector: 'app-liquidacion-comparacion',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './liquidacion-comparacion.component.html',
  styleUrls: ['./liquidacion-comparacion.component.scss']
})
export class LiquidacionComparacionComponent implements OnInit, OnChanges {
  @Input() loteId: number | null = null;
  @Input() fechaHasta?: string;

  comparacion: LiquidacionTecnicaComparacionDto | null = null;
  observaciones: string | null = null;
  loading = false;
  error: string | null = null;

  constructor(private liquidacionComparacionService: LiquidacionComparacionService) {}

  ngOnInit(): void {
    if (this.loteId) {
      this.cargarComparacion();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['loteId'] && this.loteId) {
      this.cargarComparacion();
    }
  }

  private cargarComparacion(): void {
    if (!this.loteId) return;

    this.loading = true;
    this.error = null;
    this.comparacion = null;
    this.observaciones = null;

    this.liquidacionComparacionService.obtenerComparacionCompleta(this.loteId, this.fechaHasta)
      .subscribe({
        next: (resultado) => {
          this.comparacion = resultado.resumen;
          this.observaciones = resultado.observaciones || null;
          this.loading = false;
        },
        error: (err) => {
          console.error('Error cargando comparación:', err);
          this.error = err.error?.error || 'Error al cargar la comparación con la guía genética';
          this.loading = false;
        }
      });
  }

  getEstadoClass(estado: string | undefined): string {
    switch (estado?.toLowerCase()) {
      case 'excelente': return 'estado-excelente';
      case 'bueno': return 'estado-bueno';
      case 'regular': return 'estado-regular';
      case 'deficiente': return 'estado-deficiente';
      default: return 'estado-sin-datos';
    }
  }

  getComparacionClass(cumple: boolean): string {
    return cumple ? 'comparacion-cumple' : 'comparacion-no-cumple';
  }

  getEstadoIndicatorClass(cumple: boolean): string {
    return cumple ? 'indicator-cumple' : 'indicator-no-cumple';
  }

  getDiferenciaClass(diferencia: number): string {
    if (Math.abs(diferencia) <= 5) return 'diferencia-baja';
    if (Math.abs(diferencia) <= 15) return 'diferencia-media';
    return 'diferencia-alta';
  }

  getProgressDashArray(): string {
    const circumference = 2 * Math.PI * 36; // r = 36
    return `${circumference} ${circumference}`;
  }

  getProgressDashOffset(): number {
    if (!this.comparacion) return 0;
    const circumference = 2 * Math.PI * 36;
    const progress = this.comparacion.porcentajeCumplimiento / 100;
    return circumference - (progress * circumference);
  }
}

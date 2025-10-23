// src/app/features/traslados-aves/components/traslado-navigation-card/traslado-navigation-card.component.ts
import { Component, Input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MovimientoAvesCompleto, UbicacionCompleta } from '../../../../core/services/traslado-navigation/traslado-navigation.service';

@Component({
  selector: 'app-traslado-navigation-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './traslado-navigation-card.component.html',
  styleUrls: ['./traslado-navigation-card.component.scss']
})
export class TrasladoNavigationCardComponent implements OnInit {
  @Input() movimiento!: MovimientoAvesCompleto;
  @Input() showDetails: boolean = true;
  @Input() compact: boolean = false;

  // Signals para el estado del componente
  isExpanded = signal(false);
  isLoading = signal(false);

  constructor() {}

  ngOnInit(): void {
    if (!this.movimiento) {
      throw new Error('MovimientoAvesCompleto es requerido');
    }
  }

  /**
   * Alterna la expansión de la tarjeta
   */
  toggleExpansion(): void {
    this.isExpanded.update(expanded => !expanded);
  }

  /**
   * Obtiene el estado del badge
   */
  getEstadoBadgeClass(estado: string): string {
    switch (estado.toLowerCase()) {
      case 'pendiente':
        return 'badge-warning';
      case 'completado':
        return 'badge-success';
      case 'cancelado':
        return 'badge-danger';
      default:
        return 'badge-secondary';
    }
  }

  /**
   * Obtiene el icono del tipo de movimiento
   */
  getTipoMovimientoIcon(tipo: string): string {
    switch (tipo.toLowerCase()) {
      case 'traslado':
        return '🚚';
      case 'ajuste':
        return '⚖️';
      case 'liquidacion':
        return '💰';
      default:
        return '📋';
    }
  }

  /**
   * Formatea la fecha para mostrar
   */
  formatFecha(fecha: string): string {
    return new Date(fecha).toLocaleDateString('es-ES', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  /**
   * Obtiene el resumen de ubicación
   */
  getUbicacionResumen(ubicacion: UbicacionCompleta): string {
    const partes = [];
    
    if (ubicacion.loteNombre) partes.push(ubicacion.loteNombre);
    if (ubicacion.granjaNombre) partes.push(ubicacion.granjaNombre);
    if (ubicacion.nucleoNombre) partes.push(ubicacion.nucleoNombre);
    if (ubicacion.galponNombre) partes.push(ubicacion.galponNombre);
    
    return partes.length > 0 ? partes.join(' - ') : 'Sin ubicación';
  }

  /**
   * Obtiene información adicional del lote
   */
  getLoteInfo(ubicacion: UbicacionCompleta): string[] {
    const info = [];
    
    if (ubicacion.raza) info.push(`Raza: ${ubicacion.raza}`);
    if (ubicacion.linea) info.push(`Línea: ${ubicacion.linea}`);
    if (ubicacion.codigoGuiaGenetica) info.push(`Guía: ${ubicacion.codigoGuiaGenetica}`);
    if (ubicacion.tecnico) info.push(`Técnico: ${ubicacion.tecnico}`);
    
    return info;
  }

  /**
   * Obtiene la clase CSS para el tipo de movimiento
   */
  getTipoMovimientoClass(tipo: string): string {
    switch (tipo.toLowerCase()) {
      case 'traslado':
        return 'tipo-traslado';
      case 'ajuste':
        return 'tipo-ajuste';
      case 'liquidacion':
        return 'tipo-liquidacion';
      default:
        return 'tipo-default';
    }
  }

  /**
   * Verifica si es un movimiento interno
   */
  esMovimientoInterno(): boolean {
    return this.movimiento.esMovimientoInterno;
  }

  /**
   * Verifica si es un movimiento entre granjas
   */
  esMovimientoEntreGranjas(): boolean {
    return this.movimiento.esMovimientoEntreGranjas;
  }

  /**
   * Obtiene el total de aves formateado
   */
  getTotalAvesFormateado(): string {
    return this.movimiento.totalAves.toLocaleString('es-ES');
  }

  /**
   * Obtiene el porcentaje de hembras
   */
  getPorcentajeHembras(): number {
    if (this.movimiento.totalAves === 0) return 0;
    return Math.round((this.movimiento.cantidadHembras / this.movimiento.totalAves) * 100);
  }

  /**
   * Obtiene el porcentaje de machos
   */
  getPorcentajeMachos(): number {
    if (this.movimiento.totalAves === 0) return 0;
    return Math.round((this.movimiento.cantidadMachos / this.movimiento.totalAves) * 100);
  }

  /**
   * Obtiene el porcentaje de mixtas
   */
  getPorcentajeMixtas(): number {
    if (this.movimiento.totalAves === 0) return 0;
    return Math.round((this.movimiento.cantidadMixtas / this.movimiento.totalAves) * 100);
  }
}






import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto, ProduccionLoteDetalleDto } from '../../services/produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { LotePosturaProduccionFilterItem } from '../filtro-select/filtro-select.component';
// Usar versión "components" que trae indicadores desde backend en 1 sola petición
import { TablaListaIndicadoresComponent } from '../../components/tabla-lista-indicadores/tabla-lista-indicadores.component';
import { GraficasPrincipalComponent } from '../graficas-principal/graficas-principal.component';

@Component({
  selector: 'app-tabs-principal',
  standalone: true,
  imports: [CommonModule, TablaListaIndicadoresComponent, GraficasPrincipalComponent],
  templateUrl: './tabs-principal.component.html',
  styleUrls: ['./tabs-principal.component.scss']
})
export class TabsPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() produccionLote: ProduccionLoteDetalleDto | null = null;
  /** ID del lote en fase Producción (hijo o mismo). Mismo que usa listado y modal de seguimiento diario. */
  @Input() produccionLoteId: number | null = null;
  /** Lote postura producción seleccionado (flujo LPP). Incluye aves, estado. */
  @Input() selectedLoteLPP: LotePosturaProduccionFilterItem | null = null;
  /** ID del lote postura producción (flujo LPP). Se pasa a indicadores/gráfica. */
  get lotePosturaProduccionId(): number | null {
    return this.selectedLoteLPP?.lotePosturaProduccionId ?? null;
  }
  @Input() loading: boolean = false;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoItemDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoItemDto>();

  activeTab: 'general' | 'indicadores' | 'grafica' = 'general';

  constructor() { }

  ngOnInit(): void {
  }

  ngOnChanges(changes: SimpleChanges): void {
  }

  // ================== EVENTOS ==================
  onTabChange(tab: 'general' | 'indicadores' | 'grafica'): void {
    this.activeTab = tab;
  }

  onCreate(): void {
    this.create.emit();
  }

  onEdit(seg: SeguimientoItemDto): void {
    this.edit.emit(seg);
  }

  onDelete(id: number): void {
    this.delete.emit(id);
  }

  onViewDetail(seg: SeguimientoItemDto): void {
    this.viewDetail.emit(seg);
  }

  /** Fecha base para calcular edad (encaset desde Lote Levante). */
  getFechaBaseEdad(): string | Date | null {
    if (this.selectedLoteLPP?.fechaEncaset) return this.selectedLoteLPP.fechaEncaset;
    if (this.produccionLote?.fechaInicio) return this.produccionLote.fechaInicio;
    return this.selectedLote?.fechaEncaset ?? null;
  }

  /** Edad del lote en semanas (desde fecha base hasta hoy). Producción: semana >= 26. */
  getEdadSemanas(): number | string {
    const base = this.getFechaBaseEdad();
    if (!base) return '—';
    const dias = this.calcularEdadDiasDesdeFecha(base, new Date().toISOString());
    return Math.floor(dias / 7) + 1;
  }

  /** Edad en días desde fecha base hasta fechaRegistro. */
  calcularEdadDias(fechaRegistro: string | Date): number {
    const base = this.getFechaBaseEdad();
    if (!base) return 0;
    return this.calcularEdadDiasDesdeFecha(base, fechaRegistro);
  }

  private calcularEdadDiasDesdeFecha(fechaBase: string | Date, fechaReg: string | Date): number {
    const inicio = new Date(fechaBase);
    const reg = new Date(fechaReg);
    const diffTime = reg.getTime() - inicio.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    return Math.max(0, diffDays);
  }

  /** Edad en semanas para un registro (días desde encaset → semanas: floor(días/7)+1, prod ≥ 26). */
  calcularEdadSemanas(fechaRegistro: string | Date): number {
    const dias = this.calcularEdadDias(fechaRegistro);
    return Math.max(26, Math.floor(dias / 7) + 1);
  }

  // ================== CALCULOS ==================
  getTotalHuevos(): number {
    return this.seguimientos.reduce((total, seg) => total + (seg.huevosTotales || 0), 0);
  }
}




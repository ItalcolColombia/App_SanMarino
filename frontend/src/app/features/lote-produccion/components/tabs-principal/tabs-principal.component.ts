import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoteProduccionDto } from '../../services/lote-produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { TablaListaIndicadoresComponent } from '../tabla-lista-indicadores/tabla-lista-indicadores.component';
import { GraficasPrincipalComponent } from '../graficas-principal/graficas-principal.component';

@Component({
  selector: 'app-tabs-principal',
  standalone: true,
  imports: [CommonModule, TablaListaIndicadoresComponent, GraficasPrincipalComponent],
  templateUrl: './tabs-principal.component.html',
  styleUrls: ['./tabs-principal.component.scss']
})
export class TabsPrincipalComponent implements OnInit, OnChanges {
  @Input() registros: LoteProduccionDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() loading: boolean = false;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<LoteProduccionDto>();
  @Output() delete = new EventEmitter<string | number>();

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

  onEdit(registro: LoteProduccionDto): void {
    this.edit.emit(registro);
  }

  onDelete(id: string | number): void {
    this.delete.emit(id);
  }

  // ================== CALCULO DE EDAD ==================
  calcularEdadDias(fechaRegistro: string | Date): number {
    if (!this.selectedLote?.fechaEncaset) return 0;
    
    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaEncaset.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    return Math.max(1, diffDays);
  }
}

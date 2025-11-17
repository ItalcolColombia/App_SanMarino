// src/app/features/lote/pages/lote-list/lote-list.component.ts
import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus } from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil, finalize } from 'rxjs';

import { LoteService, LoteDto } from '../../services/lote.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';

// Componentes separados
import { FiltroLotesComponent, FiltroLotesEvent } from '../../components/filtro-lotes/filtro-lotes.component';
import { TablaRegistroListComponent, TablaLotesEvent } from '../../components/tabla-registro-list/tabla-registro-list.component';
import { ModalCreateEditLoteComponent } from '../../components/modal-create-edit-lote/modal-create-edit-lote.component';

@Component({
  selector: 'app-lote-list',
  standalone: true,
  imports: [
    CommonModule,
    FontAwesomeModule,
    FiltroLotesComponent,
    TablaRegistroListComponent,
    ModalCreateEditLoteComponent
  ],
  templateUrl: './lote-list.component.html',
  styleUrls: ['./lote-list.component.scss']
})
export class LoteListComponent implements OnInit, OnDestroy {
  // Iconos
  faPlus = faPlus;

  // Estado UI
  loading = false;
  modalOpen = false;
  editingLote: LoteDto | null = null;

  // Datos
  lotes: LoteDto[] = [];
  farms: FarmDto[] = [];

  // Filtros
  selectedCompanyId: number | null = null;
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  filtro = '';
  sortKey: 'edad' | 'fecha' = 'edad';
  sortDir: 'asc' | 'desc' = 'desc';

  // Mapas para búsqueda rápida
  farmMap: Record<number, string> = {};

  private destroy$ = new Subject<void>();

  constructor(
    private loteSvc: LoteService,
    private farmSvc: FarmService
  ) {}

  ngOnInit(): void {
    this.loadMasterData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadMasterData(): void {
    this.loading = true;
    
    this.farmSvc.getAll().pipe(takeUntil(this.destroy$)).subscribe(farms => {
      this.farms = farms;
      this.farmMap = {};
      farms.forEach(f => this.farmMap[f.id] = f.name);
      this.loadLotes();
    });
  }

  private loadLotes(): void {
    this.loading = true;
    this.loteSvc.getAll()
      .pipe(
        finalize(() => this.loading = false),
        takeUntil(this.destroy$)
      )
      .subscribe(lotes => {
        this.lotes = lotes;
        this.applyFilters();
      });
  }

  // Eventos de filtros
  onFiltrosChanged(event: FiltroLotesEvent): void {
    this.selectedCompanyId = event.companyId;
    this.selectedFarmId = event.farmId;
    this.selectedNucleoId = event.nucleoId;
    this.selectedGalponId = event.galponId;
    this.filtro = event.searchTerm;
    this.sortKey = event.sortKey;
    this.sortDir = event.sortDir;
    
    this.applyFilters();
  }

  onResetFiltros(): void {
    this.selectedCompanyId = null;
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.filtro = '';
    this.sortKey = 'edad';
    this.sortDir = 'desc';
    
    this.applyFilters();
  }

  private applyFilters(): void {
    let filteredLotes = [...this.lotes];

    // Si no hay granja seleccionada, no mostrar lotes
    if (!this.selectedFarmId) {
      filteredLotes = [];
    } else {
      // Filtrar obligatoriamente por granja seleccionada
      filteredLotes = filteredLotes.filter(l => l.granjaId === this.selectedFarmId);

      // Filtros adicionales (opcionales)
      if (this.selectedCompanyId != null) {
        filteredLotes = filteredLotes.filter(l => this.farmMap[l.granjaId] && this.farms.find(f => f.id === l.granjaId)?.companyId === this.selectedCompanyId);
      }
      if (this.selectedNucleoId != null) {
        filteredLotes = filteredLotes.filter(l => (l.nucleoId ?? null) === this.selectedNucleoId);
      }
      if (this.selectedGalponId != null) {
        filteredLotes = filteredLotes.filter(l => (l.galponId ?? null) === this.selectedGalponId);
      }

      // Búsqueda
      if (this.filtro) {
        const term = this.normalize(this.filtro);
        filteredLotes = filteredLotes.filter(l => {
          const haystack = [
            l.loteId ?? 0,
            l.loteNombre ?? '',
            l.nucleoId ?? '',
            this.farmMap[l.granjaId] ?? '',
            l.galponId ?? ''
          ].map(s => this.normalize(String(s))).join(' ');
          return haystack.includes(term);
        });
      }

      // Ordenamiento
      filteredLotes = this.sortLotes(filteredLotes);
    }

    // Actualizar la lista de lotes filtrados
    this.lotes = filteredLotes;
  }

  private sortLotes(lotes: LoteDto[]): LoteDto[] {
    return lotes.sort((a, b) => {
      let aVal: any, bVal: any;
      
      if (this.sortKey === 'edad') {
        aVal = this.calcularEdadDias(a.fechaEncaset);
        bVal = this.calcularEdadDias(b.fechaEncaset);
      } else {
        aVal = new Date(a.fechaEncaset || 0).getTime();
        bVal = new Date(b.fechaEncaset || 0).getTime();
      }
      
      if (this.sortDir === 'asc') {
        return aVal - bVal;
      } else {
        return bVal - aVal;
      }
    });
  }

  private calcularEdadDias(fechaEncaset?: string | Date | null): number {
    if (!fechaEncaset) return 0;
    const inicio = new Date(fechaEncaset);
    const hoy = new Date();
    const msDia = 1000 * 60 * 60 * 24;
    return Math.floor((hoy.getTime() - inicio.getTime()) / msDia) + 1;
  }

  private normalize(str: string): string {
    return str.toLowerCase().trim().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  }

  // Eventos de la tabla
  onLoteAction(event: TablaLotesEvent): void {
    switch (event.type) {
      case 'edit':
        this.openModal(event.lote);
        break;
      case 'delete':
        this.deleteLote(event.lote);
        break;
      case 'view':
        this.viewLote(event.lote);
        break;
    }
  }

  onCreateNew(): void {
    this.openModal();
  }

  private openModal(lote?: LoteDto): void {
    this.editingLote = lote || null;
    this.modalOpen = true;
  }

  onModalClose(): void {
    this.modalOpen = false;
    this.editingLote = null;
  }

  onLoteSaved(savedLote: LoteDto): void {
    // Recargar la lista de lotes
    this.loadLotes();
    this.onModalClose();
  }

  private deleteLote(lote: LoteDto): void {
    if (!confirm(`¿Eliminar lote "${lote.loteNombre}"?`)) return;
    
    this.loading = true;
    this.loteSvc.delete(lote.loteId)
      .pipe(
        finalize(() => this.loading = false),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        this.loadLotes();
      });
  }

  private viewLote(lote: LoteDto): void {
    // TODO: Implementar vista de detalle del lote
    console.log('Ver detalle del lote:', lote);
  }

  // Getters para el template
  get selectedFarmName(): string {
    if (!this.selectedFarmId) return '';
    return this.farmMap[this.selectedFarmId] || '';
  }

  get hasFarmSelected(): boolean {
    return this.selectedFarmId !== null;
  }
}

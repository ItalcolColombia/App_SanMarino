// frontend/src/app/features/lote-engorde/components/asignar-granjas-lote-base/asignar-granjas-lote-base.component.ts
import { Component, Input, Output, EventEmitter, OnChanges, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faBuilding, faPlus, faTrash, faSearch, faTimes } from '@fortawesome/free-solid-svg-icons';
import { forkJoin } from 'rxjs';

import { LoteBaseEngordeApi, LoteBaseEngordeGranjaDto } from '../../../engorde-comun/services/lote-base-engorde.api';
import { FarmService, Farm } from '../../../../core/services/farm/farm.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';

/**
 * Modal "Asignar granjas" de un lote base de engorde. Reutiliza el mismo criterio de
 * granjas asignables que Usuarios (`FarmService.getAssignableFarms()`): Admin de Empresa /
 * Super Admin ven todas las granjas de la empresa; el resto solo las asignadas.
 * Asignar/quitar una granja controla en qué granjas es visible el lote base al crear un lote.
 */
@Component({
  selector: 'app-asignar-granjas-lote-base',
  standalone: true,
  imports: [FormsModule, FontAwesomeModule],
  templateUrl: './asignar-granjas-lote-base.component.html',
  changeDetection: ChangeDetectionStrategy.Eager
})
export class AsignarGranjasLoteBaseComponent implements OnChanges {
  @Input() loteBaseId: number | null = null;
  @Input() loteBaseNombre = '';
  @Input() isOpen = false;

  @Output() close = new EventEmitter<void>();
  /** Se emite cuando cambia el conjunto de granjas asignadas (para refrescar la lista externa). */
  @Output() updated = new EventEmitter<void>();

  faBuilding = faBuilding;
  faPlus = faPlus;
  faTrash = faTrash;
  faSearch = faSearch;
  faTimes = faTimes;

  loading = false;
  saving = false;
  searchTerm = '';

  asignadas: LoteBaseEngordeGranjaDto[] = [];
  disponibles: Farm[] = [];

  private lastLoadedId: number | null = null;

  constructor(
    private loteBaseApi: LoteBaseEngordeApi,
    private farmService: FarmService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService
  ) {}

  ngOnChanges(): void {
    if (this.isOpen && this.loteBaseId != null && this.loteBaseId !== this.lastLoadedId) {
      this.loadData();
    }
    if (!this.isOpen) {
      this.lastLoadedId = null;
      this.searchTerm = '';
    }
  }

  private loadData(): void {
    const id = this.loteBaseId;
    if (id == null) return;
    this.lastLoadedId = id;
    this.loading = true;
    forkJoin({
      asignadas: this.loteBaseApi.getGranjas(id),
      todas: this.farmService.getAssignableFarms()
    }).subscribe({
      next: ({ asignadas, todas }) => {
        this.asignadas = asignadas ?? [];
        this.disponibles = (todas ?? []).filter(f => f.status === 'A');
        this.loading = false;
      },
      error: () => {
        this.toast.error('No se pudieron cargar las granjas del lote base.');
        this.loading = false;
      }
    });
  }

  /** Granjas disponibles (no asignadas), filtradas por búsqueda. Referencia recalculada solo en CD. */
  get filteredDisponibles(): Farm[] {
    const asignadasIds = new Set(this.asignadas.map(a => a.farmId));
    const term = this.searchTerm.trim().toLowerCase();
    return this.disponibles
      .filter(f => !asignadasIds.has(f.id))
      .filter(f => !term || (f.name || '').toLowerCase().includes(term));
  }

  get filteredAsignadas(): LoteBaseEngordeGranjaDto[] {
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return this.asignadas;
    return this.asignadas.filter(a => (a.farmName || '').toLowerCase().includes(term));
  }

  asignar(farm: Farm): void {
    if (this.loteBaseId == null || this.saving) return;
    this.saving = true;
    this.loteBaseApi.assignGranja(this.loteBaseId, farm.id).subscribe({
      next: (g) => {
        this.asignadas = [...this.asignadas, g].sort((a, b) => (a.farmName || '').localeCompare(b.farmName || '', 'es'));
        this.saving = false;
        this.updated.emit();
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(err?.error?.message || 'No se pudo asignar la granja.');
      }
    });
  }

  async quitar(g: LoteBaseEngordeGranjaDto): Promise<void> {
    if (this.loteBaseId == null) return;
    if (!(await this.confirmDialog.ask({
      title: 'Quitar granja',
      message: `¿Quitar la granja "${g.farmName}" del lote base "${this.loteBaseNombre}"? Dejará de aparecer al crear lotes en esa granja.`,
      type: 'warning',
      confirmText: 'Quitar'
    }))) return;

    this.saving = true;
    this.loteBaseApi.unassignGranja(this.loteBaseId, g.farmId).subscribe({
      next: () => {
        this.asignadas = this.asignadas.filter(a => a.farmId !== g.farmId);
        this.saving = false;
        this.updated.emit();
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(err?.error?.message || 'No se pudo quitar la granja.');
      }
    });
  }

  closeModal(): void {
    this.close.emit();
  }

  clearSearch(): void {
    this.searchTerm = '';
  }

  trackByFarmId = (_: number, f: Farm) => f.id;
  trackByAsignadaId = (_: number, g: LoteBaseEngordeGranjaDto) => g.farmId;
}

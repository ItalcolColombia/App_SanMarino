// Seguimiento Diario Lote Reproductora Aves de Engorde.
// Filtros: Granja → Núcleo → Galpón → Lote Aves Engorde → Lote Reproductora.
// API: SeguimientoDiarioLoteReproductora (tabla seguimiento_diario_lote_reproductora_aves_engorde).
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';

import {
  SeguimientoDiarioLoteReproductoraService,
  SeguimientoDiarioLoteReproductoraFilterDataDto,
  SeguimientoLoteLevanteDto,
  CreateSeguimientoDiarioLoteReproductoraDto,
  UpdateSeguimientoDiarioLoteReproductoraDto,
  LoteReproductoraSeguimientoFilterItemDto
} from '../../services/seguimiento-diario-lote-reproductora.service';
import type { CreateSeguimientoLoteLevanteDto, UpdateSeguimientoLoteLevanteDto } from '../../../lote-levante/services/seguimiento-lote-levante.service';
import { ModalCreateEditComponent } from '../../../lote-levante/pages/modal-create-edit/modal-create-edit.component';
import { ModalDetalleSeguimientoLevanteComponent } from '../../../lote-levante/pages/modal-detalle-seguimiento/modal-detalle-seguimiento.component';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { LoteReproductoraAveEngordeService, LoteReproductoraAveEngordeDto } from '../../../lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faFilter, faEye } from '@fortawesome/free-solid-svg-icons';

@Component({
  selector: 'app-seguimiento-diario-lote-reproductora-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ModalCreateEditComponent,
    ModalDetalleSeguimientoLevanteComponent,
    ConfirmationModalComponent,
    FontAwesomeModule
  ],
  templateUrl: './seguimiento-diario-lote-reproductora-list.component.html',
  styleUrls: ['./seguimiento-diario-lote-reproductora-list.component.scss']
})
export class SeguimientoDiarioLoteReproductoraListComponent implements OnInit {
  readonly SIN_GALPON = '__SIN_GALPON__';

  granjas: Array<{ id: number; name: string }> = [];
  nucleos: Array<{ nucleoId: string; nucleoNombre: string; granjaId: number }> = [];
  galpones: Array<{ id: string; label: string }> = [];
  lotes: Array<{ loteId: number; loteNombre: string; granjaId: number; nucleoId: string | null; galponId: string | null }> = [];
  /** Lotes reproductora (del filter-data), filtrados por selectedLoteId para el 5º dropdown */
  lotesReproductoraFiltered: LoteReproductoraSeguimientoFilterItemDto[] = [];

  private allNucleos: Array<{ nucleoId: string; nucleoNombre: string; granjaId: number }> = [];
  private allGalpones: Array<{ galponId: string; galponNombre: string; nucleoId: string; granjaId: number }> = [];
  private allLotes: Array<{ loteId: number; loteNombre: string; granjaId: number; nucleoId: string | null; galponId: string | null }> = [];
  private allLotesReproductora: LoteReproductoraSeguimientoFilterItemDto[] = [];
  private galponNameById = new Map<string, string>();

  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  /** Lote Aves de Engorde (4º dropdown) */
  selectedLoteId: number | null = null;
  /** Lote Reproductora Aves de Engorde id (5º dropdown) */
  selectedLoteReproductoraId: number | null = null;

  /** Detalle del lote reproductora seleccionado (info + aves disponibles) */
  selectedReproductoraDetail: LoteReproductoraAveEngordeDto | null = null;

  hasSinGalpon = false;
  seguimientos: SeguimientoLoteLevanteDto[] = [];
  loading = false;
  modalOpen = false;
  detailModalOpen = false;
  editing: SeguimientoLoteLevanteDto | null = null;

  /** Para el modal create/edit: "lotes" = solo el lote reproductora seleccionado (loteId = id del lote reproductora) */
  lotesParaModal: LoteDto[] = [];
  confirmModalOpen = false;
  confirmModalData: ConfirmationModalData = {
    title: 'Eliminar registro',
    message: '¿Estás seguro de que deseas eliminar este registro de seguimiento diario?',
    type: 'warning',
    confirmText: 'Eliminar',
    cancelText: 'Cancelar',
    showCancel: true
  };
  private pendingDeleteId: number | null = null;

  /** Modal informativo cuando el lote reproductora seleccionado está cerrado */
  loteCerradoModalOpen = false;
  loteCerradoModalData: ConfirmationModalData = {
    title: 'Lote reproductora cerrado',
    message: 'El lote reproductora seleccionado está cerrado. No se pueden agregar nuevos registros de seguimiento. La información mostrada es solo de consulta.',
    type: 'info',
    confirmText: 'Entendido',
    showCancel: false
  };

  /** True si el lote reproductora actual está cerrado (no se permite nuevo registro). */
  get isLoteReproductoraCerrado(): boolean {
    return this.selectedReproductoraDetail?.estado === 'Cerrado';
  }

  faPlus = faPlus;
  faPen = faPen;
  faTrash = faTrash;
  faFilter = faFilter;
  faEye = faEye;

  constructor(
    private segSvc: SeguimientoDiarioLoteReproductoraService,
    private loteReproductoraSvc: LoteReproductoraAveEngordeService,
    private toastService: ToastService
  ) {}

  get selectedGranjaName(): string {
    return this.granjas.find(g => g.id === this.selectedGranjaId)?.name ?? '';
  }
  get selectedNucleoNombre(): string {
    return this.nucleos.find(n => n.nucleoId === this.selectedNucleoId)?.nucleoNombre ?? (this.selectedNucleoId ?? '');
  }
  get selectedGalponNombre(): string {
    if (this.selectedGalponId === this.SIN_GALPON) return '— Sin galpón —';
    return this.galponNameById.get((this.selectedGalponId ?? '').trim()) || (this.selectedGalponId ?? '');
  }
  get selectedLoteNombre(): string {
    const l = this.lotes.find(x => x.loteId === this.selectedLoteId);
    return l?.loteNombre ?? (this.selectedLoteId?.toString() ?? '');
  }
  get selectedLoteReproductoraNombre(): string {
    const r = this.lotesReproductoraFiltered.find(x => x.id === this.selectedLoteReproductoraId);
    return r?.nombreLote ?? (this.selectedLoteReproductoraId?.toString() ?? '');
  }

  ngOnInit(): void {
    this.loading = true;
    this.segSvc.getFilterData().subscribe({
      next: (data: SeguimientoDiarioLoteReproductoraFilterDataDto) => {
        this.granjas = [...(data.farms ?? [])];
        this.allNucleos = [...(data.nucleos ?? [])];
        this.allGalpones = [...(data.galpones ?? [])];
        this.allLotes = [...(data.lotes ?? [])];
        this.allLotesReproductora = [...(data.lotesReproductora ?? [])];
        this.galponNameById.clear();
        (data.galpones ?? []).forEach(g => {
          if (g.galponId) this.galponNameById.set(String(g.galponId).trim(), (g.galponNombre || g.galponId).trim());
        });
        this.loading = false;
      },
      error: () => {
        this.granjas = [];
        this.allNucleos = [];
        this.allGalpones = [];
        this.allLotes = [];
        this.allLotesReproductora = [];
        this.loading = false;
      }
    });
  }

  onGranjaChange(): void {
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.selectedLoteReproductoraId = null;
    this.seguimientos = [];
    this.nucleos = [];
    this.galpones = [];
    this.lotes = [];
    this.lotesReproductoraFiltered = [];
    if (!this.selectedGranjaId) return;
    const gid = Number(this.selectedGranjaId);
    this.nucleos = this.allNucleos.filter(n => n.granjaId === gid);
    this.applyFiltersToLotes();
    this.buildGalpones();
  }

  onNucleoChange(): void {
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.selectedLoteReproductoraId = null;
    this.seguimientos = [];
    this.galpones = [];
    this.lotes = [];
    this.lotesReproductoraFiltered = [];
    this.applyFiltersToLotes();
    this.buildGalpones();
  }

  onGalponChange(): void {
    this.selectedLoteId = null;
    this.selectedLoteReproductoraId = null;
    this.seguimientos = [];
    this.lotes = [];
    this.lotesReproductoraFiltered = [];
    this.applyFiltersToLotes();
  }

  onLoteChange(): void {
    this.selectedLoteReproductoraId = null;
    this.selectedReproductoraDetail = null;
    this.seguimientos = [];
    this.lotesReproductoraFiltered = this.selectedLoteId != null
      ? this.allLotesReproductora.filter(r => r.loteAveEngordeId === this.selectedLoteId)
      : [];
  }

  /** granjaId del lote aves engorde seleccionado; el modal lo usa para cargar inventario (alimentos). */
  private getGranjaIdForModal(): number {
    if (this.selectedLoteId == null) return 0;
    return this.lotes.find(l => l.loteId === this.selectedLoteId)?.granjaId ?? 0;
  }

  onLoteReproductoraChange(): void {
    this.seguimientos = [];
    this.selectedReproductoraDetail = null;
    if (!this.selectedLoteReproductoraId) return;
    this.loading = true;
    const granjaId = this.getGranjaIdForModal();
    const repId = this.selectedLoteReproductoraId;
    this.segSvc.getByLoteReproductoraId(repId)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: rows => (this.seguimientos = rows ?? []),
        error: () => (this.seguimientos = [])
      });
    this.loteReproductoraSvc.getById(repId).subscribe({
      next: detail => {
        this.selectedReproductoraDetail = detail;
        if (detail?.estado === 'Cerrado') {
          this.loteCerradoModalOpen = true;
        }
      },
      error: () => (this.selectedReproductoraDetail = null)
    });
    this.lotesParaModal = this.lotesReproductoraFiltered
      .filter(r => r.id === this.selectedLoteReproductoraId)
      .map(r => ({ loteId: r.id, loteNombre: r.nombreLote, granjaId, nucleoId: null, galponId: null } as LoteDto));
  }

  private applyFiltersToLotes(): void {
    if (!this.selectedGranjaId) {
      this.lotes = [];
      return;
    }
    const gid = String(this.selectedGranjaId);
    let filtered = this.allLotes.filter(l => String(l.granjaId) === gid);
    if (this.selectedNucleoId)
      filtered = filtered.filter(l => String(l.nucleoId) === String(this.selectedNucleoId));
    if (!this.selectedGalponId) {
      this.lotes = filtered;
      return;
    }
    if (this.selectedGalponId === this.SIN_GALPON) {
      this.lotes = filtered.filter(l => !this.hasValue(l.galponId));
      return;
    }
    const sel = this.normalizeId(this.selectedGalponId);
    this.lotes = filtered.filter(l => this.normalizeId(l.galponId) === sel);
  }

  private buildGalpones(): void {
    if (!this.selectedGranjaId) {
      this.galpones = [];
      this.hasSinGalpon = false;
      return;
    }
    const gid = Number(this.selectedGranjaId);
    const nid = this.selectedNucleoId ? String(this.selectedNucleoId) : null;
    const list = this.allGalpones.filter(g => g.granjaId === gid && (!nid || g.nucleoId === nid));
    this.galpones = list.map(g => ({ id: String(g.galponId).trim(), label: (g.galponNombre || g.galponId).trim() }));
    this.hasSinGalpon = this.allLotes.some(l =>
      l.granjaId === gid && (!nid || String(l.nucleoId) === nid) && !this.hasValue(l.galponId)
    );
    if (this.hasSinGalpon) this.galpones.unshift({ id: this.SIN_GALPON, label: '— Sin galpón —' });
    this.galpones.sort((a, b) => a.label.localeCompare(b.label, 'es', { numeric: true, sensitivity: 'base' }));
  }

  create(): void {
    if (!this.selectedLoteReproductoraId || this.isLoteReproductoraCerrado) return;
    this.editing = null;
    const granjaId = this.getGranjaIdForModal();
    this.lotesParaModal = this.lotesReproductoraFiltered.map(r => ({
      loteId: r.id,
      loteNombre: r.nombreLote,
      granjaId,
      nucleoId: null,
      galponId: null
    } as LoteDto));
    this.modalOpen = true;
  }

  edit(seg: SeguimientoLoteLevanteDto): void {
    if (this.isLoteReproductoraCerrado) return;
    this.editing = seg;
    const granjaId = this.getGranjaIdForModal();
    this.lotesParaModal = this.lotesReproductoraFiltered.map(r => ({
      loteId: r.id,
      loteNombre: r.nombreLote,
      granjaId,
      nucleoId: null,
      galponId: null
    } as LoteDto));
    this.modalOpen = true;
  }

  viewDetail(seg: SeguimientoLoteLevanteDto): void {
    this.editing = seg;
    this.detailModalOpen = true;
  }

  cancel(): void {
    this.modalOpen = false;
    this.detailModalOpen = false;
    this.editing = null;
  }

  /** Payload del modal (mismo shape que CreateSeguimientoDiarioLoteReproductoraDto). Envía a API SeguimientoDiarioLoteReproductora → tabla seguimiento_diario_lote_reproductora_aves_engorde. */
  onSave(event: { data: CreateSeguimientoLoteLevanteDto | UpdateSeguimientoLoteLevanteDto; isEdit: boolean }): void {
    const op$ = event.isEdit
      ? this.segSvc.update(event.data as UpdateSeguimientoDiarioLoteReproductoraDto)
      : this.segSvc.create(event.data as CreateSeguimientoDiarioLoteReproductoraDto);
    this.loading = true;
    op$.pipe(finalize(() => (this.loading = false))).subscribe({
      next: () => {
        this.modalOpen = false;
        this.editing = null;
        this.toastService.success(event.isEdit ? 'Registro actualizado.' : 'Registro creado.', 'Éxito', 4000);
        if (this.selectedLoteReproductoraId != null) {
          this.segSvc.getByLoteReproductoraId(this.selectedLoteReproductoraId).subscribe({
            next: rows => (this.seguimientos = rows ?? [])
          });
          this.loteReproductoraSvc.getById(this.selectedLoteReproductoraId).subscribe({
            next: detail => (this.selectedReproductoraDetail = detail),
            error: () => (this.selectedReproductoraDetail = null)
          });
        }
      },
      error: err => {
        this.toastService.error(
          err?.error?.message || err?.message || 'Error al guardar.',
          'Error',
          6000
        );
      }
    });
  }

  delete(id: number): void {
    if (this.isLoteReproductoraCerrado) return;
    this.pendingDeleteId = id;
    this.confirmModalOpen = true;
  }

  onConfirmDelete(): void {
    if (this.pendingDeleteId == null) {
      this.confirmModalOpen = false;
      return;
    }
    const id = this.pendingDeleteId;
    this.pendingDeleteId = null;
    this.confirmModalOpen = false;
    this.loading = true;
    this.segSvc.delete(id).pipe(finalize(() => (this.loading = false))).subscribe({
      next: () => {
        this.toastService.success('Registro eliminado.', 'Éxito', 4000);
        if (this.selectedLoteReproductoraId != null) {
          this.segSvc.getByLoteReproductoraId(this.selectedLoteReproductoraId).subscribe({
            next: rows => (this.seguimientos = rows ?? [])
          });
          this.loteReproductoraSvc.getById(this.selectedLoteReproductoraId).subscribe({
            next: detail => (this.selectedReproductoraDetail = detail),
            error: () => (this.selectedReproductoraDetail = null)
          });
        }
      },
      error: err => {
        this.toastService.error(err?.error?.message || err?.message || 'Error al eliminar.', 'Error', 6000);
      }
    });
  }

  onCancelDelete(): void {
    this.pendingDeleteId = null;
    this.confirmModalOpen = false;
  }

  trackById = (_: number, r: SeguimientoLoteLevanteDto) => r.id;
  trackByIdx = (i: number) => i;

  private hasValue(v: unknown): boolean {
    if (v === null || v === undefined) return false;
    const s = String(v).trim().toLowerCase();
    return !(s === '' || s === '0' || s === 'null' || s === 'undefined');
  }
  private normalizeId(v: unknown): string {
    if (v === null || v === undefined) return '';
    return String(v).trim();
  }
}

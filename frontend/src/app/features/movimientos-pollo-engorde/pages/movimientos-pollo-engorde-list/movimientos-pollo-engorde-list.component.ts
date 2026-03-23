import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import {
  MovimientoPolloEngordeService,
  MovimientoPolloEngordeDto,
  ResumenAvesLoteDto
} from '../../services/movimiento-pollo-engorde.service';
import { FarmDto } from '../../../farm/services/farm.service';
import { NucleoDto } from '../../../lote-produccion/services/nucleo.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { LoteEngordeService, LoteAveEngordeDto } from '../../../lote-engorde/services/lote-engorde.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import { ModalMovimientoPolloEngordeComponent } from '../../components/modal-movimiento-pollo-engorde/modal-movimiento-pollo-engorde.component';
import { ToastService } from '../../../../shared/services/toast.service';

/** Opción del dropdown Lote (solo Ave Engorde). */
export interface LoteOption {
  value: string; // "ae-123"
  tipo: 'ae';
  id: number;
  label: string;
}

@Component({
  selector: 'app-movimientos-pollo-engorde-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ConfirmationModalComponent,
    ModalMovimientoPolloEngordeComponent
  ],
  templateUrl: './movimientos-pollo-engorde-list.component.html',
  styleUrls: ['./movimientos-pollo-engorde-list.component.scss']
})
export class MovimientosPolloEngordeListComponent implements OnInit {
  readonly SIN_GALPON = '__SIN_GALPON__';

  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: Array<{ id: string; label: string }> = [];
  lotesOpciones: LoteOption[] = [];

  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteValue: string | null = null; // "ae-123"

  allLoteAveEngorde: LoteAveEngordeDto[] = [];
  /** Catálogo completo desde filter-data (se filtra en cliente al elegir granja/núcleo). */
  private allNucleosFull: NucleoDto[] = [];
  private allGalponesFull: GalponDetailDto[] = [];

  movimientos: MovimientoPolloEngordeDto[] = [];
  filteredMovimientos: MovimientoPolloEngordeDto[] = [];

  /** Detalle del lote Ave Engorde seleccionado para la tabla informativa. */
  loteDetalleAveEngorde: LoteAveEngordeDto | null = null;
  loadingLoteDetalle = false;
  /** Resumen de aves del lote (inicio, salidas, vendidas, actuales) para reporte. */
  resumenAvesLote: ResumenAvesLoteDto | null = null;
  loadingResumen = false;

  filtroBusqueda = '';
  filtroTipoMovimiento = '';
  filtroEstado = '';

  loading = false;
  error: string | null = null;
  modalOpen = false;
  editingMovimiento: MovimientoPolloEngordeDto | null = null;

  showConfirmationModal = false;
  confirmationModalData: ConfirmationModalData = {
    title: 'Confirmar',
    message: '¿Estás seguro?',
    type: 'info',
    confirmText: 'Confirmar',
    cancelText: 'Cancelar',
    showCancel: true
  };
  movimientoToDelete: MovimientoPolloEngordeDto | null = null;
  movimientoToComplete: MovimientoPolloEngordeDto | null = null;

  private galponNameById = new Map<string, string>();

  get selectedGranjaName(): string {
    const g = this.granjas.find((x) => x.id === this.selectedGranjaId);
    return g?.name ?? '';
  }

  get selectedNucleoNombre(): string {
    const n = this.nucleos.find((x) => x.nucleoId === this.selectedNucleoId);
    return n?.nucleoNombre ?? '';
  }

  get selectedGalponNombre(): string {
    if (this.selectedGalponId === this.SIN_GALPON) return '— Sin galpón —';
    const id = (this.selectedGalponId ?? '').trim();
    return (this.galponNameById.get(id) || this.selectedGalponId) ?? '';
  }

  get selectedLoteNombre(): string {
    const opt = this.lotesOpciones.find((x) => x.value === this.selectedLoteValue);
    return opt?.label ?? '—';
  }

  get hasLoteSelected(): boolean {
    return !!this.selectedLoteValue;
  }

  get loteAveEngordeOrigenId(): number | null {
    if (!this.selectedLoteValue || !this.selectedLoteValue.startsWith('ae-')) return null;
    const id = parseInt(this.selectedLoteValue.replace('ae-', ''), 10);
    return isNaN(id) ? null : id;
  }

  constructor(
    private loteEngordeSvc: LoteEngordeService,
    private movimientoSvc: MovimientoPolloEngordeService,
    private toastService: ToastService
  ) {}

  ngOnInit(): void {
    this.filteredMovimientos = [];
    this.loading = true;
    this.movimientoSvc
      .getFilterData()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => {
          this.granjas = data.farms ?? [];
          this.allNucleosFull = data.nucleos ?? [];
          this.allGalponesFull = data.galpones ?? [];
          this.allLoteAveEngorde = data.lotesAveEngorde ?? [];
        },
        error: () => {
          this.granjas = [];
          this.allNucleosFull = [];
          this.allGalponesFull = [];
          this.allLoteAveEngorde = [];
          this.toastService.error('No se pudieron cargar los filtros. Revise la sesión o intente de nuevo.');
        }
      });
  }

  onGranjaChange(granjaId: number | null): void {
    this.selectedGranjaId = granjaId;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteValue = null;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.galpones = [];
    this.lotesOpciones = [];
    this.nucleos = [];

    if (!this.selectedGranjaId) return;

    this.nucleos = this.allNucleosFull.filter((n) => n.granjaId === this.selectedGranjaId);
    this.fillGalponMapFromCache();
    this.buildLotesOpciones();
  }

  onNucleoChange(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId;
    this.selectedGalponId = null;
    this.selectedLoteValue = null;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.selectedLoteValue = null;
    this.buildLotesOpciones();
    this.fillGalponMapFromCache();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteValue = null;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.buildLotesOpciones();
  }

  onLoteChange(value: string | null): void {
    this.selectedLoteValue = value;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.loteDetalleAveEngorde = null;
    if (!this.selectedLoteValue) return;
    this.resumenAvesLote = null;
    this.loadLoteDetalle();
    this.loadMovimientos();
  }

  /** Nombres de galpón desde el catálogo precargado (misma lógica que por API, sin peticiones extra). */
  private fillGalponMapFromCache(): void {
    this.galponNameById.clear();
    if (!this.selectedGranjaId) return;
    let rows = this.allGalponesFull.filter((g) => g.granjaId === this.selectedGranjaId);
    if (this.selectedNucleoId) {
      const nid = String(this.selectedNucleoId);
      rows = rows.filter((g) => String(g.nucleoId) === nid);
    }
    this.fillGalponMap(rows);
  }

  private fillGalponMap(rows: GalponDetailDto[] | null | undefined): void {
    for (const g of rows || []) {
      const id = String(g.galponId).trim();
      if (id) this.galponNameById.set(id, (g.galponNombre || id).trim());
    }
    this.buildGalponesFromLotes();
  }

  private buildGalponesFromLotes(): void {
    if (!this.selectedGranjaId) {
      this.galpones = [];
      return;
    }
    const gid = String(this.selectedGranjaId);
    let base = this.allLoteAveEngorde.filter((l) => String(l.granjaId) === gid);
    if (this.selectedNucleoId) {
      const nid = String(this.selectedNucleoId);
      base = base.filter((l) => (l.nucleoId ?? '') === nid);
    }
    const seen = new Set<string>();
    const result: Array<{ id: string; label: string }> = [];
    for (const l of base) {
      const id = (l.galponId ?? '').trim();
      if (!id || seen.has(id)) continue;
      seen.add(id);
      result.push({ id, label: this.galponNameById.get(id) || id });
    }
    if (base.some((l) => !this.hasValue(l.galponId))) {
      result.unshift({ id: this.SIN_GALPON, label: '— Sin galpón —' });
    }
    this.galpones = result.sort((a, b) => a.label.localeCompare(b.label, 'es', { numeric: true }));
  }

  private buildLotesOpciones(): void {
    if (!this.selectedGranjaId) {
      this.lotesOpciones = [];
      return;
    }
    const gid = String(this.selectedGranjaId);
    const nid = this.selectedNucleoId ? String(this.selectedNucleoId) : null;
    const gpid = this.selectedGalponId && this.selectedGalponId !== this.SIN_GALPON ? String(this.selectedGalponId).trim() : null;

    let filteredAE = this.allLoteAveEngorde.filter((l) => String(l.granjaId) === gid);
    if (nid) filteredAE = filteredAE.filter((l) => (l.nucleoId ?? '') === nid);
    if (gpid) {
      filteredAE = filteredAE.filter((l) => (l.galponId ?? '').trim() === gpid);
    } else if (this.selectedGalponId === this.SIN_GALPON) {
      filteredAE = filteredAE.filter((l) => !this.hasValue(l.galponId));
    }

    const options: LoteOption[] = [];
    for (const l of filteredAE) {
      const id = l.loteAveEngordeId;
      if (id == null) continue;
      options.push({
        value: `ae-${id}`,
        tipo: 'ae',
        id,
        label: `Ave Engorde: ${l.loteNombre || id}`
      });
    }
    this.lotesOpciones = options.sort((a, b) => a.label.localeCompare(b.label, 'es', { numeric: true }));
  }

  /** Carga el detalle del lote Ave Engorde seleccionado para la tabla informativa. */
  private loadLoteDetalle(): void {
    const aeId = this.loteAveEngordeOrigenId;
    this.loteDetalleAveEngorde = null;
    if (aeId == null) return;

    this.loadingLoteDetalle = true;
    this.loteEngordeSvc
      .getById(aeId)
      .pipe(finalize(() => (this.loadingLoteDetalle = false)))
      .subscribe({
        next: (dto) => {
          this.loteDetalleAveEngorde = dto;
          this.loadResumenAvesLote('LoteAveEngorde', aeId);
        },
        error: () => (this.loadingLoteDetalle = false)
      });
  }

  private loadResumenAvesLote(tipo: 'LoteAveEngorde', id: number): void {
    this.loadingResumen = true;
    this.movimientoSvc
      .getResumenAvesLote(tipo, id)
      .pipe(finalize(() => (this.loadingResumen = false)))
      .subscribe({
        next: (r) => (this.resumenAvesLote = r),
        error: () => (this.resumenAvesLote = null)
      });
  }

  private loadMovimientos(): void {
    const aeId = this.loteAveEngordeOrigenId;
    if (aeId == null) return;

    this.loading = true;
    this.error = null;
    this.movimientoSvc
      .getByLoteOrigen(aeId, undefined)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (list) => {
          this.movimientos = list ?? [];
          this.aplicarFiltros();
        },
        error: (err) => {
          this.error = err?.message ?? 'Error al cargar movimientos';
          this.movimientos = [];
          this.filteredMovimientos = [];
        }
      });
  }

  aplicarFiltros(): void {
    let filtered = [...this.movimientos];
    const term = (this.filtroBusqueda || '').trim().toLowerCase();
    if (term) {
      filtered = filtered.filter((m) => {
        const searchText = [
          m.numeroMovimiento ?? '',
          m.tipoMovimiento ?? '',
          m.loteOrigenNombre ?? '',
          m.loteDestinoNombre ?? '',
          m.granjaOrigenNombre ?? '',
          m.granjaDestinoNombre ?? '',
          m.motivoMovimiento ?? '',
          m.estado ?? ''
        ]
          .join(' ')
          .toLowerCase();
        return searchText.includes(term);
      });
    }
    if (this.filtroTipoMovimiento) filtered = filtered.filter((m) => m.tipoMovimiento === this.filtroTipoMovimiento);
    if (this.filtroEstado) filtered = filtered.filter((m) => m.estado === this.filtroEstado);
    this.filteredMovimientos = filtered;
  }

  onFiltroChange(): void {
    this.aplicarFiltros();
  }

  limpiarFiltros(): void {
    this.filtroBusqueda = '';
    this.filtroTipoMovimiento = '';
    this.filtroEstado = '';
    this.aplicarFiltros();
  }

  create(): void {
    if (!this.hasLoteSelected) return;
    this.editingMovimiento = null;
    this.modalOpen = true;
  }

  viewDetail(m: MovimientoPolloEngordeDto): void {
    this.movimientoSvc.getById(m.id).subscribe({
      next: (full) => {
        this.editingMovimiento = full;
        this.modalOpen = true;
      },
      error: () => {
        this.editingMovimiento = m;
        this.modalOpen = true;
      }
    });
  }

  editMovimiento(m: MovimientoPolloEngordeDto): void {
    if (m.estado === 'Completado' || m.estado === 'Cancelado') return;
    this.movimientoSvc.getById(m.id).subscribe({
      next: (full) => {
        this.editingMovimiento = full;
        this.modalOpen = true;
      },
      error: () => {
        this.editingMovimiento = m;
        this.modalOpen = true;
      }
    });
  }

  closeModal(): void {
    this.modalOpen = false;
    this.editingMovimiento = null;
  }

  onMovimientoSaved(): void {
    this.toastService.success('Movimiento guardado correctamente.');
    this.closeModal();
    if (this.hasLoteSelected) {
      this.loadMovimientos();
      this.refreshResumenIfLoteSelected();
    }
  }

  private refreshResumenIfLoteSelected(): void {
    if (!this.selectedLoteValue) return;
    const aeId = this.loteAveEngordeOrigenId;
    if (aeId != null) this.loadResumenAvesLote('LoteAveEngorde', aeId);
  }

  completarMovimiento(m: MovimientoPolloEngordeDto): void {
    if (m.estado !== 'Pendiente') return;
    this.movimientoToComplete = m;
    this.confirmationModalData = {
      title: 'Completar movimiento',
      message: `¿Completar el movimiento ${m.numeroMovimiento}? Se descontarán ${this.formatearNumero(m.totalAves)} aves del lote origen y se actualizará el inventario.`,
      type: 'info',
      confirmText: 'Sí, completar',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  onConfirmCompletar(): void {
    if (!this.movimientoToComplete) return;
    const m = this.movimientoToComplete;
    this.showConfirmationModal = false;
    this.loading = true;
    this.movimientoSvc
      .complete(m.id)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          this.toastService.success(`Movimiento ${m.numeroMovimiento} completado. Se actualizó el inventario del lote.`);
          this.loadMovimientos();
          this.refreshResumenIfLoteSelected();
          this.movimientoToComplete = null;
        },
        error: (err) => {
          this.showErrorMessage('Error al completar: ' + (err?.message ?? ''));
          this.movimientoToComplete = null;
        }
      });
  }

  deleteMovimiento(m: MovimientoPolloEngordeDto): void {
    if (m.estado === 'Cancelado') {
      this.showErrorMessage('No se puede cancelar un movimiento ya cancelado.');
      return;
    }
    this.movimientoToDelete = m;
    this.confirmationModalData = {
      title: 'Confirmar cancelación',
      message: `¿Cancelar el movimiento ${m.numeroMovimiento}? Esta acción no se puede deshacer.`,
      type: 'warning',
      confirmText: 'Sí, cancelar',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  onConfirmDelete(): void {
    if (!this.movimientoToDelete) return;
    const m = this.movimientoToDelete;
    this.showConfirmationModal = false;
    this.loading = true;
    this.movimientoSvc
      .cancel(m.id, 'Cancelado por usuario')
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          this.showSuccessMessage(`Movimiento ${m.numeroMovimiento} cancelado.`);
          this.loadMovimientos();
          this.movimientoToDelete = null;
        },
        error: (err) => {
          this.showErrorMessage('Error al cancelar: ' + (err?.message ?? ''));
          this.movimientoToDelete = null;
        }
      });
  }

  onCancelDelete(): void {
    this.showConfirmationModal = false;
    this.movimientoToDelete = null;
    this.movimientoToComplete = null;
  }

  showSuccessMessage(message: string): void {
    this.confirmationModalData = {
      title: 'Operación exitosa',
      message,
      type: 'success',
      confirmText: 'Aceptar',
      showCancel: false
    };
    this.showConfirmationModal = true;
  }

  showErrorMessage(message: string): void {
    this.confirmationModalData = {
      title: 'Error',
      message,
      type: 'error',
      confirmText: 'Cerrar',
      showCancel: false
    };
    this.showConfirmationModal = true;
  }

  onConfirmationModalClose(): void {
    this.showConfirmationModal = false;
    this.movimientoToDelete = null;
    this.movimientoToComplete = null;
  }

  private hasValue(v: unknown): boolean {
    if (v == null) return false;
    const s = String(v).trim().toLowerCase();
    return s !== '' && s !== '0' && s !== 'null' && s !== 'undefined';
  }

  formatearNumero(num: number): string {
    return new Intl.NumberFormat('es-CO').format(num);
  }

  /** Total aves en lote Ave Engorde (hembras + machos + mixtas o avesEncasetadas). */
  totalAvesAveEngorde(l: LoteAveEngordeDto | null): number {
    if (!l) return 0;
    const h = l.hembrasL ?? 0;
    const m = l.machosL ?? 0;
    const x = l.mixtas ?? 0;
    if (h + m + x > 0) return h + m + x;
    return l.avesEncasetadas ?? 0;
  }

  /** Disponibilidad en lote para el modal (límite al crear movimiento). */
  get availableBirdsForModal(): { total: number; hembras?: number; machos?: number; mixtas?: number } | null {
    if (this.loteDetalleAveEngorde) {
      const l = this.loteDetalleAveEngorde;
      const h = l.hembrasL ?? 0;
      const m = l.machosL ?? 0;
      const x = l.mixtas ?? 0;
      const total = h + m + x > 0 ? h + m + x : (l.avesEncasetadas ?? 0);
      return { total, hembras: h, machos: m, mixtas: x };
    }
    return null;
  }

  /** Datos del lote seleccionado (raza, año tabla, fecha encasetamiento) para prellenar y calcular edad. */
  get lotInfoForMovement(): { raza?: string | null; anoTablaGenetica?: number | null; fechaEncasetamiento?: string | null } | null {
    if (this.loteDetalleAveEngorde) {
      return {
        raza: this.loteDetalleAveEngorde.raza ?? null,
        anoTablaGenetica: this.loteDetalleAveEngorde.anoTablaGenetica ?? null,
        fechaEncasetamiento: this.loteDetalleAveEngorde.fechaEncaset ?? null
      };
    }
    return null;
  }

  fechaCorta(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    return isNaN(d.getTime()) ? iso : d.toLocaleDateString('es');
  }

  trackById(_: number, m: MovimientoPolloEngordeDto): number {
    return m.id;
  }
}

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
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
import {
  ModalMovimientoPolloEngordeComponent,
  MovimientoPolloEngordeSaveDetail
} from '../../components/modal-movimiento-pollo-engorde/modal-movimiento-pollo-engorde.component';
import { ToastService } from '../../../../shared/services/toast.service';

/** Opción del dropdown Lote (solo Ave Engorde). */
export interface LoteOption {
  value: string; // "ae-123"
  tipo: 'ae';
  id: number;
  label: string;
}

/** Fila agrupada: varios movimientos de venta con el mismo número de despacho (mismo viaje). */
export interface FilaDespachoGrupo {
  kind: 'despacho-grupo';
  clave: string;
  numeroDespacho: string;
  fechaMovimiento: string;
  movimientos: MovimientoPolloEngordeDto[];
}

export interface FilaMovimientoSimple {
  kind: 'simple';
  movimiento: MovimientoPolloEngordeDto;
}

export type FilaTablaMovimiento = FilaDespachoGrupo | FilaMovimientoSimple;

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
  /**
   * Lotes para venta por granja (misma lógica que antes en getter).
   * Referencia estable: si se recalcula con getter nuevo en cada CD, el modal hijo recibe @Input distinto
   * en cada ciclo y Angular puede re-ejecutar ngOnChanges sin parar (congelamiento).
   */
  lotesParaVentaGranjaList: LoteAveEngordeDto[] = [];
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
  /** Sin lote seleccionado: venta desde granja (varios galpones/lotes en un despacho). */
  ventaPorGranjaMode = false;
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
  /** Completar varios movimientos del mismo despacho (sin cambiar backend). */
  movimientoToCompleteGroup: MovimientoPolloEngordeDto[] | null = null;
  movimientoToDeleteGroup: MovimientoPolloEngordeDto[] | null = null;

  /** Despachos multi-lote: filas expandidas para ver lotes. */
  expandedDespacho: Record<string, boolean> = {};

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
    // En este módulo solo se filtra por Granja; no se selecciona lote desde filtros.
    return false;
  }

  /** Nuevo registro: con lote (cualquier tipo) o sin lote solo si hay lotes en la granja (venta por granja). */
  get canOpenNuevoRegistro(): boolean {
    if (!this.selectedGranjaId) return false;
    return this.lotesParaVentaGranjaList.length > 0;
  }

  get loteAveEngordeOrigenId(): number | null {
    if (!this.selectedLoteValue || !this.selectedLoteValue.startsWith('ae-')) return null;
    const id = parseInt(this.selectedLoteValue.replace('ae-', ''), 10);
    return isNaN(id) ? null : id;
  }

  /** Filas de tabla: agrupa ventas con el mismo número de despacho (mismo viaje, varios lotes). */
  get filasTabla(): FilaTablaMovimiento[] {
    return this.buildFilasTabla(this.filteredMovimientos);
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
          this.refreshLotesParaVentaGranja();
        },
        error: () => {
          this.granjas = [];
          this.allNucleosFull = [];
          this.allGalponesFull = [];
          this.allLoteAveEngorde = [];
          this.refreshLotesParaVentaGranja();
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

    if (!this.selectedGranjaId) {
      this.refreshLotesParaVentaGranja();
      return;
    }

    this.nucleos = this.allNucleosFull.filter((n) => n.granjaId === this.selectedGranjaId);
    this.fillGalponMapFromCache();
    this.buildLotesOpciones();
    this.refreshLotesParaVentaGranja();
    this.loadMovimientos();
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
    this.refreshLotesParaVentaGranja();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteValue = null;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.buildLotesOpciones();
    this.loadMovimientos();
  }

  onLoteChange(value: string | null): void {
    this.selectedLoteValue = value;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.loteDetalleAveEngorde = null;
    this.resumenAvesLote = null;
    if (this.selectedLoteValue) {
      this.loadLoteDetalle();
    }
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

  /**
   * Lotes Ave Engorde de la granja (y núcleo si aplica) para venta por despacho;
   * incluye todos los galpones; no filtra por el desplegable de galpón.
   */
  private refreshLotesParaVentaGranja(): void {
    if (!this.selectedGranjaId) {
      this.lotesParaVentaGranjaList = [];
      return;
    }
    const gid = String(this.selectedGranjaId);
    let rows = this.allLoteAveEngorde.filter((l) => String(l.granjaId) === gid);
    if (this.selectedNucleoId) {
      const nid = String(this.selectedNucleoId);
      rows = rows.filter((l) => (l.nucleoId ?? '') === nid);
    }
    this.lotesParaVentaGranjaList = rows.sort((a, b) => {
      const ga = (a.galponId ?? '').localeCompare(b.galponId ?? '', 'es');
      if (ga !== 0) return ga;
      return (a.loteNombre || '').localeCompare(b.loteNombre || '', 'es', { numeric: true });
    });
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

  /** Carga movimientos según granja (global) y filtros en cascada (núcleo, galpón, lote). */
  private loadMovimientos(): void {
    if (!this.selectedGranjaId) return;

    this.loading = true;
    this.error = null;
    const params = this.buildMovimientoSearchParams();
    this.movimientoSvc
      .search(params)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (res) => {
          this.movimientos = res.items ?? [];
          this.aplicarFiltros();
        },
        error: (err) => {
          this.error = err?.message ?? 'Error al cargar movimientos';
          this.movimientos = [];
          this.filteredMovimientos = [];
        }
      });
  }

  private buildMovimientoSearchParams(): {
    page: number;
    pageSize: number;
    sortBy: string;
    sortDesc: boolean;
    granjaOrigenId: number;
    nucleoOrigenId?: string;
    galponOrigenId?: string;
    galponOrigenSinAsignar?: boolean;
    loteAveEngordeOrigenId?: number;
  } {
    const p: {
      page: number;
      pageSize: number;
      sortBy: string;
      sortDesc: boolean;
      granjaOrigenId: number;
      nucleoOrigenId?: string;
      galponOrigenId?: string;
      galponOrigenSinAsignar?: boolean;
      loteAveEngordeOrigenId?: number;
    } = {
      page: 1,
      pageSize: 3000,
      sortBy: 'FechaMovimiento',
      sortDesc: true,
      granjaOrigenId: this.selectedGranjaId!
    };
    if (this.selectedNucleoId) {
      p.nucleoOrigenId = String(this.selectedNucleoId).trim();
    }
    if (this.selectedGalponId === this.SIN_GALPON) {
      p.galponOrigenSinAsignar = true;
    } else if (this.selectedGalponId) {
      p.galponOrigenId = String(this.selectedGalponId).trim();
    }
    if (this.selectedLoteValue?.startsWith('ae-')) {
      const id = parseInt(this.selectedLoteValue.replace('ae-', ''), 10);
      if (!isNaN(id)) p.loteAveEngordeOrigenId = id;
    }
    return p;
  }

  aplicarFiltros(): void {
    let filtered = [...this.movimientos];
    const term = (this.filtroBusqueda || '').trim().toLowerCase();
    if (term) {
      filtered = filtered.filter((m) => {
        const searchText = [
          m.numeroMovimiento ?? '',
          m.tipoMovimiento ?? '',
          m.numeroDespacho ?? '',
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
    if (!this.selectedGranjaId) return;
    if (this.lotesParaVentaGranjaList.length === 0) return;
    // Siempre crear venta por granja (despacho). No se filtra/selecciona por galpón/lote.
    this.ventaPorGranjaMode = true;
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
    this.ventaPorGranjaMode = false;
    this.editingMovimiento = null;
  }

  onMovimientoSaved(detail?: MovimientoPolloEngordeSaveDetail): void {
    const n = detail?.ventaGranjaBatchCount;
    if (n != null && n > 0) {
      this.toastService.success(
        `Se registraron ${this.formatearNumero(n)} movimiento(s) de venta (uno por lote). Quedan pendientes de completar.`
      );
    } else {
      this.toastService.success('Movimiento guardado correctamente.');
    }
    this.closeModal();
    if (this.selectedGranjaId) {
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
    this.movimientoToCompleteGroup = null;
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
    const grupo = this.movimientoToCompleteGroup;
    if (grupo?.length) {
      void this.ejecutarCompletarGrupo(grupo);
      return;
    }
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

  private async ejecutarCompletarGrupo(pendientes: MovimientoPolloEngordeDto[]): Promise<void> {
    this.showConfirmationModal = false;
    this.movimientoToCompleteGroup = null;
    this.loading = true;
    const ok: string[] = [];
    try {
      for (const m of pendientes) {
        if (m.estado !== 'Pendiente') continue;
        await firstValueFrom(this.movimientoSvc.complete(m.id));
        ok.push(m.numeroMovimiento);
      }
      this.toastService.success(
        `Despacho completado: ${ok.length} movimiento(s). Se descontaron las aves en cada lote.`
      );
      this.loadMovimientos();
      this.refreshResumenIfLoteSelected();
    } catch (err: unknown) {
      this.showErrorMessage('Error al completar el despacho: ' + (err instanceof Error ? err.message : String(err)));
      this.loadMovimientos();
    } finally {
      this.loading = false;
    }
  }

  deleteMovimiento(m: MovimientoPolloEngordeDto): void {
    this.movimientoToDeleteGroup = null;
    this.movimientoToDelete = m;
    const extra =
      m.estado === 'Completado'
        ? ' Las aves registradas en la venta volverán al inventario del lote de origen (y se ajustará el destino si hubo traslado).'
        : m.estado === 'Pendiente'
          ? ' Aún no se había descontado inventario al completar.'
          : '';
    this.confirmationModalData = {
      title: 'Eliminar movimiento',
      message: `¿Eliminar el movimiento ${m.numeroMovimiento}? Desaparecerá del listado.${extra}`,
      type: 'warning',
      confirmText: 'Sí, eliminar',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  onConfirmDelete(): void {
    const grupo = this.movimientoToDeleteGroup;
    if (grupo?.length) {
      void this.ejecutarCancelarGrupo(grupo);
      return;
    }
    if (!this.movimientoToDelete) return;
    const m = this.movimientoToDelete;
    this.showConfirmationModal = false;
    this.loading = true;
    this.movimientoSvc
      .eliminar(m.id, 'Eliminado por usuario')
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          const msg =
            m.estado === 'Completado'
              ? `Movimiento ${m.numeroMovimiento} eliminado. Las aves volvieron al inventario del lote.`
              : `Movimiento ${m.numeroMovimiento} eliminado.`;
          this.showSuccessMessage(msg);
          this.loadMovimientos();
          this.refreshResumenIfLoteSelected();
          this.movimientoToDelete = null;
        },
        error: (err) => {
          this.showErrorMessage('Error al eliminar: ' + (err?.message ?? ''));
          this.movimientoToDelete = null;
        }
      });
  }

  private async ejecutarCancelarGrupo(items: MovimientoPolloEngordeDto[]): Promise<void> {
    this.showConfirmationModal = false;
    this.movimientoToDeleteGroup = null;
    this.loading = true;
    const afectados = items.filter((m) => m.estado !== 'Cancelado');
    try {
      for (const m of afectados) {
        await firstValueFrom(this.movimientoSvc.eliminar(m.id, 'Eliminado por usuario (despacho)'));
      }
      this.showSuccessMessage(`Se eliminaron ${afectados.length} movimiento(s) del despacho. Si estaban completados, el inventario se revirtió en cada lote.`);
      this.loadMovimientos();
      this.refreshResumenIfLoteSelected();
    } catch (err: unknown) {
      this.showErrorMessage('Error al eliminar el despacho: ' + (err instanceof Error ? err.message : String(err)));
      this.loadMovimientos();
    } finally {
      this.loading = false;
    }
  }

  onCancelDelete(): void {
    this.showConfirmationModal = false;
    this.movimientoToDelete = null;
    this.movimientoToComplete = null;
    this.movimientoToCompleteGroup = null;
    this.movimientoToDeleteGroup = null;
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
    this.movimientoToCompleteGroup = null;
    this.movimientoToDeleteGroup = null;
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

  trackByFila(_: number, fila: FilaTablaMovimiento): string {
    return fila.kind === 'despacho-grupo' ? fila.clave : `s-${fila.movimiento.id}`;
  }

  toggleExpandDespacho(clave: string): void {
    this.expandedDespacho = { ...this.expandedDespacho, [clave]: !this.expandedDespacho[clave] };
  }

  isDespachoExpanded(clave: string): boolean {
    return !!this.expandedDespacho[clave];
  }

  estadoGrupoDespacho(movs: MovimientoPolloEngordeDto[]): string {
    const est = movs.map((m) => m.estado);
    const allEq = (s: string) => est.every((e) => e === s);
    if (allEq('Pendiente')) return 'Pendiente';
    if (allEq('Completado')) return 'Completado';
    if (allEq('Cancelado')) return 'Cancelado';
    return 'Parcial';
  }

  totalAvesGrupo(movs: MovimientoPolloEngordeDto[]): number {
    return movs.reduce((s, m) => s + (m.totalAves ?? 0), 0);
  }

  sumCantidadGrupo(movs: MovimientoPolloEngordeDto[], campo: 'cantidadHembras' | 'cantidadMachos' | 'cantidadMixtas'): number {
    return movs.reduce((s, m) => s + (m[campo] ?? 0), 0);
  }

  pendientesEnGrupo(movs: MovimientoPolloEngordeDto[]): MovimientoPolloEngordeDto[] {
    return movs.filter((m) => m.estado === 'Pendiente');
  }

  puedeCompletarGrupo(movs: MovimientoPolloEngordeDto[]): boolean {
    return movs.some((m) => m.estado === 'Pendiente');
  }

  puedeCancelarGrupo(movs: MovimientoPolloEngordeDto[]): boolean {
    return movs.some((m) => m.estado !== 'Cancelado');
  }

  completarGrupoDespacho(fila: FilaDespachoGrupo): void {
    const pend = this.pendientesEnGrupo(fila.movimientos);
    if (pend.length === 0) return;
    this.movimientoToComplete = null;
    this.movimientoToCompleteGroup = pend;
    const total = pend.reduce((s, m) => s + m.totalAves, 0);
    const lineas = pend
      .map((m) => `${m.loteOrigenNombre ?? 'Lote'}: ${this.formatearNumero(m.totalAves)} aves`)
      .join(' · ');
    this.confirmationModalData = {
      title: 'Completar despacho (varios lotes)',
      message: `Despacho ${fila.numeroDespacho}: se completarán ${pend.length} movimiento(s) pendientes (${this.formatearNumero(total)} aves en total). Se descontará de cada lote su cantidad. Detalle: ${lineas}.`,
      type: 'info',
      confirmText: 'Sí, completar todo',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  cancelarGrupoDespacho(fila: FilaDespachoGrupo): void {
    const afect = fila.movimientos.filter((m) => m.estado !== 'Cancelado');
    if (afect.length === 0) return;
    this.movimientoToDelete = null;
    this.movimientoToDeleteGroup = afect;
    const completados = afect.filter((m) => m.estado === 'Completado').length;
    const extra =
      completados > 0
        ? ` En ${completados} línea(s) ya completada(s), las aves volverán al inventario de cada lote de origen.`
        : '';
    this.confirmationModalData = {
      title: 'Eliminar despacho completo',
      message: `¿Eliminar ${afect.length} movimiento(s) del despacho ${fila.numeroDespacho}? Desaparecerán del listado.${extra}`,
      type: 'warning',
      confirmText: 'Sí, eliminar todo',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  private buildFilasTabla(list: MovimientoPolloEngordeDto[]): FilaTablaMovimiento[] {
    const puedeAgrupar = (m: MovimientoPolloEngordeDto) =>
      m.tipoMovimiento === 'Venta' && !!(m.numeroDespacho ?? '').trim();

    const grupoKey = (m: MovimientoPolloEngordeDto) =>
      `${(m.numeroDespacho ?? '').trim().toLowerCase()}|${this.fechaDiaISO(m.fechaMovimiento)}|${m.granjaOrigenId ?? 0}`;

    const groups = new Map<string, MovimientoPolloEngordeDto[]>();
    const sueltos: MovimientoPolloEngordeDto[] = [];

    for (const m of list) {
      if (!puedeAgrupar(m)) {
        sueltos.push(m);
        continue;
      }
      const k = grupoKey(m);
      if (!groups.has(k)) groups.set(k, []);
      groups.get(k)!.push(m);
    }

    const filas: FilaTablaMovimiento[] = [];

    for (const [, movs] of groups) {
      if (movs.length >= 2) {
        movs.sort((a, b) => (a.numeroMovimiento ?? '').localeCompare(b.numeroMovimiento ?? ''));
        const clave = grupoKey(movs[0]);
        filas.push({
          kind: 'despacho-grupo',
          clave,
          numeroDespacho: (movs[0].numeroDespacho ?? '').trim(),
          fechaMovimiento: movs[0].fechaMovimiento,
          movimientos: movs
        });
      } else {
        sueltos.push(movs[0]);
      }
    }

    sueltos.sort((a, b) => this.compareMovimientoFechaDesc(a, b));
    for (const m of sueltos) {
      filas.push({ kind: 'simple', movimiento: m });
    }

    filas.sort((a, b) => this.compareFilaTablaDesc(a, b));
    return filas;
  }

  private fechaDiaISO(iso: string): string {
    if (!iso) return '';
    return iso.slice(0, 10);
  }

  private compareMovimientoFechaDesc(a: MovimientoPolloEngordeDto, b: MovimientoPolloEngordeDto): number {
    const da = new Date(a.fechaMovimiento).getTime();
    const db = new Date(b.fechaMovimiento).getTime();
    if (db !== da) return db - da;
    return (b.numeroMovimiento ?? '').localeCompare(a.numeroMovimiento ?? '');
  }

  private compareFilaTablaDesc(a: FilaTablaMovimiento, b: FilaTablaMovimiento): number {
    const fa = a.kind === 'despacho-grupo' ? a.fechaMovimiento : a.movimiento.fechaMovimiento;
    const fb = b.kind === 'despacho-grupo' ? b.fechaMovimiento : b.movimiento.fechaMovimiento;
    const ta = new Date(fa).getTime();
    const tb = new Date(fb).getTime();
    if (tb !== ta) return tb - ta;
    const na = a.kind === 'despacho-grupo' ? a.numeroDespacho : a.movimiento.numeroMovimiento;
    const nb = b.kind === 'despacho-grupo' ? b.numeroDespacho : b.movimiento.numeroMovimiento;
    return (nb ?? '').localeCompare(na ?? '');
  }
}

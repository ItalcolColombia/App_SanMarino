// Seguimiento Diario Lote Reproductora Aves de Engorde.
// Filtros: Granja → Núcleo → Galpón → Lote Aves Engorde → Lote Reproductora.
// API: SeguimientoDiarioLoteReproductora (tabla seguimiento_diario_lote_reproductora_aves_engorde).
import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
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
import { LoteSeguimientoDto, CreateLoteSeguimientoDto, UpdateLoteSeguimientoDto } from '../../services/lote-seguimiento.service';
import { ModalSeguimientoReproductoraComponent } from '../modal-seguimiento-reproductora/modal-seguimiento-reproductora.component';
import { ModalDetalleSeguimientoReproductoraComponent } from '../modal-detalle-seguimiento-reproductora/modal-detalle-seguimiento-reproductora.component';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import { ShowIfCountryDirective } from '../../../../core/directives/show-if-country.directive';
import { LesionTabComponent } from '../../../lesiones/components/lesion-tab/lesion-tab.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { ymdSinTz } from '../../../../shared/utils/format';
import { LesionService } from '../../../lesiones/services/lesion.service';
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
    ModalSeguimientoReproductoraComponent,
    ModalDetalleSeguimientoReproductoraComponent,
    ConfirmationModalComponent,
    FontAwesomeModule,
    ShowIfCountryDirective,
    LesionTabComponent
  ],
  templateUrl: './seguimiento-diario-lote-reproductora-list.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
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
  /** UI: pestaña activa dentro del módulo (seguimiento, lesiones) */
  activeTab: 'seguimiento' | 'lesiones' = 'seguimiento';
  modalOpen = false;
  detailModalOpen = false;

  /** Registro seleccionado para vista detalle (modal solo lectura). */
  editing: SeguimientoLoteLevanteDto | null = null;
  /** Registro mapeado al formato local para el modal de crear/editar. */
  editingModal: LoteSeguimientoDto | null = null;

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

  /** Máximo de registros diarios permitidos por lote reproductora (regla de negocio: 7 días). */
  readonly MAX_DIAS_SEGUIMIENTO = 7;

  /** True si el lote reproductora actual está cerrado (no se permite nuevo registro). */
  get isLoteReproductoraCerrado(): boolean {
    return this.selectedReproductoraDetail?.estado === 'Cerrado';
  }

  /** True si el lote cerrado fue reabierto con novedad (habilita eliminar). */
  get isReabierto(): boolean {
    return this.selectedReproductoraDetail?.reabierto === true;
  }

  /** Se puede eliminar si el lote está abierto, o si está cerrado pero reabierto con novedad. */
  get puedeEliminar(): boolean {
    return !this.isLoteReproductoraCerrado || this.isReabierto;
  }

  /** Modal de reapertura: pide la novedad (motivo) para reabrir un lote cerrado. */
  reabrirModalOpen = false;
  novedadInput = '';
  reabriendo = false;

  /** True si ya se alcanzaron los 7 registros diarios del lote. */
  get isSeguimientoCompleto(): boolean {
    return this.seguimientos.length >= this.MAX_DIAS_SEGUIMIENTO;
  }

  /** True si se puede crear un nuevo registro (lote abierto + menos de 7 días). */
  get canCreateSeguimiento(): boolean {
    return !!this.selectedLoteReproductoraId
      && !this.isLoteReproductoraCerrado
      && !this.isSeguimientoCompleto;
  }

  /**
   * Fecha sugerida para el próximo registro, siempre consecutiva:
   * - Sin registros → fecha de encasetamiento del lote reproductora (día 0).
   * - Con registros → último registro + 1 día.
   * Si no hay fecha de encasetamiento disponible se usa hoy.
   */
  get nextSuggestedFecha(): string {
    if (this.seguimientos.length === 0) {
      // Primer registro: día siguiente al encasetamiento (día 1 = encasetamiento + 1)
      const enc = ymdSinTz(this.selectedReproductoraDetail?.fechaEncasetamiento);
      if (enc) return this.addDaysToYmd(enc, 1);
      return this.todayYmd();
    }
    // Registro N: último registrado + 1 día
    const last = this.seguimientos[this.seguimientos.length - 1];
    const lastFecha = ymdSinTz(last?.fechaRegistro) ?? this.todayYmd();
    return this.addDaysToYmd(lastFecha, 1);
  }

  private addDaysToYmd(ymd: string, days: number): string {
    const [y, m, d] = ymd.split('-').map(Number);
    const date = new Date(Date.UTC(y, m - 1, d + days));
    return date.toISOString().slice(0, 10);
  }

  private todayYmd(): string {
    // Fecha LOCAL (toISOString daría el día siguiente después de las 19:00 en UTC-5)
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  faPlus = faPlus;
  faPen = faPen;
  faTrash = faTrash;
  faFilter = faFilter;
  faEye = faEye;

  constructor(
    private segSvc: SeguimientoDiarioLoteReproductoraService,
    private loteReproductoraSvc: LoteReproductoraAveEngordeService,
    private toastService: ToastService,
    private lesionSvc: LesionService
  ) {}

  openLesionCreate(): void {
    this.activeTab = 'lesiones';
    this.lesionSvc.openCreate$.next();
  }

  onOpenLesion(): void {
    this.activeTab = 'lesiones';
    this.lesionSvc.openCreate$.next();
  }

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

  /** selectedLoteReproductoraId como string para los inputs del modal local. */
  get selectedReproIdStr(): string | null {
    return this.selectedLoteReproductoraId != null ? String(this.selectedLoteReproductoraId) : null;
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

  /** Fecha a usar como valor por defecto en el modal de nuevo registro */
  modalDefaultFecha: string | null = null;

  create(): void {
    if (!this.canCreateSeguimiento) return;
    this.editing = null;
    this.editingModal = null;
    this.modalDefaultFecha = this.nextSuggestedFecha;
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
    this.editingModal = this.mapSegToLocalDto(seg);
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
    this.editingModal = null;
  }

  /** Mapea SeguimientoLoteLevanteDto (formato API) → LoteSeguimientoDto (formato modal local). */
  private mapSegToLocalDto(seg: SeguimientoLoteLevanteDto): LoteSeguimientoDto {
    const s = seg as any;
    return {
      id: seg.id,
      fecha: seg.fechaRegistro ?? '',
      loteId: Number(this.selectedLoteReproductoraId),
      reproductoraId: String(this.selectedLoteReproductoraId),
      mortalidadH: s.mortalidadHembras ?? 0,
      mortalidadM: s.mortalidadMachos ?? 0,
      selH: seg.selH ?? 0,
      selM: seg.selM ?? 0,
      errorH: s.errorSexajeHembras ?? 0,
      errorM: s.errorSexajeMachos ?? 0,
      tipoAlimento: seg.tipoAlimento ?? '',
      consumoAlimento: s.consumoKgHembras ?? 0,
      consumoKgMachos: seg.consumoKgMachos ?? null,
      observaciones: seg.observaciones ?? null,
      ciclo: seg.ciclo ?? 'Normal',
      pesoPromH: seg.pesoPromH ?? null,
      pesoPromM: seg.pesoPromM ?? null,
      uniformidadH: s.uniformidadH ?? null,
      uniformidadM: s.uniformidadM ?? null,
      cvH: s.cvH ?? null,
      cvM: s.cvM ?? null,
      consumoAguaDiario: s.consumoAguaDiario ?? null,
      consumoAguaPh: s.consumoAguaPh ?? null,
      consumoAguaOrp: s.consumoAguaOrp ?? null,
      consumoAguaTemperatura: s.consumoAguaTemperatura ?? null,
      pesoInicial: s.pesoInicial ?? null,
      pesoFinal: s.pesoFinal ?? null,
      metadata: seg.metadata ?? null,
      itemsAdicionales: seg.itemsAdicionales ?? null,
    };
  }

  /** Mapea el payload del modal local → DTO que espera el servicio de reproductora. */
  private mapLocalDtoToServiceDto(
    d: CreateLoteSeguimientoDto | UpdateLoteSeguimientoDto
  ): CreateSeguimientoDiarioLoteReproductoraDto {
    return {
      fechaRegistro: d.fecha,
      loteId: d.loteId,
      mortalidadHembras: d.mortalidadH ?? 0,
      mortalidadMachos: d.mortalidadM ?? 0,
      selH: d.selH ?? 0,
      selM: d.selM ?? 0,
      errorSexajeHembras: d.errorH ?? 0,
      errorSexajeMachos: d.errorM ?? 0,
      tipoAlimento: d.tipoAlimento ?? '',
      consumoHembras: d.consumoAlimento ?? null,
      consumoMachos: d.consumoKgMachos ?? null,
      ciclo: d.ciclo ?? 'Normal',
      observaciones: d.observaciones ?? null,
      itemsHembras: d.metadata?.itemsHembras ?? null,
      itemsMachos: d.metadata?.itemsMachos ?? null,
      pesoPromH: d.pesoPromH ?? null,
      pesoPromM: d.pesoPromM ?? null,
      uniformidadH: d.uniformidadH ?? null,
      uniformidadM: d.uniformidadM ?? null,
      cvH: d.cvH ?? null,
      cvM: d.cvM ?? null,
      consumoAguaDiario: d.consumoAguaDiario ?? null,
      consumoAguaPh: d.consumoAguaPh ?? null,
      consumoAguaOrp: d.consumoAguaOrp ?? null,
      consumoAguaTemperatura: d.consumoAguaTemperatura ?? null,
    };
  }

  /** Recibe el evento save del modal local y llama al servicio correcto. */
  onSave(event: { data: CreateLoteSeguimientoDto | UpdateLoteSeguimientoDto; isEdit: boolean }): void {
    const serviceDto = this.mapLocalDtoToServiceDto(event.data);

    const op$ = event.isEdit
      ? this.segSvc.update({ ...serviceDto, id: (event.data as UpdateLoteSeguimientoDto).id } as UpdateSeguimientoDiarioLoteReproductoraDto)
      : this.segSvc.create(serviceDto);

    this.loading = true;
    op$.pipe(finalize(() => (this.loading = false))).subscribe({
      next: () => {
        this.modalOpen = false;
        this.editing = null;
        this.editingModal = null;
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
    if (!this.puedeEliminar) return;
    this.pendingDeleteId = id;
    this.confirmModalOpen = true;
  }

  /** Abre el modal para reabrir un lote cerrado capturando la novedad (motivo). */
  openReabrir(): void {
    if (!this.selectedLoteReproductoraId || !this.isLoteReproductoraCerrado || this.isReabierto) return;
    this.novedadInput = '';
    this.reabrirModalOpen = true;
  }

  cancelReabrir(): void {
    this.reabrirModalOpen = false;
    this.novedadInput = '';
  }

  confirmReabrir(): void {
    const id = this.selectedLoteReproductoraId;
    const novedad = (this.novedadInput || '').trim();
    if (!id || !novedad || this.reabriendo) return;
    this.reabriendo = true;
    this.loteReproductoraSvc.reabrir(id, novedad).subscribe({
      next: detail => {
        this.selectedReproductoraDetail = detail;
        this.reabrirModalOpen = false;
        this.reabriendo = false;
        this.novedadInput = '';
        this.loteCerradoModalOpen = false;
        this.toastService.success('Lote reabierto. Ahora puede eliminar registros.', 'Reapertura', 4000);
      },
      error: err => {
        this.reabriendo = false;
        this.toastService.error(err?.error?.detail || err?.message || 'No se pudo reabrir el lote.', 'Error', 6000);
      }
    });
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

  /**
   * Días de edad del lote en el momento de un registro.
   * Día 1 = primer día después del encasetamiento.
   * Devuelve null si alguna fecha no está disponible o la fecha es anterior al encasetamiento.
   */
  calcularEdad(fechaRegistro: string | Date | null | undefined): number | null {
    // Días de calendario sobre la fecha intencional: con instantes crudos, un encaset a
    // medianoche y un registro a mediodía difieren 0.5 días y Math.round sumaría un día.
    const baseYmd = ymdSinTz(this.selectedReproductoraDetail?.fechaEncasetamiento);
    const regYmd = ymdSinTz(fechaRegistro);
    if (!baseYmd || !regYmd) return null;
    const inicio = new Date(baseYmd + 'T00:00:00');
    const registro = new Date(regYmd + 'T00:00:00');
    if (isNaN(inicio.getTime()) || isNaN(registro.getTime())) return null;
    const dias = Math.round((registro.getTime() - inicio.getTime()) / (1000 * 60 * 60 * 24));
    return dias > 0 ? dias : null;
  }

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

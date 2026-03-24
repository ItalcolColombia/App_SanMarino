import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';

import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { LoteService, LoteDto, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';
import { LoteEngordeService, LoteAveEngordeDto } from '../../../lote-engorde/services/lote-engorde.service';
import {
  SeguimientoAvesEngordeService,
  SeguimientoLoteLevanteDto,
  CreateSeguimientoLoteLevanteDto,
  UpdateSeguimientoLoteLevanteDto
} from '../../services/seguimiento-aves-engorde.service';
import { LoteReproductoraAveEngordeService, AvesDisponiblesDto, LoteReproductoraAveEngordeDto } from '../../../lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../lote-levante/services/nucleo.service';
import { ModalLiquidacionComponent } from '../../../lote-levante/pages/modal-liquidacion/modal-liquidacion.component';
import { ModalCalculosComponent } from '../../../lote-levante/pages/modal-calculos/modal-calculos.component';
import { ModalCreateEditComponent } from '../../../lote-levante/pages/modal-create-edit/modal-create-edit.component';
import { ModalDetalleSeguimientoLevanteComponent } from '../../../lote-levante/pages/modal-detalle-seguimiento/modal-detalle-seguimiento.component';
import { FiltroSelectComponent, FilterDataResponse } from '../../../lote-levante/pages/filtro-select/filtro-select.component';
import { TabsPrincipalComponent } from '../../../lote-levante/pages/tabs-principal/tabs-principal.component';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import {
  CatalogoAlimentosService,
  CatalogItemDto,
  PagedResult
} from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { EMPTY } from 'rxjs';
import { expand, map, reduce } from 'rxjs/operators';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-seguimiento-aves-engorde-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    ModalLiquidacionComponent,
    ModalCalculosComponent,
    ModalCreateEditComponent,
    ModalDetalleSeguimientoLevanteComponent,
    FiltroSelectComponent,
    TabsPrincipalComponent,
    ConfirmationModalComponent
  ],
  templateUrl: './seguimiento-aves-engorde-list.component.html',
  styleUrls: ['./seguimiento-aves-engorde-list.component.scss']
})
export class SeguimientoAvesEngordeListComponent implements OnInit {
  readonly SIN_GALPON = '__SIN_GALPON__';
  readonly filterDataUrl = `${environment.apiUrl}/SeguimientoAvesEngorde/filter-data`;

  alimentosCatalog: CatalogItemDto[] = [];
  private alimentosByCode = new Map<string, CatalogItemDto>();
  private alimentosById = new Map<number, CatalogItemDto>();
  private alimentosByName = new Map<string, CatalogItemDto>();

  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: Array<{ id: string; label: string }> = [];
  private filterData: FilterDataResponse | null = null;
  private allNucleos: NucleoDto[] = [];
  private allGalpones: Array<{ galponId: string; galponNombre: string; nucleoId: string; granjaId: number }> = [];

  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;

  private allLotes: LoteDto[] = [];
  lotes: LoteDto[] = [];
  seguimientos: SeguimientoLoteLevanteDto[] = [];
  selectedLote: LoteDto | null = null;
  resumenSelected: LoteMortalidadResumenDto | null = null;

  /** Aves disponibles del lote (después de restar las asignadas a lotes reproductora). */
  avesDisponibles: AvesDisponiblesDto | null = null;
  /** Lotes reproductora del lote aves engorde seleccionado (para saber si todos están cerrados). */
  lotesReproductora: LoteReproductoraAveEngordeDto[] = [];

  loading = false;
  /** GET por id al pulsar Editar (antes de llenar el modal). */
  loadingEdit = false;
  modalOpen = false;
  detailModalOpen = false;
  editing: SeguimientoLoteLevanteDto | null = null;

  /** Modal de error al guardar (ej. registro duplicado lote+fecha). */
  errorModalOpen = false;
  errorModalData: ConfirmationModalData = {
    title: 'Error al guardar',
    message: '',
    type: 'error',
    confirmText: 'Entendido',
    showCancel: false
  };

  hasSinGalpon = false;
  activeTab: 'principal' | 'calculos' | 'liquidacion' = 'principal';
  private galponNameById = new Map<string, string>();

  calcsOpen = false;
  liquidacionOpen = false;

  constructor(
    private farmSvc: FarmService,
    private nucleoSvc: NucleoService,
    private loteSvc: LoteService,
    private loteEngordeSvc: LoteEngordeService,
    private segSvc: SeguimientoAvesEngordeService,
    private loteReproductoraSvc: LoteReproductoraAveEngordeService,
    private galponSvc: GalponService,
    private catalogSvc: CatalogoAlimentosService
  ) {}

  /** Total aves disponibles para seguimiento (hembras + machos, después de restar asignadas a reproductoras). */
  get avesDisponiblesTotal(): number {
    if (!this.avesDisponibles) return 0;
    return (this.avesDisponibles.hembrasDisponibles ?? 0) + (this.avesDisponibles.machosDisponibles ?? 0);
  }

  /** True si hay al menos un lote reproductora y todos están cerrados (sin aves). */
  get loteCerradoPorReproductoras(): boolean {
    if (this.lotesReproductora.length === 0) return false;
    return this.lotesReproductora.every(r => r.estado === 'Cerrado' || (r.avesActuales ?? 0) <= 0);
  }

  /** No se puede crear/editar/eliminar seguimiento: sin aves disponibles (ya cargado) o lote cerrado por reproductoras. */
  get noPuedeSeguimiento(): boolean {
    const sinAves = this.avesDisponibles != null && this.avesDisponiblesTotal === 0;
    return sinAves || this.loteCerradoPorReproductoras;
  }

  ngOnInit(): void {
    this.loadAlimentosCatalog();
  }

  private loadAlimentosCatalog(): void {
    const firstPage = 1;
    const pageSize = 100;
    this.catalogSvc.list('', firstPage, pageSize).pipe(
      expand((res: PagedResult<CatalogItemDto>) => {
        const received = res.page * res.pageSize;
        return received < (res.total ?? 0) ? this.catalogSvc.list('', res.page + 1, res.pageSize) : EMPTY;
      }),
      reduce((acc: CatalogItemDto[], res: PagedResult<CatalogItemDto>) => acc.concat(res.items ?? []), []),
      map(all => all.sort((a, b) => (a.nombre || '').localeCompare(b.nombre || '', 'es', { numeric: true, sensitivity: 'base' })))
    ).subscribe(all => {
      this.alimentosCatalog = all;
      this.alimentosById.clear();
      this.alimentosByCode.clear();
      this.alimentosByName.clear();
      for (const it of all) {
        if (it.id != null) this.alimentosById.set(it.id, it);
        if (it.codigo) this.alimentosByCode.set(String(it.codigo).trim(), it);
        if (it.nombre) this.alimentosByName.set(it.nombre.trim().toLowerCase(), it);
      }
    });
  }

  mapAlimentoNombre = (value?: string | number | null): string => {
    if (value == null || value === '') return '';
    if (typeof value === 'number') {
      const f = this.alimentosById.get(value);
      return f?.nombre || String(value);
    }
    const k = value.toString().trim();
    const found = this.alimentosByCode.get(k);
    if (found) return found.nombre || k;
    const asId = Number(k);
    if (!Number.isNaN(asId)) {
      const byId = this.alimentosById.get(asId);
      if (byId) return byId.nombre || k;
    }
    return k;
  };

  private loadGalponCatalog(): void {
    this.galponNameById.clear();
    if (!this.selectedGranjaId) return;
    if (this.selectedNucleoId) {
      this.galponSvc.getByGranjaAndNucleo(this.selectedGranjaId, this.selectedNucleoId).subscribe({
        next: rows => this.fillGalponMap(rows),
        error: () => this.galponNameById.clear()
      });
      return;
    }
    this.galponSvc.search({ granjaId: this.selectedGranjaId, page: 1, pageSize: 1000, soloActivos: true }).subscribe({
      next: res => this.fillGalponMap(res?.items || []),
      error: () => this.galponNameById.clear()
    });
  }

  private fillGalponMap(rows: GalponDetailDto[] | null | undefined): void {
    for (const g of rows || []) {
      const id = String(g.galponId).trim();
      if (!id) continue;
      this.galponNameById.set(id, (g.galponNombre || id).trim());
    }
    this.buildGalponesFromLotes();
  }

  onFilterDataLoaded(data: FilterDataResponse): void {
    this.filterData = data;
    this.granjas = data.farms ?? [];
    this.allNucleos = data.nucleos ?? [];
    this.allGalpones = data.galpones ?? [];
    this.allLotes = (data.lotes ?? []).map(l => ({
      loteId: l.loteId,
      loteNombre: l.loteNombre,
      granjaId: l.granjaId,
      nucleoId: l.nucleoId ?? undefined,
      galponId: l.galponId ?? undefined,
      loteErp: l.loteErp ?? undefined
    })) as LoteDto[];
    (data.galpones ?? []).forEach(g => {
      if (g.galponId) this.galponNameById.set(String(g.galponId).trim(), (g.galponNombre || g.galponId).trim());
    });
  }

  onGranjaChange(granjaId: number | null): void {
    this.selectedGranjaId = granjaId;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.seguimientos = [];
    this.galpones = [];
    this.hasSinGalpon = false;
    this.lotes = [];
    this.selectedLote = null;
    this.resumenSelected = null;
    this.nucleos = [];
    if (!this.selectedGranjaId) return;
    if (this.filterData) {
      const gid = Number(this.selectedGranjaId);
      this.nucleos = this.allNucleos.filter(n => n.granjaId === gid);
      this.applyFiltersToLotes();
      this.buildGalponesFromFilterData();
      return;
    }
    this.nucleoSvc.getByGranja(this.selectedGranjaId).subscribe({
      next: rows => (this.nucleos = rows || []),
      error: () => (this.nucleos = [])
    });
    this.reloadLotesThenApplyFilters();
    this.loadGalponCatalog();
  }

  onNucleoChange(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.seguimientos = [];
    this.selectedLote = null;
    this.resumenSelected = null;
    if (this.filterData) {
      this.applyFiltersToLotes();
      this.buildGalponesFromFilterData();
      return;
    }
    this.applyFiltersToLotes();
    this.loadGalponCatalog();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteId = null;
    this.seguimientos = [];
    this.selectedLote = null;
    this.resumenSelected = null;
    this.applyFiltersToLotes();
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLoteId = loteId;
    this.seguimientos = [];
    this.selectedLote = null;
    this.resumenSelected = null;
    this.avesDisponibles = null;
    this.lotesReproductora = [];
    if (!this.selectedLoteId) return;
    this.loading = true;
    const id = this.selectedLoteId;
    // Lote seleccionado = lote_ave_engorde. Cargar detalle, seguimientos, aves disponibles y lotes reproductora.
    this.loteEngordeSvc.getById(id).subscribe({
      next: l => (this.selectedLote = this.mapLoteAveEngordeToLoteDto(l)),
      error: () => (this.selectedLote = null)
    });
    this.loteReproductoraSvc.getAvesDisponibles(id).subscribe({
      next: aves => (this.avesDisponibles = aves),
      error: () => (this.avesDisponibles = null)
    });
    this.loteReproductoraSvc.getAll(id).subscribe({
      next: list => (this.lotesReproductora = list ? [...list] : []),
      error: () => (this.lotesReproductora = [])
    });
    this.segSvc.getByLoteId(id)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: rows => (this.seguimientos = rows || []),
        error: () => (this.seguimientos = [])
      });
  }

  /** Mapea LoteAveEngordeDto a LoteDto para reutilizar tabs/header (fechaEncaset, avesEncasetadas, etc.). */
  private mapLoteAveEngordeToLoteDto(l: LoteAveEngordeDto): LoteDto {
    const hembras = l.hembrasL ?? 0;
    const machos = l.machosL ?? 0;
    const mixtas = l.mixtas ?? 0;
    return {
      loteId: l.loteAveEngordeId,
      loteNombre: l.loteNombre ?? '',
      granjaId: l.granjaId,
      nucleoId: l.nucleoId ?? null,
      galponId: l.galponId ?? null,
      regional: l.regional ?? undefined,
      fechaEncaset: l.fechaEncaset ?? undefined,
      hembrasL: hembras,
      machosL: machos,
      mixtas: l.mixtas ?? null,
      pesoInicialH: l.pesoInicialH,
      pesoInicialM: l.pesoInicialM,
      pesoMixto: l.pesoMixto ?? null,
      unifH: l.unifH,
      unifM: l.unifM,
      mortCajaH: l.mortCajaH,
      mortCajaM: l.mortCajaM,
      avesEncasetadas: l.avesEncasetadas ?? (hembras + machos + mixtas),
      farm: l.farm ?? null,
      nucleo: l.nucleo ?? null,
      galpon: l.galpon ?? null
    };
  }

  private reloadLotesThenApplyFilters(): void {
    if (!this.selectedGranjaId) {
      this.allLotes = [];
      this.lotes = [];
      this.galpones = [];
      this.hasSinGalpon = false;
      return;
    }
    this.loading = true;
    this.loteSvc.getAll()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: all => {
          this.allLotes = all || [];
          this.applyFiltersToLotes();
          this.buildGalponesFromLotes();
        },
        error: () => {
          this.allLotes = [];
          this.lotes = [];
          this.galpones = [];
          this.hasSinGalpon = false;
        }
      });
  }

  private applyFiltersToLotes(): void {
    if (!this.selectedGranjaId) {
      this.lotes = [];
      return;
    }
    const gid = String(this.selectedGranjaId);
    let filtered = this.allLotes.filter(l => String(l.granjaId) === gid);
    if (this.selectedNucleoId) {
      filtered = filtered.filter(l => String(l.nucleoId) === String(this.selectedNucleoId));
    }
    this.hasSinGalpon = filtered.some(l => !this.hasValue(l.galponId));
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

  private buildGalponesFromFilterData(): void {
    if (!this.selectedGranjaId) {
      this.galpones = [];
      this.hasSinGalpon = false;
      return;
    }
    const gid = Number(this.selectedGranjaId);
    const nid = this.selectedNucleoId ? String(this.selectedNucleoId) : null;
    const list = this.allGalpones.filter(g => g.granjaId === gid && (!nid || g.nucleoId === nid));
    this.galpones = list.map(g => ({
      id: String(g.galponId).trim(),
      label: (g.galponNombre || g.galponId).trim()
    }));
    this.hasSinGalpon = this.allLotes.some(l =>
      l.granjaId === gid && (!nid || String(l.nucleoId) === nid) && !this.hasValue(l.galponId)
    );
    if (this.hasSinGalpon) {
      this.galpones.unshift({ id: this.SIN_GALPON, label: '— Sin galpón —' });
    }
    this.galpones.sort((a, b) => a.label.localeCompare(b.label, 'es', { numeric: true, sensitivity: 'base' }));
  }

  private buildGalponesFromLotes(): void {
    if (!this.selectedGranjaId) {
      this.galpones = [];
      this.hasSinGalpon = false;
      return;
    }
    const gid = String(this.selectedGranjaId);
    let base = this.allLotes.filter(l => String(l.granjaId) === gid);
    if (this.selectedNucleoId) {
      base = base.filter(l => String(l.nucleoId) === String(this.selectedNucleoId));
    }
    const seen = new Set<string>();
    this.galpones = [];
    for (const l of base) {
      const id = this.normalizeId(l.galponId);
      if (!id || seen.has(id)) continue;
      seen.add(id);
      this.galpones.push({ id, label: this.galponNameById.get(id) || id });
    }
    this.hasSinGalpon = base.some(l => !this.hasValue(l.galponId));
    if (this.hasSinGalpon) {
      this.galpones.unshift({ id: this.SIN_GALPON, label: '— Sin galpón —' });
    }
    this.galpones.sort((a, b) => a.label.localeCompare(b.label, 'es', { numeric: true, sensitivity: 'base' }));
  }

  create(): void {
    if (!this.selectedLoteId || this.noPuedeSeguimiento) return;
    this.editing = null;
    this.modalOpen = true;
  }

  edit(seg: SeguimientoLoteLevanteDto): void {
    if (this.noPuedeSeguimiento) return;
    // Cargar registro completo (metadata + ítems) para que el modal pueda editar alimentos y el resto
    this.loading = true;
    this.segSvc.getById(seg.id).pipe(finalize(() => (this.loading = false))).subscribe({
      next: full => {
        this.editing = full;
        this.modalOpen = true;
      },
      error: () => {
        this.editing = seg;
        this.modalOpen = true;
      }
    });
  }

  viewDetail(seg: SeguimientoLoteLevanteDto): void {
    this.editing = seg;
    this.detailModalOpen = true;
  }

  delete(id: number): void {
    if (this.noPuedeSeguimiento) return;
    if (!confirm('¿Estás seguro de eliminar este registro de seguimiento diario? Esta acción no se puede deshacer.')) return;
    this.loading = true;
    this.segSvc.delete(id).subscribe({
      next: () => {
        this.loading = false;
        this.onLoteChange(this.selectedLoteId);
      },
      error: err => {
        this.loading = false;
        alert(err?.error?.message || err?.message || 'Error al eliminar el registro.');
        this.onLoteChange(this.selectedLoteId);
      }
    });
  }

  cancel(): void {
    this.modalOpen = false;
    this.editing = null;
    this.loadingEdit = false;
  }

  onSave(event: { data: CreateSeguimientoLoteLevanteDto | UpdateSeguimientoLoteLevanteDto; isEdit: boolean }): void {
    const op$ = event.isEdit
      ? this.segSvc.update(event.data as UpdateSeguimientoLoteLevanteDto)
      : this.segSvc.create(event.data as CreateSeguimientoLoteLevanteDto);
    this.loading = true;
    op$.pipe(finalize(() => (this.loading = false))).subscribe({
      next: () => {
        this.modalOpen = false;
        this.editing = null;
        this.loadingEdit = false;
        this.onLoteChange(this.selectedLoteId);
      },
      error: err => {
        this.errorModalData = {
          ...this.errorModalData,
          message: err?.error?.message || err?.message || 'Error al guardar el registro.'
        };
        this.errorModalOpen = true;
      }
    });
  }

  onErrorModalClose(): void {
    this.errorModalOpen = false;
  }

  trackById = (_: number, r: SeguimientoLoteLevanteDto) => r.id;
  trackByNucleo = (_: number, n: NucleoDto) => n.nucleoId;

  get selectedLoteNombre(): string {
    const l = this.lotes.find(x => x.loteId === this.selectedLoteId);
    return l?.loteNombre ?? (this.selectedLoteId?.toString() || '—');
  }
  get selectedGranjaName(): string {
    const g = this.granjas.find(x => x.id === this.selectedGranjaId);
    return g?.name ?? '';
  }
  get selectedNucleoNombre(): string {
    const n = this.nucleos.find(x => x.nucleoId === this.selectedNucleoId);
    return n?.nucleoNombre ?? '';
  }
  get selectedGalponNombre(): string {
    if (this.selectedGalponId === this.SIN_GALPON) return '— Sin galpón —';
    return this.galponNameById.get((this.selectedGalponId ?? '').trim()) || (this.selectedGalponId ?? '');
  }

  calcularEdadDias(fechaEncaset?: string | Date | null): number {
    const encYmd = this.toYMD(fechaEncaset);
    const enc = this.ymdToLocalNoonDate(encYmd);
    if (!enc) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const now = this.ymdToLocalNoonDate(this.todayYMD())!;
    return Math.max(1, Math.floor((now.getTime() - enc.getTime()) / MS_DAY) + 1);
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

  openCalculos(): void {
    if (!this.selectedLoteId) return;
    this.calcsOpen = true;
  }
  closeCalculos(): void {
    this.calcsOpen = false;
  }
  openLiquidacion(): void {
    if (!this.selectedLoteId) return;
    this.liquidacionOpen = true;
  }
  closeLiquidacion(): void {
    this.liquidacionOpen = false;
  }

  private todayYMD(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
  private toYMD(input: string | Date | null | undefined): string | null {
    if (!input) return null;
    if (input instanceof Date && !isNaN(input.getTime())) {
      return `${input.getFullYear()}-${String(input.getMonth() + 1).padStart(2, '0')}-${String(input.getDate()).padStart(2, '0')}`;
    }
    const s = String(input).trim();
    const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
    const m1 = s.match(ymd);
    if (m1) return `${m1[1]}-${m1[2]}-${m1[3]}`;
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    return null;
  }
  formatDMY = (input: string | Date | null | undefined): string => {
    const ymd = this.toYMD(input);
    if (!ymd) return '';
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  };
  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) return null;
    const d = new Date(`${ymd}T12:00:00`);
    return isNaN(d.getTime()) ? null : d;
  }
}

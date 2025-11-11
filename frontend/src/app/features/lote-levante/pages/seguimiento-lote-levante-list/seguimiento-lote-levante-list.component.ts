import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';

import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';

import { LoteService, LoteDto, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';

import {
  SeguimientoLoteLevanteService,
  SeguimientoLoteLevanteDto,
  CreateSeguimientoLoteLevanteDto,
  UpdateSeguimientoLoteLevanteDto
} from '../../services/seguimiento-lote-levante.service';

import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../services/nucleo.service';
import { ModalLiquidacionComponent } from '../modal-liquidacion/modal-liquidacion.component';
import { ModalCalculosComponent } from '../modal-calculos/modal-calculos.component';
import { TablaListaRegistroComponent } from '../tabla-lista-registro/tabla-lista-registro.component';
import { ModalCreateEditComponent } from '../modal-create-edit/modal-create-edit.component';
import { FichaLoteSelectComponent } from '../ficha-lote-select/ficha-lote-select.component';
import { FiltroSelectComponent } from '../filtro-select/filtro-select.component';
import { TabsPrincipalComponent } from '../tabs-principal/tabs-principal.component';

// ===== Importa el servicio del catálogo =====
import {
  CatalogoAlimentosService,
  CatalogItemDto,
  PagedResult
} from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { EMPTY } from 'rxjs';
import { expand, map, reduce } from 'rxjs/operators';




@Component({
  selector: 'app-seguimiento-lote-levante-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, SidebarComponent, ModalLiquidacionComponent, ModalCalculosComponent, TablaListaRegistroComponent, ModalCreateEditComponent, FichaLoteSelectComponent, FiltroSelectComponent, TabsPrincipalComponent],
  templateUrl: './seguimiento-lote-levante-list.component.html',
  styleUrls: ['./seguimiento-lote-levante-list.component.scss']
})
export class SeguimientoLoteLevanteListComponent implements OnInit {
  // ================== constantes / sentinelas ==================
  readonly SIN_GALPON = '__SIN_GALPON__';

  // ================== catálogos (BACKEND) ==================
  alimentosCatalog: CatalogItemDto[] = [];
  private alimentosByCode = new Map<string, CatalogItemDto>();
  private alimentosById = new Map<number, CatalogItemDto>();
  private alimentosByName = new Map<string, CatalogItemDto>(); // nombre en minúsculas



  // ================== catálogos (otros) ==================
  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: Array<{ id: string; label: string }> = [];

  // ================== selección / filtro ==================
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;

  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;  // Changed from string | null to number | null

  // ================== datos ==================
  private allLotes: LoteDto[] = [];
  lotes: LoteDto[] = [];
  seguimientos: SeguimientoLoteLevanteDto[] = [];

  selectedLote: LoteDto | null = null;
  resumenSelected: LoteMortalidadResumenDto | null = null;

  // ================== UI ==================
  loading = false;
  modalOpen = false;
  editing: SeguimientoLoteLevanteDto | null = null;
  hasSinGalpon = false;

  activeTab: 'principal' | 'calculos' | 'liquidacion' = 'principal';

  private galponNameById = new Map<string, string>();

  constructor(
    private farmSvc: FarmService,
    private nucleoSvc: NucleoService,
    private loteSvc: LoteService,
    private segSvc: SeguimientoLoteLevanteService,
    private galponSvc: GalponService,
    // servicio de catálogo
    private catalogSvc: CatalogoAlimentosService,
  ) {}

  // ================== INIT ==================
  ngOnInit(): void {
    // cargar catálogos de granjas, etc.
    this.farmSvc.getAll().subscribe({
      next: fs => (this.granjas = fs || []),
      error: () => (this.granjas = [])
    });

    // CARGA CATÁLOGO DE ALIMENTOS (para selects y mapeos en tabla)
    this.loadAlimentosCatalog();
  }

  // ================== CATALOGO ALIMENTOS ==================
  private loadAlimentosCatalog(): void {
    const firstPage = 1;
    const pageSize = 100; // ajusta si tu API permite más

    this.catalogSvc.list('', firstPage, pageSize).pipe(
      // si aún faltan páginas, sigue pidiéndolas
      expand((res: PagedResult<CatalogItemDto>) => {
        const received = res.page * res.pageSize;
        const more = received < (res.total ?? 0);
        return more
          ? this.catalogSvc.list('', res.page + 1, res.pageSize)
          : EMPTY;
      }),
      // acumula todos los items de todas las páginas
      reduce((acc: CatalogItemDto[], res: PagedResult<CatalogItemDto>) => {
        const items = Array.isArray(res.items) ? res.items : [];
        return acc.concat(items);
      }, []),
      // ordena por nombre (opcional)
      map(all => all.sort((a, b) =>
        (a.nombre || '').localeCompare(b.nombre || '', 'es', { numeric: true, sensitivity: 'base' })
      ))
    ).subscribe(all => {
      this.alimentosCatalog = all;

      // reconstruye índices
      this.alimentosById.clear();
      this.alimentosByCode.clear();
      this.alimentosByName.clear();

      for (const it of all) {
        if (it.id != null) this.alimentosById.set(it.id, it);
        if (it.codigo)     this.alimentosByCode.set(String(it.codigo).trim(), it);
        if (it.nombre)     this.alimentosByName.set(it.nombre.trim().toLowerCase(), it);
      }
    });
  }

  // Dado un string (código) o número (id), devolver el nombre del alimento
  mapAlimentoNombre = (value?: string | number | null): string => {
    if (value == null || value === '') return '';
    // si vino un número (id)
    if (typeof value === 'number') {
      const f = this.alimentosById.get(value);
      return f?.nombre || String(value);
    }
    // si vino un string (guardamos códigos como strings)
    const k = value.toString().trim();
    // 1) buscar por código
    const found = this.alimentosByCode.get(k);
    if (found) return found.nombre || k;
    // 2) a veces el backend pudo guardar el id como string; intentar parsear
    const asId = Number(k);
    if (!Number.isNaN(asId)) {
      const byId = this.alimentosById.get(asId);
      if (byId) return byId.nombre || k;
    }
    // 3) fallback
    return k;
  };


  // ================== CARGA GALPONES ==================
  private loadGalponCatalog(): void {
    this.galponNameById.clear();
    if (!this.selectedGranjaId) return;

    if (this.selectedNucleoId) {
      this.galponSvc.getByGranjaAndNucleo(this.selectedGranjaId, this.selectedNucleoId).subscribe({
        next: rows => this.fillGalponMap(rows),
        error: () => this.galponNameById.clear(),
      });
      return;
    }

    this.galponSvc.search({ granjaId: this.selectedGranjaId, page: 1, pageSize: 1000, soloActivos: true })
      .subscribe({
        next: res => this.fillGalponMap(res?.items || []),
        error: () => this.galponNameById.clear(),
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

  // ================== CASCADA DE FILTROS ==================
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

    if (!this.selectedLoteId) return;

    this.loading = true;
    this.segSvc.getByLoteId(this.selectedLoteId)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: rows => (this.seguimientos = rows || []),
        error: () => (this.seguimientos = [])
      });

    this.loteSvc.getById(this.selectedLoteId).subscribe({
      next: l => (this.selectedLote = l || null),
      error: () => (this.selectedLote = null)
    });

    this.loteSvc.getResumenMortalidad(this.selectedLoteId).subscribe({
      next: r => (this.resumenSelected = r),
      error: () => (this.resumenSelected = null)
    });
  }

  // ================== CARGA Y FILTRADO ==================
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
    if (!this.selectedGranjaId) { this.lotes = []; return; }
    const gid = String(this.selectedGranjaId);

    let filtered = this.allLotes.filter(l => String(l.granjaId) === gid);

    if (this.selectedNucleoId) {
      const nid = String(this.selectedNucleoId);
      filtered = filtered.filter(l => String(l.nucleoId) === nid);
    }

    this.hasSinGalpon = filtered.some(l => !this.hasValue(l.galponId));

    if (!this.selectedGalponId) { this.lotes = filtered; return; }

    if (this.selectedGalponId === this.SIN_GALPON) {
      this.lotes = filtered.filter(l => !this.hasValue(l.galponId));
      return;
    }

    const sel = this.normalizeId(this.selectedGalponId);
    this.lotes = filtered.filter(l => this.normalizeId(l.galponId) === sel);
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
      const nid = String(this.selectedNucleoId);
      base = base.filter(l => String(l.nucleoId) === nid);
    }

    const seen = new Set<string>();
    const result: Array<{ id: string; label: string }> = [];

    for (const l of base) {
      const id = this.normalizeId(l.galponId);
      if (!id) continue;
      if (seen.has(id)) continue;
      seen.add(id);
      const label = this.galponNameById.get(id) || id;
      result.push({ id, label });
    }

    this.hasSinGalpon = base.some(l => !this.hasValue(l.galponId));
    if (this.hasSinGalpon) {
      result.unshift({ id: this.SIN_GALPON, label: '— Sin galpón —' });
    }

    this.galpones = result.sort((a, b) =>
      a.label.localeCompare(b.label, 'es', { numeric: true, sensitivity: 'base' })
    );
  }

  // ================== CRUD modal ==================
  create(): void {
    if (!this.selectedLoteId) return;
    this.editing = null;
    this.modalOpen = true;
  }

  edit(seg: SeguimientoLoteLevanteDto): void {
    this.editing = seg;
    this.modalOpen = true;
  }

  delete(id: number): void {
    if (!confirm('¿Estás seguro de eliminar este registro de seguimiento diario? Esta acción no se puede deshacer.')) return;

    this.loading = true;
    this.segSvc.delete(id).subscribe({
      next: () => {
        this.loading = false;
        // Recargar los registros del lote después de eliminar
        this.onLoteChange(this.selectedLoteId);
      },
      error: (err) => {
        this.loading = false;
        console.error('Error al eliminar registro:', err);
        const errorMessage = err?.error?.message || err?.message || 'Error al eliminar el registro. Por favor, intenta nuevamente.';
        alert(errorMessage);
        // Recargar los registros incluso si hay error para mantener consistencia
        this.onLoteChange(this.selectedLoteId);
      }
    });
  }

  cancel(): void {
    this.modalOpen = false;
    this.editing = null;
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
        this.onLoteChange(this.selectedLoteId);
      },
      error: () => { /* TODO: toast de error */ }
    });
  }

  // ================== helpers ==================
  trackById = (_: number, r: SeguimientoLoteLevanteDto) => r.id;
  trackByNucleo = (_: number, n: NucleoDto) => n.nucleoId;

  get selectedLoteNombre(): string {
    const l = this.lotes.find(x => x.loteId === this.selectedLoteId);  // Now both are number | null
    return l?.loteNombre ?? (this.selectedLoteId?.toString() || '—');  // Convert number to string for display
  }

  get selectedGranjaName(): string {
    const g = this.granjas.find(x => x.id === this.selectedGranjaId);
    return g?.name ?? '';
  }

  get selectedNucleoNombre(): string {
    const n = this.nucleos.find(x => x.nucleoId === this.selectedNucleoId);
    return n?.nucleoNombre ?? '';
  }

  /** Nombre legible del galpón seleccionado para chips/ficha */
  get selectedGalponNombre(): string {
    if (this.selectedGalponId === this.SIN_GALPON) return '— Sin galpón —';
    const id = (this.selectedGalponId ?? '').trim();
    return this.galponNameById.get(id) || id;
  }

  /** Edad (en días) a HOY desde fecha de encasetamiento (mínimo 1). */
  calcularEdadDias(fechaEncaset?: string | Date | null): number {
    const encYmd = this.toYMD(fechaEncaset);
    const enc = this.ymdToLocalNoonDate(encYmd);
    if (!enc) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const now = this.ymdToLocalNoonDate(this.todayYMD())!; // hoy al mediodía local
    return Math.max(1, Math.floor((now.getTime() - enc.getTime()) / MS_DAY) + 1);
  }


  // ========== MÉTODOS LEGACY (SEMANAS) - MANTENER PARA COMPATIBILIDAD ==========
  /** @deprecated Usar calcularEdadDias() en su lugar */
  calcularEdadSemanas(fechaEncaset?: string | Date | null): number {
    const encYmd = this.toYMD(fechaEncaset);
    const enc = this.ymdToLocalNoonDate(encYmd);
    if (!enc) return 0;
    const MS_WEEK = 7 * 24 * 60 * 60 * 1000;
    const now = this.ymdToLocalNoonDate(this.todayYMD())!; // hoy al mediodía local
    return Math.max(1, Math.floor((now.getTime() - enc.getTime()) / MS_WEEK) + 1);
  }

  /**
   * @deprecated Usar calcularEdadDiasDesde() en su lugar
   * Edad (en semanas) del pollito AL MOMENTO DEL REGISTRO.
   * Calcula semanas desde fechaEncaset hasta fechaRegistro (mínimo 1), sin corrimientos.
   */
  calcularEdadSemanasDesde(fechaEncaset?: string | Date | null, fechaReferencia?: string | Date | null): number {
    const enc = this.ymdToLocalNoonDate(this.toYMD(fechaEncaset));
    const ref = this.ymdToLocalNoonDate(this.toYMD(fechaReferencia));
    if (!enc || !ref) return 0;
    const MS_WEEK = 7 * 24 * 60 * 60 * 1000;
    const diff = ref.getTime() - enc.getTime();
    if (diff < 0) return 0;
    return Math.max(1, Math.floor(diff / MS_WEEK) + 1);
  }

  /** @deprecated Usar edadDiasDe() en su lugar */
  edadSemanaDe(s: SeguimientoLoteLevanteDto): number {
    return this.calcularEdadSemanasDesde(this.selectedLote?.fechaEncaset ?? null, s?.fechaRegistro ?? null);
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

  // ========= MODALES =========
  calcsOpen = false;
  liquidacionOpen = false;

  openCalculos(): void {
    if (!this.selectedLoteId) return;
    this.calcsOpen = true;
  }

  closeCalculos(): void {
    this.calcsOpen = false;
  }

  // ========= LIQUIDACIÓN TÉCNICA =========
  openLiquidacion(): void {
    if (!this.selectedLoteId) return;
    this.liquidacionOpen = true;
  }

  closeLiquidacion(): void {
    this.liquidacionOpen = false;
  }

  // ******************************* Helpers de fecha *******************************

  /** Hoy en formato YYYY-MM-DD (local, sin zona) para <input type="date"> */
  private todayYMD(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  /** Normaliza cadenas mm/dd/aaaa, dd/mm/aaaa, ISO o Date a YYYY-MM-DD (local) */
  private toYMD(input: string | Date | null | undefined): string | null {
    if (!input) return null;

    if (input instanceof Date && !isNaN(input.getTime())) {
      const y = input.getFullYear();
      const m = String(input.getMonth() + 1).padStart(2, '0');
      const d = String(input.getDate()).padStart(2, '0');
      return `${y}-${m}-${d}`;
    }

    const s = String(input).trim();

    // YYYY-MM-DD
    const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
    const m1 = s.match(ymd);
    if (m1) return `${m1[1]}-${m1[2]}-${m1[3]}`;

    // mm/dd/aaaa o dd/mm/aaaa
    const sl = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;
    const m2 = s.match(sl);
    if (m2) {
      let a = parseInt(m2[1], 10); // mm o dd
      let b = parseInt(m2[2], 10); // dd o mm
      const yyyy = parseInt(m2[3], 10);
      let mm = a, dd = b;
      if (a > 12 && b <= 12) { mm = b; dd = a; }
      const mmS = String(mm).padStart(2, '0');
      const ddS = String(dd).padStart(2, '0');
      return `${yyyy}-${mmS}-${ddS}`;
    }

    // ISO (con T). Extrae la fecha en LOCAL sin cambiar el día
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${y}-${m}-${day}`;
    }

    return null;
  }

  /** Muestra dd/MM/aaaa SIN timezone shift a partir de cualquier entrada */
  formatDMY = (input: string | Date | null | undefined): string => {
    const ymd = this.toYMD(input);
    if (!ymd) return '';
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  };

  /** Convierte YYYY-MM-DD a ISO asegurando MEDIODÍA local → evita cruzar de día por zona horaria */
  private ymdToIsoAtNoon(ymd: string): string {
    const iso = new Date(`${ymd}T12:00:00`);
    return iso.toISOString();
  }

  /** Date (local) a partir de YMD en el mediodía local (para cálculos de semanas sin corrimientos) */
  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) return null;
    const d = new Date(`${ymd}T12:00:00`);
    return isNaN(d.getTime()) ? null : d;
  }
}

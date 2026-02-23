import { Component, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../services/nucleo.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { ProduccionService } from '../../services/produccion.service';

/** Item de lote postura producción para el filtro (Seguimiento Diario Producción). */
export interface LotePosturaProduccionFilterItem {
  lotePosturaProduccionId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId?: string | null;
  galponId?: string | null;
  avesHInicial?: number | null;
  avesMInicial?: number | null;
  avesHActual?: number | null;
  avesMActual?: number | null;
  estadoCierre?: string | null;
  /** Fecha de encasetamiento (desde Lote Levante). Usada para calcular edad en días/semanas. */
  fechaEncaset?: string | null;
}

/** Respuesta del endpoint filter-data (Granja → Núcleo → Galpón → Lote).
 * En el flujo LPP, lotes provienen de lote_postura_produccion (lotePosturaProduccionId, aves, estado).
 */
export interface FilterDataResponse {
  farms: FarmDto[];
  nucleos: NucleoDto[];
  galpones: Array<{ galponId: string; galponNombre: string; nucleoId: string; granjaId: number }>;
  /** Lotes LPP: tienen lotePosturaProduccionId, aves, estadoCierre. loteId se usa como alias de lotePosturaProduccionId para compatibilidad. */
  lotes: Array<LotePosturaProduccionFilterItem & { loteId?: number }>;
}

@Component({
  selector: 'app-filtro-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './filtro-select.component.html',
  styleUrls: ['./filtro-select.component.scss']
})
export class FiltroSelectComponent implements OnInit {
  // ================== constantes / sentinelas ==================
  readonly SIN_GALPON = '__SIN_GALPON__';

  // ================== inputs ==================
  @Input() selectedGranjaId: number | null = null;
  @Input() selectedNucleoId: string | null = null;
  @Input() selectedGalponId: string | null = null;
  @Input() selectedLoteId: number | null = null;
  /** Si se define, se carga todo en una sola llamada GET a esta URL (filter-data). */
  @Input() filterDataUrl: string | null = null;

  // ================== outputs ==================
  @Output() granjaChange = new EventEmitter<number | null>();
  @Output() nucleoChange = new EventEmitter<string | null>();
  @Output() galponChange = new EventEmitter<string | null>();
  @Output() loteChange = new EventEmitter<number | null>();
  @Output() filterDataLoaded = new EventEmitter<FilterDataResponse>();

  // ================== catálogos ==================
  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: Array<{ id: string; label: string }> = [];
  lotes: LoteDto[] = [];

  // ================== estado interno ==================
  hasSinGalpon = false;
  private allLotes: LoteDto[] = [];
  private allNucleos: NucleoDto[] = [];
  private allGalpones: Array<{ galponId: string; galponNombre: string; nucleoId: string; granjaId: number }> = [];
  private galponNameById = new Map<string, string>();

  constructor(
    private farmSvc: FarmService,
    private nucleoSvc: NucleoService,
    private loteSvc: LoteService,
    private galponSvc: GalponService,
    private produccionSvc: ProduccionService,
    private http: HttpClient
  ) { }

  ngOnInit(): void {
    if (this.filterDataUrl) {
      this.loadFromFilterData();
    } else {
      this.loadGranjas();
    }
  }

  private loadFromFilterData(): void {
    this.http.get<FilterDataResponse>(this.filterDataUrl!).subscribe({
      next: (data) => {
        this.granjas = data.farms ?? [];
        this.allNucleos = data.nucleos ?? [];
        this.allGalpones = data.galpones ?? [];
        const lotesRaw = data.lotes ?? [];
        const farmIds = new Set((data.farms ?? []).map(f => f.id));
        const nucleoIdsByGranja = new Map<number, Set<string>>();
        (data.nucleos ?? []).forEach(n => {
          if (!nucleoIdsByGranja.has(n.granjaId)) nucleoIdsByGranja.set(n.granjaId, new Set());
          nucleoIdsByGranja.get(n.granjaId)!.add(n.nucleoId);
        });
        const galponKeys = new Set((data.galpones ?? []).map(g => `${g.granjaId}|${g.nucleoId}|${String(g.galponId).trim()}`));
        const lotesValidos = lotesRaw.filter((l: any) => {
          if (!farmIds.has(l.granjaId)) return false;
          const nid = (l.nucleoId ?? '').trim();
          if (!nid || !nucleoIdsByGranja.get(l.granjaId)?.has(nid)) return false;
          const gid = (l.galponId ?? '').trim();
          if (!gid) return false;
          if (!galponKeys.has(`${l.granjaId}|${nid}|${gid}`)) return false;
          return true;
        });
        this.allLotes = lotesValidos.map((l: any) => {
          const lppId = l.lotePosturaProduccionId ?? l.loteId;
          return {
            loteId: lppId,
            loteNombre: l.loteNombre,
            granjaId: l.granjaId,
            nucleoId: l.nucleoId ?? undefined,
            galponId: l.galponId ?? undefined
          };
        }) as LoteDto[];
        this.galponNameById.clear();
        (data.galpones ?? []).forEach(g => {
          if (g.galponId) this.galponNameById.set(String(g.galponId).trim(), (g.galponNombre || g.galponId).trim());
        });
        this.filterDataLoaded.emit({ ...data, lotes: lotesValidos });
      },
      error: () => {
        this.granjas = [];
        this.allNucleos = [];
        this.allGalpones = [];
        this.allLotes = [];
      }
    });
  }

  // ================== CARGA DE CATÁLOGOS ==================
  private loadGranjas(): void {
    this.farmSvc.getAll().subscribe({
      next: fs => (this.granjas = fs || []),
      error: () => (this.granjas = [])
    });
  }

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

  private reloadLotesThenApplyFilters(): void {
    if (!this.selectedGranjaId) {
      this.allLotes = [];
      this.lotes = [];
      this.galpones = [];
      this.hasSinGalpon = false;
      return;
    }

    // Usar el nuevo endpoint que filtra lotes con semana >= 26 para producción
    this.produccionSvc.obtenerLotesProduccion().subscribe({
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

  private buildGalponesFromFilterData(): void {
    if (!this.selectedGranjaId) {
      this.galpones = [];
      this.hasSinGalpon = false;
      return;
    }
    const gid = Number(this.selectedGranjaId);
    const nid = this.selectedNucleoId ? String(this.selectedNucleoId) : null;
    const list = this.allGalpones.filter(g => g.granjaId === gid && (!nid || g.nucleoId === nid));
    const result: Array<{ id: string; label: string }> = list.map(g => ({
      id: String(g.galponId).trim(),
      label: (g.galponNombre || g.galponId).trim()
    }));
    this.hasSinGalpon = this.allLotes.some(l =>
      l.granjaId === gid && (!nid || String(l.nucleoId) === nid) && !this.hasValue(l.galponId)
    );
    if (this.hasSinGalpon) {
      result.unshift({ id: this.SIN_GALPON, label: '— Sin galpón —' });
    }
    this.galpones = result.sort((a, b) =>
      a.label.localeCompare(b.label, 'es', { numeric: true, sensitivity: 'base' })
    );
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

  // ================== EVENTOS DE CAMBIO ==================
  onGranjaChange(): void {
    this.granjaChange.emit(this.selectedGranjaId);
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.lotes = [];
    this.galpones = [];
    this.hasSinGalpon = false;
    this.nucleos = [];

    if (!this.selectedGranjaId) return;

    if (this.filterDataUrl) {
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

  onNucleoChange(): void {
    this.nucleoChange.emit(this.selectedNucleoId);
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.lotes = [];
    this.galpones = [];

    if (this.filterDataUrl) {
      this.applyFiltersToLotes();
      this.buildGalponesFromFilterData();
      return;
    }

    this.applyFiltersToLotes();
    this.loadGalponCatalog();
  }

  onGalponChange(): void {
    this.galponChange.emit(this.selectedGalponId);
    this.selectedLoteId = null;
    this.lotes = [];
    this.applyFiltersToLotes();
  }

  onLoteChange(): void {
    this.loteChange.emit(this.selectedLoteId);
  }

  // ================== HELPERS ==================
  private hasValue(v: unknown): boolean {
    if (v === null || v === undefined) return false;
    const s = String(v).trim().toLowerCase();
    return !(s === '' || s === '0' || s === 'null' || s === 'undefined');
  }

  private normalizeId(v: unknown): string {
    if (v === null || v === undefined) return '';
    return String(v).trim();
  }

  // ================== GETTERS ==================
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
    const id = (this.selectedGalponId ?? '').trim();
    return this.galponNameById.get(id) || id;
  }

  get selectedLoteNombre(): string {
    const l = this.lotes.find(x => x.loteId === this.selectedLoteId);
    return l?.loteNombre ?? (this.selectedLoteId?.toString() || '—');
  }

  // ================== TRACK BY FUNCTIONS ==================
  trackByNucleo = (_: number, n: NucleoDto) => n.nucleoId;
  trackByGalpon = (_: number, g: { id: string; label: string }) => g.id;
  trackByLote = (_: number, l: LoteDto) => l.loteId;
}

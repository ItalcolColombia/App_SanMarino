// frontend/src/app/features/traslados-huevos/pages/traslados-huevos-list/traslados-huevos-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FiltroSelectComponent } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';
import { ModalTrasladoHuevosComponent } from '../../components/modal-traslado-huevos/modal-traslado-huevos.component';
import { TrasladosHuevosService, TrasladoHuevosDto } from '../../services/traslados-huevos.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../lote-produccion/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { ProduccionService } from '../../../lote-produccion/services/produccion.service';

@Component({
  selector: 'app-traslados-huevos-list',
  standalone: true,
  imports: [CommonModule, FormsModule, SidebarComponent, FiltroSelectComponent, ModalTrasladoHuevosComponent],
  templateUrl: './traslados-huevos-list.component.html',
  styleUrls: ['./traslados-huevos-list.component.scss']
})
export class TrasladosHuevosListComponent implements OnInit {
  // ================== constantes / sentinelas ==================
  readonly SIN_GALPON = '__SIN_GALPON__';

  // ================== catálogos (otros) ==================
  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: Array<{ id: string; label: string }> = [];

  // ================== selección / filtro ==================
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;

  // ================== datos ==================
  private allLotes: LoteDto[] = [];
  lotes: LoteDto[] = [];
  traslados: TrasladoHuevosDto[] = [];
  filteredTraslados: TrasladoHuevosDto[] = [];
  selectedLote: LoteDto | null = null;

  // ================== Filtros de tabla ==================
  filtroBusqueda: string = '';
  filtroTipoOperacion: string = '';
  filtroEstado: string = '';

  // ================== UI ==================
  loading = false;
  error: string | null = null;
  modalOpen = false;
  editingTraslado: TrasladoHuevosDto | null = null;

  private galponNameById = new Map<string, string>();

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

  constructor(
    private farmSvc: FarmService,
    private nucleoSvc: NucleoService,
    private loteSvc: LoteService,
    private galponSvc: GalponService,
    private produccionSvc: ProduccionService,
    private trasladosService: TrasladosHuevosService
  ) {}

  // ================== INIT ==================
  ngOnInit(): void {
    // Inicializar filteredTraslados
    this.filteredTraslados = [];
    // cargar catálogos de granjas, etc.
    this.farmSvc.getAll().subscribe({
      next: fs => (this.granjas = fs || []),
      error: () => (this.granjas = [])
    });
  }


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
    this.traslados = [];
    this.galpones = [];
    this.lotes = [];
    this.selectedLote = null;
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
    this.traslados = [];
    this.selectedLote = null;
    this.applyFiltersToLotes();
    this.loadGalponCatalog();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteId = null;
    this.traslados = [];
    this.selectedLote = null;
    this.applyFiltersToLotes();
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLoteId = loteId;
    this.traslados = [];
    this.selectedLote = null;

    if (!this.selectedLoteId) return;

    this.loading = true;

    // Cargar datos del lote
    this.loteSvc.getById(this.selectedLoteId).subscribe({
      next: l => {
        this.selectedLote = l || null;
        this.loadTraslados();
      },
      error: () => {
        this.selectedLote = null;
        this.loading = false;
      }
    });
  }

  // ================== CARGA Y FILTRADO ==================
  private reloadLotesThenApplyFilters(): void {
    // Usar el endpoint que filtra lotes con semana >= 26 (producción)
    this.loading = true;
    this.produccionSvc.obtenerLotesProduccion()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (lotes) => {
          this.allLotes = lotes || [];
          this.applyFiltersToLotes();
          this.buildGalponesFromLotes();
        },
        error: (err) => {
          console.error('Error al cargar lotes de producción:', err);
          this.allLotes = [];
          this.applyFiltersToLotes();
          this.buildGalponesFromLotes();
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

    this.galpones = result.sort((a, b) =>
      a.label.localeCompare(b.label, 'es', { numeric: true, sensitivity: 'base' })
    );
  }

  // ================== CARGA DE TRASLADOS ==================
  private loadTraslados(): void {
    if (!this.selectedLoteId) return;

    this.loading = true;
    this.error = null;

    this.trasladosService.getTrasladosHuevosPorLote(this.selectedLoteId.toString())
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (traslados) => {
          this.traslados = traslados || [];
          this.filteredTraslados = traslados || [];
          this.aplicarFiltros();
        },
        error: (err) => {
          console.error('Error cargando traslados:', err);
          this.error = 'Error al cargar los traslados de huevos';
          this.traslados = [];
          this.filteredTraslados = [];
        }
      });
  }


  // ================== FILTROS DE TABLA ==================
  aplicarFiltros(): void {
    let filtered = [...this.traslados];

    // Filtro de búsqueda
    if (this.filtroBusqueda.trim()) {
      const term = this.filtroBusqueda.toLowerCase().trim();
      filtered = filtered.filter(t => {
        const searchText = [
          t.numeroTraslado || '',
          t.tipoOperacion || '',
          t.granjaDestinoNombre || '',
          t.motivo || '',
          t.estado || ''
        ].join(' ').toLowerCase();
        return searchText.includes(term);
      });
    }

    // Filtro por tipo de operación
    if (this.filtroTipoOperacion) {
      filtered = filtered.filter(t => t.tipoOperacion === this.filtroTipoOperacion);
    }

    // Filtro por estado
    if (this.filtroEstado) {
      filtered = filtered.filter(t => t.estado === this.filtroEstado);
    }

    this.filteredTraslados = filtered;
  }

  onFiltroChange(): void {
    this.aplicarFiltros();
  }

  limpiarFiltros(): void {
    this.filtroBusqueda = '';
    this.filtroTipoOperacion = '';
    this.filtroEstado = '';
    this.aplicarFiltros();
  }

  // ================== CRUD modal ==================
  create(): void {
    if (!this.selectedLoteId) return;
    this.editingTraslado = null;
    this.modalOpen = true;
  }

  viewDetail(traslado: TrasladoHuevosDto): void {
    // Cargar el traslado completo desde el backend para tener toda la información
    this.loading = true;
    this.trasladosService.getTrasladoHuevos(traslado.id).subscribe({
      next: (trasladoCompleto) => {
        this.editingTraslado = trasladoCompleto;
        this.modalOpen = true;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error cargando detalle del traslado:', err);
        // Si falla, usar el traslado que ya tenemos
        this.editingTraslado = traslado;
        this.modalOpen = true;
        this.loading = false;
      }
    });
  }

  editTraslado(traslado: TrasladoHuevosDto): void {
    // Cargar el traslado completo desde el backend para tener toda la información
    this.loading = true;
    this.trasladosService.getTrasladoHuevos(traslado.id).subscribe({
      next: (trasladoCompleto) => {
        this.editingTraslado = trasladoCompleto;
        this.modalOpen = true;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error cargando traslado para editar:', err);
        // Si falla, usar el traslado que ya tenemos
        this.editingTraslado = traslado;
        this.modalOpen = true;
        this.loading = false;
      }
    });
  }

  closeModal(): void {
    this.modalOpen = false;
    this.editingTraslado = null;
  }

  onTrasladoSaved(): void {
    this.closeModal();
    if (this.selectedLoteId) {
      this.loadTraslados();
    }
  }

  deleteTraslado(traslado: TrasladoHuevosDto): void {
    if (!confirm(`¿Estás seguro de que deseas cancelar el traslado ${traslado.numeroTraslado}?`)) {
      return;
    }

    if (traslado.estado === 'Pendiente') {
      this.loading = true;
      this.trasladosService.cancelarTrasladoHuevos(traslado.id, 'Cancelado por usuario').subscribe({
        next: () => {
          this.loading = false;
          this.loadTraslados();
        },
        error: (err) => {
          this.loading = false;
          this.error = 'Error al cancelar el traslado: ' + (err.message || 'Error desconocido');
          setTimeout(() => this.error = null, 5000);
        }
      });
    } else {
      alert('Solo se pueden cancelar traslados con estado "Pendiente"');
    }
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

  formatearNumero(num: number): string {
    return new Intl.NumberFormat('es-CO').format(num);
  }

  getTotalHuevos(traslado: TrasladoHuevosDto): number {
    return traslado.cantidadLimpio + traslado.cantidadTratado + traslado.cantidadSucio +
           traslado.cantidadDeforme + traslado.cantidadBlanco + traslado.cantidadDobleYema +
           traslado.cantidadPiso + traslado.cantidadPequeno + traslado.cantidadRoto +
           traslado.cantidadDesecho + traslado.cantidadOtro;
  }

  calcularEdadDiasDesdeEncasetamiento(): number {
    if (!this.selectedLote?.fechaEncaset) return 0;
    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const hoy = new Date();
    const diffTime = hoy.getTime() - fechaEncaset.getTime();
    return Math.floor(diffTime / (1000 * 60 * 60 * 24));
  }
}

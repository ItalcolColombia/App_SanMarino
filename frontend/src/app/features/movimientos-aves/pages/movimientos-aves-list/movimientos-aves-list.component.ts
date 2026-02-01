// frontend/src/app/features/movimientos-aves/pages/movimientos-aves-list/movimientos-aves-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FiltroSelectComponent } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';
import { ModalMovimientoAvesComponent } from '../../components/modal-movimiento-aves/modal-movimiento-aves.component';
import { MovimientosAvesService, MovimientoAvesDto } from '../../services/movimientos-aves.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../lote-produccion/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { ProduccionService } from '../../../lote-produccion/services/produccion.service';

@Component({
  selector: 'app-movimientos-aves-list',
  standalone: true,
  imports: [CommonModule, FormsModule, SidebarComponent, FiltroSelectComponent, ModalMovimientoAvesComponent],
  templateUrl: './movimientos-aves-list.component.html',
  styleUrls: ['./movimientos-aves-list.component.scss']
})
export class MovimientosAvesListComponent implements OnInit {
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
  movimientos: MovimientoAvesDto[] = [];
  filteredMovimientos: MovimientoAvesDto[] = [];
  selectedLote: LoteDto | null = null;

  // ================== Filtros de tabla ==================
  filtroBusqueda: string = '';
  filtroTipoMovimiento: string = '';
  filtroEstado: string = '';

  // ================== UI ==================
  loading = false;
  error: string | null = null;
  modalOpen = false;
  editingMovimiento: MovimientoAvesDto | null = null;

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
    private movimientosService: MovimientosAvesService
  ) {}

  // ================== INIT ==================
  ngOnInit(): void {
    // Inicializar filteredMovimientos
    this.filteredMovimientos = [];
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
    this.movimientos = [];
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
    this.movimientos = [];
    this.selectedLote = null;
    this.applyFiltersToLotes();
    this.loadGalponCatalog();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteId = null;
    this.movimientos = [];
    this.selectedLote = null;
    this.applyFiltersToLotes();
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLoteId = loteId;
    this.movimientos = [];
    this.selectedLote = null;

    if (!this.selectedLoteId) return;

    this.loading = true;

    // Cargar datos del lote
    this.loteSvc.getById(this.selectedLoteId).subscribe({
      next: l => {
        this.selectedLote = l || null;
        this.loadMovimientos();
      },
      error: () => {
        this.selectedLote = null;
        this.loading = false;
      }
    });
  }

  // ================== CARGA Y FILTRADO ==================
  private reloadLotesThenApplyFilters(): void {
    // Cargar todos los lotes (tanto de Levante como de Producción)
    // Los lotes con registros diarios aparecerán en ambos módulos
    this.loading = true;
    
    // Cargar todos los lotes disponibles
    this.loteSvc.getAll()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (todosLotes) => {
          // Combinar con lotes de producción (que tienen semana >= 26)
          this.produccionSvc.obtenerLotesProduccion()
            .subscribe({
              next: (lotesProduccion) => {
                // Combinar ambos listados y eliminar duplicados
                const allLotesMap = new Map<number, LoteDto>();
                
                (todosLotes || []).forEach(l => {
                  if (l.loteId) allLotesMap.set(l.loteId, l);
                });
                
                (lotesProduccion || []).forEach(l => {
                  if (l.loteId) allLotesMap.set(l.loteId, l);
                });
                
                this.allLotes = Array.from(allLotesMap.values());
                this.applyFiltersToLotes();
                this.buildGalponesFromLotes();
              },
              error: (err) => {
                console.error('Error al cargar lotes de producción:', err);
                this.allLotes = todosLotes || [];
                this.applyFiltersToLotes();
                this.buildGalponesFromLotes();
              }
            });
        },
        error: (err) => {
          console.error('Error al cargar lotes:', err);
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

  // ================== CARGA DE MOVIMIENTOS ==================
  private loadMovimientos(): void {
    if (!this.selectedLoteId) return;

    this.loading = true;
    this.error = null;

    this.movimientosService.getMovimientosAvesPorLote(this.selectedLoteId)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (movimientos) => {
          this.movimientos = movimientos || [];
          this.filteredMovimientos = movimientos || [];
          this.aplicarFiltros();
        },
        error: (err) => {
          console.error('Error cargando movimientos:', err);
          this.error = 'Error al cargar los movimientos de aves';
          this.movimientos = [];
          this.filteredMovimientos = [];
        }
      });
  }

  // ================== FILTROS DE TABLA ==================
  aplicarFiltros(): void {
    let filtered = [...this.movimientos];

    // Filtro de búsqueda
    if (this.filtroBusqueda.trim()) {
      const term = this.filtroBusqueda.toLowerCase().trim();
      filtered = filtered.filter(m => {
        const searchText = [
          m.numeroMovimiento || '',
          m.tipoMovimiento || '',
          m.origen?.granjaNombre || '',
          m.destino?.granjaNombre || '',
          m.motivoMovimiento || '',
          m.estado || ''
        ].join(' ').toLowerCase();
        return searchText.includes(term);
      });
    }

    // Filtro por tipo de movimiento
    if (this.filtroTipoMovimiento) {
      filtered = filtered.filter(m => m.tipoMovimiento === this.filtroTipoMovimiento);
    }

    // Filtro por estado
    if (this.filtroEstado) {
      filtered = filtered.filter(m => m.estado === this.filtroEstado);
    }

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

  // ================== CRUD modal ==================
  create(): void {
    if (!this.selectedLoteId) return;
    this.editingMovimiento = null;
    this.modalOpen = true;
  }

  viewDetail(movimiento: MovimientoAvesDto): void {
    // Cargar el movimiento completo desde el backend para tener toda la información
    this.loading = true;
    this.movimientosService.getMovimientoAves(movimiento.id).subscribe({
      next: (movimientoCompleto) => {
        this.editingMovimiento = movimientoCompleto;
        this.modalOpen = true;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error cargando detalle del movimiento:', err);
        // Si falla, usar el movimiento que ya tenemos
        this.editingMovimiento = movimiento;
        this.modalOpen = true;
        this.loading = false;
      }
    });
  }

  editMovimiento(movimiento: MovimientoAvesDto): void {
    // Cargar el movimiento completo desde el backend para tener toda la información
    this.loading = true;
    this.movimientosService.getMovimientoAves(movimiento.id).subscribe({
      next: (movimientoCompleto) => {
        this.editingMovimiento = movimientoCompleto;
        this.modalOpen = true;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error cargando movimiento para editar:', err);
        // Si falla, usar el movimiento que ya tenemos
        this.editingMovimiento = movimiento;
        this.modalOpen = true;
        this.loading = false;
      }
    });
  }

  closeModal(): void {
    this.modalOpen = false;
    this.editingMovimiento = null;
  }

  onMovimientoSaved(): void {
    this.closeModal();
    if (this.selectedLoteId) {
      this.loadMovimientos();
    }
  }

  deleteMovimiento(movimiento: MovimientoAvesDto): void {
    if (!confirm(`¿Estás seguro de que deseas cancelar el movimiento ${movimiento.numeroMovimiento}?`)) {
      return;
    }

    if (movimiento.estado === 'Pendiente' || movimiento.estado === 'Completado') {
      this.loading = true;
      this.movimientosService.cancelarMovimientoAves(movimiento.id, 'Cancelado por usuario').subscribe({
        next: () => {
          this.loading = false;
          this.loadMovimientos();
        },
        error: (err) => {
          this.loading = false;
          this.error = 'Error al cancelar el movimiento: ' + (err.message || 'Error desconocido');
          setTimeout(() => this.error = null, 5000);
        }
      });
    } else {
      alert('No se puede cancelar un movimiento que ya está cancelado');
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

  calcularEdadDiasDesdeEncasetamiento(): number {
    if (!this.selectedLote?.fechaEncaset) return 0;
    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const hoy = new Date();
    const diffTime = hoy.getTime() - fechaEncaset.getTime();
    return Math.floor(diffTime / (1000 * 60 * 60 * 24));
  }
}

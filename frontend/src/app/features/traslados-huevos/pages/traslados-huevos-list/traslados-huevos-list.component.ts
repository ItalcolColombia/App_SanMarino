// frontend/src/app/features/traslados-huevos/pages/traslados-huevos-list/traslados-huevos-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FiltroSelectComponent, FilterDataResponse } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';
import { ModalTrasladoHuevosComponent } from '../../components/modal-traslado-huevos/modal-traslado-huevos.component';
import { TrasladosHuevosService, TrasladoHuevosDto } from '../../services/traslados-huevos.service';
import { FarmDto } from '../../../farm/services/farm.service';
import { NucleoDto } from '../../../lote-produccion/services/nucleo.service';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-traslados-huevos-list',
  standalone: true,
  imports: [CommonModule, FormsModule, SidebarComponent, FiltroSelectComponent, ModalTrasladoHuevosComponent],
  templateUrl: './traslados-huevos-list.component.html',
  styleUrls: ['./traslados-huevos-list.component.scss']
})
export class TrasladosHuevosListComponent implements OnInit {
  // ================== filter-data (igual que SeguimientoProduccion) ==================
  /** URL para filter-data: Granja → Núcleo → Galpón → Lote LPP en una sola petición */
  readonly filterDataUrl = `${environment.apiUrl}/traslados/filter-data`;

  // ================== catálogos (desde filter-data) ==================
  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: Array<{ galponId: string; galponNombre: string; nucleoId: string; granjaId: number }> = [];
  lotes: Array<{ loteId: number; loteNombre: string; fechaEncaset?: string | null }> = [];

  // ================== selección / filtro ==================
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;

  // ================== datos ==================
  traslados: TrasladoHuevosDto[] = [];
  filteredTraslados: TrasladoHuevosDto[] = [];
  /** Lote LPP seleccionado (para fechaEncaset, etc.) */
  selectedLoteInfo: { loteNombre: string; fechaEncaset?: string | null } | null = null;

  // ================== Filtros de tabla ==================
  filtroBusqueda: string = '';
  filtroTipoOperacion: string = '';
  filtroEstado: string = '';

  // ================== UI ==================
  loading = false;
  error: string | null = null;
  modalOpen = false;
  editingTraslado: TrasladoHuevosDto | null = null;

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
    if (this.selectedGalponId === '__SIN_GALPON__') return '— Sin galpón —';
    const g = this.galpones.find(x => String(x.galponId).trim() === String(this.selectedGalponId).trim());
    return g?.galponNombre ?? this.selectedGalponId?.toString() ?? '—';
  }

  get selectedLoteNombre(): string {
    const l = this.lotes.find(x => x.loteId === this.selectedLoteId);
    return l?.loteNombre ?? this.selectedLoteInfo?.loteNombre ?? (this.selectedLoteId?.toString() || '—');
  }

  constructor(private trasladosService: TrasladosHuevosService) {}

  // ================== INIT ==================
  ngOnInit(): void {
    this.filteredTraslados = [];
  }

  onFilterDataLoaded(data: FilterDataResponse): void {
    this.granjas = data.farms ?? [];
    this.nucleos = data.nucleos ?? [];
    this.galpones = data.galpones ?? [];
    const raw = data.lotes ?? [];
    this.lotes = raw.map((l: any) => ({
      loteId: l.lotePosturaProduccionId ?? l.loteId,
      loteNombre: l.loteNombre ?? '',
      fechaEncaset: l.fechaEncaset ?? null
    }));
  }

  // ================== CASCADA DE FILTROS (manejados por FiltroSelectComponent) ==================
  onGranjaChange(granjaId: number | null): void {
    this.selectedGranjaId = granjaId;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.traslados = [];
    this.filteredTraslados = [];
    this.selectedLoteInfo = null;
  }

  onNucleoChange(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.traslados = [];
    this.filteredTraslados = [];
    this.selectedLoteInfo = null;
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteId = null;
    this.traslados = [];
    this.filteredTraslados = [];
    this.selectedLoteInfo = null;
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLoteId = loteId;
    this.traslados = [];
    this.filteredTraslados = [];
    const l = this.lotes.find(x => x.loteId === loteId);
    this.selectedLoteInfo = l ? { loteNombre: l.loteNombre, fechaEncaset: l.fechaEncaset } : null;

    if (!this.selectedLoteId) return;

    this.loadTraslados();
  }

  // ================== CARGA DE TRASLADOS ==================
  private loadTraslados(): void {
    if (!this.selectedLoteId) return;

    this.loading = true;
    this.error = null;

    // Traslados LPP usan LoteId = "LPP-{id}"
    const loteKey = `LPP-${this.selectedLoteId}`;
    this.trasladosService.getTrasladosHuevosPorLote(loteKey)
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
    const fechaEncaset = this.selectedLoteInfo?.fechaEncaset;
    if (!fechaEncaset) return 0;
    const fecha = new Date(fechaEncaset);
    const hoy = new Date();
    const diffTime = hoy.getTime() - fecha.getTime();
    return Math.floor(diffTime / (1000 * 60 * 60 * 24));
  }
}

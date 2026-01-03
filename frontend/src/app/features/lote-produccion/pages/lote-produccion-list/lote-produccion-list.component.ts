import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';

import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';

import { LoteService, LoteDto } from '../../../lote/services/lote.service';

import {
  ProduccionService,
  SeguimientoItemDto,
  CrearSeguimientoRequest,
  CrearProduccionLoteRequest,
  ProduccionLoteDetalleDto,
  ExisteProduccionLoteResponse
} from '../../services/produccion.service';

import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../services/nucleo.service';
import { FiltroSelectComponent } from '../filtro-select/filtro-select.component';
import { TabsPrincipalComponent } from '../tabs-principal/tabs-principal.component';
import { ModalRegistroInicialComponent } from '../modal-registro-inicial/modal-registro-inicial.component';
import { ModalSeguimientoDiarioComponent } from '../modal-seguimiento-diario/modal-seguimiento-diario.component';
import { ModalAnalisisComponent } from '../modal-analisis/modal-analisis.component';
import { ModalLiquidacionComponent } from '../../components/modal-liquidacion/modal-liquidacion.component';
import { ModalDetalleSeguimientoComponent } from '../modal-detalle-seguimiento/modal-detalle-seguimiento.component';

@Component({
  selector: 'app-lote-produccion-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SidebarComponent,
    FiltroSelectComponent,
    TabsPrincipalComponent,
    ModalRegistroInicialComponent,
    ModalSeguimientoDiarioComponent,
    ModalAnalisisComponent,
    ModalLiquidacionComponent,
    ModalDetalleSeguimientoComponent
  ],
  templateUrl: './lote-produccion-list.component.html',
  styleUrls: ['./lote-produccion-list.component.scss']
})
export class LoteProduccionListComponent implements OnInit {
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
  seguimientos: SeguimientoItemDto[] = [];

  selectedLote: LoteDto | null = null;
  produccionLote: ProduccionLoteDetalleDto | null = null;
  currentProduccionLoteId: number | null = null;

  // Datos para el modal de registro inicial
  modalLoteNombre: string = '';
  modalNucleoAsignado: string = '';
  modalNucleosDisponibles: Array<{ nucleoId: string, nucleoNombre: string }> = [];

  // ================== UI ==================
  loading = false;
  modalRegistroInicialOpen = false;
  modalSeguimientoDiarioOpen = false;
  analisisOpen = false;
  liquidacionOpen = false;
  modalDetalleSeguimientoOpen = false;
  seguimientoIdParaDetalle: number | null = null;
  editingSeguimiento: SeguimientoItemDto | null = null;

  private galponNameById = new Map<string, string>();

  constructor(
    private farmSvc: FarmService,
    private nucleoSvc: NucleoService,
    private loteSvc: LoteService,
    private produccionSvc: ProduccionService,
    private galponSvc: GalponService
  ) {}

  // ================== INIT ==================
  ngOnInit(): void {
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
    this.seguimientos = [];
    this.galpones = [];
    this.lotes = [];
    this.selectedLote = null;
    this.produccionLote = null;
    this.currentProduccionLoteId = null;
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
    this.produccionLote = null;
    this.currentProduccionLoteId = null;
    this.applyFiltersToLotes();
    this.loadGalponCatalog();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteId = null;
    this.seguimientos = [];
    this.selectedLote = null;
    this.produccionLote = null;
    this.currentProduccionLoteId = null;
    this.applyFiltersToLotes();
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLoteId = loteId;
    this.seguimientos = [];
    this.selectedLote = null;
    this.produccionLote = null;
    this.currentProduccionLoteId = null;

    if (!this.selectedLoteId) return;

    this.loading = true;

    // Cargar datos del lote
    this.loteSvc.getById(this.selectedLoteId).subscribe({
      next: l => (this.selectedLote = l || null),
      error: () => (this.selectedLote = null)
    });

    // Verificar si existe registro inicial de producción
    this.produccionSvc.existsProduccionLote(this.selectedLoteId).subscribe({
      next: (response: ExisteProduccionLoteResponse) => {
        this.currentProduccionLoteId = response.produccionLoteId || null;

        if (response.exists && this.currentProduccionLoteId) {
          // Cargar el detalle de ProduccionLote
          this.produccionSvc.getProduccionLote(this.selectedLoteId!).subscribe({
            next: (detalle) => {
              this.produccionLote = detalle;
              // Cargar seguimientos diarios
              this.loadSeguimientos();
            },
            error: () => {
              this.produccionLote = null;
              this.loading = false;
            }
          });
        } else {
          this.produccionLote = null;
          this.loading = false;
        }
      },
      error: () => {
        this.produccionLote = null;
        this.loading = false;
      }
    });
  }

  private loadSeguimientos(): void {
    if (!this.selectedLoteId) return;

    this.produccionSvc.listarSeguimiento({
      loteId: this.selectedLoteId,
      page: 1,
      size: 100
    }).pipe(finalize(() => (this.loading = false)))
    .subscribe({
      next: response => (this.seguimientos = response.items || []),
      error: () => (this.seguimientos = [])
    });
  }

  // ================== CARGA Y FILTRADO ==================
  private reloadLotesThenApplyFilters(): void {
    // Usar el nuevo endpoint que filtra lotes con semana >= 26
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

  // ================== CRUD modal ==================
  create(): void {
    if (!this.selectedLoteId) return;

    // Verificar si existe registro inicial
    this.produccionSvc.existsProduccionLote(this.selectedLoteId).subscribe({
      next: (response: ExisteProduccionLoteResponse) => {
        if (response.exists && response.produccionLoteId) {
          // Abrir modal de seguimiento diario
          this.currentProduccionLoteId = response.produccionLoteId;
          this.editingSeguimiento = null;
          this.modalSeguimientoDiarioOpen = true;
        } else {
          // Abrir modal de registro inicial - cargar datos del lote
          this.loadLoteDataForModal();
        }
      },
      error: () => {
        // En caso de error, abrir modal de registro inicial - cargar datos del lote
        this.loadLoteDataForModal();
      }
    });
  }

  // Cargar datos del lote para el modal de registro inicial
  private loadLoteDataForModal(): void {
    if (!this.selectedLoteId) return;

    // 1. Obtener detalles del lote
    this.loteSvc.getById(this.selectedLoteId).subscribe({
      next: (lote: any) => {
        this.modalLoteNombre = lote.loteNombre || '—';
        this.modalNucleoAsignado = lote.nucleo?.nucleoNombre || lote.nucleoId || '';

        // 2. Obtener todos los núcleos de la granja
        if (lote.granjaId) {
          this.nucleoSvc.getByGranja(lote.granjaId).subscribe({
            next: (nucleos: NucleoDto[]) => {
              this.modalNucleosDisponibles = nucleos.map(n => ({
                nucleoId: n.nucleoId,
                nucleoNombre: n.nucleoNombre || n.nucleoId
              }));

              // 3. Abrir modal con los datos
              this.modalRegistroInicialOpen = true;
            },
            error: () => {
              this.modalNucleosDisponibles = [];
              this.modalRegistroInicialOpen = true;
            }
          });
        } else {
          this.modalNucleosDisponibles = [];
          this.modalRegistroInicialOpen = true;
        }
      },
      error: () => {
        this.modalLoteNombre = '';
        this.modalNucleoAsignado = '';
        this.modalNucleosDisponibles = [];
        this.modalRegistroInicialOpen = true;
      }
    });
  }

  edit(seg: SeguimientoItemDto): void {
    this.editingSeguimiento = seg;
    this.modalSeguimientoDiarioOpen = true;
  }

  delete(id: number): void {
    if (!confirm('¿Eliminar este registro?')) return;
    // TODO: Implementar delete en el servicio
    // this.produccionSvc.delete(id).subscribe(() => this.onLoteChange(this.selectedLoteId));
  }

  // ================== MODALES ==================
  cancelRegistroInicial(): void {
    this.modalRegistroInicialOpen = false;
  }

  onSaveRegistroInicial(request: CrearProduccionLoteRequest): void {
    this.loading = true;
    this.produccionSvc.crearProduccionLote(request)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (produccionLoteId: number) => {
          this.modalRegistroInicialOpen = false;
          this.currentProduccionLoteId = produccionLoteId;

          // Abrir modal de seguimiento diario automáticamente
          this.editingSeguimiento = null;
          this.modalSeguimientoDiarioOpen = true;
        },
        error: () => { /* TODO: toast de error */ }
      });
  }

  cancelSeguimientoDiario(): void {
    this.modalSeguimientoDiarioOpen = false;
    this.editingSeguimiento = null;
  }

  onSaveSeguimientoDiario(request: CrearSeguimientoRequest): void {
    this.loading = true;
    this.produccionSvc.crearSeguimiento(request)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          this.modalSeguimientoDiarioOpen = false;
          this.editingSeguimiento = null;
          this.loadSeguimientos();
        },
        error: () => { /* TODO: toast de error */ }
      });
  }

  openAnalisis(): void {
    if (!this.selectedLoteId) return;
    this.analisisOpen = true;
  }

  closeAnalisis(): void {
    this.analisisOpen = false;
  }

  // ================== LIQUIDACIÓN TÉCNICA ==================
  openLiquidacion(): void {
    if (!this.selectedLoteId) return;
    this.liquidacionOpen = true;
  }

  closeLiquidacion(): void {
    this.liquidacionOpen = false;
  }

  // ================== helpers ==================
  trackById = (_: number, r: SeguimientoItemDto) => r.id;
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
    const id = (this.selectedGalponId ?? '').trim();
    return this.galponNameById.get(id) || id;
  }

  /** Edad (en días) a HOY desde fecha de inicio (mínimo 1). */
  calcularEdadDias(fechaInicio?: string | Date | null): number {
    const inicioYmd = this.toYMD(fechaInicio);
    const inicio = this.ymdToLocalNoonDate(inicioYmd);
    if (!inicio) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const now = this.ymdToLocalNoonDate(this.todayYMD())!;
    return Math.max(1, Math.floor((now.getTime() - inicio.getTime()) / MS_DAY) + 1);
  }

  /** 
   * Calcula la edad en días desde la fecha de encasetamiento del lote (fechaEncaset).
   * Siempre usa la fecha de creación/encasetamiento del lote, no la de ProduccionLote.
   */
  calcularEdadDiasDesdeEncasetamiento(): number {
    if (!this.selectedLote?.fechaEncaset) return 0;
    return this.calcularEdadDias(this.selectedLote.fechaEncaset);
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

  // ================== HELPERS DE FECHA ==================
  private todayYMD(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

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
      let a = parseInt(m2[1], 10);
      let b = parseInt(m2[2], 10);
      const yyyy = parseInt(m2[3], 10);
      let mm = a, dd = b;
      if (a > 12 && b <= 12) { mm = b; dd = a; }
      const mmS = String(mm).padStart(2, '0');
      const ddS = String(dd).padStart(2, '0');
      return `${yyyy}-${mmS}-${ddS}`;
    }

    // ISO (con T)
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${y}-${m}-${day}`;
    }

    return null;
  }

  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) return null;
    const d = new Date(`${ymd}T12:00:00`);
    return isNaN(d.getTime()) ? null : d;
  }

  // ================== MÉTODOS DE MODALES ==================
  openDailyTrackingModal(): void {
    if (!this.selectedLoteId || !this.produccionLote?.id) return;
    this.editingSeguimiento = null;
    this.modalSeguimientoDiarioOpen = true;
  }

  editDailyTracking(seguimiento: SeguimientoItemDto): void {
    this.editingSeguimiento = seguimiento;
    this.modalSeguimientoDiarioOpen = true;
  }

  openDetailModal(seguimiento: SeguimientoItemDto): void {
    this.seguimientoIdParaDetalle = seguimiento.id;
    this.modalDetalleSeguimientoOpen = true;
  }

  closeDetailModal(): void {
    this.modalDetalleSeguimientoOpen = false;
    this.seguimientoIdParaDetalle = null;
  }

  deleteDailyTracking(id: number): void {
    if (!confirm('¿Estás seguro de eliminar este registro de seguimiento diario? Esta acción no se puede deshacer.')) return;

    this.loading = true;
    this.produccionSvc.eliminarSeguimiento(id).subscribe({
      next: () => {
        this.loading = false;
        // Recargar los seguimientos del lote después de eliminar
        this.onLoteChange(this.selectedLoteId);
      },
      error: (err) => {
        this.loading = false;
        console.error('Error al eliminar registro:', err);
        const errorMessage = err?.error?.message || err?.message || 'Error al eliminar el registro. Por favor, intenta nuevamente.';
        alert(errorMessage);
        // Recargar los seguimientos incluso si hay error para mantener consistencia
        this.onLoteChange(this.selectedLoteId);
      }
    });
  }

  exportarAnalisis(): void {
    // TODO: Implementar exportación de análisis
    console.log('Exportar análisis');
  }
}

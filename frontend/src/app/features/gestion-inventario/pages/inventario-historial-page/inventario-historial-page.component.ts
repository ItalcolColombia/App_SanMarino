// src/app/features/gestion-inventario/pages/inventario-historial-page/inventario-historial-page.component.ts
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  GestionInventarioService,
  InventarioGestionFilterDataDto,
  InventarioGestionTrasladoListDto,
  InventarioGestionIngresoListDto,
  FarmDto,
  NucleoDto,
  GalponLiteDto,
} from '../../services/gestion-inventario.service';

type ActiveTab = 'traslados' | 'ingresos';

interface TrasladoFilter {
  farmId: number | null;
  nucleoId: string;
  galponId: string;
  fechaDesde: string;
  fechaHasta: string;
  search: string;
}

interface IngresoFilter {
  farmId: number | null;
  nucleoId: string;
  galponId: string;
  fechaDesde: string;
  fechaHasta: string;
  search: string;
}

const emptyTrasladoFilter = (): TrasladoFilter => ({
  farmId: null, nucleoId: '', galponId: '', fechaDesde: '', fechaHasta: '', search: '',
});
const emptyIngresoFilter = (): IngresoFilter => ({
  farmId: null, nucleoId: '', galponId: '', fechaDesde: '', fechaHasta: '', search: '',
});

@Component({
  selector: 'app-inventario-historial-page',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, RouterModule],
  templateUrl: './inventario-historial-page.component.html',
  styleUrls: ['./inventario-historial-page.component.scss'],
})
export class InventarioHistorialPageComponent implements OnInit {
  private svc = inject(GestionInventarioService);

  activeTab: ActiveTab = 'traslados';

  // ── Filter data ──────────────────────────────────────────────────────────────
  filterData: InventarioGestionFilterDataDto | null = null;
  filterDataLoading = false;

  // ── Traslados ─────────────────────────────────────────────────────────────────
  traslados: InventarioGestionTrasladoListDto[] = [];
  trasladosLoading = false;
  trasladosFilter = emptyTrasladoFilter();

  get nucleosTrasladosFiltrados(): NucleoDto[] {
    if (!this.filterData) return [];
    if (!this.trasladosFilter.farmId) return this.filterData.nucleosOrigen;
    return this.filterData.nucleosOrigen.filter(n => n.granjaId === this.trasladosFilter.farmId);
  }
  get galponesTrasladosFiltrados(): GalponLiteDto[] {
    if (!this.filterData || !this.trasladosFilter.nucleoId) return [];
    return this.filterData.galponesOrigen.filter(g => g.nucleoId === this.trasladosFilter.nucleoId);
  }

  // ── Ingresos ──────────────────────────────────────────────────────────────────
  ingresos: InventarioGestionIngresoListDto[] = [];
  ingresosLoading = false;
  ingresosFilter = emptyIngresoFilter();

  get nucleosIngresosFiltrados(): NucleoDto[] {
    if (!this.filterData) return [];
    if (!this.ingresosFilter.farmId) return this.filterData.nucleosOrigen;
    return this.filterData.nucleosOrigen.filter(n => n.granjaId === this.ingresosFilter.farmId);
  }
  get galponesIngresosFiltrados(): GalponLiteDto[] {
    if (!this.filterData || !this.ingresosFilter.nucleoId) return [];
    return this.filterData.galponesOrigen.filter(g => g.nucleoId === this.ingresosFilter.nucleoId);
  }

  // ── Edit date modal ───────────────────────────────────────────────────────────
  editOpen = false;
  editType: ActiveTab = 'traslados';
  editId: string | number = '';
  editFecha = '';
  editSaving = false;
  editError = '';

  ngOnInit(): void {
    this.loadFilterData();
    this.loadTraslados();
  }

  loadFilterData(): void {
    this.filterDataLoading = true;
    this.svc.getFilterData().subscribe({
      next: data => { this.filterData = data; this.filterDataLoading = false; },
      error: () => { this.filterDataLoading = false; },
    });
  }

  // ── Tab ───────────────────────────────────────────────────────────────────────
  switchTab(tab: ActiveTab): void {
    this.activeTab = tab;
    if (tab === 'ingresos' && this.ingresos.length === 0 && !this.ingresosLoading) {
      this.loadIngresos();
    }
  }

  // ── Traslados actions ─────────────────────────────────────────────────────────
  onFarmChangeTraslados(): void {
    this.trasladosFilter.nucleoId = '';
    this.trasladosFilter.galponId = '';
  }
  onNucleoChangeTraslados(): void {
    this.trasladosFilter.galponId = '';
  }

  loadTraslados(): void {
    this.trasladosLoading = true;
    const f = this.trasladosFilter;
    this.svc.getTraslados({
      farmId: f.farmId ?? undefined,
      nucleoId: f.nucleoId || undefined,
      galponId: f.galponId || undefined,
      fechaDesde: f.fechaDesde || undefined,
      fechaHasta: f.fechaHasta || undefined,
      search: f.search || undefined,
    }).subscribe({
      next: data => { this.traslados = data; this.trasladosLoading = false; },
      error: () => { this.trasladosLoading = false; },
    });
  }

  clearFiltersTraslados(): void {
    this.trasladosFilter = emptyTrasladoFilter();
    this.loadTraslados();
  }

  openEditTraslado(t: InventarioGestionTrasladoListDto): void {
    this.editType = 'traslados';
    this.editId = t.transferGroupId;
    this.editFecha = t.fechaMovimiento.substring(0, 10);
    this.editError = '';
    this.editOpen = true;
  }

  // ── Ingresos actions ──────────────────────────────────────────────────────────
  onFarmChangeIngresos(): void {
    this.ingresosFilter.nucleoId = '';
    this.ingresosFilter.galponId = '';
  }
  onNucleoChangeIngresos(): void {
    this.ingresosFilter.galponId = '';
  }

  loadIngresos(): void {
    this.ingresosLoading = true;
    const f = this.ingresosFilter;
    this.svc.getIngresos({
      farmId: f.farmId ?? undefined,
      nucleoId: f.nucleoId || undefined,
      galponId: f.galponId || undefined,
      fechaDesde: f.fechaDesde || undefined,
      fechaHasta: f.fechaHasta || undefined,
      search: f.search || undefined,
    }).subscribe({
      next: data => { this.ingresos = data; this.ingresosLoading = false; },
      error: () => { this.ingresosLoading = false; },
    });
  }

  clearFiltersIngresos(): void {
    this.ingresosFilter = emptyIngresoFilter();
    this.loadIngresos();
  }

  openEditIngreso(i: InventarioGestionIngresoListDto): void {
    this.editType = 'ingresos';
    this.editId = i.movimientoId;
    this.editFecha = i.fechaMovimiento.substring(0, 10);
    this.editError = '';
    this.editOpen = true;
  }

  // ── Edit modal ────────────────────────────────────────────────────────────────
  closeEdit(): void {
    if (this.editSaving) return;
    this.editOpen = false;
  }

  saveEdit(): void {
    if (!this.editFecha || this.editSaving) return;
    this.editSaving = true;
    this.editError = '';

    if (this.editType === 'traslados') {
      this.svc.actualizarFechaTraslado(this.editId as string, { fechaMovimiento: this.editFecha }).subscribe({
        next: updated => {
          const idx = this.traslados.findIndex(t => t.transferGroupId === this.editId);
          if (idx >= 0) this.traslados = [...this.traslados.slice(0, idx), updated, ...this.traslados.slice(idx + 1)];
          this.editOpen = false;
          this.editSaving = false;
        },
        error: err => {
          this.editError = err?.error?.message ?? 'Error al guardar la fecha.';
          this.editSaving = false;
        },
      });
    } else {
      this.svc.actualizarFechaIngreso(this.editId as number, { fechaMovimiento: this.editFecha }).subscribe({
        next: updated => {
          const idx = this.ingresos.findIndex(i => i.movimientoId === this.editId);
          if (idx >= 0) this.ingresos = [...this.ingresos.slice(0, idx), updated, ...this.ingresos.slice(idx + 1)];
          this.editOpen = false;
          this.editSaving = false;
        },
        error: err => {
          this.editError = err?.error?.message ?? 'Error al guardar la fecha.';
          this.editSaving = false;
        },
      });
    }
  }

  // ── Helpers ───────────────────────────────────────────────────────────────────
  estadoBadgeClass(estado: string | null): string {
    switch (estado?.toLowerCase()) {
      case 'completado': return 'badge-completado';
      case 'en tránsito': return 'badge-transito';
      case 'pendiente despacho': return 'badge-pendiente';
      case 'rechazado': return 'badge-rechazado';
      default: return 'badge-default';
    }
  }

  formatUbicacion(granja: string | null, nucleo: string | null, galpon: string | null): string {
    const parts = [granja, nucleo, galpon].filter(Boolean);
    return parts.length ? parts.join(' › ') : '—';
  }
}

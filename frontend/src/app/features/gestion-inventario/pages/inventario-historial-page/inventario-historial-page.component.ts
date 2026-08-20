// src/app/features/gestion-inventario/pages/inventario-historial-page/inventario-historial-page.component.ts
import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
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
import {
  esFechaIngresoOfrecible,
  esFechaMovimientoPermitida,
  extremosFechaIngreso,
  hintFechaIngreso,
  mensajeFechaFueraDeVentana,
  mensajeFechaIngresoFueraDeVentana,
  PERMISO_FECHA_RETROACTIVA,
  type VentanaFechaIngreso,
} from '../../funciones/ventana-fecha-movimiento.funcion';
import { hintVentanaFechaRegistro } from '../../../../shared/utils/fecha/ventana-fecha-registro.funcion';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';

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
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./inventario-historial-page.component.scss'],
})
export class InventarioHistorialPageComponent implements OnInit {
  private svc = inject(GestionInventarioService);
  private confirmDialog = inject(ConfirmDialogService);
  private toast = inject(ToastService);
  private userPermService = inject(UserPermissionService);

  /** Permiso `registros.fecha_retroactiva`: destraba el campo de fecha más allá de la ventana base. */
  readonly puedeRetroactivar = this.userPermService.has(PERMISO_FECHA_RETROACTIVA);

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
  /** movimientoId del ingreso cuya marca «próximo ciclo» se está guardando (deshabilita su botón). */
  destinoCicloSavingId: number | null = null;

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
  /**
   * Hint de la ventana admitida para la fecha (mes en curso ∪ últimos 15 días → hoy, o sin piso con
   * el permiso de fecha retroactiva). Campo, no getter: el template lo lee en cada ciclo y tiene que
   * ser una referencia estable.
   */
  hintFechaMovimientoTexto = '';

  /**
   * D4 — el modal de fecha lo comparten traslados e ingresos, pero la excepción del alimento previo
   * al encasetamiento es SOLO de los ingresos (igual que en el backend). `null` ⇒ regla dura.
   * `editFechaMin: string | null` — `null` = sin piso, ligado con `[attr.min]`.
   */
  ventanaEdicion: VentanaFechaIngreso | null = null;
  editFechaMin: string | null = '';
  editFechaMax = '';
  editHint = '';

  // ── Delete confirm modal ──────────────────────────────────────────────────────
  deleteOpen = false;
  deleteType: ActiveTab = 'traslados';
  deleteId: string | number = '';
  deleteLabel = '';
  deleteDeleting = false;
  deleteError = '';

  ngOnInit(): void {
    const hoy = new Date();
    this.hintFechaMovimientoTexto = hintVentanaFechaRegistro(hoy, this.puedeRetroactivar);
    this.aplicarVentanaEdicion(null);
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
    // El traslado conserva la regla dura: D4 es del alimento que llega antes que los pollitos.
    this.aplicarVentanaEdicion(null);
    this.editOpen = true;
  }

  openDeleteTraslado(t: InventarioGestionTrasladoListDto): void {
    this.deleteType = 'traslados';
    this.deleteId = t.transferGroupId;
    this.deleteLabel = `${t.itemNombre} — ${t.quantity} ${t.unit} (${t.fechaMovimiento.substring(0, 10)})`;
    this.deleteError = '';
    this.deleteOpen = true;
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
    this.aplicarVentanaEdicion(null);
    this.editOpen = true;
    // D4 — la ubicación sale del movimiento, no del modal. Fail-OPEN: si falla, queda la regla dura
    // y el rechazo lo hace el controller (cerrar acá repondría el bloqueo que esto viene a sacar).
    this.svc.getVentanaFechaIngresoExistente(i.movimientoId).subscribe({
      next: v => this.aplicarVentanaEdicion(v ?? null),
      error: () => this.aplicarVentanaEdicion(null)
    });
  }

  /** Deja extremos y hint del modal listos para el template (referencias estables). */
  private aplicarVentanaEdicion(ventana: VentanaFechaIngreso | null): void {
    const hoy = new Date();
    this.ventanaEdicion = ventana;
    const extremos = extremosFechaIngreso(hoy, ventana, this.puedeRetroactivar);
    this.editFechaMin = extremos.min;
    this.editFechaMax = extremos.max;
    this.editHint = hintFechaIngreso(hoy, ventana, this.puedeRetroactivar);
  }

  openDeleteIngreso(i: InventarioGestionIngresoListDto): void {
    this.deleteType = 'ingresos';
    this.deleteId = i.movimientoId;
    this.deleteLabel = `${i.itemNombre} — ${i.quantity} ${i.unit} (${i.fechaMovimiento.substring(0, 10)})`;
    this.deleteError = '';
    this.deleteOpen = true;
  }

  /**
   * ⛔ Marcar ingresos nuevos está DESHABILITADO (09-ago-2026) — ver el comentario extenso en
   * `gestion-inventario-page.component.ts` (`mostrarParaProximoCicloIngreso`): la marca rompe la
   * conservación de kilos en galpones con ciclos que conviven y espera su rediseño.
   *
   * Acá se conserva a propósito el camino de SALIDA: una fila que YA está marcada sigue mostrando su
   * badge y se puede DESMARCAR. Nunca se esconde alimento — es la regla del dueño del producto: si el
   * sistema no sabe a qué ciclo pertenece, tiene que verse para poder corregirlo.
   */
  puedeMarcarDestinoCiclo(i: InventarioGestionIngresoListDto): boolean {
    return !!(i.galponId ?? '').trim() && i.paraProximoCiclo === true;
  }

  /** Pone o quita la marca «próximo ciclo» de un ingreso ya registrado, con confirmación y toast. */
  async toggleDestinoCiclo(i: InventarioGestionIngresoListDto): Promise<void> {
    if (this.destinoCicloSavingId != null || !this.puedeMarcarDestinoCiclo(i)) return;
    const marcar = !i.paraProximoCiclo;
    const ok = await this.confirmDialog.ask({
      title: marcar ? 'Marcar para el próximo ciclo' : 'Quitar marca de próximo ciclo',
      message: marcar
        ? `¿Marcar este ingreso de «${i.itemNombre}» (${i.quantity} ${i.unit}) como alimento para el PRÓXIMO encasetamiento de este galpón? El seguimiento diario lo tomará como ingreso inicial del ciclo que viene.`
        : `¿Quitar la marca de «próximo ciclo» de este ingreso de «${i.itemNombre}»? Volverá a evaluarse por su fecha, como cualquier otro ingreso.`,
      type: 'warning',
      confirmText: marcar ? 'Marcar' : 'Quitar marca',
    });
    if (!ok) return;

    this.destinoCicloSavingId = i.movimientoId;
    this.svc.actualizarDestinoCicloIngreso(i.movimientoId, { paraProximoCiclo: marcar }).subscribe({
      next: updated => {
        const idx = this.ingresos.findIndex(x => x.movimientoId === i.movimientoId);
        if (idx >= 0) this.ingresos = [...this.ingresos.slice(0, idx), updated, ...this.ingresos.slice(idx + 1)];
        this.destinoCicloSavingId = null;
        this.toast.success(
          marcar ? 'Ingreso marcado para el próximo ciclo.' : 'Se quitó la marca de próximo ciclo.',
          'Listo'
        );
      },
      error: err => {
        this.destinoCicloSavingId = null;
        this.toast.error(err?.error?.message ?? 'No se pudo actualizar la marca de ciclo.', 'Error');
      },
    });
  }

  // ── Edit modal ────────────────────────────────────────────────────────────────
  closeEdit(): void {
    if (this.editSaving) return;
    this.editOpen = false;
  }

  // ── Delete modal ──────────────────────────────────────────────────────────────
  closeDelete(): void {
    if (this.deleteDeleting) return;
    this.deleteOpen = false;
  }

  confirmDelete(): void {
    if (this.deleteDeleting) return;
    this.deleteDeleting = true;
    this.deleteError = '';

    if (this.deleteType === 'traslados') {
      this.svc.eliminarTraslado(this.deleteId as string).subscribe({
        next: () => {
          this.traslados = this.traslados.filter(t => t.transferGroupId !== this.deleteId);
          this.deleteOpen = false;
          this.deleteDeleting = false;
        },
        error: err => {
          this.deleteError = err?.error?.message ?? 'Error al eliminar el traslado.';
          this.deleteDeleting = false;
        },
      });
    } else {
      this.svc.eliminarIngreso(this.deleteId as number).subscribe({
        next: () => {
          this.ingresos = this.ingresos.filter(i => i.movimientoId !== this.deleteId);
          this.deleteOpen = false;
          this.deleteDeleting = false;
        },
        error: err => {
          this.deleteError = err?.error?.message ?? 'Error al eliminar el ingreso.';
          this.deleteDeleting = false;
        },
      });
    }
  }

  saveEdit(): void {
    if (!this.editFecha || this.editSaving) return;
    // Misma ventana que el alta: si no, la regla se esquiva cargando hoy y reeditando la fecha.
    // El ingreso lleva además la excepción D4; el traslado conserva la regla dura.
    const hoy = new Date();
    if (this.editType === 'ingresos') {
      if (!esFechaIngresoOfrecible(this.editFecha, hoy, this.ventanaEdicion, this.puedeRetroactivar)) {
        this.editError = mensajeFechaIngresoFueraDeVentana(hoy, this.ventanaEdicion, this.puedeRetroactivar);
        return;
      }
    } else if (!esFechaMovimientoPermitida(this.editFecha, hoy, this.puedeRetroactivar)) {
      this.editError = mensajeFechaFueraDeVentana(hoy, this.puedeRetroactivar);
      return;
    }
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

  /**
   * Ubicación legible de una fila. Con inventario por silo el saldo vive en el silo/bodega y
   * núcleo/galpón llegan en null: se muestra `Granja › Silo 4`. La decisión sale del DATO de la
   * fila (hay silo o no), nunca de un flag: así el histórico mezcla filas viejas y nuevas sin
   * mentir en ninguna.
   */
  formatUbicacion(
    granja: string | null,
    nucleo: string | null,
    galpon: string | null,
    silo: string | null = null
  ): string {
    const parts = silo ? [granja, silo] : [granja, nucleo, galpon];
    return parts.filter(Boolean).length ? parts.filter(Boolean).join(' › ') : '—';
  }
}

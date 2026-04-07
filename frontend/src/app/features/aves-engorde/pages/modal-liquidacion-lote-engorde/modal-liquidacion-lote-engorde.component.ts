import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { concatMap, finalize, map, take } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { GestionInventarioService, InventarioGestionStockDto } from '../../../gestion-inventario/services/gestion-inventario.service';
import {
  SeguimientoAvesEngordeService,
  LiquidacionLoteEngordeResumenDto,
  LoteRegistroHistoricoUnificadoDto
} from '../../services/seguimiento-aves-engorde.service';
import { LoteEngordeService } from '../../../lote-engorde/services/lote-engorde.service';
import {
  AvesDisponiblesDto,
  LoteReproductoraAveEngordeService
} from '../../../lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service';

@Component({
  selector: 'app-modal-liquidacion-lote-engorde',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './modal-liquidacion-lote-engorde.component.html',
  styleUrls: ['./modal-liquidacion-lote-engorde.component.scss']
})
export class ModalLiquidacionLoteEngordeComponent implements OnChanges {
  private readonly seg = inject(SeguimientoAvesEngordeService);
  private readonly loteEngorde = inject(LoteEngordeService);
  private readonly repSvc = inject(LoteReproductoraAveEngordeService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly invGestion = inject(GestionInventarioService);

  @Input() isOpen = false;
  @Input() loteId: number | null = null;
  @Input() loteNombre = '';
  @Input() granjaId: number | null = null;
  @Input() nucleoId: string | null = null;
  @Input() galponId: string | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() loteActualizado = new EventEmitter<void>();

  loading = false;
  error: string | null = null;
  resumen: LiquidacionLoteEngordeResumenDto | null = null;
  avesDisponibles: AvesDisponiblesDto | null = null;
  verVentasRegistros = false;
  loadingVentasRegistros = false;
  ventasRegistrosError: string | null = null;
  ventasRegistros: LoteRegistroHistoricoUnificadoDto[] = [];

  /** Stock de alimento (inventario-gestion) en la ubicación del lote (EC/PA). */
  stockAlimento: InventarioGestionStockDto[] = [];
  loadingStock = false;
  stockError: string | null = null;
  /** Si hubo que consultar inventario sin filtrar por galpón para encontrar filas. */
  stockUsandoFallbackUbicacion = false;

  abrirModal = false;
  motivoReapertura = '';
  guardandoAbrir = false;
  guardandoCerrar = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen && this.loteId) {
      this.cargarResumen();
    }
    if (changes['isOpen'] && !this.isOpen) {
      this.resetLocal();
    }
  }

  private resetLocal(): void {
    this.error = null;
    this.resumen = null;
    this.avesDisponibles = null;
    this.verVentasRegistros = false;
    this.loadingVentasRegistros = false;
    this.ventasRegistrosError = null;
    this.ventasRegistros = [];
    this.stockAlimento = [];
    this.loadingStock = false;
    this.stockError = null;
    this.stockUsandoFallbackUbicacion = false;
    this.abrirModal = false;
    this.motivoReapertura = '';
  }

  cargarResumen(): void {
    if (!this.loteId) return;
    this.loading = true;
    this.error = null;
    this.resumen = null;
    this.avesDisponibles = null;
    forkJoin({
      resumen: this.seg.getResumenLiquidacion(this.loteId),
      aves: this.repSvc.getAvesDisponibles(this.loteId)
    })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: ({ resumen, aves }) => {
          this.resumen = resumen;
          this.avesDisponibles = aves ?? null;
          this.cargarStockAlimento();
        },
        error: err => {
          this.error =
            err?.error?.message ?? err?.error?.error ?? err?.message ?? 'No se pudo cargar el resumen.';
        }
      });
  }

  /** Aves al inicio: preferir API aves-disponibles (incluye lógica de asignadas), fallback a resumen-liquidacion. */
  get hembrasInicioUi(): number {
    return Number(this.avesDisponibles?.hembrasIniciales ?? this.resumen?.hembrasInicio ?? 0);
  }
  get machosInicioUi(): number {
    return Number(this.avesDisponibles?.machosIniciales ?? this.resumen?.machosInicio ?? 0);
  }
  get mixtasInicioUi(): number {
    return Number(this.resumen?.mixtasInicio ?? 0);
  }
  get totalInicioUi(): number {
    return this.hembrasInicioUi + this.machosInicioUi + this.mixtasInicioUi;
  }

  /** Alertas: ventas por sexo exceden iniciales (inconsistencia a corregir). */
  get sobreventaHembras(): boolean {
    const v = Number(this.resumen?.ventasTotalHembras ?? 0);
    return v > this.hembrasInicioUi && this.hembrasInicioUi > 0;
  }
  get sobreventaMachos(): boolean {
    const v = Number(this.resumen?.ventasTotalMachos ?? 0);
    return v > this.machosInicioUi && this.machosInicioUi > 0;
  }
  get sobreventaTotal(): boolean {
    const v = Number(this.resumen?.ventasTotalHembras ?? 0) + Number(this.resumen?.ventasTotalMachos ?? 0) + Number(this.resumen?.ventasTotalMixtas ?? 0);
    return v > this.totalInicioUi && this.totalInicioUi > 0;
  }

  toggleVerVentasRegistros(): void {
    this.verVentasRegistros = !this.verVentasRegistros;
    if (this.verVentasRegistros) {
      this.cargarVentasRegistros();
    }
  }

  private cargarVentasRegistros(): void {
    if (!this.loteId) return;
    this.loadingVentasRegistros = true;
    this.ventasRegistrosError = null;
    this.ventasRegistros = [];
    this.seg
      .getHistoricoUnificadoPorLote(this.loteId)
      .pipe(finalize(() => (this.loadingVentasRegistros = false)))
      .subscribe({
        next: rows => {
          const list = (rows ?? []).filter(r => String(r.tipoEvento || '').toUpperCase() === 'VENTA_AVES' && !r.anulado);
          // Orden: fecha operación asc, id asc
          list.sort((a, b) => {
            const da = String(a.fechaOperacion ?? '');
            const db = String(b.fechaOperacion ?? '');
            if (da !== db) return da.localeCompare(db);
            return (a.id ?? 0) - (b.id ?? 0);
          });
          this.ventasRegistros = list;
        },
        error: err => {
          this.ventasRegistrosError =
            err?.error?.message ?? err?.error?.error ?? err?.message ?? 'No se pudieron cargar los registros de venta.';
          this.ventasRegistros = [];
        }
      });
  }

  /** Totales calculados desde los registros (para comparar con el resumen). */
  get ventasRegistrosTotH(): number {
    return (this.ventasRegistros ?? []).reduce((s, r) => s + (Number(r.cantidadHembras) || 0), 0);
  }
  get ventasRegistrosTotM(): number {
    return (this.ventasRegistros ?? []).reduce((s, r) => s + (Number(r.cantidadMachos) || 0), 0);
  }
  get ventasRegistrosTotX(): number {
    return (this.ventasRegistros ?? []).reduce((s, r) => s + (Number(r.cantidadMixtas) || 0), 0);
  }
  get diffVentasH(): number {
    return (Number(this.resumen?.ventasTotalHembras ?? 0) - this.ventasRegistrosTotH) || 0;
  }
  get diffVentasM(): number {
    return (Number(this.resumen?.ventasTotalMachos ?? 0) - this.ventasRegistrosTotM) || 0;
  }

  get saldoPositivo(): boolean {
    return this.totalKgStockInventario > 0;
  }

  /** Suma kg de ítems alimento en inventario (misma ubicación consultada). */
  get totalKgStockInventario(): number {
    return (this.stockAlimento ?? []).reduce((s, r) => s + (Number(r.quantity) || 0), 0);
  }

  /** No se puede liquidar si aún hay aves vivas en el lote. */
  get avesVivasPendientes(): number {
    return Math.max(0, Number(this.resumen?.avesVivasActuales ?? 0));
  }

  get puedeLiquidarPorAves(): boolean {
    return this.avesVivasPendientes === 0;
  }

  private cargarStockAlimento(): void {
    if (this.granjaId == null) {
      this.stockAlimento = [];
      this.stockUsandoFallbackUbicacion = false;
      return;
    }
    this.loadingStock = true;
    this.stockError = null;
    this.stockUsandoFallbackUbicacion = false;

    const pFull = {
      farmId: this.granjaId,
      nucleoId: this.nucleoId ?? undefined,
      galponId: this.galponId ?? undefined
    };

    this.invGestion
      .getStock(pFull)
      .pipe(
        concatMap(rows => {
          const food = this.filtrarFilasAlimento(rows ?? []).filter(r => (r.quantity ?? 0) > 0);
          if (food.length > 0 || !this.galponId) {
            return of({ rows: food, fallback: false });
          }
          return this.invGestion
            .getStock({ farmId: this.granjaId!, nucleoId: this.nucleoId ?? undefined })
            .pipe(
              map(rows2 => {
                const f2 = this.filtrarFilasAlimento(rows2 ?? []).filter(r => (r.quantity ?? 0) > 0);
                return { rows: f2, fallback: f2.length > 0 };
              })
            );
        }),
        finalize(() => (this.loadingStock = false))
      )
      .subscribe({
        next: ({ rows, fallback }) => {
          this.stockAlimento = rows;
          this.stockUsandoFallbackUbicacion = fallback;
        },
        error: err => {
          this.stockError = err?.error?.message ?? err?.message ?? 'No se pudo cargar el stock de alimento.';
          this.stockAlimento = [];
          this.stockUsandoFallbackUbicacion = false;
        }
      });
  }

  /** Ítems alimento (concepto/tipo puede ser "alimento" o texto que comience por "alimento"). */
  private filtrarFilasAlimento(rows: InventarioGestionStockDto[]): InventarioGestionStockDto[] {
    return rows.filter(r => this.esItemTipoAlimento(r));
  }

  private esItemTipoAlimento(r: InventarioGestionStockDto): boolean {
    const t = String(r.itemType ?? '').trim().toLowerCase();
    return t === 'alimento' || t.startsWith('alimento');
  }

  /** Saldo diario (seguimiento) vs inventario físico: avisar si difieren de forma relevante. */
  get inventarioDifiereDelSeguimiento(): boolean {
    const s = this.resumen?.saldoAlimentoKg;
    if (s == null || this.loadingStock) return false;
    return Math.abs(Number(s) - this.totalKgStockInventario) > 0.5;
  }

  private async userIdStr(): Promise<string> {
    const s = await firstValueFrom(this.auth.session$.pipe(take(1)));
    return s?.user?.id ?? '';
  }

  irGestionInventario(): void {
    const q: Record<string, string> = { tab: 'traslados' };
    if (this.granjaId != null) q['farmId'] = String(this.granjaId);
    if (this.nucleoId) q['nucleoId'] = this.nucleoId;
    if (this.galponId) q['galponId'] = this.galponId;
    this.router.navigate(['/gestion-inventario'], { queryParams: q });
    this.onClose();
  }

  get loteCerrado(): boolean {
    return (this.resumen?.estadoOperativoLote ?? '').trim().toLowerCase() === 'cerrado';
  }

  get puedeConfirmarCierre(): boolean {
    return !!this.resumen && !this.loteCerrado && this.puedeLiquidarPorAves;
  }

  async cerrarLote(): Promise<void> {
    if (!this.loteId) return;
    const uid = await this.userIdStr();
    if (!uid) {
      this.error = 'No hay usuario en sesión; vuelva a iniciar sesión.';
      return;
    }
    this.guardandoCerrar = true;
    this.error = null;
    this.loteEngorde
      .cerrarLote(this.loteId, uid)
      .pipe(finalize(() => (this.guardandoCerrar = false)))
      .subscribe({
        next: () => {
          this.loteActualizado.emit();
          this.onClose();
        },
        error: err => {
          this.error = err?.error?.message ?? err?.error ?? err?.message ?? 'No se pudo cerrar el lote.';
        }
      });
  }

  abrirDialogoReapertura(): void {
    this.abrirModal = true;
    this.motivoReapertura = '';
    this.error = null;
  }

  cancelarAbrir(): void {
    this.abrirModal = false;
    this.motivoReapertura = '';
  }

  async confirmarAbrirLote(): Promise<void> {
    if (!this.loteId) return;
    const uid = await this.userIdStr();
    const m = (this.motivoReapertura || '').trim();
    if (!uid) {
      this.error = 'No hay usuario en sesión.';
      return;
    }
    if (m.length < 3) {
      this.error = 'Indique el motivo de reapertura (mínimo 3 caracteres).';
      return;
    }
    this.guardandoAbrir = true;
    this.error = null;
    this.loteEngorde
      .abrirLote(this.loteId, { motivo: m, openedByUserId: uid })
      .pipe(finalize(() => (this.guardandoAbrir = false)))
      .subscribe({
        next: () => {
          this.abrirModal = false;
          this.motivoReapertura = '';
          this.loteActualizado.emit();
          this.cargarResumen();
        },
        error: err => {
          this.error = err?.error?.message ?? err?.error ?? err?.message ?? 'No se pudo abrir el lote.';
        }
      });
  }

  onClose(): void {
    this.close.emit();
  }
}

import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize, take } from 'rxjs/operators';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { GestionInventarioService, InventarioGestionStockDto } from '../../../gestion-inventario/services/gestion-inventario.service';
import {
  SeguimientoAvesEngordeService,
  LiquidacionLoteEngordeResumenDto
} from '../../services/seguimiento-aves-engorde.service';
import { LoteEngordeService } from '../../../lote-engorde/services/lote-engorde.service';

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

  /** Stock de alimento (inventario-gestion) en la ubicación del lote (EC/PA). */
  stockAlimento: InventarioGestionStockDto[] = [];
  loadingStock = false;
  stockError: string | null = null;

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
    this.stockAlimento = [];
    this.loadingStock = false;
    this.stockError = null;
    this.abrirModal = false;
    this.motivoReapertura = '';
  }

  cargarResumen(): void {
    if (!this.loteId) return;
    this.loading = true;
    this.error = null;
    this.resumen = null;
    this.seg
      .getResumenLiquidacion(this.loteId)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: r => {
          this.resumen = r;
          this.cargarStockAlimento();
        },
        error: err => {
          this.error =
            err?.error?.message ?? err?.error?.error ?? err?.message ?? 'No se pudo cargar el resumen.';
        }
      });
  }

  get saldoPositivo(): boolean {
    return (this.stockAlimento ?? []).some(r => (r?.quantity ?? 0) > 0);
  }

  /** No se puede liquidar si aún hay aves vivas en el lote. */
  get avesVivasPendientes(): number {
    return Math.max(0, Number(this.resumen?.avesVivasActuales ?? 0));
  }

  get puedeLiquidarPorAves(): boolean {
    return this.avesVivasPendientes === 0;
  }

  private cargarStockAlimento(): void {
    // Solo aplica cuando ya no hay aves vivas (cierre operativo requiere lote en 0 aves).
    if (this.granjaId == null || !this.puedeLiquidarPorAves) {
      this.stockAlimento = [];
      return;
    }
    this.loadingStock = true;
    this.stockError = null;
    const params: { farmId: number; nucleoId?: string; galponId?: string } = { farmId: this.granjaId };
    if (this.nucleoId) params.nucleoId = this.nucleoId;
    if (this.galponId) params.galponId = this.galponId;
    this.invGestion
      .getStock(params)
      .pipe(finalize(() => (this.loadingStock = false)))
      .subscribe({
        next: rows => {
          const list = rows ?? [];
          this.stockAlimento = list
            .filter(r => String(r.itemType ?? '').trim().toLowerCase() === 'alimento')
            .filter(r => (r.quantity ?? 0) > 0);
        },
        error: err => {
          this.stockError = err?.error?.message ?? err?.message ?? 'No se pudo cargar el stock de alimento.';
          this.stockAlimento = [];
        }
      });
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

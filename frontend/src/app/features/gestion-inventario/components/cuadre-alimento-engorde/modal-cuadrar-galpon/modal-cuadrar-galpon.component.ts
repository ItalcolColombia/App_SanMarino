// src/app/features/gestion-inventario/components/cuadre-alimento-engorde/modal-cuadrar-galpon/modal-cuadrar-galpon.component.ts
//
// Cierra el descuadre de UN galpón desde la misma pestaña que lo señala.
//
// El pedido original fue «editar el saldo desde este tab». El saldo de la tabla diaria no es un
// campo —lo deriva `fn_seguimiento_diario_engorde`—, así que lo que este modal pide es el dato que
// la persona parada frente al galpón sí conoce: CUÁNTOS KILOS HAY. De ahí el backend deriva qué
// escribir de cada lado, y el modal lo muestra ANTES de confirmar: nadie firma un ajuste sin ver
// qué va a mover.
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';

import {
  CuadrarGalponAlimentoResultDto,
  CuadreAlimentoEngordeFilaDto,
  CuadreAlimentoEngordeService
} from '../../../services/cuadre-alimento-engorde.service';
import { GestionInventarioService, InventarioGestionStockDto } from '../../../services/gestion-inventario.service';

@Component({
  selector: 'app-modal-cuadrar-galpon',
  standalone: true,
  imports: [CommonModule, FormsModule],
  // Eager obligatorio: el estado se escribe desde callbacks de HttpClient. Con el OnPush que Angular
  // 22 aplica por defecto, el modal se quedaría en «Cargando…» con la red ya respondida.
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './modal-cuadrar-galpon.component.html',
  styleUrls: ['./modal-cuadrar-galpon.component.scss']
})
export class ModalCuadrarGalponComponent implements OnChanges {
  private readonly svc = inject(CuadreAlimentoEngordeService);
  private readonly inventarioSvc = inject(GestionInventarioService);

  @Input() abierto = false;
  @Input() fila: CuadreAlimentoEngordeFilaDto | null = null;

  @Output() cerrar = new EventEmitter<void>();
  /** Emite cuando el cuadre se aplicó: la pestaña recarga para mostrar el galpón ya en cero. */
  @Output() cuadrado = new EventEmitter<CuadrarGalponAlimentoResultDto>();

  /** Ítems de alimento con stock en el galpón. El de mayor saldo queda preseleccionado. */
  items: InventarioGestionStockDto[] = [];
  itemId: number | null = null;

  kilosReales: number | null = null;
  motivo = '';

  cargandoItems = false;
  guardando = false;
  error: string | null = null;

  ngOnChanges(): void {
    if (!this.abierto || !this.fila) return;
    this.error = null;
    this.guardando = false;
    this.motivo = '';
    // Se propone el stock físico, que es el lado que la operación suele tener contado. Si el que
    // está mal es el inventario, el operador escribe el número real y el backend corrige ese lado.
    this.kilosReales = this.fila.stockKg;
    this.cargarItems();
  }

  private cargarItems(): void {
    if (!this.fila) return;
    this.cargandoItems = true;
    this.items = [];
    this.itemId = null;

    this.inventarioSvc
      .getStock({
        farmId: this.fila.granjaId,
        nucleoId: this.fila.nucleoId,
        galponId: this.fila.galponId,
        itemType: 'alimento'
      })
      .pipe(finalize(() => (this.cargandoItems = false)))
      .subscribe({
        next: filas => {
          this.items = [...filas].sort((a, b) => b.quantity - a.quantity);
          this.itemId = this.items.length > 0 ? this.items[0].itemInventarioEcuadorId : null;
        },
        error: err => {
          this.error = err?.error?.message ?? err?.message ?? 'No se pudieron cargar los ítems del galpón.';
        }
      });
  }

  // ── Previsualización. Es la MISMA aritmética que aplica el backend; acá solo se muestra, y el
  //    backend vuelve a calcularla sobre los números vigentes al confirmar (la pantalla puede estar
  //    vieja). Si difieren, manda el backend.
  get deltaStock(): number {
    if (!this.fila || this.kilosReales == null) return 0;
    return this.kilosReales - this.fila.stockKg;
  }

  get deltaTabla(): number {
    if (!this.fila || this.kilosReales == null) return 0;
    // 🔴 Lo RESERVADO se resta igual que los movimientos posteriores: con doble validación, ese
    // consumo ya está dentro del saldo pero todavía no salió del inventario. Sin restarlo, el
    // ajuste dejaría el galpón descuadrado POR EL MONTO RESERVADO después de una pantalla que dijo
    // «cuadrado». Con el flag apagado es 0 y esta línea no cambia nada.
    return (this.kilosReales - this.fila.reservadoActivoKg - this.fila.movPostKg) - this.fila.saldoTablaKg;
  }

  get tocaStock(): boolean { return Math.abs(this.deltaStock) > 1; }
  get tocaTabla(): boolean { return Math.abs(this.deltaTabla) > 1; }

  /**
   * Saldo del ítem elegido, o `null` si no hay ninguno.
   *
   * El ajuste de stock aplica el DELTA sobre ESE ítem, no los kilos totales del galpón (que son la
   * suma de todos). Si el descuento supera lo que ese ítem tiene, el backend rechaza; acá se avisa
   * antes para no gastarle el viaje al usuario.
   */
  get itemSeleccionadoSaldo(): number | null {
    if (this.itemId == null) return null;
    const it = this.items.find(x => x.itemInventarioEcuadorId === this.itemId);
    return it ? it.quantity : null;
  }

  get motivoValido(): boolean { return this.motivo.trim().length >= 10; }

  get puedeGuardar(): boolean {
    const saldoItem = this.itemSeleccionadoSaldo;
    const descuentoPosible = !this.tocaStock || saldoItem == null || saldoItem + this.deltaStock >= 0;

    return !this.guardando
        && this.itemId != null
        && this.kilosReales != null
        && this.kilosReales >= 0
        && this.motivoValido
        && descuentoPosible
        && (this.tocaStock || this.tocaTabla);
  }

  onCerrar(): void {
    if (this.guardando) return;
    this.cerrar.emit();
  }

  aplicar(): void {
    if (!this.fila || !this.puedeGuardar) return;

    this.guardando = true;
    this.error = null;

    this.svc
      .cuadrarGalpon({
        loteAveEngordeId: this.fila.loteAveEngordeId,
        itemInventarioEcuadorId: this.itemId!,
        kilosRealesKg: this.kilosReales!,
        motivo: this.motivo.trim()
      })
      .pipe(finalize(() => (this.guardando = false)))
      .subscribe({
        next: r => this.cuadrado.emit(r),
        error: err => {
          this.error = err?.error?.message ?? err?.error?.error ?? err?.message ?? 'No se pudo cuadrar el galpón.';
        }
      });
  }

  numero(v: number | null | undefined): string {
    if (v == null) return '—';
    return v.toLocaleString('es-CO', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  }
}

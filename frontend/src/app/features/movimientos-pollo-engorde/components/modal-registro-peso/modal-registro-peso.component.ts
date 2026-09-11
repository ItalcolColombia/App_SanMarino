import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import {
  MovimientoPolloEngordeDto,
  MovimientoPolloEngordeService,
  RegistrarPesoFacturaResponse
} from '../../services/movimiento-pollo-engorde.service';
import {
  calcularProrateoDespacho,
  totalesProrateoDespacho,
  LineaDespachoPeso,
  ProrateoDespachoRow
} from '../../funciones/prorateo-peso-despacho.funcion';
import { formatearNumero as fmtNumero } from '../../funciones/formato.funcion';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';

/**
 * Modal de REGISTRO DE PESO de un despacho (empresas con `venta_engorde_peso_diferido`).
 *
 * La venta se registró sin peso porque la báscula llega al día siguiente; acá se carga el peso del
 * camión, se muestra el prorrateo por lote y —si el despacho sigue Pendiente— se confirma la venta
 * en la misma operación. El peso SIEMPRE se carga por despacho (factura) y nunca por movimiento
 * suelto: `pesoBruto`/`pesoTara` son el peso del camión clonado en cada línea, y el individual
 * correcto es el prorrateo por aves.
 *
 * `changeDetection: Eager` es obligatorio: el componente tiene estado mutable y `subscribe`
 * (en Angular 22 omitirlo equivale a OnPush ⇒ el modal quedaría colgado en «Guardando…»).
 */
@Component({
  selector: 'app-modal-registro-peso',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './modal-registro-peso.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./modal-registro-peso.component.scss']
})
export class ModalRegistroPesoComponent implements OnChanges {
  @Input() isOpen = false;
  /** Líneas del despacho (todas comparten `facturaId`). */
  @Input() movimientos: MovimientoPolloEngordeDto[] = [];

  @Output() close = new EventEmitter<void>();
  /** Emite la respuesta del backend cuando el peso quedó guardado. */
  @Output() saved = new EventEmitter<RegistrarPesoFacturaResponse>();

  pesoBruto: number | null = null;
  pesoTara: number | null = null;
  loading = false;
  error: string | null = null;

  /**
   * Empresa que recibe UNA sola cifra de kilos por despacho (`venta_engorde_peso_neto_unico`): se
   * OCULTA «Peso tara» y se manda en 0, así que el neto es lo digitado. Este modal es justo el de
   * la báscula diferida, donde el operario tiene una sola cifra en la mano — pedirle dos es lo que
   * lo llevaba a repetir el número y dejar el despacho en 0 kg. Fail-closed: arranca en false.
   */
  pesoNetoUnico = false;

  /** Filas del prorrateo, memoizadas: un getter que alocara por ciclo rompe change detection. */
  prorrateo: ProrateoDespachoRow[] = [];
  totales = totalesProrateoDespacho([]);

  constructor(
    private movimientoSvc: MovimientoPolloEngordeService,
    private companyConfig: ActiveCompanyConfigService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      this.error = null;
      this.loading = false;
      // Corrección de un despacho que ya tenía peso: se precarga para editarlo.
      const conPeso = this.movimientos.find((m) => m.pesoBrutoGlobal != null || m.pesoBruto != null);
      this.pesoBruto = conPeso?.pesoBrutoGlobal ?? conPeso?.pesoBruto ?? null;
      this.pesoTara = conPeso?.pesoTaraGlobal ?? conPeso?.pesoTara ?? null;
      this.resolverPesoNetoUnico();
      this.recalcular();
    }
  }

  /** Nº de despacho legible (o el número del primer movimiento si no hay). */
  get tituloDespacho(): string {
    const m = this.movimientos[0];
    if (!m) return '';
    const nro = (m.numeroDespacho ?? '').trim();
    return nro || m.numeroMovimiento;
  }

  get hayPendientes(): boolean {
    return this.movimientos.some((m) => m.estado === 'Pendiente');
  }

  get totalAves(): number {
    return this.movimientos.reduce((s, m) => s + (m.totalAves ?? 0), 0);
  }

  get pesoNeto(): number | null {
    if (this.pesoBruto == null || this.pesoTara == null) return null;
    return Math.round((this.pesoBruto - this.pesoTara) * 1000) / 1000;
  }

  get promedioPesoAve(): number | null {
    const neto = this.pesoNeto;
    return neto != null && this.totalAves > 0 ? neto / this.totalAves : null;
  }

  /**
   * Aviso en ROJO bajo el campo de tara: la tara vacía con el bruto cargado, o la tara igual al
   * bruto (neto 0). Este modal es justo el de la báscula diferida de Panamá, donde el operario tiene
   * UNA sola cifra de kilos en la mano: sin el aviso la repite en los dos campos y el despacho queda
   * en 0 kg (206 ventas así hasta el 9-sep-2026).
   */
  get avisoPesoTara(): string | null {
    // Con el campo oculto no hay nada que avisar: la tara la pone el componente en 0.
    if (this.pesoNetoUnico) return null;
    if (this.pesoBruto == null) return null;
    if (this.pesoTara == null)
      return 'Falta el peso tara (el camión VACÍO). Sin él el despacho queda sin kilos: no suman en '
           + 'el seguimiento diario, ni en el informe semanal, ni en la liquidación.';
    if (this.pesoBruto === this.pesoTara)
      return 'El peso bruto y el peso tara son iguales: el neto daría 0 kg. Si sólo tiene los kilos '
           + 'netos del despacho, cárguelos en el peso bruto y deje la tara en 0.';
    return null;
  }

  get puedeGuardar(): boolean {
    return (
      !this.loading &&
      this.pesoBruto != null &&
      this.pesoTara != null &&
      this.pesoBruto > 0 &&
      this.pesoTara >= 0 &&
      // Estricto, no `>=`: bruto == tara es neto 0 — la venta contaría las aves y aportaría 0 kg.
      this.pesoBruto > this.pesoTara &&
      this.movimientos.length > 0
    );
  }

  /**
   * Resuelve el flag de la empresa activa. Fail-closed: ante error se piden los dos pesos, que es el
   * comportamiento de siempre.
   */
  private resolverPesoNetoUnico(): void {
    this.companyConfig.ventaEngordePesoNetoUnico().subscribe({
      next: (activo) => {
        this.pesoNetoUnico = activo;
        this.recalcular();
        this.cdr.detectChanges();
      },
      error: () => {
        this.pesoNetoUnico = false;
        this.cdr.detectChanges();
      }
    });
  }

  /** Con «Peso tara» oculto la escribe el componente: 0 si hay kilos digitados, null si no. */
  private sincronizarTaraOculta(): void {
    if (!this.pesoNetoUnico) return;
    this.pesoTara = this.pesoBruto == null ? null : 0;
  }

  /** Recalcula el prorrateo. Se llama desde los inputs, no desde un getter del template. */
  recalcular(): void {
    this.sincronizarTaraOculta();
    const lineas: LineaDespachoPeso[] = this.movimientos.map((m) => ({
      id: m.id,
      loteNombre: m.loteOrigenNombre ?? `Lote ${m.loteOrigenId ?? ''}`.trim(),
      galponLabel: m.numeroMovimiento,
      aves: m.totalAves ?? 0
    }));
    this.prorrateo = calcularProrateoDespacho(lineas, this.pesoBruto, this.pesoTara);
    this.totales = totalesProrateoDespacho(this.prorrateo);
  }

  formatearNumero(n: number | null | undefined): string {
    return n == null ? '—' : fmtNumero(n);
  }

  onClose(): void {
    if (this.loading) return;
    this.close.emit();
  }

  async onSubmit(): Promise<void> {
    if (!this.puedeGuardar) {
      this.error = this.mensajeInvalido();
      return;
    }
    const facturaId = this.movimientos[0]?.facturaId;
    if (!facturaId) {
      this.error = 'Este movimiento no pertenece a un despacho identificado; no se le puede cargar peso desde acá.';
      return;
    }

    this.loading = true;
    this.error = null;
    try {
      const res = await firstValueFrom(
        this.movimientoSvc.registrarPesoFactura(facturaId, {
          pesoBruto: this.pesoBruto!,
          pesoTara: this.pesoTara!,
          // Cargar el peso ES confirmar la venta cuando quedan líneas pendientes; si el despacho
          // ya está completado, esto es sólo una corrección del peso.
          confirmar: this.hayPendientes
        })
      );
      this.loading = false;
      this.saved.emit(res);
    } catch (err: unknown) {
      this.loading = false;
      this.error = err instanceof Error ? err.message : 'No se pudo registrar el peso del despacho.';
      this.cdr.detectChanges();
    }
  }

  private mensajeInvalido(): string {
    if (this.pesoBruto == null || this.pesoTara == null)
      return 'Digite el peso bruto y el peso tara del camión.';
    if (this.pesoBruto <= 0) return 'El peso bruto debe ser mayor a 0 kg.';
    if (this.pesoTara < 0) return 'El peso tara no puede ser negativo.';
    if (this.pesoBruto < this.pesoTara) return 'El peso bruto no puede ser menor que el peso tara.';
    // Espejo del gate del backend (MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta).
    if (this.pesoBruto === this.pesoTara)
      return 'El peso neto no puede ser 0 kg: el bruto y la tara son iguales. El bruto es el camión CARGADO y la tara el camión VACÍO; si sólo tiene los kilos netos del despacho, cárguelos en el peso bruto y deje la tara en 0.';
    return 'Revise el peso del despacho.';
  }
}

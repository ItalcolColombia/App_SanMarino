import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
  ChangeDetectorRef
} from '@angular/core';

import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MovimientoPolloEngordeService } from '../../services/movimiento-pollo-engorde.service';
import { VentaPanamaPolloEngordeService } from '../../services/venta-panama-pollo-engorde.service';
import { LoteAveEngordeDto } from '../../../lote-engorde/services/lote-engorde.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { VentaPanamaLineaUI } from '../../models/venta-panama.model';
import { buildVentaPanamaDespachoDto, VentaPanamaFormValue } from '../../funciones/mapear-venta-panama-dto.funcion';
import { formatearNumero as fmtNumero } from '../../funciones/formato.funcion';

const SIN_GALPON = '__SIN_GALPON__';

/**
 * Modal de venta Panamá (despacho por galpón). El usuario elige un galpón de la granja, ve las
 * MIXTAS disponibles de cada lote y asigna H/M sobre ellas (con tope H+M ≤ mixtas). Al guardar se
 * crea un movimiento Pendiente por lote (EsVentaMixta) que descuenta las mixtas del lote.
 */
@Component({
  selector: 'app-modal-venta-panama',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule],
  templateUrl: './modal-venta-panama.component.html',
  styleUrls: ['./modal-venta-panama.component.scss']
})
export class ModalVentaPanamaComponent implements OnChanges {
  @Input() isOpen = false;
  @Input() granjaId: number | null = null;
  @Input() granjaNombre = '';
  /** Lotes Ave Engorde de la granja (se filtran por galpón dentro del modal). */
  @Input() lotesGranja: LoteAveEngordeDto[] = [];

  @Output() close = new EventEmitter<void>();
  /** Emite la cantidad de movimientos creados. */
  @Output() saved = new EventEmitter<number>();

  form!: FormGroup;
  loading = false;
  loadingLineas = false;
  error: string | null = null;

  galpones: Array<{ id: string; label: string }> = [];
  selectedGalponId: string | null = null;
  lineas: VentaPanamaLineaUI[] = [];

  constructor(
    private fb: FormBuilder,
    private movimientoSvc: MovimientoPolloEngordeService,
    private panamaSvc: VentaPanamaPolloEngordeService,
    private tokenStorage: TokenStorageService,
    private cdr: ChangeDetectorRef
  ) {
    this.buildForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      this.error = null;
      this.selectedGalponId = null;
      this.lineas = [];
      this.resetForm();
      this.buildGalpones();
    }
  }

  private buildForm(): void {
    const hoy = new Date();
    hoy.setHours(0, 0, 0, 0);
    this.form = this.fb.group({
      fechaMovimiento: [hoy.toISOString().slice(0, 10), [Validators.required]],
      numeroDespacho: [null as string | null],
      edadAves: [null as number | null],
      totalPollosGalpon: [null as number | null],
      raza: [null as string | null],
      placa: [null as string | null],
      horaSalida: [null as string | null],
      guiaAgrocalidad: [null as string | null],
      sellos: [null as string | null],
      ayuno: [null as string | null],
      conductor: [null as string | null],
      // Peso báscula obligatorio en ventas (misma regla que la venta Ecuador).
      pesoBruto: [null as number | null, [Validators.required, Validators.min(0.01)]],
      pesoTara: [null as number | null, [Validators.required, Validators.min(0)]],
      motivoMovimiento: [null as string | null],
      observaciones: [null as string | null]
    });
  }

  private resetForm(): void {
    const hoy = new Date();
    hoy.setHours(0, 0, 0, 0);
    this.form.reset({ fechaMovimiento: hoy.toISOString().slice(0, 10) });
  }

  /** Galpones (con lotes Ave Engorde) de la granja seleccionada. */
  private buildGalpones(): void {
    const seen = new Set<string>();
    const result: Array<{ id: string; label: string }> = [];
    for (const l of this.lotesGranja ?? []) {
      const id = (l.galponId ?? '').trim() || SIN_GALPON;
      if (seen.has(id)) continue;
      seen.add(id);
      result.push({ id, label: this.labelGalpon(l) });
    }
    this.galpones = result.sort((a, b) => a.label.localeCompare(b.label, 'es', { numeric: true }));
  }

  private labelGalpon(l: LoteAveEngordeDto): string {
    const n = l.galpon?.galponNombre;
    if (n && String(n).trim()) return String(n).trim();
    const id = (l.galponId ?? '').trim();
    return id || '— Sin galpón —';
  }

  get hayLineasConCantidad(): boolean {
    return this.lineas.some((l) => l.h + l.m > 0);
  }

  get totalAsignado(): number {
    return this.lineas.reduce((s, l) => s + l.h + l.m, 0);
  }

  /** Carga los lotes del galpón elegido y su disponibilidad de mixtas. */
  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.lineas = [];
    this.error = null;
    if (!galponId) return;

    const lotes = (this.lotesGranja ?? []).filter(
      (l) => ((l.galponId ?? '').trim() || SIN_GALPON) === galponId
    );
    if (lotes.length === 0) return;

    this.loadingLineas = true;
    const loteIds = lotes.map((l) => l.loteAveEngordeId);
    this.movimientoSvc.postAvesDisponiblesLotes({ tipoLote: 'LoteAveEngorde', loteIds }).subscribe({
      next: (resp) => {
        const dispById = new Map<number, number>();
        for (const row of resp.items ?? []) dispById.set(row.loteId, row.disponibles?.mixtasDisponibles ?? 0);
        this.lineas = lotes.map((l) => ({
          loteId: l.loteAveEngordeId,
          loteNombre: l.loteNombre || `Lote ${l.loteAveEngordeId}`,
          galponId: galponId,
          galponLabel: this.labelGalpon(l),
          mixtasDisponibles: Math.max(0, dispById.get(l.loteAveEngordeId) ?? l.mixtas ?? 0),
          h: 0,
          m: 0,
          hStr: '',
          mStr: '',
          flashExceso: false
        }));
        this.loadingLineas = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loadingLineas = false;
        this.error = 'No se pudo cargar la disponibilidad de mixtas por lote.';
      }
    });
  }

  /** Asignación H/M con tope conjunto: H + M ≤ mixtas disponibles del lote. */
  onCantidadInput(ev: Event, line: VentaPanamaLineaUI, field: 'h' | 'm'): void {
    const input = ev.target as HTMLInputElement;
    const digits = (input.value ?? '').replace(/\D/g, '');
    const parsed = digits === '' ? 0 : parseInt(digits, 10) || 0;
    const otro = field === 'h' ? line.m : line.h;
    const maxField = Math.max(0, line.mixtasDisponibles - otro);
    const clamped = Math.min(parsed, maxField);
    const exceeded = parsed > maxField;
    const nextStr = digits === '' ? '' : String(clamped);

    if (field === 'h') {
      line.h = clamped;
      line.hStr = nextStr;
    } else {
      line.m = clamped;
      line.mStr = nextStr;
    }
    if (exceeded) {
      line.flashExceso = true;
      window.setTimeout(() => {
        line.flashExceso = false;
        this.cdr.markForCheck();
      }, 900);
    }
    if (input.value !== nextStr) input.value = nextStr;
    this.cdr.detectChanges();
  }

  formatearNumero(n: number): string {
    return fmtNumero(n);
  }

  onClose(): void {
    this.close.emit();
  }

  async onSubmit(): Promise<void> {
    if (this.loading || this.loadingLineas) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error = (this.form.get('pesoBruto')?.invalid || this.form.get('pesoTara')?.invalid)
        ? 'El peso báscula es obligatorio para registrar la venta: digite peso bruto (> 0) y peso tara.'
        : 'Complete la fecha del despacho.';
      return;
    }
    if (!this.hayLineasConCantidad) {
      this.error = 'Asigne hembras/machos en al menos un lote.';
      return;
    }
    const session = this.tokenStorage.get();
    const userId = session?.user?.userId ?? 0;
    const dto = buildVentaPanamaDespachoDto(this.form.getRawValue() as VentaPanamaFormValue, {
      granjaId: this.granjaId,
      usuarioMovimientoId: userId,
      lineas: this.lineas,
      lotesGranja: this.lotesGranja.map((l) => ({
        loteAveEngordeId: l.loteAveEngordeId,
        granjaId: l.granjaId,
        nucleoId: l.nucleoId,
        galponId: l.galponId
      }))
    });
    if (!dto) {
      this.error = 'Sin líneas con cantidad.';
      return;
    }

    this.loading = true;
    this.error = null;
    try {
      const res = await firstValueFrom(this.panamaSvc.createVentaPanamaDespacho(dto));
      this.loading = false;
      this.saved.emit(res.movimientos.length);
    } catch (err: unknown) {
      this.loading = false;
      this.error = err instanceof Error ? err.message : 'Error al guardar la venta Panamá.';
    }
  }
}

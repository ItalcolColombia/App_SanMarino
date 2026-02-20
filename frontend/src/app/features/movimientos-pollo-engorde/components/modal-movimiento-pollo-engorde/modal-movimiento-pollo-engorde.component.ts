import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
  OnDestroy
} from '@angular/core';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  Validators,
  ReactiveFormsModule
} from '@angular/forms';
import {
  MovimientoPolloEngordeService,
  MovimientoPolloEngordeDto,
  CreateMovimientoPolloEngordeDto,
  UpdateMovimientoPolloEngordeDto
} from '../../services/movimiento-pollo-engorde.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';

export interface LoteDestinoOption {
  value: string;
  label: string;
}

export interface AvailableBirds {
  total: number;
  hembras?: number;
  machos?: number;
  mixtas?: number;
}

@Component({
  selector: 'app-modal-movimiento-pollo-engorde',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ConfirmationModalComponent],
  templateUrl: './modal-movimiento-pollo-engorde.component.html',
  styleUrls: ['./modal-movimiento-pollo-engorde.component.scss']
})
export class ModalMovimientoPolloEngordeComponent implements OnChanges, OnDestroy {
  @Input() isOpen = false;
  @Input() loteOrigenValue: string | null = null; // "ae-123" | "rae-456"
  @Input() lotesDestinoOptions: LoteDestinoOption[] = [];
  @Input() editingMovimiento: MovimientoPolloEngordeDto | null = null;
  /** Disponibilidad en lote (para limitar cantidades al crear). */
  @Input() availableBirds: AvailableBirds | null = null;
  /** Datos del lote seleccionado (raza, año, fecha encasetamiento) para prellenar y calcular edad en días. Del lote normal o del lote padre si es reproductora. */
  @Input() lotInfoFromLote: { raza?: string | null; anoTablaGenetica?: number | null; fechaEncasetamiento?: string | null } | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();

  form!: FormGroup;
  loading = false;
  error: string | null = null;
  showConfirmModal = false;
  private fechaMovimientoSub?: Subscription;
  confirmModalData: ConfirmationModalData = {
    title: 'Confirmar movimiento',
    message: '¿Confirmar registro del movimiento?',
    type: 'info',
    confirmText: 'Confirmar',
    cancelText: 'Cancelar',
    showCancel: true
  };

  get isReadOnly(): boolean {
    const e = this.editingMovimiento;
    return !!(e && (e.estado === 'Completado' || e.estado === 'Cancelado'));
  }

  get modalTitle(): string {
    if (this.editingMovimiento) {
      if (this.isReadOnly) return 'Detalle de Movimiento';
      return 'Editar Movimiento';
    }
    return 'Nuevo Movimiento de Pollo Engorde';
  }

  /** Opciones de destino excluyendo el lote origen actual. */
  get destinoOpciones(): LoteDestinoOption[] {
    if (!this.loteOrigenValue) return this.lotesDestinoOptions;
    return this.lotesDestinoOptions.filter((o) => o.value !== this.loteOrigenValue);
  }

  /** True si el tipo de movimiento es Venta (las aves salen a comprador externo; destino interno suele no aplicar). */
  get isTipoVenta(): boolean {
    return this.form?.get('tipoMovimiento')?.value === 'Venta';
  }

  get availableTotal(): number {
    return this.availableBirds?.total ?? 0;
  }

  get maxHembras(): number | null {
    return this.availableBirds?.hembras ?? null;
  }

  get maxMachos(): number | null {
    return this.availableBirds?.machos ?? null;
  }

  get maxMixtas(): number | null {
    return this.availableBirds?.mixtas ?? null;
  }

  /** True si el total a mover supera lo disponible (solo aplica al crear con availableBirds). */
  get exceedsAvailable(): boolean {
    if (!this.availableBirds || this.editingMovimiento) return false;
    return this.totalAves > this.availableBirds.total;
  }

  /** True si Raza y Edad (días) vienen del lote y deben mostrarse deshabilitados en gris. */
  get lotFieldsReadOnly(): boolean {
    return !!(this.lotInfoFromLote && !this.editingMovimiento);
  }

  /** Edad en días calculada desde fecha encasetamiento del lote hasta la fecha del movimiento. */
  get edadCalculadaEnDias(): number | null {
    const fechaEnc = this.lotInfoFromLote?.fechaEncasetamiento;
    const fechaMov = this.form?.get('fechaMovimiento')?.value;
    if (!fechaEnc || !fechaMov) return null;
    const dEnc = new Date(fechaEnc);
    const dMov = new Date(fechaMov);
    if (isNaN(dEnc.getTime()) || isNaN(dMov.getTime())) return null;
    const diffMs = dMov.getTime() - dEnc.getTime();
    const dias = Math.floor(diffMs / (24 * 60 * 60 * 1000));
    return dias >= 0 ? dias : null;
  }

  get origenLabel(): string {
    if (this.editingMovimiento) {
      const g = this.editingMovimiento.granjaOrigenNombre ?? '';
      const l = this.editingMovimiento.loteOrigenNombre ?? '';
      if (g || l) return [g, l].filter(Boolean).join(' · ');
      return this.editingMovimiento.tipoLoteOrigen === 'AveEngorde'
        ? `Ave Engorde #${this.editingMovimiento.loteOrigenId ?? '?'}`
        : `Reproductora #${this.editingMovimiento.loteOrigenId ?? '?'}`;
    }
    if (!this.loteOrigenValue) return '—';
    const opt = this.lotesDestinoOptions.find((o) => o.value === this.loteOrigenValue);
    if (opt) return opt.label;
    if (this.loteOrigenValue.startsWith('ae-')) return `Ave Engorde (ID: ${this.loteOrigenValue.replace('ae-', '')})`;
    if (this.loteOrigenValue.startsWith('rae-')) return `Reproductora (ID: ${this.loteOrigenValue.replace('rae-', '')})`;
    return this.loteOrigenValue;
  }

  constructor(
    private fb: FormBuilder,
    private movimientoSvc: MovimientoPolloEngordeService,
    private tokenStorage: TokenStorageService
  ) {
    this.buildForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      this.error = null;
      this.fechaMovimientoSub?.unsubscribe();
      if (this.editingMovimiento) {
        this.loadFormFromMovimiento(this.editingMovimiento);
        this.form.get('raza')?.enable();
        this.form.get('edadAves')?.enable();
      } else {
        this.resetForm();
        this.applyLotInfoToForm();
        this.subscribeFechaMovimientoForEdad();
      }
    }
  }

  ngOnDestroy(): void {
    this.fechaMovimientoSub?.unsubscribe();
  }

  private buildForm(): void {
    const hoy = new Date();
    hoy.setHours(0, 0, 0, 0);
    this.form = this.fb.group({
      fechaMovimiento: [hoy.toISOString().slice(0, 10), [Validators.required]],
      tipoMovimiento: ['Venta', [Validators.required]],
      loteDestinoValue: [null as string | null],
      cantidadHembras: [0, [Validators.required, Validators.min(0)]],
      cantidadMachos: [0, [Validators.required, Validators.min(0)]],
      cantidadMixtas: [0, [Validators.required, Validators.min(0)]],
      motivoMovimiento: [null as string | null],
      observaciones: [null as string | null],
      // Despacho / salida (venta)
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
      pesoBruto: [null as number | null],
      pesoTara: [null as number | null]
    });
  }

  private resetForm(): void {
    const hoy = new Date();
    hoy.setHours(0, 0, 0, 0);
    this.form.patchValue({
      fechaMovimiento: hoy.toISOString().slice(0, 10),
      tipoMovimiento: 'Venta',
      loteDestinoValue: null,
      cantidadHembras: 0,
      cantidadMachos: 0,
      cantidadMixtas: 0,
      motivoMovimiento: null,
      observaciones: null,
      numeroDespacho: null,
      edadAves: null,
      totalPollosGalpon: null,
      raza: null,
      placa: null,
      horaSalida: null,
      guiaAgrocalidad: null,
      sellos: null,
      ayuno: null,
      conductor: null,
      pesoBruto: null,
      pesoTara: null
    });
    this.form.markAsUntouched();
  }

  /** Prellena Raza y Edad (días) desde el lote; deshabilita ambos cuando vienen del lote. */
  private applyLotInfoToForm(): void {
    const info = this.lotInfoFromLote;
    if (!info || this.editingMovimiento) return;
    const patch: { raza?: string | null; edadAves?: number | null } = {};
    if (info.raza != null && info.raza !== '') patch.raza = info.raza;
    const edad = this.edadCalculadaEnDias;
    if (edad != null) patch.edadAves = edad;
    if (Object.keys(patch).length > 0) this.form.patchValue(patch);
    if (this.lotFieldsReadOnly) {
      this.form.get('raza')?.disable();
      this.form.get('edadAves')?.disable();
    }
  }

  /** Al cambiar la fecha del movimiento, actualizar edad calculada si los campos vienen del lote. */
  private subscribeFechaMovimientoForEdad(): void {
    if (!this.lotInfoFromLote?.fechaEncasetamiento) return;
    this.fechaMovimientoSub = this.form.get('fechaMovimiento')?.valueChanges?.subscribe(() => {
      const edad = this.edadCalculadaEnDias;
      if (edad != null) this.form.get('edadAves')?.setValue(edad, { emitEvent: false });
    }) ?? undefined;
  }

  private loadFormFromMovimiento(m: MovimientoPolloEngordeDto): void {
    const destValue =
      m.tipoLoteDestino === 'AveEngorde' && m.loteDestinoId != null
        ? `ae-${m.loteDestinoId}`
        : m.tipoLoteDestino === 'ReproductoraAveEngorde' && m.loteDestinoId != null
          ? `rae-${m.loteDestinoId}`
          : null;
    const horaSalida = m.horaSalida != null ? String(m.horaSalida).slice(0, 5) : null;
    this.form.patchValue({
      fechaMovimiento: m.fechaMovimiento?.slice(0, 10) ?? '',
      tipoMovimiento: m.tipoMovimiento ?? 'Traslado',
      loteDestinoValue: destValue,
      cantidadHembras: m.cantidadHembras ?? 0,
      cantidadMachos: m.cantidadMachos ?? 0,
      cantidadMixtas: m.cantidadMixtas ?? 0,
      motivoMovimiento: m.motivoMovimiento ?? null,
      observaciones: m.observaciones ?? null,
      numeroDespacho: m.numeroDespacho ?? null,
      edadAves: m.edadAves ?? null,
      totalPollosGalpon: m.totalPollosGalpon ?? null,
      raza: m.raza ?? null,
      placa: m.placa ?? null,
      horaSalida,
      guiaAgrocalidad: m.guiaAgrocalidad ?? null,
      sellos: m.sellos ?? null,
      ayuno: m.ayuno ?? null,
      conductor: m.conductor ?? null,
      pesoBruto: m.pesoBruto ?? null,
      pesoTara: m.pesoTara ?? null
    });
    if (this.isReadOnly) this.form.disable();
    else this.form.enable();
  }

  onClose(): void {
    this.close.emit();
  }

  onSubmit(): void {
    if (this.form.invalid || this.loading) return;
    if (!this.loteOrigenValue && !this.editingMovimiento) return;
    if (this.exceedsAvailable) {
      this.error = `No puede mover más de ${this.formatearNumero(this.availableTotal)} aves (disponibles en el lote).`;
      return;
    }
    this.error = null;
    this.confirmModalData = {
      title: this.editingMovimiento ? 'Confirmar actualización' : 'Confirmar movimiento',
      message: this.editingMovimiento
        ? `¿Actualizar movimiento con ${this.formatearNumero(this.totalAves)} aves?`
        : `¿Registrar movimiento de ${this.formatearNumero(this.totalAves)} aves?`,
      type: 'info',
      confirmText: 'Confirmar',
      cancelText: 'Cancelar',
      showCancel: true
    };
    this.showConfirmModal = true;
  }

  onConfirmSubmit(): void {
    this.showConfirmModal = false;
    this.doSubmit();
  }

  onCancelConfirm(): void {
    this.showConfirmModal = false;
  }

  private doSubmit(): void {
    const session = this.tokenStorage.get();
    const userId = session?.user?.userId ?? 0;

    this.loading = true;

    if (this.editingMovimiento && !this.isReadOnly) {
      const updateDto = this.buildUpdateDto();
      this.movimientoSvc.update(this.editingMovimiento.id, updateDto).subscribe({
        next: () => {
          this.loading = false;
          this.save.emit();
        },
        error: (err) => {
          this.loading = false;
          this.error = err?.message ?? err?.error ?? 'Error al actualizar.';
        }
      });
      return;
    }

    const dto = this.buildCreateDto(userId);
    if (!dto) {
      this.loading = false;
      this.error = 'Origen no válido.';
      return;
    }

    this.movimientoSvc.create(dto).subscribe({
      next: () => {
        this.loading = false;
        this.save.emit();
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.message ?? err?.error ?? 'Error al guardar.';
      }
    });
  }

  private buildUpdateDto(): UpdateMovimientoPolloEngordeDto {
    const v = this.form.getRawValue();
    return {
      fechaMovimiento: v.fechaMovimiento ? new Date(v.fechaMovimiento).toISOString() : undefined,
      tipoMovimiento: v.tipoMovimiento || undefined,
      cantidadHembras: Number(v.cantidadHembras) ?? undefined,
      cantidadMachos: Number(v.cantidadMachos) ?? undefined,
      cantidadMixtas: Number(v.cantidadMixtas) ?? undefined,
      motivoMovimiento: v.motivoMovimiento || undefined,
      observaciones: v.observaciones || undefined,
      numeroDespacho: v.numeroDespacho || undefined,
      edadAves: v.edadAves != null && v.edadAves !== '' ? Number(v.edadAves) : undefined,
      totalPollosGalpon: v.totalPollosGalpon != null && v.totalPollosGalpon !== '' ? Number(v.totalPollosGalpon) : undefined,
      raza: v.raza || undefined,
      placa: v.placa || undefined,
      horaSalida: v.horaSalida ? `${v.horaSalida}:00` : undefined,
      guiaAgrocalidad: v.guiaAgrocalidad || undefined,
      sellos: v.sellos || undefined,
      ayuno: v.ayuno || undefined,
      conductor: v.conductor || undefined,
      pesoBruto: v.pesoBruto != null && v.pesoBruto !== '' ? Number(v.pesoBruto) : undefined,
      pesoTara: v.pesoTara != null && v.pesoTara !== '' ? Number(v.pesoTara) : undefined
    };
  }

  private buildCreateDto(usuarioMovimientoId: number): CreateMovimientoPolloEngordeDto | null {
    const v = this.form.getRawValue();
    const origen = this.parseLoteValue(this.loteOrigenValue!);
    if (!origen) return null;

    const dest = this.isTipoVenta ? null : (v.loteDestinoValue ? this.parseLoteValue(v.loteDestinoValue) : null);

    const dto: CreateMovimientoPolloEngordeDto = {
      fechaMovimiento: new Date(v.fechaMovimiento).toISOString(),
      tipoMovimiento: v.tipoMovimiento || 'Venta',
      loteAveEngordeOrigenId: origen.tipo === 'ae' ? origen.id : null,
      loteReproductoraAveEngordeOrigenId: origen.tipo === 'rae' ? origen.id : null,
      loteAveEngordeDestinoId: dest?.tipo === 'ae' ? dest.id : null,
      loteReproductoraAveEngordeDestinoId: dest?.tipo === 'rae' ? dest.id : null,
      cantidadHembras: Number(v.cantidadHembras) || 0,
      cantidadMachos: Number(v.cantidadMachos) || 0,
      cantidadMixtas: Number(v.cantidadMixtas) || 0,
      motivoMovimiento: v.motivoMovimiento || null,
      observaciones: v.observaciones || null,
      usuarioMovimientoId,
      numeroDespacho: v.numeroDespacho || null,
      edadAves: v.edadAves != null && v.edadAves !== '' ? Number(v.edadAves) : null,
      totalPollosGalpon: v.totalPollosGalpon != null && v.totalPollosGalpon !== '' ? Number(v.totalPollosGalpon) : null,
      raza: v.raza || null,
      placa: v.placa || null,
      horaSalida: v.horaSalida ? `${v.horaSalida}:00` : null,
      guiaAgrocalidad: v.guiaAgrocalidad || null,
      sellos: v.sellos || null,
      ayuno: v.ayuno || null,
      conductor: v.conductor || null,
      pesoBruto: v.pesoBruto != null && v.pesoBruto !== '' ? Number(v.pesoBruto) : null,
      pesoTara: v.pesoTara != null && v.pesoTara !== '' ? Number(v.pesoTara) : null
    };
    return dto;
  }

  private parseLoteValue(value: string): { tipo: 'ae' | 'rae'; id: number } | null {
    if (!value) return null;
    if (value.startsWith('ae-')) {
      const id = parseInt(value.replace('ae-', ''), 10);
      return isNaN(id) ? null : { tipo: 'ae', id };
    }
    if (value.startsWith('rae-')) {
      const id = parseInt(value.replace('rae-', ''), 10);
      return isNaN(id) ? null : { tipo: 'rae', id };
    }
    return null;
  }

  get totalAves(): number {
    const v = this.form?.getRawValue();
    if (!v) return 0;
    return (Number(v.cantidadHembras) || 0) + (Number(v.cantidadMachos) || 0) + (Number(v.cantidadMixtas) || 0);
  }

  /** Muestra la sección de despacho (venta / salida de aves). */
  get isDespacho(): boolean {
    return (this.form?.get('tipoMovimiento')?.value ?? '') === 'Venta';
  }

  get pesoNeto(): number | null {
    const v = this.form?.getRawValue();
    if (!v) return null;
    const bruto = v.pesoBruto != null && v.pesoBruto !== '' ? Number(v.pesoBruto) : null;
    const tara = v.pesoTara != null && v.pesoTara !== '' ? Number(v.pesoTara) : null;
    if (bruto == null || tara == null) return null;
    return bruto - tara;
  }

  get promedioPesoAve(): number | null {
    const neto = this.pesoNeto;
    const total = this.totalAves;
    if (neto == null || total <= 0) return null;
    return neto / total;
  }

  formatearNumero(n: number): string {
    return new Intl.NumberFormat('es-CO').format(n);
  }

  /** Valor para vista solo lectura (detalle). */
  valor(m: MovimientoPolloEngordeDto | null, key: keyof MovimientoPolloEngordeDto): string | number {
    if (!m) return '—';
    const v = (m as unknown as Record<string, unknown>)[key];
    if (v == null || v === '') return '—';
    if (typeof v === 'number') return this.formatearNumero(v);
    if (key === 'fechaMovimiento' && typeof v === 'string') return this.fechaCorta(v);
    if (key === 'horaSalida' && typeof v === 'string') return String(v).slice(0, 5);
    return String(v);
  }

  fechaCorta(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    return isNaN(d.getTime()) ? iso : d.toLocaleDateString('es');
  }

  get showDespachoEnDetalle(): boolean {
    return (this.editingMovimiento?.tipoMovimiento ?? '') === 'Venta';
  }
}

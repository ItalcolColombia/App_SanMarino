import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
  OnDestroy
} from '@angular/core';
import { Subscription, firstValueFrom } from 'rxjs';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  Validators,
  ReactiveFormsModule,
  FormsModule
} from '@angular/forms';
import {
  MovimientoPolloEngordeService,
  MovimientoPolloEngordeDto,
  CreateMovimientoPolloEngordeDto,
  CreateVentaGranjaDespachoDto,
  UpdateMovimientoPolloEngordeDto,
  ResumenAvesLoteDto,
  VentaGranjaDespachoLineaDto
} from '../../services/movimiento-pollo-engorde.service';
import { LoteAveEngordeDto } from '../../../lote-engorde/services/lote-engorde.service';
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

/** Una fila por lote en venta por granja (despacho multi-galpón). */
export interface VentaLineaGranja {
  loteId: number;
  loteNombre: string;
  galponId: string;
  galponLabel: string;
  maxH: number;
  maxM: number;
  maxX: number;
  h: number;
  m: number;
  x: number;
  /** Texto del input (solo dígitos); evita [value] numérico que pisa el cursor al escribir. */
  hStr: string;
  mStr: string;
  xStr: string;
}

export interface MovimientoPolloEngordeSaveDetail {
  ventaGranjaBatchCount?: number;
}

@Component({
  selector: 'app-modal-movimiento-pollo-engorde',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, ConfirmationModalComponent],
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
  /** Venta desde granja: sin lote previo; cantidades por lote en `ventaLineasGranja`. */
  @Input() ventaPorGranjaMode = false;
  @Input() lotesVentaGranja: LoteAveEngordeDto[] = [];
  @Input() granjaVentaNombre = '';

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<MovimientoPolloEngordeSaveDetail>();

  form!: FormGroup;
  loading = false;
  /** Carga de disponibilidad por lote (resumen) al abrir venta por granja. */
  loadingVentaLineas = false;
  ventaLineasGranja: VentaLineaGranja[] = [];
  /** Cache de grupos por galpón (no recalcular en cada CD con un getter). */
  gruposVentaPorGalpon: { galponId: string; galponLabel: string; lineas: VentaLineaGranja[] }[] = [];
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
    if (this.ventaPorGranjaMode) return 'Nueva venta por granja (despacho)';
    return 'Nuevo Movimiento de Pollo Engorde';
  }

  /** Opciones de destino excluyendo el lote origen actual. */
  get destinoOpciones(): LoteDestinoOption[] {
    if (!this.loteOrigenValue) return this.lotesDestinoOptions;
    return this.lotesDestinoOptions.filter((o) => o.value !== this.loteOrigenValue);
  }

  /** True si el tipo de movimiento es Venta (las aves salen a comprador externo; destino interno suele no aplicar). */
  get isTipoVenta(): boolean {
    return (this.form?.getRawValue()?.tipoMovimiento ?? '') === 'Venta';
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

  /** True si el total a mover supera lo disponible (solo aplica al crear con availableBirds o líneas venta granja). */
  get exceedsAvailable(): boolean {
    if (this.editingMovimiento) return false;
    if (this.ventaPorGranjaMode) return this.exceedsVentaGranjaLine;
    if (!this.availableBirds) return false;
    return this.totalAves > this.availableBirds.total;
  }

  /** Alguna fila en venta por granja supera disponibles por sexo. */
  get exceedsVentaGranjaLine(): boolean {
    return this.ventaLineasGranja.some((l) => l.h > l.maxH || l.m > l.maxM || l.x > l.maxX);
  }

  /** True si Raza y Edad (días) vienen del lote y deben mostrarse deshabilitados en gris. */
  get lotFieldsReadOnly(): boolean {
    if (this.ventaPorGranjaMode && !this.editingMovimiento) return false;
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
    if (this.ventaPorGranjaMode) {
      const g = (this.granjaVentaNombre || '').trim();
      return g ? `Granja: ${g} (varios galpones / lotes)` : 'Granja (varios galpones / lotes)';
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
    if (changes['isOpen'] && !this.isOpen) {
      this.ventaLineasGranja = [];
      this.loadingVentaLineas = false;
    }
    if (changes['isOpen'] && this.isOpen) {
      this.error = null;
      this.fechaMovimientoSub?.unsubscribe();
      if (this.editingMovimiento) {
        this.loadFormFromMovimiento(this.editingMovimiento);
        this.form.get('raza')?.enable();
        this.form.get('edadAves')?.enable();
      } else {
        this.resetForm();
        if (this.ventaPorGranjaMode) {
          this.form.patchValue({ tipoMovimiento: 'Venta' });
          this.form.get('tipoMovimiento')?.disable({ emitEvent: false });
          this.loadVentaGranjaLineas();
        } else {
          this.form.get('tipoMovimiento')?.enable({ emitEvent: false });
          this.applyLotInfoToForm();
          this.subscribeFechaMovimientoForEdad();
        }
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
    if (this.loading) return;

    if (this.ventaPorGranjaMode && !this.editingMovimiento) {
      if (this.form.invalid || this.loadingVentaLineas) return;
      const withQty = this.ventaLineasGranja.filter((l) => l.h + l.m + l.x > 0);
      if (withQty.length === 0) {
        this.error = 'Indique cantidad a vender en al menos un lote.';
        return;
      }
      if (this.exceedsVentaGranjaLine) {
        this.error =
          'Alguna cantidad supera lo disponible en el lote (H / M / mixtas según corresponda).';
        return;
      }
      this.error = null;
      this.confirmModalData = {
        title: 'Confirmar venta por granja',
        message: `Se registrarán ${this.formatearNumero(withQty.length)} movimiento(s) de venta (uno por lote). El mismo despacho y datos de transporte aplican a todos.`,
        type: 'info',
        confirmText: 'Confirmar',
        cancelText: 'Cancelar',
        showCancel: true
      };
      this.showConfirmModal = true;
      return;
    }

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

    if (this.ventaPorGranjaMode && !this.editingMovimiento) {
      void this.doSubmitVentaGranjaAsync(userId);
      return;
    }

    this.loading = true;

    if (this.editingMovimiento && !this.isReadOnly) {
      const updateDto = this.buildUpdateDto();
      this.movimientoSvc.update(this.editingMovimiento.id, updateDto).subscribe({
        next: () => {
          this.loading = false;
          this.save.emit({});
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
        this.save.emit({});
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.message ?? err?.error ?? 'Error al guardar.';
      }
    });
  }

  private async doSubmitVentaGranjaAsync(usuarioMovimientoId: number): Promise<void> {
    const dto = this.buildVentaGranjaDespachoDto(usuarioMovimientoId);
    if (!dto) {
      this.error = 'Sin líneas con cantidad.';
      return;
    }
    this.loading = true;
    this.error = null;
    try {
      const res = await firstValueFrom(this.movimientoSvc.createVentaGranjaDespacho(dto));
      this.loading = false;
      this.save.emit({ ventaGranjaBatchCount: res.movimientos.length });
    } catch (err: unknown) {
      this.loading = false;
      this.error =
        err instanceof Error ? err.message : String((err as { message?: string })?.message ?? 'Error al guardar.');
    }
  }

  private buildVentaGranjaDespachoDto(usuarioMovimientoId: number): CreateVentaGranjaDespachoDto | null {
    const conQty = this.ventaLineasGranja.filter((l) => l.h + l.m + l.x > 0);
    if (conQty.length === 0) return null;
    const v = this.form.getRawValue();
    const granjaId = this.lotesVentaGranja[0]?.granjaId ?? null;
    const lineas: VentaGranjaDespachoLineaDto[] = [];
    for (const linea of conQty) {
      const lote = this.lotesVentaGranja.find((l) => l.loteAveEngordeId === linea.loteId);
      if (!lote) return null;
      const nid = lote.nucleoId != null && String(lote.nucleoId).trim() !== '' ? String(lote.nucleoId).trim() : null;
      const gpid =
        lote.galponId != null && String(lote.galponId).trim() !== '' ? String(lote.galponId).trim() : null;
      lineas.push({
        loteAveEngordeOrigenId: linea.loteId,
        granjaOrigenId: lote.granjaId ?? null,
        nucleoOrigenId: nid,
        galponOrigenId: gpid,
        cantidadHembras: linea.h,
        cantidadMachos: linea.m,
        cantidadMixtas: linea.x
      });
    }
    return {
      fechaMovimiento: new Date(v.fechaMovimiento).toISOString(),
      tipoMovimiento: 'Venta',
      granjaOrigenId: granjaId,
      usuarioMovimientoId,
      motivoMovimiento: v.motivoMovimiento || null,
      observaciones: v.observaciones || null,
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
      pesoTara: v.pesoTara != null && v.pesoTara !== '' ? Number(v.pesoTara) : null,
      lineas
    };
  }

  private loadVentaGranjaLineas(): void {
    const lotes = this.lotesVentaGranja ?? [];
    this.ventaLineasGranja = [];
    this.gruposVentaPorGalpon = [];
    if (lotes.length === 0) {
      this.loadingVentaLineas = false;
      return;
    }
    this.loadingVentaLineas = true;
    this.error = null;
    const loteIds = lotes.map((l) => l.loteAveEngordeId);
    this.movimientoSvc.postResumenAvesLotes({ tipoLote: 'LoteAveEngorde', loteIds }).subscribe({
      next: (resp) => {
        const byId = new Map<number, ResumenAvesLoteDto | null>();
        for (const row of resp.items ?? []) {
          byId.set(row.loteId, row.resumen);
        }
        this.ventaLineasGranja = lotes.map((l) => {
          const r = byId.get(l.loteAveEngordeId);
          const maxH = r?.avesActualesHembras ?? l.hembrasL ?? 0;
          const maxM = r?.avesActualesMachos ?? l.machosL ?? 0;
          const maxX = r?.avesActualesMixtas ?? l.mixtas ?? 0;
          const galponId = (l.galponId ?? '').trim() || '__SIN_GALPON__';
          return {
            loteId: l.loteAveEngordeId,
            loteNombre: l.loteNombre || `Lote ${l.loteAveEngordeId}`,
            galponId,
            galponLabel: this.labelGalpon(l),
            maxH: Math.max(0, maxH),
            maxM: Math.max(0, maxM),
            maxX: Math.max(0, maxX),
            h: 0,
            m: 0,
            x: 0,
            hStr: '',
            mStr: '',
            xStr: ''
          };
        });
        this.loadingVentaLineas = false;
        this.rebuildGruposVentaPorGalpon();
      },
      error: () => {
        this.loadingVentaLineas = false;
        this.error = 'No se pudo cargar la disponibilidad por lote.';
      }
    });
  }

  private rebuildGruposVentaPorGalpon(): void {
    const map = new Map<string, { galponId: string; galponLabel: string; lineas: VentaLineaGranja[] }>();
    for (const line of this.ventaLineasGranja) {
      const key = line.galponId;
      if (!map.has(key)) {
        map.set(key, { galponId: key, galponLabel: line.galponLabel, lineas: [] });
      }
      map.get(key)!.lineas.push(line);
    }
    this.gruposVentaPorGalpon = Array.from(map.values()).sort((a, b) =>
      a.galponLabel.localeCompare(b.galponLabel, 'es', { numeric: true })
    );
  }

  private labelGalpon(l: LoteAveEngordeDto): string {
    const n = l.galpon?.galponNombre;
    if (n && String(n).trim()) return String(n).trim();
    const id = (l.galponId ?? '').trim();
    return id || '— Sin galpón —';
  }

  /**
   * Solo dígitos; actualiza el número usado en totales y validación.
   * Si supera el máximo del lote, el texto se ajusta al máximo permitido.
   */
  onLineaCantidadStr(line: VentaLineaGranja, field: 'h' | 'm' | 'x', value: string): void {
    const digits = (value ?? '').replace(/\D/g, '');
    const max = field === 'h' ? line.maxH : field === 'm' ? line.maxM : line.maxX;
    const parsed = digits === '' ? 0 : parseInt(digits, 10) || 0;
    const clamped = Math.min(parsed, max);
    if (field === 'h') {
      line.hStr = parsed > max ? String(max) : digits;
      line.h = clamped;
    } else if (field === 'm') {
      line.mStr = parsed > max ? String(max) : digits;
      line.m = clamped;
    } else {
      line.xStr = parsed > max ? String(max) : digits;
      line.x = clamped;
    }
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
    if (this.ventaPorGranjaMode && !this.editingMovimiento) {
      return this.ventaLineasGranja.reduce((s, l) => s + l.h + l.m + l.x, 0);
    }
    const v = this.form?.getRawValue();
    if (!v) return 0;
    return (Number(v.cantidadHembras) || 0) + (Number(v.cantidadMachos) || 0) + (Number(v.cantidadMixtas) || 0);
  }

  /** Muestra la sección de despacho (venta / salida de aves). */
  get isDespacho(): boolean {
    if (this.ventaPorGranjaMode && !this.editingMovimiento) return true;
    return (this.form?.getRawValue()?.tipoMovimiento ?? '') === 'Venta';
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

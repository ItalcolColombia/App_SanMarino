import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
  OnDestroy,
  ChangeDetectorRef,
  ChangeDetectionStrategy
} from '@angular/core';
import { Subscription, firstValueFrom } from 'rxjs';

import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import {
  MovimientoPolloEngordeService,
  MovimientoPolloEngordeDto,
  CreateMovimientoPolloEngordeDto,
  CreateVentaGranjaDespachoDto,
  UpdateMovimientoPolloEngordeDto,
  AvesDisponiblesVentaLoteDto
} from '../../services/movimiento-pollo-engorde.service';
import { LoteAveEngordeDto } from '../../../lote-engorde/services/lote-engorde.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import {
  LoteDestinoOption,
  AvailableBirds,
  VentaLineaGranja,
  MovimientoPolloEngordeSaveDetail
} from '../../models/venta-granja.model';
import {
  buildCreateDto as crearCreateDto,
  buildUpdateDto as crearUpdateDto,
  buildVentaGranjaDespachoDto as crearVentaGranjaDto,
  MovimientoModalFormValue
} from '../../funciones/mapear-movimiento-dto.funcion';
import {
  calcularProrateoPreview,
  calcularProrateoTotales,
  ProrateoRow,
  ProrateoTotales
} from '../../funciones/prorateo-peso.funcion';
import { formatearNumero as fmtNumero, fechaCorta as fmtFecha } from '../../funciones/formato.funcion';
import { marcarLotesBloqueadosVenta } from '../../funciones/detectar-lotes-bloqueados-venta.funcion';

/** Permiso que habilita cargar cantidades en lotes cerrados o de una corrida anterior en el mismo galpón. */
const PERMISO_VENDER_LOTES_CERRADOS = 'movimientos_pollo_engorde.vender_lotes_cerrados';

// Tipos movidos a models/; se re-exportan para no romper imports externos previos.
export type { LoteDestinoOption, AvailableBirds, VentaLineaGranja, MovimientoPolloEngordeSaveDetail };

@Component({
  selector: 'app-modal-movimiento-pollo-engorde',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, ConfirmationModalComponent],
  templateUrl: './modal-movimiento-pollo-engorde.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
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
  /** R2: permite vender por encima del disponible (sobrante de aves por galpón). */
  permitirSobrante = false;
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
    return 'Nueva venta de Pollo Engorde';
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
    private tokenStorage: TokenStorageService,
    private permService: UserPermissionService,
    private cdr: ChangeDetectorRef
  ) {
    this.buildForm();
  }

  /** True si el usuario puede cargar cantidades en lotes cerrados o de una corrida anterior en el mismo galpón. */
  get puedeVenderLotesCerrados(): boolean {
    return this.permService.has(PERMISO_VENDER_LOTES_CERRADOS);
  }

  /** Alguna línea está bloqueada (cerrado / corrida anterior) y el usuario no tiene el permiso para saltarlo. */
  get hayLoteBloqueadoSinPermiso(): boolean {
    if (this.puedeVenderLotesCerrados) return false;
    return this.ventaLineasGranja.some((l) => l.bloqueada);
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
        this.configureTotalPollosGalponControl();
      } else {
        this.resetForm();
        if (this.ventaPorGranjaMode) {
          this.form.patchValue({ tipoMovimiento: 'Venta' });
          this.form.get('tipoMovimiento')?.disable({ emitEvent: false });
          this.applyRazaFromVentaGranja();
          this.loadVentaGranjaLineas();
        } else {
          this.form.get('tipoMovimiento')?.enable({ emitEvent: false });
          this.applyLotInfoToForm();
          this.subscribeFechaMovimientoForEdad();
          this.configureTotalPollosGalponControl();
        }
      }
      this.syncPesoValidators();
    }
  }

  private applyRazaFromVentaGranja(): void {
    if (!this.ventaPorGranjaMode || this.editingMovimiento) return;
    const razas = (this.lotesVentaGranja ?? [])
      .map((l) => (l.raza ?? '').trim())
      .filter((x) => !!x);
    const unica = Array.from(new Set(razas));
    const razaValue = unica.length === 1 ? unica[0] : unica.length > 1 ? 'Varias' : null;
    this.form.patchValue({ raza: razaValue });
    this.form.get('raza')?.disable({ emitEvent: false });
  }

  totalSeleccionadoGalpon(lines: VentaLineaGranja[]): number {
    return (lines || []).reduce((s, l) => s + (l.h ?? 0) + (l.m ?? 0) + (l.x ?? 0), 0);
  }

  /**
   * En venta por granja: total pollos del despacho = suma de cantidades por lote (solo lectura).
   */
  private configureTotalPollosGalponControl(): void {
    const ctrl = this.form.get('totalPollosGalpon');
    if (!ctrl) return;
    if (this.ventaPorGranjaMode && !this.editingMovimiento) {
      ctrl.setValue(this.totalAves, { emitEvent: false });
      ctrl.disable({ emitEvent: false });
    } else if (!this.isReadOnly) {
      ctrl.enable({ emitEvent: false });
    }
  }

  private syncTotalPollosGalponVentaGranja(): void {
    if (!this.ventaPorGranjaMode || this.editingMovimiento) return;
    const ctrl = this.form.get('totalPollosGalpon');
    ctrl?.setValue(this.totalAves, { emitEvent: false });
  }

  private clearLineaFlash(line: VentaLineaGranja, field: 'h' | 'm' | 'x'): void {
    if (field === 'h') line.flashExcesoH = false;
    else if (field === 'm') line.flashExcesoM = false;
    else line.flashExcesoX = false;
    this.cdr.markForCheck();
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
    // Peso báscula obligatorio en ventas: al cambiar el tipo se ajustan los validadores.
    this.form.get('tipoMovimiento')?.valueChanges.subscribe(() => this.syncPesoValidators());
    this.syncPesoValidators();
  }

  /**
   * Peso báscula (bruto y tara) OBLIGATORIO cuando el movimiento es una venta.
   * Regla de negocio tras el incidente de una venta guardada sin pesos que
   * descuadró los reportes de liquidación (todo quedaba en 0 kg).
   */
  private syncPesoValidators(): void {
    const bruto = this.form?.get('pesoBruto');
    const tara = this.form?.get('pesoTara');
    if (!bruto || !tara) return;
    if (this.isDespacho) {
      bruto.setValidators([Validators.required, Validators.min(0.01)]);
      tara.setValidators([Validators.required, Validators.min(0)]);
    } else {
      bruto.clearValidators();
      tara.clearValidators();
    }
    bruto.updateValueAndValidity({ emitEvent: false });
    tara.updateValueAndValidity({ emitEvent: false });
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

    // Venta sin peso báscula: bloquear con mensaje claro (antes el form quedaba
    // inválido en silencio y en el peor caso se guardaban ventas con pesos NULL).
    if (this.isDespacho) {
      const brutoCtrl = this.form.get('pesoBruto');
      const taraCtrl = this.form.get('pesoTara');
      if (brutoCtrl?.invalid || taraCtrl?.invalid) {
        brutoCtrl?.markAsTouched();
        taraCtrl?.markAsTouched();
        this.error = 'El peso báscula es obligatorio para registrar la venta: digite peso bruto (> 0) y peso tara.';
        return;
      }
      const neto = this.pesoNeto;
      if (neto != null && neto < 0) {
        this.error = 'El peso bruto no puede ser menor que el peso tara.';
        return;
      }
    }

    if (this.ventaPorGranjaMode && !this.editingMovimiento) {
      if (this.form.invalid || this.loadingVentaLineas) return;
      const withQty = this.ventaLineasGranja.filter((l) => l.h + l.m + l.x > 0);
      if (withQty.length === 0) {
        this.error = 'Indique cantidad a vender en al menos un lote.';
        return;
      }
      if (!this.permitirSobrante && this.exceedsVentaGranjaLine) {
        this.error =
          'Alguna cantidad supera lo disponible en el lote (H / M / mixtas según corresponda). Marque "Permitir sobrante de aves" para registrar de más.';
        return;
      }
      if (!this.puedeVenderLotesCerrados && withQty.some((l) => l.bloqueada)) {
        this.error =
          'Hay cantidades cargadas en lotes cerrados o de una corrida anterior en el mismo galpón. Quite esas cantidades o solicite el permiso correspondiente.';
        return;
      }
      this.error = null;
      const sobranteMsg = this.permitirSobrante && this.exceedsVentaGranjaLine
        ? ' Se registrará el excedente como SOBRANTE de aves en el lote.'
        : '';
      this.confirmModalData = {
        title: 'Confirmar venta por granja',
        message: `Se registrarán ${this.formatearNumero(withQty.length)} movimiento(s) de venta (uno por lote). El mismo despacho y datos de transporte aplican a todos.${sobranteMsg}`,
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
    return crearVentaGranjaDto(this.form.getRawValue() as MovimientoModalFormValue, {
      ventaLineasGranja: this.ventaLineasGranja,
      lotesVentaGranja: this.lotesVentaGranja,
      permitirSobrante: this.permitirSobrante,
      usuarioMovimientoId
    });
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
    this.movimientoSvc.postAvesDisponiblesLotes({ tipoLote: 'LoteAveEngorde', loteIds }).subscribe({
      next: (resp) => {
        const byId = new Map<number, AvesDisponiblesVentaLoteDto | null>();
        for (const row of resp.items ?? []) byId.set(row.loteId, row.disponibles);
        this.ventaLineasGranja = lotes.map((l) => {
          const r = byId.get(l.loteAveEngordeId);
          const maxH = r?.hembrasDisponibles ?? l.hembrasL ?? 0;
          const maxM = r?.machosDisponibles ?? l.machosL ?? 0;
          const maxX = r?.mixtasDisponibles ?? l.mixtas ?? 0;
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
            xStr: '',
            flashExcesoH: false,
            flashExcesoM: false,
            flashExcesoX: false
          };
        });
        this.ventaLineasGranja = marcarLotesBloqueadosVenta(this.ventaLineasGranja, lotes);
        this.loadingVentaLineas = false;
        this.rebuildGruposVentaPorGalpon();
        this.configureTotalPollosGalponControl();
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
   * Cantidad por lote (venta granja): lee el valor real del input, limita al máximo disponible
   * y mantiene modelo + DOM alineados (evita quedar en 4710 cuando el tope es 471).
   */
  onLineaCantidadInput(ev: Event, line: VentaLineaGranja, field: 'h' | 'm' | 'x'): void {
    const input = ev.target as HTMLInputElement;
    // Refuerzo del `disabled` del template: un lote cerrado / de corrida anterior no admite
    // cantidad salvo que el usuario tenga el permiso de bypass.
    if (line.bloqueada && !this.puedeVenderLotesCerrados) {
      input.value = field === 'h' ? line.hStr : field === 'm' ? line.mStr : line.xStr;
      return;
    }
    const digits = (input.value ?? '').replace(/\D/g, '');
    const max = field === 'h' ? line.maxH : field === 'm' ? line.maxM : line.maxX;
    const parsed = digits === '' ? 0 : parseInt(digits, 10) || 0;
    // R2: con sobrante permitido NO se limita al disponible (se podrá vender de más).
    const clamped = this.permitirSobrante ? parsed : Math.min(parsed, max);
    const exceeded = parsed > max;
    const nextStr = digits === '' ? '' : String(clamped);

    if (field === 'h') {
      line.h = clamped;
      line.hStr = nextStr;
      if (exceeded) {
        line.flashExcesoH = true;
        window.setTimeout(() => this.clearLineaFlash(line, 'h'), 900);
      }
    } else if (field === 'm') {
      line.m = clamped;
      line.mStr = nextStr;
      if (exceeded) {
        line.flashExcesoM = true;
        window.setTimeout(() => this.clearLineaFlash(line, 'm'), 900);
      }
    } else {
      line.x = clamped;
      line.xStr = nextStr;
      if (exceeded) {
        line.flashExcesoX = true;
        window.setTimeout(() => this.clearLineaFlash(line, 'x'), 900);
      }
    }

    if (input.value !== nextStr) {
      input.value = nextStr;
    }

    this.syncTotalPollosGalponVentaGranja();
    this.cdr.detectChanges();
  }

  private buildUpdateDto(): UpdateMovimientoPolloEngordeDto {
    return crearUpdateDto(this.form.getRawValue() as MovimientoModalFormValue);
  }

  private buildCreateDto(usuarioMovimientoId: number): CreateMovimientoPolloEngordeDto | null {
    return crearCreateDto(this.form.getRawValue() as MovimientoModalFormValue, {
      loteOrigenValue: this.loteOrigenValue!,
      isTipoVenta: this.isTipoVenta,
      usuarioMovimientoId
    });
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

  /** Líneas de venta granja con al menos una ave asignada. */
  get prorateoLineasActivas(): VentaLineaGranja[] {
    return this.ventaLineasGranja.filter(l => l.h + l.m + l.x > 0);
  }

  /** Distribución proporcional de pesos por línea (espejo del algoritmo backend con ajuste de residuo). */
  get prorateoPreview(): ProrateoRow[] {
    const v = this.form?.getRawValue();
    const pesoBruto = v?.pesoBruto != null && v.pesoBruto !== '' ? Number(v.pesoBruto) : null;
    const pesoTara = v?.pesoTara != null && v.pesoTara !== '' ? Number(v.pesoTara) : null;
    return calcularProrateoPreview(this.prorateoLineasActivas, pesoBruto, pesoTara);
  }

  /** Fila de totales para la tabla de prorrateo. */
  get prorateoTotales(): ProrateoTotales {
    return calcularProrateoTotales(this.prorateoPreview);
  }

  formatearNumero(n: number): string {
    return fmtNumero(n);
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
    return fmtFecha(iso);
  }

  get showDespachoEnDetalle(): boolean {
    return (this.editingMovimiento?.tipoMovimiento ?? '') === 'Venta';
  }
}

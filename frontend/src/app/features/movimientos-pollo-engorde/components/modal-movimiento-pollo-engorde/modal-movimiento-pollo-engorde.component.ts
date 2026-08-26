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
  AvesDisponiblesVentaLoteDto,
  AvesDisponiblesLotePorIdDto
} from '../../services/movimiento-pollo-engorde.service';
import { LoteAveEngordeDto, LoteEngordeService } from '../../../lote-engorde/services/lote-engorde.service';
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
import {
  formatearNumero as fmtNumero,
  fechaCorta as fmtFecha,
  fechaHoraCorta as fmtFechaHora
} from '../../funciones/formato.funcion';
import { marcarLotesBloqueadosVenta } from '../../funciones/detectar-lotes-bloqueados-venta.funcion';
import {
  filtrarLotesDestinoEngorde,
  construirOpcionesLoteDestino
} from '../../funciones/filtrar-lotes-destino.funcion';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../lote-levante/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { catchError, of } from 'rxjs';
import {
  extremosVentanaRegistro,
  hintVentanaFechaRegistro,
  PERMISO_FECHA_RETROACTIVA
} from '../../../../shared/utils/fecha/ventana-fecha-registro.funcion';

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
  /**
   * Traslado de aves: el ORIGEN se elige dentro del modal (lotes abiertos de la granja filtrada) y el
   * DESTINO por la cascada Granja → Núcleo → Galpón → Lote, que puede apuntar a otra granja/galpón.
   */
  @Input() trasladoMode = false;
  @Input() lotesOrigenTraslado: LoteAveEngordeDto[] = [];

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
    if (this.trasladoMode) return 'Nuevo traslado de aves';
    if (this.ventaPorGranjaMode) return 'Nueva venta por granja (despacho)';
    return 'Nueva venta de Pollo Engorde';
  }

  /** Etiqueta del lote origen en el select del traslado: "Galpón · Lote". */
  etiquetaLoteOrigen(l: LoteAveEngordeDto): string {
    const galpon = (l.galpon?.galponNombre ?? l.galponId ?? '').toString().trim();
    const lote = (l.loteNombre || `Lote ${l.loteAveEngordeId}`).trim();
    return galpon ? `${galpon} · ${lote}` : lote;
  }

  /** Lote origen efectivo: en traslado lo elige el usuario en el modal; si no, viene del @Input. */
  get loteOrigenEfectivo(): string | null {
    if (this.trasladoMode && !this.editingMovimiento) {
      const v = this.form?.getRawValue()?.loteOrigenValue;
      return v ? String(v) : null;
    }
    return this.loteOrigenValue;
  }

  /**
   * Disponibilidad del lote origen elegido en modo traslado (mismo endpoint que la venta por granja,
   * `aves-disponibles-lotes`, para que el tope sea EL MISMO número que usa el backend al validar).
   */
  availableTrasladoOrigen: AvailableBirds | null = null;
  cargandoDisponibleOrigen = false;

  /** Al elegir el lote origen del traslado: pedir su disponibilidad y limpiar cantidades. */
  onLoteOrigenTrasladoChange(): void {
    this.form.patchValue({ cantidadHembras: 0, cantidadMachos: 0, cantidadMixtas: 0 });
    this.availableTrasladoOrigen = null;
    // El lote origen cambió ⇒ hay que reevaluar la auto-exclusión en la lista de destinos.
    this.refrescarOpcionesDestino();

    const value = this.form.getRawValue()?.loteOrigenValue as string | null;
    if (!value || !value.startsWith('ae-')) return;
    const loteId = Number(value.replace('ae-', ''));
    if (isNaN(loteId)) return;

    this.cargandoDisponibleOrigen = true;
    this.movimientoSvc
      .postAvesDisponiblesLotes({ tipoLote: 'LoteAveEngorde', loteIds: [loteId] })
      .pipe(catchError(() => of({ items: [] as AvesDisponiblesLotePorIdDto[] })))
      .subscribe((resp) => {
        const d = (resp.items ?? [])[0]?.disponibles ?? null;
        this.availableTrasladoOrigen = d
          ? {
              total: d.totalDisponibles ?? 0,
              hembras: d.hembrasDisponibles ?? 0,
              machos: d.machosDisponibles ?? 0,
              mixtas: d.mixtasDisponibles ?? 0
            }
          : null;
        this.cargandoDisponibleOrigen = false;
        this.cdr.detectChanges();
      });
  }

  /**
   * Opciones de lote destino de la cascada (Granja → Núcleo → Galpón).
   * Referencia ESTABLE: se recalcula solo al mover la cascada, nunca por getter en cada ciclo de CD
   * (un getter que aloca array nuevo por ciclo rompe el change detection del `@for`).
   */
  destinoOpciones: LoteDestinoOption[] = [];

  /** True si el tipo de movimiento es Venta (las aves salen a comprador externo; destino interno suele no aplicar). */
  get isTipoVenta(): boolean {
    return (this.form?.getRawValue()?.tipoMovimiento ?? '') === 'Venta';
  }

  /**
   * La cascada de destino es editable solo al CREAR: `UpdateMovimientoPolloEngordeDto` no lleva campos
   * de destino (cambiarlo en un movimiento ya completado exigiría revertir y reaplicar el stock), así
   * que al editar se muestra el destino como texto en vez de un select que no guardaría nada.
   */
  get mostrarCascadaDestino(): boolean {
    return !this.editingMovimiento && !this.ventaPorGranjaMode;
  }

  /** Destino legible de un movimiento ya registrado (granja · lote). */
  get destinoLabel(): string {
    const m = this.editingMovimiento;
    if (!m) return '—';
    const partes = [m.granjaDestinoNombre, m.loteDestinoNombre].filter((x) => !!x && String(x).trim());
    return partes.length ? partes.join(' · ') : '—';
  }

  /** Disponibilidad efectiva: la del origen elegido en traslado, o la que envía la pantalla. */
  private get disponiblesEfectivos(): AvailableBirds | null {
    if (this.trasladoMode && !this.editingMovimiento) return this.availableTrasladoOrigen;
    return this.availableBirds;
  }

  get availableTotal(): number {
    return this.disponiblesEfectivos?.total ?? 0;
  }

  get maxHembras(): number | null {
    return this.disponiblesEfectivos?.hembras ?? null;
  }

  get maxMachos(): number | null {
    return this.disponiblesEfectivos?.machos ?? null;
  }

  get maxMixtas(): number | null {
    return this.disponiblesEfectivos?.mixtas ?? null;
  }

  /** True si el total a mover supera lo disponible (solo aplica al crear con availableBirds o líneas venta granja). */
  get exceedsAvailable(): boolean {
    if (this.editingMovimiento) return false;
    if (this.ventaPorGranjaMode) return this.exceedsVentaGranjaLine;
    const disp = this.disponiblesEfectivos;
    if (!disp) return false;
    return this.totalAves > disp.total;
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

  // ── Cascada de DESTINO del traslado (Granja → Núcleo → Galpón → Lote) ──────────────
  //   Permite trasladar a OTRA granja / OTRO galpón, no solo dentro de la granja filtrada en la
  //   pantalla. Mismo contrato que el modal de traslado de postura: granjas = todas las de la
  //   empresa activa; núcleos/galpones/lotes con `paraDestino=true` (omiten el alcance granular).
  granjasDestino: FarmDto[] = [];
  nucleosDestino: NucleoDto[] = [];
  galponesDestino: GalponDetailDto[] = [];
  /** Catálogo completo de lotes destino (se filtra en cliente al mover la cascada). */
  private catalogoLotesDestino: LoteAveEngordeDto[] = [];
  /** True mientras se carga el catálogo de destino (granjas + lotes). */
  cargandoDestino = false;
  /** El catálogo se pide UNA vez por apertura y solo si el movimiento es un traslado. */
  private destinoCatalogoCargado = false;

  constructor(
    private fb: FormBuilder,
    private movimientoSvc: MovimientoPolloEngordeService,
    private tokenStorage: TokenStorageService,
    private permService: UserPermissionService,
    private companyConfig: ActiveCompanyConfigService,
    private farmSvc: FarmService,
    private nucleoSvc: NucleoService,
    private galponSvc: GalponService,
    private loteEngordeSvc: LoteEngordeService,
    private cdr: ChangeDetectorRef
  ) {
    this.buildForm();
  }

  /**
   * Empresa con báscula diferida (`venta_engorde_peso_diferido`): sus ventas pueden existir sin
   * peso, así que este modal NO debe exigirlo — si no, el usuario no podría ni siquiera EDITAR la
   * venta que acaba de registrar. Fail-closed: arranca apagado ⇒ peso obligatorio.
   */
  pesoDiferido = false;

  /**
   * ¿El usuario tiene el permiso de bypass?
   *
   * ⚠️ Destraba SOLO las líneas de **corrida anterior** (`bypassablePorPermiso`). Sobre un lote
   * **liquidado** no tiene ningún efecto: lo rechaza el gate de liquidación congelada del backend
   * (`LiquidacionCongeladaGateCalculos.ValidarEscritura` → 400), que no consulta este permiso. Para
   * decidir si una fila admite cantidad usar {@link lineaBloqueadaEfectiva}, nunca este getter solo.
   */
  get puedeVenderLotesCerrados(): boolean {
    return this.permService.has(PERMISO_VENDER_LOTES_CERRADOS);
  }

  /** ¿Esta línea queda bloqueada de verdad, ya considerado el permiso? Única fuente para la UI. */
  lineaBloqueadaEfectiva(line: VentaLineaGranja): boolean {
    if (!line.bloqueada) return false;
    return !(line.bypassablePorPermiso && this.puedeVenderLotesCerrados);
  }

  /**
   * Hay líneas de **corrida anterior** bloqueadas que el permiso destrabaría, y el usuario no lo
   * tiene. Las de lote cerrado quedan fuera a propósito: no las destraba ningún permiso, así que
   * ofrecerlo sería mandar al usuario a pedir algo que no le va a servir.
   */
  get hayLoteBloqueadoSinPermiso(): boolean {
    if (this.puedeVenderLotesCerrados) return false;
    return this.ventaLineasGranja.some((l) => l.bloqueada && l.bypassablePorPermiso);
  }

  /** Permiso `registros.fecha_retroactiva`: destraba `fechaMovimiento` más allá de la ventana base. */
  private get puedeRetroactivar(): boolean {
    return this.permService.has(PERMISO_FECHA_RETROACTIVA);
  }

  get fechaMovimientoMin(): string | null {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).min;
  }

  get fechaMovimientoMax(): string {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).max;
  }

  get fechaMovimientoHint(): string {
    return hintVentanaFechaRegistro(new Date(), this.puedeRetroactivar);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && !this.isOpen) {
      this.ventaLineasGranja = [];
      this.loadingVentaLineas = false;
    }
    if (changes['isOpen'] && this.isOpen) {
      this.error = null;
      this.resolverPesoDiferido();
      this.fechaMovimientoSub?.unsubscribe();
      if (this.editingMovimiento) {
        this.loadFormFromMovimiento(this.editingMovimiento);
        this.form.get('raza')?.enable();
        this.form.get('edadAves')?.enable();
        this.configureTotalPollosGalponControl();
      } else {
        this.resetForm();
        this.availableTrasladoOrigen = null;
        if (this.trasladoMode) {
          // El traslado no pasa por báscula y no admite otro tipo: se fija y se bloquea.
          this.form.patchValue({ tipoMovimiento: 'Traslado' });
          this.form.get('tipoMovimiento')?.disable({ emitEvent: false });
          this.cargarCatalogoDestinoSiHaceFalta();
        } else if (this.ventaPorGranjaMode) {
          this.form.patchValue({ tipoMovimiento: 'Venta' });
          this.form.get('tipoMovimiento')?.disable({ emitEvent: false });
          this.applyRazaFromVentaGranja();
          this.loadVentaGranjaLineas();
        } else {
          this.form.get('tipoMovimiento')?.enable({ emitEvent: false });
          this.applyLotInfoToForm();
          this.subscribeFechaMovimientoForEdad();
          this.configureTotalPollosGalponControl();
          // Si el usuario abre directamente en un tipo que no es Venta (o vuelve a Traslado), el
          // catálogo de destino se pide acá; el `valueChanges` cubre el cambio posterior de tipo.
          if (!this.isTipoVenta) this.cargarCatalogoDestinoSiHaceFalta();
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
      // Destino del traslado: cascada Granja → Núcleo → Galpón → Lote (los tres primeros son
      // opcionales para el backend; el lote sigue siendo opcional como siempre).
      // Lote origen: solo se usa en modo traslado (en los demás flujos viene por @Input).
      loteOrigenValue: [null as string | null],
      granjaDestinoId: [null as number | null],
      nucleoDestinoId: [null as string | null],
      galponDestinoId: [null as string | null],
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
    // El catálogo de destino se pide recién al pasar a un tipo que lo usa (la venta, que es el
    // grueso del uso, no paga la petición).
    this.form.get('tipoMovimiento')?.valueChanges.subscribe(() => {
      this.syncPesoValidators();
      if (!this.isTipoVenta) this.cargarCatalogoDestinoSiHaceFalta();
    });
    this.syncPesoValidators();
  }

  // ── Cascada de destino ────────────────────────────────────────────────────────────

  /**
   * Carga (una sola vez por apertura) las granjas y el catálogo de lotes destino.
   * Fail-closed: si alguna petición falla, la lista queda vacía y el traslado no se puede apuntar a
   * ningún lote — nunca se cae al comportamiento viejo de ofrecer lotes de la granja filtrada.
   */
  private cargarCatalogoDestinoSiHaceFalta(): void {
    if (!this.mostrarCascadaDestino || this.isReadOnly) return;
    if (this.destinoCatalogoCargado || this.cargandoDestino) return;
    this.destinoCatalogoCargado = true;
    this.cargandoDestino = true;

    let huboError = false;

    this.farmSvc
      .getForTrasladoSeguimiento()
      .pipe(
        catchError(() => {
          huboError = true;
          return of<FarmDto[]>([]);
        })
      )
      .subscribe((farms) => {
        this.granjasDestino = farms;
        this.cdr.detectChanges();
      });

    // paraDestino=true: catálogo de lotes DESTINO — omite el alcance granular núcleo/galpón
    this.loteEngordeSvc
      .getAll(true)
      .pipe(
        catchError(() => {
          huboError = true;
          return of<LoteAveEngordeDto[]>([]);
        })
      )
      .subscribe((lotes) => {
        this.catalogoLotesDestino = lotes;
        this.cargandoDestino = false;
        // Fail-closed pero reintentable: la lista queda vacía (no se ofrece un destino que no se pudo
        // verificar), y al volver a elegir "Traslado" se pide el catálogo de nuevo.
        if (huboError) this.destinoCatalogoCargado = false;
        this.refrescarOpcionesDestino();
        this.cdr.detectChanges();
      });
  }

  onGranjaDestinoChange(): void {
    this.form.patchValue({ nucleoDestinoId: null, galponDestinoId: null, loteDestinoValue: null });
    this.nucleosDestino = [];
    this.galponesDestino = [];
    this.refrescarOpcionesDestino();

    const granjaId = this.form.get('granjaDestinoId')?.value;
    if (granjaId == null) return;

    // paraDestino=true: cascada de DESTINO — no se restringe por el alcance granular del usuario
    this.nucleoSvc
      .getByGranja(Number(granjaId), true)
      .pipe(catchError(() => of<NucleoDto[]>([])))
      .subscribe((ns) => {
        this.nucleosDestino = ns;
        this.cdr.detectChanges();
      });
  }

  onNucleoDestinoChange(): void {
    this.form.patchValue({ galponDestinoId: null, loteDestinoValue: null });
    this.galponesDestino = [];
    this.refrescarOpcionesDestino();

    const granjaId = this.form.get('granjaDestinoId')?.value;
    const nucleoId = this.form.get('nucleoDestinoId')?.value;
    if (granjaId == null || !nucleoId) return;

    this.galponSvc
      .getByGranjaAndNucleo(Number(granjaId), String(nucleoId), true)
      .pipe(catchError(() => of<GalponDetailDto[]>([])))
      .subscribe((gs) => {
        this.galponesDestino = gs;
        this.cdr.detectChanges();
      });
  }

  onGalponDestinoChange(): void {
    this.form.patchValue({ loteDestinoValue: null });
    this.refrescarOpcionesDestino();
  }

  /** Recalcula las opciones de lote destino desde el catálogo + la ubicación elegida (referencia nueva SOLO aquí). */
  private refrescarOpcionesDestino(): void {
    const v = this.form.getRawValue();
    const lotes = filtrarLotesDestinoEngorde(
      this.catalogoLotesDestino,
      {
        granjaId: v.granjaDestinoId != null ? Number(v.granjaDestinoId) : null,
        nucleoId: v.nucleoDestinoId ?? null,
        galponId: v.galponDestinoId ?? null
      },
      this.loteOrigenEfectivo
    );
    this.destinoOpciones = construirOpcionesLoteDestino(lotes);
  }

  /** Resuelve el flag de la empresa activa y re-aplica los validadores de peso. */
  private resolverPesoDiferido(): void {
    this.companyConfig.ventaEngordePesoDiferido().subscribe({
      next: (activo) => {
        this.pesoDiferido = activo;
        this.syncPesoValidators();
        this.cdr.detectChanges();
      },
      error: () => {
        this.pesoDiferido = false;
        this.syncPesoValidators();
        this.cdr.detectChanges();
      }
    });
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
    if (this.isDespacho && this.pesoDiferido) {
      // Báscula diferida: el peso es opcional (se carga al confirmar), pero si se digita se
      // valida igual que siempre.
      bruto.setValidators([Validators.min(0.01)]);
      tara.setValidators([Validators.min(0)]);
    } else if (this.isDespacho) {
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
      loteOrigenValue: null,
      granjaDestinoId: null,
      nucleoDestinoId: null,
      galponDestinoId: null,
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
    // Sin granja destino elegida no hay lotes candidatos; además el lote ORIGEN pudo cambiar entre
    // aperturas, así que las opciones se reconstruyen desde cero.
    this.nucleosDestino = [];
    this.galponesDestino = [];
    this.refrescarOpcionesDestino();
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
      granjaDestinoId: m.granjaDestinoId ?? null,
      nucleoDestinoId: null,
      galponDestinoId: null,
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
      // El mensaje distingue las dos causas: la corrida anterior se destraba con el permiso, el lote
      // cerrado NO (hay que reabrirlo). Mandar a pedir un permiso que no sirve fue el defecto previo.
      const bloqueadas = withQty.filter((l) => this.lineaBloqueadaEfectiva(l));
      if (bloqueadas.length > 0) {
        this.error = bloqueadas.some((l) => !l.bypassablePorPermiso)
          ? 'Hay cantidades cargadas en lotes cerrados (liquidados). Quite esas cantidades o reabra el lote: ningún permiso habilita escribir sobre un lote liquidado.'
          : 'Hay cantidades cargadas en lotes de una corrida anterior en el mismo galpón. Quite esas cantidades o solicite el permiso correspondiente.';
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
    if (!this.loteOrigenEfectivo && !this.editingMovimiento) {
      this.error = 'Seleccione el lote de origen del traslado.';
      return;
    }
    if (this.trasladoMode && !this.editingMovimiento && !this.form.getRawValue()?.loteDestinoValue) {
      this.error = 'Seleccione el lote de destino: sin destino las aves saldrían del origen sin entrar a ningún lote.';
      return;
    }
    if (this.totalAves <= 0) {
      this.error = 'Indique cuántas aves se trasladan.';
      return;
    }
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
          this.error = err?.error?.message ?? err?.error?.error ?? err?.message ?? 'Error al actualizar.';
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
        this.error = err?.error?.message ?? err?.error?.error ?? err?.message ?? 'Error al guardar.';
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
      const httpErr = err as { error?: { message?: string; error?: string }; message?: string };
      this.error = httpErr?.error?.message ?? httpErr?.error?.error ?? httpErr?.message ?? 'Error al guardar.';
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
    // Refuerzo del `disabled` del template: un lote de corrida anterior no admite cantidad salvo que
    // el usuario tenga el permiso de bypass; uno cerrado no la admite nunca.
    if (this.lineaBloqueadaEfectiva(line)) {
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
      loteOrigenValue: this.loteOrigenEfectivo!,
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

  /** Fecha + hora de creación del registro (`created_at`): cuándo se cargó en el sistema. */
  fechaHoraCorta(iso: string | null | undefined): string {
    return fmtFechaHora(iso);
  }

  get showDespachoEnDetalle(): boolean {
    return (this.editingMovimiento?.tipoMovimiento ?? '') === 'Venta';
  }
}

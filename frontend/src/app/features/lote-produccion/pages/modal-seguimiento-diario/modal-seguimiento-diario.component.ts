import { Component, Input, Output, EventEmitter, OnInit, OnChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { CrearSeguimientoRequest, SeguimientoItemDto } from '../../services/produccion.service';
import { CatalogoAlimentosService, CatalogItemDto, CatalogItemType } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { InventarioService, FarmInventoryDto } from '../../../inventario/services/inventario.service';
import { GestionInventarioService, ItemInventarioDto, InventarioGestionStockDto } from '../../../gestion-inventario/services/gestion-inventario.service';
import { EMPTY, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';

// Interfaz extendida localmente para incluir tipoItem y unidad
interface CatalogItemExtended extends CatalogItemDto {
  tipoItem?: string;
  unidad?: string;
}

/** Metadata del seguimiento (puede venir como objeto o JSON string; soporta camelCase y snake_case). */
export interface MetadataSeguimientoNormalizada {
  itemsHembras: Array<{ tipoItem: string; catalogItemId: number; itemInventarioEcuadorId?: number; cantidad: number; unidad: string }>;
  itemsMachos: Array<{ tipoItem: string; catalogItemId: number; itemInventarioEcuadorId?: number; cantidad: number; unidad: string }>;
  consumoOriginalHembras?: number;
  unidadConsumoOriginalHembras?: string;
  consumoOriginalMachos?: number;
  unidadConsumoOriginalMachos?: string;
  tipoItemHembras?: string | null;
  tipoItemMachos?: string | null;
  tipoAlimentoHembras?: number | null;
  tipoAlimentoMachos?: number | null;
  [key: string]: unknown;
}

@Component({
  selector: 'app-modal-seguimiento-diario',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ConfirmationModalComponent],
  templateUrl: './modal-seguimiento-diario.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./modal-seguimiento-diario.component.scss']
})
export class ModalSeguimientoDiarioComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() produccionLoteId: number | null = null;
  /** ID del lote postura producción (flujo LPP). Usar solo uno: produccionLoteId o lotePosturaProduccionId. */
  @Input() lotePosturaProduccionId: number | null = null;
  @Input() editingSeguimiento: SeguimientoItemDto | null = null;
  @Input() loading: boolean = false;
  @Input() fechaEncaset: string | Date | null = null; // Fecha de encaset para calcular etapa
  @Input() granjaId: number | null = null; // ID de la granja para cargar inventario
  /** Ecuador/Panamá: núcleo para inventario-gestion (obligatorio para alimento). */
  @Input() nucleoId: string | null = null;
  /** Ecuador/Panamá: galpón para inventario-gestion (obligatorio para alimento). */
  @Input() galponId: string | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<CrearSeguimientoRequest>();
  @Output() saveSuccess = new EventEmitter<void>();
  @Output() saveError = new EventEmitter<string>();

  // Formulario
  form!: FormGroup;

  // Catálogo de alimentos (desde inventario de la granja)
  alimentosCatalog: CatalogItemExtended[] = [];
  alimentosFiltradosHembras: CatalogItemExtended[] = [];
  alimentosFiltradosMachos: CatalogItemExtended[] = [];
  private alimentosByCode = new Map<string, CatalogItemExtended>();
  private alimentosById = new Map<number, CatalogItemExtended>();
  private alimentosByName = new Map<string, CatalogItemExtended>();
  private inventarioPorItem = new Map<number, { quantity: number; unit: string }>();

  // Tipos de ítem (fijos para no-EC/PA; dinámicos conceptos para Ecuador/Panamá)
  tiposItem: string[] = ['alimento', 'medicamento', 'accesorio', 'biologico', 'consumible', 'otro'];
  /** Ecuador/Panamá: catálogo item_inventario_ecuador y conceptos únicos. */
  itemsEcuadorPanama: ItemInventarioDto[] = [];
  conceptosEcuadorPanama: string[] = [];

  // Inventario
  inventarioDisponibleHembras: number | null = null;
  inventarioDisponibleMachos: number | null = null;
  inventarioUnidadHembras: string = 'kg';
  inventarioUnidadMachos: string = 'kg';
  inventarioCantidadOriginalHembras: number | null = null;
  inventarioCantidadOriginalMachos: number | null = null;
  cargandoInventarioHembras = false;
  cargandoInventarioMachos = false;
  mensajeInventarioHembras: string = '';
  mensajeInventarioMachos: string = '';
  
  // Flag para mostrar tab de agua (solo Ecuador y Panamá)
  isEcuadorOrPanama: boolean = false;
  /** Colombia opera el inventario unificado (modelo B) a NIVEL GRANJA. */
  isColombia: boolean = false;
  /** Colombia + EC/PA leen el catálogo/stock desde item_inventario_ecuador (inventario-gestion). */
  get usaInventarioGestion(): boolean { return this.isEcuadorOrPanama || this.isColombia; }
  /** Colombia: codigo(normalizado) → catalogo_items.id (contrato de ids al descontar). */
  private catalogItemIdPorCodigo = new Map<string, number>();

  // Modal de mensaje
  showMessageModal = false;
  messageModalData: ConfirmationModalData = {
    title: '',
    message: '',
    type: 'info',
    confirmText: 'Aceptar',
    showCancel: false
  };

  constructor(
    private fb: FormBuilder,
    private catalogSvc: CatalogoAlimentosService,
    private inventarioSvc: InventarioService,
    private gestionInventarioSvc: GestionInventarioService,
    private countryFilter: CountryFilterService,
    private storage: TokenStorageService
  ) { }

  ngOnInit(): void {
    this.initializeForm();
    this.checkCountry();
  }
  
  private checkCountry(): void {
    this.isEcuadorOrPanama = this.countryFilter.isEcuadorOrPanama();
    this.isColombia = this.countryFilter.isColombia();
  }

  ngOnChanges(): void {
    if (this.isOpen && this.form) {
      if (this.lotePosturaProduccionId != null) {
        this.form.patchValue({ lotePosturaProduccionId: this.lotePosturaProduccionId, produccionLoteId: null });
        this.form.get('produccionLoteId')?.clearValidators();
      } else if (this.produccionLoteId) {
        this.form.patchValue({ produccionLoteId: this.produccionLoteId, lotePosturaProduccionId: null });
      }
    }

    if (this.isOpen) {
      if (this.usaInventarioGestion && this.granjaId) {
        this.cargarCatalogEcuadorPanama();
      } else if (this.granjaId) {
        this.cargarInventarioGranja(this.granjaId);
      }

      if (this.editingSeguimiento) {
        // Ejecutar en el siguiente tick para que el form esté en el DOM (*ngIf="isOpen") y los valores se apliquen correctamente
        setTimeout(() => this.populateForm(), 0);
      } else {
        this.resetForm();
      }
    }
  }

  // ================== FORMULARIO ==================
  private initializeForm(): void {
    this.form = this.fb.group({
      fechaRegistro: [this.todayYMD(), Validators.required],
      produccionLoteId: [null],
      lotePosturaProduccionId: [null],
      mortalidadH: [0, [Validators.required, Validators.min(0)]],
      mortalidadM: [0, [Validators.required, Validators.min(0)]],
      selH: [0, [Validators.required, Validators.min(0)]],
      selM: [0, [Validators.required, Validators.min(0)]],
      errorSexajeHembras: [0, [Validators.min(0)]],
      errorSexajeMachos: [0, [Validators.min(0)]],
      ciclo: ['Normal'],
      // Múltiples ítems por hembras/machos (como en levante)
      itemsHembras: this.fb.array([]),
      itemsMachos: this.fb.array([]),
      // Compatibilidad: tipo/consumo único (si no hay ítems)
      tipoItemHembras: [null],
      tipoItemMachos: [null],
      tipoAlimentoHembras: [null],
      tipoAlimentoMachos: [null],
      consumoHembras: [0, [Validators.min(0)]],
      unidadConsumoHembras: ['kg'],
      consumoMachos: [0, [Validators.min(0)]],
      unidadConsumoMachos: ['kg'],
      huevosTotales: [0, [Validators.required, Validators.min(0)]],
      huevosIncubables: [0, [Validators.required, Validators.min(0)]],
      // Campos de Clasificadora de Huevos - (Limpio, Tratado) = HuevoInc +
      huevoLimpio: [0, [Validators.min(0)]],
      huevoTratado: [0, [Validators.min(0)]],
      // Campos de Clasificadora de Huevos - (Sucio, Deforme, Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, Otro) = Huevo Total
      huevoSucio: [0, [Validators.min(0)]],
      huevoDeforme: [0, [Validators.min(0)]],
      huevoBlanco: [0, [Validators.min(0)]],
      huevoDobleYema: [0, [Validators.min(0)]],
      huevoPiso: [0, [Validators.min(0)]],
      huevoPequeno: [0, [Validators.min(0)]],
      huevoRoto: [0, [Validators.min(0)]],
      huevoDesecho: [0, [Validators.min(0)]],
      huevoOtro: [0, [Validators.min(0)]],
      tipoAlimento: ['Standard', Validators.required],
      pesoHuevo: [0, [Validators.required, Validators.min(0)]],
      etapa: [1, [Validators.required, Validators.min(1), Validators.max(3)]],
      observaciones: [''],
      // Campos de Pesaje Semanal / por sexo (alineados con seguimiento_diario)
      pesoH: [null, [Validators.min(0)]],
      pesoM: [null, [Validators.min(0)]],
      uniformidad: [null, [Validators.min(0), Validators.max(100)]],
      coeficienteVariacion: [null, [Validators.min(0), Validators.max(100)]],
      uniformidadHembras: [null, [Validators.min(0), Validators.max(100)]],
      uniformidadMachos: [null, [Validators.min(0), Validators.max(100)]],
      cvHembras: [null, [Validators.min(0), Validators.max(100)]],
      cvMachos: [null, [Validators.min(0), Validators.max(100)]],
      observacionesPesaje: [''],
      // Campos de agua (solo para Ecuador y Panamá)
      consumoAguaDiario: [null, [Validators.min(0)]],
      consumoAguaPh: [null, [Validators.min(0)]],
      consumoAguaOrp: [null, [Validators.min(0)]],
      consumoAguaTemperatura: [null, [Validators.min(0)]]
    });

    // Huevos Totales/Incubables son calculados automáticamente desde clasificadora.
    this.form.get('huevosTotales')?.disable({ emitEvent: false });
    this.form.get('huevosIncubables')?.disable({ emitEvent: false });
    this.setupHuevosAutoCalculo();

    // Calcular etapa automáticamente cuando cambia la fecha
    this.form.get('fechaRegistro')?.valueChanges.subscribe(() => {
      this.calcularYActualizarEtapa();
    });

    // Suscribirse a cambios en tipoItem para recargar inventario filtrado desde el backend
    this.form.get('tipoItemHembras')?.valueChanges.subscribe(tipo => {
      if (this.granjaId) {
        this.cargarInventarioGranja(this.granjaId, 'hembras', tipo);
      }
      this.form.patchValue({ tipoAlimentoHembras: null }, { emitEvent: false });
      this.inventarioDisponibleHembras = null;
      this.mensajeInventarioHembras = '';
    });

    this.form.get('tipoItemMachos')?.valueChanges.subscribe(tipo => {
      if (this.granjaId) {
        this.cargarInventarioGranja(this.granjaId, 'machos', tipo);
      }
      this.form.patchValue({ tipoAlimentoMachos: null }, { emitEvent: false });
      this.inventarioDisponibleMachos = null;
      this.mensajeInventarioMachos = '';
    });

    // Consultar inventario cuando se selecciona un alimento
    this.form.get('tipoAlimentoHembras')?.valueChanges.subscribe(id => {
      if (id && this.granjaId) {
        this.consultarInventario('hembras', id);
      } else {
        this.inventarioDisponibleHembras = null;
        this.mensajeInventarioHembras = '';
      }
    });

    this.form.get('tipoAlimentoMachos')?.valueChanges.subscribe(id => {
      if (id && this.granjaId) {
        this.consultarInventario('machos', id);
      } else {
        this.inventarioDisponibleMachos = null;
        this.mensajeInventarioMachos = '';
      }
    });
  }

  get itemsHembrasArray(): FormArray {
    return this.form.get('itemsHembras') as FormArray;
  }

  get itemsMachosArray(): FormArray {
    return this.form.get('itemsMachos') as FormArray;
  }

  /**
   * Crea un grupo de ítem. `esFijo` = el ítem de ALIMENTO pre-cargado y NO removible.
   * En el fijo los campos son opcionales (no bloquean el guardado si ese día no hubo consumo;
   * el save ya filtra ítems sin producto seleccionado). El control `esFijo` no se guarda.
   */
  private crearItemGroup(tipoInicial: string | null = null, esFijo = false): FormGroup {
    const grp = this.fb.group({
      tipoItem: [tipoInicial, esFijo ? [] : [Validators.required]],
      catalogItemId: [null, esFijo ? [] : [Validators.required]],
      cantidad: [0, esFijo ? [Validators.min(0)] : [Validators.required, Validators.min(0)]],
      unidad: ['kg', Validators.required],
      esFijo: [esFijo]
    });
    grp.get('tipoItem')?.valueChanges.subscribe(tipo => {
      if (tipo === 'alimento') grp.patchValue({ unidad: 'kg' }, { emitEvent: false });
      else if (tipo) grp.patchValue({ unidad: 'unidades' }, { emitEvent: false });
      grp.patchValue({ catalogItemId: null }, { emitEvent: false });
    });
    return grp;
  }

  /** Garantiza que el ítem de ALIMENTO esté siempre presente (fijo, no removible) y al inicio de la lista. */
  private asegurarAlimentoFijo(array: FormArray): void {
    const idx = array.controls.findIndex(c => c.get('tipoItem')?.value === 'alimento');
    if (idx === -1) { array.insert(0, this.crearItemGroup('alimento', true)); return; }
    const grp = array.at(idx) as FormGroup;
    if (grp.get('esFijo')) grp.get('esFijo')!.setValue(true);
    else grp.addControl('esFijo', this.fb.control(true));
    if (idx !== 0) { array.removeAt(idx); array.insert(0, grp); }
  }

  agregarItemHembras(): void {
    this.itemsHembrasArray.push(this.crearItemGroup());
  }

  eliminarItemHembras(index: number): void {
    if (this.itemsHembrasArray.at(index)?.get('esFijo')?.value) return; // el alimento fijo no se elimina
    this.itemsHembrasArray.removeAt(index);
  }

  agregarItemMachos(): void {
    this.itemsMachosArray.push(this.crearItemGroup());
  }

  eliminarItemMachos(index: number): void {
    if (this.itemsMachosArray.at(index)?.get('esFijo')?.value) return;
    this.itemsMachosArray.removeAt(index);
  }

  /**
   * Caché de referencias estables por tipoItem. Este método se usa dentro de un *ngFor;
   * devolver un array nuevo en cada ciclo de detección de cambios provoca NG0103
   * (Infinite change detection). Se recalcula el contenido, pero si es igual (mismos ids
   * en el mismo orden) se devuelve la MISMA referencia previa para no romper el CD.
   */
  private readonly _alimentosFiltradosCache = new Map<string, CatalogItemExtended[]>();

  getAlimentosFiltradosPorTipo(tipoItem: string | null): CatalogItemExtended[] {
    const computed = this.computeAlimentosFiltradosPorTipo(tipoItem);
    const key = `${tipoItem ?? ''}`;
    const prev = this._alimentosFiltradosCache.get(key);
    if (prev && prev.length === computed.length && prev.every((a, i) => a.id === computed[i].id)) {
      return prev;
    }
    this._alimentosFiltradosCache.set(key, computed);
    return computed;
  }

  private computeAlimentosFiltradosPorTipo(tipoItem: string | null): CatalogItemExtended[] {
    if (this.usaInventarioGestion && this.itemsEcuadorPanama.length > 0) {
      const c = (tipoItem ?? '').trim().toLowerCase();
      if (!c) return this.itemsEcuadorPanama.map(i => this.itemEcuadorToExtended(i));
      return this.itemsEcuadorPanama
        .filter(i => ((i.concepto ?? i.tipoItem ?? '').trim().toLowerCase() === c))
        .map(i => this.itemEcuadorToExtended(i));
    }
    if (!tipoItem) return this.alimentosCatalog;
    return this.alimentosCatalog.filter(a => {
      const t = a.tipoItem || (a as any).metadata?.type_item || (a as any).metadata?.itemType;
      return t === tipoItem;
    });
  }

  private itemEcuadorToExtended(i: ItemInventarioDto): CatalogItemExtended {
    return {
      id: i.id,
      codigo: i.codigo,
      nombre: i.nombre,
      tipoItem: (i.concepto ?? i.tipoItem ?? '').trim() || i.tipoItem,
      unidad: (i.unidad ?? 'kg').trim() || 'kg',
      activo: i.activo,
      metadata: { type_item: i.tipoItem, concepto: i.concepto }
    };
  }

  getCantidadDisponible(catalogItemId: number | null | undefined): { quantity: number; unit: string } | null {
    if (!catalogItemId) return null;
    return this.inventarioPorItem.get(catalogItemId) ?? null;
  }

  getItemDisplayText(item: CatalogItemExtended): string {
    const cantidad = this.getCantidadDisponible(item.id ?? undefined);
    if (cantidad) return `${item.codigo} — ${item.nombre} (Disp.: ${cantidad.quantity.toFixed(2)} ${cantidad.unit})`;
    return `${item.codigo} — ${item.nombre}`;
  }

  /**
   * Obtiene la metadata del registro normalizada: si viene como string JSON la parsea,
   * mapea snake_case a camelCase y asegura itemsHembras/itemsMachos como arrays.
   */
  private getNormalizedMetadata(): MetadataSeguimientoNormalizada {
    const raw = this.editingSeguimiento?.metadata;
    let obj: Record<string, unknown> = {};
    if (typeof raw === 'string') {
      try {
        obj = JSON.parse(raw) as Record<string, unknown>;
      } catch {
        return this.emptyMetadata();
      }
    } else if (raw && typeof raw === 'object') {
      obj = { ...raw } as Record<string, unknown>;
    } else {
      return this.emptyMetadata();
    }
    const get = (key: string): unknown => obj[key] ?? obj[this.snakeCase(key)];
    const getNum = (key: string): number | undefined => {
      const v = get(key);
      if (v === null || v === undefined) return undefined;
      const n = Number(v);
      return isNaN(n) ? undefined : n;
    };
    const getStr = (key: string): string | undefined => {
      const v = get(key);
      return v != null ? String(v) : undefined;
    };
    const toItem = (x: unknown): MetadataSeguimientoNormalizada['itemsHembras'][0] => {
      if (x && typeof x === 'object') {
        const o = x as Record<string, unknown>;
        const catalogItemId = Number(o['catalogItemId'] ?? o['catalog_item_id'] ?? o['catalogItem_id']) || 0;
        const itemInventarioEcuadorId = Number(o['itemInventarioEcuadorId'] ?? o['item_inventario_ecuador_id']) || undefined;
        const id = itemInventarioEcuadorId || catalogItemId;
        const cantidad = Number(o['cantidad']) || 0;
        const unidad = String(o['unidad'] ?? 'kg').trim() || 'kg';
        const tipoItem = String(o['tipoItem'] ?? o['tipo_item'] ?? 'alimento').trim() || 'alimento';
        return { tipoItem, catalogItemId: id || 0, itemInventarioEcuadorId: itemInventarioEcuadorId || (this.isEcuadorOrPanama ? id : undefined), cantidad, unidad };
      }
      return { tipoItem: 'alimento', catalogItemId: 0, cantidad: 0, unidad: 'kg' };
    };
    const toItems = (arr: unknown): MetadataSeguimientoNormalizada['itemsHembras'] => {
      if (Array.isArray(arr)) return arr.map(toItem).filter(i => i.catalogItemId > 0 || i.cantidad > 0);
      return [];
    };
    const itemsH = toItems(get('itemsHembras') ?? get('items_hembras'));
    const itemsM = toItems(get('itemsMachos') ?? get('items_machos'));
    return {
      itemsHembras: itemsH,
      itemsMachos: itemsM,
      consumoOriginalHembras: getNum('consumoOriginalHembras') ?? getNum('consumo_original_hembras'),
      unidadConsumoOriginalHembras: getStr('unidadConsumoOriginalHembras') ?? getStr('unidad_consumo_original_hembras') ?? 'kg',
      consumoOriginalMachos: getNum('consumoOriginalMachos') ?? getNum('consumo_original_machos'),
      unidadConsumoOriginalMachos: getStr('unidadConsumoOriginalMachos') ?? getStr('unidad_consumo_original_machos') ?? 'kg',
      tipoItemHembras: getStr('tipoItemHembras') ?? getStr('tipo_item_hembras') ?? null,
      tipoItemMachos: getStr('tipoItemMachos') ?? getStr('tipo_item_machos') ?? null,
      tipoAlimentoHembras: getNum('tipoAlimentoHembras') ?? getNum('tipo_alimento_hembras') ?? null,
      tipoAlimentoMachos: getNum('tipoAlimentoMachos') ?? getNum('tipo_alimento_machos') ?? null,
      ...obj
    } as MetadataSeguimientoNormalizada;
  }

  private snakeCase(key: string): string {
    return key.replace(/([A-Z])/g, '_$1').toLowerCase().replace(/^_/, '');
  }

  private emptyMetadata(): MetadataSeguimientoNormalizada {
    return {
      itemsHembras: [],
      itemsMachos: [],
      consumoOriginalHembras: undefined,
      unidadConsumoOriginalHembras: 'kg',
      consumoOriginalMachos: undefined,
      unidadConsumoOriginalMachos: 'kg',
      tipoItemHembras: null,
      tipoItemMachos: null,
      tipoAlimentoHembras: null,
      tipoAlimentoMachos: null
    };
  }

  private resetForm(): void {
    while (this.itemsHembrasArray.length) this.itemsHembrasArray.removeAt(0);
    while (this.itemsMachosArray.length) this.itemsMachosArray.removeAt(0);
    const fechaHoy = this.todayYMD();
    this.form.reset({
      fechaRegistro: fechaHoy,
      produccionLoteId: this.lotePosturaProduccionId != null ? null : this.produccionLoteId,
      lotePosturaProduccionId: this.lotePosturaProduccionId,
      mortalidadH: 0,
      mortalidadM: 0,
      selH: 0,
      selM: 0,
      errorSexajeHembras: 0,
      errorSexajeMachos: 0,
      ciclo: 'Normal',
      itemsHembras: [],
      itemsMachos: [],
      consumoHembras: 0,
      unidadConsumoHembras: 'kg',
      consumoMachos: 0,
      unidadConsumoMachos: 'kg',
      tipoItemHembras: null,
      tipoItemMachos: null,
      tipoAlimentoHembras: null,
      tipoAlimentoMachos: null,
      huevosTotales: 0,
      huevosIncubables: 0,
      huevoLimpio: 0,
      huevoTratado: 0,
      huevoSucio: 0,
      huevoDeforme: 0,
      huevoBlanco: 0,
      huevoDobleYema: 0,
      huevoPiso: 0,
      huevoPequeno: 0,
      huevoRoto: 0,
      huevoDesecho: 0,
      huevoOtro: 0,
      tipoAlimento: 'Standard',
      pesoHuevo: 0,
      etapa: this.calcularEtapa(fechaHoy),
      observaciones: '',
      // Campos de Pesaje Semanal / por sexo
      pesoH: null,
      pesoM: null,
      uniformidad: null,
      coeficienteVariacion: null,
      uniformidadHembras: null,
      uniformidadMachos: null,
      cvHembras: null,
      cvMachos: null,
      observacionesPesaje: '',
      // Campos de agua (solo para Ecuador y Panamá)
      consumoAguaDiario: null,
      consumoAguaPh: null,
      consumoAguaOrp: null,
      consumoAguaTemperatura: null
    });
    // Alimento fijo pre-cargado (no removible) en Hembras y Machos — sin tener que "Agregar Ítem".
    this.asegurarAlimentoFijo(this.itemsHembrasArray);
    this.asegurarAlimentoFijo(this.itemsMachosArray);
  }

  private populateForm(): void {
    const seg = this.editingSeguimiento;
    if (!seg) return;

    while (this.itemsHembrasArray.length) this.itemsHembrasArray.removeAt(0);
    while (this.itemsMachosArray.length) this.itemsMachosArray.removeAt(0);

    // Metadata completa normalizada (soporta JSON string, objeto, camelCase y snake_case)
    const metadata = this.getNormalizedMetadata();
    const itemsHembras = metadata.itemsHembras ?? [];
    const itemsMachos = metadata.itemsMachos ?? [];
    itemsHembras.forEach((item) => {
      this.itemsHembrasArray.push(this.fb.group({
        tipoItem: [item.tipoItem ?? 'alimento', Validators.required],
        catalogItemId: [item.catalogItemId ?? null, Validators.required],
        cantidad: [Number(item.cantidad) ?? 0, [Validators.required, Validators.min(0)]],
        unidad: [item.unidad ?? 'kg', Validators.required]
      }));
    });
    itemsMachos.forEach((item) => {
      this.itemsMachosArray.push(this.fb.group({
        tipoItem: [item.tipoItem ?? 'alimento', Validators.required],
        catalogItemId: [item.catalogItemId ?? null, Validators.required],
        cantidad: [Number(item.cantidad) ?? 0, [Validators.required, Validators.min(0)]],
        unidad: [item.unidad ?? 'kg', Validators.required]
      }));
    });
    if (itemsHembras.length === 0 && itemsMachos.length === 0) {
      const consH = metadata.consumoOriginalHembras ?? (seg as any).consKgH ?? 0;
      const unidH = metadata.unidadConsumoOriginalHembras ?? 'kg';
      const consM = metadata.consumoOriginalMachos ?? (seg as any).consKgM ?? 0;
      const unidM = metadata.unidadConsumoOriginalMachos ?? 'kg';
      const tipoAlimentoH = metadata.tipoAlimentoHembras ?? null;
      const tipoAlimentoM = metadata.tipoAlimentoMachos ?? null;
      if (tipoAlimentoH || consH > 0) {
        this.itemsHembrasArray.push(this.fb.group({
          tipoItem: ['alimento', Validators.required],
          catalogItemId: [tipoAlimentoH ?? null, Validators.required],
          cantidad: [consH, [Validators.required, Validators.min(0)]],
          unidad: [unidH, Validators.required]
        }));
      }
      if (tipoAlimentoM || consM > 0) {
        this.itemsMachosArray.push(this.fb.group({
          tipoItem: ['alimento', Validators.required],
          catalogItemId: [tipoAlimentoM ?? null, Validators.required],
          cantidad: [consM, [Validators.required, Validators.min(0)]],
          unidad: [unidM, Validators.required]
        }));
      }
    }
    // Garantiza el alimento fijo (no removible) también al editar.
    this.asegurarAlimentoFijo(this.itemsHembrasArray);
    this.asegurarAlimentoFijo(this.itemsMachosArray);

    const fechaRegistro = this.toYMD(seg.fechaRegistro);
    const consumoOriginalHembras = metadata.consumoOriginalHembras ?? (seg as any).consKgH ?? 0;
    const unidadConsumoOriginalHembras = metadata.unidadConsumoOriginalHembras ?? 'kg';
    const consumoOriginalMachos = metadata.consumoOriginalMachos ?? (seg as any).consKgM ?? 0;
    const unidadConsumoOriginalMachos = metadata.unidadConsumoOriginalMachos ?? 'kg';
    const tipoItemHembras = metadata.tipoItemHembras ?? null;
    const tipoItemMachos = metadata.tipoItemMachos ?? null;
    const tipoAlimentoHembras = metadata.tipoAlimentoHembras ?? null;
    const tipoAlimentoMachos = metadata.tipoAlimentoMachos ?? null;

    // Convertir a gramos si la unidad original es kg y el valor es pequeño (para mejor UX)
    let consumoHembrasDisplay = consumoOriginalHembras;
    if (unidadConsumoOriginalHembras === 'kg' && consumoOriginalHembras < 1) {
      consumoHembrasDisplay = consumoOriginalHembras * 1000;
    }
    let consumoMachosDisplay = consumoOriginalMachos;
    if (unidadConsumoOriginalMachos === 'kg' && consumoOriginalMachos < 1) {
      consumoMachosDisplay = consumoOriginalMachos * 1000;
    }

    // Mapeo explícito de cada campo del payload del servicio al formulario de edición
    this.form.patchValue({
      fechaRegistro: fechaRegistro ?? this.todayYMD(),
      produccionLoteId: seg.lotePosturaProduccionId ? null : (seg as any).produccionLoteId ?? null,
      lotePosturaProduccionId: seg.lotePosturaProduccionId ?? (seg as any).lotePosturaProduccionId ?? null,
      mortalidadH: seg.mortalidadH ?? 0,
      mortalidadM: seg.mortalidadM ?? 0,
      selH: (seg as any).selH ?? 0,
      selM: (seg as any).selM ?? 0,
      errorSexajeHembras: (seg as any).errorSexajeHembras ?? 0,
      errorSexajeMachos: (seg as any).errorSexajeMachos ?? 0,
      ciclo: (seg as any).ciclo || 'Normal',
      consumoHembras: consumoHembrasDisplay,
      unidadConsumoHembras: unidadConsumoOriginalHembras === 'kg' && consumoOriginalHembras < 1 ? 'g' : unidadConsumoOriginalHembras,
      consumoMachos: consumoMachosDisplay,
      unidadConsumoMachos: unidadConsumoOriginalMachos === 'kg' && consumoOriginalMachos < 1 ? 'g' : unidadConsumoOriginalMachos,
      tipoItemHembras: tipoItemHembras,
      tipoItemMachos: tipoItemMachos,
      tipoAlimentoHembras: tipoAlimentoHembras,
      tipoAlimentoMachos: tipoAlimentoMachos,
      huevosTotales: seg.huevosTotales ?? 0,
      huevosIncubables: seg.huevosIncubables ?? 0,
      huevoLimpio: (seg as any).huevoLimpio ?? 0,
      huevoTratado: (seg as any).huevoTratado ?? 0,
      huevoSucio: (seg as any).huevoSucio ?? 0,
      huevoDeforme: (seg as any).huevoDeforme ?? 0,
      huevoBlanco: (seg as any).huevoBlanco ?? 0,
      huevoDobleYema: (seg as any).huevoDobleYema ?? 0,
      huevoPiso: (seg as any).huevoPiso ?? 0,
      huevoPequeno: (seg as any).huevoPequeno ?? 0,
      huevoRoto: (seg as any).huevoRoto ?? 0,
      huevoDesecho: (seg as any).huevoDesecho ?? 0,
      huevoOtro: (seg as any).huevoOtro ?? 0,
      tipoAlimento: seg.tipoAlimento || 'Standard',
      pesoHuevo: seg.pesoHuevo ?? 0,
      etapa: seg.etapa ?? this.calcularEtapa(fechaRegistro || this.todayYMD()),
      observaciones: seg.observaciones ?? '',
      pesoH: (seg as any).pesoH ?? null,
      pesoM: (seg as any).pesoM ?? null,
      uniformidad: (seg as any).uniformidad ?? null,
      coeficienteVariacion: (seg as any).coeficienteVariacion ?? null,
      uniformidadHembras: (seg as any).uniformidadHembras ?? null,
      uniformidadMachos: (seg as any).uniformidadMachos ?? null,
      cvHembras: (seg as any).cvHembras ?? null,
      cvMachos: (seg as any).cvMachos ?? null,
      observacionesPesaje: (seg as any).observacionesPesaje ?? '',
      consumoAguaDiario: (seg as any).consumoAguaDiario ?? null,
      consumoAguaPh: (seg as any).consumoAguaPh ?? null,
      consumoAguaOrp: (seg as any).consumoAguaOrp ?? null,
      consumoAguaTemperatura: (seg as any).consumoAguaTemperatura ?? null
    });

    // Cargar inventario y alimentos si hay tipo de ítem seleccionado
    // Si no hay tipoItem pero hay tipoAlimento, intentar obtener el tipoItem del inventario
    if (this.granjaId) {
      if (tipoItemHembras) {
        this.cargarInventarioGranja(this.granjaId, 'hembras', tipoItemHembras);
        setTimeout(() => {
          if (tipoAlimentoHembras) {
            this.consultarInventario('hembras', tipoAlimentoHembras);
          }
        }, 100);
      } else if (tipoAlimentoHembras) {
        // Si no hay tipoItem pero hay tipoAlimento, cargar inventario sin filtro y actualizar tipoItem
        this.cargarInventarioGranja(this.granjaId, 'hembras');
        setTimeout(() => {
          this.actualizarTiposItemDesdeInventario('hembras');
          if (tipoAlimentoHembras) {
            this.consultarInventario('hembras', tipoAlimentoHembras);
          }
        }, 100);
      }
      
      if (tipoItemMachos) {
        this.cargarInventarioGranja(this.granjaId, 'machos', tipoItemMachos);
        setTimeout(() => {
          if (tipoAlimentoMachos) {
            this.consultarInventario('machos', tipoAlimentoMachos);
          }
        }, 100);
      } else if (tipoAlimentoMachos) {
        // Si no hay tipoItem pero hay tipoAlimento, cargar inventario sin filtro y actualizar tipoItem
        this.cargarInventarioGranja(this.granjaId, 'machos');
        setTimeout(() => {
          this.actualizarTiposItemDesdeInventario('machos');
          if (tipoAlimentoMachos) {
            this.consultarInventario('machos', tipoAlimentoMachos);
          }
        }, 100);
      }
    }
  }

  // ================== EVENTOS ==================
  onClose(): void {
    if (!this.loading) {
      this.close.emit();
    }
  }

  // Métodos públicos para mostrar mensajes (llamados desde el componente padre)
  showSuccessMessage(isUpdate: boolean = false): void {
    const action = isUpdate ? 'actualizado' : 'creado';
    this.messageModalData = {
      title: `✅ Seguimiento ${action.charAt(0).toUpperCase() + action.slice(1)}`,
      message: `El seguimiento diario se ha ${action} exitosamente.`,
      type: 'success',
      confirmText: 'Aceptar',
      showCancel: false
    };
    this.showMessageModal = true;
    this.saveSuccess.emit();
  }

  showErrorMessage(message: string): void {
    const errorMsg = message || 'Ocurrió un error al intentar guardar el seguimiento diario. Por favor, intente nuevamente.';
    this.messageModalData = {
      title: '❌ Error al Guardar',
      message: errorMsg,
      type: 'error',
      confirmText: 'Entendido',
      showCancel: false
    };
    this.showMessageModal = true;
    this.saveError.emit(errorMsg);
  }

  onMessageModalClose(): void {
    this.showMessageModal = false;
    // Si el mensaje era de éxito, cerrar el modal de seguimiento diario
    if (this.messageModalData.type === 'success') {
      this.close.emit();
    }
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.showErrorMessage('Por favor, complete todos los campos requeridos correctamente.');
      return;
    }

    if (!this.lotePosturaProduccionId && !this.produccionLoteId) {
      this.showErrorMessage('Error: No se pudo identificar el lote de producción. Por favor, intente nuevamente.');
      return;
    }

    const raw = this.form.getRawValue(); // incluye campos deshabilitados (HuevosTotales/HuevosIncubables)
    const ymd = this.toYMD(raw.fechaRegistro);

    if (!ymd) {
      this.showErrorMessage('La fecha de registro es inválida. Por favor, seleccione una fecha válida.');
      return;
    }

    // Contrato de ids por país (Colombia: migrado→catalogItemId camino-1 / nuevo→itemInventarioEcuadorId camino-2).
    const mapItemControl = (c: any) => {
      const idf = this.buildItemPersistFields(Number(c.get('catalogItemId')?.value) || 0);
      return {
        tipoItem: c.get('tipoItem')?.value,
        catalogItemId: idf.catalogItemId,
        itemInventarioEcuadorId: idf.itemInventarioEcuadorId,
        nombre: idf.nombre ?? null,
        cantidad: Number(c.get('cantidad')?.value) || 0,
        unidad: c.get('unidad')?.value || 'kg'
      };
    };
    const itemsHembras = this.itemsHembrasArray.controls
      .map(mapItemControl)
      .filter((x: any) => x.tipoItem && (x.catalogItemId || x.itemInventarioEcuadorId));
    const itemsMachos = this.itemsMachosArray.controls
      .map(mapItemControl)
      .filter((x: any) => x.tipoItem && (x.catalogItemId || x.itemInventarioEcuadorId));
    const useItems = itemsHembras.length > 0 || itemsMachos.length > 0;
    let tipoAlimentoVal = raw.tipoAlimento || 'Standard';
    if (useItems) {
      const nombres: string[] = [];
      [...itemsHembras, ...itemsMachos].forEach((it: any) => {
        // El nombre viaja en el ítem (contrato de ids de Colombia puede usar id de catalogo_items,
        // ausente en alimentosById que está indexado por id de dropdown).
        const id = it.itemInventarioEcuadorId ?? it.catalogItemId;
        const name = it.nombre ?? this.alimentosById.get(id)?.nombre;
        if (name) nombres.push(name);
      });
      if (nombres.length) tipoAlimentoVal = nombres.join(' / ');
    }

    const useLPP = this.lotePosturaProduccionId != null;
    const request: CrearSeguimientoRequest = {
      produccionLoteId: useLPP ? undefined : this.produccionLoteId ?? undefined,
      lotePosturaProduccionId: useLPP ? this.lotePosturaProduccionId ?? undefined : undefined,
      fechaRegistro: this.ymdToIsoAtNoon(ymd),
      mortalidadH: Number(raw.mortalidadH) || 0,
      mortalidadM: Number(raw.mortalidadM) || 0,
      selH: Number(raw.selH) || 0,
      selM: Number(raw.selM) || 0,
      errorSexajeHembras: raw.errorSexajeHembras != null ? Number(raw.errorSexajeHembras) : 0,
      errorSexajeMachos: raw.errorSexajeMachos != null ? Number(raw.errorSexajeMachos) : 0,
      ciclo: raw.ciclo?.trim() || undefined,
      consumoH: useItems ? undefined : (Number(raw.consumoHembras) || 0),
      unidadConsumoH: raw.unidadConsumoHembras || 'kg',
      consumoM: useItems ? undefined : (Number(raw.consumoMachos) || 0),
      unidadConsumoM: raw.unidadConsumoMachos || 'kg',
      tipoItemHembras: useItems ? undefined : (raw.tipoItemHembras || undefined),
      tipoItemMachos: useItems ? undefined : (raw.tipoItemMachos || undefined),
      tipoAlimentoHembras: raw.tipoAlimentoHembras ? Number(raw.tipoAlimentoHembras) : undefined,
      tipoAlimentoMachos: raw.tipoAlimentoMachos ? Number(raw.tipoAlimentoMachos) : undefined,
      huevosTotales: Number(raw.huevosTotales) || 0,
      huevosIncubables: Number(raw.huevosIncubables) || 0,
      huevoLimpio: Number(raw.huevoLimpio) || 0,
      huevoTratado: Number(raw.huevoTratado) || 0,
      huevoSucio: Number(raw.huevoSucio) || 0,
      huevoDeforme: Number(raw.huevoDeforme) || 0,
      huevoBlanco: Number(raw.huevoBlanco) || 0,
      huevoDobleYema: Number(raw.huevoDobleYema) || 0,
      huevoPiso: Number(raw.huevoPiso) || 0,
      huevoPequeno: Number(raw.huevoPequeno) || 0,
      huevoRoto: Number(raw.huevoRoto) || 0,
      huevoDesecho: Number(raw.huevoDesecho) || 0,
      huevoOtro: Number(raw.huevoOtro) || 0,
      tipoAlimento: tipoAlimentoVal,
      itemsHembras: useItems ? itemsHembras : undefined,
      itemsMachos: useItems ? itemsMachos : undefined,
      granjaId: this.isEcuadorOrPanama && useItems && this.granjaId ? this.granjaId : undefined,
      nucleoId: this.isEcuadorOrPanama && useItems ? (this.nucleoId || undefined) : undefined,
      galponId: this.isEcuadorOrPanama && useItems ? (this.galponId || undefined) : undefined,
      pesoHuevo: Number(raw.pesoHuevo) || 0,
      etapa: Number(raw.etapa) || this.calcularEtapa(ymd),
      observaciones: raw.observaciones?.trim() || undefined,
      // Campos de Pesaje Semanal
      pesoH: raw.pesoH ? Number(raw.pesoH) : undefined,
      pesoM: raw.pesoM ? Number(raw.pesoM) : undefined,
      uniformidad: raw.uniformidad ? Number(raw.uniformidad) : undefined,
      coeficienteVariacion: raw.coeficienteVariacion ? Number(raw.coeficienteVariacion) : undefined,
      uniformidadHembras: raw.uniformidadHembras != null && raw.uniformidadHembras !== '' ? Number(raw.uniformidadHembras) : undefined,
      uniformidadMachos: raw.uniformidadMachos != null && raw.uniformidadMachos !== '' ? Number(raw.uniformidadMachos) : undefined,
      cvHembras: raw.cvHembras != null && raw.cvHembras !== '' ? Number(raw.cvHembras) : undefined,
      cvMachos: raw.cvMachos != null && raw.cvMachos !== '' ? Number(raw.cvMachos) : undefined,
      observacionesPesaje: raw.observacionesPesaje?.trim() || undefined,
      // Campos de agua (solo para Ecuador y Panamá)
      consumoAguaDiario: raw.consumoAguaDiario ? Number(raw.consumoAguaDiario) : undefined,
      consumoAguaPh: raw.consumoAguaPh ? Number(raw.consumoAguaPh) : undefined,
      consumoAguaOrp: raw.consumoAguaOrp ? Number(raw.consumoAguaOrp) : undefined,
      consumoAguaTemperatura: raw.consumoAguaTemperatura ? Number(raw.consumoAguaTemperatura) : undefined,
      createdByUserId: this.storage.get()?.user?.id ?? null,
      tipoSeguimiento: 'produccion'
    };

    this.save.emit(request);
  }

  private setupHuevosAutoCalculo(): void {
    const keys = [
      'huevoLimpio',
      'huevoTratado',
      'huevoSucio',
      'huevoDeforme',
      'huevoBlanco',
      'huevoDobleYema',
      'huevoPiso',
      'huevoPequeno',
      'huevoRoto',
      'huevoDesecho',
      'huevoOtro'
    ] as const;

    const recalc = () => {
      const n = (k: typeof keys[number]) => Number(this.form.get(k)?.value) || 0;
      const limpio = n('huevoLimpio');
      const tratado = n('huevoTratado');
      const incubables = limpio + tratado;
      const total =
        incubables +
        n('huevoSucio') +
        n('huevoDeforme') +
        n('huevoBlanco') +
        n('huevoDobleYema') +
        n('huevoPiso') +
        n('huevoPequeno') +
        n('huevoRoto') +
        n('huevoDesecho') +
        n('huevoOtro');

      this.form.patchValue(
        {
          huevosIncubables: incubables,
          huevosTotales: total
        },
        { emitEvent: false }
      );
    };

    keys.forEach(k => this.form.get(k)?.valueChanges.subscribe(recalc));
    recalc();
  }

  // ================== HELPERS ==================
  getTotalMortalidad(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const machos = Number(this.form.get('mortalidadM')?.value) || 0;
    return hembras + machos;
  }

  getTotalRetiradas(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const machos = Number(this.form.get('mortalidadM')?.value) || 0;
    const selH = Number(this.form.get('selH')?.value) || 0;
    const selM = Number(this.form.get('selM')?.value) || 0;
    return hembras + machos + selH + selM;
  }

  getTotalRetiradasHembras(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const selH = Number(this.form.get('selH')?.value) || 0;
    return hembras + selH;
  }

  getTotalRetiradasMachos(): number {
    const machos = Number(this.form.get('mortalidadM')?.value) || 0;
    const selM = Number(this.form.get('selM')?.value) || 0;
    return machos + selM;
  }

  getTotalConsumo(): number {
    const consumoH = Number(this.form.get('consumoHembras')?.value) || 0;
    const unidadH = this.form.get('unidadConsumoHembras')?.value || 'kg';
    const consumoM = Number(this.form.get('consumoMachos')?.value) || 0;
    const unidadM = this.form.get('unidadConsumoMachos')?.value || 'kg';
    
    // Convertir todo a kg para sumar
    const consumoHkg = unidadH === 'g' ? consumoH / 1000 : consumoH;
    const consumoMkg = unidadM === 'g' ? consumoM / 1000 : consumoM;
    return consumoHkg + consumoMkg;
  }

  getEficienciaProduccion(): number {
    const total = Number(this.form.get('huevosTotales')?.value) || 0;
    const incubables = Number(this.form.get('huevosIncubables')?.value) || 0;

    if (total === 0) return 0;
    return Math.round((incubables / total) * 100);
  }

  calcularYActualizarEtapa(): void {
    const fechaRegistro = this.form.get('fechaRegistro')?.value;
    if (fechaRegistro) {
      const etapa = this.calcularEtapa(fechaRegistro);
      this.form.patchValue({ etapa }, { emitEvent: false });
    }
  }

  calcularEtapa(fechaRegistro: string | Date | null): number {
    if (!fechaRegistro || !this.fechaEncaset) return 1;

    const fechaEncaset = new Date(this.fechaEncaset);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaEncaset.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    const semana = Math.max(25, Math.ceil(diffDays / 7));

    // Etapa 1: semana 25-33
    if (semana >= 25 && semana <= 33) return 1;
    // Etapa 2: semana 34-50
    if (semana >= 34 && semana <= 50) return 2;
    // Etapa 3: semana >50
    return 3;
  }

  getEtapaLabel(etapa: number): string {
    const labels: { [key: number]: string } = {
      1: 'Etapa 1 (Semana 25-33)',
      2: 'Etapa 2 (Semana 34-50)',
      3: 'Etapa 3 (Semana >50)'
    };
    return labels[etapa] || `Etapa ${etapa}`;
  }

  /** Hoy en formato YYYY-MM-DD (local, sin zona) para <input type="date"> */
  private todayYMD(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  /** Normaliza cadenas mm/dd/aaaa, dd/mm/aaaa, ISO o Date a YYYY-MM-DD (local) */
  private toYMD(input: string | Date | null | undefined): string | null {
    if (!input) return null;

    if (input instanceof Date && !isNaN(input.getTime())) {
      const y = input.getFullYear();
      const m = String(input.getMonth() + 1).padStart(2, '0');
      const d = String(input.getDate()).padStart(2, '0');
      return `${y}-${m}-${d}`;
    }

    const s = String(input).trim();

    // YYYY-MM-DD
    const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
    const m1 = s.match(ymd);
    if (m1) return `${m1[1]}-${m1[2]}-${m1[3]}`;

    // mm/dd/aaaa o dd/mm/aaaa
    const sl = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;
    const m2 = s.match(sl);
    if (m2) {
      let a = parseInt(m2[1], 10);
      let b = parseInt(m2[2], 10);
      const yyyy = parseInt(m2[3], 10);
      let mm = a, dd = b;
      if (a > 12 && b <= 12) { mm = b; dd = a; }
      const mmS = String(mm).padStart(2, '0');
      const ddS = String(dd).padStart(2, '0');
      return `${yyyy}-${mmS}-${ddS}`;
    }

    // ISO (con T). Extrae la fecha en LOCAL sin cambiar el día
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${y}-${m}-${day}`;
    }

    return null;
  }

  /** Convierte YYYY-MM-DD a ISO asegurando MEDIODÍA local → evita cruzar de día por zona horaria */
  private ymdToIsoAtNoon(ymd: string): string {
    const iso = new Date(`${ymd}T12:00:00`);
    return iso.toISOString();
  }

  // ================== INVENTARIO Y ALIMENTOS ==================

  /** Colombia + EC/PA: carga catálogo item_inventario_ecuador y conceptos; luego stock por ubicación. */
  private cargarCatalogEcuadorPanama(): void {
    if (!this.granjaId) return;
    // Colombia: mapa codigo→catalogo_items.id para el contrato de ids al descontar (una sola vez).
    if (this.isColombia && this.catalogItemIdPorCodigo.size === 0) {
      this.inventarioSvc.getCatalogo('', 1, 2000).pipe(
        catchError(() => of([] as CatalogItemDto[]))
      ).subscribe(cat => {
        this.catalogItemIdPorCodigo.clear();
        for (const c of cat) {
          if (c?.codigo && c.id != null) this.catalogItemIdPorCodigo.set(String(c.codigo).trim().toLowerCase(), Number(c.id));
        }
      });
    }
    this.gestionInventarioSvc.getItemsByType(null, null, true).pipe(
      catchError(err => { console.error('Error al cargar ítems inventario:', err); return of([]); })
    ).subscribe((list: ItemInventarioDto[]) => {
      this.itemsEcuadorPanama = list ?? [];
      this.conceptosEcuadorPanama = Array.from(
        new Set(
          this.itemsEcuadorPanama
            .map(i => (i.concepto ?? i.tipoItem ?? '').trim())
            .filter(x => !!x)
        )
      ).sort((a, b) => a.localeCompare(b));
      // Colombia usa la lista fija de tipos ('alimento' en minúscula matchea el filtro por concepto);
      // solo EC/PA reemplazan los tipos por los conceptos del catálogo.
      if (this.isEcuadorOrPanama && this.conceptosEcuadorPanama.length > 0) {
        this.tiposItem = this.conceptosEcuadorPanama;
      }
      this.cargarStockEcuadorPanama();
    });
  }

  /** Colombia + EC/PA: carga stock inventario-gestion y llena inventarioPorItem por item id.
   * Colombia opera a NIVEL GRANJA (núcleo/galpón NULL); EC/PA por núcleo/galpón del lote. */
  private cargarStockEcuadorPanama(): void {
    if (!this.granjaId) return;
    const params: { farmId: number; nucleoId?: string; galponId?: string; itemType?: string } = { farmId: this.granjaId };
    if (!this.isColombia && this.nucleoId) params.nucleoId = this.nucleoId;
    if (!this.isColombia && this.galponId) params.galponId = this.galponId;
    this.gestionInventarioSvc.getStock(params).pipe(
      catchError(err => { console.error('Error al cargar stock:', err); return of([]); })
    ).subscribe((rows: InventarioGestionStockDto[]) => {
      this.inventarioPorItem.clear();
      rows.forEach(r => {
        const prev = this.inventarioPorItem.get(r.itemInventarioEcuadorId);
        const q = prev ? prev.quantity + r.quantity : r.quantity;
        this.inventarioPorItem.set(r.itemInventarioEcuadorId, { quantity: q, unit: r.unit });
      });
      this.alimentosCatalog = this.itemsEcuadorPanama.map(i => this.itemEcuadorToExtended(i));
      this.alimentosFiltradosHembras = this.alimentosCatalog;
      this.alimentosFiltradosMachos = this.alimentosCatalog;
      this.alimentosById.clear();
      this.alimentosCatalog.forEach(a => { if (a.id != null) this.alimentosById.set(a.id, a); });
      // Colombia edición: traducir ids guardados (catalogo_items) al id del dropdown (iie) por código.
      if (this.isColombia && this.editingSeguimiento) this.traducirIdsColombiaAlEditar();
    });
  }

  /** Colombia edición: traduce el id guardado (posible catalogo_items.id) al id del dropdown actual
   * (item_inventario_ecuador.id), buscando por código. Si ya es un iieId, lo deja igual. */
  private mapStoredIdToDropdownId(storedId: number): number {
    if (!this.isColombia || !storedId) return storedId;
    if (this.itemsEcuadorPanama.some(i => i.id === storedId)) return storedId;
    let codigo: string | undefined;
    for (const [cod, id] of this.catalogItemIdPorCodigo.entries()) {
      if (id === storedId) { codigo = cod; break; }
    }
    if (!codigo) return storedId;
    const iie = this.itemsEcuadorPanama.find(i => (i.codigo ?? '').trim().toLowerCase() === codigo);
    return iie ? iie.id : storedId;
  }

  private traducirIdsColombiaAlEditar(): void {
    for (const arr of [this.itemsHembrasArray, this.itemsMachosArray]) {
      for (const control of arr.controls) {
        const actual = Number(control.get('catalogItemId')?.value);
        if (!actual) continue;
        const traducido = this.mapStoredIdToDropdownId(actual);
        if (traducido !== actual) control.patchValue({ catalogItemId: traducido }, { emitEvent: false });
      }
    }
  }

  /**
   * Campos de id + nombre para persistir un ítem según el contrato de inventario del país.
   * `itemId` = id del dropdown (Colombia/EC/PA = item_inventario_ecuador.id). Colombia: si el ítem
   * tiene espejo en catalogo_items (mismo código) se envía ese id (camino-1); si es nuevo sin espejo
   * (p.ej. "moises") se envía el id de item_inventario_ecuador (camino-2).
   */
  private buildItemPersistFields(itemId: number): { catalogItemId: number; itemInventarioEcuadorId: number | null; nombre?: string } {
    const nombre = this.alimentosById.get(itemId)?.nombre ?? undefined;
    if (this.isEcuadorOrPanama) {
      return { catalogItemId: itemId, itemInventarioEcuadorId: itemId, nombre };
    }
    if (this.isColombia) {
      const item = this.itemsEcuadorPanama.find(i => i.id === itemId);
      const codigo = (item?.codigo ?? '').trim().toLowerCase();
      const catalogoItemsId = codigo ? this.catalogItemIdPorCodigo.get(codigo) : undefined;
      if (catalogoItemsId) return { catalogItemId: catalogoItemsId, itemInventarioEcuadorId: null, nombre };
      return { catalogItemId: 0, itemInventarioEcuadorId: itemId, nombre };
    }
    return { catalogItemId: itemId, itemInventarioEcuadorId: null, nombre };
  }

  /**
   * Carga el inventario de la granja filtrado por tipo de item desde el backend
   * El backend filtra por empresa, país, granja y tipo de item
   */
  cargarInventarioGranja(granjaId: number, sexo?: 'hembras' | 'machos', itemType?: string | null): void {
    if (!granjaId) return;
    // Colombia + EC/PA usan el inventario nuevo (cargarCatalog/StockEcuadorPanama). No tocar el viejo
    // (evita clobber de inventarioPorItem/alimentosCatalog con datos del inventario legacy).
    if (this.usaInventarioGestion) return;

    // Obtener el tipo de item del formulario si no se proporciona
    if (!itemType && sexo) {
      itemType = sexo === 'hembras' 
        ? this.form.get('tipoItemHembras')?.value 
        : this.form.get('tipoItemMachos')?.value;
    }

    this.inventarioSvc.getInventory(granjaId, itemType).pipe(
      catchError(err => {
        console.error('Error al cargar inventario:', err);
        return of([]);
      })
    ).subscribe((items: FarmInventoryDto[]) => {
      const itemsActivos = items.filter((item: FarmInventoryDto) => item.active && item.quantity > 0);
      itemsActivos.forEach((item: FarmInventoryDto) => {
        this.inventarioPorItem.set(item.catalogItemId, { quantity: item.quantity, unit: item.unit || 'kg' });
      });
      const catalogItems = itemsActivos.map((item: FarmInventoryDto) => {
        const metadata = item.catalogItemMetadata || {};
        // Asegurar que el tipoItem esté disponible
        const itemType = metadata.itemType || metadata.type_item || 'alimento';
        if (!metadata.itemType && !metadata.type_item) {
          metadata.itemType = itemType;
        }
        
        const catalogItem: CatalogItemExtended = {
          id: item.catalogItemId,
          codigo: item.codigo,
          nombre: item.nombre,
          tipoItem: itemType,
          unidad: item.unit,
          activo: item.active,
          metadata: metadata
        };
        
        this.alimentosByCode.set(item.codigo, catalogItem);
        this.alimentosById.set(item.catalogItemId, catalogItem);
        this.alimentosByName.set(item.nombre.toLowerCase(), catalogItem);
        
        return catalogItem;
      });

      // Actualizar el catálogo completo y los filtrados según el sexo
      if (sexo === 'hembras') {
        this.alimentosFiltradosHembras = catalogItems;
        // Después de cargar el inventario, actualizar los tipos de ítem si estamos editando
        this.actualizarTiposItemDesdeInventario('hembras');
      } else if (sexo === 'machos') {
        this.alimentosFiltradosMachos = catalogItems;
        // Después de cargar el inventario, actualizar los tipos de ítem si estamos editando
        this.actualizarTiposItemDesdeInventario('machos');
      } else {
        this.alimentosCatalog = catalogItems;
        this.alimentosFiltradosHembras = catalogItems;
        this.alimentosFiltradosMachos = catalogItems;
        const tipoH = this.form.get('tipoItemHembras')?.value;
        const tipoM = this.form.get('tipoItemMachos')?.value;
        if (tipoH) {
          this.cargarInventarioGranja(granjaId, 'hembras', tipoH);
        }
        if (tipoM) {
          this.cargarInventarioGranja(granjaId, 'machos', tipoM);
        }
      }
    });
  }

  /**
   * Actualiza los tipos de ítem cuando se carga el inventario
   * Esto asegura que el tipoItem se muestre correctamente al editar
   */
  private actualizarTiposItemDesdeInventario(sexo: 'hembras' | 'machos'): void {
    if (!this.editingSeguimiento) return;

    const catalogItemId = sexo === 'hembras' 
      ? this.form.get('tipoAlimentoHembras')?.value 
      : this.form.get('tipoAlimentoMachos')?.value;
    
    const tipoItemControl = sexo === 'hembras'
      ? this.form.get('tipoItemHembras')
      : this.form.get('tipoItemMachos');
    
    if (catalogItemId && tipoItemControl) {
      const item = this.alimentosById.get(catalogItemId);
      if (item) {
        // Obtener tipoItem desde múltiples fuentes posibles
        const tipoItem = item.metadata?.type_item || item.metadata?.itemType || item.tipoItem || 'alimento';
        const tipoItemActual = tipoItemControl.value;
        
        // Si el tipoItem actual es 'alimento' (fallback) o está vacío, actualizarlo con el real
        if ((!tipoItemActual || tipoItemActual === 'alimento') && tipoItem && tipoItem !== tipoItemActual) {
          tipoItemControl.patchValue(tipoItem, { emitEvent: false });
          // Recargar inventario con el tipo correcto
          if (this.granjaId) {
            this.cargarInventarioGranja(this.granjaId, sexo, tipoItem);
          }
        } else if (tipoItemActual && tipoItemActual !== tipoItem && tipoItem) {
          // Si el tipoItem actual no coincide con el del catálogo, actualizar
          tipoItemControl.patchValue(tipoItem, { emitEvent: false });
          if (this.granjaId) {
            this.cargarInventarioGranja(this.granjaId, sexo, tipoItem);
          }
        }
      }
    }
  }

  /**
   * Consulta el inventario disponible para un alimento específico
   */
  consultarInventario(sexo: 'hembras' | 'machos', catalogItemId: number): void {
    if (!this.granjaId || !catalogItemId) return;
    // Colombia + EC/PA: la disponibilidad se lee del inventario nuevo (inventarioPorItem vía
    // getCantidadDisponible en la plantilla). No consultar el endpoint del inventario viejo.
    if (this.usaInventarioGestion) return;

    if (sexo === 'hembras') {
      this.cargandoInventarioHembras = true;
      this.mensajeInventarioHembras = '';
    } else {
      this.cargandoInventarioMachos = true;
      this.mensajeInventarioMachos = '';
    }

    this.inventarioSvc.getInventoryByItem(this.granjaId, catalogItemId).pipe(
      catchError(err => {
        console.error('Error al consultar inventario:', err);
        if (sexo === 'hembras') {
          this.cargandoInventarioHembras = false;
          this.mensajeInventarioHembras = 'Error al consultar inventario';
        } else {
          this.cargandoInventarioMachos = false;
          this.mensajeInventarioMachos = 'Error al consultar inventario';
        }
        return of(null);
      })
    ).subscribe(item => {
      if (sexo === 'hembras') {
        this.cargandoInventarioHembras = false;
        if (item) {
          this.inventarioDisponibleHembras = item.quantity;
          this.inventarioUnidadHembras = item.unit;
          this.inventarioCantidadOriginalHembras = item.quantity;
          this.mensajeInventarioHembras = `Disponible: ${item.quantity} ${item.unit}`;
        } else {
          this.inventarioDisponibleHembras = null;
          this.mensajeInventarioHembras = 'No hay inventario disponible';
        }
      } else {
        this.cargandoInventarioMachos = false;
        if (item) {
          this.inventarioDisponibleMachos = item.quantity;
          this.inventarioUnidadMachos = item.unit;
          this.inventarioCantidadOriginalMachos = item.quantity;
          this.mensajeInventarioMachos = `Disponible: ${item.quantity} ${item.unit}`;
        } else {
          this.inventarioDisponibleMachos = null;
          this.mensajeInventarioMachos = 'No hay inventario disponible';
        }
      }
    });
  }
}

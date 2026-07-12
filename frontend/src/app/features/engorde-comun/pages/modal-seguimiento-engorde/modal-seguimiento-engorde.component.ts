import { Component, Input, Output, EventEmitter, OnInit, OnChanges, OnDestroy, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { ToastService } from '../../../../shared/services/toast.service';
import { Subscription } from 'rxjs';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { SeguimientoLoteLevanteDto, CreateSeguimientoLoteLevanteDto, UpdateSeguimientoLoteLevanteDto, ItemSeguimientoDto } from '../../../lote-levante/services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { CatalogoAlimentosService, CatalogItemDto, PagedResult, CatalogItemType } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { InventarioService, FarmInventoryDto } from '../../../inventario/services/inventario.service';
import { GestionInventarioService, ItemInventarioDto, InventarioGestionStockDto } from '../../../gestion-inventario/services/gestion-inventario.service';
import { EMPTY, forkJoin, of } from 'rxjs';
import { expand, map, reduce, finalize, debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';
import { ShowIfEcuadorPanamaDirective } from '../../../../core/directives';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { AvesDisponiblesDto } from '../../../lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service';
import { InventarioUbicacion } from '../../models/inventario-ubicacion.model';
import { computeDefaultFecha, toYMD, ymdToIsoAtNoon } from '../../funciones/fecha.funcion';
import {
  toKg,
  esUnidadDesconocidaParaGramos,
  cantidadOriginalAGramos,
  normalizarIdCatalogoSeleccion
} from '../../funciones/inventario-calculos.funcion';
import {
  normalizeJsonField,
  resolveItemCatalogId,
  getInventarioUbicacionFromLote,
  itemEcuadorToCatalogItem,
  construirItemsSeguimiento,
  construirItemsAdicionales,
  construirTipoAlimentoStr,
  aplicarCerosSinAvesDisponibles,
  mapearPanamaMixtoAHM,
  buildBaseSeguimientoDto
} from '../../funciones/mapear-seguimiento-dto.funcion';

@Component({
  selector: 'app-modal-seguimiento-engorde',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ShowIfEcuadorPanamaDirective],
  templateUrl: './modal-seguimiento-engorde.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./modal-seguimiento-engorde.component.scss']
})
export class ModalSeguimientoEngordeComponent implements OnInit, OnChanges, OnDestroy {
  @Input() isOpen: boolean = false;
  @Input() editing: SeguimientoLoteLevanteDto | null = null;
  @Input() lotes: LoteDto[] = [];
  @Input() selectedLoteId: number | null = null;
  @Input() loading: boolean = false;
  /** True mientras se obtiene el registro por ID (editar) antes de mostrar el formulario. */
  @Input() loadingRecord: boolean = false;
  /** Aves disponibles por sexo (GET aves-disponibles); solo reglas de UI en nuevo registro. */
  @Input() avesDisponibles: AvesDisponiblesDto | null = null;
  /** Fechas de registros ya existentes (ISO o YYYY-MM-DD) para calcular el día siguiente como default. */
  @Input() existingFechas: string[] = [];

  /** Pollo engorde: consumo solo alimento; UI simplificada (no comparte el modal de Levante). */
  readonly hembrasSoloAlimento = true;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{ data: CreateSeguimientoLoteLevanteDto | UpdateSeguimientoLoteLevanteDto; isEdit: boolean }>();

  // Formulario
  form!: FormGroup;

  // Catálogo de alimentos (ahora desde inventario de la granja)
  alimentosCatalog: CatalogItemDto[] = [];
  alimentosFiltradosHembras: CatalogItemDto[] = [];
  alimentosFiltradosMachos: CatalogItemDto[] = [];
  private alimentosByCode = new Map<string, CatalogItemDto>();
  private alimentosById = new Map<number, CatalogItemDto>();
  private alimentosByName = new Map<string, CatalogItemDto>();
  private granjaIdActual: number | null = null;
  /** Núcleo/galpón del lote seleccionado: el stock EC/PA se consulta por esta ubicación, no agregado a toda la granja. */
  private inventarioUbicacionActual: InventarioUbicacion = {
    nucleoId: null,
    galponId: null
  };
  private inventarioLoadId = 0;
  private cargandoInventarioGranja = false;

  // Mapa para guardar información de inventario (cantidad disponible) por catalogItemId
  private inventarioPorItem = new Map<number, { quantity: number; unit: string }>();
  // Edición: consumo original por ítem en kg (para validar delta en update)
  private originalConsumoKgByItem = new Map<number, number>();

  // Tipos de ítem (Ecuador/Panamá: se reemplaza por conceptos de item_inventario_ecuador)
  private readonly TIPOS_ITEM_DEFAULT: CatalogItemType[] = ['alimento', 'medicamento', 'accesorio', 'biologico', 'consumible', 'otro'];
  tiposItem: string[] = ['alimento', 'medicamento', 'accesorio', 'biologico', 'consumible', 'otro'];
  itemsEcuadorPanama: ItemInventarioDto[] = [];
  conceptosEcuadorPanama: string[] = [];

  // Inventario
  inventarioDisponibleHembras: number | null = null;
  inventarioDisponibleMachos: number | null = null;
  inventarioUnidadHembras: string = 'kg'; // Unidad del inventario (kg, g, etc.)
  inventarioUnidadMachos: string = 'kg';
  inventarioCantidadOriginalHembras: number | null = null; // Cantidad en la unidad original del inventario
  inventarioCantidadOriginalMachos: number | null = null;
  cargandoInventarioHembras = false;
  cargandoInventarioMachos = false;
  mensajeInventarioHembras: string = '';
  mensajeInventarioMachos: string = '';

  // Propiedad para verificar si es Ecuador o Panamá
  isEcuadorOrPanama = false;
  /** True cuando el país activo es Panamá → modo Mixto (una sola columna en lugar de H/M). */
  get isPanama(): boolean { return this.countryFilter.isPanama(); }
  private sessionSubscription?: Subscription;

  constructor(private toast: ToastService, 
    private fb: FormBuilder,
    private catalogSvc: CatalogoAlimentosService,
    private inventarioSvc: InventarioService,
    private gestionInventarioSvc: GestionInventarioService,
    private countryFilter: CountryFilterService,
    private storage: TokenStorageService
  ) { }

  /** Opciones de "Tipo de ítem": en Ecuador/Panamá son los conceptos de item_inventario_ecuador; si no, la lista fija. */
  get tiposItemDisplay(): string[] {
    return (this.isEcuadorOrPanama && this.conceptosEcuadorPanama.length > 0)
      ? this.conceptosEcuadorPanama
      : this.tiposItem;
  }

  ngOnInit(): void {
    this.initializeForm();
    // No cargar catálogo automáticamente, se cargará cuando se seleccione un tipo de ítem
    // Verificar si es Ecuador o Panamá
    this.updateEcuadorOrPanamaStatus();

    // Suscribirse a cambios en la sesión
    this.sessionSubscription = this.storage.session$.subscribe(() => {
      this.updateEcuadorOrPanamaStatus();
    });
  }

  ngOnDestroy(): void {
    this.sessionSubscription?.unsubscribe();
  }

  private updateEcuadorOrPanamaStatus(): void {
    this.isEcuadorOrPanama = this.countryFilter.isEcuadorOrPanama();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.isOpen) return;
    // Evitar repoblar al cambiar solo lotes/loading (borraría lo que el usuario editó en el modal)
    if (!changes['isOpen'] && !changes['editing'] && !changes['avesDisponibles']) return;

    if (changes['avesDisponibles'] && !this.editing && this.form) {
      this.aplicarBloqueoPorAvesDisponibles();
    }

    if (this.editing) {
      this.populateForm();
    } else {
      this.resetForm();
    }
  }

  // ================== FORMULARIO ==================
  private initializeForm(): void {
    this.form = this.fb.group({
      fechaRegistro: [computeDefaultFecha(this.existingFechas, this.selectedLoteId, this.lotes), Validators.required],
      loteId: ['', Validators.required],
      mortalidadHembras: [0, [Validators.required, Validators.min(0)]],
      mortalidadMachos: [0, [Validators.required, Validators.min(0)]],
      selH: [0, [Validators.required, Validators.min(0)]],
      selM: [0, [Validators.required, Validators.min(0)]],
      errorSexajeHembras: [0, [Validators.required, Validators.min(0)]],
      errorSexajeMachos: [0, [Validators.required, Validators.min(0)]],
      // Campos Mixto (Panamá): reemplazan H/M en la UI; en onSave() se mapean a H con M=0.
      mortalidadMixtas: [0, [Validators.required, Validators.min(0)]],
      selMixtas: [0, [Validators.required, Validators.min(0)]],
      errorSexajeMixtas: [0, [Validators.required, Validators.min(0)]],
      pesoPromMixto: [null, [Validators.min(0)]],
      uniformidadMixta: [null, [Validators.min(0), Validators.max(100)]],
      cvMixto: [null, [Validators.min(0)]],
      tipoAlimento: [''],
      // FormArrays para múltiples ítems (alimentos y otros)
      itemsHembras: this.fb.array([]),
      itemsMachos: this.fb.array([]),
      observaciones: [''],
      pesoPromH: [null, [Validators.min(0)]],
      pesoPromM: [null, [Validators.min(0)]],
      uniformidadH: [null, [Validators.min(0), Validators.max(100)]],
      uniformidadM: [null, [Validators.min(0), Validators.max(100)]],
      cvH: [null, [Validators.min(0)]],
      cvM: [null, [Validators.min(0)]],
      ciclo: ['Normal'],
      // Campos de agua (solo para Ecuador y Panamá)
      consumoAguaDiario: [null, [Validators.min(0)]],
      consumoAguaPh: [null, [Validators.min(0)]],
      consumoAguaOrp: [null, [Validators.min(0)]],
      consumoAguaTemperatura: [null, [Validators.min(0)]],
      // Campos específicos Panamá: quintales de alimento por categoría
      qqMixtas: [null, [Validators.min(0)]],
      qqHembras: [null, [Validators.min(0)]],
      qqMachos: [null, [Validators.min(0)]],
    });

    // Ya no necesitamos suscripciones individuales, los ítems se manejan en el FormArray

    // Suscribirse a cambios en loteId para obtener granjaId y cargar inventario
    this.form.get('loteId')?.valueChanges.subscribe(loteId => {
      if (loteId) {
        // Obtener el lote para conseguir granjaId
        const lote = this.lotes.find(l => String(l.loteId) === String(loteId));
        if (lote && lote.granjaId) {
          const nuevaGranjaId = lote.granjaId;
          const u = getInventarioUbicacionFromLote(lote);
          const granjaCambio = this.granjaIdActual !== nuevaGranjaId;
          const ubicCambio =
            this.inventarioUbicacionActual.nucleoId !== u.nucleoId ||
            this.inventarioUbicacionActual.galponId !== u.galponId;
          if (granjaCambio || ubicCambio) {
            this.granjaIdActual = nuevaGranjaId;
            this.inventarioUbicacionActual = u;
            this.cargarInventarioGranja(nuevaGranjaId, undefined, u);
          }
        }

        // Limpiar inventario cuando cambia el lote
        this.inventarioDisponibleHembras = null;
        this.inventarioDisponibleMachos = null;
        this.inventarioUnidadHembras = 'kg';
        this.inventarioUnidadMachos = 'kg';
        this.inventarioCantidadOriginalHembras = null;
        this.inventarioCantidadOriginalMachos = null;
        this.mensajeInventarioHembras = '';
        this.mensajeInventarioMachos = '';

        // Reconsultar inventario si hay alimento seleccionado
        const alimentoH = this.form.get('tipoAlimentoHembras')?.value;
        const alimentoM = this.form.get('tipoAlimentoMachos')?.value;
        if (alimentoH) this.consultarInventario('hembras', alimentoH);
        if (alimentoM) this.consultarInventario('machos', alimentoM);
      } else {
        // Si no hay lote seleccionado, limpiar todo
        this.granjaIdActual = null;
        this.alimentosCatalog = [];
        this.alimentosFiltradosHembras = [];
        this.alimentosFiltradosMachos = [];
        this.alimentosById.clear();
        this.alimentosByCode.clear();
        this.alimentosByName.clear();
        this.inventarioPorItem.clear();
        this.itemsEcuadorPanama = [];
        this.conceptosEcuadorPanama = [];
        this.inventarioUbicacionActual = { nucleoId: null, galponId: null };
      }
    });

    // Ya no necesitamos actualizar consumoKg aquí, el backend hace la conversión
    // Solo mantenemos los valores para validación de inventario en el frontend
  }

  private resetForm(): void {
    // Limpiar FormArrays
    while (this.itemsHembrasArray.length !== 0) {
      this.itemsHembrasArray.removeAt(0);
    }
    while (this.itemsMachosArray.length !== 0) {
      this.itemsMachosArray.removeAt(0);
    }

    this.form.reset({
      fechaRegistro: computeDefaultFecha(this.existingFechas, this.selectedLoteId, this.lotes),
      loteId: this.selectedLoteId,
      mortalidadHembras: 0,
      mortalidadMachos: 0,
      selH: 0,
      selM: 0,
      errorSexajeHembras: 0,
      errorSexajeMachos: 0,
      tipoAlimento: '',
      observaciones: '',
      ciclo: 'Normal',
      pesoPromH: null,
      pesoPromM: null,
      uniformidadH: null,
      uniformidadM: null,
      cvH: null,
      cvM: null,
      // Campos de agua (solo para Ecuador y Panamá)
      consumoAguaDiario: null,
      consumoAguaPh: null,
      consumoAguaOrp: null,
      consumoAguaTemperatura: null,
      // Campos específicos Panamá: quintales
      qqMixtas: null,
      qqHembras: null,
      qqMachos: null,
      // Campos Mixto Panamá: mortalidad/selección/peso en columna única
      mortalidadMixtas: 0,
      selMixtas: 0,
      errorSexajeMixtas: 0,
      pesoPromMixto: null,
      uniformidadMixta: null,
      cvMixto: null,
    });
    this.inventarioDisponibleHembras = null;
    this.inventarioDisponibleMachos = null;
    this.inventarioUnidadHembras = 'kg';
    this.inventarioUnidadMachos = 'kg';
    this.inventarioCantidadOriginalHembras = null;
    this.inventarioCantidadOriginalMachos = null;
    this.mensajeInventarioHembras = '';
    this.mensajeInventarioMachos = '';
    this.alimentosFiltradosHembras = [];
    this.alimentosFiltradosMachos = [];
    this.originalConsumoKgByItem.clear();

    // Si hay lote seleccionado, cargar inventario de su granja
    if (this.selectedLoteId) {
      const lote = this.lotes.find(l => String(l.loteId) === String(this.selectedLoteId));
      if (lote && lote.granjaId) {
        this.granjaIdActual = lote.granjaId;
        this.inventarioUbicacionActual = getInventarioUbicacionFromLote(lote);
        this.cargarInventarioGranja(lote.granjaId, undefined, this.inventarioUbicacionActual);
      }
    } else {
      this.granjaIdActual = null;
      this.alimentosCatalog = [];
    }

    // Pollo engorde: iniciar con al menos una fila de alimento por sexo.
    this.ensureDefaultFoodRows();
    this.applyEditModeFieldLocks();
  }

  // ================== MÉTODOS PARA MANEJAR FORMARRAY DE ÍTEMS ==================

  get itemsHembrasArray(): FormArray {
    return this.form.get('itemsHembras') as FormArray;
  }

  get itemsMachosArray(): FormArray {
    return this.form.get('itemsMachos') as FormArray;
  }

  agregarItemHembras(): void {
    const soloAlimento = this.hembrasSoloAlimento;
    const itemForm = this.fb.group({
      tipoItem: [soloAlimento ? 'alimento' : null, Validators.required],
      catalogItemId: [null],
      cantidad: [0, [Validators.min(0)]],
      unidad: ['kg', Validators.required]
    });

    if (!soloAlimento) {
      // Cuando cambia el tipo de ítem, actualizar la unidad automáticamente
      itemForm.get('tipoItem')?.valueChanges.subscribe(tipo => {
        if (tipo === 'alimento') {
          itemForm.patchValue({ unidad: 'kg' }, { emitEvent: false });
        } else if (tipo) {
          itemForm.patchValue({ unidad: 'unidades' }, { emitEvent: false });
        }
        // Limpiar el catalogItemId cuando cambia el tipo
        itemForm.patchValue({ catalogItemId: null }, { emitEvent: false });
      });
    } else {
      itemForm.patchValue({ unidad: 'kg' }, { emitEvent: false });
    }

    this.itemsHembrasArray.push(itemForm);
  }

  /** Tipo de ítem usado para filtrar el catálogo en la fila Hembras (engorde = siempre alimento). */
  tipoItemFiltroHembra(itemGroup: FormGroup): string | null {
    if (this.hembrasSoloAlimento) return 'alimento';
    return itemGroup.get('tipoItem')?.value ?? null;
  }

  /** Tipo de ítem usado para filtrar el catálogo en la fila Machos (engorde = siempre alimento). */
  tipoItemFiltroMacho(itemGroup: FormGroup): string | null {
    if (this.hembrasSoloAlimento) return 'alimento';
    return itemGroup.get('tipoItem')?.value ?? null;
  }

  eliminarItemHembras(index: number): void {
    this.itemsHembrasArray.removeAt(index);
  }

  agregarItemMachos(): void {
    const soloAlimento = this.hembrasSoloAlimento;
    const itemForm = this.fb.group({
      tipoItem: [soloAlimento ? 'alimento' : null, Validators.required],
      catalogItemId: [null],
      cantidad: [0, [Validators.min(0)]],
      unidad: ['kg', Validators.required]
    });

    if (!soloAlimento) {
      // Cuando cambia el tipo de ítem, actualizar la unidad automáticamente
      itemForm.get('tipoItem')?.valueChanges.subscribe(tipo => {
        if (tipo === 'alimento') {
          itemForm.patchValue({ unidad: 'kg' }, { emitEvent: false });
        } else if (tipo) {
          itemForm.patchValue({ unidad: 'unidades' }, { emitEvent: false });
        }
        // Limpiar el catalogItemId cuando cambia el tipo
        itemForm.patchValue({ catalogItemId: null }, { emitEvent: false });
      });
    } else {
      itemForm.patchValue({ unidad: 'kg' }, { emitEvent: false });
    }

    this.itemsMachosArray.push(itemForm);
  }

  eliminarItemMachos(index: number): void {
    this.itemsMachosArray.removeAt(index);
  }

  /** En engorde (hembrasSoloAlimento), asegura una fila inicial de alimento por sexo. */
  private ensureDefaultFoodRows(): void {
    if (!this.hembrasSoloAlimento) return;
    while (this.itemsHembrasArray.length === 0) this.agregarItemHembras();
    // En modo UI engorde simplificado mostramos una sola lista visible:
    // migrar cualquier item de machos a la lista visible antes de limpiar.
    if (this.itemsMachosArray.length > 0) {
      const machos = [...this.itemsMachosArray.controls];
      machos.forEach(c => this.itemsHembrasArray.push(c));
      while (this.itemsMachosArray.length > 0) this.itemsMachosArray.removeAt(this.itemsMachosArray.length - 1);
    }
  }

  // Obtener alimentos filtrados para un ítem específico
  // Ecuador/Panamá: filtra por concepto (tipo ítem = conceptos de item_inventario_ecuador); mismo criterio en tabs hembras y machos
  /** `selectedCatalogId`: id ya elegido en la fila; se mantiene en la lista aunque el stock sea 0 (p. ej. edición). */
  /**
   * Caché de referencias estables por (tipoItem|idSeleccionado). El template usa este método
   * dentro de un *ngFor; devolver un array nuevo en cada ciclo de detección de cambios provocaba
   * NG0103 (Infinite change detection). Se recalcula el contenido, pero si el resultado es igual
   * (mismos ids en el mismo orden) se devuelve la MISMA referencia previa para no romper el CD.
   */
  private readonly _alimentosFiltradosCache = new Map<string, CatalogItemDto[]>();

  getAlimentosFiltradosPorTipo(tipoItem: string | null, selectedCatalogId?: number | string | null): CatalogItemDto[] {
    const computed = this.computeAlimentosFiltradosPorTipo(tipoItem, selectedCatalogId);
    const key = `${tipoItem ?? ''}|${selectedCatalogId ?? ''}`;
    const prev = this._alimentosFiltradosCache.get(key);
    if (prev && prev.length === computed.length && prev.every((a, i) => a.id === computed[i].id)) {
      return prev;
    }
    this._alimentosFiltradosCache.set(key, computed);
    return computed;
  }

  private computeAlimentosFiltradosPorTipo(tipoItem: string | null, selectedCatalogId?: number | string | null): CatalogItemDto[] {
    let list: CatalogItemDto[];
    if (this.isEcuadorOrPanama && this.itemsEcuadorPanama.length > 0) {
      const c = (tipoItem ?? '').trim().toLowerCase();
      if (!c) {
        list = this.itemsEcuadorPanama.map(i => itemEcuadorToCatalogItem(i));
      } else {
        list = this.itemsEcuadorPanama
          .filter(i => ((i.concepto ?? i.tipoItem ?? '').trim().toLowerCase() === c))
          .map(i => itemEcuadorToCatalogItem(i));
      }
    } else if (!tipoItem || !this.granjaIdActual) {
      list = this.alimentosCatalog;
    } else {
      list = this.alimentosCatalog.filter(a => {
        const metadata = a.metadata;
        const itemType = metadata?.type_item || metadata?.itemType;
        return metadata && itemType === tipoItem;
      });
    }
    return this.filtrarAlimentosConStockDisponible(list, selectedCatalogId);
  }

  /**
   * Solo ítems con existencia &gt; 0 en inventario (misma fuente que "Disponible actual").
   * El id seleccionado en la fila siempre se incluye para no romper edición o valor ya guardado.
   */
  private filtrarAlimentosConStockDisponible(items: CatalogItemDto[], selectedCatalogId?: number | string | null): CatalogItemDto[] {
    const sel = normalizarIdCatalogoSeleccion(selectedCatalogId);
    return items.filter(a => {
      if (sel != null && a.id === sel) return true;
      return this.itemTieneStockDisponibleEnUbicacion(a.id);
    });
  }

  /** True si hay cantidad &gt; 0 en `inventarioPorItem` para el ítem (kg/g según backend). */
  private itemTieneStockDisponibleEnUbicacion(catalogItemId: number | null | undefined): boolean {
    if (catalogItemId == null) return false;
    const d = this.getCantidadDisponible(catalogItemId);
    if (!d) return false;
    const q = Number(d.quantity);
    return Number.isFinite(q) && q > 0;
  }

  // Obtener cantidad disponible en inventario para un ítem
  getCantidadDisponible(catalogItemId: number | null | undefined): { quantity: number; unit: string } | null {
    if (!catalogItemId) return null;
    return this.inventarioPorItem.get(catalogItemId) || null;
  }

  getOriginalConsumoKg(catalogItemId: number | null | undefined): number {
    if (!catalogItemId) return 0;
    return this.originalConsumoKgByItem.get(Number(catalogItemId)) ?? 0;
  }

  private buildOriginalConsumoMap(itemsH: any[] | null | undefined, itemsM: any[] | null | undefined): void {
    const map = new Map<number, number>();
    const addArr = (arr: any[] | null | undefined) => {
      if (!arr || !Array.isArray(arr)) return;
      arr.forEach((it: any) => {
        const id = resolveItemCatalogId(it);
        if (!id) return;
        const cantidad = Number(it?.cantidad ?? it?.cantidadKg ?? 0);
        if (!Number.isFinite(cantidad) || cantidad <= 0) return;
        const kg = toKg(cantidad, it?.unidad ?? 'kg');
        map.set(id, (map.get(id) ?? 0) + kg);
      });
    };
    addArr(itemsH);
    addArr(itemsM);
    this.originalConsumoKgByItem = map;
  }

  getMaxPermitidoKg(catalogItemId: number | null | undefined): number | null {
    if (!catalogItemId) return null;
    const disponible = this.getCantidadDisponible(catalogItemId);
    if (!disponible) return null;
    const disponibleKg = toKg(Number(disponible.quantity || 0), disponible.unit);
    const originalKg = this.editing ? this.getOriginalConsumoKg(catalogItemId) : 0;
    return disponibleKg + originalKg;
  }

  /** Resumen API aves-disponibles (solo nuevo registro). */
  get muestraResumenAvesDisponibles(): boolean {
    return !this.editing && this.avesDisponibles != null;
  }

  get bloqueoHembrasPorAves(): boolean {
    return !this.editing && this.avesDisponibles != null && (this.avesDisponibles.hembrasDisponibles ?? 0) <= 0;
  }

  get bloqueoMachosPorAves(): boolean {
    return !this.editing && this.avesDisponibles != null && (this.avesDisponibles.machosDisponibles ?? 0) <= 0;
  }

  /** Panamá: sin aves mixtas disponibles (total H+M = 0). */
  get bloqueoMixtoPorAves(): boolean {
    if (!this.isPanama) return false;
    if (this.editing || this.avesDisponibles == null) return false;
    const mixtas = this.avesDisponibles.mixtasDisponibles ?? 0;
    return mixtas <= 0;
  }

  /** Nuevo registro: no queda ningún ave del sexo en el lote (API aves-disponibles). */
  get bloqueoAmbosSexosPorAves(): boolean {
    if (this.isPanama) return this.bloqueoMixtoPorAves;
    return this.bloqueoHembrasPorAves && this.bloqueoMachosPorAves;
  }

  /** True si la cantidad ingresada supera el disponible del ítem seleccionado. */
  cantidadExcedeDisponible(itemGroup: FormGroup): boolean {
    const catalogItemId = Number(itemGroup.get('catalogItemId')?.value);
    const cantidad = Number(itemGroup.get('cantidad')?.value || 0);
    const unidad = String(itemGroup.get('unidad')?.value || 'kg');
    if (!catalogItemId || cantidad <= 0) return false;
    const maxPermitidoKg = this.getMaxPermitidoKg(catalogItemId);
    if (maxPermitidoKg == null) return false; // si no hay dato, no bloquear aquí
    const qtyKg = toKg(cantidad, unidad);
    return qtyKg > maxPermitidoKg;
  }

  /** Bloquea guardar si cualquier fila de alimento supera el disponible (solo en creación; en edición no aplica). */
  get hasCantidadExcedida(): boolean {
    if (this.editing) return false;
    return this.itemsHembrasArray.controls.some(c => this.cantidadExcedeDisponible(c as FormGroup));
  }

  /** En edición pollo engorde: alimento, mortalidad, selección y error de sexaje quedan bloqueados (solo lectura). */
  get camposCalculoBloqueadosEnEdicion(): boolean {
    return !!this.editing && this.hembrasSoloAlimento;
  }

  /** El formulario muestra solo Fecha y Observaciones: en edición o cuando no hay aves disponibles. */
  get soloFechaObservacion(): boolean {
    return this.camposCalculoBloqueadosEnEdicion || this.bloqueoAmbosSexosPorAves;
  }

  /**
   * Días transcurridos desde fechaEncaset para la fecha seleccionada. -1 si no aplica.
   */
  private get diasDesdeEncaset(): number {
    const loteId = this.form?.get('loteId')?.value;
    const fechaStr = this.form?.get('fechaRegistro')?.value;
    if (!loteId || !fechaStr) return -1;
    const lote = this.lotes.find(l => String(l.loteId) === String(loteId));
    if (!lote?.fechaEncaset) return -1;
    const encaset = new Date(lote.fechaEncaset.substring(0, 10) + 'T00:00:00');
    const registro = new Date(fechaStr + 'T00:00:00');
    if (isNaN(encaset.getTime()) || isNaN(registro.getTime())) return -1;
    return Math.round((registro.getTime() - encaset.getTime()) / (1000 * 60 * 60 * 24));
  }

  /**
   * True si la fecha está dentro de los primeros 7 días desde fechaEncaset (días 1–7).
   * En este período el peso es obligatorio cada día.
   */
  get esPrimeraSemana(): boolean {
    if (this.editing) return false;
    const d = this.diasDesdeEncaset;
    return d >= 1 && d <= 7;
  }

  /**
   * True si la fecha del registro cae en un día de pesaje obligatorio:
   * - Días 1–7 desde fechaEncaset: obligatorio cada día (primera semana).
   * - A partir del día 8: obligatorio cada múltiplo de 7 (día 14, 21, 28…).
   * Solo aplica en nuevo registro, no en edición.
   */
  get esDiaPesoObligatorio(): boolean {
    if (this.editing) return false;
    const d = this.diasDesdeEncaset;
    if (d < 0) return false;
    return (d >= 1 && d <= 7) || (d > 7 && d % 7 === 0);
  }

  /** True si es día semanal de pesaje obligatorio pero falta el peso en algún sexo activo. */
  get pesoPendienteEnDiaSemanal(): boolean {
    if (!this.esDiaPesoObligatorio) return false;
    if (this.isPanama) {
      const pesoMixto = this.form?.get('pesoPromMixto')?.value;
      return !this.bloqueoMixtoPorAves && (pesoMixto === null || pesoMixto === '' || pesoMixto === undefined);
    }
    const pesoH = this.form?.get('pesoPromH')?.value;
    const pesoM = this.form?.get('pesoPromM')?.value;
    const faltaH = !this.bloqueoHembrasPorAves && (pesoH === null || pesoH === '' || pesoH === undefined);
    const faltaM = !this.bloqueoMachosPorAves && (pesoM === null || pesoM === '' || pesoM === undefined);
    return faltaH || faltaM;
  }

  // Obtener texto completo del ítem con cantidad disponible para mostrar en el dropdown
  getItemDisplayText(item: CatalogItemDto): string {
    const cantidad = this.getCantidadDisponible(item.id);
    if (cantidad) {
      return `${item.codigo} — ${item.nombre} (Disponible: ${cantidad.quantity.toFixed(2)} ${cantidad.unit})`;
    }
    return `${item.codigo} — ${item.nombre}`;
  }

  // Ya no necesitamos actualizar consumoKg aquí, el backend lo hace
  // Mantenemos el método por compatibilidad pero no hace nada
  private actualizarConsumoKg(tipo: 'hembras' | 'machos'): void {
    // El backend ahora hace la conversión, solo mantenemos los valores para validación de inventario
    // No necesitamos actualizar consumoKgHembras/Machos ya que el backend los calcula
  }

  /** Metadata puede venir como objeto, string JSON o con claves snake_case desde la API. */
  private normalizeSeguimientoMetadata(editing: SeguimientoLoteLevanteDto): any {
    return normalizeJsonField(editing.metadata) ?? {};
  }

  private populateForm(): void {
    if (!this.editing) return;
    this.originalConsumoKgByItem.clear();

    while (this.itemsHembrasArray.length > 0) {
      this.itemsHembrasArray.removeAt(0);
    }
    while (this.itemsMachosArray.length > 0) {
      this.itemsMachosArray.removeAt(0);
    }

    // Leer consumo original desde Metadata JSONB (compatibilidad hacia atrás)
    const metadata: any = this.normalizeSeguimientoMetadata(this.editing);
    const consumoHembras = metadata?.consumoOriginalHembras ?? this.editing.consumoKgHembras ?? 0;
    const unidadConsumoHembras = metadata?.unidadConsumoOriginalHembras ?? 'kg';
    const consumoMachos = metadata?.consumoOriginalMachos ?? this.editing.consumoKgMachos ?? null;
    const unidadConsumoMachos = metadata?.unidadConsumoOriginalMachos ?? 'kg';

    // Leer tipo de ítem y IDs de alimentos desde Metadata (compatibilidad hacia atrás)
    const tipoItemHembras = metadata?.tipoItemHembras || null;
    const tipoItemMachos = metadata?.tipoItemMachos || null;
    const tipoAlimentoHembras = metadata?.tipoAlimentoHembras ?? this.editing.tipoAlimentoHembras ?? null;
    const tipoAlimentoMachos = metadata?.tipoAlimentoMachos ?? this.editing.tipoAlimentoMachos ?? null;

    // Fallback para registros antiguos sin itemsHembras/itemsMachos en metadata.
    if (this.originalConsumoKgByItem.size === 0) {
      if (tipoAlimentoHembras && consumoHembras > 0) {
        const kgH = toKg(Number(consumoHembras), unidadConsumoHembras);
        this.originalConsumoKgByItem.set(Number(tipoAlimentoHembras), kgH);
      }
      if (tipoAlimentoMachos && consumoMachos && consumoMachos > 0) {
        const kgM = toKg(Number(consumoMachos), unidadConsumoMachos);
        const idM = Number(tipoAlimentoMachos);
        this.originalConsumoKgByItem.set(idM, (this.originalConsumoKgByItem.get(idM) ?? 0) + kgM);
      }
    }

    // Cargar ítems en FormArrays
    // Primero, verificar si hay itemsHembras/itemsMachos en metadata (nuevo formato)
    const itemsHembrasFromMetadata =
      metadata?.itemsHembras ?? metadata?.items_hembras ?? metadata?.ItemsHembras;
    const itemsMachosFromMetadata =
      metadata?.itemsMachos ?? metadata?.items_machos ?? metadata?.ItemsMachos;
    this.buildOriginalConsumoMap(itemsHembrasFromMetadata, itemsMachosFromMetadata);

    // Si hay items en metadata, cargarlos todos (incluyendo alimentos)
    // IMPORTANTE: El tipoItem debe venir desde metadata, si no está, se obtendrá del inventario después
    if (itemsHembrasFromMetadata && Array.isArray(itemsHembrasFromMetadata)) {
      itemsHembrasFromMetadata.forEach((item: any) => {
        // Obtener tipoItem desde el item (debe estar guardado en metadata)
        // Si no está, se actualizará después cuando se cargue el inventario
        const tipoItem = item.tipoItem || 'alimento'; // Fallback temporal, se actualizará después
        if (this.hembrasSoloAlimento && String(tipoItem).trim().toLowerCase() !== 'alimento') {
          return; // Pollo engorde: pestaña hembras solo alimentos
        }

        const cid = resolveItemCatalogId(item);
        if (cid == null) return;

        this.itemsHembrasArray.push(this.fb.group({
          tipoItem: [tipoItem, Validators.required],
          catalogItemId: [cid, Validators.required],
          cantidad: [item.cantidad ?? item.cantidadKg ?? consumoHembras, [Validators.required, Validators.min(0)]],
          unidad: [item.unidad || unidadConsumoHembras || 'kg', Validators.required]
        }));
      });
    } else {
      // Compatibilidad hacia atrás: agregar alimentos si existen
      if (tipoAlimentoHembras && (tipoItemHembras === 'alimento' || !tipoItemHembras) && consumoHembras > 0) {
        this.itemsHembrasArray.push(this.fb.group({
          tipoItem: ['alimento', Validators.required],
          catalogItemId: [tipoAlimentoHembras, Validators.required],
          cantidad: [consumoHembras, [Validators.required, Validators.min(0)]],
          unidad: [unidadConsumoHembras, Validators.required]
        }));
      }
    }

    if (itemsMachosFromMetadata && Array.isArray(itemsMachosFromMetadata)) {
      itemsMachosFromMetadata.forEach((item: any) => {
        // Obtener tipoItem desde el item (debe estar guardado en metadata)
        // Si no está, se actualizará después cuando se cargue el inventario
        const tipoItem = item.tipoItem || 'alimento'; // Fallback temporal, se actualizará después

        const cid = resolveItemCatalogId(item);
        if (cid == null) return;

        this.itemsMachosArray.push(this.fb.group({
          tipoItem: [tipoItem, Validators.required],
          catalogItemId: [cid, Validators.required],
          cantidad: [item.cantidad ?? item.cantidadKg ?? consumoMachos ?? 0, [Validators.required, Validators.min(0)]],
          unidad: [item.unidad || unidadConsumoMachos || 'kg', Validators.required]
        }));
      });
    } else {
      // Compatibilidad hacia atrás: agregar alimentos si existen
      if (tipoAlimentoMachos && (tipoItemMachos === 'alimento' || !tipoItemMachos) && consumoMachos && consumoMachos > 0) {
        this.itemsMachosArray.push(this.fb.group({
          tipoItem: ['alimento', Validators.required],
          catalogItemId: [tipoAlimentoMachos, Validators.required],
          cantidad: [consumoMachos, [Validators.required, Validators.min(0)]],
          unidad: [unidadConsumoMachos, Validators.required]
        }));
      }
    }

    // itemsAdicionales solo contiene ítems NO alimentos; metadata.itemsHembras/Machos ya tiene TODOS los ítems.
    // Solo cargar desde itemsAdicionales si no había items en metadata (registros antiguos).
    const itemsAdicionales: any = normalizeJsonField(this.editing.itemsAdicionales);
    const yaCargamosHembras = (itemsHembrasFromMetadata && itemsHembrasFromMetadata.length > 0);
    const yaCargamosMachos = (itemsMachosFromMetadata && itemsMachosFromMetadata.length > 0);

    if (itemsAdicionales && !yaCargamosHembras) {
      if (itemsAdicionales.itemsHembras && Array.isArray(itemsAdicionales.itemsHembras)) {
        itemsAdicionales.itemsHembras.forEach((item: ItemSeguimientoDto) => {
          if (this.hembrasSoloAlimento && String(item.tipoItem || '').trim().toLowerCase() !== 'alimento') {
            return;
          }
          const cid = resolveItemCatalogId(item as any) ?? item.catalogItemId;
          if (cid == null) return;
          this.itemsHembrasArray.push(this.fb.group({
            tipoItem: [item.tipoItem, Validators.required],
            catalogItemId: [cid, Validators.required],
            cantidad: [item.cantidad, [Validators.required, Validators.min(0)]],
            unidad: [item.unidad || 'unidades', Validators.required]
          }));
        });
      }
    }
    if (itemsAdicionales && !yaCargamosMachos) {
      if (itemsAdicionales.itemsMachos && Array.isArray(itemsAdicionales.itemsMachos)) {
        itemsAdicionales.itemsMachos.forEach((item: ItemSeguimientoDto) => {
          const cid = resolveItemCatalogId(item as any) ?? item.catalogItemId;
          if (cid == null) return;
          this.itemsMachosArray.push(this.fb.group({
            tipoItem: [item.tipoItem, Validators.required],
            catalogItemId: [cid, Validators.required],
            cantidad: [item.cantidad, [Validators.required, Validators.min(0)]],
            unidad: [item.unidad || 'unidades', Validators.required]
          }));
        });
      }
    }

    this.ensureDefaultFoodRows();

    this.form.patchValue({
      fechaRegistro: toYMD(this.editing.fechaRegistro),
      loteId: this.editing.loteId,
      mortalidadHembras: this.editing.mortalidadHembras,
      mortalidadMachos: this.editing.mortalidadMachos,
      selH: this.editing.selH,
      selM: this.editing.selM,
      errorSexajeHembras: this.editing.errorSexajeHembras,
      errorSexajeMachos: this.editing.errorSexajeMachos,
      tipoAlimento: this.editing.tipoAlimento ?? '',
      observaciones: this.editing.observaciones || '',
      ciclo: this.editing.ciclo || 'Normal',
      pesoPromH: this.editing.pesoPromH ?? null,
      pesoPromM: this.editing.pesoPromM ?? null,
      uniformidadH: this.editing.uniformidadH ?? null,
      uniformidadM: this.editing.uniformidadM ?? null,
      cvH: this.editing.cvH ?? null,
      cvM: this.editing.cvM ?? null,
      // Campos de agua (solo para Ecuador y Panamá)
      consumoAguaDiario: this.editing.consumoAguaDiario ?? null,
      consumoAguaPh: this.editing.consumoAguaPh ?? null,
      consumoAguaOrp: this.editing.consumoAguaOrp ?? null,
      consumoAguaTemperatura: this.editing.consumoAguaTemperatura ?? null,
      // Campos específicos Panamá: quintales
      qqMixtas: this.editing.qqMixtas ?? null,
      qqHembras: this.editing.qqHembras ?? null,
      qqMachos: this.editing.qqMachos ?? null,
      // Campos Mixto Panamá: H+M acumulados (compat. con registros históricos H/M y nuevos H=mixto,M=0)
      mortalidadMixtas: (this.editing.mortalidadHembras ?? 0) + (this.editing.mortalidadMachos ?? 0),
      selMixtas: (this.editing.selH ?? 0) + (this.editing.selM ?? 0),
      errorSexajeMixtas: (this.editing.errorSexajeHembras ?? 0) + (this.editing.errorSexajeMachos ?? 0),
      pesoPromMixto: this.editing.pesoPromH ?? this.editing.pesoPromM ?? null,
      uniformidadMixta: this.editing.uniformidadH ?? this.editing.uniformidadM ?? null,
      cvMixto: this.editing.cvH ?? this.editing.cvM ?? null,
    });

    // Si hay alimento seleccionado, necesitamos cargar el catálogo primero para obtener el tipo de ítem
    // Esto se hará después de que se cargue el inventario de la granja
    const loteId = this.editing.loteId;
    const lote = this.lotes.find(l => String(l.loteId) === String(loteId));

    if (lote && lote.granjaId) {
      this.granjaIdActual = lote.granjaId;
      this.inventarioUbicacionActual = getInventarioUbicacionFromLote(lote);

      // Cargar inventario de la granja (y núcleo/galpón del lote en EC/PA)
      this.cargarInventarioGranja(lote.granjaId, undefined, this.inventarioUbicacionActual);

      // Después de cargar el inventario, establecer el tipo de ítem y alimento
      // Esto se hará en el callback de cargarInventarioGranja
      setTimeout(() => {
        this.establecerTipoItemYAlimentoAlEditar();
      }, 500); // Dar tiempo para que se cargue el inventario
    } else {
      // Si no hay granja, intentar obtener el tipo desde el catálogo completo
      this.establecerTipoItemYAlimentoAlEditar();
    }

    this.applyEditModeFieldLocks();
  }

  private setAllFormControlsEnabled(enabled: boolean): void {
    Object.keys(this.form.controls).forEach(key => {
      const c = this.form.get(key);
      if (!c) return;
      if (key === 'itemsHembras' || key === 'itemsMachos') {
        const fa = c as FormArray;
        fa.controls.forEach(ctrl => {
          if (enabled) ctrl.enable({ emitEvent: false });
          else ctrl.disable({ emitEvent: false });
        });
      } else {
        if (enabled) c.enable({ emitEvent: false });
        else c.disable({ emitEvent: false });
      }
    });
  }

  /**
   * Nuevo registro: todo habilitado.
   * Edición levante (no engorde): todo habilitado.
   * Edición pollo engorde: solo lectura en alimento (FormArrays), mortalidad, selección y error de sexaje.
   */
  private applyEditModeFieldLocks(): void {
    if (!this.editing) {
      this.setAllFormControlsEnabled(true);
      this.aplicarBloqueoPorAvesDisponibles();
      return;
    }
    if (!this.hembrasSoloAlimento) {
      this.setAllFormControlsEnabled(true);
      return;
    }
    this.setAllFormControlsEnabled(true);
    const bloqueados = [
      'mortalidadHembras',
      'mortalidadMachos',
      'selH',
      'selM',
      'errorSexajeHembras',
      'errorSexajeMachos',
      // Campos Mixto Panamá
      'mortalidadMixtas',
      'selMixtas',
      'errorSexajeMixtas',
    ];
    bloqueados.forEach(k => this.form.get(k)?.disable({ emitEvent: false }));
    this.itemsHembrasArray.controls.forEach(ctrl => ctrl.disable({ emitEvent: false }));
    this.itemsMachosArray.controls.forEach(ctrl => ctrl.disable({ emitEvent: false }));
  }

  /**
   * Nuevo registro: si no hay aves disponibles por sexo, deshabilita mortalidad/selección/error de sexaje
   * en esa columna y fija 0. Si `avesDisponibles` es null (API aún no cargó), no bloquea.
   */
  private aplicarBloqueoPorAvesDisponibles(): void {
    if (this.editing) return;
    if (!this.form) return;
    const adv = this.avesDisponibles;

    // Panamá: bloqueo sobre la columna Mixto única
    if (this.isPanama) {
      const mixOk = adv == null || (adv.mixtasDisponibles ?? 0) > 0;
      const mixtaKeys = [
        'mortalidadMixtas', 'selMixtas', 'errorSexajeMixtas',
        'pesoPromMixto', 'uniformidadMixta', 'cvMixto'
      ] as const;
      const esPeso = (k: string) => ['pesoPromMixto', 'uniformidadMixta', 'cvMixto'].includes(k);
      for (const k of mixtaKeys) {
        const c = this.form.get(k);
        if (!c) continue;
        if (mixOk) {
          c.enable({ emitEvent: false });
        } else {
          c.setValue(esPeso(k) ? null : 0, { emitEvent: false });
          c.disable({ emitEvent: false });
        }
      }
      return;
    }

    // Resto de países: bloqueo por columna H/M
    const hOk = adv == null || (adv.hembrasDisponibles ?? 0) > 0;
    const mOk = adv == null || (adv.machosDisponibles ?? 0) > 0;
    const hemKeys = [
      'mortalidadHembras', 'selH', 'errorSexajeHembras',
      'pesoPromH', 'uniformidadH', 'cvH'
    ] as const;
    const machKeys = [
      'mortalidadMachos', 'selM', 'errorSexajeMachos',
      'pesoPromM', 'uniformidadM', 'cvM'
    ] as const;
    const valorBloqueo = (key: string) =>
      ['pesoPromH', 'pesoPromM', 'uniformidadH', 'uniformidadM', 'cvH', 'cvM'].includes(key) ? null : 0;
    for (const k of hemKeys) {
      const c = this.form.get(k);
      if (!c) continue;
      if (hOk) { c.enable({ emitEvent: false }); }
      else { c.setValue(valorBloqueo(k), { emitEvent: false }); c.disable({ emitEvent: false }); }
    }
    for (const k of machKeys) {
      const c = this.form.get(k);
      if (!c) continue;
      if (mOk) { c.enable({ emitEvent: false }); }
      else { c.setValue(valorBloqueo(k), { emitEvent: false }); c.disable({ emitEvent: false }); }
    }
  }

  private establecerTipoItemYAlimentoAlEditar(): void {
    if (!this.editing) return;

    const metadata: any = this.normalizeSeguimientoMetadata(this.editing);

    // Leer tipo de ítem desde Metadata (si está guardado)
    const tipoItemHembrasFromMetadata = metadata?.tipoItemHembras;
    const tipoItemMachosFromMetadata = metadata?.tipoItemMachos;
    const tipoAlimentoHembrasFromMetadata = metadata?.tipoAlimentoHembras ?? this.editing.tipoAlimentoHembras;
    const tipoAlimentoMachosFromMetadata = metadata?.tipoAlimentoMachos ?? this.editing.tipoAlimentoMachos;

    // Para hembras
    if (tipoAlimentoHembrasFromMetadata) {
      const alimentoId = Number(tipoAlimentoHembrasFromMetadata);

      // Si tenemos el tipo de ítem desde Metadata, usarlo directamente
      if (tipoItemHembrasFromMetadata) {
        this.form.patchValue({ tipoItemHembras: tipoItemHembrasFromMetadata }, { emitEvent: false });
        this.filtrarAlimentosPorTipo('hembras', tipoItemHembrasFromMetadata);
      }

      // Intentar obtener desde el catálogo cargado
      let alimento = this.alimentosById.get(alimentoId);

      // Si no está en el catálogo cargado, intentar cargarlo desde el servicio
      if (!alimento) {
        this.catalogSvc.getById(alimentoId).subscribe({
          next: (item) => {
            if (item) {
              // Agregar al catálogo temporalmente
              this.alimentosById.set(item.id!, item);
              // Si no teníamos el tipo desde Metadata, obtenerlo del catálogo
              if (!tipoItemHembrasFromMetadata && item.metadata?.type_item) {
                this.form.patchValue({ tipoItemHembras: item.metadata?.type_item }, { emitEvent: false });
                this.filtrarAlimentosPorTipo('hembras', item.metadata?.type_item);
              }
              this.consultarInventario('hembras', alimentoId);
            }
          },
          error: (err) => {
            console.error('Error al cargar alimento para edición:', err);
          }
        });
      } else {
        // Si ya está en el catálogo, establecer directamente
        // Solo actualizar tipo de ítem si no lo teníamos desde Metadata
        if (!tipoItemHembrasFromMetadata && alimento.metadata?.type_item) {
          this.form.patchValue({ tipoItemHembras: alimento.metadata?.type_item }, { emitEvent: false });
          this.filtrarAlimentosPorTipo('hembras', alimento.metadata?.type_item);
        }
        this.consultarInventario('hembras', alimentoId);
      }
    }

    // Para machos
    if (tipoAlimentoMachosFromMetadata) {
      const alimentoId = Number(tipoAlimentoMachosFromMetadata);

      // Si tenemos el tipo de ítem desde Metadata, usarlo directamente
      if (tipoItemMachosFromMetadata) {
        this.form.patchValue({ tipoItemMachos: tipoItemMachosFromMetadata }, { emitEvent: false });
        this.filtrarAlimentosPorTipo('machos', tipoItemMachosFromMetadata);
      }

      let alimento = this.alimentosById.get(alimentoId);

      if (!alimento) {
        this.catalogSvc.getById(alimentoId).subscribe({
          next: (item) => {
            if (item) {
              this.alimentosById.set(item.id!, item);
              // Si no teníamos el tipo desde Metadata, obtenerlo del catálogo
              if (!tipoItemMachosFromMetadata && item.metadata?.type_item) {
                this.form.patchValue({ tipoItemMachos: item.metadata?.type_item }, { emitEvent: false });
                this.filtrarAlimentosPorTipo('machos', item.metadata?.type_item);
              }
              this.consultarInventario('machos', alimentoId);
            }
          },
          error: (err) => {
            console.error('Error al cargar alimento para edición:', err);
          }
        });
      } else {
        // Solo actualizar tipo de ítem si no lo teníamos desde Metadata
        if (!tipoItemMachosFromMetadata && alimento.metadata?.type_item) {
          this.form.patchValue({ tipoItemMachos: alimento.metadata?.type_item }, { emitEvent: false });
          this.filtrarAlimentosPorTipo('machos', alimento.metadata?.type_item);
        }
        this.consultarInventario('machos', alimentoId);
      }
    }
  }

  // ================== CARGA DE INVENTARIO DE LA GRANJA ==================
  private cargarInventarioGranja(
    granjaId: number,
    itemType?: string | null,
    ubicacion?: { nucleoId: string | null; galponId: string | null }
  ): void {
    if (ubicacion !== undefined) {
      this.inventarioUbicacionActual = {
        nucleoId: ubicacion.nucleoId ?? null,
        galponId: ubicacion.galponId ?? null
      };
    }
    const loadId = ++this.inventarioLoadId;
    this.cargandoInventarioGranja = true;

    // Ecuador/Panamá: catálogo desde item_inventario_ecuador (conceptos como tipo ítem) y stock desde inventario-gestion
    if (this.isEcuadorOrPanama) {
      this.cargarCatalogEcuadorPanama(granjaId, loadId, this.inventarioUbicacionActual);
      return;
    }

    // Cargar inventario de la granja filtrado por itemType desde el backend
    // El backend filtra por empresa, país, granja y tipo de item
    this.inventarioSvc.getInventory(granjaId, itemType).subscribe({
      next: (inventario) => {
        if (loadId !== this.inventarioLoadId) return;
        

        // Si no viene catalogItemMetadata, necesitamos cargarlo desde el catálogo
        const itemsSinMetadata = inventario.filter(item =>
          item.active &&
          item.quantity > 0 &&
          (!item.catalogItemMetadata || Object.keys(item.catalogItemMetadata).length === 0)
        );

        if (itemsSinMetadata.length > 0) {
          // Cargar metadata de los CatalogItems que no lo tienen
          const catalogItemIds = itemsSinMetadata.map(item => item.catalogItemId);
          const catalogRequests = catalogItemIds.map(id =>
            this.catalogSvc.getById(id).pipe(
              catchError(err => {
                console.warn(`No se pudo cargar CatalogItem ${id}:`, err);
                return of(null);
              })
            )
          );

          forkJoin(catalogRequests).subscribe(catalogItems => {
            if (loadId !== this.inventarioLoadId) return;
            const catalogItemsMap = new Map<number, CatalogItemDto>();
            catalogItems.forEach(item => {
              if (item && item.id) {
                catalogItemsMap.set(item.id, item);
              }
            });

            // Guardar información de inventario por ítem
            this.inventarioPorItem.clear();
            inventario.forEach(item => {
              if (item.active && item.quantity > 0) {
                this.inventarioPorItem.set(item.catalogItemId, {
                  quantity: item.quantity,
                  unit: item.unit || 'kg'
                });
              }
            });

            // Convertir inventario a formato CatalogItemDto
            this.alimentosCatalog = inventario
              .filter(item => item.active && item.quantity > 0)
              .map(item => {
                const catalogItem = catalogItemsMap.get(item.catalogItemId);
                const metadata = item.catalogItemMetadata || catalogItem?.metadata || item.metadata || {};
                // El backend ya incluye itemType en catalogItemMetadata, asegurarnos de que esté disponible
                const itemType = metadata.itemType || metadata.type_item || catalogItem?.itemType || 'alimento';
                if (!metadata.itemType && !metadata.type_item) {
                  metadata.itemType = itemType;
                }
                return {
                  id: item.catalogItemId,
                  codigo: item.codigo,
                  nombre: item.nombre,
                  metadata: metadata,
                  itemType: itemType,
                  activo: item.active
                } as CatalogItemDto;
              })
              .sort((a, b) =>
                (a.nombre || '').localeCompare(b.nombre || '', 'es', { numeric: true, sensitivity: 'base' })
              );

            this.actualizarMapasYFiltros();
            if (loadId === this.inventarioLoadId) this.cargandoInventarioGranja = false;

            // Después de cargar el inventario, actualizar los tipos de ítem de los items cargados
            this.actualizarTiposItemDesdeInventario();
          });
        } else {
          if (loadId !== this.inventarioLoadId) return;
          // Guardar información de inventario por ítem
          this.inventarioPorItem.clear();
          inventario.forEach(item => {
            if (item.active && item.quantity > 0) {
              this.inventarioPorItem.set(item.catalogItemId, {
                quantity: item.quantity,
                unit: item.unit || 'kg'
              });
            }
          });

          // Todos los items tienen catalogItemMetadata, procesar directamente
          this.alimentosCatalog = inventario
            .filter(item => item.active && item.quantity > 0)
            .map(item => {
              const metadata = item.catalogItemMetadata || item.metadata || {};
              // Asegurar que el tipoItem esté disponible
              const itemType = metadata.itemType || metadata.type_item || 'alimento';
              if (!metadata.itemType && !metadata.type_item) {
                metadata.itemType = itemType;
              }
              return {
                id: item.catalogItemId,
                codigo: item.codigo,
                nombre: item.nombre,
                metadata: metadata,
                itemType: itemType,
                activo: item.active
              } as CatalogItemDto;
            })
            .sort((a, b) =>
              (a.nombre || '').localeCompare(b.nombre || '', 'es', { numeric: true, sensitivity: 'base' })
            );

          this.actualizarMapasYFiltros();
          if (loadId === this.inventarioLoadId) this.cargandoInventarioGranja = false;

          // Después de cargar el inventario, actualizar los tipos de ítem de los items cargados
          this.actualizarTiposItemDesdeInventario();
        }
      },
      error: (err) => {
        if (loadId !== this.inventarioLoadId) return;
        console.error('Error al cargar inventario de la granja:', err);
        this.alimentosCatalog = [];
        this.alimentosFiltradosHembras = [];
        this.alimentosFiltradosMachos = [];
        this.cargandoInventarioGranja = false;
      }
    });
  }

  /** Ecuador/Panamá: carga catálogo item_inventario_ecuador y conceptos; luego stock por ubicación (granja y opc. núcleo/galpón del lote). */
  private cargarCatalogEcuadorPanama(
    granjaId: number,
    loadId: number,
    u: { nucleoId: string | null; galponId: string | null }
  ): void {
    this.gestionInventarioSvc.getItemsByType(null, null, true).pipe(
      catchError(err => { console.error('Error al cargar ítems inventario Ecuador:', err); return of([]); })
    ).subscribe((list: ItemInventarioDto[]) => {
      if (loadId !== this.inventarioLoadId) return;
      this.itemsEcuadorPanama = list ?? [];
      this.conceptosEcuadorPanama = Array.from(
        new Set(
          this.itemsEcuadorPanama
            .map(i => (i.concepto ?? i.tipoItem ?? '').trim())
            .filter(x => !!x)
        )
      ).sort((a, b) => a.localeCompare(b));
      this.cargarStockEcuadorPanama(granjaId, loadId, u);
    });
  }

  /** Ecuador/Panamá: stock del galpón (y núcleo) del lote; si no hay galpón en el lote, se mantiene el total granja. */
  private cargarStockEcuadorPanama(
    granjaId: number,
    loadId: number,
    u: { nucleoId: string | null; galponId: string | null }
  ): void {
    const stockParams: { farmId: number; nucleoId?: string; galponId?: string } = { farmId: granjaId };
    if (u.nucleoId) stockParams.nucleoId = u.nucleoId;
    if (u.galponId) stockParams.galponId = u.galponId;
    this.gestionInventarioSvc.getStock(stockParams).pipe(
      catchError(err => { console.error('Error al cargar stock:', err); return of([]); })
    ).subscribe((rows: InventarioGestionStockDto[]) => {
      if (loadId !== this.inventarioLoadId) return;
      this.inventarioPorItem.clear();
      rows.forEach(r => {
        const prev = this.inventarioPorItem.get(r.itemInventarioEcuadorId);
        const q = prev ? prev.quantity + r.quantity : r.quantity;
        this.inventarioPorItem.set(r.itemInventarioEcuadorId, { quantity: q, unit: r.unit });
      });
      this.alimentosCatalog = this.itemsEcuadorPanama.map(i => itemEcuadorToCatalogItem(i))
        .sort((a, b) => (a.nombre || '').localeCompare(b.nombre || '', 'es', { numeric: true, sensitivity: 'base' }));
      this.actualizarMapasYFiltros();
      this.cargandoInventarioGranja = false;
      this.actualizarTiposItemDesdeInventario();
    });
  }

  /**
   * Actualiza los tipos de ítem de los items cargados en los FormArrays usando el inventario cargado
   */
  /**
   * Actualiza los tipos de ítem de los items cargados en los FormArrays usando el inventario cargado
   * Esto asegura que el tipoItem se muestre correctamente al editar
   */
  private actualizarTiposItemDesdeInventario(): void {
    // Actualizar items de hembras
    this.itemsHembrasArray.controls.forEach((control, index) => {
      const catalogItemId = control.get('catalogItemId')?.value;
      const tipoItemActual = control.get('tipoItem')?.value;

      if (this.hembrasSoloAlimento) {
        control.patchValue({ tipoItem: 'alimento' }, { emitEvent: false });
        this.filtrarAlimentosPorTipo('hembras', 'alimento');
        // No borrar catalogItemId: el concepto en catálogo puede no ser el string exacto "alimento"
        return;
      }

      if (catalogItemId) {
        let tipoItem: string | undefined;
        if (this.isEcuadorOrPanama && this.itemsEcuadorPanama.length > 0) {
          const ecuadorItem = this.itemsEcuadorPanama.find(i => i.id === catalogItemId);
          tipoItem = ecuadorItem ? (ecuadorItem.concepto ?? ecuadorItem.tipoItem ?? '').trim() || ecuadorItem.tipoItem : undefined;
        } else {
          const item = this.alimentosById.get(catalogItemId);
          tipoItem = item ? (item.metadata?.type_item || item.metadata?.itemType || (item as any).itemType || 'alimento') : undefined;
        }
        if (tipoItem) {
          if ((!tipoItemActual || tipoItemActual === 'alimento') && tipoItem !== tipoItemActual) {
            control.patchValue({ tipoItem }, { emitEvent: false });
            this.filtrarAlimentosPorTipo('hembras', tipoItem);
          } else if (tipoItemActual && tipoItemActual !== tipoItem) {
            control.patchValue({ tipoItem }, { emitEvent: false });
            this.filtrarAlimentosPorTipo('hembras', tipoItem);
          }
        }
      }
    });

    // Actualizar items de machos
    this.itemsMachosArray.controls.forEach((control, index) => {
      const catalogItemId = control.get('catalogItemId')?.value;
      const tipoItemActual = control.get('tipoItem')?.value;

      if (catalogItemId) {
        let tipoItem: string | undefined;
        if (this.isEcuadorOrPanama && this.itemsEcuadorPanama.length > 0) {
          const ecuadorItem = this.itemsEcuadorPanama.find(i => i.id === catalogItemId);
          tipoItem = ecuadorItem ? (ecuadorItem.concepto ?? ecuadorItem.tipoItem ?? '').trim() || ecuadorItem.tipoItem : undefined;
        } else {
          const item = this.alimentosById.get(catalogItemId);
          tipoItem = item ? (item.metadata?.type_item || item.metadata?.itemType || (item as any).itemType || 'alimento') : undefined;
        }
        if (tipoItem) {

          // Si el tipoItem actual es 'alimento' (fallback) o está vacío, actualizarlo con el real
          if ((!tipoItemActual || tipoItemActual === 'alimento') && tipoItem && tipoItem !== tipoItemActual) {
            control.patchValue({ tipoItem }, { emitEvent: false });
            // Filtrar alimentos por el nuevo tipo para que el select muestre los items correctos
            this.filtrarAlimentosPorTipo('machos', tipoItem);
          } else if (tipoItemActual && tipoItemActual !== tipoItem && tipoItem) {
            // Si el tipoItem actual no coincide con el del catálogo, actualizar
            control.patchValue({ tipoItem }, { emitEvent: false });
            this.filtrarAlimentosPorTipo('machos', tipoItem);
          }
        }
      }
    });

    if (this.editing) {
      this.applyEditModeFieldLocks();
    }
  }

  /**
   * Obtiene el tipo de ítem desde el catálogo y actualiza el FormArray
   */
  private obtenerTipoItemDesdeCatalogo(catalogItemId: number, genero: 'hembras' | 'machos', index: number): void {
    if (this.isEcuadorOrPanama && this.itemsEcuadorPanama.length > 0) {
      const ecuadorItem = this.itemsEcuadorPanama.find(i => i.id === catalogItemId);
      if (ecuadorItem) {
        const tipoItem = (ecuadorItem.concepto ?? ecuadorItem.tipoItem ?? '').trim() || ecuadorItem.tipoItem;
        const array = genero === 'hembras' ? this.itemsHembrasArray : this.itemsMachosArray;
        const control = array.at(index);
        if (control) {
          control.patchValue({ tipoItem }, { emitEvent: false });
          this.filtrarAlimentosPorTipo(genero, tipoItem);
        }
        return;
      }
    }
    this.catalogSvc.getById(catalogItemId).subscribe({
      next: (item) => {
        if (item) {
          const tipoItem = item.metadata?.type_item || item.metadata?.itemType || item.itemType || 'alimento';
          const array = genero === 'hembras' ? this.itemsHembrasArray : this.itemsMachosArray;
          const control = array.at(index);

          if (control) {
            control.patchValue({ tipoItem }, { emitEvent: false });
            this.filtrarAlimentosPorTipo(genero, tipoItem);
          }
        }
      },
      error: (err) => {
        console.warn(`No se pudo obtener tipoItem para catalogItemId ${catalogItemId}:`, err);
      }
    });
  }

  private actualizarMapasYFiltros(): void {
    

    // Actualizar mapas
    this.alimentosById.clear();
    this.alimentosByCode.clear();
    this.alimentosByName.clear();

    for (const it of this.alimentosCatalog) {
      if (it.id != null) this.alimentosById.set(it.id, it);
      if (it.codigo)     this.alimentosByCode.set(String(it.codigo).trim(), it);
      if (it.nombre)     this.alimentosByName.set(it.nombre.trim().toLowerCase(), it);
    }

    // Si estamos editando y hay alimentos seleccionados, establecer el tipo de ítem
    if (this.editing) {
      this.establecerTipoItemYAlimentoAlEditar();
    } else {
      // Si no estamos editando, actualizar los filtros si hay tipos seleccionados
      // Solo actualizar si no estamos cargando para prevenir ciclos infinitos
      if (!this.cargandoInventarioGranja) {
        const tipoItemH = this.form.get('tipoItemHembras')?.value;
        const tipoItemM = this.form.get('tipoItemMachos')?.value;
        if (tipoItemH) this.filtrarAlimentosPorTipo('hembras', tipoItemH);
        if (tipoItemM) this.filtrarAlimentosPorTipo('machos', tipoItemM);
      }
    }
  }

  // ================== CATALOGO ALIMENTOS (DEPRECADO - usar cargarInventarioGranja) ==================
  private loadAlimentosCatalog(): void {
    // Si ya tenemos granjaId, usar el método de inventario
    if (this.granjaIdActual) {
      this.cargarInventarioGranja(this.granjaIdActual);
      return;
    }

    // Fallback: cargar desde catálogo completo (solo si no hay granja seleccionada)
    const firstPage = 1;
    const pageSize = 100;

    this.catalogSvc.list('', firstPage, pageSize).pipe(
      expand((res: PagedResult<CatalogItemDto>) => {
        const received = res.page * res.pageSize;
        const more = received < (res.total ?? 0);
        return more
          ? this.catalogSvc.list('', res.page + 1, res.pageSize)
          : EMPTY;
      }),
      reduce((acc: CatalogItemDto[], res: PagedResult<CatalogItemDto>) => {
        const items = Array.isArray(res.items) ? res.items : [];
        return acc.concat(items);
      }, []),
      map(all => all.sort((a, b) =>
        (a.nombre || '').localeCompare(b.nombre || '', 'es', { numeric: true, sensitivity: 'base' })
      ))
    ).subscribe(all => {
      this.alimentosCatalog = all.filter(a => a.activo);

      this.alimentosById.clear();
      this.alimentosByCode.clear();
      this.alimentosByName.clear();

      for (const it of this.alimentosCatalog) {
        if (it.id != null) this.alimentosById.set(it.id, it);
        if (it.codigo)     this.alimentosByCode.set(String(it.codigo).trim(), it);
        if (it.nombre)     this.alimentosByName.set(it.nombre.trim().toLowerCase(), it);
      }

      // Después de cargar, actualizar los filtros si hay tipos seleccionados
      const tipoItemH = this.form.get('tipoItemHembras')?.value;
      const tipoItemM = this.form.get('tipoItemMachos')?.value;
      if (tipoItemH) this.filtrarAlimentosPorTipo('hembras', tipoItemH);
      if (tipoItemM) this.filtrarAlimentosPorTipo('machos', tipoItemM);
    });
  }

  // ================== FILTRADO POR TIPO ==================
  filtrarAlimentosPorTipo(tipoGenero: 'hembras' | 'machos', tipoItem: string | null): void {
    if (this.alimentosCatalog.length === 0 && this.granjaIdActual && !this.cargandoInventarioGranja) {
      this.cargarInventarioGranja(this.granjaIdActual);
      return;
    }

    if (!this.granjaIdActual || this.cargandoInventarioGranja) {
      if (tipoGenero === 'hembras') {
        this.alimentosFiltradosHembras = [];
      } else {
        this.alimentosFiltradosMachos = [];
      }
      return;
    }

    if (!tipoItem) {
      if (tipoGenero === 'hembras') {
        this.alimentosFiltradosHembras = this.alimentosCatalog;
      } else {
        this.alimentosFiltradosMachos = this.alimentosCatalog;
      }
    } else {
      const alimentos = this.isEcuadorOrPanama && this.itemsEcuadorPanama.length > 0
        ? this.getAlimentosFiltradosPorTipo(tipoItem)
        : this.alimentosCatalog.filter(a => {
            const metadata = a.metadata;
            const itemType = metadata?.type_item || metadata?.itemType;
            return metadata && itemType === tipoItem;
          });

      if (tipoGenero === 'hembras') {
        this.alimentosFiltradosHembras = alimentos;
      } else {
        this.alimentosFiltradosMachos = alimentos;
      }
    }
  }

  // ================== CONSULTA DE INVENTARIO ==================
  /**
   * Ecuador/Panamá: el id del ítem es item_inventario_ecuador; el stock viene de inventario_gestion
   * (mapa `inventarioPorItem`). No usar GET /farms/.../inventory/by-item (catálogo legacy catalog_items → 404).
   */
  private aplicarConsultaInventarioEcuadorPanama(
    tipoGenero: 'hembras' | 'machos',
    itemInventarioEcuadorId: number
  ): void {
    const inv = this.inventarioPorItem.get(itemInventarioEcuadorId);
    const item = inv
      ? { quantity: inv.quantity, unit: inv.unit || 'kg' }
      : null;

    if (tipoGenero === 'hembras') {
      this.cargandoInventarioHembras = false;
      if (item && item.quantity != null && item.quantity > 0) {
        const unidad = (item.unit || 'kg').trim();
        const cantidadOriginal = Number(item.quantity);
        this.inventarioUnidadHembras = unidad;
        this.inventarioCantidadOriginalHembras = cantidadOriginal;
        const cantidadGramos = cantidadOriginalAGramos(cantidadOriginal, unidad);
        this.inventarioDisponibleHembras = cantidadGramos;
        this.mensajeInventarioHembras = '';
      } else {
        this.inventarioDisponibleHembras = 0;
        this.inventarioCantidadOriginalHembras = 0;
        this.inventarioUnidadHembras = 'kg';
        this.mensajeInventarioHembras = 'No hay alimento en existencia (inventario gestión)';
      }
    } else {
      this.cargandoInventarioMachos = false;
      if (item && item.quantity != null && item.quantity > 0) {
        const unidad = (item.unit || 'kg').trim();
        const cantidadOriginal = Number(item.quantity);
        this.inventarioUnidadMachos = unidad;
        this.inventarioCantidadOriginalMachos = cantidadOriginal;
        const cantidadGramos = cantidadOriginalAGramos(cantidadOriginal, unidad);
        this.inventarioDisponibleMachos = cantidadGramos;
        this.mensajeInventarioMachos = '';
      } else {
        this.inventarioDisponibleMachos = 0;
        this.inventarioCantidadOriginalMachos = 0;
        this.inventarioUnidadMachos = 'kg';
        this.mensajeInventarioMachos = 'No hay alimento en existencia (inventario gestión)';
      }
    }
  }

  consultarInventario(tipoGenero: 'hembras' | 'machos', alimentoId: number | string): void {
    const loteId = this.form.get('loteId')?.value;
    if (!loteId) {
      if (tipoGenero === 'hembras') {
        this.mensajeInventarioHembras = 'Seleccione un lote primero';
        this.cargandoInventarioHembras = false;
      } else {
        this.mensajeInventarioMachos = 'Seleccione un lote primero';
        this.cargandoInventarioMachos = false;
      }
      return;
    }

    // Obtener el lote para conseguir granjaId
    const lote = this.lotes.find(l => String(l.loteId) === String(loteId));
    if (!lote || !lote.granjaId) {
      if (tipoGenero === 'hembras') {
        this.mensajeInventarioHembras = 'No se pudo obtener la granja del lote';
        this.cargandoInventarioHembras = false;
        this.inventarioDisponibleHembras = null;
      } else {
        this.mensajeInventarioMachos = 'No se pudo obtener la granja del lote';
        this.cargandoInventarioMachos = false;
        this.inventarioDisponibleMachos = null;
      }
      return;
    }

    const granjaId = lote.granjaId;
    const catalogItemId = typeof alimentoId === 'string' ? parseInt(alimentoId, 10) : alimentoId;

    if (tipoGenero === 'hembras') {
      this.cargandoInventarioHembras = true;
      this.mensajeInventarioHembras = '';
    } else {
      this.cargandoInventarioMachos = true;
      this.mensajeInventarioMachos = '';
    }

    if (this.isEcuadorOrPanama) {
      this.aplicarConsultaInventarioEcuadorPanama(tipoGenero, catalogItemId);
      return;
    }

    // Colombia / catálogo legacy: farm_product_inventory por catalogItemId
    this.inventarioSvc.getInventoryByItem(granjaId, catalogItemId).subscribe({
      next: (item) => {
        if (tipoGenero === 'hembras') {
          this.cargandoInventarioHembras = false;
          if (item && item.quantity != null && item.quantity > 0) {
            // Guardar la unidad y cantidad original del inventario
            // IMPORTANTE: Usar la unidad exacta que viene del backend (kg, g, etc.)
            // No normalizar aquí, solo guardar tal cual viene para que coincida exactamente
            const unidad = (item.unit || 'kg').trim();
            const cantidadOriginal = Number(item.quantity);

            // Guardar la unidad original tal cual viene del backend (sin normalizar)
            // Esto es crítico porque el backend compara unidades exactamente
            this.inventarioUnidadHembras = unidad;
            this.inventarioCantidadOriginalHembras = cantidadOriginal;

            

            // Convertir a gramos para mostrar en la interfaz (si el inventario está en kg)
            // Si el inventario está en gramos, no hay conversión
            if (esUnidadDesconocidaParaGramos(unidad)) {
              // Para otras unidades, asumir kg
              console.warn(`Unidad desconocida del inventario: ${unidad}, asumiendo kg`);
            }
            const cantidadGramos = cantidadOriginalAGramos(cantidadOriginal, unidad);

            this.inventarioDisponibleHembras = cantidadGramos;
            this.mensajeInventarioHembras = '';
          } else {
            this.inventarioDisponibleHembras = 0;
            this.inventarioCantidadOriginalHembras = 0;
            this.inventarioUnidadHembras = 'kg';
            this.mensajeInventarioHembras = 'No hay alimento en existencia';
          }
        } else {
          this.cargandoInventarioMachos = false;
          if (item && item.quantity != null && item.quantity > 0) {
            // Guardar la unidad y cantidad original del inventario
            // IMPORTANTE: Usar la unidad exacta que viene del backend
            const unidad = (item.unit || 'kg').trim();
            const cantidadOriginal = Number(item.quantity);

            // Guardar la unidad original tal cual viene del backend
            this.inventarioUnidadMachos = unidad;
            this.inventarioCantidadOriginalMachos = cantidadOriginal;

            // Convertir a gramos para mostrar en la interfaz (si el inventario está en kg)
            if (esUnidadDesconocidaParaGramos(unidad)) {
              // Para otras unidades, asumir kg
              console.warn(`Unidad desconocida del inventario: ${unidad}, asumiendo kg`);
            }
            const cantidadGramos = cantidadOriginalAGramos(cantidadOriginal, unidad);

            this.inventarioDisponibleMachos = cantidadGramos;
            this.mensajeInventarioMachos = '';
          } else {
            this.inventarioDisponibleMachos = 0;
            this.inventarioCantidadOriginalMachos = 0;
            this.inventarioUnidadMachos = 'kg';
            this.mensajeInventarioMachos = 'No hay alimento en existencia';
          }
        }
      },
      error: (err) => {
        console.error('Error al consultar inventario:', err);
        if (tipoGenero === 'hembras') {
          this.cargandoInventarioHembras = false;
          this.inventarioDisponibleHembras = null;
          this.mensajeInventarioHembras = 'Error al consultar inventario';
        } else {
          this.cargandoInventarioMachos = false;
          this.inventarioDisponibleMachos = null;
          this.mensajeInventarioMachos = 'Error al consultar inventario';
        }
      }
    });
  }

  // ================== EVENTOS ==================
  onClose(): void {
    this.close.emit();
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.pesoPendienteEnDiaSemanal) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() incluye todos los controles (también los que están en tabs condicionales como Agua)
    const raw = { ...this.form.getRawValue() };
    if (!this.editing && this.avesDisponibles) {
      aplicarCerosSinAvesDisponibles(raw, this.isPanama, this.avesDisponibles);
    }

    // Panamá: mapear campos Mixto → H (M=0) en el DTO antes de enviar al backend
    if (this.isPanama) {
      mapearPanamaMixtoAHM(raw);
    }
    const loteId = raw.loteId;
    const lote = this.lotes.find(l => String(l.loteId) === String(loteId));

    if (!lote || !lote.granjaId) {
      this.toast.error('No se pudo obtener la granja del lote seleccionado');
      return;
    }

    // Construir arrays de ítems desde FormArrays
    const itemsHembras = construirItemsSeguimiento(
      this.itemsHembrasArray.controls.map(c => c.value),
      { forzarAlimento: this.hembrasSoloAlimento, isEcuadorOrPanama: this.isEcuadorOrPanama }
    );
    const itemsMachos = construirItemsSeguimiento(
      this.itemsMachosArray.controls.map(c => c.value),
      { forzarAlimento: false, isEcuadorOrPanama: this.isEcuadorOrPanama }
    );

    // Items adicionales JSONB (solo ítems que NO son alimentos)
    const itemsAdicionales = construirItemsAdicionales(itemsHembras, itemsMachos);

    // Construir string de tipoAlimento desde los alimentos
    const alimentosHembras = itemsHembras.filter(item => item.tipoItem === 'alimento');
    const alimentosMachos = itemsMachos.filter(item => item.tipoItem === 'alimento');
    const tipoAlimentoStr = construirTipoAlimentoStr(alimentosHembras, alimentosMachos, this.alimentosById, raw.tipoAlimento);

    const ymd = toYMD(raw.fechaRegistro)!;

    // El backend ahora acepta consumo con unidad y hace la conversión automáticamente
    const lotePosturaLevanteId = this.editing?.lotePosturaLevanteId ?? null;
    const baseDto = buildBaseSeguimientoDto({
      fechaRegistroIso: ymdToIsoAtNoon(ymd),
      raw,
      lotePosturaLevanteId,
      itemsHembras,
      itemsMachos,
      itemsAdicionales,
      tipoAlimentoStr,
      isPanama: this.isPanama,
      createdByUserId: this.storage.get()?.user?.id ?? null,
    });

    const isEdit = !!this.editing;
    const data = isEdit
      ? { ...baseDto, id: this.editing!.id } as UpdateSeguimientoLoteLevanteDto
      : baseDto as CreateSeguimientoLoteLevanteDto;

    // Ecuador/Panamá: no usar inventario legacy (GET .../by-item ni POST .../movements/out): los IDs son de item_inventario_ecuador.
    // En Pollo engorde el consumo lo aplica el backend al guardar (SeguimientoAvesEngorde → Metadata → RegistrarConsumoAsync).
    if (this.isEcuadorOrPanama) {
      this.save.emit({ data, isEdit });
      return;
    }

    // Edición: consumo/alimento no cambia en UI (engorde); no repetir salidas de inventario legacy en Colombia.
    if (isEdit) {
      this.save.emit({ data, isEdit: true });
      return;
    }

    // Hacer resta al inventario antes de guardar (solo creación Colombia; inventario legacy por catalog_items)
    const restas: Promise<void>[] = [];

    // Procesar alimentos de hembras
    alimentosHembras.forEach(item => {
      const cantidad = item.cantidad;
      const unidad = item.unidad || 'kg';
      let cantidadKg = cantidad;

      // Convertir a kg si viene en gramos
      if (unidad === 'g' || unidad === 'gramos') {
        cantidadKg = cantidad / 1000;
      }

      if (cantidadKg > 0) {
        // Consultar inventario para obtener la unidad exacta
        this.consultarInventario('hembras', item.catalogItemId);
        const unidadInventario = this.inventarioUnidadHembras || 'kg';

        restas.push(
          this.inventarioSvc.postExit(lote.granjaId, {
            catalogItemId: item.catalogItemId,
            quantity: cantidadKg,
            unit: unidadInventario,
            reference: `Consumo diario levante - Lote ${lote.loteNombre || loteId}`,
            reason: 'Consumo diario',
            destination: 'Consumo'
          }).toPromise().then(() => {
            
          }).catch(err => {
            console.error('Error al restar inventario hembras:', err);
            throw new Error('Error al registrar consumo en inventario (hembras)');
          })
        );
      }
    });

    // Procesar alimentos de machos
    alimentosMachos.forEach(item => {
      const cantidad = item.cantidad;
      const unidad = item.unidad || 'kg';
      let cantidadKg = cantidad;

      // Convertir a kg si viene en gramos
      if (unidad === 'g' || unidad === 'gramos') {
        cantidadKg = cantidad / 1000;
      }

      if (cantidadKg > 0) {
        // Consultar inventario para obtener la unidad exacta
        this.consultarInventario('machos', item.catalogItemId);
        const unidadInventario = this.inventarioUnidadMachos || 'kg';

        restas.push(
          this.inventarioSvc.postExit(lote.granjaId, {
            catalogItemId: item.catalogItemId,
            quantity: cantidadKg,
            unit: unidadInventario,
            reference: `Consumo diario levante - Lote ${lote.loteNombre || loteId}`,
            reason: 'Consumo diario',
            destination: 'Consumo'
          }).toPromise().then(() => {
            
          }).catch(err => {
            console.error('Error al restar inventario machos:', err);
            throw new Error('Error al registrar consumo en inventario (machos)');
          })
        );
      }
    });

    // Esperar a que se completen las restas antes de emitir el save
    if (restas.length > 0) {
      Promise.all(restas).then(() => {
        this.save.emit({ data, isEdit });
      }).catch(err => {
        this.toast.error(err.message || 'Error al registrar consumo en inventario');
      });
    } else {
      this.save.emit({ data, isEdit });
    }
  }

}

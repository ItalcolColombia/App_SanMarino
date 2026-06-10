import { Component, Input, Output, EventEmitter, OnInit, OnChanges, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { LoteSeguimientoDto, CreateLoteSeguimientoDto, UpdateLoteSeguimientoDto } from '../../services/lote-seguimiento.service';
import { LoteReproductoraDto } from '../../../lote-reproductora/services/lote-reproductora.service';
import { GestionInventarioService, InventarioGestionStockDto } from '../../../gestion-inventario/services/gestion-inventario.service';
import { ShowIfEcuadorPanamaDirective } from '../../../../core/directives';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';

interface CatalogItemExtended {
  id: number;
  codigo: string;
  nombre: string;
  tipoItem: string;
  unidad?: string;
  activo: boolean;
  metadata?: any;
}

type CatalogItemType = 'alimento' | 'medicamento' | 'accesorio' | 'biologico' | 'consumible' | 'otro';

interface ItemSeguimientoDto {
  tipoItem: string;
  catalogItemId: number;
  cantidad: number;
  unidad: string;
  cantidadUnidades?: number | null;
}

@Component({
  selector: 'app-modal-seguimiento-reproductora',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    ShowIfEcuadorPanamaDirective
  ],
  templateUrl: './modal-seguimiento-reproductora.component.html',
  styleUrls: ['./modal-seguimiento-reproductora.component.scss']
})
export class ModalSeguimientoReproductoraComponent implements OnInit, OnChanges, OnDestroy {
  @Input() isOpen: boolean = false;
  @Input() editing: LoteSeguimientoDto | null = null;
  /** Lista de lotes (LoteDto o LoteFilterItemDto: loteId, loteNombre, granjaId opcional). */
  @Input() lotes: Array<{ loteId: string | number; loteNombre: string; granjaId?: number }> = [];
  @Input() repros: LoteReproductoraDto[] = [];
  @Input() selectedLoteId: string | null = null;
  @Input() selectedReproId: string | null = null;
  @Input() loading: boolean = false;
  /**
   * Granja asociada al contexto actual. Si el padre puede resolverla, pásala explícitamente.
   * Si no, se intenta derivar del lote seleccionado (granjaIdActual).
   * TODO: si el componente padre no provee farmId, asegurar que los lotes pasados incluyan `granjaId`
   * o leerlo de la sesión activa del usuario.
   */
  @Input() farmId: number | null = null;
  /** galponId del galpón seleccionado — filtra el stock de alimento por ubicación. */
  @Input() galponId: string | null = null;
  /** Fecha pre-calculada por el padre (encasetamiento + N días). Se fija como readonly al crear. */
  @Input() defaultFecha: string | null = null;
  /** Número de registros existentes en el lote reproductora (para mostrar el hint). */
  @Input() registrosCount: number = 0;
  /** Contexto de navegación mostrado en la banda superior del modal. */
  @Input() ctxGranja: string | null = null;
  @Input() ctxNucleo: string | null = null;
  @Input() ctxGalpon: string | null = null;
  @Input() ctxLoteEngorde: string | null = null;
  @Input() ctxReproductora: string | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{ data: CreateLoteSeguimientoDto | UpdateLoteSeguimientoDto; isEdit: boolean }>();

  // Estado para mensajes de validación
  showValidationErrors = false;
  saving = false;

  // Formulario
  form!: FormGroup;

  // Catálogo de alimentos (desde inventario de la granja)
  alimentosCatalog: CatalogItemExtended[] = [];
  alimentosFiltradosHembras: CatalogItemExtended[] = [];
  alimentosFiltradosMachos: CatalogItemExtended[] = [];
  private alimentosByCode = new Map<string, CatalogItemExtended>();
  private alimentosById = new Map<number, CatalogItemExtended>();
  private alimentosByName = new Map<string, CatalogItemExtended>();
  private granjaIdActual: number | null = null;
  cargandoInventarioGranja = false;

  // Mapa para guardar información de inventario
  private inventarioPorItem = new Map<number, { quantity: number; unit: string }>();

  // Tipos de ítem
  tiposItem: CatalogItemType[] = ['alimento', 'medicamento', 'accesorio', 'biologico', 'consumible', 'otro'];

  // (estados de inventario por género eliminados — se usa un único selector de alimento)

  // Propiedad para verificar si es Ecuador o Panamá
  isEcuadorOrPanama = false;
  private sessionSubscription?: Subscription;

  constructor(
    private fb: FormBuilder,
    private inventarioSvc: GestionInventarioService,
    private countryFilter: CountryFilterService,
    private storage: TokenStorageService
  ) {}

  ngOnInit(): void {
    this.initializeForm();
    this.updateEcuadorOrPanamaStatus();
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

  /**
   * Granja efectiva para componentes hijos (Lesiones). Prioriza el Input `farmId`
   * y cae al `granjaIdActual` derivado del lote elegido. Puede ser null hasta que
   * el usuario seleccione un lote o el padre provea el valor.
   */
  get farmIdActual(): number | null {
    return this.farmId ?? this.granjaIdActual ?? null;
  }

  /** Lote seleccionado en formato numérico para hijos (LesionTab espera number). */
  get loteIdActual(): number | null {
    const raw = this.form?.get('loteId')?.value;
    if (raw === null || raw === undefined || raw === '') return null;
    const n = Number(raw);
    return Number.isFinite(n) ? n : null;
  }

  ngOnChanges(): void {
    if (this.isOpen && this.editing) {
      this.populateForm();
    } else if (this.isOpen && !this.editing) {
      this.resetForm();
    }
    // Cargar inventario directamente con farmId cuando se abre el modal
    // (el loteId pre-set puede no disparar valueChanges)
    if (this.isOpen && this.farmId && !this.granjaIdActual) {
      this.granjaIdActual = this.farmId;
      this.cargarInventarioGranja(this.farmId);
    }
  }

  /** 1 quintal (QQ) = 45.36 kg (100 libras). */
  static readonly QQ_TO_KG = 45.36;

  // ================== FORMULARIO ==================
  private initializeForm(): void {
    this.form = this.fb.group({
      fecha: [this.todayYMD(), Validators.required],
      loteId: ['', Validators.required],
      reproductoraId: ['', Validators.required],
      mortalidadH: [0, [Validators.required, Validators.min(0)]],
      mortalidadM: [0, [Validators.required, Validators.min(0)]],
      selH: [0, [Validators.required, Validators.min(0)]],
      selM: [0, [Validators.required, Validators.min(0)]],
      errorH: [0, [Validators.required, Validators.min(0)]],
      errorM: [0, [Validators.required, Validators.min(0)]],
      // Alimento único compartido + consumo por género
      alimentoId:          [null],
      consumoHembrasQty:   [null, [Validators.min(0)]],
      unidadHembras:       ['kg'],
      consumoMachosQty:    [null, [Validators.min(0)]],
      unidadMachos:        ['kg'],
      observaciones: [''],
      ciclo: ['Normal'],
      // Pesaje
      pesoInicial: [null, [Validators.min(0)]],
      pesoFinal:   [null, [Validators.min(0)]],
      pesoPromH:   [null, [Validators.min(0)]],
      pesoPromM:   [null, [Validators.min(0)]],
      uniformidadH:[null, [Validators.min(0), Validators.max(100)]],
      uniformidadM:[null, [Validators.min(0), Validators.max(100)]],
      cvH: [null, [Validators.min(0)]],
      cvM: [null, [Validators.min(0)]],
      // Agua (Ecuador / Panamá)
      consumoAguaDiario:      [null, [Validators.min(0)]],
      consumoAguaPh:          [null, [Validators.min(0)]],
      consumoAguaOrp:         [null, [Validators.min(0)]],
      consumoAguaTemperatura: [null, [Validators.min(0)]],
    });

    // Recargar inventario si cambia el lote (loteId pre-seteado desde contexto padre)
    this.form.get('loteId')?.valueChanges.subscribe(loteId => {
      if (!loteId) { this.granjaIdActual = null; this.limpiarCatalogos(); }
    });
  }

  /** Alimento actualmente seleccionado (para mostrar stock). */
  get alimentoSeleccionado(): CatalogItemExtended | null {
    const id = this.form?.get('alimentoId')?.value;
    if (!id) return null;
    return this.alimentosById.get(Number(id)) ?? null;
  }

  /** Lista de alimentos disponibles (tipo 'alimento' con stock > 0). */
  get alimentosList(): CatalogItemExtended[] {
    return this.alimentosCatalog.filter(a =>
      !a.tipoItem || a.tipoItem.toLowerCase() === 'alimento'
    );
  }

  /** Convierte qty a kg según la unidad seleccionada. */
  toKg(qty: number | null | undefined, unit: string): number {
    const q = Number(qty ?? 0);
    return unit === 'qq'
      ? Math.round(q * ModalSeguimientoReproductoraComponent.QQ_TO_KG * 1000) / 1000
      : q;
  }

  /** Muestra el equivalente en kg cuando se selecciona QQ. */
  get consumoHembrasEnKg(): number {
    return this.toKg(
      this.form?.get('consumoHembrasQty')?.value,
      this.form?.get('unidadHembras')?.value ?? 'kg'
    );
  }

  get consumoMachosEnKg(): number {
    return this.toKg(
      this.form?.get('consumoMachosQty')?.value,
      this.form?.get('unidadMachos')?.value ?? 'kg'
    );
  }

  private limpiarCatalogos(): void {
    this.alimentosCatalog = [];
    this.alimentosFiltradosHembras = [];
    this.alimentosFiltradosMachos = [];
    this.alimentosById.clear();
    this.alimentosByCode.clear();
    this.alimentosByName.clear();
    this.inventarioPorItem.clear();
  }

  // (FormArrays eliminados — reemplazados por controles simples alimentoId/consumoHembrasQty/consumoMachosQty)

  // ================== INVENTARIO ==================
  /**
   * Carga el stock de alimentos desde /api/inventario-gestion/stock,
   * filtrando por farmId + galponId (galpón del lote reproductora) e itemType='alimento'.
   */
  cargarInventarioGranja(granjaId: number): void {
    if (!granjaId || this.cargandoInventarioGranja) return;
    this.cargandoInventarioGranja = true;
    this.limpiarCatalogos();

    const params: Parameters<GestionInventarioService['getStock']>[0] = {
      farmId: granjaId,
      itemType: 'alimento',
    };
    if (this.galponId) params.galponId = this.galponId;

    this.inventarioSvc.getStock(params).pipe(
      catchError(err => {
        console.error('[Modal Reproductora] Error al cargar stock:', err);
        return of([]);
      }),
      finalize(() => { this.cargandoInventarioGranja = false; })
    ).subscribe((items: InventarioGestionStockDto[]) => {
      const catalogItems = items
        .filter(item => item.quantity > 0)
        .map(item => {
          const cat: CatalogItemExtended = {
            id: item.itemInventarioEcuadorId,
            codigo: item.itemCodigo,
            nombre: item.itemNombre,
            tipoItem: item.itemType || 'alimento',
            unidad: item.unit,
            activo: true,
          };
          this.alimentosById.set(cat.id, cat);
          this.alimentosByCode.set(cat.codigo, cat);
          this.alimentosByName.set(cat.nombre.toLowerCase(), cat);
          this.inventarioPorItem.set(cat.id, { quantity: item.quantity, unit: item.unit });
          return cat;
        });

      this.alimentosCatalog = catalogItems;
      this.alimentosFiltradosHembras = catalogItems;
      this.alimentosFiltradosMachos = catalogItems;
    });
  }

  getAlimentosFiltradosPorTipo(tipoItem: string): CatalogItemExtended[] {
    if (tipoItem === 'alimento') {
      return this.alimentosFiltradosHembras.filter(a => a.tipoItem === 'alimento');
    }
    return this.alimentosCatalog.filter(a => a.tipoItem === tipoItem);
  }

  getCantidadDisponible(catalogItemId: number): { quantity: number; unit: string } | null {
    return this.inventarioPorItem.get(catalogItemId) || null;
  }

  getItemDisplayText(item: CatalogItemExtended): string {
    const inv = this.getCantidadDisponible(item.id);
    const stock = inv ? ` (Stock: ${inv.quantity} ${inv.unit})` : '';
    return `${item.codigo} - ${item.nombre}${stock}`;
  }

  // ================== HELPERS ==================
  private todayYMD(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  private resetForm(): void {
    this.form.reset({
      fecha: this.defaultFecha || this.todayYMD(),
      loteId: this.selectedLoteId || '',
      reproductoraId: this.selectedReproId || '',
      mortalidadH: 0, mortalidadM: 0,
      selH: 0, selM: 0,
      errorH: 0, errorM: 0,
      alimentoId: null,
      consumoHembrasQty: null, unidadHembras: 'kg',
      consumoMachosQty: null,  unidadMachos: 'kg',
      observaciones: '', ciclo: 'Normal',
      pesoInicial: null, pesoFinal: null,
      pesoPromH: null, pesoPromM: null,
      uniformidadH: null, uniformidadM: null,
      cvH: null, cvM: null,
      consumoAguaDiario: null, consumoAguaPh: null,
      consumoAguaOrp: null, consumoAguaTemperatura: null,
    });

    if (this.selectedLoteId) {
      const lote = this.lotes.find(l => String(l.loteId) === String(this.selectedLoteId));
      if (lote?.granjaId) {
        this.granjaIdActual = Number(lote.granjaId);
        this.cargarInventarioGranja(this.granjaIdActual);
      }
    } else if (this.farmId) {
      this.granjaIdActual = this.farmId;
      this.cargarInventarioGranja(this.farmId);
    }
  }

  private populateForm(): void {
    if (!this.editing) return;
    const e = this.editing;
    const meta = (e.metadata as any) ?? {};

    // Recuperar el alimento guardado (primer ítem de metadata.itemsHembras)
    const primerH: any = meta.itemsHembras?.[0] ?? null;
    const primerM: any = meta.itemsMachos?.[0] ?? null;
    const alimentoId = primerH?.catalogItemId ?? primerM?.catalogItemId ?? null;
    const cantH = primerH?.cantidad ?? null;
    const cantM = primerM?.cantidad ?? null;

    const fecha = e.fecha ? new Date(e.fecha).toISOString().substring(0, 10) : this.todayYMD();
    this.form.patchValue({
      fecha,
      loteId: String(e.loteId),
      reproductoraId: e.reproductoraId,
      mortalidadH: e.mortalidadH ?? 0,
      mortalidadM: e.mortalidadM ?? 0,
      selH: e.selH ?? 0, selM: e.selM ?? 0,
      errorH: e.errorH ?? 0, errorM: e.errorM ?? 0,
      alimentoId,
      consumoHembrasQty: cantH, unidadHembras: 'kg',
      consumoMachosQty: cantM,  unidadMachos: 'kg',
      observaciones: e.observaciones ?? '',
      ciclo: e.ciclo ?? 'Normal',
      pesoInicial: e.pesoInicial ?? null, pesoFinal: e.pesoFinal ?? null,
      pesoPromH: e.pesoPromH, pesoPromM: e.pesoPromM,
      uniformidadH: e.uniformidadH, uniformidadM: e.uniformidadM,
      cvH: e.cvH, cvM: e.cvM,
      consumoAguaDiario: e.consumoAguaDiario,
      consumoAguaPh: e.consumoAguaPh,
      consumoAguaOrp: e.consumoAguaOrp,
      consumoAguaTemperatura: e.consumoAguaTemperatura,
    });

    const lote = this.lotes.find(l => String(l.loteId) === String(e.loteId));
    const gid = lote?.granjaId ? Number(lote.granjaId) : (this.farmId ?? null);
    if (gid) { this.granjaIdActual = gid; this.cargarInventarioGranja(gid); }
  }

  // ================== GUARDAR ==================
  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.showValidationErrors = true;
      return;
    }

    this.saving = true;
    this.showValidationErrors = false;
    const raw = this.form.value;

    // Convertir consumo a kg según unidad seleccionada
    const consumoHKg = this.toKg(raw.consumoHembrasQty, raw.unidadHembras ?? 'kg');
    const consumoMKg = this.toKg(raw.consumoMachosQty, raw.unidadMachos ?? 'kg');
    const alimentoIdNum = raw.alimentoId ? Number(raw.alimentoId) : null;

    // Construir items con la cantidad ya en kg
    const itemsHembras = alimentoIdNum && consumoHKg > 0 ? [{
      tipoItem: 'alimento', catalogItemId: alimentoIdNum,
      cantidad: consumoHKg, unidad: 'kg'
    }] : null;
    const itemsMachos = alimentoIdNum && consumoMKg > 0 ? [{
      tipoItem: 'alimento', catalogItemId: alimentoIdNum,
      cantidad: consumoMKg, unidad: 'kg'
    }] : null;

    const tipoAlimentoNombre = alimentoIdNum
      ? (this.alimentosById.get(alimentoIdNum)?.nombre ?? 'Mixto')
      : '';

    const metadata: any = {};
    if (itemsHembras) metadata.itemsHembras = itemsHembras;
    if (itemsMachos)  metadata.itemsMachos  = itemsMachos;

    const payload: any = {
      fecha: new Date(raw.fecha).toISOString(),
      loteId: Number(raw.loteId),
      reproductoraId: raw.reproductoraId,
      mortalidadH: raw.mortalidadH, mortalidadM: raw.mortalidadM,
      selH: raw.selH, selM: raw.selM,
      errorH: raw.errorH, errorM: raw.errorM,
      tipoAlimento: tipoAlimentoNombre,
      consumoAlimento: consumoHKg,
      consumoKgMachos: consumoMKg,
      observaciones: raw.observaciones || null,
      ciclo: raw.ciclo || 'Normal',
      pesoInicial: raw.pesoInicial ?? null, pesoFinal: raw.pesoFinal ?? null,
      pesoPromH: raw.pesoPromH, pesoPromM: raw.pesoPromM,
      uniformidadH: raw.uniformidadH, uniformidadM: raw.uniformidadM,
      cvH: raw.cvH, cvM: raw.cvM,
      consumoAguaDiario: raw.consumoAguaDiario,
      consumoAguaPh: raw.consumoAguaPh,
      consumoAguaOrp: raw.consumoAguaOrp,
      consumoAguaTemperatura: raw.consumoAguaTemperatura,
      metadata: Object.keys(metadata).length > 0 ? metadata : null,
      itemsAdicionales: null,
    };

    if (this.editing) payload.id = this.editing.id;

    this.save.emit({ data: payload as CreateLoteSeguimientoDto | UpdateLoteSeguimientoDto, isEdit: !!this.editing });
    setTimeout(() => { this.saving = false; }, 100);
  }

  onClose(): void {
    this.close.emit();
  }

  // Helper para convertir a string
  toString(value: any): string {
    return String(value);
  }
}

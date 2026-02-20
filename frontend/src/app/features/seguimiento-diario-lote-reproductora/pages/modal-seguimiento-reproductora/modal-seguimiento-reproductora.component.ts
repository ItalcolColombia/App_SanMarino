import { Component, Input, Output, EventEmitter, OnInit, OnChanges, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { LoteSeguimientoDto, CreateLoteSeguimientoDto, UpdateLoteSeguimientoDto } from '../../services/lote-seguimiento.service';
import { LoteReproductoraDto } from '../../../lote-reproductora/services/lote-reproductora.service';
import { InventarioService, FarmInventoryDto } from '../../../inventario/services/inventario.service';
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
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ShowIfEcuadorPanamaDirective],
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
  private cargandoInventarioGranja = false;

  // Mapa para guardar información de inventario
  private inventarioPorItem = new Map<number, { quantity: number; unit: string }>();

  // Tipos de ítem
  tiposItem: CatalogItemType[] = ['alimento', 'medicamento', 'accesorio', 'biologico', 'consumible', 'otro'];

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

  // Propiedad para verificar si es Ecuador o Panamá
  isEcuadorOrPanama = false;
  private sessionSubscription?: Subscription;

  constructor(
    private fb: FormBuilder,
    private inventarioSvc: InventarioService,
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

  ngOnChanges(): void {
    if (this.isOpen && this.editing) {
      this.populateForm();
    } else if (this.isOpen && !this.editing) {
      this.resetForm();
    }
  }

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
      // FormArrays para múltiples ítems
      itemsHembras: this.fb.array([]),
      itemsMachos: this.fb.array([]),
      observaciones: [''],
      ciclo: ['Normal'],
      // Peso inicial/final (opcionales)
      pesoInicial: [null, [Validators.min(0)]],
      pesoFinal: [null, [Validators.min(0)]],
      // Campos de peso y uniformidad
      pesoPromH: [null, [Validators.min(0)]],
      pesoPromM: [null, [Validators.min(0)]],
      uniformidadH: [null, [Validators.min(0), Validators.max(100)]],
      uniformidadM: [null, [Validators.min(0), Validators.max(100)]],
      cvH: [null, [Validators.min(0)]],
      cvM: [null, [Validators.min(0)]],
      // Campos de agua (solo para Ecuador y Panamá)
      consumoAguaDiario: [null, [Validators.min(0)]],
      consumoAguaPh: [null, [Validators.min(0)]],
      consumoAguaOrp: [null, [Validators.min(0)]],
      consumoAguaTemperatura: [null, [Validators.min(0)]],
    });

    // Suscribirse a cambios en loteId para obtener granjaId y cargar inventario
    this.form.get('loteId')?.valueChanges.subscribe(loteId => {
      if (loteId) {
        const lote = this.lotes.find(l => String(l.loteId) === String(loteId));
        if (lote && lote.granjaId) {
          const nuevaGranjaId = Number(lote.granjaId);
          if (this.granjaIdActual !== nuevaGranjaId) {
            this.granjaIdActual = nuevaGranjaId;
            this.cargarInventarioGranja(nuevaGranjaId);
          }
        }
        this.limpiarInventario();
      } else {
        this.granjaIdActual = null;
        this.limpiarCatalogos();
      }
    });
  }

  private limpiarInventario(): void {
    this.inventarioDisponibleHembras = null;
    this.inventarioDisponibleMachos = null;
    this.inventarioUnidadHembras = 'kg';
    this.inventarioUnidadMachos = 'kg';
    this.inventarioCantidadOriginalHembras = null;
    this.inventarioCantidadOriginalMachos = null;
    this.mensajeInventarioHembras = '';
    this.mensajeInventarioMachos = '';
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

  // ================== FORM ARRAYS ==================
  get itemsHembrasArray(): FormArray {
    return this.form.get('itemsHembras') as FormArray;
  }

  get itemsMachosArray(): FormArray {
    return this.form.get('itemsMachos') as FormArray;
  }

  agregarItemHembras(): void {
    const itemGroup = this.fb.group({
      tipoItem: [null, Validators.required],
      catalogItemId: [null, Validators.required],
      cantidad: [0, [Validators.required, Validators.min(0)]],
      unidad: ['kg', Validators.required],
      cantidadUnidades: [null]
    });

    // Suscribirse a cambios en tipoItem para cargar inventario
    itemGroup.get('tipoItem')?.valueChanges.subscribe(tipoItem => {
      if (tipoItem && this.granjaIdActual) {
        this.cargarInventarioGranja(this.granjaIdActual, 'hembras', tipoItem);
      }
      itemGroup.patchValue({ catalogItemId: null }, { emitEvent: false });
    });

    this.itemsHembrasArray.push(itemGroup);
  }

  agregarItemMachos(): void {
    const itemGroup = this.fb.group({
      tipoItem: [null, Validators.required],
      catalogItemId: [null, Validators.required],
      cantidad: [0, [Validators.required, Validators.min(0)]],
      unidad: ['kg', Validators.required],
      cantidadUnidades: [null]
    });

    // Suscribirse a cambios en tipoItem para cargar inventario
    itemGroup.get('tipoItem')?.valueChanges.subscribe(tipoItem => {
      if (tipoItem && this.granjaIdActual) {
        this.cargarInventarioGranja(this.granjaIdActual, 'machos', tipoItem);
      }
      itemGroup.patchValue({ catalogItemId: null }, { emitEvent: false });
    });

    this.itemsMachosArray.push(itemGroup);
  }

  eliminarItemHembras(index: number): void {
    this.itemsHembrasArray.removeAt(index);
  }

  eliminarItemMachos(index: number): void {
    this.itemsMachosArray.removeAt(index);
  }

  // ================== INVENTARIO ==================
  private cargarInventarioGranja(granjaId: number, sexo?: 'hembras' | 'machos', itemType?: string | null): void {
    if (!granjaId || this.cargandoInventarioGranja) return;

    this.cargandoInventarioGranja = true;

    this.inventarioSvc.getInventory(granjaId, itemType || undefined).pipe(
      catchError(err => {
        console.error('Error al cargar inventario:', err);
        return of([]);
      }),
      finalize(() => { this.cargandoInventarioGranja = false; })
    ).subscribe((items: FarmInventoryDto[]) => {
      const itemsActivos = items.filter(item => item.active && item.quantity > 0);

      const catalogItems = itemsActivos.map((item: FarmInventoryDto) => {
        const metadata = item.catalogItemMetadata || {};
        const itemType = metadata.itemType || metadata.type_item || 'alimento';

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
        this.inventarioPorItem.set(item.catalogItemId, { quantity: item.quantity, unit: item.unit });

        return catalogItem;
      });

      if (sexo === 'hembras') {
        this.alimentosFiltradosHembras = catalogItems;
      } else if (sexo === 'machos') {
        this.alimentosFiltradosMachos = catalogItems;
      } else {
        this.alimentosCatalog = catalogItems;
        this.alimentosFiltradosHembras = catalogItems;
        this.alimentosFiltradosMachos = catalogItems;
      }
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
    while (this.itemsHembrasArray.length !== 0) {
      this.itemsHembrasArray.removeAt(0);
    }
    while (this.itemsMachosArray.length !== 0) {
      this.itemsMachosArray.removeAt(0);
    }

    this.form.reset({
      fecha: this.todayYMD(),
      loteId: this.selectedLoteId || '',
      reproductoraId: this.selectedReproId || '',
      mortalidadH: 0,
      mortalidadM: 0,
      selH: 0,
      selM: 0,
      errorH: 0,
      errorM: 0,
      observaciones: '',
      ciclo: 'Normal',
      pesoInicial: null,
      pesoFinal: null,
      pesoPromH: null,
      pesoPromM: null,
      uniformidadH: null,
      uniformidadM: null,
      cvH: null,
      cvM: null,
      consumoAguaDiario: null,
      consumoAguaPh: null,
      consumoAguaOrp: null,
      consumoAguaTemperatura: null,
    });

    if (this.selectedLoteId) {
      const lote = this.lotes.find(l => String(l.loteId) === String(this.selectedLoteId));
      if (lote && lote.granjaId) {
        this.granjaIdActual = Number(lote.granjaId);
        this.cargarInventarioGranja(this.granjaIdActual);
      }
    }
  }

  private populateForm(): void {
    if (!this.editing) return;

    // Limpiar FormArrays
    while (this.itemsHembrasArray.length !== 0) {
      this.itemsHembrasArray.removeAt(0);
    }
    while (this.itemsMachosArray.length !== 0) {
      this.itemsMachosArray.removeAt(0);
    }

    // Cargar datos básicos
    const fecha = this.editing.fecha ? new Date(this.editing.fecha).toISOString().substring(0, 10) : this.todayYMD();
    this.form.patchValue({
      fecha: fecha,
      loteId: String(this.editing.loteId),
      reproductoraId: this.editing.reproductoraId,
      mortalidadH: this.editing.mortalidadH ?? 0,
      mortalidadM: this.editing.mortalidadM ?? 0,
      selH: this.editing.selH ?? 0,
      selM: this.editing.selM ?? 0,
      errorH: this.editing.errorH ?? 0,
      errorM: this.editing.errorM ?? 0,
      observaciones: this.editing.observaciones ?? '',
      ciclo: this.editing.ciclo ?? 'Normal',
      pesoInicial: this.editing.pesoInicial ?? null,
      pesoFinal: this.editing.pesoFinal ?? null,
      pesoPromH: this.editing.pesoPromH,
      pesoPromM: this.editing.pesoPromM,
      uniformidadH: this.editing.uniformidadH,
      uniformidadM: this.editing.uniformidadM,
      cvH: this.editing.cvH,
      cvM: this.editing.cvM,
      consumoAguaDiario: this.editing.consumoAguaDiario,
      consumoAguaPh: this.editing.consumoAguaPh,
      consumoAguaOrp: this.editing.consumoAguaOrp,
      consumoAguaTemperatura: this.editing.consumoAguaTemperatura,
    });

    // Cargar inventario si hay lote
    const lote = this.lotes.find(l => String(l.loteId) === String(this.editing!.loteId));
    if (lote && lote.granjaId) {
      this.granjaIdActual = Number(lote.granjaId);
      this.cargarInventarioGranja(this.granjaIdActual);
    }

    // Cargar items desde metadata
    if (this.editing.metadata?.itemsHembras && Array.isArray(this.editing.metadata.itemsHembras)) {
      this.editing.metadata.itemsHembras.forEach((item: ItemSeguimientoDto) => {
        const itemGroup = this.fb.group({
          tipoItem: [item.tipoItem || 'alimento', Validators.required],
          catalogItemId: [item.catalogItemId, Validators.required],
          cantidad: [item.cantidad || 0, [Validators.required, Validators.min(0)]],
          unidad: [item.unidad || 'kg', Validators.required],
          cantidadUnidades: [item.cantidadUnidades || null]
        });
        this.itemsHembrasArray.push(itemGroup);
      });
    }

    if (this.editing.metadata?.itemsMachos && Array.isArray(this.editing.metadata.itemsMachos)) {
      this.editing.metadata.itemsMachos.forEach((item: ItemSeguimientoDto) => {
        const itemGroup = this.fb.group({
          tipoItem: [item.tipoItem || 'alimento', Validators.required],
          catalogItemId: [item.catalogItemId, Validators.required],
          cantidad: [item.cantidad || 0, [Validators.required, Validators.min(0)]],
          unidad: [item.unidad || 'kg', Validators.required],
          cantidadUnidades: [item.cantidadUnidades || null]
        });
        this.itemsMachosArray.push(itemGroup);
      });
    }

    // Cargar items adicionales (no alimentos)
    if (this.editing.itemsAdicionales?.itemsHembras && Array.isArray(this.editing.itemsAdicionales.itemsHembras)) {
      this.editing.itemsAdicionales.itemsHembras.forEach((item: ItemSeguimientoDto) => {
        const itemGroup = this.fb.group({
          tipoItem: [item.tipoItem || 'medicamento', Validators.required],
          catalogItemId: [item.catalogItemId, Validators.required],
          cantidad: [item.cantidad || 0, [Validators.required, Validators.min(0)]],
          unidad: [item.unidad || 'unidades', Validators.required],
          cantidadUnidades: [item.cantidadUnidades || null]
        });
        this.itemsHembrasArray.push(itemGroup);
      });
    }

    if (this.editing.itemsAdicionales?.itemsMachos && Array.isArray(this.editing.itemsAdicionales.itemsMachos)) {
      this.editing.itemsAdicionales.itemsMachos.forEach((item: ItemSeguimientoDto) => {
        const itemGroup = this.fb.group({
          tipoItem: [item.tipoItem || 'medicamento', Validators.required],
          catalogItemId: [item.catalogItemId, Validators.required],
          cantidad: [item.cantidad || 0, [Validators.required, Validators.min(0)]],
          unidad: [item.unidad || 'unidades', Validators.required],
          cantidadUnidades: [item.cantidadUnidades || null]
        });
        this.itemsMachosArray.push(itemGroup);
      });
    }
  }

  // ================== GUARDAR ==================
  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.showValidationErrors = true;
      // Scroll al primer campo con error
      const firstError = document.querySelector('.form-field.ng-invalid');
      if (firstError) {
        firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
      return;
    }

    this.saving = true;
    this.showValidationErrors = false;

    const raw = this.form.value;

    // Construir metadata con items
    const itemsHembras: ItemSeguimientoDto[] = this.itemsHembrasArray.controls
      .map(control => {
        const v = control.value;
        return {
          tipoItem: v.tipoItem,
          catalogItemId: v.catalogItemId,
          cantidad: v.cantidad,
          unidad: v.unidad,
          cantidadUnidades: v.cantidadUnidades
        };
      })
      .filter(item => item.catalogItemId);

    const itemsMachos: ItemSeguimientoDto[] = this.itemsMachosArray.controls
      .map(control => {
        const v = control.value;
        return {
          tipoItem: v.tipoItem,
          catalogItemId: v.catalogItemId,
          cantidad: v.cantidad,
          unidad: v.unidad,
          cantidadUnidades: v.cantidadUnidades
        };
      })
      .filter(item => item.catalogItemId);

    // Separar alimentos de otros items
    const alimentosHembras = itemsHembras.filter(i => i.tipoItem === 'alimento');
    const otrosItemsHembras = itemsHembras.filter(i => i.tipoItem !== 'alimento');
    const alimentosMachos = itemsMachos.filter(i => i.tipoItem === 'alimento');
    const otrosItemsMachos = itemsMachos.filter(i => i.tipoItem !== 'alimento');

    // Calcular consumo total (solo alimentos)
    const consumoHembras = alimentosHembras.reduce((sum, item) => {
      const cantidadKg = item.unidad === 'g' ? item.cantidad / 1000 : item.cantidad;
      return sum + cantidadKg;
    }, 0);

    const consumoMachos = alimentosMachos.reduce((sum, item) => {
      const cantidadKg = item.unidad === 'g' ? item.cantidad / 1000 : item.cantidad;
      return sum + cantidadKg;
    }, 0);

    const metadata: any = {};
    if (itemsHembras.length > 0) metadata.itemsHembras = itemsHembras;
    if (itemsMachos.length > 0) metadata.itemsMachos = itemsMachos;

    const itemsAdicionales: any = {};
    if (otrosItemsHembras.length > 0) itemsAdicionales.itemsHembras = otrosItemsHembras;
    if (otrosItemsMachos.length > 0) itemsAdicionales.itemsMachos = otrosItemsMachos;

    const payload: CreateLoteSeguimientoDto | UpdateLoteSeguimientoDto = {
      fecha: new Date(raw.fecha).toISOString(),
      loteId: Number(raw.loteId),
      reproductoraId: raw.reproductoraId,
      mortalidadH: raw.mortalidadH,
      mortalidadM: raw.mortalidadM,
      selH: raw.selH,
      selM: raw.selM,
      errorH: raw.errorH,
      errorM: raw.errorM,
      tipoAlimento: alimentosHembras.length > 0 ? 'Mixto' : (raw.tipoAlimento || ''),
      consumoAlimento: consumoHembras,
      consumoKgMachos: consumoMachos,
      observaciones: raw.observaciones || null,
      ciclo: raw.ciclo || 'Normal',
      pesoInicial: raw.pesoInicial ?? null,
      pesoFinal: raw.pesoFinal ?? null,
      pesoPromH: raw.pesoPromH,
      pesoPromM: raw.pesoPromM,
      uniformidadH: raw.uniformidadH,
      uniformidadM: raw.uniformidadM,
      cvH: raw.cvH,
      cvM: raw.cvM,
      consumoAguaDiario: raw.consumoAguaDiario,
      consumoAguaPh: raw.consumoAguaPh,
      consumoAguaOrp: raw.consumoAguaOrp,
      consumoAguaTemperatura: raw.consumoAguaTemperatura,
      metadata: Object.keys(metadata).length > 0 ? metadata : null,
      itemsAdicionales: Object.keys(itemsAdicionales).length > 0 ? itemsAdicionales : null
    };

    if (this.editing) {
      (payload as UpdateLoteSeguimientoDto).id = this.editing.id;
    }

    this.save.emit({ data: payload, isEdit: !!this.editing });
    // El componente padre manejará el loading y los mensajes
    // Resetear saving después de un breve delay para permitir que el padre maneje el estado
    setTimeout(() => {
      this.saving = false;
    }, 100);
  }

  onClose(): void {
    this.close.emit();
  }

  // Helper para convertir a string
  toString(value: any): string {
    return String(value);
  }
}

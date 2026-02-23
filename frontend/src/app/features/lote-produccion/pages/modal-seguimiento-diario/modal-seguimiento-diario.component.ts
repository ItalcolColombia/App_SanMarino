import { Component, Input, Output, EventEmitter, OnInit, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { CrearSeguimientoRequest, SeguimientoItemDto } from '../../services/produccion.service';
import { CatalogoAlimentosService, CatalogItemDto, CatalogItemType } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { InventarioService, FarmInventoryDto } from '../../../inventario/services/inventario.service';
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

@Component({
  selector: 'app-modal-seguimiento-diario',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ConfirmationModalComponent],
  templateUrl: './modal-seguimiento-diario.component.html',
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
  
  // Flag para mostrar tab de agua (solo Ecuador y Panamá)
  isEcuadorOrPanama: boolean = false;

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
    private countryFilter: CountryFilterService,
    private storage: TokenStorageService
  ) { }

  ngOnInit(): void {
    this.initializeForm();
    this.checkCountry();
  }
  
  private checkCountry(): void {
    this.isEcuadorOrPanama = this.countryFilter.isEcuadorOrPanama();
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
      // Cargar inventario de la granja si está disponible (sin filtro inicial)
      if (this.granjaId) {
        this.cargarInventarioGranja(this.granjaId);
      }

      if (this.editingSeguimiento) {
        this.populateForm();
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

  agregarItemHembras(): void {
    const itemForm = this.fb.group({
      tipoItem: [null, Validators.required],
      catalogItemId: [null, Validators.required],
      cantidad: [0, [Validators.required, Validators.min(0)]],
      unidad: ['kg', Validators.required]
    });
    itemForm.get('tipoItem')?.valueChanges.subscribe(tipo => {
      if (tipo === 'alimento') itemForm.patchValue({ unidad: 'kg' }, { emitEvent: false });
      else if (tipo) itemForm.patchValue({ unidad: 'unidades' }, { emitEvent: false });
      itemForm.patchValue({ catalogItemId: null }, { emitEvent: false });
    });
    this.itemsHembrasArray.push(itemForm);
  }

  eliminarItemHembras(index: number): void {
    this.itemsHembrasArray.removeAt(index);
  }

  agregarItemMachos(): void {
    const itemForm = this.fb.group({
      tipoItem: [null, Validators.required],
      catalogItemId: [null, Validators.required],
      cantidad: [0, [Validators.required, Validators.min(0)]],
      unidad: ['kg', Validators.required]
    });
    itemForm.get('tipoItem')?.valueChanges.subscribe(tipo => {
      if (tipo === 'alimento') itemForm.patchValue({ unidad: 'kg' }, { emitEvent: false });
      else if (tipo) itemForm.patchValue({ unidad: 'unidades' }, { emitEvent: false });
      itemForm.patchValue({ catalogItemId: null }, { emitEvent: false });
    });
    this.itemsMachosArray.push(itemForm);
  }

  eliminarItemMachos(index: number): void {
    this.itemsMachosArray.removeAt(index);
  }

  getAlimentosFiltradosPorTipo(tipoItem: string | null): CatalogItemExtended[] {
    if (!tipoItem) return this.alimentosCatalog;
    return this.alimentosCatalog.filter(a => {
      const t = a.tipoItem || (a as any).metadata?.type_item || (a as any).metadata?.itemType;
      return t === tipoItem;
    });
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
  }

  private populateForm(): void {
    if (!this.editingSeguimiento) return;

    while (this.itemsHembrasArray.length) this.itemsHembrasArray.removeAt(0);
    while (this.itemsMachosArray.length) this.itemsMachosArray.removeAt(0);

    const metadata: any = this.editingSeguimiento.metadata || {};
    const itemsHembras = metadata?.itemsHembras ?? [];
    const itemsMachos = metadata?.itemsMachos ?? [];
    (itemsHembras as any[]).forEach((item: any) => {
      this.itemsHembrasArray.push(this.fb.group({
        tipoItem: [item.tipoItem ?? 'alimento', Validators.required],
        catalogItemId: [item.catalogItemId ?? null, Validators.required],
        cantidad: [Number(item.cantidad) ?? 0, [Validators.required, Validators.min(0)]],
        unidad: [item.unidad ?? 'kg', Validators.required]
      }));
    });
    (itemsMachos as any[]).forEach((item: any) => {
      this.itemsMachosArray.push(this.fb.group({
        tipoItem: [item.tipoItem ?? 'alimento', Validators.required],
        catalogItemId: [item.catalogItemId ?? null, Validators.required],
        cantidad: [Number(item.cantidad) ?? 0, [Validators.required, Validators.min(0)]],
        unidad: [item.unidad ?? 'kg', Validators.required]
      }));
    });
    if (itemsHembras.length === 0 && itemsMachos.length === 0) {
      const consH = metadata?.consumoOriginalHembras ?? this.editingSeguimiento.consKgH ?? 0;
      const unidH = metadata?.unidadConsumoOriginalHembras ?? 'kg';
      const consM = metadata?.consumoOriginalMachos ?? this.editingSeguimiento.consKgM ?? 0;
      const unidM = metadata?.unidadConsumoOriginalMachos ?? 'kg';
      const tipoAlimentoH = metadata?.tipoAlimentoHembras ?? null;
      const tipoAlimentoM = metadata?.tipoAlimentoMachos ?? null;
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

    const fechaRegistro = this.toYMD(this.editingSeguimiento.fechaRegistro);
    const consumoOriginalHembras = metadata?.consumoOriginalHembras ?? this.editingSeguimiento.consKgH;
    const unidadConsumoOriginalHembras = metadata?.unidadConsumoOriginalHembras ?? 'kg';
    const consumoOriginalMachos = metadata?.consumoOriginalMachos ?? this.editingSeguimiento.consKgM;
    const unidadConsumoOriginalMachos = metadata?.unidadConsumoOriginalMachos ?? 'kg';
    const tipoItemHembras = metadata?.tipoItemHembras ?? null;
    const tipoItemMachos = metadata?.tipoItemMachos ?? null;
    const tipoAlimentoHembras = metadata?.tipoAlimentoHembras ?? null;
    const tipoAlimentoMachos = metadata?.tipoAlimentoMachos ?? null;

    // Convertir a gramos si la unidad original es kg y el valor es pequeño (para mejor UX)
    let consumoHembrasDisplay = consumoOriginalHembras;
    if (unidadConsumoOriginalHembras === 'kg' && consumoOriginalHembras < 1) {
      consumoHembrasDisplay = consumoOriginalHembras * 1000;
    }
    
    let consumoMachosDisplay = consumoOriginalMachos;
    if (unidadConsumoOriginalMachos === 'kg' && consumoOriginalMachos < 1) {
      consumoMachosDisplay = consumoOriginalMachos * 1000;
    }

    this.form.patchValue({
      fechaRegistro: fechaRegistro,
      produccionLoteId: this.editingSeguimiento.lotePosturaProduccionId ? null : this.editingSeguimiento.produccionLoteId,
      lotePosturaProduccionId: this.editingSeguimiento.lotePosturaProduccionId ?? null,
      mortalidadH: this.editingSeguimiento.mortalidadH,
      mortalidadM: this.editingSeguimiento.mortalidadM,
      selH: this.editingSeguimiento.selH || 0,
      selM: (this.editingSeguimiento as any).selM || 0,
      errorSexajeHembras: (this.editingSeguimiento as any).errorSexajeHembras ?? 0,
      errorSexajeMachos: (this.editingSeguimiento as any).errorSexajeMachos ?? 0,
      ciclo: (this.editingSeguimiento as any).ciclo || 'Normal',
      consumoHembras: consumoHembrasDisplay,
      unidadConsumoHembras: unidadConsumoOriginalHembras === 'kg' && consumoOriginalHembras < 1 ? 'g' : unidadConsumoOriginalHembras,
      consumoMachos: consumoMachosDisplay,
      unidadConsumoMachos: unidadConsumoOriginalMachos === 'kg' && consumoOriginalMachos < 1 ? 'g' : unidadConsumoOriginalMachos,
      tipoItemHembras: tipoItemHembras,
      tipoItemMachos: tipoItemMachos,
      tipoAlimentoHembras: tipoAlimentoHembras,
      tipoAlimentoMachos: tipoAlimentoMachos,
      huevosTotales: this.editingSeguimiento.huevosTotales,
      huevosIncubables: this.editingSeguimiento.huevosIncubables,
      huevoLimpio: (this.editingSeguimiento as any).huevoLimpio || 0,
      huevoTratado: (this.editingSeguimiento as any).huevoTratado || 0,
      huevoSucio: (this.editingSeguimiento as any).huevoSucio || 0,
      huevoDeforme: (this.editingSeguimiento as any).huevoDeforme || 0,
      huevoBlanco: (this.editingSeguimiento as any).huevoBlanco || 0,
      huevoDobleYema: (this.editingSeguimiento as any).huevoDobleYema || 0,
      huevoPiso: (this.editingSeguimiento as any).huevoPiso || 0,
      huevoPequeno: (this.editingSeguimiento as any).huevoPequeno || 0,
      huevoRoto: (this.editingSeguimiento as any).huevoRoto || 0,
      huevoDesecho: (this.editingSeguimiento as any).huevoDesecho || 0,
      huevoOtro: (this.editingSeguimiento as any).huevoOtro || 0,
      tipoAlimento: this.editingSeguimiento.tipoAlimento || 'Standard',
      pesoHuevo: this.editingSeguimiento.pesoHuevo,
      etapa: this.editingSeguimiento.etapa || this.calcularEtapa(fechaRegistro || this.todayYMD()),
      observaciones: this.editingSeguimiento.observaciones || '',
      // Campos de Pesaje Semanal / por sexo
      pesoH: (this.editingSeguimiento as any).pesoH || null,
      pesoM: (this.editingSeguimiento as any).pesoM || null,
      uniformidad: (this.editingSeguimiento as any).uniformidad || null,
      coeficienteVariacion: (this.editingSeguimiento as any).coeficienteVariacion || null,
      uniformidadHembras: (this.editingSeguimiento as any).uniformidadHembras ?? null,
      uniformidadMachos: (this.editingSeguimiento as any).uniformidadMachos ?? null,
      cvHembras: (this.editingSeguimiento as any).cvHembras ?? null,
      cvMachos: (this.editingSeguimiento as any).cvMachos ?? null,
      observacionesPesaje: (this.editingSeguimiento as any).observacionesPesaje || '',
      // Campos de agua (solo para Ecuador y Panamá)
      consumoAguaDiario: (this.editingSeguimiento as any).consumoAguaDiario ?? null,
      consumoAguaPh: (this.editingSeguimiento as any).consumoAguaPh ?? null,
      consumoAguaOrp: (this.editingSeguimiento as any).consumoAguaOrp ?? null,
      consumoAguaTemperatura: (this.editingSeguimiento as any).consumoAguaTemperatura ?? null
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

    const raw = this.form.value;
    const ymd = this.toYMD(raw.fechaRegistro);

    if (!ymd) {
      this.showErrorMessage('La fecha de registro es inválida. Por favor, seleccione una fecha válida.');
      return;
    }

    const itemsHembras = this.itemsHembrasArray.controls
      .map(c => ({ tipoItem: c.get('tipoItem')?.value, catalogItemId: c.get('catalogItemId')?.value, cantidad: Number(c.get('cantidad')?.value) || 0, unidad: c.get('unidad')?.value || 'kg' }))
      .filter((x: any) => x.tipoItem && x.catalogItemId);
    const itemsMachos = this.itemsMachosArray.controls
      .map(c => ({ tipoItem: c.get('tipoItem')?.value, catalogItemId: c.get('catalogItemId')?.value, cantidad: Number(c.get('cantidad')?.value) || 0, unidad: c.get('unidad')?.value || 'kg' }))
      .filter((x: any) => x.tipoItem && x.catalogItemId);
    const useItems = itemsHembras.length > 0 || itemsMachos.length > 0;
    let tipoAlimentoVal = raw.tipoAlimento || 'Standard';
    if (useItems) {
      const nombres: string[] = [];
      [...itemsHembras, ...itemsMachos].forEach((it: any) => {
        if (it.tipoItem === 'alimento') {
          const a = this.alimentosById.get(it.catalogItemId);
          if (a?.nombre) nombres.push(a.nombre);
        }
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
  
  /**
   * Carga el inventario de la granja filtrado por tipo de item desde el backend
   * El backend filtra por empresa, país, granja y tipo de item
   */
  cargarInventarioGranja(granjaId: number, sexo?: 'hembras' | 'machos', itemType?: string | null): void {
    if (!granjaId) return;

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

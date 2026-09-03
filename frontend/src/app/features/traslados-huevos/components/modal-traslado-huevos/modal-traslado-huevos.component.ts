// frontend/src/app/features/traslados-huevos/components/modal-traslado-huevos/modal-traslado-huevos.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { catchError } from 'rxjs/operators';
import { of } from 'rxjs';
import { TrasladosHuevosService, DisponibilidadLoteDto, CrearTrasladoHuevosDto, ActualizarTrasladoHuevosDto, HuevosDisponiblesDto, TrasladoHuevosDto } from '../../services/traslados-huevos.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { MasterListService } from '../../../../core/services/master-list/master-list.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { InventarioService } from '../../../inventario/services/inventario.service';
import { CatalogItemDto } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { HuevoCatalogGrupo, HuevoCatalogOption, ITEM_TYPE_HUEVO } from '../../../lote-produccion/models/huevo-clasificacion.model';
import { HuevoItemSeguimiento } from '../../../lote-produccion/services/produccion.service';
import {
  agruparItemsHuevoPorTipo,
  fusionarItemsHuevoGuardados,
  mapearItemsHuevoACatalogo,
  sumarCantidadesHuevo
} from '../../../lote-produccion/funciones/items-huevo-catalogo.funcion';
import {
  extremosVentanaRegistro,
  hintVentanaFechaRegistro,
  PERMISO_FECHA_RETROACTIVA
} from '../../../../shared/utils/fecha/ventana-fecha-registro.funcion';

@Component({
  selector: 'app-modal-traslado-huevos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './modal-traslado-huevos.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./modal-traslado-huevos.component.scss']
})
export class ModalTrasladoHuevosComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() loteId: number | null = null;
  /** Lote LPP: cuando está presente, se usa flujo LPP (espejo, filter-data) */
  @Input() lotePosturaProduccionId: number | null = null;
  @Input() editingTraslado: TrasladoHuevosDto | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<TrasladoHuevosDto>();
  /** Solicitud de anular un registro Completado (misma acción que Eliminar en la lista: devuelve inventario). */
  @Output() anular = new EventEmitter<TrasladoHuevosDto>();

  // Formulario
  formHuevos!: FormGroup;

  // Estado
  disponibilidad = signal<DisponibilidadLoteDto | null>(null);
  loteInfo = signal<LoteDto | null>(null);
  loading = signal<boolean>(false);
  loadingDisponibilidad = signal<boolean>(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);
  showSuccessModal = signal<boolean>(false);
  showErrorModal = signal<boolean>(false);
  successMessage = signal<string>('');
  errorMessage = signal<string>('');
  isEditMode = signal<boolean>(false);
  isReadOnly = computed(() => {
    return this.editingTraslado?.estado === 'Completado' || this.editingTraslado?.estado === 'Cancelado';
  });

  // Signal para rastrear cambios en tipoOperacion del formulario
  tipoOperacionForm = signal<string>('');

  // Computed properties para títulos y botones dinámicos
  modalTitle = computed(() => {
    if (this.editingTraslado && this.isReadOnly()) {
      const tipo = this.editingTraslado.tipoOperacion?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
      return `Detalle de ${tipo} de Huevos`;
    }
    if (this.editingTraslado && !this.isReadOnly() && this.isEditMode()) {
      const tipo = this.editingTraslado.tipoOperacion?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
      return `Editar ${tipo} de Huevos`;
    }
    // Si no hay editingTraslado, usar el valor del formulario
    const tipoOperacion = this.tipoOperacionForm() || this.formHuevos?.get('tipoOperacion')?.value || '';
    const tipo = tipoOperacion.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
    return `Nuevo ${tipo} de Huevos`;
  });

  saveButtonText = computed(() => {
    if (this.editingTraslado && this.isEditMode()) {
      return 'Actualizar Registro';
    }
    const tipoOperacion = this.tipoOperacionForm() || this.formHuevos?.get('tipoOperacion')?.value || '';
    if (tipoOperacion.toLowerCase().includes('venta')) {
      return 'Guardar Venta';
    }
    return 'Guardar Traslado';
  });

  /** Valores fijos alineados con el backend (`TrasladoHuevos.TipoOperacion`). */
  readonly tiposOperacionHuevos: readonly ('Traslado' | 'Venta')[] = ['Traslado', 'Venta'];

  /** Traslado: destino fijo en BD como `Planta` (UI deshabilitada). */
  readonly tipoDestinoTrasladoFijo = 'Planta';

  /** Destinos para venta. */
  readonly tiposDestinoVenta: readonly string[] = ['Cliente', 'Empresa', 'Planta'];

  // Plantas destino desde lista maestra (solo venta → Planta)
  plantasDestino = signal<string[]>([]);

  // ===== Clasificación de huevos por ÍTEMS (flag de empresa · Santa Reyes, ver F10 §9 del plan) =====
  /** Flag `companies.clasificacion_huevo_por_items`: reemplaza la grilla de 11 tipos fijos por filas ítem+cantidad. */
  clasificacionHuevoPorItems = false;
  gruposHuevoItems: HuevoCatalogGrupo[] = [];
  totalHuevosClasificados = 0;
  cargandoItemsHuevo = false;
  private huevoItemsById = new Map<number, HuevoCatalogOption>();
  private huevoItemsGuardados = new Map<number, HuevoItemSeguimiento>();

  /** Flag para cargar catálogos solo una vez al abrir el modal */
  private catalogosCargados = false;

  // Tipos de huevo para el formulario
  tiposHuevo: Array<{ key: string; label: string; disponible: () => number }> = [
    { key: 'limpio', label: 'Limpio', disponible: () => this.disponibilidad()?.huevos?.limpio ?? 0 },
    { key: 'tratado', label: 'Tratado', disponible: () => this.disponibilidad()?.huevos?.tratado ?? 0 },
    { key: 'sucio', label: 'Sucio', disponible: () => this.disponibilidad()?.huevos?.sucio ?? 0 },
    { key: 'deforme', label: 'Deforme', disponible: () => this.disponibilidad()?.huevos?.deforme ?? 0 },
    { key: 'blanco', label: 'Blanco', disponible: () => this.disponibilidad()?.huevos?.blanco ?? 0 },
    { key: 'dobleYema', label: 'Doble Yema', disponible: () => this.disponibilidad()?.huevos?.dobleYema ?? 0 },
    { key: 'piso', label: 'Piso', disponible: () => this.disponibilidad()?.huevos?.piso ?? 0 },
    { key: 'pequeno', label: 'Pequeño', disponible: () => this.disponibilidad()?.huevos?.pequeno ?? 0 },
    { key: 'roto', label: 'Roto', disponible: () => this.disponibilidad()?.huevos?.roto ?? 0 },
    { key: 'desecho', label: 'Desecho', disponible: () => this.disponibilidad()?.huevos?.desecho ?? 0 },
    { key: 'otro', label: 'Otro', disponible: () => this.disponibilidad()?.huevos?.otro ?? 0 }
  ];

  constructor(
    private fb: FormBuilder,
    private trasladosService: TrasladosHuevosService,
    private loteService: LoteService,
    private masterListService: MasterListService,
    private userPermService: UserPermissionService,
    private companyConfig: ActiveCompanyConfigService,
    private inventarioSvc: InventarioService,
    private toast: ToastService
  ) {
    this.initForm();
    this.companyConfig.getFlags().subscribe(flags => {
      if (this.clasificacionHuevoPorItems === flags.clasificacionHuevoPorItems) return;
      this.clasificacionHuevoPorItems = flags.clasificacionHuevoPorItems;
      if (!this.clasificacionHuevoPorItems) return;
      this.cargarItemsHuevo();
      if (this.editingTraslado) this.rehidratarHuevoItems();
      else this.asegurarFilaHuevoInicial();
    });
  }

  ngOnInit(): void {
    // Catálogos se cargan al abrir el modal (ngOnChanges isOpen) para evitar llamadas múltiples
  }

  /** Ventana de fechas admitida para `fechaTraslado` (mes en curso ∪ últimos 15 días). */
  private get puedeRetroactivar(): boolean {
    return this.userPermService.has(PERMISO_FECHA_RETROACTIVA);
  }

  get fechaTrasladoMin(): string | null {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).min;
  }

  get fechaTrasladoMax(): string {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).max;
  }

  get fechaTrasladoHint(): string {
    return hintVentanaFechaRegistro(new Date(), this.puedeRetroactivar);
  }

  /** Carga Farm y listas maestras solo cuando se abre el modal y una sola vez */
  private cargarCatalogosSiNecesario(): void {
    if (this.catalogosCargados) return;
    this.catalogosCargados = true;
    this.cargarPlantasDestino();
  }

  private cargarPlantasDestino(): void {
    this.masterListService.getByKey('traslado_de_huevos_planta_destino').subscribe({
      next: (masterList) => {
        if (masterList && (masterList.optionValues ?? masterList.options?.length)) {
          this.plantasDestino.set(masterList.optionValues ?? (masterList.options as { value?: string }[]).map(o => o?.value ?? ''));
        } else {
          this.plantasDestino.set([]);
        }
      },
      error: (error) => {
        console.error('Error cargando plantas destino desde lista maestra:', error);
        this.plantasDestino.set([]);
      }
    });
  }

  get huevoItemsArray(): FormArray {
    return this.formHuevos.get('huevoItems') as FormArray;
  }

  /** Catálogo de ítems de huevo de la empresa activa (mismo patrón que F7, modal-seguimiento-diario). */
  private cargarItemsHuevo(): void {
    if (this.cargandoItemsHuevo || this.huevoItemsById.size > 0) return;
    this.cargandoItemsHuevo = true;
    this.inventarioSvc.getCatalogoByType(ITEM_TYPE_HUEVO).pipe(
      catchError(err => {
        console.error('Error al cargar ítems de huevo:', err);
        return of([] as CatalogItemDto[]);
      })
    ).subscribe((items: CatalogItemDto[]) => {
      this.cargandoItemsHuevo = false;
      const opciones = mapearItemsHuevoACatalogo(items ?? []);
      this.huevoItemsById.clear();
      opciones.forEach(o => this.huevoItemsById.set(o.id, o));
      this.refrescarGruposHuevoItems();
    });
  }

  private refrescarGruposHuevoItems(): void {
    const opciones = fusionarItemsHuevoGuardados(
      Array.from(this.huevoItemsById.values()),
      Array.from(this.huevoItemsGuardados.values())
    );
    this.gruposHuevoItems = agruparItemsHuevoPorTipo(opciones);
  }

  private get totalItemsHuevoOfrecidos(): number {
    let total = 0;
    for (const g of this.gruposHuevoItems) total += g.items.length;
    return total;
  }

  private crearFilaHuevo(catalogItemId: number | null = null, cantidad: number = 0): FormGroup {
    const grp = this.fb.group({
      catalogItemId: [catalogItemId],
      cantidad: [cantidad, [Validators.min(0)]]
    });
    grp.get('catalogItemId')!.valueChanges.subscribe(valor => this.onCambioItemHuevo(grp, valor));
    return grp;
  }

  private onCambioItemHuevo(fila: FormGroup, valor: unknown): void {
    const id = Number(valor) || 0;
    if (!id) return;
    const duplicado = this.huevoItemsArray.controls
      .some(c => c !== fila && Number(c.get('catalogItemId')?.value) === id);
    if (!duplicado) return;
    fila.get('catalogItemId')!.setValue(null, { emitEvent: false });
    this.toast.warning(
      'Ese ítem de huevo ya está en otra fila. Sumá la cantidad en la fila existente.',
      'Ítem duplicado'
    );
  }

  agregarFilaHuevo(): void {
    const tope = this.totalItemsHuevoOfrecidos;
    if (tope > 0 && this.huevoItemsArray.length >= tope) {
      this.toast.warning('Ya hay una fila por cada ítem de huevo del catálogo.', 'Clasificación de huevos');
      return;
    }
    this.huevoItemsArray.push(this.crearFilaHuevo());
  }

  eliminarFilaHuevo(index: number): void {
    this.huevoItemsArray.removeAt(index);
  }

  itemHuevoUsadoEnOtraFila(catalogItemId: number, index: number): boolean {
    const controls = this.huevoItemsArray.controls;
    for (let i = 0; i < controls.length; i++) {
      if (i === index) continue;
      if (Number(controls[i].get('catalogItemId')?.value) === catalogItemId) return true;
    }
    return false;
  }

  detalleItemHuevo(catalogItemId: unknown): string {
    const id = Number(catalogItemId) || 0;
    if (!id) return '';
    const item = this.huevoItemsById.get(id);
    const guardado = this.huevoItemsGuardados.get(id);
    const tipo = item?.tipoHuevo ?? guardado?.tipoHuevo ?? '';
    const um = item?.um ?? guardado?.um ?? '';
    if (tipo && um) return `${tipo} · ${um}`;
    return tipo || um || '';
  }

  /** Disponible del ítem según `disponibilidad()?.huevoItemsDisponibles` (F10). */
  disponibleItemHuevo(catalogItemId: unknown): number {
    const id = Number(catalogItemId) || 0;
    if (!id) return 0;
    const fila = this.disponibilidad()?.huevoItemsDisponibles?.find(d => d.catalogItemId === id);
    return fila?.cantidad ?? 0;
  }

  private asegurarFilaHuevoInicial(): void {
    if (!this.clasificacionHuevoPorItems) return;
    if (this.huevoItemsArray.length > 0) return;
    this.huevoItemsArray.push(this.crearFilaHuevo());
  }

  /** Edición/vista: reconstruye las filas desde `editingTraslado.huevoItems`. */
  private rehidratarHuevoItems(): void {
    const guardados = this.editingTraslado?.huevoItems ?? [];
    this.huevoItemsGuardados.clear();
    guardados.forEach(i => this.huevoItemsGuardados.set(i.catalogItemId, i));
    while (this.huevoItemsArray.length) this.huevoItemsArray.removeAt(0);
    guardados.forEach(i => this.huevoItemsArray.push(this.crearFilaHuevo(i.catalogItemId, i.cantidad)));
    this.refrescarGruposHuevoItems();
    if (this.huevoItemsArray.length === 0) this.asegurarFilaHuevoInicial();
    if (this.isReadOnly()) this.huevoItemsArray.disable({ emitEvent: false });
  }

  private recalcularTotalHuevosClasificados(): void {
    this.totalHuevosClasificados = sumarCantidadesHuevo(
      this.huevoItemsArray.controls.map(c => c.get('cantidad')?.value)
    );
  }

  /** Payload `huevoItems`: filas con ítem elegido, completadas con los datos del catálogo. */
  private construirHuevoItemsPayload(): HuevoItemSeguimiento[] {
    const filas: HuevoItemSeguimiento[] = [];
    for (const control of this.huevoItemsArray.controls) {
      const catalogItemId = Number(control.get('catalogItemId')?.value) || 0;
      const cantidad = Number(control.get('cantidad')?.value) || 0;
      if (catalogItemId <= 0 || cantidad <= 0) continue;
      const item = this.huevoItemsById.get(catalogItemId);
      const guardado = this.huevoItemsGuardados.get(catalogItemId);
      filas.push({
        catalogItemId,
        codigo: item?.codigo ?? guardado?.codigo ?? null,
        nombre: item?.nombre ?? guardado?.nombre ?? null,
        tipoHuevo: item?.tipoHuevo ?? guardado?.tipoHuevo ?? null,
        cantidad,
        um: item?.um ?? guardado?.um ?? null
      });
    }
    return filas;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      this.cargarCatalogosSiNecesario();
      if (this.editingTraslado) {
        if (this.isReadOnly()) {
          this.isEditMode.set(false);
        } else {
          this.isEditMode.set(true);
        }
        this.loadEditingData();
        if (this.clasificacionHuevoPorItems) this.rehidratarHuevoItems();
        if (this.editingTraslado.lotePosturaProduccionId) {
          this.cargarDisponibilidadLPP(this.editingTraslado.lotePosturaProduccionId);
          this.loteInfo.set({
            loteId: this.editingTraslado.lotePosturaProduccionId,
            loteNombre: this.editingTraslado.loteNombre,
            granjaId: 0
          } as LoteDto);
        } else if (this.editingTraslado.loteId) {
          const loteIdNum = Number(this.editingTraslado.loteId);
          if (!isNaN(loteIdNum)) {
            this.cargarDisponibilidad(this.editingTraslado.loteId);
            this.cargarLoteInfo(loteIdNum);
          }
        }
      } else {
        this.isEditMode.set(false);
        this.resetForm();
        if (this.lotePosturaProduccionId) {
          this.cargarDisponibilidadLPP(this.lotePosturaProduccionId);
        } else if (this.loteId) {
          this.cargarLoteInfo(this.loteId);
          this.cargarDisponibilidad(this.loteId.toString());
        }
      }
    }
    
    if (changes['editingTraslado']) {
      if (this.editingTraslado) {
        // Si el estado es Completado o Cancelado, es solo lectura
        if (this.isReadOnly()) {
          this.isEditMode.set(false);
        } else {
          // Por defecto, si es Pendiente, activar modo edición
          this.isEditMode.set(true);
        }
        this.loadEditingData();
        if (this.clasificacionHuevoPorItems) this.rehidratarHuevoItems();
        // Cargar el lote desde el traslado
        if (this.editingTraslado.lotePosturaProduccionId) {
          this.cargarDisponibilidadLPP(this.editingTraslado.lotePosturaProduccionId);
          this.loteInfo.set({ loteId: this.editingTraslado.lotePosturaProduccionId, loteNombre: this.editingTraslado.loteNombre, granjaId: 0 } as LoteDto);
        } else if (this.editingTraslado.loteId) {
          const loteIdNum = Number(this.editingTraslado.loteId);
          if (!isNaN(loteIdNum)) {
            this.cargarDisponibilidad(this.editingTraslado.loteId);
            this.cargarLoteInfo(loteIdNum);
          }
        }
      } else {
        this.isEditMode.set(false);
        this.resetForm();
      }
    }
  }

  private cargarDisponibilidadLPP(lotePosturaProduccionId: number): void {
    this.loadingDisponibilidad.set(true);
    this.error.set(null);
    this.trasladosService.getDisponibilidadLoteLPP(lotePosturaProduccionId).subscribe({
      next: (disp) => {
        this.disponibilidad.set(disp);
        this.loteInfo.set({
          loteId: disp.loteId,
          loteNombre: disp.loteNombre,
          granjaId: disp.granjaId
        } as LoteDto);
        this.loadingDisponibilidad.set(false);
      },
      error: () => {
        this.error.set('Error al cargar disponibilidad del lote');
        this.disponibilidad.set(null);
        this.loadingDisponibilidad.set(false);
      }
    });
  }

  private cargarLoteInfo(loteId?: number): void {
    const idToLoad = loteId || this.loteId;
    if (!idToLoad) return;

    this.loteService.getById(idToLoad).subscribe({
      next: (lote) => {
        this.loteInfo.set(lote);
      },
      error: (error) => {
        console.error('Error cargando información del lote:', error);
        this.loteInfo.set(null);
      }
    });
  }

  private initForm(): void {
    const hoyHuevos = new Date().toISOString().split('T')[0];
    const tipoOperacionDefault = this.tiposOperacionHuevos[0];
    // Inicializar signal con el valor por defecto
    this.tipoOperacionForm.set(tipoOperacionDefault);
    
    const huevosControls: any = {
      loteId: ['', [Validators.required]],
      fechaTraslado: [hoyHuevos, [Validators.required]],
      tipoOperacion: [tipoOperacionDefault, [Validators.required]],
      tipoDestino: [this.tipoDestinoTrasladoFijo],
      granjaDestinoId: [null],
      plantaDestino: [null],
      loteDestinoId: [null],
      motivo: [null],
      descripcion: [null],
      observaciones: [null]
    };

    this.tiposHuevo.forEach(tipo => {
      huevosControls[`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`] = [0, [Validators.min(0)]];
    });
    huevosControls['huevoItems'] = this.fb.array([]);

    this.formHuevos = this.fb.group(huevosControls, { validators: this.validarTrasladoHuevos.bind(this) });
    this.huevoItemsArray.valueChanges.subscribe(() => this.recalcularTotalHuevosClasificados());

    this.aplicarModoTipoOperacion(tipoOperacionDefault);

    this.formHuevos.get('tipoOperacion')?.valueChanges.subscribe((tipo) => {
      if (this.esTipoVenta(tipo)) {
        this.formHuevos.patchValue(
          {
            tipoDestino: null,
            granjaDestinoId: null,
            plantaDestino: null,
            loteDestinoId: null,
            observaciones: null
          },
          { emitEvent: false }
        );
      } else {
        this.formHuevos.patchValue(
          {
            tipoDestino: this.tipoDestinoTrasladoFijo,
            granjaDestinoId: null,
            plantaDestino: null,
            loteDestinoId: null,
            descripcion: null,
            motivo: null
          },
          { emitEvent: false }
        );
      }
      this.aplicarModoTipoOperacion(tipo);
      this.actualizarValidadoresDestino(tipo ?? '');
      this.tipoOperacionForm.set(tipo || '');
    });

    this.formHuevos.get('tipoDestino')?.valueChanges.subscribe(() => {
      this.formHuevos.patchValue(
        { granjaDestinoId: null, plantaDestino: null, loteDestinoId: null },
        { emitEvent: false }
      );
      this.actualizarValidadoresDestino(this.formHuevos.get('tipoOperacion')?.value ?? '');
    });
  }

  /** Traslado: destino Planta fijo y control deshabilitado; Venta: selector habilitado. */
  private aplicarModoTipoOperacion(tipo: string | null | undefined): void {
    const tdCtrl = this.formHuevos.get('tipoDestino');
    if (!tdCtrl) return;
    if (this.esTipoTraslado(tipo)) {
      tdCtrl.patchValue(this.tipoDestinoTrasladoFijo, { emitEvent: false });
      if (!this.isReadOnly()) {
        tdCtrl.disable({ emitEvent: false });
      }
    } else {
      tdCtrl.enable({ emitEvent: false });
    }
  }

  private loadEditingData(): void {
    if (!this.editingTraslado) return;

    const t = this.editingTraslado;
    const fecha = t.fechaTraslado instanceof Date 
      ? t.fechaTraslado.toISOString().split('T')[0]
      : new Date(t.fechaTraslado).toISOString().split('T')[0];

    // Determinar si el loteDestinoId es una planta (está en la lista de plantas)
    const loteDestinoEsPlanta = t.loteDestinoId && this.plantasDestino().includes(t.loteDestinoId);
    const plantaDestinoValue = loteDestinoEsPlanta ? t.loteDestinoId : null;

    // Si estamos en modo solo lectura, deshabilitar el formulario
    if (this.isReadOnly()) {
      this.formHuevos.disable();
    } else {
      this.formHuevos.enable();
      // Mantener loteId deshabilitado si estamos editando
      this.formHuevos.get('loteId')?.disable();
    }

    this.formHuevos.patchValue({
      loteId: t.loteId,
      fechaTraslado: fecha,
      tipoOperacion: this.normalizarTipoOperacion(t.tipoOperacion),
      cantidadLimpio: t.cantidadLimpio,
      cantidadTratado: t.cantidadTratado,
      cantidadSucio: t.cantidadSucio,
      cantidadDeforme: t.cantidadDeforme,
      cantidadBlanco: t.cantidadBlanco,
      cantidadDobleYema: t.cantidadDobleYema,
      cantidadPiso: t.cantidadPiso,
      cantidadPequeno: t.cantidadPequeno,
      cantidadRoto: t.cantidadRoto,
      cantidadDesecho: t.cantidadDesecho,
      cantidadOtro: t.cantidadOtro,
      tipoDestino:
        this.normalizarTipoOperacion(t.tipoOperacion) === 'Traslado'
          ? this.tipoDestinoTrasladoFijo
          : this.normalizarTipoDestinoVenta(t.tipoDestino),
      granjaDestinoId: t.granjaDestinoId,
      plantaDestino: plantaDestinoValue,
      loteDestinoId: loteDestinoEsPlanta ? null : t.loteDestinoId,
      motivo: t.motivo,
      descripcion: t.descripcion,
      observaciones: t.observaciones
    }, { emitEvent: false });

    // Actualizar signal para que los computed properties reaccionen
    this.tipoOperacionForm.set(this.normalizarTipoOperacion(t.tipoOperacion));

    const op = this.normalizarTipoOperacion(t.tipoOperacion);
    if (!this.isReadOnly()) {
      this.aplicarModoTipoOperacion(op);
    }

    this.actualizarValidadoresDestino(op);
  }

  private resetForm(): void {
    const hoyHuevos = new Date().toISOString().split('T')[0];
    const tipoOperacionDefault = this.tiposOperacionHuevos[0];
    this.formHuevos.reset({
      loteId: this.lotePosturaProduccionId ? `LPP-${this.lotePosturaProduccionId}` : (this.loteId?.toString() || ''),
      fechaTraslado: hoyHuevos,
      tipoOperacion: tipoOperacionDefault,
      tipoDestino: this.tipoDestinoTrasladoFijo,
      granjaDestinoId: null,
      plantaDestino: null,
      loteDestinoId: null,
      motivo: null,
      descripcion: null,
      observaciones: null
    });
    this.tiposHuevo.forEach(tipo => {
      this.formHuevos.get(`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`)?.setValue(0);
    });
    this.huevoItemsGuardados.clear();
    while (this.huevoItemsArray.length) this.huevoItemsArray.removeAt(0);
    this.refrescarGruposHuevoItems();
    this.asegurarFilaHuevoInicial();
    this.tipoOperacionForm.set(tipoOperacionDefault);
    this.aplicarModoTipoOperacion(tipoOperacionDefault);
    this.actualizarValidadoresDestino(tipoOperacionDefault);
    this.error.set(null);
    this.success.set(null);
    this.disponibilidad.set(null);
    this.loteInfo.set(null);
  }

  private actualizarValidadoresDestino(tipo: string): void {
    const granjaDestino = this.formHuevos.get('granjaDestinoId');
    const plantaDestino = this.formHuevos.get('plantaDestino');
    const loteDestino = this.formHuevos.get('loteDestinoId');
    const tipoDestino = this.formHuevos.get('tipoDestino');
    const motivo = this.formHuevos.get('motivo');
    const descripcion = this.formHuevos.get('descripcion');
    const observaciones = this.formHuevos.get('observaciones');

    const esVenta = tipo && tipo.toLowerCase().includes('venta');
    const rawTd = this.formHuevos.getRawValue().tipoDestino as string | null | undefined;
    const tipoDestinoValue = esVenta ? rawTd : this.tipoDestinoTrasladoFijo;
    const esPlanta = !!tipoDestinoValue && tipoDestinoValue.toLowerCase().includes('planta');

    motivo?.clearValidators();
    observaciones?.clearValidators();

    if (esVenta) {
      tipoDestino?.setValidators([Validators.required]);
      descripcion?.setValidators([Validators.required]);
      if (esPlanta) {
        plantaDestino?.setValidators([Validators.required]);
      } else {
        plantaDestino?.clearValidators();
      }
      granjaDestino?.clearValidators();
      loteDestino?.clearValidators();
    } else {
      tipoDestino?.clearValidators();
      descripcion?.clearValidators();
      granjaDestino?.clearValidators();
      plantaDestino?.clearValidators();
      loteDestino?.clearValidators();
      observaciones?.setValidators([Validators.required, Validators.minLength(2), Validators.maxLength(2000)]);
    }

    granjaDestino?.updateValueAndValidity();
    plantaDestino?.updateValueAndValidity();
    loteDestino?.updateValueAndValidity();
    tipoDestino?.updateValueAndValidity();
    motivo?.updateValueAndValidity();
    descripcion?.updateValueAndValidity();
    observaciones?.updateValueAndValidity({ emitEvent: false });
  }

  esTipoPlanta(tipoDestino: string | null | undefined): boolean {
    if (!tipoDestino) return false;
    return tipoDestino.toLowerCase().includes('planta');
  }

  getTiposDestinoVenta(): string[] {
    return [...this.tiposDestinoVenta];
  }

  /**
   * Traslado: la planta/bodega de destino se elige de la lista maestra
   * `traslado_de_huevos_planta_destino` de la empresa.
   *
   * Requerimiento del cliente (`Requerimientos de Italapp.docx`, sección TRASLADO DE HUEVOS):
   * *«…no vemos bien que la bodega de salida sea digitada, pues si lo vamos a integrar al ERP,
   * esta debe ser como esté creada, por lo cual debe ser una lista desplegable»*.
   *
   * 🔴 **Se decide por DATO, no por empresa** (CLAUDE.md prohíbe `if (empresa == X)`): hay
   * desplegable si la empresa tiene opciones cargadas en su lista maestra. Medido el 24-ago-2026,
   * Sanmarino y Demo tienen la lista **sin ninguna opción**; volverles el campo obligatorio y
   * vacío les impediría registrar un traslado. Por eso, sin opciones se conserva el input de texto
   * de siempre (ver {@link mostrarOtraGranjaTraslado}) y el cambio es aditivo: habilitarlo para
   * cualquier empresa es cargar sus destinos en `/config/master-lists`, sin tocar código.
   *
   * Escribe en el MISMO control (`observaciones`) que el input que reemplaza, así que ni el DTO,
   * ni la columna, ni ningún lector cambian.
   */
  mostrarPlantaCatalogoTraslado(): boolean {
    return this.esTipoTraslado(this.formHuevos?.get('tipoOperacion')?.value)
      && this.plantasDestino().length > 0;
  }

  /**
   * Traslado sin catálogo de plantas cargado: campo único "Otra granja" digitado en
   * `observaciones` — el comportamiento de siempre, que se conserva como respaldo (ver
   * {@link mostrarPlantaCatalogoTraslado}).
   */
  mostrarOtraGranjaTraslado(): boolean {
    return this.esTipoTraslado(this.formHuevos?.get('tipoOperacion')?.value)
      && this.plantasDestino().length === 0;
  }

  /** Venta con destino Planta: catálogo de planta en `loteDestinoId` / `plantaDestino`. */
  mostrarPlantaCatalogoVenta(): boolean {
    return this.esTipoVenta(this.formHuevos?.get('tipoOperacion')?.value) && this.esTipoPlanta(this.formHuevos?.get('tipoDestino')?.value);
  }

  /** Observaciones opcionales solo en venta (no en traslado: va en "Otra granja"). */
  mostrarObservacionesTextarea(): boolean {
    return this.esTipoVenta(this.formHuevos?.get('tipoOperacion')?.value);
  }

  // Métodos helper para determinar el tipo de operación
  esTipoVenta(tipo: string | null | undefined): boolean {
    if (!tipo) return false;
    return tipo.toLowerCase().includes('venta');
  }

  esTipoTraslado(tipo: string | null | undefined): boolean {
    if (!tipo) return false;
    return tipo.toLowerCase().includes('traslado');
  }

  private normalizarTipoOperacion(value: string | null | undefined): 'Traslado' | 'Venta' {
    const v = (value ?? '').toLowerCase();
    if (v.includes('venta')) return 'Venta';
    return 'Traslado';
  }

  private normalizarTipoDestinoVenta(value: string | null | undefined): string | null {
    if (!value?.trim()) return null;
    const v = value.trim();
    const list = this.tiposDestinoVenta;
    const found = list.find((o) => o.toLowerCase() === v.toLowerCase());
    if (found) return found;
    const low = v.toLowerCase();
    if (low.includes('cliente')) return 'Cliente';
    if (low.includes('empresa')) return 'Empresa';
    if (low.includes('planta')) return 'Planta';
    return null;
  }

  private validarTrasladoHuevos(control: AbstractControl): ValidationErrors | null {
    if (this.clasificacionHuevoPorItems) {
      const items = (control.get('huevoItems') as FormArray | null)?.controls ?? [];
      const total = sumarCantidadesHuevo(items.map(c => c.get('cantidad')?.value));
      return total === 0 ? { sinCantidad: true } : null;
    }

    let totalHuevos = 0;
    this.tiposHuevo.forEach(tipo => {
      const cantidad = control.get(`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`)?.value || 0;
      totalHuevos += cantidad;
    });

    if (totalHuevos === 0) {
      return { sinCantidad: true };
    }

    return null;
  }

  private cargarDisponibilidad(loteId: string): void {
    this.loadingDisponibilidad.set(true);
    this.error.set(null);

    this.trasladosService.getDisponibilidadLote(loteId).subscribe({
      next: (disponibilidad) => {
        this.disponibilidad.set(disponibilidad);
        this.loadingDisponibilidad.set(false);

        // Se gatea por la PRESENCIA del bloque de huevos, no por la fase del lote: `lote.fase` no
        // dice en qué fase está (el paso a producción no la escribe), y exigir 'Produccion'
        // escondía la producción de lotes que la tenían — medido, 3,6 M de huevos en K345A/K345B.
        if (!disponibilidad.huevos) {
          this.error.set('Este lote no tiene producción de huevos registrada');
        }

        // Si estamos editando, no actualizamos el loteId del formulario
        if (!this.editingTraslado && this.loteId) {
          this.formHuevos.patchValue({ loteId: loteId });
        }
      },
      error: (error) => {
        console.error('Error cargando disponibilidad:', error);
        this.error.set('Error al cargar disponibilidad del lote');
        this.disponibilidad.set(null);
        this.loadingDisponibilidad.set(false);
      }
    });
  }

  onSubmitHuevos(): void {
    if (this.formHuevos.invalid) {
      this.formHuevos.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);

    const formValue = this.formHuevos.getRawValue();
    const fechaTraslado = typeof formValue.fechaTraslado === 'string'
      ? new Date(formValue.fechaTraslado)
      : (formValue.fechaTraslado instanceof Date ? formValue.fechaTraslado : new Date());

    const lppId = this.lotePosturaProduccionId ?? (formValue.lotePosturaProduccionId ? Number(formValue.lotePosturaProduccionId) : undefined);
    const esTraslado = this.esTipoTraslado(formValue.tipoOperacion);
    const loteIdLegacy = String(formValue.loteId || this.loteId || '');

    let loteDestinoId = formValue.loteDestinoId ? String(formValue.loteDestinoId) : undefined;
    if (!esTraslado && formValue.plantaDestino && !loteDestinoId) {
      loteDestinoId = String(formValue.plantaDestino);
    }

    // Clasificación por ítems (Santa Reyes): reemplaza las 11 cantidades fijas (van en 0) por el
    // desglose del catálogo. Solo alta nueva — editar un traslado existente sigue el flujo legacy
    // (ver F10 §9 del plan: gap conocido, riesgo bajo porque el alta procesa en el mismo request).
    const usaHuevoItems = this.clasificacionHuevoPorItems && !(this.editingTraslado && this.isEditMode());
    const huevoItems = usaHuevoItems ? this.construirHuevoItemsPayload() : undefined;

    const dto: CrearTrasladoHuevosDto = {
      lotePosturaProduccionId: lppId,
      loteId: lppId ? '' : loteIdLegacy,
      fechaTraslado,
      tipoOperacion: formValue.tipoOperacion,
      cantidadLimpio: usaHuevoItems ? 0 : (formValue.cantidadLimpio || 0),
      cantidadTratado: usaHuevoItems ? 0 : (formValue.cantidadTratado || 0),
      cantidadSucio: usaHuevoItems ? 0 : (formValue.cantidadSucio || 0),
      cantidadDeforme: usaHuevoItems ? 0 : (formValue.cantidadDeforme || 0),
      cantidadBlanco: usaHuevoItems ? 0 : (formValue.cantidadBlanco || 0),
      cantidadDobleYema: usaHuevoItems ? 0 : (formValue.cantidadDobleYema || 0),
      cantidadPiso: usaHuevoItems ? 0 : (formValue.cantidadPiso || 0),
      cantidadPequeno: usaHuevoItems ? 0 : (formValue.cantidadPequeno || 0),
      cantidadRoto: usaHuevoItems ? 0 : (formValue.cantidadRoto || 0),
      cantidadDesecho: usaHuevoItems ? 0 : (formValue.cantidadDesecho || 0),
      cantidadOtro: usaHuevoItems ? 0 : (formValue.cantidadOtro || 0),
      huevoItems,
      granjaDestinoId: esTraslado ? undefined : (formValue.granjaDestinoId ? Number(formValue.granjaDestinoId) : undefined),
      loteDestinoId,
      tipoDestino: esTraslado ? this.tipoDestinoTrasladoFijo : formValue.tipoDestino,
      motivo: esTraslado ? undefined : (formValue.motivo || undefined),
      descripcion: esTraslado ? undefined : (formValue.descripcion || undefined),
      observaciones: (formValue.observaciones && String(formValue.observaciones).trim()) || undefined
    };

    if (this.editingTraslado && this.isEditMode()) {
      const updateDto: ActualizarTrasladoHuevosDto = {
        fechaTraslado: dto.fechaTraslado,
        tipoOperacion: dto.tipoOperacion,
        cantidadLimpio: dto.cantidadLimpio,
        cantidadTratado: dto.cantidadTratado,
        cantidadSucio: dto.cantidadSucio,
        cantidadDeforme: dto.cantidadDeforme,
        cantidadBlanco: dto.cantidadBlanco,
        cantidadDobleYema: dto.cantidadDobleYema,
        cantidadPiso: dto.cantidadPiso,
        cantidadPequeno: dto.cantidadPequeno,
        cantidadRoto: dto.cantidadRoto,
        cantidadDesecho: dto.cantidadDesecho,
        cantidadOtro: dto.cantidadOtro,
        granjaDestinoId: dto.granjaDestinoId,
        loteDestinoId: dto.loteDestinoId,
        tipoDestino: dto.tipoDestino,
        motivo: dto.motivo,
        descripcion: dto.descripcion,
        observaciones: dto.observaciones
      };

      this.trasladosService.actualizarTrasladoHuevos(this.editingTraslado.id, updateDto).subscribe({
        next: (result) => {
          this.loading.set(false);
          const tipoOperacion = formValue.tipoOperacion?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
          this.successMessage.set(`${tipoOperacion} actualizado exitosamente. Número: ${result.numeroTraslado}`);
          this.showSuccessModal.set(true);
          this.error.set(null);
          this.success.set(null);
          setTimeout(() => {
            this.showSuccessModal.set(false);
            this.save.emit(result);
          }, 2000);
        },
        error: (error) => {
          console.error('Error actualizando traslado de huevos:', error);
          this.loading.set(false);
          const tipoOperacion = formValue.tipoOperacion?.toLowerCase().includes('venta') ? 'venta' : 'traslado';
          const detalle = error?.error?.message || error?.error?.error || error?.message || 'Error desconocido';
          this.errorMessage.set(`No se pudo actualizar el ${tipoOperacion}. ${detalle}`);
          this.showErrorModal.set(true);
          this.error.set(null);
          this.success.set(null);
          // NO cerrar el modal en caso de error
        }
      });
    } else {
      this.trasladosService.crearTrasladoHuevos(dto).subscribe({
        next: (result) => {
          this.loading.set(false);
          const tipoOperacion = formValue.tipoOperacion?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
          this.successMessage.set(`${tipoOperacion} creado exitosamente. Número: ${result.numeroTraslado}`);
          this.showSuccessModal.set(true);
          this.error.set(null);
          this.success.set(null);
          setTimeout(() => {
            this.showSuccessModal.set(false);
            this.save.emit(result);
          }, 2000);
        },
        error: (error) => {
          console.error('Error creando traslado de huevos:', error);
          this.loading.set(false);
          const tipoOperacion = formValue.tipoOperacion?.toLowerCase().includes('venta') ? 'venta' : 'traslado';
          const detalle = error?.error?.message || error?.error?.error || error?.message || 'Error desconocido';
          this.errorMessage.set(`No se pudo guardar el ${tipoOperacion}. ${detalle}`);
          this.showErrorModal.set(true);
          this.error.set(null);
          this.success.set(null);
          // NO cerrar el modal en caso de error
        }
      });
    }
  }

  closeModal(): void {
    this.resetForm();
    this.showSuccessModal.set(false);
    this.showErrorModal.set(false);
    this.close.emit();
  }

  closeSuccessModal(): void {
    this.showSuccessModal.set(false);
    this.save.emit();
  }

  closeErrorModal(): void {
    this.showErrorModal.set(false);
  }

  onOverlayClick(event: MouseEvent): void {
    // Cerrar solo si se hace clic en el overlay, no en el contenido del modal
    if (event.target === event.currentTarget) {
      this.closeModal();
    }
  }

  getMaxCantidad(tipoKey: string): number {
    const disponibilidad = this.disponibilidad();
    if (!disponibilidad?.huevos) return 0;

    const keyMap: Record<string, keyof HuevosDisponiblesDto> = {
      'limpio': 'limpio',
      'tratado': 'tratado',
      'sucio': 'sucio',
      'deforme': 'deforme',
      'blanco': 'blanco',
      'dobleYema': 'dobleYema',
      'piso': 'piso',
      'pequeno': 'pequeno',
      'roto': 'roto',
      'desecho': 'desecho',
      'otro': 'otro'
    };

    const valor = disponibilidad.huevos[keyMap[tipoKey]];
    return typeof valor === 'number' ? valor : 0;
  }
}

import { Component, Input, Output, EventEmitter, OnInit, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SeguimientoLoteLevanteDto, CreateSeguimientoLoteLevanteDto, UpdateSeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { CatalogoAlimentosService, CatalogItemDto, PagedResult, CatalogItemType } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { InventarioService, FarmInventoryDto } from '../../../inventario/services/inventario.service';
import { EMPTY, forkJoin, of } from 'rxjs';
import { expand, map, reduce, finalize, debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';

@Component({
  selector: 'app-modal-create-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './modal-create-edit.component.html',
  styleUrls: ['./modal-create-edit.component.scss']
})
export class ModalCreateEditComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() editing: SeguimientoLoteLevanteDto | null = null;
  @Input() lotes: LoteDto[] = [];
  @Input() selectedLoteId: number | null = null;
  @Input() loading: boolean = false;
  
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

  // Tipos de ítem
  tiposItem: CatalogItemType[] = ['alimento', 'medicamento', 'accesorio', 'biologico', 'consumible', 'otro'];

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

  constructor(
    private fb: FormBuilder,
    private catalogSvc: CatalogoAlimentosService,
    private inventarioSvc: InventarioService
  ) { }

  ngOnInit(): void {
    this.initializeForm();
    // No cargar catálogo automáticamente, se cargará cuando se seleccione un tipo de ítem
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
      fechaRegistro: [this.todayYMD(), Validators.required],
      loteId: ['', Validators.required],
      mortalidadHembras: [0, [Validators.required, Validators.min(0)]],
      mortalidadMachos: [0, [Validators.required, Validators.min(0)]],
      selH: [0, [Validators.required, Validators.min(0)]],
      selM: [0, [Validators.required, Validators.min(0)]],
      errorSexajeHembras: [0, [Validators.required, Validators.min(0)]],
      errorSexajeMachos: [0, [Validators.required, Validators.min(0)]],
      tipoAlimento: [''],
      // Nuevos campos para tipo de ítem
      tipoItemHembras: [null],
      tipoItemMachos: [null],
      // Alimentos (ahora filtrados por tipo)
      tipoAlimentoHembras: [null],
      tipoAlimentoMachos: [null],
      // Consumo con unidad de medida - el backend hace la conversión
      consumoHembras: [0, [Validators.required, Validators.min(0)]],
      unidadConsumoHembras: ['kg', Validators.required], // 'kg' o 'g'
      consumoMachos: [null, [Validators.min(0)]],
      unidadConsumoMachos: ['kg'], // 'kg' o 'g'
      observaciones: [''],
      pesoPromH: [null, [Validators.min(0)]],
      pesoPromM: [null, [Validators.min(0)]],
      uniformidadH: [null, [Validators.min(0), Validators.max(100)]],
      uniformidadM: [null, [Validators.min(0), Validators.max(100)]],
      cvH: [null, [Validators.min(0)]],
      cvM: [null, [Validators.min(0)]],
      ciclo: ['Normal'],
    });

    // Suscribirse a cambios en tipoItem para filtrar productos
    this.form.get('tipoItemHembras')?.valueChanges.subscribe(tipo => {
      this.filtrarAlimentosPorTipo('hembras', tipo);
      this.form.patchValue({ tipoAlimentoHembras: null }, { emitEvent: false });
      this.inventarioDisponibleHembras = null;
      this.mensajeInventarioHembras = '';
    });

    this.form.get('tipoItemMachos')?.valueChanges.subscribe(tipo => {
      this.filtrarAlimentosPorTipo('machos', tipo);
      this.form.patchValue({ tipoAlimentoMachos: null }, { emitEvent: false });
      this.inventarioDisponibleMachos = null;
      this.mensajeInventarioMachos = '';
    });

    // Suscribirse a cambios en alimento seleccionado para consultar inventario
    this.form.get('tipoAlimentoHembras')?.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(alimentoId => {
      if (alimentoId) {
        this.consultarInventario('hembras', alimentoId);
      } else {
        this.inventarioDisponibleHembras = null;
        this.mensajeInventarioHembras = '';
      }
    });

    this.form.get('tipoAlimentoMachos')?.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(alimentoId => {
      if (alimentoId) {
        this.consultarInventario('machos', alimentoId);
      } else {
        this.inventarioDisponibleMachos = null;
        this.mensajeInventarioMachos = '';
      }
    });

    // Suscribirse a cambios en loteId para obtener granjaId y cargar inventario
    this.form.get('loteId')?.valueChanges.subscribe(loteId => {
      if (loteId) {
        // Obtener el lote para conseguir granjaId
        const lote = this.lotes.find(l => String(l.loteId) === String(loteId));
        if (lote && lote.granjaId) {
          const nuevaGranjaId = lote.granjaId;
          
          // Si cambió la granja, cargar inventario de la nueva granja
          if (this.granjaIdActual !== nuevaGranjaId) {
            this.granjaIdActual = nuevaGranjaId;
            this.cargarInventarioGranja(nuevaGranjaId);
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
      }
    });

    // Ya no necesitamos actualizar consumoKg aquí, el backend hace la conversión
    // Solo mantenemos los valores para validación de inventario en el frontend
  }

  private resetForm(): void {
    this.form.reset({
      fechaRegistro: this.todayYMD(),
      loteId: this.selectedLoteId,
      mortalidadHembras: 0,
      mortalidadMachos: 0,
      selH: 0,
      selM: 0,
      errorSexajeHembras: 0,
      errorSexajeMachos: 0,
      tipoAlimento: '',
      tipoItemHembras: null,
      tipoItemMachos: null,
      tipoAlimentoHembras: null,
      tipoAlimentoMachos: null,
      consumoHembras: 0,
      unidadConsumoHembras: 'kg',
      consumoMachos: null,
      unidadConsumoMachos: 'kg',
      observaciones: '',
      ciclo: 'Normal',
      pesoPromH: null,
      pesoPromM: null,
      uniformidadH: null,
      uniformidadM: null,
      cvH: null,
      cvM: null,
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
    
    // Si hay lote seleccionado, cargar inventario de su granja
    if (this.selectedLoteId) {
      const lote = this.lotes.find(l => String(l.loteId) === String(this.selectedLoteId));
      if (lote && lote.granjaId) {
        this.granjaIdActual = lote.granjaId;
        this.cargarInventarioGranja(lote.granjaId);
      }
    } else {
      this.granjaIdActual = null;
      this.alimentosCatalog = [];
    }
  }

  // Ya no necesitamos actualizar consumoKg aquí, el backend lo hace
  // Mantenemos el método por compatibilidad pero no hace nada
  private actualizarConsumoKg(tipo: 'hembras' | 'machos'): void {
    // El backend ahora hace la conversión, solo mantenemos los valores para validación de inventario
    // No necesitamos actualizar consumoKgHembras/Machos ya que el backend los calcula
  }

  private populateForm(): void {
    if (!this.editing) return;
    
    // Leer consumo original y tipo de alimento desde Metadata JSONB
    const metadata: any = this.editing.metadata || {};
    const consumoHembras = metadata?.consumoOriginalHembras ?? this.editing.consumoKgHembras ?? 0;
    const unidadConsumoHembras = metadata?.unidadConsumoOriginalHembras ?? 'kg';
    const consumoMachos = metadata?.consumoOriginalMachos ?? this.editing.consumoKgMachos ?? null;
    const unidadConsumoMachos = metadata?.unidadConsumoOriginalMachos ?? 'kg';
    
    // Leer tipo de ítem y IDs de alimentos desde Metadata
    const tipoItemHembras = metadata?.tipoItemHembras || null;
    const tipoItemMachos = metadata?.tipoItemMachos || null;
    const tipoAlimentoHembras = metadata?.tipoAlimentoHembras ?? this.editing.tipoAlimentoHembras ?? null;
    const tipoAlimentoMachos = metadata?.tipoAlimentoMachos ?? this.editing.tipoAlimentoMachos ?? null;
    
    this.form.patchValue({
      fechaRegistro: this.toYMD(this.editing.fechaRegistro),
      loteId: this.editing.loteId,
      mortalidadHembras: this.editing.mortalidadHembras,
      mortalidadMachos: this.editing.mortalidadMachos,
      selH: this.editing.selH,
      selM: this.editing.selM,
      errorSexajeHembras: this.editing.errorSexajeHembras,
      errorSexajeMachos: this.editing.errorSexajeMachos,
      tipoAlimento: this.editing.tipoAlimento ?? '',
      tipoItemHembras: tipoItemHembras, // Leer desde Metadata
      tipoItemMachos: tipoItemMachos, // Leer desde Metadata
      tipoAlimentoHembras: tipoAlimentoHembras, // Leer desde Metadata
      tipoAlimentoMachos: tipoAlimentoMachos, // Leer desde Metadata
      consumoHembras: consumoHembras,
      unidadConsumoHembras: unidadConsumoHembras, // Usar la unidad original guardada
      consumoMachos: consumoMachos,
      unidadConsumoMachos: unidadConsumoMachos, // Usar la unidad original guardada
      observaciones: this.editing.observaciones || '',
      ciclo: this.editing.ciclo || 'Normal',
      pesoPromH: this.editing.pesoPromH ?? null,
      pesoPromM: this.editing.pesoPromM ?? null,
      uniformidadH: this.editing.uniformidadH ?? null,
      uniformidadM: this.editing.uniformidadM ?? null,
      cvH: this.editing.cvH ?? null,
      cvM: this.editing.cvM ?? null,
    });

    // Si hay alimento seleccionado, necesitamos cargar el catálogo primero para obtener el tipo de ítem
    // Esto se hará después de que se cargue el inventario de la granja
    const loteId = this.editing.loteId;
    const lote = this.lotes.find(l => String(l.loteId) === String(loteId));
    
    if (lote && lote.granjaId) {
      this.granjaIdActual = lote.granjaId;
      
      // Cargar inventario de la granja primero
      this.cargarInventarioGranja(lote.granjaId);
      
      // Después de cargar el inventario, establecer el tipo de ítem y alimento
      // Esto se hará en el callback de cargarInventarioGranja
      setTimeout(() => {
        this.establecerTipoItemYAlimentoAlEditar();
      }, 500); // Dar tiempo para que se cargue el inventario
    } else {
      // Si no hay granja, intentar obtener el tipo desde el catálogo completo
      this.establecerTipoItemYAlimentoAlEditar();
    }
  }

  private establecerTipoItemYAlimentoAlEditar(): void {
    if (!this.editing) return;
    
    const metadata: any = this.editing.metadata || {};
    
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
  private cargarInventarioGranja(granjaId: number): void {
    // Cargar inventario de la granja (solo productos que tienen stock)
    this.inventarioSvc.getInventory(granjaId).subscribe({
      next: (inventario) => {
        console.log('Inventario recibido:', inventario);
        
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
            const catalogItemsMap = new Map<number, CatalogItemDto>();
            catalogItems.forEach(item => {
              if (item && item.id) {
                catalogItemsMap.set(item.id, item);
              }
            });
            
            // Convertir inventario a formato CatalogItemDto
            this.alimentosCatalog = inventario
              .filter(item => item.active && item.quantity > 0)
              .map(item => {
                const catalogItem = catalogItemsMap.get(item.catalogItemId);
                return {
                  id: item.catalogItemId,
                  codigo: item.codigo,
                  nombre: item.nombre,
                  metadata: item.catalogItemMetadata || catalogItem?.metadata || item.metadata || {},
                  activo: item.active
                } as CatalogItemDto;
              })
              .sort((a, b) =>
                (a.nombre || '').localeCompare(b.nombre || '', 'es', { numeric: true, sensitivity: 'base' })
              );
            
            this.actualizarMapasYFiltros();
          });
        } else {
          // Todos los items tienen catalogItemMetadata, procesar directamente
          this.alimentosCatalog = inventario
            .filter(item => item.active && item.quantity > 0)
            .map(item => ({
              id: item.catalogItemId,
              codigo: item.codigo,
              nombre: item.nombre,
              metadata: item.catalogItemMetadata || item.metadata || {},
              activo: item.active
            } as CatalogItemDto))
            .sort((a, b) =>
              (a.nombre || '').localeCompare(b.nombre || '', 'es', { numeric: true, sensitivity: 'base' })
            );
          
          this.actualizarMapasYFiltros();
        }
      },
      error: (err) => {
        console.error('Error al cargar inventario de la granja:', err);
        this.alimentosCatalog = [];
        this.alimentosFiltradosHembras = [];
        this.alimentosFiltradosMachos = [];
      }
    });
  }

  private actualizarMapasYFiltros(): void {
    console.log('Alimentos catalog cargados:', this.alimentosCatalog);
    
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
      const tipoItemH = this.form.get('tipoItemHembras')?.value;
      const tipoItemM = this.form.get('tipoItemMachos')?.value;
      if (tipoItemH) this.filtrarAlimentosPorTipo('hembras', tipoItemH);
      if (tipoItemM) this.filtrarAlimentosPorTipo('machos', tipoItemM);
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
  filtrarAlimentosPorTipo(tipoGenero: 'hembras' | 'machos', tipoItem: CatalogItemType | null): void {
    // Si no hay catálogo cargado y hay granja seleccionada, cargar inventario
    if (this.alimentosCatalog.length === 0 && this.granjaIdActual) {
      this.cargarInventarioGranja(this.granjaIdActual);
      // Salir temprano, el filtrado se hará cuando termine de cargar
      return;
    }
    
    // Si no hay granja seleccionada, no podemos cargar inventario
    if (!this.granjaIdActual) {
      if (tipoGenero === 'hembras') {
        this.alimentosFiltradosHembras = [];
      } else {
        this.alimentosFiltradosMachos = [];
      }
      return;
    }

    console.log('Filtrando alimentos por tipo:', { tipoItem, totalCatalog: this.alimentosCatalog.length });
    console.log('Alimentos catalog:', this.alimentosCatalog.map(a => ({ 
      id: a.id, 
      nombre: a.nombre, 
      metadata: a.metadata,
      type_item: a.metadata?.type_item 
    })));

    if (!tipoItem) {
      // Si no hay tipo seleccionado, mostrar todos los items del inventario
      if (tipoGenero === 'hembras') {
        this.alimentosFiltradosHembras = this.alimentosCatalog;
      } else {
        this.alimentosFiltradosMachos = this.alimentosCatalog;
      }
    } else {
      // Filtrar por tipo de ítem
      const alimentos = this.alimentosCatalog.filter(a => {
        const metadata = a.metadata;
        const itemType = metadata?.type_item;
        console.log(`Item ${a.nombre}: type_item = ${itemType}, buscando: ${tipoItem}`);
        return metadata && itemType === tipoItem;
      });
      
      console.log(`Alimentos filtrados para ${tipoItem}:`, alimentos.length);
      
      if (tipoGenero === 'hembras') {
        this.alimentosFiltradosHembras = alimentos;
      } else {
        this.alimentosFiltradosMachos = alimentos;
      }
    }
  }

  // ================== CONSULTA DE INVENTARIO ==================
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

    // Usar el nuevo endpoint que filtra directamente por farmId y catalogItemId
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
            
            console.log(`Inventario hembras cargado: ${cantidadOriginal} ${unidad}`);
            
            // Convertir a gramos para mostrar en la interfaz (si el inventario está en kg)
            // Si el inventario está en gramos, no hay conversión
            let cantidadGramos: number;
            const unidadLower = unidad.toLowerCase();
            if (unidadLower === 'kg' || unidadLower === 'kilogramos' || unidadLower === 'kilogramo') {
              cantidadGramos = Math.round(cantidadOriginal * 1000);
            } else if (unidadLower === 'g' || unidadLower === 'gramos' || unidadLower === 'gramo') {
              cantidadGramos = Math.round(cantidadOriginal);
            } else {
              // Para otras unidades, asumir kg
              console.warn(`Unidad desconocida del inventario: ${unidad}, asumiendo kg`);
              cantidadGramos = Math.round(cantidadOriginal * 1000);
            }
            
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
            let cantidadGramos: number;
            const unidadLower = unidad.toLowerCase();
            if (unidadLower === 'kg' || unidadLower === 'kilogramos' || unidadLower === 'kilogramo') {
              cantidadGramos = Math.round(cantidadOriginal * 1000);
            } else if (unidadLower === 'g' || unidadLower === 'gramos' || unidadLower === 'gramo') {
              cantidadGramos = Math.round(cantidadOriginal);
            } else {
              // Para otras unidades, asumir kg
              console.warn(`Unidad desconocida del inventario: ${unidad}, asumiendo kg`);
              cantidadGramos = Math.round(cantidadOriginal * 1000);
            }
            
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

    const raw = this.form.value;
    const loteId = raw.loteId;
    const lote = this.lotes.find(l => String(l.loteId) === String(loteId));
    
    if (!lote || !lote.granjaId) {
      alert('No se pudo obtener la granja del lote seleccionado');
      return;
    }

    // Obtener consumo y unidad
    const consumoH = Number(raw.consumoHembras) || 0;
    const unidadH = raw.unidadConsumoHembras || 'kg';
    const consumoM = Number(raw.consumoMachos) || 0;
    const unidadM = raw.unidadConsumoMachos || 'kg';
    const alimentoHId = raw.tipoAlimentoHembras;
    const alimentoMId = raw.tipoAlimentoMachos;

    // Convertir a gramos para validar inventario (el inventario se muestra en gramos)
    const consumoGramosH = (unidadH === 'g' || unidadH === 'gramos') ? consumoH : consumoH * 1000;
    const consumoGramosM = (unidadM === 'g' || unidadM === 'gramos') ? consumoM : consumoM * 1000;
    
    // Calcular consumo en kg para la reducción de inventario
    const consumoKgH = (unidadH === 'g' || unidadH === 'gramos') ? consumoH / 1000 : consumoH;
    const consumoKgM = (unidadM === 'g' || unidadM === 'gramos') ? (consumoM > 0 ? consumoM / 1000 : 0) : (consumoM > 0 ? consumoM : 0);

    // Validar inventario disponible
    if (alimentoHId && consumoH > 0) {
      if (this.inventarioDisponibleHembras === null || this.inventarioDisponibleHembras < consumoGramosH) {
        alert('No hay suficiente alimento disponible para hembras. Verifique el inventario.');
        return;
      }
    }

    if (alimentoMId && consumoM > 0) {
      if (this.inventarioDisponibleMachos === null || this.inventarioDisponibleMachos < consumoGramosM) {
        alert('No hay suficiente alimento disponible para machos. Verifique el inventario.');
        return;
      }
    }

    // Obtener nombres de alimentos desde los IDs
    let nombreAlimentoH = '';
    let nombreAlimentoM = '';
    
    if (alimentoHId) {
      const alimentoH = this.alimentosById.get(Number(alimentoHId));
      nombreAlimentoH = alimentoH?.nombre || '';
    }
    
    if (alimentoMId) {
      const alimentoM = this.alimentosById.get(Number(alimentoMId));
      nombreAlimentoM = alimentoM?.nombre || '';
    }
    
    // Construir string de tipoAlimento (concatenar si hay ambos)
    let tipoAlimentoStr = raw.tipoAlimento || '';
    if (nombreAlimentoH && nombreAlimentoM) {
      tipoAlimentoStr = `${nombreAlimentoH} / ${nombreAlimentoM}`;
    } else if (nombreAlimentoH) {
      tipoAlimentoStr = nombreAlimentoH;
    } else if (nombreAlimentoM) {
      tipoAlimentoStr = nombreAlimentoM;
    }
    
    const ymd = this.toYMD(raw.fechaRegistro)!;

    // El backend ahora acepta consumo con unidad y hace la conversión automáticamente
    const baseDto = {
      fechaRegistro: this.ymdToIsoAtNoon(ymd),
      loteId: raw.loteId,
      mortalidadHembras: Number(raw.mortalidadHembras) || 0,
      mortalidadMachos: Number(raw.mortalidadMachos) || 0,
      selH: Number(raw.selH) || 0,
      selM: Number(raw.selM) || 0,
      errorSexajeHembras: Number(raw.errorSexajeHembras) || 0,
      errorSexajeMachos: Number(raw.errorSexajeMachos) || 0,
      tipoAlimento: tipoAlimentoStr || '',
      // Enviar consumo con unidad - el backend hace la conversión
      consumoHembras: consumoH,
      unidadConsumoHembras: unidadH,
      consumoMachos: consumoM > 0 ? consumoM : null,
      unidadConsumoMachos: unidadM,
      pesoPromH: this.toNumOrNull(raw.pesoPromH),
      pesoPromM: this.toNumOrNull(raw.pesoPromM),
      uniformidadH: this.toNumOrNull(raw.uniformidadH),
      uniformidadM: this.toNumOrNull(raw.uniformidadM),
      cvH: this.toNumOrNull(raw.cvH),
      cvM: this.toNumOrNull(raw.cvM),
      observaciones: raw.observaciones,
      kcalAlH: null,
      protAlH: null,
      kcalAveH: null,
      protAveH: null,
      ciclo: raw.ciclo,
      // Tipo de ítem y IDs de alimentos (se guardan en Metadata)
      tipoItemHembras: raw.tipoItemHembras || null,
      tipoItemMachos: raw.tipoItemMachos || null,
      tipoAlimentoHembras: alimentoHId ? Number(alimentoHId) : null,
      tipoAlimentoMachos: alimentoMId ? Number(alimentoMId) : null,
    };

    const isEdit = !!this.editing;
    const data = isEdit 
      ? { ...baseDto, id: this.editing!.id } as UpdateSeguimientoLoteLevanteDto
      : baseDto as CreateSeguimientoLoteLevanteDto;

    // Hacer resta al inventario antes de guardar
    const restas: Promise<void>[] = [];

    if (alimentoHId && consumoKgH > 0) {
      // IMPORTANTE: El backend NO hace conversión de unidades, resta directamente
      // El consumoKgH ya está en kg (convertido si era gramos)
      const unidadInventario = this.inventarioUnidadHembras || 'kg';
      
      console.log(`Reduciendo inventario hembras: ${consumoKgH} kg (inventario en ${unidadInventario})`);
      
      restas.push(
        this.inventarioSvc.postExit(lote.granjaId, {
          catalogItemId: Number(alimentoHId),
          quantity: consumoKgH, // Ya está en kg
          unit: unidadInventario, // Usar la unidad EXACTA del inventario
          reference: `Consumo diario levante - Lote ${lote.loteNombre || loteId}`,
          reason: 'Consumo diario',
          destination: 'Consumo'
        }).toPromise().then(() => {
          console.log(`Inventario hembras reducido correctamente: ${consumoKgH} ${unidadInventario}`);
        }).catch(err => {
          console.error('Error al restar inventario hembras:', err);
          throw new Error('Error al registrar consumo en inventario (hembras)');
        })
      );
    }

    if (alimentoMId && consumoKgM > 0) {
      // IMPORTANTE: El backend NO hace conversión de unidades, resta directamente
      // El consumoKgM ya está en kg (convertido si era gramos)
      const unidadInventario = this.inventarioUnidadMachos || 'kg';
      
      console.log(`Reduciendo inventario machos: ${consumoKgM} kg (inventario en ${unidadInventario})`);
      
      restas.push(
        this.inventarioSvc.postExit(lote.granjaId, {
          catalogItemId: Number(alimentoMId),
          quantity: consumoKgM, // Ya está en kg
          unit: unidadInventario, // Usar la unidad EXACTA del inventario
          reference: `Consumo diario levante - Lote ${lote.loteNombre || loteId}`,
          reason: 'Consumo diario',
          destination: 'Consumo'
        }).toPromise().then(() => {
          console.log(`Inventario machos reducido correctamente: ${consumoKgM} ${unidadInventario}`);
        }).catch(err => {
          console.error('Error al restar inventario machos:', err);
          throw new Error('Error al registrar consumo en inventario (machos)');
        })
      );
    }

    // Esperar a que se completen las restas antes de emitir el save
    if (restas.length > 0) {
      Promise.all(restas).then(() => {
        this.save.emit({ data, isEdit });
      }).catch(err => {
        alert(err.message || 'Error al registrar consumo en inventario');
      });
    } else {
      this.save.emit({ data, isEdit });
    }
  }

  // ================== HELPERS ==================
  private toNumOrNull(v: any): number | null {
    if (v === null || v === undefined || v === '') return null;
    const n = typeof v === 'number' ? v : Number(v);
    return isNaN(n) ? null : n;
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
}

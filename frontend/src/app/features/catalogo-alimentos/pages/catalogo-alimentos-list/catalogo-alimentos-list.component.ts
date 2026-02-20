import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule,
  ReactiveFormsModule,
  FormArray,
  FormBuilder,
  FormGroup,
  Validators
} from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faPlus, faPen, faTrash, faSearch, faChevronLeft, faChevronRight
} from '@fortawesome/free-solid-svg-icons';
import { finalize } from 'rxjs/operators';

import {
  CatalogoAlimentosService,
  CatalogItemDto,
  CatalogItemCreateRequest,
  CatalogItemUpdateRequest,
  PagedResult
} from '../../services/catalogo-alimentos.service';

type CatalogItemType = 'alimento'|'medicamento'|'accesorio'|'biologico'|'consumible'|'otro';
type Genero = 'Hembra'|'Macho'|'Mixto';

@Component({
  selector: 'app-catalogo-alimentos-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, FontAwesomeModule],
  templateUrl: './catalogo-alimentos-list.component.html',
  styleUrls: ['./catalogo-alimentos-list.component.scss']
})
export class CatalogoAlimentosListComponent implements OnInit {
  // Icons
  faPlus = faPlus; faPen = faPen; faTrash = faTrash; faSearch = faSearch;
  faChevronLeft = faChevronLeft; faChevronRight = faChevronRight;

  // UI state
  loading = false;
  modalOpen = false;
  detailModalOpen = false;
  selectedItem: CatalogItemDto | null = null;
  editing: CatalogItemDto | null = null;

  // Listado
  q = '';
  typeFilter: ''|CatalogItemType = '';
  statusFilter: 'all'|'active'|'inactive' = 'all';
  pageSizes = [10, 20, 50];
  page = 1;
  pageSize = 20;
  total = 0;
  items: CatalogItemDto[] = [];

  // opciones para metadata estructurada
  tiposItem: CatalogItemType[] = ['alimento','medicamento','accesorio','biologico','consumible','otro'];
  especies = ['pollo', 'pavo', 'pato', 'ganso', 'codorniz', 'otro'];
  razas = ['Ross', 'Cobb', 'Hubbard', 'Lohmann'];
  generos: Genero[] = ['Hembra', 'Macho', 'Mixto'];

  // Campos dinámicos por tipo de item
  camposPorTipo: Record<CatalogItemType, string[]> = {
    'alimento': ['especie', 'raza', 'genero'],
    'medicamento': ['tipo_medicamento', 'via_administracion', 'presentacion'],
    'biologico': ['tipo_biologico', 'via_aplicacion', 'temperatura_almacenamiento'],
    'accesorio': ['tipo_accesorio', 'material', 'dimensiones'],
    'consumible': ['tipo_consumible', 'unidad_medida'],
    'otro': []
  };

  // Claves reservadas que gestionamos de forma estructurada
  private readonly RESERVED_KEYS = new Set(['type_item','especie','raza','genero','tipo_medicamento','via_administracion','presentacion','tipo_biologico','via_aplicacion','temperatura_almacenamiento','tipo_accesorio','material','dimensiones','tipo_consumible','unidad_medida']);

  // Form
  form!: FormGroup;

  constructor(
    private fb: FormBuilder,
    private svc: CatalogoAlimentosService
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      codigo: ['', [Validators.required, Validators.maxLength(10)]],
      nombre: ['', [Validators.required, Validators.maxLength(150)]],
      activo: [true, Validators.required],
      // Tipo de ítem (columna separada)
      itemType: ['alimento' as CatalogItemType, Validators.required], // Por defecto: alimento
      // Campos para alimento
      especie: ['pollo'],
      raza: ['Ross'],
      genero: ['Mixto'],
      // Campos para medicamento
      tipo_medicamento: [''],
      via_administracion: [''],
      presentacion: [''],
      // Campos para biologico
      tipo_biologico: [''],
      via_aplicacion: [''],
      temperatura_almacenamiento: [''],
      // Campos para accesorio
      tipo_accesorio: [''],
      material: [''],
      dimensiones: [''],
      // Campos para consumible
      tipo_consumible: [''],
      unidad_medida: [''],
      // key/value libres
      metadata: this.fb.array<FormGroup>([])
    });

    // Mostrar/ocultar campos dinámicos cuando cambia el tipo
    this.form.get('itemType')?.valueChanges.subscribe(type => {
      this.resetearCamposDinamicos(type);
    });

    this.load();
  }

  private resetearCamposDinamicos(type: CatalogItemType | null): void {
    // Resetear todos los campos dinámicos
    const campos = ['especie', 'raza', 'genero', 'tipo_medicamento', 'via_administracion', 'presentacion',
                    'tipo_biologico', 'via_aplicacion', 'temperatura_almacenamiento', 'tipo_accesorio',
                    'material', 'dimensiones', 'tipo_consumible', 'unidad_medida'];
    
    campos.forEach(campo => {
      const control = this.form.get(campo);
      if (control) {
        // Resetear a valores por defecto según el tipo
        if (type === 'alimento') {
          if (campo === 'especie') control.setValue('pollo');
          else if (campo === 'raza') control.setValue('Ross');
          else if (campo === 'genero') control.setValue('Mixto');
          else control.setValue('');
        } else {
          control.setValue('');
        }
      }
    });
  }

  // ======= Helpers formulario =======
  get metadataArray(): FormArray<FormGroup> {
    return this.form.get('metadata') as FormArray<FormGroup>;
  }
  get isAlimento(): boolean {
    return this.form?.get('itemType')?.value === 'alimento';
  }
  
  get isVacuna(): boolean {
    return this.form?.get('itemType')?.value === 'vacuna';
  }

  get isMedicamento(): boolean {
    return this.form?.get('type_item')?.value === 'medicamento';
  }

  get isBiologico(): boolean {
    return this.form?.get('type_item')?.value === 'biologico';
  }

  get isAccesorio(): boolean {
    return this.form?.get('type_item')?.value === 'accesorio';
  }

  get isConsumible(): boolean {
    return this.form?.get('type_item')?.value === 'consumible';
  }

  get tipoItemSeleccionado(): CatalogItemType | null {
    return this.form?.get('type_item')?.value || null;
  }

  addMetaRow(k = '', v = ''): void {
    this.metadataArray.push(this.fb.group({
      key: [k, [Validators.maxLength(50)]], // Sin required - opcional
      value: [v, [Validators.maxLength(500)]] // Sin required - opcional
    }));
  }
  removeMetaRow(i: number): void {
    this.metadataArray.removeAt(i);
  }

  private buildMetadataFromForm(): any {
    const meta: Record<string, any> = {};
    // Nota: itemType ahora es una columna separada, no se guarda en metadata
    const itemType = this.form.get('itemType')?.value as CatalogItemType || 'alimento';

    // Campos específicos según el tipo de item
    if (itemType === 'alimento') {
      meta['especie'] = this.form.get('especie')?.value || 'pollo';
      meta['raza'] = this.form.get('raza')?.value || 'Ross';
      meta['genero'] = this.form.get('genero')?.value || 'Mixto';
    } else if (itemType === 'medicamento') {
      const tipoMed = this.form.get('tipo_medicamento')?.value;
      const viaAdmin = this.form.get('via_administracion')?.value;
      const presentacion = this.form.get('presentacion')?.value;
      if (tipoMed) meta['tipo_medicamento'] = tipoMed;
      if (viaAdmin) meta['via_administracion'] = viaAdmin;
      if (presentacion) meta['presentacion'] = presentacion;
    } else if (itemType === 'biologico') {
      const tipoBio = this.form.get('tipo_biologico')?.value;
      const viaAplic = this.form.get('via_aplicacion')?.value;
      const tempAlm = this.form.get('temperatura_almacenamiento')?.value;
      if (tipoBio) meta['tipo_biologico'] = tipoBio;
      if (viaAplic) meta['via_aplicacion'] = viaAplic;
      if (tempAlm) meta['temperatura_almacenamiento'] = tempAlm;
    } else if (itemType === 'accesorio') {
      const tipoAcc = this.form.get('tipo_accesorio')?.value;
      const material = this.form.get('material')?.value;
      const dimensiones = this.form.get('dimensiones')?.value;
      if (tipoAcc) meta['tipo_accesorio'] = tipoAcc;
      if (material) meta['material'] = material;
      if (dimensiones) meta['dimensiones'] = dimensiones;
    } else if (itemType === 'consumible') {
      const tipoCons = this.form.get('tipo_consumible')?.value;
      const unidadMed = this.form.get('unidad_medida')?.value;
      if (tipoCons) meta['tipo_consumible'] = tipoCons;
      if (unidadMed) meta['unidad_medida'] = unidadMed;
    }

    // 2) libres (sin pisar reservadas) - solo agregar si tienen clave y valor
    for (const g of this.metadataArray.controls) {
      const k = (g.get('key')?.value || '').trim();
      const v = g.get('value')?.value;
      // Solo agregar si tiene clave (el valor puede estar vacío)
      if (!k) continue;
      if (this.RESERVED_KEYS.has(k)) continue;
      meta[k] = v || ''; // Permitir valores vacíos
    }
    return meta;
  }

  private fillFormFromMetadata(meta: any, itemType?: string): void {
    // defaults
    const tipo = itemType || 'alimento';
    this.form.get('itemType')?.setValue(tipo);
    this.resetearCamposDinamicos(tipo as CatalogItemType);

    this.metadataArray.clear();

    if (meta && typeof meta === 'object') {
      // Cargar campos específicos según el tipo
      if (tipo === 'alimento') {
        if (meta['especie']) this.form.get('especie')?.setValue(meta['especie']);
        if (meta['raza']) this.form.get('raza')?.setValue(meta['raza']);
        if (meta['genero']) this.form.get('genero')?.setValue(meta['genero']);
      } else if (tipo === 'medicamento') {
        if (meta['tipo_medicamento']) this.form.get('tipo_medicamento')?.setValue(meta['tipo_medicamento']);
        if (meta['via_administracion']) this.form.get('via_administracion')?.setValue(meta['via_administracion']);
        if (meta['presentacion']) this.form.get('presentacion')?.setValue(meta['presentacion']);
      } else if (tipo === 'biologico') {
        if (meta['tipo_biologico']) this.form.get('tipo_biologico')?.setValue(meta['tipo_biologico']);
        if (meta['via_aplicacion']) this.form.get('via_aplicacion')?.setValue(meta['via_aplicacion']);
        if (meta['temperatura_almacenamiento']) this.form.get('temperatura_almacenamiento')?.setValue(meta['temperatura_almacenamiento']);
      } else if (tipo === 'accesorio') {
        if (meta['tipo_accesorio']) this.form.get('tipo_accesorio')?.setValue(meta['tipo_accesorio']);
        if (meta['material']) this.form.get('material')?.setValue(meta['material']);
        if (meta['dimensiones']) this.form.get('dimensiones')?.setValue(meta['dimensiones']);
      } else if (tipo === 'consumible') {
        if (meta['tipo_consumible']) this.form.get('tipo_consumible')?.setValue(meta['tipo_consumible']);
        if (meta['unidad_medida']) this.form.get('unidad_medida')?.setValue(meta['unidad_medida']);
      }

      // libres - solo cargar los que tienen clave y no son campos estructurados
      Object.keys(meta)
        .filter(k => !this.RESERVED_KEYS.has(k))
        .forEach(k => this.addMetaRow(k, meta[k] || ''));
    }

    // No agregar fila vacía por defecto al editar - el usuario puede agregar si quiere
  }

  // ======= CRUD UI =======
  create(): void {
    this.editing = null;
    this.form.reset({
      codigo: '',
      nombre: '',
      activo: true,
      itemType: 'alimento', // Por defecto: alimento
      especie: 'pollo',
      raza: 'Ross',
      genero: 'Mixto',
      tipo_medicamento: '',
      via_administracion: '',
      presentacion: '',
      tipo_biologico: '',
      via_aplicacion: '',
      temperatura_almacenamiento: '',
      tipo_accesorio: '',
      material: '',
      dimensiones: '',
      tipo_consumible: '',
      unidad_medida: ''
    });
    this.metadataArray.clear();
    // No agregar fila vacía por defecto - el usuario puede agregar si quiere
    this.form.get('codigo')?.enable(); // Habilitar código al crear
    this.modalOpen = true;
  }

  edit(item: CatalogItemDto): void {
    this.editing = item;
    this.form.reset({
      codigo: item.codigo,
      nombre: item.nombre,
      activo: item.activo,
      itemType: item.itemType || 'alimento', // Usar itemType de la columna
      raza: 'Ross',
      genero: 'Mixto'
    });
    // Código no editable al editar (clave natural)
    this.form.get('codigo')?.disable();
    this.fillFormFromMetadata(item.metadata, item.itemType);
    this.modalOpen = true;
  }

  cancel(): void {
    this.modalOpen = false;
    this.editing = null;
    this.form.get('codigo')?.enable();
  }

  save(): void {
    if (this.form.invalid) {
      // Marcar todos los campos como touched para mostrar errores
      Object.keys(this.form.controls).forEach(key => {
        this.form.get(key)?.markAsTouched();
      });
      return;
    }

    // Validar que itemType esté seleccionado
    const itemType = this.form.get('itemType')?.value;
    if (!itemType) {
      alert('Por favor, seleccione un tipo de ítem');
      this.form.get('itemType')?.markAsTouched();
      return;
    }

    try {
      const raw = this.form.getRawValue();
      const metadata = this.buildMetadataFromForm();

      this.loading = true;

      if (this.editing) {
        const dto: CatalogItemUpdateRequest = {
          nombre: raw.nombre,
          itemType: raw.itemType,
          activo: raw.activo,
          metadata
        };
        this.svc.update(this.editing.id!, dto)
          .pipe(finalize(() => this.loading = false))
          .subscribe({
            next: () => {
              this.cancel();
              this.load();
            },
            error: (err) => {
              console.error('Error al actualizar:', err);
              alert('Error al actualizar el ítem. Por favor, intente nuevamente.');
            }
          });
      } else {
        const dto: CatalogItemCreateRequest = {
          codigo: raw.codigo,
          nombre: raw.nombre,
          itemType: raw.itemType || 'alimento',
          activo: raw.activo,
          metadata
        };
        this.svc.create(dto)
          .pipe(finalize(() => this.loading = false))
          .subscribe({
            next: () => {
              this.cancel();
              this.load();
            },
            error: (err) => {
              console.error('Error al crear:', err);
              alert('Error al crear el ítem. Por favor, intente nuevamente.');
            }
          });
      }
    } catch (error: any) {
      this.loading = false;
      alert(error.message || 'Error al procesar el formulario');
    }
  }

  delete(id: number): void {
    if (!confirm('¿Eliminar este ítem del catálogo?')) return;
    this.loading = true;
    this.svc.delete(id)
      .pipe(finalize(() => this.loading = false))
      .subscribe(() => this.load());
  }

  // ======= Data =======
  load(): void {
    this.loading = true;
    this.svc.list(this.q, this.page, this.pageSize)
      .pipe(finalize(() => this.loading = false))
      .subscribe((res: PagedResult<CatalogItemDto>) => {
        // filtros client-side (opcional; pásalos al backend cuando quieras)
        let items = res.items;

        if (this.typeFilter) {
          items = items.filter(x => (x.itemType || '') === this.typeFilter);
        }
        if (this.statusFilter !== 'all') {
          const active = this.statusFilter === 'active';
          items = items.filter(x => x.activo === active);
        }

        this.items = items;
        this.total = res.total;     // total real del backend
        this.page = res.page;
        this.pageSize = res.pageSize;
      });
  }

  next(): void {
    if (this.page * this.pageSize >= this.total) return;
    this.page++;
    this.load();
  }
  prev(): void {
    if (this.page <= 1) return;
    this.page--;
    this.load();
  }

  clearFilters(): void {
    this.q = '';
    this.typeFilter = '';
    this.statusFilter = 'all';
    this.pageSize = 20;
    this.page = 1;
    this.load();
  }

  // ======= Presentación / utils =======
  trackById = (_: number, r: CatalogItemDto) => r.id;

  typeOf(item: CatalogItemDto): string {
    return item.itemType || item.metadata?.type_item || '';
  }
  
  showDetail(item: CatalogItemDto): void {
    this.selectedItem = item;
    this.detailModalOpen = true;
  }
  
  closeDetail(): void {
    this.detailModalOpen = false;
    this.selectedItem = null;
  }
  
  // Convertir metadata a array para mostrar en tabla
  getMetadataAsArray(meta: any): Array<{key: string; value: any}> {
    if (!meta || typeof meta !== 'object') return [];
    return Object.keys(meta).map(key => ({
      key: key,
      value: meta[key]
    }));
  }
  
  // Formatear valor de metadata para mostrar
  formatMetadataValue(value: any): string {
    if (value === null || value === undefined) return '—';
    if (typeof value === 'boolean') return value ? 'Sí' : 'No';
    if (typeof value === 'object') return JSON.stringify(value);
    return String(value);
  }

  metaChips(meta: any): Array<{key: string; value: string}> {
    if (!meta) return [];
    const chips: Array<{key:string, value:string}> = [];
    if (meta.especie) chips.push({ key: 'especie', value: String(meta.especie) });
    if (meta.raza)    chips.push({ key: 'raza', value: String(meta.raza) });
    if (meta.genero)  chips.push({ key: 'género', value: String(meta.genero) });
    const reserved = this.RESERVED_KEYS;
    Object.keys(meta)
      .filter(k => !reserved.has(k))
      .slice(0, 2) // no saturar la tabla
      .forEach(k => chips.push({ key: k, value: String(meta[k]) }));
    return chips;
  }

  metaPreview(m: any): string {
    try {
      const s = JSON.stringify(m);
      return s.length > 60 ? s.substring(0, 60) + '…' : s;
    } catch { return ''; }
  }

  get totalPages(): number {
    const size = this.pageSize || 1;
    return Math.max(1, Math.ceil(this.total / size));
  }
  // Añade dentro de la clase CatalogoAlimentosListComponent
jsonPreview(): string {
  try {
    return JSON.stringify(this.buildMetadataFromForm(), null, 2);
  } catch {
    return '{}';
  }
}

}

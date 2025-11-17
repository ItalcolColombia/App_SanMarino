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
  razas = ['Ross', 'Cobb', 'Hubbard', 'Lohmann'];
  generos: Genero[] = ['Hembra', 'Macho', 'Mixto'];

  // Claves reservadas que gestionamos de forma estructurada
  private readonly RESERVED_KEYS = new Set(['type_item','especie','raza','genero']);

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
      // Estructurados → se guardan dentro de metadata
      type_item: ['alimento' as CatalogItemType, Validators.required],
      raza: ['Ross'],
      genero: ['Mixto'],
      // key/value libres
      metadata: this.fb.array<FormGroup>([])
    });

    this.load();
  }

  // ======= Helpers formulario =======
  get metadataArray(): FormArray<FormGroup> {
    return this.form.get('metadata') as FormArray<FormGroup>;
  }
  get isAlimento(): boolean {
    return this.form?.get('type_item')?.value === 'alimento';
  }

  addMetaRow(k = '', v = ''): void {
    this.metadataArray.push(this.fb.group({
      key: [k, [Validators.required, Validators.maxLength(50)]],
      value: [v, [Validators.required, Validators.maxLength(500)]]
    }));
  }
  removeMetaRow(i: number): void {
    this.metadataArray.removeAt(i);
  }

  private buildMetadataFromForm(): any {
    const meta: Record<string, any> = {};
    // 1) estructurados
    const type_item = this.form.get('type_item')?.value as CatalogItemType;
    meta['type_item'] = type_item;
    if (type_item === 'alimento') {
      meta['especie'] = 'pollo';
      meta['raza'] = this.form.get('raza')?.value;
      meta['genero'] = this.form.get('genero')?.value;
    }
    // 2) libres (sin pisar reservadas)
    for (const g of this.metadataArray.controls) {
      const k = (g.get('key')?.value || '').trim();
      const v = g.get('value')?.value;
      if (!k) continue;
      if (this.RESERVED_KEYS.has(k)) continue;
      meta[k] = v;
    }
    return meta;
    }

  private fillFormFromMetadata(meta: any): void {
    // defaults
    this.form.get('type_item')?.setValue('alimento');
    this.form.get('raza')?.setValue('Ross');
    this.form.get('genero')?.setValue('Mixto');

    this.metadataArray.clear();

    if (meta && typeof meta === 'object') {
      if (meta['type_item']) this.form.get('type_item')?.setValue(meta['type_item']);
      if (meta['raza']) this.form.get('raza')?.setValue(meta['raza']);
      if (meta['genero']) this.form.get('genero')?.setValue(meta['genero']);

      // libres
      Object.keys(meta)
        .filter(k => !this.RESERVED_KEYS.has(k))
        .forEach(k => this.addMetaRow(k, meta[k]));
    }

    if (this.metadataArray.length === 0) this.addMetaRow();
  }

  // ======= CRUD UI =======
  create(): void {
    this.editing = null;
    this.form.reset({
      codigo: '',
      nombre: '',
      activo: true,
      type_item: 'alimento',
      raza: 'Ross',
      genero: 'Mixto'
    });
    this.metadataArray.clear();
    this.addMetaRow(); // primera fila vacía
    this.modalOpen = true;
  }

  edit(item: CatalogItemDto): void {
    this.editing = item;
    this.form.reset({
      codigo: item.codigo,
      nombre: item.nombre,
      activo: item.activo,
      type_item: 'alimento',
      raza: 'Ross',
      genero: 'Mixto'
    });
    // Código no editable al editar (clave natural)
    this.form.get('codigo')?.disable();
    this.fillFormFromMetadata(item.metadata);
    this.modalOpen = true;
  }

  cancel(): void {
    this.modalOpen = false;
    this.editing = null;
    this.form.get('codigo')?.enable();
  }

  save(): void {
    if (this.form.invalid) return;

    const raw = this.form.getRawValue();
    const metadata = this.buildMetadataFromForm();

    this.loading = true;

    if (this.editing) {
      const dto: CatalogItemUpdateRequest = {
        nombre: raw.nombre,
        activo: raw.activo,
        metadata
      };
      this.svc.update(this.editing.id!, dto)
        .pipe(finalize(() => this.loading = false))
        .subscribe(() => {
          this.cancel();
          this.load();
        });
    } else {
      const dto: CatalogItemCreateRequest = {
        codigo: raw.codigo,
        nombre: raw.nombre,
        activo: raw.activo,
        metadata
      };
      this.svc.create(dto)
        .pipe(finalize(() => this.loading = false))
        .subscribe(() => {
          this.cancel();
          this.load();
        });
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
          items = items.filter(x => (x.metadata?.type_item || '') === this.typeFilter);
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

  typeOf(meta: any): string {
    return meta?.type_item || '';
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

// apps/features/catalogo-alimentos/pages/catalogo-alimentos-form/catalogo-alimentos-form.component.ts
import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';

import {
  CatalogoAlimentosService,
  CatalogItemCreateRequest,
  CatalogItemUpdateRequest,
  CatalogItemDto,
  CatalogItemType
} from '../../services/catalogo-alimentos.service';

type Genero = 'Hembra' | 'Macho' | 'Mixto';

@Component({
  selector: 'app-catalogo-alimentos-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, SidebarComponent],
  templateUrl: './catalogo-alimentos-form.component.html',
  styleUrls: ['./catalogo-alimentos-form.component.scss']
})
export class CatalogoAlimentosFormComponent implements OnInit {
  loading = false;
  editingId: number | null = null;

  // Opciones UI
  tiposItem: CatalogItemType[] = ['alimento', 'medicamento', 'accesorio', 'biologico', 'consumible', 'otro'];
  razas = ['Ross', 'Cobb', 'Hubbard', 'Lohmann'];
  generos: Genero[] = ['Hembra', 'Macho', 'Mixto'];

  // Reservadas para metadata estructurada
  private readonly RESERVED_KEYS = new Set(['type_item', 'especie', 'raza', 'genero']);

  form!: FormGroup;

  get fCodigo(): FormControl { return this.form.get('codigo') as FormControl; }
  get fNombre(): FormControl { return this.form.get('nombre') as FormControl; }
  get fActivo(): FormControl { return this.form.get('activo') as FormControl; }

  // Controles “estructurados” (se vuelcan a metadata)
  get fTypeItem(): FormControl { return this.form.get('type_item') as FormControl; }
  get fEsParaPollos(): FormControl { return this.form.get('es_para_pollos') as FormControl; }
  get fRaza(): FormControl { return this.form.get('raza') as FormControl; }
  get fGenero(): FormControl { return this.form.get('genero') as FormControl; }

  // Editor libre de metadata
  get metadataArray(): FormArray<FormGroup> {
    return this.form.get('metadata') as FormArray<FormGroup>;
  }

  // Derivados
  isAlimento = computed(() => this.fTypeItem.value === 'alimento');
  requiereEstructuraPollos = computed(() => this.isAlimento() && this.fEsParaPollos.value === true);

  jsonPreview = signal('{}');

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private svc: CatalogoAlimentosService
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      codigo: ['', [Validators.required, Validators.maxLength(10)]],
      nombre: ['', [Validators.required, Validators.maxLength(150)]],
      activo: [true, Validators.required],

      // Campos estructurados -> se guardan en metadata
      type_item: ['alimento' as CatalogItemType, Validators.required],
      es_para_pollos: [true],                 // solo guía UI; no se guarda como tal, se traduce a especie
      raza: ['Ross'],
      genero: ['Mixto'],

      // key/value libres
      metadata: this.fb.array<FormGroup>([])
    });

    // Cambios que afectan validaciones dinámicas
    this.fTypeItem.valueChanges.subscribe(() => this.applyDynamicValidators());
    this.fEsParaPollos.valueChanges.subscribe(() => this.applyDynamicValidators());
    this.applyDynamicValidators();

    // Cargar si viene id
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.editingId = +idParam;
      this.loadItem(this.editingId);
    } else {
      this.addMetaRow();
      this.updateJsonPreview();
    }

    // Mantener vista previa JSON actualizada
    this.form.valueChanges.subscribe(() => this.updateJsonPreview());
  }

  // ====== Metadata libre ======
  addMetaRow(k = '', v = ''): void {
    this.metadataArray.push(this.fb.group({
      key: [k, [Validators.required, Validators.maxLength(50)]],
      value: [v, [Validators.required, Validators.maxLength(500)]]
    }));
  }
  removeMetaRow(i: number): void {
    this.metadataArray.removeAt(i);
    this.updateJsonPreview();
  }

  private toMetadataObject(): any {
    const meta: Record<string, any> = {};

    // 1) Estructurados
    meta['type_item'] = this.fTypeItem.value;
    if (this.requiereEstructuraPollos()) {
      meta['especie'] = 'pollo';
      meta['raza'] = this.fRaza.value;
      meta['genero'] = this.fGenero.value;
    }

    // 2) Libres (ignorando claves reservadas para no sobrescribir)
    for (const g of this.metadataArray.controls) {
      const k = (g.get('key')?.value || '').trim();
      const v = g.get('value')?.value;
      if (!k) continue;
      if (this.RESERVED_KEYS.has(k)) continue; // protegido
      meta[k] = v;
    }
    return meta;
  }

  private fromMetadataObject(meta: any): void {
    // Default
    this.fTypeItem.setValue('alimento');
    this.fEsParaPollos.setValue(true);
    this.fRaza.setValue('Ross');
    this.fGenero.setValue('Mixto');

    this.metadataArray.clear();

    if (meta && typeof meta === 'object') {
      // Estructurados
      if (meta['type_item']) this.fTypeItem.setValue(meta['type_item']);
      const esPollo = meta['especie'] === 'pollo';
      this.fEsParaPollos.setValue(esPollo);
      if (meta['raza']) this.fRaza.setValue(meta['raza']);
      if (meta['genero']) this.fGenero.setValue(meta['genero']);

      // Libres
      Object.keys(meta)
        .filter(k => !this.RESERVED_KEYS.has(k))
        .forEach(k => this.addMetaRow(k, meta[k]));
    }
    if (this.metadataArray.length === 0) this.addMetaRow();

    this.applyDynamicValidators();
    this.updateJsonPreview();
  }

  // ====== Validación dinámica ======
  private applyDynamicValidators(): void {
    if (this.requiereEstructuraPollos()) {
      this.fRaza.addValidators([Validators.required]);
      this.fGenero.addValidators([Validators.required]);
    } else {
      this.fRaza.clearValidators();
      this.fGenero.clearValidators();
    }
    this.fRaza.updateValueAndValidity({ emitEvent: false });
    this.fGenero.updateValueAndValidity({ emitEvent: false });
  }

  // ====== Carga / Guardado ======
  loadItem(id: number): void {
    this.loading = true;
    this.svc.getById(id)
      .pipe(finalize(() => this.loading = false))
      .subscribe((item: CatalogItemDto) => {
        this.form.patchValue({
          codigo: item.codigo,
          nombre: item.nombre,
          activo: item.activo
        });
        this.form.get('codigo')?.disable(); // código no editable
        this.fromMetadataObject(item.metadata);
      });
  }

  save(): void {
    if (this.form.invalid) return;

    const raw = this.form.getRawValue();
    const metadata = this.toMetadataObject();

    this.loading = true;

    if (this.editingId) {
      const dto: CatalogItemUpdateRequest = {
        nombre: raw.nombre,
        activo: raw.activo,
        metadata
      };
      this.svc.update(this.editingId, dto)
        .pipe(finalize(() => this.loading = false))
        .subscribe(() => this.router.navigate(['../'], { relativeTo: this.route }));
    } else {
      const dto: CatalogItemCreateRequest = {
        codigo: raw.codigo,
        nombre: raw.nombre,
        activo: raw.activo,
        metadata
      };
      this.svc.create(dto)
        .pipe(finalize(() => this.loading = false))
        .subscribe(() => this.router.navigate(['../'], { relativeTo: this.route }));
    }
  }

  cancel(): void {
    this.router.navigate(['../'], { relativeTo: this.route });
  }

  // ====== Vista previa JSON ======
  private updateJsonPreview(): void {
    try {
      this.jsonPreview.set(JSON.stringify(this.toMetadataObject(), null, 2));
    } catch {
      this.jsonPreview.set('{}');
    }
  }
}

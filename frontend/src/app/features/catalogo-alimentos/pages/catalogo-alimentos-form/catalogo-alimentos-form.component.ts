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
  imports: [CommonModule, ReactiveFormsModule],
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

  // Controles "estructurados"
  get fItemType(): FormControl { return this.form.get('itemType') as FormControl; }
  get fEsParaPollos(): FormControl { return this.form.get('es_para_pollos') as FormControl; }
  get fRaza(): FormControl { return this.form.get('raza') as FormControl; }
  get fGenero(): FormControl { return this.form.get('genero') as FormControl; }
  
  // Controles específicos para VACUNA
  get fTemperatura(): FormControl { return this.form.get('temperatura') as FormControl; }
  get fFechaVencimiento(): FormControl { return this.form.get('fecha_vencimiento') as FormControl; }
  get fLote(): FormControl { return this.form.get('lote') as FormControl; }
  get fFabricante(): FormControl { return this.form.get('fabricante') as FormControl; }
  get fDosis(): FormControl { return this.form.get('dosis') as FormControl; }
  get fViaAdministracion(): FormControl { return this.form.get('via_administracion') as FormControl; }

  // Editor libre de metadata
  get metadataArray(): FormArray<FormGroup> {
    return this.form.get('metadata') as FormArray<FormGroup>;
  }

  // Derivados
  isAlimento = computed(() => this.fItemType.value === 'alimento');
  isVacuna = computed(() => this.fItemType.value === 'vacuna');
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

      // Tipo de item (columna separada, no en metadata)
      itemType: ['alimento' as CatalogItemType, Validators.required],

      // Campos estructurados para ALIMENTO -> se guardan en metadata
      es_para_pollos: [true],                 // solo guía UI; no se guarda como tal, se traduce a especie
      raza: ['Ross'],
      genero: ['Mixto'],

      // Campos específicos para VACUNA -> se guardan en metadata
      temperatura: [null],                    // Temperatura de almacenamiento
      fecha_vencimiento: [null],              // Fecha de vencimiento
      lote: [''],                             // Número de lote
      fabricante: [''],                       // Fabricante
      dosis: [''],                            // Dosis recomendada
      via_administracion: [''],                // Vía de administración

      // key/value libres
      metadata: this.fb.array<FormGroup>([])
    });

    // Cambios que afectan validaciones dinámicas
    this.fItemType.valueChanges.subscribe(() => {
      this.applyDynamicValidators();
      this.clearTypeSpecificFields();
    });
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

    // 1) Campos específicos para ALIMENTO
    if (this.isAlimento()) {
      if (this.requiereEstructuraPollos()) {
        meta['especie'] = 'pollo';
        meta['raza'] = this.fRaza.value;
        meta['genero'] = this.fGenero.value;
      }
    }

    // 2) Campos específicos para VACUNA
    if (this.isVacuna()) {
      if (this.fTemperatura.value) meta['temperatura'] = this.fTemperatura.value;
      if (this.fFechaVencimiento.value) meta['fecha_vencimiento'] = this.fFechaVencimiento.value;
      if (this.fLote.value) meta['lote'] = this.fLote.value;
      if (this.fFabricante.value) meta['fabricante'] = this.fFabricante.value;
      if (this.fDosis.value) meta['dosis'] = this.fDosis.value;
      if (this.fViaAdministracion.value) meta['via_administracion'] = this.fViaAdministracion.value;
    }

    // 3) Libres (ignorando claves reservadas para no sobrescribir)
    for (const g of this.metadataArray.controls) {
      const k = (g.get('key')?.value || '').trim();
      const v = g.get('value')?.value;
      if (!k) continue;
      if (this.RESERVED_KEYS.has(k)) continue; // protegido
      meta[k] = v;
    }
    return meta;
  }

  private fromMetadataObject(meta: any, itemType?: string): void {
    // Default
    this.fItemType.setValue(itemType || 'alimento');
    this.fEsParaPollos.setValue(true);
    this.fRaza.setValue('Ross');
    this.fGenero.setValue('Mixto');
    
    // Limpiar campos de vacuna
    this.fTemperatura.setValue(null);
    this.fFechaVencimiento.setValue(null);
    this.fLote.setValue('');
    this.fFabricante.setValue('');
    this.fDosis.setValue('');
    this.fViaAdministracion.setValue('');

    this.metadataArray.clear();

    if (meta && typeof meta === 'object') {
      // Campos específicos para ALIMENTO
      const esPollo = meta['especie'] === 'pollo';
      this.fEsParaPollos.setValue(esPollo);
      if (meta['raza']) this.fRaza.setValue(meta['raza']);
      if (meta['genero']) this.fGenero.setValue(meta['genero']);

      // Campos específicos para VACUNA
      if (meta['temperatura']) this.fTemperatura.setValue(meta['temperatura']);
      if (meta['fecha_vencimiento']) this.fFechaVencimiento.setValue(meta['fecha_vencimiento']);
      if (meta['lote']) this.fLote.setValue(meta['lote']);
      if (meta['fabricante']) this.fFabricante.setValue(meta['fabricante']);
      if (meta['dosis']) this.fDosis.setValue(meta['dosis']);
      if (meta['via_administracion']) this.fViaAdministracion.setValue(meta['via_administracion']);

      // Libres (excluyendo campos estructurados)
      const reservedKeys = new Set([...this.RESERVED_KEYS, 'especie', 'raza', 'genero', 'temperatura', 'fecha_vencimiento', 'lote', 'fabricante', 'dosis', 'via_administracion']);
      Object.keys(meta)
        .filter(k => !reservedKeys.has(k))
        .forEach(k => this.addMetaRow(k, meta[k]));
    }
    if (this.metadataArray.length === 0) this.addMetaRow();

    this.applyDynamicValidators();
    this.updateJsonPreview();
  }
  
  private clearTypeSpecificFields(): void {
    // Limpiar campos cuando cambia el tipo
    if (!this.isAlimento()) {
      this.fEsParaPollos.setValue(true);
      this.fRaza.setValue('Ross');
      this.fGenero.setValue('Mixto');
    }
    if (!this.isVacuna()) {
      this.fTemperatura.setValue(null);
      this.fFechaVencimiento.setValue(null);
      this.fLote.setValue('');
      this.fFabricante.setValue('');
      this.fDosis.setValue('');
      this.fViaAdministracion.setValue('');
    }
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
          itemType: item.itemType || 'alimento',
          activo: item.activo
        });
        this.form.get('codigo')?.disable(); // código no editable
        this.fromMetadataObject(item.metadata, item.itemType);
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
        itemType: raw.itemType,
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
        itemType: raw.itemType || 'alimento',
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

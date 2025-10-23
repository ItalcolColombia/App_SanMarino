// src/app/features/farm/pages/farm-list/farm-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash,faMagnifyingGlass } from '@fortawesome/free-solid-svg-icons';

import { FarmService, FarmDto, UpdateFarmDto } from '../../services/farm.service';
import { Company, CompanyService } from '../../../../core/services/company/company.service';
import { MasterListService } from '../../../../core/services/master-list/master-list.service';

import { DepartamentoService, DepartamentoDto } from '../../services/departamento.service';
import { CiudadService, CiudadDto } from '../../services/ciudad.service';
import { PaisService, PaisDto } from '../..//services/pais.service';

@Component({
  selector: 'app-farm-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SidebarComponent,
    FontAwesomeModule,
  ],
  templateUrl: './farm-list.component.html',
  styleUrls: ['./farm-list.component.scss'],
})
export class FarmListComponent implements OnInit {
  // Icons
  faPlus = faPlus;
  faPen  = faPen;
  faTrash = faTrash;
   faMagnifyingGlass = faMagnifyingGlass;

  // Estado general
  loading = false;
  embedded = false; // por si esta vista se embebe en otra

  // Datos base
  farms: FarmDto[] = [];
  companies: Company[] = [];
  regionales: string[] = [];         // opciones de regional (texto)
  paises: PaisDto[] = [];            // País → raíz de la cascada
  departamentos: DepartamentoDto[] = [];
  ciudades: CiudadDto[] = [];

  // Vista filtrada
  viewFarms: FarmDto[] = [];

  // Filtros (coinciden con el HTML)
  selectedRegional: string | null = null;
  filtroNombre = '';
  filtroEstado: 'A' | 'I' | '' = '';
  filtroTexto = '';

  // Modal / form
  modalOpen = false;
  form!: FormGroup;
  editing: FarmDto | null = null;


  // índices rápidos por ID
  private dptoById = new Map<number, DepartamentoDto>();
  private ciudadById = new Map<number, CiudadDto>();

  constructor(
    private readonly fb: FormBuilder,
    private readonly farmSvc: FarmService,
    private readonly companySvc: CompanyService,
    private readonly masterSvc: MasterListService,
    private readonly dptoSvc: DepartamentoService,
    private readonly ciudadSvc: CiudadService,
    private readonly paisSvc: PaisService
  ) {}

  // ================
  // Ciclo de vida
  // ================
  ngOnInit(): void {
    this.buildForm();
    this.loadAll();
  }

  // ==================
  // Inicialización
  // ==================
  private buildForm(): void {
    this.form = this.fb.group({
      // ID sugerido (solo lectura en el HTML)
      id:             [null],

      // Requeridos
      companyId:      [null, Validators.required],
      name:           ['', [Validators.required, Validators.maxLength(200)]],
      status:         ['A', Validators.required], // 'A'|'I'

      // Regional como texto (si luego pasa a ID, mapear)
      regional:       [''],

      // Cascada País → Departamento → Ciudad
      paisId:         [null],          // solo UI; backend infiere país vía departamento
      departamentoId: [null],
      ciudadId:       [null],

      // (Opcional) visibles en tu tabla
      department:     [''],
      city:           [''],
    });
  }

  private loadAll(): void {
    this.loading = true;
    forkJoin({
      farms:     this.farmSvc.getAll(),
      companies: this.companySvc.getAll(),
      regionMl:  this.masterSvc.getByKey('region_option_key'),
      paises:    this.paisSvc.getAll(),
      dptos:     this.dptoSvc.getAll(),     // ⬅️ ahora cargamos todos los dptos
      ciudades:  this.ciudadSvc.getAll(),   // ⬅️ y todas las ciudades
    })
    .pipe(finalize(() => (this.loading = false)))
    .subscribe(({ farms, companies, regionMl, paises, dptos, ciudades }) => {
      this.farms      = farms ?? [];
      this.companies  = companies ?? [];
      this.regionales = this.normalizeRegionStrings(regionMl);
      this.paises     = paises ?? [];
      this.departamentos = dptos ?? [];
      this.ciudades      = ciudades ?? [];
      // Construir índices rápidos
       this.dptoById.clear();
       this.ciudadById.clear();
        (this.departamentos ?? []).forEach(d => this.dptoById.set(d.departamentoId, d));
        (this.ciudades ?? []).forEach(c => this.ciudadById.set(c.municipioId, c));
      // Rellenar nombres de dpto/ciudad en las granjas
      this.farms.forEach(f => {
        if (f.departamentoId != null) {
          f.department = this.dptoById.get(f.departamentoId)?.departamentoNombre ?? '';
        }
        if (f.ciudadId != null) {
          f.city = this.ciudadById.get(f.ciudadId)?.municipioNombre ?? '';
        }
      });

      this.recomputeList();

    });
  }

  private normalizeRegionStrings(src: any): string[] {
    const raw = src?.options ?? src ?? [];
    if (!Array.isArray(raw)) return [];
    if (raw.length && typeof raw[0] === 'object') {
      return raw.map((o: any) => String(o.label ?? o.name ?? o.text ?? o.value ?? o.id));
    }
    return raw.map((x: any) => String(x));
  }

  // =========================
  // Filtros / Vista tabla
  // =========================
  recomputeList(): void {
    const nameFilter = (this.filtroNombre || '').trim().toLowerCase();
    const text = (this.filtroTexto || '').trim().toLowerCase();
    const selectedRegional = (this.selectedRegional || '').toLowerCase();
    const estado = this.filtroEstado;

    this.viewFarms = (this.farms ?? []).filter((f) => {
      const regionalTxt = (f.regional ?? '').toLowerCase();
      const nombreTxt   = (f.name ?? '').toLowerCase();
      const companyTxt  = (this.companyName(f.companyId) ?? '').toLowerCase();
      const deptTxt     = (f.department ?? '').toLowerCase();
      const cityTxt     = (f.city ?? '').toLowerCase();

      const okRegional = selectedRegional ? regionalTxt === selectedRegional : true;
      const okNombre   = nameFilter ? nombreTxt.includes(nameFilter) : true;
      const okEstado   = estado ? f.status === estado : true;

      const okText     = text
        ? (nombreTxt.includes(text)
          || regionalTxt.includes(text)
          || companyTxt.includes(text)
          || deptTxt.includes(text)
          || cityTxt.includes(text))
        : true;

      return okRegional && okNombre && okEstado && okText;
    });
  }

  resetFilters(): void {
    this.selectedRegional = null;
    this.filtroNombre = '';
    this.filtroEstado = '';
    this.filtroTexto = '';
    this.recomputeList();
  }

  // =============
  // Modal
  // =============
  openModal(farm?: FarmDto): void {
    this.editing = farm ?? null;

    if (farm) {
      // ----- EDICIÓN -----
      this.form.reset({
        id: farm.id ?? null,                 // mostrado solo lectura
        companyId: farm.companyId ?? null,
        name: farm.name ?? '',
        regional: farm.regional ?? '',
        status: farm.status ?? 'A',

        // inferimos país desde el dpto
        paisId: null,
        departamentoId: farm.departamentoId ?? null,
        ciudadId: farm.ciudadId ?? null,

        department: farm.department ?? '',
        city: farm.city ?? '',
      });
      // Filtrar dptos/ciudades al dpto/ciudad de la granja (si tiene)
      if (farm.departamentoId) {
        this.ciudades = this.ciudades.filter(c => c.departamentoId === farm.departamentoId);
      }

      // Cargar la cascada existente
      if (farm.departamentoId) {
        // 1) obtener dpto para saber paisId
        this.dptoSvc.getById(farm.departamentoId).subscribe(d => {
          const paisId = (d as any)?.paisId ?? null;
          this.form.patchValue({ paisId });

          // 2) cargar dptos del país
          if (paisId != null) {
            this.dptoSvc.getByPaisId(paisId).subscribe(ds => {
              this.departamentos = ds ?? [];

              // 3) cargar ciudades del dpto actual
              this.ciudadSvc.getByDepartamentoId(farm.departamentoId!).subscribe(cs => {
                this.ciudades = cs ?? [];
              });
            });
          } else {
            this.departamentos = [];
            this.ciudades = [];
          }
        });
      } else {
        this.departamentos = [];
        this.ciudades = [];
      }
    } else {
      // ----- NUEVO -----
      const nextId = this.getNextFarmId();  // consecutivo sugerido (máx + 1)

      this.form.reset({
        id: nextId,                          // se muestra, no se envía
        companyId: null,
        name: '',
        regional: '',
        status: 'A',
        paisId: null,
        departamentoId: null,
        ciudadId: null,
        department: '',
        city: '',
      });
      this.departamentos = [];
      this.ciudades = [];
    }

    this.modalOpen = true;
  }

  cancel(): void {
    this.modalOpen = false;
    this.form.reset();
  }

  // =========================
  // Cascada País → Dpto → Ciudad
  // =========================
  onPaisChange(): void {
    const paisId = this.form.get('paisId')?.value;
    // limpiar descendientes
    this.form.patchValue({ departamentoId: null, ciudadId: null });
    this.departamentos = [];
    this.ciudades = [];
    if (paisId != null) {
      this.dptoSvc.getByPaisId(+paisId).subscribe(ds => this.departamentos = ds ?? []);
    }
  }

  onDepartamentoChange(): void {
    const dptoId = this.form.get('departamentoId')?.value;
    this.form.patchValue({ ciudadId: null, city: '' });
    this.ciudades = [];
    if (dptoId != null) {
      this.ciudadSvc.getByDepartamentoId(+dptoId).subscribe(cs => this.ciudades = cs ?? []);
    }
  }

  // =============
  // Persistencia
  // =============
  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue(); // incluye controles disabled

    // Resuelve regionalId numérico con fallback a 1
    const regionalId: number =
      raw?.regionalId !== undefined && raw?.regionalId !== null && raw?.regionalId !== ''
        ? Number(raw.regionalId)
        : (typeof raw?.regional === 'string' && /^\d+$/.test(raw.regional.trim()))
          ? Number(raw.regional.trim())
          : 1;

    // Normaliza status a 'A' | 'I'
    const status: 'A' | 'I' = (String(raw.status ?? 'A').toUpperCase() === 'I' ? 'I' : 'A');

    // Construye DTO base para API (sin campos sólo-UI)
    const dtoBase = {
      name: (raw.name ?? '').trim(),
      companyId: Number(raw.companyId ?? 1),
      status,
      regionalId, // 👈 siempre entero
      departamentoId: raw?.departamentoId != null && raw?.departamentoId !== '' ? Number(raw.departamentoId) : null,
      ciudadId:       raw?.ciudadId       != null && raw?.ciudadId       !== '' ? Number(raw.ciudadId)       : null,
    };

    this.loading = true;

    if (this.editing) {
      const dto = { id: this.editing.id, ...dtoBase }; // UpdateFarmDto
      this.farmSvc.update(dto)
        .pipe(finalize(() => (this.loading = false)))
        .subscribe(() => {
          this.modalOpen = false;
          this.loadAll();
        });
    } else {
      // CreateFarmDto
      this.farmSvc.create(dtoBase)
        .pipe(finalize(() => (this.loading = false)))
        .subscribe(() => {
          this.modalOpen = false;
          this.loadAll();
        });
    }
  }


  delete(id: number): void {
    if (!confirm('¿Eliminar esta granja?')) return;
    this.loading = true;
    this.farmSvc
      .delete(id)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe(() => this.loadAll());
  }

  // =============
  // Helpers
  // =============
  /** Calcula el siguiente ID consecutivo a partir de la lista cargada (máximo + 1). */
  private getNextFarmId(): number {
    const ids = (this.farms ?? []).map(f => Number(f.id)).filter(n => Number.isFinite(n));
    if (!ids.length) return 1;
    return Math.max(...ids) + 1;
    // Si quieres evitar “huecos”, aquí podrías buscar el menor entero no usado.
  }

  companyName(id: number | null | undefined): string {
    if (id == null) return '';
    return this.companies.find((c) => c.id === id)?.name ?? '';
  }

  trackByFarm = (_: number, f: FarmDto) => f.id;
}

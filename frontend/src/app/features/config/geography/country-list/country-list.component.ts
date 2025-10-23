import {
  Component,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  OnInit,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faGlobeAmericas,
  faMap,
  faCity,
  faPlus,
  faPen,
  faTrash,
  faTimes,
  faSave,
  faMagnifyingGlass,
  faCheck,
} from '@fortawesome/free-solid-svg-icons';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';

import {
  CountryService,
  PaisDto,
} from '../../../../core/services/country/country.service';
import {
  DepartmentService,
  DepartamentoDto,
} from '../../../../core/services/department/department.service';
import {
  CityService,
  CityDto,
  CreateCityDto,
  UpdateCityDto,
} from '../../../../core/services/city/city.service';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-country-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, FontAwesomeModule, SidebarComponent],
  templateUrl: './country-list.component.html',
  styleUrls: ['./country-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CountryListComponent implements OnInit {
  // Icons
  faGlobe = faGlobeAmericas;
  faMap   = faMap;
  faCity  = faCity;
  faPlus  = faPlus;
  faPen   = faPen;
  faTrash = faTrash;
  faTimes = faTimes;
  faSave  = faSave;
  faSearch = faMagnifyingGlass;
  faCheck = faCheck;

  // Tabs
  activeTab: 'countries' | 'departments' | 'cities' = 'countries';

  // Data
  countries: PaisDto[] = [];
  departments: DepartamentoDto[] = [];
  cities: CityDto[] = [];

  // Lookups
  countryMap: Record<number, string> = {};
  deptMap: Record<number, string> = {};
  deptByCountry: Map<number, DepartamentoDto[]> = new Map();
  private departmentsById = new Map<number, DepartamentoDto>();

  // Filters
  filterCountries = '';
  filterDepartments = '';
  filterCities = '';

  // Forms + modal state
  countryForm!: FormGroup;
  deptForm!: FormGroup;
  cityForm!: FormGroup;

  countryModalOpen = false;
  deptModalOpen = false;
  cityModalOpen = false;

  editingCountry: PaisDto | null = null;
  editingDept: DepartamentoDto | null = null;
  editingCity: CityDto | null = null;

  // For city form: filtered department list by country selection
  cityFormDepartments: DepartamentoDto[] = [];

  loading = false;

  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);
  private countrySvc = inject(CountryService);
  private deptSvc = inject(DepartmentService);
  private citySvc = inject(CityService);

  constructor(library: FaIconLibrary) {
    library.addIcons(
      this.faGlobe, this.faMap, this.faCity,
      this.faPlus, this.faPen, this.faTrash, this.faTimes, this.faSave, this.faSearch, this.faCheck
    );
  }

  ngOnInit(): void {
    // Build forms
    this.countryForm = this.fb.group({
      paisNombre: ['', Validators.required],
    });

    this.deptForm = this.fb.group({
      departamentoNombre: ['', Validators.required],
      paisId: [null, Validators.required],
      active: [true],
    });

    this.cityForm = this.fb.group({
      countryId: [null, Validators.required],
      departamentoId: [null, Validators.required],
      municipioNombre: ['', Validators.required],
      active: [true],
    });

    // Cascade: when country in cityForm changes → filter departments
    this.cityForm.get('countryId')!.valueChanges.subscribe((cid: number | null) => {
      this.cityFormDepartments = cid ? (this.deptByCountry.get(cid) ?? []) : [];
      // al cambiar país, reinicia el departamento
      this.cityForm.get('departamentoId')!.setValue(null);
      this.cdr.markForCheck();
    });

    this.loadAll();
  }

  // ────────────────────────────────────────────────────────────────
  // Carga de datos
  // ────────────────────────────────────────────────────────────────
  private loadAll(): void {
    this.loading = true;

    // Países
    this.countrySvc.getAll().subscribe({
      next: (countries) => {
        this.countries = countries;
        this.countryMap = {};
        countries.forEach((c) => (this.countryMap[c.paisId] = c.paisNombre));
        this.cdr.markForCheck();
      },
      error: (e) => console.error('Error countries', e),
    });

    // Departamentos
    this.deptSvc.getAll().subscribe({
      next: (deps) => {
        this.departments = deps;
        this.deptMap = {};
        this.deptByCountry = new Map();
        this.departmentsById = new Map();

        for (const d of deps) {
          this.deptMap[d.departamentoId] = d.departamentoNombre;
          this.departmentsById.set(d.departamentoId, d);

          const list = this.deptByCountry.get(d.paisId) ?? [];
          list.push(d);
          this.deptByCountry.set(d.paisId, list);
        }
        this.cdr.markForCheck();
      },
      error: (e) => console.error('Error departments', e),
    });

    // Ciudades
    this.citySvc.getAll().subscribe({
      next: (cities) => {
        this.cities = cities;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (e) => {
        console.error('Error cities', e);
        this.loading = false;
      },
    });
  }

  // ────────────────────────────────────────────────────────────────
  // Filtros (UI)
  // ────────────────────────────────────────────────────────────────
  get filteredCountries(): PaisDto[] {
    const t = this.filterCountries.trim().toLowerCase();
    if (!t) return this.countries;
    return this.countries.filter((c) => c.paisNombre.toLowerCase().includes(t));
  }
  get filteredDepartments(): DepartamentoDto[] {
    const t = this.filterDepartments.trim().toLowerCase();
    if (!t) return this.departments;
    return this.departments.filter(
      (d) =>
        d.departamentoNombre.toLowerCase().includes(t) ||
        (this.countryMap[d.paisId] ?? '').toLowerCase().includes(t)
    );
  }
  get filteredCities(): CityDto[] {
    const t = this.filterCities.trim().toLowerCase();
    if (!t) return this.cities;

    return this.cities.filter((m) => {
      const dept = this.departmentsById.get(m.departamentoId);
      const deptName = dept ? dept.departamentoNombre : '';
      const countryName = dept ? (this.countryMap[dept.paisId] ?? '') : '';
      return (
        m.municipioNombre.toLowerCase().includes(t) ||
        deptName.toLowerCase().includes(t) ||
        countryName.toLowerCase().includes(t)
      );
    });
  }

  // ────────────────────────────────────────────────────────────────
  // Tabs
  // ────────────────────────────────────────────────────────────────
  setTab(tab: 'countries' | 'departments' | 'cities') {
    this.activeTab = tab;
    // opcional: limpiar filtro al cambiar
    // this.filterCountries = this.filterDepartments = this.filterCities = '';
  }

  // ────────────────────────────────────────────────────────────────
  // País
  // ────────────────────────────────────────────────────────────────
  openCountryModal(c?: PaisDto) {
    this.editingCountry = c ?? null;
    this.countryForm.reset({ paisNombre: c?.paisNombre ?? '' });
    this.countryModalOpen = true;
  }
  closeCountryModal() {
    this.countryModalOpen = false;
  }
  saveCountry() {
    if (this.countryForm.invalid) {
      this.countryForm.markAllAsTouched();
      return;
    }
    this.loading = true;
    const v = this.countryForm.value as { paisNombre: string };

    const call$: Observable<any> = this.editingCountry
      ? this.countrySvc.update({ paisId: this.editingCountry.paisId, paisNombre: v.paisNombre })
      : this.countrySvc.create({ paisNombre: v.paisNombre });

    call$.subscribe({
      next: () => {
        this.closeCountryModal();
        this.loadAll();
      },
      error: (e) => {
        console.error('Error saving country', e);
        this.loading = false;
      },
    });
  }
  deleteCountry(id: number) {
    if (!confirm('¿Eliminar este país?')) return;
    this.loading = true;
    this.countrySvc.delete(id).subscribe({
      next: () => this.loadAll(),
      error: (e) => {
        console.error('Error deleting country', e);
        this.loading = false;
      },
    });
  }

  // ────────────────────────────────────────────────────────────────
  // Departamento
  // ────────────────────────────────────────────────────────────────
  openDeptModal(d?: DepartamentoDto) {
    this.editingDept = d ?? null;
    this.deptForm.reset({
      departamentoNombre: d?.departamentoNombre ?? '',
      paisId: d?.paisId ?? null,
      active: d?.active ?? true,
    });
    this.deptModalOpen = true;
  }
  closeDeptModal() {
    this.deptModalOpen = false;
  }
  saveDept() {
    if (this.deptForm.invalid) {
      this.deptForm.markAllAsTouched();
      return;
    }
    this.loading = true;
    const v = this.deptForm.value as {
      departamentoNombre: string;
      paisId: number;
      active: boolean;
    };

    const call$: Observable<any> = this.editingDept
      ? this.deptSvc.update({
          departamentoId: this.editingDept.departamentoId,
          departamentoNombre: v.departamentoNombre,
          paisId: v.paisId,
          active: v.active,
        })
      : this.deptSvc.create({
          departamentoNombre: v.departamentoNombre,
          paisId: v.paisId,
          active: v.active,
        });

    call$.subscribe({
      next: () => {
        this.closeDeptModal();
        this.loadAll();
      },
      error: (e) => {
        console.error('Error saving department', e);
        this.loading = false;
      },
    });
  }
  deleteDept(id: number) {
    if (!confirm('¿Eliminar este departamento?')) return;
    this.loading = true;
    this.deptSvc.delete(id).subscribe({
      next: () => this.loadAll(),
      error: (e) => {
        console.error('Error deleting department', e);
        this.loading = false;
      },
    });
  }

  // ────────────────────────────────────────────────────────────────
  // Ciudad
  // ────────────────────────────────────────────────────────────────
  openCityModal(c?: CityDto) {
    this.editingCity = c ?? null;

    let countryId: number | null = null;
    if (c) {
      const dept = this.departmentsById.get(c.departamentoId) || this.departments.find(d => d.departamentoId === c.departamentoId);
      countryId = dept?.paisId ?? null;
    }

    // Sembrar la lista dependiente para que no "desaparezcan" los combos
    this.cityFormDepartments = countryId ? (this.deptByCountry.get(countryId) ?? []) : [];

    // Reset controlado (sin emitir para no pisar el departamento en edición)
    this.cityForm.reset({
      countryId,
      departamentoId: c?.departamentoId ?? null,
      municipioNombre: c?.municipioNombre ?? '',
      active: c?.active ?? true,
    }, { emitEvent: false });

    // Si hay país precargado y quieres re-ejecutar el filtro:
    if (countryId) this.cityForm.get('countryId')!.setValue(countryId);

    this.cityModalOpen = true;
    this.cdr.detectChanges();
  }
  closeCityModal() {
    this.cityModalOpen = false;
    this.editingCity = null;
    // Limpia form y lista dependiente para el próximo alta
    this.cityForm.reset({
      countryId: null,
      departamentoId: null,
      municipioNombre: '',
      active: true,
    }, { emitEvent: false });
    this.cityFormDepartments = [];
    this.cdr.markForCheck();
  }
  saveCity() {
    if (this.cityForm.invalid) {
      this.cityForm.markAllAsTouched();
      return;
    }
    this.loading = true;

    const v = this.cityForm.value as {
      countryId: number;
      departamentoId: number;
      municipioNombre: string;
      active: boolean;
    };

    const call$: Observable<any> = this.editingCity
      ? this.citySvc.update({
          municipioId: this.editingCity.municipioId,
          municipioNombre: v.municipioNombre,
          departamentoId: v.departamentoId,
          active: v.active,
        } as UpdateCityDto)
      : this.citySvc.create({
          municipioNombre: v.municipioNombre,
          departamentoId: v.departamentoId,
          active: v.active,
        } as CreateCityDto);

    call$.subscribe({
      next: () => {
        // Cierra y limpia para evitar estados residuales
        this.closeCityModal();
        this.loadAll();
      },
      error: (e) => {
        console.error('Error saving city', e);
        this.loading = false;
      },
    });
  }
  deleteCity(id: number) {
    if (!confirm('¿Eliminar esta ciudad?')) return;
    this.loading = true;
    this.citySvc.delete(id).subscribe({
      next: () => this.loadAll(),
      error: (e) => {
        console.error('Error deleting city', e);
        this.loading = false;
      },
    });
  }

  // ────────────────────────────────────────────────────────────────
  // Helpers para template
  // ────────────────────────────────────────────────────────────────
  getCountryName(departamentoId: number | null | undefined): string {
    if (!departamentoId) return '—';
    const d = this.departmentsById.get(departamentoId)
          ?? this.departments.find(x => x.departamentoId === departamentoId);
    if (!d) return '—';
    return this.countryMap[d.paisId] ?? '—';
  }

  trackByCountryId = (_: number, c: PaisDto) => c.paisId;
  trackByDeptId    = (_: number, d: DepartamentoDto) => d.departamentoId;
  trackByCityId    = (_: number, m: CityDto) => m.municipioId;
}

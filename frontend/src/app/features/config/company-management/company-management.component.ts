// src/app/features/config/company-management/company-management.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  Validators
} from '@angular/forms';

import { SidebarComponent } from '../../../shared/components/sidebar/sidebar.component';
import {
  FontAwesomeModule,
  FaIconLibrary
} from '@fortawesome/angular-fontawesome';
import {
  faBuilding,
  faMobileAlt,
  faPen,
  faTrash,
  faPlus,
  faAngleLeft,
  faAngleRight,
  faMagnifyingGlass,
  faShieldHalved
} from '@fortawesome/free-solid-svg-icons';

import { finalize, Observable, forkJoin, of } from 'rxjs';
import { switchMap, catchError, map } from 'rxjs/operators';

// Servicios: company + master-list (tipos ID)
import { CompanyService, Company } from '../../../core/services/company/company.service';
import { MasterListService } from '../../../core/services/master-list/master-list.service';

// Servicios: geografía (IDs → nombres)
import { CountryService, PaisDto } from '../../../core/services/country/country.service';
import { DepartmentService, DepartamentoDto } from '../../../core/services/department/department.service';
import { CityService, CityDto } from '../../../core/services/city/city.service';

// Servicios: roles (paso 2)
import { RoleService, Role } from '../../../core/services/role/role.service';
// Servicio: empresa-país
import { CompanyPaisService } from '../../../core/services/company-pais/company-pais.service';

@Component({
  selector: 'app-company-management',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    RouterModule,
    SidebarComponent,
    FontAwesomeModule
  ],
  templateUrl: './company-management.component.html',
  styleUrls: ['./company-management.component.scss']
})
export class CompanyManagementComponent implements OnInit {
  // Icons
  faBuilding = faBuilding;
  faMobileAlt = faMobileAlt;
  faPen = faPen;
  faTrash = faTrash;
  faPlus = faPlus;
  faPrev = faAngleLeft;
  faNext = faAngleRight;
  faSearch = faMagnifyingGlass;
  faShield = faShieldHalved;

  // Listado principal
  list: Company[] = [];
  filteredList: Company[] = []; // Lista filtrada para mostrar en tabla

  // Filtro por país
  filterPaisId: number | null = null;

  // Form / estado
  form!: FormGroup;
  editing = false;
  modalOpen = false;
  loading = false;

  // Wizard
  step: 1 | 2 = 1;

  // Geografía para selects (usando servicios)
  countries: { code: string; name: string }[] = []; // code = paisId en string
  states:    { code: string; name: string }[] = []; // code = departamentoId en string
  cities:    string[] = [];                         // nombre de ciudad

  // Lookups por ID para mostrar nombres legibles en tabla
  geoCountries: PaisDto[] = [];
  geoDepartments: DepartamentoDto[] = [];
  geoCities: CityDto[] = [];
  countryMapById: Record<number, string> = {};
  deptMapById: Record<number, string> = {};
  cityMapById: Record<number, string> = {};
  deptByCountryId = new Map<number, DepartamentoDto[]>();
  cityByDeptId   = new Map<number, CityDto[]>();

  // Roles (paso 2)
  roles: Role[] = [];
  rolesMap = new Map<number, Role>();
  roleIds: number[] = [];
  roleFilter = '';
  previewRoleId: number | null = null;

  // Países asignados a la empresa (multi-selección)
  selectedPaisIds: number[] = [];
  availablePaises: PaisDto[] = [];

  // Mapa de países por empresa (para mostrar en tabla)
  companyPaisesMap: Map<number, PaisDto[]> = new Map();

  // Módulos visuales
  allModules = [
    { key: 'dashboard', label: 'Dashboard' },
    { key: 'reports',   label: 'Reportes'  },
    { key: 'farms',     label: 'Granjas'   },
    { key: 'users',     label: 'Usuarios'  }
  ];

  // Tipos de identificación
  identificationOptions: string[] = [];

  constructor(
    private fb: FormBuilder,
    private svc: CompanyService,
    private mlSvc: MasterListService,
    private countrySvc: CountryService,
    private deptSvc: DepartmentService,
    private citySvc: CityService,
    private roleSvc: RoleService,
    private companyPaisSvc: CompanyPaisService,
    library: FaIconLibrary
  ) {
    library.addIcons(
      faBuilding, faMobileAlt, faPen, faTrash, faPlus,
      faAngleLeft, faAngleRight, faMagnifyingGlass, faShieldHalved
    );
  }

  // ========= Lifecycle =========
  ngOnInit(): void {
    // Form base (mantengo country/state/city como strings para compatibilidad con tu HTML)
    this.form = this.fb.group({
      id:            [null],
      name:          ['', Validators.required],
      identifier:    ['', Validators.required],
      documentType:  ['', Validators.required],
      address:       [''],
      phone:         [''],
      email:         ['', Validators.email],
      country:       [''], // ← paisId como string (p.ej. "5")
      state:         [''], // ← departamentoId como string (p.ej. "42")
      city:          [''], // ← nombre de ciudad
      visualPermissions: this.fb.group(
        this.allModules.reduce((acc, mod) => {
          acc[mod.key] = [false];
          return acc;
        }, {} as Record<string, any>)
      ),
      mobileAccess:  [false],
    });

    // Master list: tipos de ID
    this.mlSvc.getByKey('type_identit').subscribe({
      next: ml => this.identificationOptions = ml?.options ?? [],
      error: err => console.error('No pude cargar tipos de identificación', err)
    });

    // Carga geografía desde servicios (y construye mapas)
    this.loadGeographyLookups();

    // Roles
    this.loadRoles();

    // Empresas
    this.loadCompanies();

    // Cargar países disponibles para selección
    this.loadAvailablePaises();
  }

  // ========= Loads =========
  private loadCompanies() {
    this.loading = true;
    this.svc.getAll()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: list => {
          this.list = list || [];
          this.applyFilters();
          // Cargar países asignados para cada empresa
          this.loadAllCompanyPaises();
        },
        error: err => console.error('Error cargando empresas', err)
      });
  }

  private loadAllCompanyPaises() {
    this.companyPaisesMap.clear();
    const loadOps = this.list.map(company => {
      if (!company.id) return of(null);
      return this.companyPaisSvc.getPaisesByCompany(company.id).pipe(
        map((paises: any[]) => {
          if (company.id) {
            this.companyPaisesMap.set(company.id, paises);
          }
          return null;
        }),
        catchError(err => {
          console.warn(`Error cargando países para empresa ${company.id}:`, err);
          return of(null);
        })
      );
    });

    if (loadOps.length > 0) {
      forkJoin(loadOps).subscribe();
    }
  }

  getCompanyPaises(companyId: number | undefined): PaisDto[] {
    if (!companyId) return [];
    return this.companyPaisesMap.get(companyId) || [];
  }

  getCompanyPaisesNames(companyId: number | undefined): string {
    const paises = this.getCompanyPaises(companyId);
    return paises.map(p => p.paisNombre).join(', ') || '—';
  }

  // ========= Filtros =========
  applyFilters() {
    let filtered = [...this.list];

    // Filtro por país
    if (this.filterPaisId !== null) {
      filtered = filtered.filter(company => {
        const paises = this.getCompanyPaises(company.id);
        return paises.some(p => p.paisId === this.filterPaisId);
      });
    }

    this.filteredList = filtered;
  }

  onFilterPaisChange(paisId: string | number | null) {
    if (paisId === '' || paisId === null || paisId === undefined) {
      this.filterPaisId = null;
    } else {
      this.filterPaisId = Number(paisId);
    }
    this.applyFilters();
  }

  clearFilters() {
    this.filterPaisId = null;
    this.applyFilters();
  }

  private loadGeographyLookups() {
    // Países
    this.countrySvc.getAll().subscribe({
      next: list => {
        this.geoCountries = list;
        this.countryMapById = {};
        this.countries = list.map(p => {
          this.countryMapById[p.paisId] = p.paisNombre;
          return { code: String(p.paisId), name: p.paisNombre };
        });
      },
      error: err => console.error('Error cargando países (IDs)', err)
    });

    // Departamentos
    this.deptSvc.getAll().subscribe({
      next: list => {
        this.geoDepartments = list;
        this.deptMapById = {};
        this.deptByCountryId = new Map<number, DepartamentoDto[]>();
        for (const d of list) {
          this.deptMapById[d.departamentoId] = d.departamentoNombre;
          const arr = this.deptByCountryId.get(d.paisId) ?? [];
          arr.push(d);
          this.deptByCountryId.set(d.paisId, arr);
        }
      },
      error: err => console.error('Error cargando departamentos (IDs)', err)
    });

    // Ciudades
    this.citySvc.getAll().subscribe({
      next: list => {
        this.geoCities = list;
        this.cityMapById = {};
        this.cityByDeptId = new Map<number, CityDto[]>();
        for (const m of list) {
          this.cityMapById[m.municipioId] = m.municipioNombre;
          const arr = this.cityByDeptId.get(m.departamentoId) ?? [];
          arr.push(m);
          this.cityByDeptId.set(m.departamentoId, arr);
        }
      },
      error: err => console.error('Error cargando ciudades (IDs)', err)
    });
  }

  private loadRoles() {
    this.roleSvc.getAll().subscribe({
      next: list => {
        this.roles = list || [];
        this.rolesMap = new Map(this.roles.map(r => [r.id, r]));
      },
      error: err => console.error('Error cargando roles', err)
    });
  }

  private loadAvailablePaises() {
    this.countrySvc.getAll().subscribe({
      next: list => {
        this.availablePaises = list || [];
      },
      error: err => console.error('Error cargando países disponibles', err)
    });
  }

  private loadCompanyPaises(companyId: number) {
    this.companyPaisSvc.getPaisesByCompany(companyId).subscribe({
      next: (paises: any[]) => {
        this.selectedPaisIds = paises.map(p => p.paisId);
      },
      error: err => console.error('Error cargando países de la empresa', err)
    });
  }

  togglePais(paisId: number) {
    const index = this.selectedPaisIds.indexOf(paisId);
    if (index >= 0) {
      this.selectedPaisIds.splice(index, 1);
    } else {
      this.selectedPaisIds.push(paisId);
    }
  }

  isPaisSelected(paisId: number): boolean {
    return this.selectedPaisIds.includes(paisId);
  }

  // ========= Wizard =========
  nextStep() {
    const required = ['name', 'identifier', 'documentType'];
    required.forEach(k => this.form.get(k)?.markAsTouched());
    if (required.some(k => this.form.get(k)?.invalid)) return;
    this.step = 2;
  }
  prevStep() { this.step = 1; }

  // ========= Modal =========
  openModal(c?: Company) {
    this.editing = !!c;
    this.step = 1;

    // Reset roles UI
    this.roleIds = [];
    this.previewRoleId = null;
    this.roleFilter = '';

    // Reset países seleccionados
    this.selectedPaisIds = [];

    // Reset visualizar permisos
    const vp = this.form.get('visualPermissions') as FormGroup;
    Object.keys(vp.controls).forEach(k => vp.get(k)?.setValue(false));

    if (c) {
      // Preseleccionar country/state/city desde IDs o strings antiguos
      const codeCountry = this.resolveCountryCode(c);
      this.onCountryChange(codeCountry); // prepara states
      const codeDept = this.resolveDeptCode(c);
      this.onStateChange(codeDept);      // prepara cities
      const cityName = this.resolveCityName(c);

      // Patch
      this.form.patchValue({
        id:           c.id ?? null,
        name:         c.name ?? '',
        identifier:   c.identifier ?? '',
        documentType: c.documentType ?? '',
        address:      c.address ?? '',
        phone:        c.phone ?? '',
        email:        c.email ?? '',
        country:      codeCountry,
        state:        codeDept,
        city:         cityName,
        mobileAccess: c.mobileAccess ?? false
      });

      // Visual perms existentes
      if (Array.isArray(c.visualPermissions)) {
        c.visualPermissions.forEach(key => vp.get(key)?.setValue(true));
      }

      // Roles existentes (si backend los envía)
      const existingRoleIds: number[] = (c as any)?.roleIds ?? [];
      if (Array.isArray(existingRoleIds)) {
        this.roleIds = [...existingRoleIds];
      }

      // Cargar países asignados a esta empresa
      if (c.id) {
        this.loadCompanyPaises(c.id);
      }
    } else {
      // Nuevo
      this.form.reset({
        id: null,
        name: '',
        identifier: '',
        documentType: '',
        address: '',
        phone: '',
        email: '',
        country: '',
        state: '',
        city: '',
        mobileAccess: false
      });
      this.states = [];
      this.cities = [];
    }

    this.modalOpen = true;
  }

  closeModal() {
    this.modalOpen = false;
  }

  // ========= Guardado =========
  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // Validar que se haya seleccionado al menos un país
    if (this.selectedPaisIds.length === 0) {
      alert('Debe seleccionar al menos un país para la empresa');
      return;
    }

    const v = this.form.getRawValue();

    // Visual permissions a array
    const vp: string[] = Object.entries(v.visualPermissions || {})
      .filter(([_, ok]) => !!ok)
      .map(([key]) => key);

    // Payload (mantengo compatibilidad con tus propiedades actuales)
    const payload: any = {
      id:            v.id,
      name:          v.name,
      identifier:    v.identifier,
      documentType:  v.documentType,
      address:       v.address,
      phone:         v.phone,
      email:         v.email,
      // Guardamos los "códigos" (ids en string) y city por nombre
      country:       v.country,
      state:         v.state,
      city:          v.city,
      visualPermissions: vp,
      mobileAccess:  v.mobileAccess,
      // NUEVO: roles asignados
      roleIds:       this.roleIds,
      // Opcional: si tu backend ya espera IDs numéricos, puedes enviar además:
      countryId:      this.toNumOrNull(v.country),
      departamentoId: this.toNumOrNull(v.state),
      municipioId:    this.findCityIdByName(this.toNumOrNull(v.state), v.city)
    };

    this.loading = true;
    const call$: Observable<any> = this.editing
      ? this.svc.update(payload)
      : this.svc.create(payload);

    call$
      .pipe(
        switchMap((company: Company) => {
          // Después de crear/actualizar la empresa, asignar países
          const companyId = company.id || payload.id;
          if (!companyId) {
            throw new Error('No se pudo obtener el ID de la empresa');
          }

          // Si es edición, primero remover todas las asignaciones existentes
          if (this.editing) {
            return this.companyPaisSvc.getPaisesByCompany(companyId).pipe(
              switchMap((currentPaises: any[]) => {
                // Remover países que ya no están seleccionados
                const toRemove = currentPaises
                  .filter(p => !this.selectedPaisIds.includes(p.paisId))
                  .map(p => ({ companyId, paisId: p.paisId }));

                // Agregar países nuevos
                const toAdd = this.selectedPaisIds
                  .filter(paisId => !currentPaises.some(p => p.paisId === paisId))
                  .map(paisId => ({ companyId, paisId }));

                // Ejecutar todas las operaciones
                const removeOps = toRemove.map(req =>
                  this.companyPaisSvc.removeCompanyFromPais(req).pipe(
                    catchError(err => {
                      console.warn('Error removiendo país:', err);
                      return of(null);
                    })
                  )
                );

                const addOps = toAdd.map(req =>
                  this.companyPaisSvc.assignCompanyToPais(req).pipe(
                    catchError(err => {
                      console.warn('Error asignando país:', err);
                      return of(null);
                    })
                  )
                );

                return forkJoin([...removeOps, ...addOps]).pipe(
                  map(() => company)
                );
              })
            );
          } else {
            // Si es creación, solo agregar países
            const addOps = this.selectedPaisIds.map(paisId =>
              this.companyPaisSvc.assignCompanyToPais({ companyId, paisId }).pipe(
                catchError(err => {
                  console.warn('Error asignando país:', err);
                  return of(null);
                })
              )
            );
            return forkJoin(addOps).pipe(map(() => company));
          }
        }),
        finalize(() => {
          this.loading = false;
          this.modalOpen = false;
        })
      )
      .subscribe({
        next: () => {
          this.loadCompanies();
          // Recargar también los países disponibles por si se creó uno nuevo
          this.loadAvailablePaises();
          alert('Empresa guardada exitosamente');
        },
        error: err => {
          console.error('Error guardando empresa:', err);
          alert('Error al guardar la empresa. Ver consola para más detalles.');
        }
      });
  }

  delete(id: number) {
    if (!confirm('¿Eliminar esta empresa?')) return;
    this.loading = true;
    this.svc.delete(id)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => this.loadCompanies(),
        error: err => console.error('Error eliminando empresa', err)
      });
  }

  // ========= Selects geografía (desde servicios) =========
  onCountryChange(code: string) {
    const countryId = this.toNumOrNull(code);
    const deps = countryId ? (this.deptByCountryId.get(countryId) ?? []) : [];
    this.states = deps.map(d => ({ code: String(d.departamentoId), name: d.departamentoNombre }));
    this.cities = [];
    this.form.patchValue({ state: '', city: '' });
  }

  onStateChange(code: string) {
    const depId = this.toNumOrNull(code);
    const cities = depId ? (this.cityByDeptId.get(depId) ?? []) : [];
    this.cities = cities.map(m => m.municipioNombre).sort((a, b) => a.localeCompare(b));
    this.form.patchValue({ city: '' });
  }

  // ========= Helpers de visualización en tabla =========
  displayCountry(c: Company): string {
    const countryId = (c as any)?.countryId as number | undefined;
    if (countryId && this.countryMapById[countryId]) return this.countryMapById[countryId];

    const asCode = this.toNumOrNull(c.country);
    if (asCode && this.countryMapById[asCode]) return this.countryMapById[asCode];

    return c.country || '—';
  }

  displayDept(c: Company): string {
    const depId = (c as any)?.departamentoId as number | undefined;
    if (depId && this.deptMapById[depId]) return this.deptMapById[depId];

    const asCode = this.toNumOrNull(c.state);
    if (asCode && this.deptMapById[asCode]) return this.deptMapById[asCode];

    return c.state || '—';
  }

  displayCity(c: Company): string {
    const cityId = (c as any)?.municipioId as number | undefined;
    if (cityId && this.cityMapById[cityId]) return this.cityMapById[cityId];

    return c.city || '—';
  }

  private toNumOrNull(v: any): number | null {
    if (v === null || v === undefined) return null;
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }

  private findCityIdByName(depId: number | null, name: string): number | null {
    if (!depId || !name) return null;
    const list = this.cityByDeptId.get(depId) ?? [];
    const m = list.find(x => (x.municipioNombre || '').toLowerCase() === name.toLowerCase());
    return m?.municipioId ?? null;
  }

  private resolveCountryCode(c: Company): string {
    const id = (c as any)?.countryId as number | undefined;
    if (id != null) return String(id);
    const asNum = this.toNumOrNull(c.country);
    return asNum != null ? String(asNum) : '';
    // Si c.country fuera nombre, no podemos mapearlo a id sin endpoint extra.
  }

  private resolveDeptCode(c: Company): string {
    const id = (c as any)?.departamentoId as number | undefined;
    if (id != null) return String(id);
    const asNum = this.toNumOrNull(c.state);
    return asNum != null ? String(asNum) : '';
  }

  private resolveCityName(c: Company): string {
    const id = (c as any)?.municipioId as number | undefined;
    if (id != null && this.cityMapById[id]) return this.cityMapById[id];
    return c.city ?? '';
  }

  // ========= Roles (paso 2) =========
  get filteredRoles(): Role[] {
    const t = (this.roleFilter || '').trim().toLowerCase();
    if (!t) return this.roles;
    return this.roles.filter(r =>
      r.name?.toLowerCase().includes(t) ||
      (r.permissions || []).some(p => (p || '').toLowerCase().includes(t))
    );
  }

  roleName(id: number): string {
    return this.rolesMap.get(id)?.name ?? `#${id}`;
  }

  toggleRole(id: number) {
    this.roleIds = this.roleIds.includes(id)
      ? this.roleIds.filter(x => x !== id)
      : [...this.roleIds, id];
  }

  removeRole(id: number) {
    this.roleIds = this.roleIds.filter(x => x !== id);
    if (this.previewRoleId === id) this.previewRoleId = null;
  }

  clearRoles() {
    this.roleIds = [];
    this.previewRoleId = null;
  }

  openRolePreview(id: number) {
    this.previewRoleId = id;
  }

  get selectedRolesPermissions(): string[] {
    const set = new Set<string>();
    for (const id of this.roleIds) {
      const r = this.rolesMap.get(id);
      (r?.permissions || []).forEach(p => set.add((p || '').toLowerCase()));
    }
    return Array.from(set).sort();
  }

  get previewRolePermissions(): string[] {
    if (!this.previewRoleId) return [];
    const r = this.rolesMap.get(this.previewRoleId);
    return (r?.permissions || []).map(p => (p || '').toLowerCase()).sort();
  }
}

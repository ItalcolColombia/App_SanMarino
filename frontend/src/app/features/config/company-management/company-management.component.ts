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
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faBuilding, faMobileAlt, faPen, faTrash, faPlus,
  faAngleLeft, faAngleRight, faMagnifyingGlass,
  faShieldHalved, faEye, faListUl
} from '@fortawesome/free-solid-svg-icons';

import { finalize, Observable, forkJoin, of } from 'rxjs';
import { switchMap, catchError, map } from 'rxjs/operators';

import { CompanyService, Company } from '../../../core/services/company/company.service';
import { TokenStorageService } from '../../../core/auth/token-storage.service';
import { MasterListService } from '../../../core/services/master-list/master-list.service';
import { CountryService, PaisDto } from '../../../core/services/country/country.service';
import { DepartmentService, DepartamentoDto } from '../../../core/services/department/department.service';
import { CityService, CityDto } from '../../../core/services/city/city.service';
import { RoleService, Role } from '../../../core/services/role/role.service';
import { CompanyPaisService } from '../../../core/services/company-pais/company-pais.service';
import { CompanyMenuService, CompanyMenuItem } from '../../../core/services/company-menu/company-menu.service';
import { MenuService, MenuItem } from '../../../core/services/menu/menu.service';

import { GeoMaps, GeoSelects } from './models/company-management.model';
import { readLogoFile } from './funciones/logo.funcion';
import {
  buildGeoMaps, getStatesForCountry, getCitiesForDept, findCityIdByName,
  resolveCountryCode, resolveDeptCode, resolveCityName, toNumOrNull
} from './funciones/geo.funcion';
import { filterRoles, getCombinedPermissions, getRolePermissions } from './funciones/roles.funcion';
import { diffPaises, addPaisesOps } from './funciones/paises.funcion';

@Component({
  selector: 'app-company-management',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterModule, FontAwesomeModule],
  templateUrl: './company-management.component.html',
  styleUrls: ['./company-management.component.scss']
})
export class CompanyManagementComponent implements OnInit {
  // Icons
  faBuilding = faBuilding; faMobileAlt = faMobileAlt; faPen = faPen;
  faTrash = faTrash; faPlus = faPlus; faPrev = faAngleLeft; faNext = faAngleRight;
  faSearch = faMagnifyingGlass; faShield = faShieldHalved; faEye = faEye; faListUl = faListUl;

  // Listado
  list: Company[] = [];
  filteredList: Company[] = [];
  filterPaisId: number | null = null;

  // Form / estado
  form!: FormGroup;
  editing = false;
  modalOpen = false;
  loading = false;
  logoPreviewDataUrl: string | null = null;
  logoChanged = false;
  logoError: string | null = null;

  // Wizard
  step: 1 | 2 = 1;

  // Geografía
  geoMaps: GeoMaps = {
    countryMapById: {},
    deptMapById: {},
    cityMapById: {},
    deptByCountryId: new Map(),
    cityByDeptId: new Map()
  };
  geoSelects: GeoSelects = { countries: [], states: [], cities: [] };
  geoCountries: PaisDto[] = [];

  // Roles
  roles: Role[] = [];
  rolesMap = new Map<number, Role>();
  roleIds: number[] = [];
  roleFilter = '';
  previewRoleId: number | null = null;

  // Países
  selectedPaisIds: number[] = [];
  availablePaises: PaisDto[] = [];
  companyPaisesMap = new Map<number, PaisDto[]>();

  // Módulos visuales
  allModules = [
    { key: 'dashboard', label: 'Dashboard' },
    { key: 'reports',   label: 'Reportes'  },
    { key: 'farms',     label: 'Granjas'   },
    { key: 'users',     label: 'Usuarios'  }
  ];
  identificationOptions: string[] = [];

  // Modal menú: ver
  menuViewModalOpen = false;
  menuViewCompanyName = '';
  menuViewCompanyId: number | null = null;
  menuViewTree: CompanyMenuItem[] = [];
  menuViewLoading = false;

  // Modal menú: editar
  menuEditModalOpen = false;
  menuEditCompanyName = '';
  menuEditCompanyId: number | null = null;
  menuEditTree: MenuItem[] = [];
  menuEditLoading = false;
  menuEditSaving = false;
  selectedMenuIdsForEdit: number[] = [];

  // Confirmación eliminar
  confirmDeleteOpen = false;
  confirmDeleteId: number | null = null;
  confirmDeleteName = '';

  // Toast
  toastVisible = false;
  toastType: 'success' | 'error' | 'info' = 'info';
  toastMessage = '';
  private toastTimeout: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: CompanyService,
    private tokenStorage: TokenStorageService,
    private mlSvc: MasterListService,
    private countrySvc: CountryService,
    private deptSvc: DepartmentService,
    private citySvc: CityService,
    private roleSvc: RoleService,
    private companyPaisSvc: CompanyPaisService,
    private companyMenuSvc: CompanyMenuService,
    private menuSvc: MenuService,
    library: FaIconLibrary
  ) {
    library.addIcons(
      faBuilding, faMobileAlt, faPen, faTrash, faPlus,
      faAngleLeft, faAngleRight, faMagnifyingGlass, faShieldHalved,
      faEye, faListUl
    );
  }

  // ========= Lifecycle =========
  ngOnInit(): void {
    this.form = this.fb.group({
      id:            [null],
      name:          ['', Validators.required],
      identifier:    ['', Validators.required],
      documentType:  ['', Validators.required],
      address:       [''],
      phone:         [''],
      email:         ['', Validators.email],
      country:       [''],
      state:         [''],
      city:          [''],
      visualPermissions: this.fb.group(
        this.allModules.reduce((acc, mod) => { acc[mod.key] = [false]; return acc; }, {} as Record<string, unknown>)
      ),
      mobileAccess: [false]
    });

    this.mlSvc.getByKey('type_identit').subscribe({
      next: ml => this.identificationOptions = ml?.optionValues ?? (Array.isArray(ml?.options) ? (ml.options as { value?: string }[]).map(o => o?.value ?? '') : []),
      error: err => console.error('Error cargando tipos de identificación', err)
    });

    this.loadGeographyLookups();
    this.loadRoles();
    this.loadCompanies();
    this.loadAvailablePaises();
  }

  // ========= Cargas =========
  private loadCompanies(): void {
    this.loading = true;
    this.svc.getAll()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: list => {
          this.list = list || [];
          this.applyFilters();
          this.loadAllCompanyPaises();
        },
        error: err => console.error('Error cargando empresas', err)
      });
  }

  private loadAllCompanyPaises(): void {
    this.companyPaisesMap.clear();
    const ops = this.list
      .filter(c => !!c.id)
      .map(c => this.companyPaisSvc.getPaisesByCompany(c.id!).pipe(
        map((paises: PaisDto[]) => { this.companyPaisesMap.set(c.id!, paises); return null; }),
        catchError(() => of(null))
      ));
    if (ops.length) forkJoin(ops).subscribe();
  }

  private loadGeographyLookups(): void {
    forkJoin({
      paises: this.countrySvc.getAll(),
      departamentos: this.deptSvc.getAll(),
      municipios: this.citySvc.getAll()
    }).subscribe({
      next: ({ paises, departamentos, municipios }) => {
        this.geoCountries = paises;
        this.geoMaps = buildGeoMaps(paises, departamentos, municipios);
        this.geoSelects = {
          ...this.geoSelects,
          countries: paises.map(p => ({ code: String(p.paisId), name: p.paisNombre }))
        };
      },
      error: err => console.error('Error cargando geografía', err)
    });
  }

  private loadRoles(): void {
    this.roleSvc.getAll().subscribe({
      next: list => {
        this.roles = list || [];
        this.rolesMap = new Map(this.roles.map(r => [r.id, r]));
      },
      error: err => console.error('Error cargando roles', err)
    });
  }

  private loadAvailablePaises(): void {
    this.countrySvc.getAll().subscribe({
      next: list => (this.availablePaises = list || []),
      error: err => console.error('Error cargando países disponibles', err)
    });
  }

  private loadCompanyPaises(companyId: number): void {
    this.companyPaisSvc.getPaisesByCompany(companyId).subscribe({
      next: (paises: PaisDto[]) => (this.selectedPaisIds = paises.map(p => p.paisId)),
      error: err => console.error('Error cargando países de la empresa', err)
    });
  }

  // ========= Filtros =========
  applyFilters(): void {
    if (this.filterPaisId === null) {
      this.filteredList = [...this.list];
      return;
    }
    this.filteredList = this.list.filter(c =>
      this.getCompanyPaises(c.id).some(p => p.paisId === this.filterPaisId)
    );
  }

  onFilterPaisChange(paisId: string | number | null): void {
    this.filterPaisId = (paisId === '' || paisId === null || paisId === undefined)
      ? null : Number(paisId);
    this.applyFilters();
  }

  clearFilters(): void {
    this.filterPaisId = null;
    this.applyFilters();
  }

  getCompanyPaises(companyId: number | undefined): PaisDto[] {
    if (!companyId) return [];
    return this.companyPaisesMap.get(companyId) || [];
  }

  getCompanyPaisesNames(companyId: number | undefined): string {
    return this.getCompanyPaises(companyId).map(p => p.paisNombre).join(', ') || '—';
  }

  // ========= Selects geografía =========
  onCountryChange(code: string): void {
    this.geoSelects = {
      ...this.geoSelects,
      states: getStatesForCountry(toNumOrNull(code), this.geoMaps),
      cities: []
    };
    this.form.patchValue({ state: '', city: '' });
  }

  onStateChange(code: string): void {
    this.geoSelects = {
      ...this.geoSelects,
      cities: getCitiesForDept(toNumOrNull(code), this.geoMaps)
    };
    this.form.patchValue({ city: '' });
  }

  // ========= Países empresa =========
  togglePais(paisId: number): void {
    const idx = this.selectedPaisIds.indexOf(paisId);
    if (idx >= 0) this.selectedPaisIds.splice(idx, 1);
    else this.selectedPaisIds.push(paisId);
  }

  isPaisSelected(paisId: number): boolean {
    return this.selectedPaisIds.includes(paisId);
  }

  // ========= Wizard =========
  nextStep(): void {
    ['name', 'identifier', 'documentType'].forEach(k => this.form.get(k)?.markAsTouched());
    if (['name', 'identifier', 'documentType'].some(k => this.form.get(k)?.invalid)) return;
    this.step = 2;
  }
  prevStep(): void { this.step = 1; }

  // ========= Modal =========
  openModal(c?: Company): void {
    this.editing = !!c;
    this.step = 1;
    this.logoError = null;
    this.logoChanged = false;
    this.logoPreviewDataUrl = null;
    this.roleIds = [];
    this.previewRoleId = null;
    this.roleFilter = '';
    this.selectedPaisIds = [];

    const vp = this.form.get('visualPermissions') as FormGroup;
    Object.keys(vp.controls).forEach(k => vp.get(k)?.setValue(false));

    if (c?.id) {
      this.loading = true;
      this.svc.getById(c.id)
        .pipe(finalize(() => (this.loading = false)))
        .subscribe({
          next: company => { this.applyCompanyToModal(company); this.modalOpen = true; },
          error: () => { this.applyCompanyToModal(c); this.modalOpen = true; }
        });
      return;
    }

    if (c) {
      this.applyCompanyToModal(c);
    } else {
      this.form.reset({ id: null, name: '', identifier: '', documentType: '', address: '', phone: '', email: '', country: '', state: '', city: '', mobileAccess: false });
      this.geoSelects = { ...this.geoSelects, states: [], cities: [] };
    }
    this.modalOpen = true;
  }

  private applyCompanyToModal(c: Company): void {
    this.logoPreviewDataUrl = c?.logoDataUrl ?? null;

    const codeCountry = resolveCountryCode((c as any)?.countryId, c.country);
    this.onCountryChange(codeCountry);
    const codeDept = resolveDeptCode((c as any)?.departamentoId, c.state);
    this.onStateChange(codeDept);
    const cityName = resolveCityName((c as any)?.municipioId, c.city, this.geoMaps);

    this.form.patchValue({
      id: c.id ?? null, name: c.name ?? '', identifier: c.identifier ?? '',
      documentType: c.documentType ?? '', address: c.address ?? '',
      phone: c.phone ?? '', email: c.email ?? '',
      country: codeCountry, state: codeDept, city: cityName,
      mobileAccess: c.mobileAccess ?? false
    });

    const vp = this.form.get('visualPermissions') as FormGroup;
    if (Array.isArray(c.visualPermissions)) {
      c.visualPermissions.forEach(key => vp.get(key)?.setValue(true));
    }

    const existingRoleIds: number[] = (c as any)?.roleIds ?? [];
    if (Array.isArray(existingRoleIds)) this.roleIds = [...existingRoleIds];

    if (c.id) this.loadCompanyPaises(c.id);
  }

  closeModal(): void { this.modalOpen = false; }

  // ========= Logo =========
  onLogoFileSelected(file: File | null): void {
    this.logoError = null;
    if (!file) return;

    readLogoFile(file).then(result => {
      if (result.error) { this.logoError = result.error; return; }
      this.logoPreviewDataUrl = result.dataUrl;
      this.logoChanged = true;
    });
  }

  clearLogo(): void {
    this.logoPreviewDataUrl = null;
    this.logoChanged = true;
    this.logoError = null;
  }

  // ========= Guardado =========
  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    if (this.selectedPaisIds.length === 0) {
      this.showToast('error', 'Debe seleccionar al menos un país para la empresa');
      return;
    }

    const v = this.form.getRawValue();
    const vp: string[] = Object.entries(v.visualPermissions || {})
      .filter(([, ok]) => !!ok)
      .map(([key]) => key);

    const stateNum = toNumOrNull(v.state);
    const payload: any = {
      id: v.id, name: v.name, identifier: v.identifier,
      documentType: v.documentType, address: v.address,
      phone: v.phone, email: v.email,
      country: v.country, state: v.state, city: v.city,
      visualPermissions: vp, mobileAccess: v.mobileAccess,
      roleIds: this.roleIds,
      countryId:      toNumOrNull(v.country),
      departamentoId: stateNum,
      municipioId:    findCityIdByName(stateNum, v.city, this.geoMaps)
    };

    if (this.logoChanged) payload.logoDataUrl = this.logoPreviewDataUrl ?? '';

    this.loading = true;
    const call$: Observable<any> = this.editing ? this.svc.update(payload) : this.svc.create(payload);

    call$.pipe(
      switchMap((company: Company) => {
        // Refrescar logo en sidebar si es la empresa activa
        const activeCompanyId = this.tokenStorage.get()?.activeCompanyId ?? null;
        if (this.logoChanged && activeCompanyId != null && company?.id === activeCompanyId) {
          this.tokenStorage.updateActiveCompanyLogo(company.logoDataUrl ?? null);
        }

        const companyId = company.id || payload.id;
        if (!companyId) throw new Error('No se pudo obtener el ID de la empresa');

        if (this.editing) {
          return this.companyPaisSvc.getPaisesByCompany(companyId).pipe(
            switchMap((currentPaises: any[]) => {
              const currentIds = currentPaises.map((p: any) => p.paisId as number);
              const { toAdd, toRemove } = diffPaises(companyId, currentIds, this.selectedPaisIds);

              const removeOps = toRemove.map(req =>
                this.companyPaisSvc.removeCompanyFromPais(req).pipe(catchError(() => of(null)))
              );
              const addOps = toAdd.map(req =>
                this.companyPaisSvc.assignCompanyToPais(req).pipe(catchError(() => of(null)))
              );

              return forkJoin([...removeOps, ...addOps]).pipe(map(() => company));
            })
          );
        } else {
          const ops = addPaisesOps(companyId, this.selectedPaisIds).map(req =>
            this.companyPaisSvc.assignCompanyToPais(req).pipe(catchError(() => of(null)))
          );
          return forkJoin(ops).pipe(map(() => company));
        }
      }),
      finalize(() => { this.loading = false; this.modalOpen = false; })
    ).subscribe({
      next: () => {
        this.loadCompanies();
        this.loadAvailablePaises();
        this.showToast('success', this.editing ? 'Empresa actualizada correctamente.' : 'Empresa creada correctamente.');
      },
      error: err => {
        console.error('Error guardando empresa:', err);
        this.showToast('error', err?.error?.message || err?.message || 'Error al guardar la empresa. Intente de nuevo.');
      }
    });
  }

  // ========= Eliminar =========
  openConfirmDelete(c: Company): void {
    this.confirmDeleteId = c.id ?? null;
    this.confirmDeleteName = c.name ?? '';
    this.confirmDeleteOpen = true;
  }

  cancelConfirmDelete(): void {
    this.confirmDeleteOpen = false;
    this.confirmDeleteId = null;
    this.confirmDeleteName = '';
  }

  confirmDelete(): void {
    if (this.confirmDeleteId == null) { this.cancelConfirmDelete(); return; }
    this.loading = true;
    this.svc.delete(this.confirmDeleteId)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => { this.cancelConfirmDelete(); this.loadCompanies(); this.showToast('success', 'Empresa eliminada correctamente.'); },
        error: err => this.showToast('error', err?.error?.message || err?.message || 'No se pudo eliminar la empresa.')
      });
  }

  // ========= Toast =========
  showToast(type: 'success' | 'error' | 'info', message: string): void {
    if (this.toastTimeout) clearTimeout(this.toastTimeout);
    this.toastType = type;
    this.toastMessage = message;
    this.toastVisible = true;
    this.toastTimeout = setTimeout(() => { this.toastVisible = false; this.toastTimeout = null; }, 5000);
  }

  closeToast(): void {
    if (this.toastTimeout) clearTimeout(this.toastTimeout);
    this.toastVisible = false;
    this.toastTimeout = null;
  }

  // ========= Getters de compatibilidad para el template =========
  get countries(): { code: string; name: string }[] { return this.geoSelects.countries; }
  get states(): { code: string; name: string }[] { return this.geoSelects.states; }
  get cities(): string[] { return this.geoSelects.cities; }

  // ========= Helpers tabla =========
  displayCountry(c: Company): string {
    const id = (c as any)?.countryId as number | undefined;
    if (id && this.geoMaps.countryMapById[id]) return this.geoMaps.countryMapById[id];
    const n = toNumOrNull(c.country);
    if (n && this.geoMaps.countryMapById[n]) return this.geoMaps.countryMapById[n];
    return c.country || '—';
  }

  displayDept(c: Company): string {
    const id = (c as any)?.departamentoId as number | undefined;
    if (id && this.geoMaps.deptMapById[id]) return this.geoMaps.deptMapById[id];
    const n = toNumOrNull(c.state);
    if (n && this.geoMaps.deptMapById[n]) return this.geoMaps.deptMapById[n];
    return c.state || '—';
  }

  displayCity(c: Company): string {
    const id = (c as any)?.municipioId as number | undefined;
    if (id && this.geoMaps.cityMapById[id]) return this.geoMaps.cityMapById[id];
    return c.city || '—';
  }

  // ========= Roles (paso 2) =========
  get filteredRoles(): Role[] {
    return filterRoles(this.roles, this.roleFilter);
  }

  roleName(id: number): string {
    return this.rolesMap.get(id)?.name ?? `#${id}`;
  }

  toggleRole(id: number): void {
    this.roleIds = this.roleIds.includes(id)
      ? this.roleIds.filter(x => x !== id)
      : [...this.roleIds, id];
  }

  removeRole(id: number): void {
    this.roleIds = this.roleIds.filter(x => x !== id);
    if (this.previewRoleId === id) this.previewRoleId = null;
  }

  clearRoles(): void {
    this.roleIds = [];
    this.previewRoleId = null;
  }

  openRolePreview(id: number): void { this.previewRoleId = id; }

  get selectedRolesPermissions(): string[] {
    return getCombinedPermissions(this.roleIds, this.rolesMap);
  }

  get previewRolePermissions(): string[] {
    return getRolePermissions(this.previewRoleId, this.rolesMap);
  }

  // ========= Menú por empresa =========
  openMenuView(c: Company): void {
    this.menuViewCompanyName = c.name ?? '';
    this.menuViewCompanyId = c.id ?? null;
    this.menuViewModalOpen = true;
    this.menuViewTree = [];
    this.menuViewLoading = true;
    if (this.menuViewCompanyId != null) {
      this.companyMenuSvc.getMenusForCompany(this.menuViewCompanyId)
        .pipe(finalize(() => (this.menuViewLoading = false)))
        .subscribe({
          next: tree => (this.menuViewTree = tree ?? []),
          error: () => (this.menuViewTree = [])
        });
    } else {
      this.menuViewLoading = false;
    }
  }

  closeMenuView(): void {
    this.menuViewModalOpen = false;
    this.menuViewCompanyName = '';
    this.menuViewCompanyId = null;
    this.menuViewTree = [];
  }

  openMenuEdit(c: Company): void {
    this.menuEditCompanyName = c.name ?? '';
    this.menuEditCompanyId = c.id ?? null;
    this.menuEditModalOpen = true;
    this.menuEditTree = [];
    this.selectedMenuIdsForEdit = [];
    this.menuEditLoading = true;
    if (this.menuEditCompanyId == null) { this.menuEditLoading = false; return; }

    forkJoin({
      allMenus: this.menuSvc.getTree(),
      companyMenus: this.companyMenuSvc.getMenusForCompany(this.menuEditCompanyId)
    }).pipe(finalize(() => (this.menuEditLoading = false)))
      .subscribe({
        next: ({ allMenus, companyMenus }) => {
          this.menuEditTree = allMenus ?? [];
          this.selectedMenuIdsForEdit = this.flattenMenuIds(companyMenus ?? []);
        },
        error: () => (this.menuEditTree = [])
      });
  }

  closeMenuEdit(): void {
    this.menuEditModalOpen = false;
    this.menuEditCompanyName = '';
    this.menuEditCompanyId = null;
    this.menuEditTree = [];
    this.selectedMenuIdsForEdit = [];
  }

  private flattenMenuIds(items: CompanyMenuItem[]): number[] {
    const ids: number[] = [];
    const visit = (nodes: CompanyMenuItem[]) => {
      for (const n of nodes) { ids.push(n.id); if (n.children?.length) visit(n.children); }
    };
    visit(items);
    return ids;
  }

  isMenuIdSelected(menuId: number): boolean {
    return this.selectedMenuIdsForEdit.includes(menuId);
  }

  toggleMenuSelection(menuId: number): void {
    const idx = this.selectedMenuIdsForEdit.indexOf(menuId);
    this.selectedMenuIdsForEdit = idx >= 0
      ? this.selectedMenuIdsForEdit.filter(id => id !== menuId)
      : [...this.selectedMenuIdsForEdit, menuId];
  }

  saveCompanyMenus(): void {
    if (this.menuEditCompanyId == null) return;
    this.menuEditSaving = true;
    this.companyMenuSvc.setCompanyMenus(this.menuEditCompanyId, {
      menuIds: this.selectedMenuIdsForEdit,
      isEnabled: true
    }).pipe(finalize(() => (this.menuEditSaving = false)))
      .subscribe({
        next: () => { this.showToast('success', 'Menú guardado correctamente.'); this.closeMenuEdit(); },
        error: err => this.showToast('error', err?.error?.message || err?.message || 'Error al guardar el menú.')
      });
  }
}

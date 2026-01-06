// src/app/features/config/role-management/role-management.component.ts
import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { SidebarComponent } from '../../../shared/components/sidebar/sidebar.component';

import {
  FontAwesomeModule,
  FaIconLibrary
} from '@fortawesome/angular-fontawesome';
import {
  faUserShield,
  faPen,
  faTrash,
  faPlus,
  faKey,
  faUsers,
  faMagnifyingGlass,
  faFolder,
  faFile
} from '@fortawesome/free-solid-svg-icons';

import {
  RoleService,
  Role,
  CreateRoleDto,
  UpdateRoleDto
} from '../../../core/services/role/role.service';

import { CompanyService, Company } from '../../../core/services/company/company.service';

import {
  PermissionService,
  Permission,
  CreatePermissionDto,
  UpdatePermissionDto
} from '../../../core/services/permission/permission.service';

import {
  MenuService,
  MenuItem
} from '../../../core/services/menu/menu.service';

import {
  BehaviorSubject,
  Subject,
  catchError,
  finalize,
  forkJoin,
  of,
  switchMap,
  takeUntil,
  take
} from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-role-management',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    FormsModule,
    SidebarComponent,
    FontAwesomeModule,
  ],
  templateUrl: './role-management.component.html',
  styleUrls: ['./role-management.component.scss']
})
export class RoleManagementComponent implements OnInit, OnDestroy {
  // Icons
  faUserShield = faUserShield;
  faPen = faPen;
  faTrash = faTrash;
  faPlus = faPlus;
  faKey = faKey;
  faUsers = faUsers;
  faMagnifyingGlass = faMagnifyingGlass;
  faFolder = faFolder;
  faFile = faFile;

  // Tabs y filtros
  activeTab: 'roles' | 'perms' | 'menus' = 'roles';
  filterRoles = '';
  filterPerms = '';
  filterMenus = '';

  // Data
  roles: Role[] = [];
  companies: Company[] = [];
  companiesMap: Record<number, string> = {};

  permissions: Permission[] = [];

  // Menús: árbol y plano
  menusTree: MenuItem[] = [];
  flatMenus: MenuItem[] = [];
  menusMap: Record<number, string> = {};

  // UI state
  loading = false;

  // Modal Roles
  modalOpen = false;
  editing = false;

  // Modal Permisos
  permModalOpen = false;
  permEditing = false;

  // Modal Menús (CRUD)
  menuModalOpen = false;
  menuEditing = false;

  // Forms
  form!: FormGroup;       // rol
  permForm!: FormGroup;   // permiso
  menuForm!: FormGroup;   // menú

  // teardown
  private destroy$ = new Subject<void>();

  // paginación simple
  private page$ = new BehaviorSubject<{page: number; pageSize: number}>({ page: 1, pageSize: 50 });

  constructor(
    private fb: FormBuilder,
    private roleSvc: RoleService,
    private companySvc: CompanyService,
    private permSvc: PermissionService,
    private menuSvc: MenuService,
    private authService: AuthService,
    library: FaIconLibrary
  ) {
    library.addIcons(
      faUserShield, faPen, faTrash, faPlus, faKey, faUsers, faMagnifyingGlass, faFolder, faFile
    );
  }

  ngOnInit(): void {
    // Form Rol
    this.form = this.fb.group({
      id:           [null],
      name:         ['', [Validators.required, Validators.maxLength(120)]],
      permissions:  [[], Validators.required],
      companyIds:   [[], Validators.required],
      menuIds:      [[]],
    });

    // Form Permiso
    this.permForm = this.fb.group({
      id:          [null],
      key:         ['', [Validators.required, Validators.pattern(/^[a-z0-9_.:-]+$/i)]],
      description: ['']
    });

    // Form Menú
    this.menuForm = this.fb.group({
      id:        [null],
      key:       ['', [Validators.required, Validators.maxLength(120)]],
      label:     ['', [Validators.required, Validators.maxLength(160)]],
      route:     [''],
      icon:      [''],
      parentId:  [null],              // puede ser null o id válido
      sortOrder: [null],              // número opcional
      isGroup:   [false],             // si es un agrupador sin ruta
    });

    this.loadCompanies();
    this.loadPermissions();
    this.loadMenus();

    this.page$
      .pipe(takeUntil(this.destroy$))
      .subscribe(({ page, pageSize }) => this.loadRoles(page, pageSize));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // =========================
  // LOADERS
  // =========================
  private loadCompanies() {
    // Obtener información del usuario en sesión y cargar empresas según rol
    this.authService.session$
      .pipe(
        take(1), // Solo tomar el primer valor (valor actual)
        switchMap((session) => {
          const userRoles = session?.user?.roles || [];
          
          // Verificar si el usuario es admin o administrador (case-insensitive)
          const isAdmin = userRoles.some(role => 
            role && (role.toLowerCase() === 'admin' || role.toLowerCase() === 'administrador')
          );

          // Si es admin/administrador, obtener todas las empresas
          // Si no, obtener solo las empresas del usuario en sesión (ya filtradas por backend)
          return isAdmin 
            ? this.companySvc.getAllForAdmin()
            : this.companySvc.getAll();
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (list) => {
          this.companies = list ?? [];
          this.companiesMap = this.companies.reduce((m, c) => {
            if (c.id !== undefined) m[c.id] = c.name;
            return m;
          }, {} as Record<number, string>);
        },
        error: (error) => {
          console.error('Error loading companies:', error);
          // Fallback: cargar empresas normales si hay error
          this.companySvc.getAll()
            .pipe(takeUntil(this.destroy$))
            .subscribe({
              next: (list) => {
                this.companies = list ?? [];
                this.companiesMap = this.companies.reduce((m, c) => {
                  if (c.id !== undefined) m[c.id] = c.name;
                  return m;
                }, {} as Record<number, string>);
              },
              error: () => alert('No se pudieron cargar las empresas.')
            });
        }
      });
  }

  private loadPermissions() {
    this.permSvc.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (list) => {
          this.permissions = (list ?? []).map(p => ({ ...p, key: (p.key || '').toLowerCase() }));
          this.syncRoleFormPermissions();
        },
        error: () => alert('No se pudieron cargar los permisos.')
      });
  }

  private loadMenus() {
    this.menuSvc.getTree()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tree) => {
          this.menusTree = tree ?? [];
          this.flatMenus = this.flattenMenus(this.menusTree);
          this.menusMap = this.flatMenus.reduce((m, it) => {
            m[it.id] = it.label || it.key;
            return m;
          }, {} as Record<number, string>);
        },
        error: () => alert('No se pudieron cargar los menús.')
      });
  }

  private loadRoles(page = 1, pageSize = 50) {
    this.loading = true;
    this.roleSvc.getAll(page, pageSize)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading = false)
      )
      .subscribe({
        next: (list) => this.roles = list ?? [],
        error: () => alert('No se pudieron cargar los roles.')
      });
  }

  private syncRoleFormPermissions() {
    if (!this.form) return;
    const selected: string[] = this.form.controls['permissions'].value || [];
    if (!selected.length) return;
    const valid = new Set(this.permissions.map(p => (p.key || '').toLowerCase()));
    const filtered = selected.filter(k => valid.has((k || '').toLowerCase()));
    if (filtered.length !== selected.length) {
      this.form.patchValue({ permissions: filtered }, { emitEvent: false });
    }
  }

  // =========================
  // FILTROS (getters)
  // =========================
  get filteredRoles(): Role[] {
    const t = this.filterRoles.trim().toLowerCase();
    if (!t) return this.roles;
    return this.roles.filter(r => {
      const inName = (r.name || '').toLowerCase().includes(t);
      const inPerms = (r.permissions || []).some(p => (p || '').toLowerCase().includes(t));
      const inCompanies = (r.companyIds || []).some(id => (this.companiesMap[id] || '').toLowerCase().includes(t));
      const inMenus = (r.menuIds || []).some(id => (this.menusMap[id] || '').toLowerCase().includes(t));
      return inName || inPerms || inCompanies || inMenus;
    });
  }

  get filteredPerms(): Permission[] {
    const t = this.filterPerms.trim().toLowerCase();
    if (!t) return this.permissions;
    return this.permissions.filter(p =>
      (p.key || '').toLowerCase().includes(t) ||
      (p.description || '').toLowerCase().includes(t)
    );
  }

  get filteredMenus(): MenuItem[] {
    const t = this.filterMenus.trim().toLowerCase();
    if (!t) return this.menusTree;
    // filtra por label/key/route y mantiene estructura parcial (hijos si matchea nodo o ancestro)
    const matches = (node: MenuItem): boolean =>
      (node.label || '').toLowerCase().includes(t) ||
      (node.key || '').toLowerCase().includes(t) ||
      (node.route || '').toLowerCase().includes(t);

    const filterTree = (nodes: MenuItem[]): MenuItem[] => {
      const out: MenuItem[] = [];
      for (const n of nodes) {
        const kids = n.children ? filterTree(n.children) : [];
        if (matches(n) || kids.length) {
          out.push({ ...n, children: kids });
        }
      }
      return out;
    };
    return filterTree(this.menusTree);
  }

  // =========================
  // MODAL ROL
  // =========================
  openModal(r?: Role) {
    this.editing = !!r;
    if (r) {
      this.form.setValue({
        id:           r.id,
        name:         r.name,
        permissions:  [...(r.permissions || []).map(k => (k || '').toLowerCase())],
        companyIds:   [...(r.companyIds || [])],
        menuIds:      [...(r.menuIds || [])],
      });
    } else {
      this.form.reset({
        id: null, name: '',
        permissions: [], companyIds: [], menuIds: []
      });
    }
    this.modalOpen = true;
  }

  closeModal() {
    this.modalOpen = false;
  }

  togglePermission(permKey: string) {
    const key = (permKey || '').toLowerCase();
    const control = this.form.controls['permissions'];
    const current = control.value as string[];
    control.setValue(current.includes(key) ? current.filter(p => p !== key) : [...current, key]);
  }

  toggleCompany(cid: number) {
    const control = this.form.controls['companyIds'];
    const current = control.value as number[];
    control.setValue(current.includes(cid) ? current.filter(id => id !== cid) : [...current, cid]);
  }

  toggleMenu(menuId: number) {
    const control = this.form.controls['menuIds'];
    const current = control.value as number[];
    control.setValue(current.includes(menuId) ? current.filter(id => id !== menuId) : [...current, menuId]);
  }

  save() {
    if (this.form.invalid) return;
    const v = this.form.value;

    const payloadBase = {
      name: (v.name as string).trim(),
      permissions: (v.permissions as string[]).map(k => (k || '').toLowerCase()),
      companyIds: (v.companyIds as number[]),
      menuIds: (v.menuIds as number[]),
    };

    // Crear
    if (!this.editing) {
      this.loading = true;
      this.roleSvc.create(payloadBase as CreateRoleDto)
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => { this.loading = false; this.modalOpen = false; })
        )
        .subscribe({
          next: () => this.refreshRolesPage(),
          error: (e) => alert(e?.error?.message || 'No se pudo crear el rol')
        });
      return;
    }

    // Editar: diffs sólo de permisos (menús van en replace por update)
    const roleId = v.id as number;
    const prev = this.roles.find(r => r.id === roleId);
    const prevPerms = new Set((prev?.permissions || []).map(k => (k || '').toLowerCase()));
    const newPerms  = new Set(payloadBase.permissions);
    const permsAdded   = [...newPerms].filter(k => !prevPerms.has(k));
    const permsRemoved = [...prevPerms].filter(k => !newPerms.has(k));

    this.loading = true;
    this.roleSvc.update({ id: roleId, ...payloadBase } as UpdateRoleDto)
      .pipe(
        switchMap(() => {
          const ops = [];
          if (permsAdded.length)   ops.push(this.roleSvc.assignPermissions(roleId, permsAdded));
          if (permsRemoved.length) ops.push(this.roleSvc.unassignPermissions(roleId, permsRemoved));
          return ops.length ? forkJoin(ops) : of(null);
        }),
        catchError(err => {
          alert(err?.error?.message || 'Error al guardar cambios del rol');
          return of(null);
        }),
        finalize(() => { this.loading = false; this.modalOpen = false; }),
        takeUntil(this.destroy$)
      )
      .subscribe(() => this.refreshRolesPage());
  }

  deleteRole(id: number) {
    if (!confirm('¿Eliminar este rol?')) return;
    this.loading = true;
    this.roleSvc.delete(id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading = false)
      )
      .subscribe({
        next: () => this.refreshRolesPage(),
        error: (e) => alert(e?.error?.message || 'No se pudo eliminar el rol')
      });
  }

  // =========================
  // MODAL PERMISOS
  // =========================
  abrirModalPermisos() {
    this.permEditing = false;
    this.permForm.reset({ id: null, key: '', description: '' });
    this.permModalOpen = true;
  }

  cerrarModalPermisos() {
    this.permModalOpen = false;
  }

  editarPermiso(p: Permission) {
    this.permEditing = true;
    this.permForm.setValue({
      id: p.id,
      key: (p.key || '').toLowerCase(),
      description: p.description || ''
    });
    this.permModalOpen = true;
  }

  guardarPermiso() {
    if (this.permForm.invalid) return;
    const v = this.permForm.value;
    const key = (v.key as string).trim().toLowerCase();
    const description = (v.description || '').trim();

    const op$ = this.permEditing
      ? this.permSvc.update({ id: v.id, key, description } as UpdatePermissionDto)
      : this.permSvc.create({ key, description } as CreatePermissionDto);

    op$
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.loadPermissions();
          this.permEditing = false;
          this.permForm.reset({ id: null, key: '', description: '' });
        },
        error: (e) => alert(e?.error?.message || 'No se pudo guardar el permiso')
      });
  }

  eliminarPermiso(p: Permission) {
    if (!confirm(`¿Eliminar permiso "${p.key}"?`)) return;
    this.permSvc.delete(p.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.loadPermissions();
          if (this.permForm.value.id === p.id) {
            this.permEditing = false;
            this.permForm.reset({ id: null, key: '', description: '' });
          }
        },
        error: (e) => alert(e?.error?.message || 'No se pudo eliminar el permiso (¿asignado a roles?)')
      });
  }

  // =========================
  // MODAL MENÚS (CRUD)
  // =========================
  abrirModalMenu(m?: MenuItem) {
    this.menuEditing = !!m;

    if (m) {
      this.menuForm.setValue({
        id: m.id,
        key: m.key || '',
        label: m.label || '',
        route: m.route || '',
        icon: m.icon || '',
        parentId: m.parentId ?? null,
        sortOrder: m.sortOrder ?? null,
        isGroup: !!m.isGroup,
      });
    } else {
      this.menuForm.reset({
        id: null,
        key: '',
        label: '',
        route: '',
        icon: '',
        parentId: null,
        sortOrder: null,
        isGroup: false
      });
    }

    this.menuModalOpen = true;
  }

  cerrarModalMenu() {
    this.menuModalOpen = false;
  }

  guardarMenu() {
    if (this.menuForm.invalid) return;
    const v = this.menuForm.value;

    // Evita que el padre sea el propio id
    if (this.menuEditing && v.parentId && v.parentId === v.id) {
      alert('Un menú no puede ser su propio padre.');
      return;
    }

    const dto = {
      id: v.id,
      key: (v.key as string).trim(),
      label: (v.label as string).trim(),
      route: (v.route as string).trim() || null,
      icon: (v.icon as string).trim() || null,
      parentId: (v.parentId as number) ?? null,
      sortOrder: v.sortOrder !== null && v.sortOrder !== undefined ? Number(v.sortOrder) : null,
      isGroup: !!v.isGroup
    };

    const req$ = this.menuEditing
      ? this.menuSvc.update(dto as any)
      : this.menuSvc.create(dto as any);

    req$
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.loadMenus();
          this.menuEditing = false;
          this.menuForm.reset({
            id: null, key: '', label: '', route: '', icon: '', parentId: null, sortOrder: null, isGroup: false
          });
          this.menuModalOpen = false;
        },
        error: (e) => alert(e?.error?.message || 'No se pudo guardar el menú')
      });
  }

  eliminarMenu(m: MenuItem) {
    if (!confirm(`¿Eliminar menú "${m.label || m.key}"?`)) return;
    this.menuSvc.delete(m.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => this.loadMenus(),
        error: (e) => alert(e?.error?.message || 'No se pudo eliminar el menú (¿tiene hijos asignados?)')
      });
  }

  // =========================
  // Helpers
  // =========================
  companyName = (id: number) => this.companiesMap[id] ?? `#${id}`;
  menuLabel   = (id: number) => this.menusMap[id] ?? `#${id}`;

  trackByRoleId = (_: number, r: Role) => r.id;
  trackByPermId = (_: number, p: Permission) => p.id;
  trackByMenuId = (_: number, m: MenuItem) => m.id;

  private refreshRolesPage() {
    const { page, pageSize } = this.page$.value;
    this.loadRoles(page, pageSize);
  }


  menusTooltip(ids?: number[]): string {
    return (ids ?? [])
      .slice(4)
      .map(id => this.menuLabel(id))
      .join(', ');
  }

  // Aplana el árbol de menús y concatena jerarquía al label para mejor UX
  private flattenMenus(nodes: MenuItem[], prefix: string = ''): MenuItem[] {
    const acc: MenuItem[] = [];
    for (const n of nodes) {
      const humanLabel = (n.label || n.key);
      const breadcrumb = prefix ? `${prefix} › ${humanLabel}` : humanLabel;
      acc.push({ ...n, label: breadcrumb, children: undefined });
      if (n.children?.length) {
        acc.push(...this.flattenMenus(n.children, breadcrumb));
      }
    }
    return acc;
  }
}

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
  faFile,
  faGripVertical
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

import { MenuService as SidebarMenuService } from '../../../shared/services/menu.service';

import {
  CompanyMenuService,
  CompanyMenuItem,
  CompanyMenuItemStructureDto,
  UpdateCompanyMenuStructureRequest
} from '../../../core/services/company-menu/company-menu.service';

import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';

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

/** Ítem plano para lista drag-and-drop (árbol aplanado con profundidad). */
export interface MenuFlatItem {
  node: MenuItem;
  depth: number;
  parentId: number | null;
}

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
    DragDropModule,
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
  faGripVertical = faGripVertical;

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

  // Menús: árbol y plano (catálogo global; para CRUD y dropdown padre)
  menusTree: MenuItem[] = [];
  flatMenus: MenuItem[] = [];
  menusMap: Record<number, string> = {};

  // Pestaña Menús: admin = menús globales, no admin = menús de la empresa activa
  tabMenusTree: MenuItem[] = [];
  tabMenusLoading = false;
  /** Lista plana para drag-and-drop (solo cuando no hay filtro de búsqueda). */
  menuFlatList: MenuFlatItem[] = [];
  menuStructureSaving = false;

  // Menús filtrados por empresa (modal Rol): solo los asignados a la empresa seleccionada
  roleModalMenusTree: MenuItem[] = [];
  roleModalFlatMenus: MenuItem[] = [];
  roleModalMenusLoading = false;
  roleModalMenusCompanyId: number | null = null;

  // UI state
  loading = false;
  isAdminUser = false;
  activeCompanyId: number | null = null;

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
    private companyMenuSvc: CompanyMenuService,
    private sidebarMenuSvc: SidebarMenuService,
    private authService: AuthService,
    library: FaIconLibrary
  ) {
    library.addIcons(
      faUserShield, faPen, faTrash, faPlus, faKey, faUsers, faMagnifyingGlass, faFolder, faFile, faGripVertical
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

    // Al cambiar empresas en el modal de rol, cargar menús de la primera empresa seleccionada
    this.form?.controls['companyIds']?.valueChanges
      ?.pipe(takeUntil(this.destroy$))
      ?.subscribe((companyIds: number[]) => {
        this.loadRoleModalMenusByCompany(companyIds);
      });

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
    this.authService.session$
      .pipe(
        take(1),
        switchMap((session) => {
          const userRoles = session?.user?.roles || [];
          this.isAdminUser = userRoles.some(role =>
            role && (role.toLowerCase() === 'admin' || role.toLowerCase() === 'administrador')
          );
          this.activeCompanyId = session?.activeCompanyId ?? null;

          return this.isAdminUser
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
          if (this.activeTab === 'menus' && this.isAdminUser) {
            this.tabMenusTree = this.menusTree;
            this.menuFlatList = this.buildMenuFlatList(this.tabMenusTree);
          }
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
    const source = this.tabMenusTree;
    if (!t) return source;
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
    return filterTree(source);
  }

  /** Carga datos de la pestaña Menús: admin = árbol global, no admin = menús de la empresa activa. */
  loadMenusForTab(): void {
    if (this.isAdminUser) {
      if (this.menusTree.length > 0) {
        this.tabMenusTree = this.menusTree;
        this.menuFlatList = this.buildMenuFlatList(this.tabMenusTree);
        this.tabMenusLoading = false;
        return;
      }
      this.tabMenusLoading = true;
      this.menuSvc
        .getTree()
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => (this.tabMenusLoading = false))
        )
        .subscribe({
        next: (tree) => {
          this.menusTree = tree ?? [];
          this.flatMenus = this.flattenMenus(this.menusTree);
          this.menusMap = this.flatMenus.reduce((m, it) => {
            m[it.id] = it.label || it.key;
            return m;
          }, {} as Record<number, string>);
          this.tabMenusTree = this.menusTree;
          this.menuFlatList = this.buildMenuFlatList(this.tabMenusTree);
        },
        error: () => {
          this.tabMenusTree = [];
          this.menuFlatList = [];
        }
      });
      return;
    }
    if (this.activeCompanyId == null) {
      this.tabMenusTree = [];
      this.menuFlatList = [];
      this.tabMenusLoading = false;
      return;
    }
    this.tabMenusLoading = true;
    this.companyMenuSvc
      .getMenusForCompany(this.activeCompanyId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.tabMenusLoading = false))
      )
      .subscribe({
        next: (tree) => {
          this.tabMenusTree = (tree ?? []).map((n) => this.companyMenuToMenuItem(n));
          this.menuFlatList = this.buildMenuFlatList(this.tabMenusTree);
        },
        error: () => {
          this.tabMenusTree = [];
          this.menuFlatList = [];
        }
      });
  }

  /** Construye lista plana desde el árbol (para drag-and-drop). */
  buildMenuFlatList(nodes: MenuItem[], depth = 0, parentId: number | null = null): MenuFlatItem[] {
    const out: MenuFlatItem[] = [];
    for (const node of nodes) {
      out.push({ node: { ...node }, depth, parentId });
      if (node.children?.length) {
        out.push(...this.buildMenuFlatList(node.children, depth + 1, node.id));
      }
    }
    return out;
  }

  /** Reconstruye árbol desde lista plana (preservando orden y jerarquía por depth). */
  buildTreeFromFlatList(flat: MenuFlatItem[]): MenuItem[] {
    const roots: MenuItem[] = [];
    const lastAtDepth: (MenuItem | null)[] = [];
    for (const { node, depth, parentId } of flat) {
      const clone: MenuItem = { ...node, children: [] };
      if (depth === 0) {
        roots.push(clone);
        lastAtDepth[0] = clone;
      } else {
        const parent = lastAtDepth[depth - 1] ?? null;
        if (parent) {
          if (!parent.children) parent.children = [];
          parent.children.push(clone);
        }
        lastAtDepth[depth] = clone;
      }
    }
    return roots;
  }

  /** Flatten tree to structure DTOs (menuId, sortOrder, parentMenuId) for company menu API. */
  private treeToStructureItems(nodes: MenuItem[], parentMenuId: number | null, startOrder: number): { order: number; items: CompanyMenuItemStructureDto[] } {
    let order = startOrder;
    const items: CompanyMenuItemStructureDto[] = [];
    for (const n of nodes) {
      items.push({
        menuId: n.id,
        sortOrder: order,
        parentMenuId,
        isEnabled: true
      });
      order += 1;
      if (n.children?.length) {
        const child = this.treeToStructureItems(n.children, n.id, order);
        items.push(...child.items);
        order = child.order;
      }
    }
    return { order, items };
  }

  onMenuDrop(event: CdkDragDrop<MenuFlatItem[]>) {
    if (event.previousIndex === event.currentIndex) return;
    const list = [...this.menuFlatList];
    moveItemInArray(list, event.previousIndex, event.currentIndex);
    this.menuFlatList = list;
    this.tabMenusTree = this.buildTreeFromFlatList(list);
    this.persistMenuOrder(event.currentIndex);
  }

  /** Parent y orden de un ítem en la lista plana (por depth). */
  private getParentAndOrder(flat: MenuFlatItem[], index: number): { parentId: number | null; order: number } {
    const item = flat[index];
    let parentId: number | null = null;
    if (item.depth > 0) {
      for (let i = index - 1; i >= 0; i--) {
        if (flat[i].depth === item.depth - 1) {
          parentId = flat[i].node.id;
          break;
        }
      }
    }
    let order = 0;
    for (let i = 0; i < index; i++) {
      if (flat[i].depth !== item.depth) continue;
      let p: number | null = null;
      if (flat[i].depth > 0) {
        for (let j = i - 1; j >= 0; j--) {
          if (flat[j].depth === flat[i].depth - 1) {
            p = flat[j].node.id;
            break;
          }
        }
      }
      if (p === parentId) order++;
    }
    return { parentId, order };
  }

  /** Persiste orden/jerarquía: admin = update un menú, no admin = estructura empresa. */
  persistMenuOrder(movedIndex?: number) {
    if (this.isAdminUser) {
      const flat = this.menuFlatList;
      const idx = movedIndex ?? 0;
      const { parentId, order } = this.getParentAndOrder(flat, idx);
      const node = flat[idx].node;
      this.menuStructureSaving = true;
      this.menuSvc
        .update({
          id: node.id,
          key: node.key,
          label: node.label,
          icon: node.icon,
          route: node.route,
          parentId: parentId ?? undefined,
          sortOrder: order,
          isGroup: !!node.children?.length
        })
        .pipe(takeUntil(this.destroy$), finalize(() => (this.menuStructureSaving = false)))
        .subscribe({
          next: () => {
            this.menusTree = this.tabMenusTree;
            this.flatMenus = this.flattenMenus(this.menusTree);
            this.menusMap = this.flatMenus.reduce((m, it) => { m[it.id] = it.label || it.key; return m; }, {} as Record<number, string>);
          },
          error: () => alert('Error al guardar el orden del menú.')
        });
      return;
    }
    if (this.activeCompanyId == null) return;
    const { items } = this.treeToStructureItems(this.tabMenusTree, null, 0);
    const request: UpdateCompanyMenuStructureRequest = { items };
    this.menuStructureSaving = true;
    this.companyMenuSvc
      .updateCompanyMenuStructure(this.activeCompanyId, request)
      .pipe(takeUntil(this.destroy$), finalize(() => (this.menuStructureSaving = false)))
      .subscribe({
        next: () => {},
        error: () => alert('Error al guardar el orden de menús de la empresa.')
      });
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
      // valueChanges de companyIds cargará los menús de la primera empresa
    } else {
      // Crear: si no es admin, asignar por defecto solo la empresa activa (storage)
      const defaultCompanyIds =
        !this.isAdminUser && this.activeCompanyId != null ? [this.activeCompanyId] : [];
      this.form.reset({
        id: null,
        name: '',
        permissions: [],
        companyIds: defaultCompanyIds,
        menuIds: []
      });
      this.roleModalFlatMenus = [];
      this.roleModalMenusTree = [];
      this.roleModalMenusCompanyId = null;
      if (defaultCompanyIds.length > 0) {
        this.loadRoleModalMenusByCompany(defaultCompanyIds);
      }
    }
    this.modalOpen = true;
  }

  /** Convierte árbol CompanyMenuItem a MenuItem (id, key, label, etc.) */
  private companyMenuToMenuItem(node: CompanyMenuItem): MenuItem {
    const children = (node.children?.length)
      ? node.children.map(ch => this.companyMenuToMenuItem(ch))
      : undefined;
    return {
      id: node.id,
      key: node.label || String(node.id),
      label: node.label,
      icon: node.icon ?? undefined,
      route: node.route ?? undefined,
      parentId: undefined,
      sortOrder: node.order,
      isGroup: !!(node.children?.length),
      children,
    };
  }

  /** Carga en el modal de rol los menús asignados a la primera empresa seleccionada. */
  loadRoleModalMenusByCompany(companyIds: number[]) {
    const companyId = companyIds?.length ? companyIds[0] : null;
    if (!companyId) {
      this.roleModalMenusTree = [];
      this.roleModalFlatMenus = [];
      this.roleModalMenusCompanyId = null;
      return;
    }
    if (this.roleModalMenusCompanyId === companyId) return; // ya cargados
    this.roleModalMenusLoading = true;
    this.roleModalMenusCompanyId = companyId;
    this.companyMenuSvc.getMenusForCompany(companyId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.roleModalMenusLoading = false)
      )
      .subscribe({
        next: (tree: CompanyMenuItem[]) => {
          this.roleModalMenusTree = (tree ?? []).map(n => this.companyMenuToMenuItem(n));
          this.roleModalFlatMenus = this.flattenMenus(this.roleModalMenusTree);
        },
        error: () => {
          this.roleModalMenusTree = [];
          this.roleModalFlatMenus = [];
          this.roleModalMenusCompanyId = null;
        }
      });
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
      .subscribe(() => {
        this.refreshRolesPage();
        // Refrescar menú del sidebar para que el usuario vea los cambios de menú sin cerrar sesión
        this.sidebarMenuSvc.preloadMyMenu(this.activeCompanyId ?? undefined).pipe(take(1)).subscribe();
      });
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
  trackByMenuFlatId = (_: number, item: MenuFlatItem) => item.node.id;

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

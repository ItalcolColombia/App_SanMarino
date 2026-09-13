// src/app/features/config/permission-modules/permission-modules.component.ts
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, forkJoin, map, of } from 'rxjs';

import { ToastService } from '../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { CompanyService, Company } from '../../../core/services/company/company.service';
import { PermissionService, Permission } from '../../../core/services/permission/permission.service';
import {
  PermissionModuleService,
  PermissionModule,
  CompanyPermissionModuleItem
} from '../../../core/services/permission-module/permission-module.service';

import {
  construirMatrizModulos,
  modulosTrasCambiar,
  type CeldaMatriz,
  type FilaMatrizModulo
} from './funciones/matriz-modulos-empresa.funcion';
import {
  describirCambioPermisos,
  filtrarPermisosCatalogo,
  idsDePermisosDelModulo,
  otrosModulosPorPermiso
} from './funciones/catalogo-modulos.funcion';

type TabModulos = 'empresas' | 'catalogo';

/**
 * «Módulos y permisos»: cada empresa se configura prendiendo módulos (Postura, Pollo Engorde…) en vez
 * de permisos sueltos. Dos tabs: la matriz módulo × empresa y el catálogo (qué permisos agrupa cada
 * módulo). Todo cambio lo materializa el backend en `company_permissions`.
 *
 * Plan: `fase_de_desarrollo/modulos_permisos_por_empresa_plan.md`.
 */
@Component({
  selector: 'app-permission-modules',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './permission-modules.component.html',
  styleUrls: ['./permission-modules.component.scss']
})
export class PermissionModulesComponent implements OnInit {
  private readonly moduleSvc = inject(PermissionModuleService);
  private readonly companySvc = inject(CompanyService);
  private readonly permissionSvc = inject(PermissionService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  tab: TabModulos = 'empresas';
  cargando = false;

  // ── Módulos por empresa ─────────────────────────────────────────────────────
  empresas: Company[] = [];
  modulos: PermissionModule[] = [];
  private estadoPorEmpresa = new Map<number, CompanyPermissionModuleItem[]>();
  /** Matriz ya armada (campo, no getter: referencia estable para el CD). */
  matriz: FilaMatrizModulo[] = [];
  cargandoMatriz = false;
  /** Celda que se está guardando (`companyId:moduleId`); mientras haya una, las demás se bloquean. */
  guardandoCelda: string | null = null;

  // ── Catálogo ────────────────────────────────────────────────────────────────
  permisos: Permission[] = [];
  permisosFiltrados: Permission[] = [];
  filtroPermisos = '';
  moduloSeleccionado: PermissionModule | null = null;
  creandoModulo = false;
  formModulo = { key: '', nombre: '', descripcion: '', orden: 0 };
  seleccionPermisos = new Set<number>();
  otrosModulos = new Map<string, string[]>();
  guardandoModulo = false;
  guardandoClasificacion = false;

  ngOnInit(): void {
    this.cargarTodo();
  }

  setTab(tab: TabModulos): void {
    this.tab = tab;
  }

  nombreEmpresa(companyId: number): string {
    return this.empresas.find(e => e.id === companyId)?.name ?? `#${companyId}`;
  }

  // ═══════════════════════════════════════════════════════════════════════════
  // Carga
  // ═══════════════════════════════════════════════════════════════════════════

  private cargarTodo(): void {
    this.cargando = true;
    forkJoin({
      modulos: this.moduleSvc.getAll(),
      empresas: this.companySvc.getAll(),
      permisos: this.permissionSvc.getAll()
    })
      .pipe(finalize(() => (this.cargando = false)))
      .subscribe({
        next: ({ modulos, empresas, permisos }) => {
          this.modulos = modulos ?? [];
          this.empresas = (empresas ?? [])
            .filter(e => e.id != null)
            .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''));
          this.permisos = [...(permisos ?? [])].sort((a, b) => (a.key || '').localeCompare(b.key || ''));
          this.aplicarFiltroPermisos();
          this.reseleccionarModulo();
          this.cargarEstadoEmpresas();
        },
        error: () => this.toast.error('No se pudieron cargar los módulos de permisos.')
      });
  }

  /** Refresca sólo el catálogo de módulos (conteos de empresas y permisos). */
  private recargarModulos(): void {
    this.moduleSvc.getAll().subscribe({
      next: modulos => {
        this.modulos = modulos ?? [];
        this.reseleccionarModulo();
        this.reconstruirMatriz();
      },
      error: () => this.toast.error('No se pudo refrescar el catálogo de módulos.')
    });
  }

  private cargarEstadoEmpresas(): void {
    const ids = this.empresas.map(e => e.id as number);
    if (!ids.length) {
      this.estadoPorEmpresa = new Map();
      this.reconstruirMatriz();
      return;
    }

    this.cargandoMatriz = true;
    forkJoin(
      ids.map(id =>
        this.moduleSvc.getForCompany(id).pipe(
          map(items => ({ id, items: items ?? [] })),
          catchError(() => of({ id, items: null as CompanyPermissionModuleItem[] | null }))
        )
      )
    )
      .pipe(finalize(() => (this.cargandoMatriz = false)))
      .subscribe(resultados => {
        const estado = new Map<number, CompanyPermissionModuleItem[]>();
        let fallas = 0;
        for (const r of resultados) {
          if (r.items) estado.set(r.id, r.items);
          else fallas++;
        }
        this.estadoPorEmpresa = estado;
        if (fallas) this.toast.error(`No se pudo cargar el estado de ${fallas} empresa(s); sus celdas quedan bloqueadas.`);
        this.reconstruirMatriz();
      });
  }

  private recargarEmpresa(companyId: number): void {
    this.moduleSvc.getForCompany(companyId).subscribe({
      next: items => {
        const estado = new Map(this.estadoPorEmpresa);
        estado.set(companyId, items ?? []);
        this.estadoPorEmpresa = estado;
        this.reconstruirMatriz();
      },
      error: () => this.toast.error('No se pudo refrescar la empresa.')
    });
  }

  private reconstruirMatriz(): void {
    this.matriz = construirMatrizModulos(
      this.modulos,
      this.empresas.map(e => e.id as number),
      this.estadoPorEmpresa
    );
  }

  // ═══════════════════════════════════════════════════════════════════════════
  // Módulos por empresa
  // ═══════════════════════════════════════════════════════════════════════════

  estaGuardando(celda: CeldaMatriz): boolean {
    return this.guardandoCelda === `${celda.companyId}:${celda.moduleId}`;
  }

  async toggleCelda(fila: FilaMatrizModulo, celda: CeldaMatriz): Promise<void> {
    if (!celda.cargada || this.guardandoCelda) return;

    const prender = !celda.prendido;
    const empresa = this.nombreEmpresa(celda.companyId);
    const ok = await this.confirmDialog.ask({
      title: prender ? 'Prender módulo' : 'Apagar módulo',
      message: prender
        ? `Se habilitan en ${empresa} los ${celda.total} permisos de «${fila.modulo.nombre}». Los roles no cambian: ` +
          'cada rol sigue necesitando que se le asigne el permiso.'
        : `Se apagan en ${empresa} los permisos de «${fila.modulo.nombre}» que ningún otro módulo prendido cubra ` +
          `(hoy tiene ${celda.habilitados} de ${celda.total} prendidos). Los roles que los tengan asignados los ` +
          'conservan, pero sin efecto: no se ofrecen ni llegan a la sesión.',
      type: prender ? 'info' : 'warning',
      confirmText: prender ? 'Prender' : 'Apagar'
    });
    if (!ok) return;

    const moduleIds = modulosTrasCambiar(this.estadoPorEmpresa.get(celda.companyId), celda.moduleId, prender);
    if (moduleIds === null) {
      this.toast.error('El estado de la empresa no está cargado; recargá la pantalla.');
      return;
    }

    this.guardandoCelda = `${celda.companyId}:${celda.moduleId}`;
    this.moduleSvc.setForCompany(celda.companyId, moduleIds)
      .pipe(finalize(() => (this.guardandoCelda = null)))
      .subscribe({
        next: cambio => {
          this.toast.success(describirCambioPermisos(cambio));
          this.recargarEmpresa(celda.companyId);
          this.recargarModulos();
        },
        error: err => this.toast.error(err?.error?.message || 'No se pudo guardar el módulo de la empresa.')
      });
  }

  // ═══════════════════════════════════════════════════════════════════════════
  // Catálogo
  // ═══════════════════════════════════════════════════════════════════════════

  aplicarFiltroPermisos(): void {
    this.permisosFiltrados = filtrarPermisosCatalogo(this.permisos, this.filtroPermisos);
  }

  nuevoModulo(): void {
    this.moduloSeleccionado = null;
    this.creandoModulo = true;
    this.formModulo = { key: '', nombre: '', descripcion: '', orden: (this.modulos.length + 1) * 10 };
    this.seleccionPermisos = new Set();
    this.otrosModulos = new Map();
  }

  seleccionarModulo(m: PermissionModule): void {
    this.creandoModulo = false;
    this.moduloSeleccionado = m;
    this.formModulo = { key: m.key, nombre: m.nombre, descripcion: m.descripcion ?? '', orden: m.orden };
    this.seleccionPermisos = idsDePermisosDelModulo(this.permisos, m);
    this.otrosModulos = otrosModulosPorPermiso(this.modulos, m.id);
  }

  /** Tras recargar el catálogo, mantiene el módulo abierto con sus datos nuevos. */
  private reseleccionarModulo(): void {
    if (!this.moduloSeleccionado) return;
    const actualizado = this.modulos.find(m => m.id === this.moduloSeleccionado!.id);
    if (actualizado) this.seleccionarModulo(actualizado);
    else this.moduloSeleccionado = null;
  }

  otrosModulosDe(p: Permission): string[] | undefined {
    return this.otrosModulos.get((p.key || '').toLowerCase());
  }

  togglePermisoModulo(p: Permission): void {
    const seleccion = new Set(this.seleccionPermisos);
    if (seleccion.has(p.id)) seleccion.delete(p.id);
    else seleccion.add(p.id);
    this.seleccionPermisos = seleccion;
  }

  guardarDatosModulo(): void {
    const nombre = this.formModulo.nombre.trim();
    if (!nombre) {
      this.toast.warning('El nombre del módulo es obligatorio.');
      return;
    }

    const datos = {
      nombre,
      descripcion: this.formModulo.descripcion.trim() || null,
      orden: Number(this.formModulo.orden) || 0
    };

    this.guardandoModulo = true;
    const peticion = this.creandoModulo
      ? this.moduleSvc.create({ key: this.formModulo.key.trim().toLowerCase(), ...datos })
      : this.moduleSvc.update(this.moduloSeleccionado!.id, datos);

    peticion.pipe(finalize(() => (this.guardandoModulo = false))).subscribe({
      next: guardado => {
        this.toast.success(this.creandoModulo ? 'Módulo creado.' : 'Datos del módulo guardados.');
        this.creandoModulo = false;
        this.moduloSeleccionado = guardado;
        this.recargarModulos();
      },
      error: err => this.toast.error(err?.error?.message || 'No se pudo guardar el módulo.')
    });
  }

  async guardarClasificacion(): Promise<void> {
    const modulo = this.moduloSeleccionado;
    if (!modulo) return;

    const ok = await this.confirmDialog.ask({
      title: 'Guardar permisos del módulo',
      message:
        `Se recalculan los permisos de las empresas: lo agregado a «${modulo.nombre}» se prende donde el módulo ` +
        'esté prendido, y lo quitado se apaga en las empresas donde ningún otro módulo prendido lo cubra.',
      type: 'warning',
      confirmText: 'Guardar'
    });
    if (!ok) return;

    this.guardandoClasificacion = true;
    this.moduleSvc.setPermissions(modulo.id, [...this.seleccionPermisos])
      .pipe(finalize(() => (this.guardandoClasificacion = false)))
      .subscribe({
        next: cambio => {
          this.toast.success(describirCambioPermisos(cambio));
          this.recargarModulos();
          this.cargarEstadoEmpresas();
        },
        error: err => this.toast.error(err?.error?.message || 'No se pudieron guardar los permisos del módulo.')
      });
  }

  async eliminarModulo(): Promise<void> {
    const modulo = this.moduloSeleccionado;
    if (!modulo || modulo.empresasConModulo > 0) return;

    const ok = await this.confirmDialog.ask({
      title: 'Eliminar módulo',
      message: `¿Eliminar «${modulo.nombre}»? Sus permisos quedan sin clasificar (no se apagan en ninguna empresa).`,
      type: 'error',
      confirmText: 'Eliminar'
    });
    if (!ok) return;

    this.moduleSvc.remove(modulo.id).subscribe({
      next: () => {
        this.toast.success('Módulo eliminado.');
        this.moduloSeleccionado = null;
        this.recargarModulos();
        this.cargarEstadoEmpresas();
      },
      error: err => this.toast.error(err?.error?.message || 'No se pudo eliminar el módulo.')
    });
  }
}

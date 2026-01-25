// src/app/app.config.ts
import { ApplicationConfig, importProvidersFrom } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './core/auth/auth.interceptor';
import { ReactiveFormsModule } from '@angular/forms';

// 👇 Mantén solo los componentes que realmente se usan por referencia directa en rutas.
// Login se usa de forma no-lazy (ok), Dashboard se cargará en lazy (NO lo importes aquí).
import { LoginComponent } from './features/auth/login/login.component';
import { PasswordRecoveryComponent } from './features/auth/password-recovery/password-recovery.component';
import { ProfileComponent } from './features/profile/profile.component';
import { HomeComponent } from './features/home/home.component';

// Rutas “config” que usas con component (no-lazy)
import { ConfigComponent }            from './features/config/config.component';
import { MasterListsComponent }       from './features/config/master-lists/master-lists.component';
import { ListDetailComponent }        from './features/config/master-lists/list-detail/list-detail.component';
import { CompanyManagementComponent } from './features/config/company-management/company-management.component';
import { RoleManagementComponent }    from './features/config/role-management/role-management.component';
import { UserManagementComponent }    from './features/config/user-management/user-management.component';
import { authGuard } from './core/auth/auth.guard';

// Geografía
import { CountryListComponent }     from './features/config/geography/country-list/country-list.component';
import { CountryDetailComponent }   from './features/config/geography/country-detail/country-detail.component';
import { StateListComponent }       from './features/config/geography/state-list/state-list.component';
import { StateDetailComponent }     from './features/config/geography/state-detail/state-detail.component';
import { DepartmentListComponent }  from './features/config/geography/department-list/department-list.component';
import { DepartmentDetailComponent }from './features/config/geography/department-detail/department-detail.component';
import { CityListComponent }        from './features/config/geography/city-list/city-list.component';
import { CityDetailComponent }      from './features/config/geography/city-detail/city-detail.component';

// Granjas / Núcleos / Galpones / Lotes
import { FarmListComponent }   from './features/farm/components/farm-list/farm-list.component';
import { FarmFormComponent }   from './features/farm/components/farm-form/farm-form.component';
import { NucleoListComponent } from './features/nucleo/components/nucleo-list/nucleo-list.component';
import { NucleoFormComponent } from './features/nucleo/components/nucleo-form/nucleo-form.component';
import { GalponListComponent } from './features/galpon/components/galpon-list/galpon-list.component';
import { GalponFormComponent } from './features/galpon/components/galpon-form/galpon-form.component';
import { LoteListComponent }   from './features/lote/components/lote-list/lote-list.component';

// Config: Guía genética (CRUD sobre ProduccionAvicolaRaw)
import { GuiaGeneticaListComponent } from './features/config/guia-genetica-admin/guia-genetica-list/guia-genetica-list.component';
import { GuiaGeneticaFormComponent } from './features/config/guia-genetica-admin/guia-genetica-form/guia-genetica-form.component';
import { GuiaGeneticaDetailComponent } from './features/config/guia-genetica-admin/guia-genetica-detail/guia-genetica-detail.component';

export const appConfig: ApplicationConfig = {
  providers: [
    importProvidersFrom(ReactiveFormsModule),
    provideHttpClient(withInterceptors([authInterceptor])),

    provideRouter([
      { path: '', redirectTo: 'home', pathMatch: 'full' },

      // Público
      { path: 'login', component: LoginComponent },
      { path: 'password-recovery', component: PasswordRecoveryComponent },

      // Protegido
      { path: 'home', component: HomeComponent, canActivate: [authGuard] },
      { path: 'profile', component: ProfileComponent, canActivate: [authGuard] },
      {
        path: 'dashboard',
        canActivate: [authGuard],
        // 👇 LAZY LOAD del componente standalone
        loadComponent: () =>
          import('./features/dashboard/dashboard.component')
            .then(m => m.DashboardComponent)
      },


      {
        path: 'daily-log',
        canActivate: [authGuard],
        children: [
          { path: '', redirectTo: 'seguimiento', pathMatch: 'full' },
          {
            path: 'seguimiento',
            loadChildren: () =>
              import('./features/lote-levante/seguimiento-lote-levante.module')
                .then(m => m.SeguimientoLoteLevanteModule)
          },
          {
            path: 'produccion',
            loadChildren: () =>
              import('./features/lote-produccion/lote-produccion.module')
                .then(m => m.LoteProduccionModule)
          },
          {
            path: 'reproductora',
            loadChildren: () =>
              import('./features/lote-reproductora/lote-reproductora.module')
                .then(m => m.LoteReproductoraModule)
          },
            {
            path: 'seguimiento-diario-lote-reproductora',
            loadChildren: () =>
              import('./features/seguimiento-diario-lote-reproductora/seguimiento-diario-lote-reproductora.module')
                .then(m => m.SeguimientoDiarioLoteReproductoraModule)
          }
        ]
      },

      {
        path: 'config',
        component: ConfigComponent,
        canActivate: [authGuard],
        children: [
          { path: 'master-lists',     component: MasterListsComponent },
          { path: 'master-lists/new', component: ListDetailComponent },
          { path: 'master-lists/:id', component: ListDetailComponent },

          { path: 'companies',       component: CompanyManagementComponent },
          { path: 'role-management', component: RoleManagementComponent },
          { path: 'users',           component: UserManagementComponent },

          // geografía
          { path: 'countries',        component: CountryListComponent },
          { path: 'countries/new',    component: CountryDetailComponent },
          { path: 'countries/:id',    component: CountryDetailComponent },
          { path: 'states',           component: StateListComponent },
          { path: 'states/new',       component: StateDetailComponent },
          { path: 'states/:id',       component: StateDetailComponent },
          { path: 'departments',      component: DepartmentListComponent },
          { path: 'departments/new',  component: DepartmentDetailComponent },
          { path: 'departments/:id',  component: DepartmentDetailComponent },
          { path: 'cities',           component: CityListComponent },
          { path: 'cities/new',       component: CityDetailComponent },
          { path: 'cities/:id',       component: CityDetailComponent },

          // CRUD Granjas
          {
            path: 'farm-management',
            loadComponent: () =>
              import('./features/farm/pages/farm-management/farm-management.component')
                .then(m => m.FarmManagementComponent)
          },
          { path: 'farms-list',          component: FarmListComponent },
          { path: 'farms-list/new',      component: FarmFormComponent },
          { path: 'farms-list/:id/edit', component: FarmFormComponent },

          // Núcleos
          { path: 'nucleos',           component: NucleoListComponent },
          { path: 'nucleos/new',       component: NucleoFormComponent },
          { path: 'nucleos/:nucleoId', component: NucleoFormComponent },

          // Galpones
          { path: 'galpones',           component: GalponListComponent },
          { path: 'galpones/new',       component: GalponFormComponent },
          { path: 'galpones/:galponId', component: GalponFormComponent },

          {
            path: 'lote-management',
            loadComponent: () =>
              import('./features/lote/page/lote-management/lote-management.componet')
                .then(m => m.LoteManagementComponent)
          },
          // Lotes
          { path: 'lotes', component: LoteListComponent },

          // Guía genética (produccion_avicola_raw)
          { path: 'guia-genetica', component: GuiaGeneticaListComponent },
          { path: 'guia-genetica/new', component: GuiaGeneticaFormComponent },
          { path: 'guia-genetica/:id', component: GuiaGeneticaDetailComponent },
          { path: 'guia-genetica/:id/edit', component: GuiaGeneticaFormComponent },

          // Catálogo de Alimentos (lazy)
          {
            path: 'catalogo-alimentos',
            loadChildren: () =>
              import('./features/catalogo-alimentos/catalogo-alimentos.module')
                .then(m => m.CatalogoAlimentosModule)
          },

          // Inventario (nuevo, lazy)
          {
            path: 'inventario',
            loadChildren: () =>
              import('./features/inventario/inventario.module')
                .then(m => m.InventarioModule)
          },

          // app.routes.ts
        {
          path: 'inventario-management',
          loadComponent: () =>
            import('./features/inventario/components/inventario-tabs/inventario-tabs.component')
              .then(m => m.InventarioTabsComponent)
        },

        // app.routes.ts
        {
          path: 'inventario/catalogo',
          loadComponent: () =>
            import('./features/catalogo-alimentos/catalogo-alimentos.module')
              .then(m => m.CatalogoAlimentosModule)
        },

        // Módulo de DB Studio (lazy)
         {
          path: 'db-studio',
          loadChildren: () =>
            import('./features/db-studio/db-studio.module')
              .then(m => m.DbStudioModule)
        },

          { path: '', redirectTo: 'farms-list', pathMatch: 'full' }
        ]
      },

      // Módulo de Traslados de Aves (lazy)
      {
        path: 'reportes-tecnicos',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/reportes-tecnicos/reportes-tecnicos.module')
            .then(m => m.ReportesTecnicosModule)
      },
      {
        path: 'reporte-contable',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/reporte-contable/reporte-contable.module')
            .then(m => m.ReporteContableModule)
      },
      {
        path: 'reporte-tecnico-administrativo',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/reporte-tecnico-administrativo/reporte-tecnico-administrativo.module')
            .then(m => m.ReporteTecnicoAdministrativoModule)
      },
      {
        path: 'reporte-tecnico-produccion',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/reporte-tecnico-produccion/reporte-tecnico-produccion.module')
            .then(m => m.ReporteTecnicoProduccionModule)
      },
      {
        path: 'traslados-aves',
        canActivate: [authGuard],
        children: [
          {
            path: '',
            redirectTo: 'dashboard',
            pathMatch: 'full'
          },
          {
            path: 'dashboard',
            loadComponent: () => import('./features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component')
              .then(m => m.InventarioDashboardComponent),
            title: 'Inventario de Aves - Dashboard'
          },
          {
            path: 'traslados',
            loadComponent: () => import('./features/traslados-aves/pages/traslado-form/traslado-form.component')
              .then(m => m.TrasladoFormComponent),
            title: 'Traslado de Aves'
          },
          {
            path: 'movimientos',
            loadComponent: () => import('./features/traslados-aves/pages/movimientos-list/movimientos-list.component')
              .then(m => m.MovimientosListComponent),
            title: 'Movimientos de Aves'
          },
          {
            path: 'historial',
            loadComponent: () => import('./features/traslados-aves/pages/historial-trazabilidad/historial-trazabilidad.component')
              .then(m => m.HistorialTrazabilidadComponent),
            title: 'Historial y Trazabilidad'
          },
          {
            path: 'historial/:loteId',
            loadComponent: () => import('./features/traslados-aves/pages/historial-trazabilidad/historial-trazabilidad.component')
              .then(m => m.HistorialTrazabilidadComponent),
            title: 'Trazabilidad de Lote'
          },
          {
            path: 'nuevo',
            loadComponent: () => import('./features/traslados-aves/pages/traslado-aves-huevos/traslado-aves-huevos.component')
              .then(m => m.TrasladoAvesHuevosComponent),
            title: 'Nuevo Traslado'
          }
        ]
      },

      { path: '**', redirectTo: 'login' }
    ])
  ]
};

// src/app/app.config.ts
import { ApplicationConfig, EnvironmentInjector, importProvidersFrom, inject, isDevMode, provideAppInitializer, runInInjectionContext } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { provideServiceWorker } from '@angular/service-worker';
import { authInterceptor } from './core/auth/auth.interceptor';
import { TRABAJO_PENDIENTE_OFFLINE, type ProveedorTrabajoPendiente } from './core/auth/session-timeout.service';
import { offlineCacheInterceptor } from './shared/offline/offline-cache.interceptor';
import { OutboxService } from './shared/offline/outbox.service';
import { ReactiveFormsModule } from '@angular/forms';

// 👇 Acá SOLO va lo que tiene que estar en el bundle inicial.
//
// Todo lo que un `component:` alcanza viaja en el arranque, para todos, siempre. Medido el
// 17-ago-2026 sobre `main.js` (1.671,9 kB): **840 kB eran pantallas de administración y CRUD**
// —config 310,6 kB · lote 157,4 · farm 84,6 · galpon 72,1 · nucleo 55,7 · clientes 50,3, más
// lote-levante/silos/implementacion/tickets/vacunacion arrastrados por ellas—. Todas pasaron a
// `loadComponent` más abajo; el aviso que ya estaba escrito para Empresas y Roles vale para todas:
// importarlas acá las devuelve al bundle inicial, que es justo lo que hacía fallar el build por
// presupuesto.
//
// Se quedan eager, a propósito: **login** (es la primera pantalla: hacerla lazy agrega un viaje
// antes de poder escribir la contraseña, y en una tablet con mala red se nota) y **home** (el
// aterrizaje inmediato del login).
import { LoginComponent } from './features/auth/login/login.component';
import { PasswordRecoveryComponent } from './features/auth/password-recovery/password-recovery.component';
import { HomeComponent } from './features/home/home.component';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/auth/permission.guard';

export const appConfig: ApplicationConfig = {
  providers: [
    importProvidersFrom(ReactiveFormsModule),
    // El orden importa: `authInterceptor` va PRIMERO para que la petición salga con sus headers
    // (token, empresa activa, SECRET_UP) ya puestos. `offlineCacheInterceptor` envuelve por dentro
    // y solo actúa sobre la respuesta —guardándola— o sobre el fallo de red —sirviendo lo guardado—.
    provideHttpClient(withXhr(), withInterceptors([authInterceptor, offlineCacheInterceptor])),

    // El seam que F0.B dejó preparado, ahora con implementación real (F3). La política de sesión lo
    // consulta antes de cerrar: cerrar sesión dispara una purga, y purgar con capturas pendientes
    // destruye trabajo de campo que el servidor nunca vio.
    {
      provide: TRABAJO_PENDIENTE_OFFLINE,
      useFactory: (): ProveedorTrabajoPendiente => {
        const outbox = inject(OutboxService);
        // Se refrescan los contadores al arrancar: la cola sobrevive al cierre de la app, así que
        // en el primer load puede haber pendientes de la jornada anterior.
        void outbox.refrescarContadores();
        return { operacionesPendientes: () => outbox.pendientes() + outbox.rechazadas() };
      }
    },

    // Instancia el sync al arrancar. Sin esto nadie lo inyecta y su `effect` de reconexión nunca
    // corre: la cola se llenaría sin que nada la vacíe.
    //
    // Se carga con `import()` diferido a propósito: el envío no hace falta en el primer frame, y
    // traerlo en el bundle inicial empujaba el presupuesto por encima del techo de error.
    provideAppInitializer(() => {
      const injector = inject(EnvironmentInjector);
      void import('./shared/offline/sync.service').then(({ SyncService }) =>
        runInInjectionContext(injector, () => inject(SyncService))
      );
    }),

    // =========================================================================
    // Service Worker (PWA)
    // =========================================================================
    // `!isDevMode()` en vez de `BUILD_ID !== 'dev'` a propósito: así un build de
    // producción servido en localhost (que ES contexto seguro) registra el SW y la
    // PWA se puede probar de punta a punta sin desplegar, mientras el dev server
    // nunca lo registra. Atarlo al BUILD_ID haría que la única forma de probar el
    // modo sin conexión fuera contra producción.
    //
    // `registerWhenStable:30000`: el registro espera a que la app quede estable para
    // no competir con la carga inicial; el tope de 30 s garantiza que se registre
    // igual, porque esta app tiene polling (heartbeat de sesión) que puede mantener
    // la zona ocupada indefinidamente y dejar el SW sin registrar para siempre.
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000'
    }),

    provideRouter([
      { path: '', redirectTo: 'home', pathMatch: 'full' },

      // Público
      { path: 'login', component: LoginComponent },
      { path: 'password-recovery', component: PasswordRecoveryComponent },
      // Aterrizaje del enlace que llega por correo (?token=...). Sin guard: por definición
      // la abre alguien que NO puede iniciar sesión.
      {
        path: 'reset-password',
        title: 'Crear contraseña nueva',
        loadComponent: () =>
          import('./features/auth/reset-password/reset-password.component')
            .then(m => m.ResetPasswordComponent)
      },

      // Aparcar la sesión propia. CON authGuard: hay que estar adentro para poder guardarse.
      {
        path: 'cambiar-usuario',
        title: 'Cambiar de usuario',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/auth/cambiar-usuario/cambiar-usuario.component')
            .then(m => m.CambiarUsuarioComponent)
      },

      // Selector de perfil — SIN authGuard a propósito: por definición la abre alguien
      // que TODAVÍA no tiene sesión activa. Anda sin red: todo lo que pinta sale del
      // padrón de slots, que va sin cifrar justamente para poder mostrarse sin PIN.
      {
        path: 'selector-usuario',
        title: 'Elegir sesión',
        loadComponent: () =>
          import('./features/auth/selector-usuario/selector-usuario.component')
            .then(m => m.SelectorUsuarioComponent)
      },

      // Diagnóstico del dispositivo — SIN authGuard a propósito: es la pantalla a la
      // que se recurre cuando nada más funciona (sesión vencida sin red para renovarla,
      // Service Worker en safe mode). Un guard la haría inalcanzable justo en el
      // escenario para el que existe. Las capturas de OTRAS sesiones se listan
      // enmascaradas —sin payload y sin poder copiarlas ni descartarlas—, que es lo
      // que sustituye al guard. Ver el doc-comment del componente.
      {
        path: 'diagnostico',
        title: 'Diagnóstico del dispositivo',
        loadComponent: () =>
          import('./features/diagnostico/diagnostico-page.component')
            .then(m => m.DiagnosticoPageComponent)
      },

      // Protegido
      { path: 'home', component: HomeComponent, canActivate: [authGuard] },
      {
        path: 'profile',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/profile/profile.component')
            .then(m => m.ProfileComponent)
      },
      {
        // Los paneles de adentro cargan solos con @defer (on viewport): el que no se scrollea
        // no dispara su request. No lleva `permissionGuard`: quien entra ve los paneles de los
        // modulos que tiene en su menu, y si no tiene ninguno la pagina se lo dice.
        path: 'dashboard',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/dashboard/pages/dashboard-page/dashboard-page.component')
            .then(m => m.DashboardPageComponent)
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
            path: 'seguimiento-diario-lote-reproductora',
            loadChildren: () =>
              import('./features/lote-levante/seguimiento-lote-levante.module')
                .then(m => m.SeguimientoLoteLevanteModule)
          },
          {
            path: 'seguimiento-diario-lote-reproductora_pollo_engorde',
            loadChildren: () =>
              import('./features/seguimiento-diario-lote-reproductora/seguimiento-diario-lote-reproductora.module')
                .then(m => m.SeguimientoDiarioLoteReproductoraModule)
          },
          {
            path: 'aves-engorde',
            loadChildren: () =>
              import('./features/aves-engorde/seguimiento-aves-engorde.module')
                .then(m => m.SeguimientoAvesEngordeModule)
          }
        ]
      },

      // Lote Reproductora (módulo independiente)
      {
        path: 'lote-reproductora',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/lote-reproductora/lote-reproductora.module')
            .then(m => m.LoteReproductoraModule)
      },

      // Tickets de soporte / requerimientos (módulo independiente)
      {
        path: 'tickets',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/tickets/tickets.routes')
            .then(m => m.TICKETS_ROUTES)
      },

      // ItalJira — gestión del área de desarrollo: historias, tareas, tiempos y roadmap
      {
        path: 'italjira',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/italjira/italjira.routes')
            .then(m => m.ITALJIRA_ROUTES)
      },

      // Gerencia — vistas de lectura de los indicadores, sin las facultades de gestión de ItalJira
      {
        path: 'gerencia',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/gerencia/gerencia.routes')
            .then(m => m.GERENCIA_ROUTES)
      },

      // Implementación (cronogramas de entrega por empresa con checklist confirmable)
      {
        path: 'implementacion',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/implementacion/implementacion.routes')
            .then(m => m.IMPLEMENTACION_ROUTES)
      },

      // Migraciones Masivas (módulo independiente)
      {
        path: 'migraciones-masivas',
        // Cualquiera de los dos permisos abre la pantalla; adentro, `filtrarTiposVisibles` decide
        // qué tiles ve. Sin ninguno la ruta ya no entra: antes bastaba con escribir la URL a mano.
        canActivate: [authGuard, permissionGuard],
        data: { permissions: ['carga_masiva_postura', 'carga_masiva_pollo_engorde'] },
        loadChildren: () =>
          import('./features/migraciones-masivas/migraciones-masivas-routing.module')
            .then(m => m.MigracionesMasivasRoutingModule)
      },

      // Sincronización / Integración Panamá (bajo el área de Migraciones)
      // Ruta resultante: /migraciones/sincronizacion-panama
      {
        path: 'migraciones',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/sincronizacion-panama/sincronizacion-panama-routing.module')
            .then(m => m.SincronizacionPanamaRoutingModule)
      },

      {
        path: 'config',
        loadComponent: () =>
          import('./features/config/config.component')
            .then(m => m.ConfigComponent),
        canActivate: [authGuard],
        children: [
          {
            path: 'master-lists',
            loadComponent: () =>
              import('./features/config/master-lists/master-lists.component')
                .then(m => m.MasterListsComponent)
          },
          {
            path: 'master-lists/new',
            loadComponent: () =>
              import('./features/config/master-lists/list-detail/list-detail.component')
                .then(m => m.ListDetailComponent)
          },
          {
            path: 'master-lists/:id',
            loadComponent: () =>
              import('./features/config/master-lists/list-detail/list-detail.component')
                .then(m => m.ListDetailComponent)
          },

          // Lista maestra de silos (empresas con inventario por silo). Lazy: no la carga quien no la usa.
          {
            path: 'silos',
            loadComponent: () =>
              import('./features/silos/pages/silo-catalogo/silo-catalogo.component')
                .then(m => m.SiloCatalogoComponent)
          },

          // Empresas y Roles son pantallas de administración: las abre poca gente y muy de vez en
          // cuando, pero al estar importadas de forma estática viajaban en el bundle INICIAL de
          // todos. Entre las dos empujaban el inicial por encima del presupuesto de error (2.05 MB)
          // y `ng build` fallaba. Lazy, como el resto de la app.
          // (17ago26: el mismo tratamiento se extendió a TODAS las pantallas de esta sección.)
          {
            path: 'companies',
            loadComponent: () =>
              import('./features/config/company-management/company-management.component')
                .then(m => m.CompanyManagementComponent)
          },
          {
            path: 'role-management',
            loadComponent: () =>
              import('./features/config/role-management/role-management.component')
                .then(m => m.RoleManagementComponent)
          },
          {
            path: 'users',
            loadComponent: () =>
              import('./features/config/user-management/user-management.component')
                .then(m => m.UserManagementComponent)
          },

          // geografía
          {
            path: 'countries',
            loadComponent: () =>
              import('./features/config/geography/country-list/country-list.component')
                .then(m => m.CountryListComponent)
          },
          {
            path: 'countries/new',
            loadComponent: () =>
              import('./features/config/geography/country-detail/country-detail.component')
                .then(m => m.CountryDetailComponent)
          },
          {
            path: 'countries/:id',
            loadComponent: () =>
              import('./features/config/geography/country-detail/country-detail.component')
                .then(m => m.CountryDetailComponent)
          },
          {
            path: 'states',
            loadComponent: () =>
              import('./features/config/geography/state-list/state-list.component')
                .then(m => m.StateListComponent)
          },
          {
            path: 'states/new',
            loadComponent: () =>
              import('./features/config/geography/state-detail/state-detail.component')
                .then(m => m.StateDetailComponent)
          },
          {
            path: 'states/:id',
            loadComponent: () =>
              import('./features/config/geography/state-detail/state-detail.component')
                .then(m => m.StateDetailComponent)
          },
          {
            path: 'departments',
            loadComponent: () =>
              import('./features/config/geography/department-list/department-list.component')
                .then(m => m.DepartmentListComponent)
          },
          {
            path: 'departments/new',
            loadComponent: () =>
              import('./features/config/geography/department-detail/department-detail.component')
                .then(m => m.DepartmentDetailComponent)
          },
          {
            path: 'departments/:id',
            loadComponent: () =>
              import('./features/config/geography/department-detail/department-detail.component')
                .then(m => m.DepartmentDetailComponent)
          },
          {
            path: 'cities',
            loadComponent: () =>
              import('./features/config/geography/city-list/city-list.component')
                .then(m => m.CityListComponent)
          },
          {
            path: 'cities/new',
            loadComponent: () =>
              import('./features/config/geography/city-detail/city-detail.component')
                .then(m => m.CityDetailComponent)
          },
          {
            path: 'cities/:id',
            loadComponent: () =>
              import('./features/config/geography/city-detail/city-detail.component')
                .then(m => m.CityDetailComponent)
          },

          // CRUD Granjas
          {
            path: 'farm-management',
            loadComponent: () =>
              import('./features/farm/pages/farm-management/farm-management.component')
                .then(m => m.FarmManagementComponent)
          },
          {
            path: 'farms-list',
            loadComponent: () =>
              import('./features/farm/components/farm-list/farm-list.component')
                .then(m => m.FarmListComponent)
          },
          {
            path: 'farms-list/new',
            loadComponent: () =>
              import('./features/farm/components/farm-form/farm-form.component')
                .then(m => m.FarmFormComponent)
          },
          {
            path: 'farms-list/:id/edit',
            loadComponent: () =>
              import('./features/farm/components/farm-form/farm-form.component')
                .then(m => m.FarmFormComponent)
          },

          // Núcleos
          {
            path: 'nucleos',
            loadComponent: () =>
              import('./features/nucleo/components/nucleo-list/nucleo-list.component')
                .then(m => m.NucleoListComponent)
          },
          {
            path: 'nucleos/new',
            loadComponent: () =>
              import('./features/nucleo/components/nucleo-form/nucleo-form.component')
                .then(m => m.NucleoFormComponent)
          },
          {
            path: 'nucleos/:nucleoId',
            loadComponent: () =>
              import('./features/nucleo/components/nucleo-form/nucleo-form.component')
                .then(m => m.NucleoFormComponent)
          },

          // Galpones
          {
            path: 'galpones',
            loadComponent: () =>
              import('./features/galpon/components/galpon-list/galpon-list.component')
                .then(m => m.GalponListComponent)
          },
          {
            path: 'galpones/new',
            loadComponent: () =>
              import('./features/galpon/components/galpon-form/galpon-form.component')
                .then(m => m.GalponFormComponent)
          },
          {
            path: 'galpones/:galponId',
            loadComponent: () =>
              import('./features/galpon/components/galpon-form/galpon-form.component')
                .then(m => m.GalponFormComponent)
          },

          {
            path: 'lote-management',
            loadComponent: () =>
              import('./features/lote/page/lote-management/lote-management.componet')
                .then(m => m.LoteManagementComponent)
          },
          {
            path: 'lote-engorde',
            loadComponent: () =>
              import('./features/lote-engorde/pages/lote-engorde-management/lote-engorde-management.component')
                .then(m => m.LoteEngordeManagementComponent)
          },
          {
            path: 'lote-reproductora-ave-engorde',
            loadChildren: () =>
              import('./features/lote-reproductora-ave-engorde/lote-reproductora-ave-engorde.module')
                .then(m => m.LoteReproductoraAveEngordeModule)
          },
          // Lotes
          {
            path: 'lotes',
            loadComponent: () =>
              import('./features/lote/components/lote-list/lote-list.component')
                .then(m => m.LoteListComponent)
          },

          // Guía genética (produccion_avicola_raw)
          {
            path: 'guia-genetica',
            loadComponent: () =>
              import('./features/config/guia-genetica-admin/guia-genetica-list/guia-genetica-list.component')
                .then(m => m.GuiaGeneticaListComponent)
          },
          {
            path: 'guia-genetica/new',
            loadComponent: () =>
              import('./features/config/guia-genetica-admin/guia-genetica-form/guia-genetica-form.component')
                .then(m => m.GuiaGeneticaFormComponent)
          },
          {
            path: 'guia-genetica/:id',
            loadComponent: () =>
              import('./features/config/guia-genetica-admin/guia-genetica-detail/guia-genetica-detail.component')
                .then(m => m.GuiaGeneticaDetailComponent)
          },
          {
            path: 'guia-genetica/:id/edit',
            loadComponent: () =>
              import('./features/config/guia-genetica-admin/guia-genetica-form/guia-genetica-form.component')
                .then(m => m.GuiaGeneticaFormComponent)
          },

          // Guía genética de POLLO ENGORDE (tabla compartida por todas las empresas y países: su
          // header tiene pais_id, y la Ross 308 AP de Panamá vive ahí). Se llamaba
          // 'guia-genetica-ecuador'; esa URL queda como REDIRECT porque es la que tiene el menú
          // guardado en BD, y el menú no se puede mover antes que el bundle.
          {
            path: 'guia-genetica-engorde',
            loadComponent: () =>
              import('./features/config/guia-genetica-engorde/guia-genetica-engorde-page/guia-genetica-engorde-page.component')
                .then(m => m.GuiaGeneticaEngordePageComponent)
          },
          { path: 'guia-genetica-ecuador', redirectTo: 'guia-genetica-engorde', pathMatch: 'full' },

          // Guía genética REDUCIDA (guia_genetica_santa_reyes) — tabla plana de 3 métricas por
          // raza/año/semana. Es la TERCERA pantalla de guía genética, y son tres a propósito: cada
          // una administra una tabla distinta (ver models/guia-genetica-santa-reyes.model.ts).
          {
            path: 'guia-genetica-santa-reyes',
            loadComponent: () =>
              import('./features/config/guia-genetica-santa-reyes/pages/guia-genetica-santa-reyes-page/guia-genetica-santa-reyes-page.component')
                .then(m => m.GuiaGeneticaSantaReyesPageComponent)
          },

          // Catálogo de Alimentos (lazy)
          {
            path: 'catalogo-alimentos',
            loadChildren: () =>
              import('./features/catalogo-alimentos/catalogo-alimentos.module')
                .then(m => m.CatalogoAlimentosModule)
          },
          // Ítems de inventario (catálogo compartido EC/PA/CO para Gestión de Inventario).
          // La URL 'item-inventario-ecuador' se conserva por compatibilidad con el menú en BD.
          {
            path: 'item-inventario-ecuador',
            loadChildren: () =>
              import('./features/config/item-inventario/item-inventario.module')
                .then(m => m.ItemInventarioModule)
          },

          // Gestión de Clientes
          {
            path: 'clientes',
            loadComponent: () =>
              import('./features/clientes/components/cliente-list/cliente-list.component')
                .then(m => m.ClienteListComponent)
          },

          // DB Studio (ruta: /config/db-studio — coincide con la ruta del menú)
          {
            path: 'db-studio',
            loadChildren: () =>
              import('./features/db-studio/db-studio.module')
                .then(m => m.DbStudioModule)
          }
        ]
      },
      
      // Indicador de pollo engorde (ruta independiente). Se llamaba 'indicador-ecuador' pero nunca
      // fue de Ecuador: adentro viven los reportes de corrida y de liquidación de Panamá. La URL
      // vieja queda como REDIRECT porque es la que tiene el menú guardado en BD.
      {
        path: 'indicador-engorde',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/indicador-engorde/indicador-engorde.module')
            .then(m => m.IndicadorEngordeModule)
      },
      { path: 'indicador-ecuador', redirectTo: 'indicador-engorde', pathMatch: 'prefix' },

      // Informe Semanal Pollo de Engorde (Panamá)
      {
        path: 'informe-semanal-engorde',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/informe-semanal-engorde/pages/informe-semanal-engorde-list/informe-semanal-engorde-list.component')
            .then(m => m.InformeSemanalEngordeListComponent)
      },

      // Reporte Diario Costos Pollo Engorde (por granja + lote base)
      {
        path: 'reporte-diario-costos-engorde',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/reporte-diario-costos-engorde/pages/reporte-diario-costos-engorde-main/reporte-diario-costos-engorde-main.component')
            .then(m => m.ReporteDiarioCostosEngordeMainComponent)
      },

      // Reporte Diario Área de Costos POSTURA (levante + producción, por lote base).
      // OJO: no es el de engorde de arriba — otras fuentes y otras reglas de negocio.
      {
        path: 'reporte-diario-costos-postura',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/reporte-diario-costos-postura/pages/reporte-diario-costos-postura-main/reporte-diario-costos-postura-main.component')
            .then(m => m.ReporteDiarioCostosPosturaMainComponent)
      },

      // Informe RA Pesadas (Sanmarino postura): shell con dos modos —
      // Resumen semanal (todos los lotes, una semana) y Detalle de lote
      // (un lote base, todas sus semanas + gráficas), ambos vs guía genética.
      // La ruta se conserva: es la que ya está sembrada en menus/role_menus.
      {
        path: 'reporte-tecnico-semanal',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/reporte-tecnico-semanal/pages/informe-ra-pesadas-main/informe-ra-pesadas-main.component')
            .then(m => m.InformeRaPesadasMainComponent)
      },

      // Inventario (fuera de config, ruta independiente)
      {
        path: 'inventario',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/inventario/inventario.module')
            .then(m => m.InventarioModule)
      },
      // Gestión de Inventario (Panama/Ecuador): ingresos y traslados; alimento → Granja/Núcleo/Galpón; otros → Granja
      {
        path: 'gestion-inventario',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/gestion-inventario/gestion-inventario.module')
            .then(m => m.GestionInventarioModule)
      },

      // Bandeja de cuadre de las capturas offline (PWA F7): capturas que se guardaron SIN descontar
      // inventario porque al llegar al servidor no había stock. Es de supervisión y se mira con red.
      {
        path: 'cuadres-offline',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/cuadres-offline/pages/cuadres-offline-page/cuadres-offline-page.component')
            .then(m => m.CuadresOfflinePageComponent)
      },

      // Gastos de Inventario (Ecuador): consumos por concepto (no alimentos), stock por granja
      {
        path: 'inventario-gastos',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/gastos-inventario/pages/gastos-inventario-page/gastos-inventario-page.component')
            .then(m => m.GastosInventarioPageComponent)
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
            // Ruta historica del menu ("Nuevo Traslado"). Paso por dos pantallas ya retiradas:
            // primero `traslado-form` (DTO desalineado del backend, borrada) y despues
            // `traslado-aves-huevos` (formulario duplicado del dashboard, con el lote destino a
            // mano como ID numerico). Hoy el traslado/venta de aves se crea desde el dashboard,
            // que filtra el lote por Granja > Nucleo > Galpon y usa el mismo endpoint
            // (`TrasladosController.CrearTrasladoAves`). El item de menu sigue funcionando.
            path: 'traslados',
            redirectTo: 'dashboard'
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
            // Conservada como redirect: habia links y marcadores apuntando aca.
            path: 'nuevo',
            redirectTo: 'dashboard'
          }
        ]
      },
      {
        path: 'traslados-huevos',
        canActivate: [authGuard],
        children: [
          {
            path: '',
            redirectTo: 'lista',
            pathMatch: 'full'
          },
          {
            path: 'lista',
            loadComponent: () => import('./features/traslados-huevos/pages/traslados-huevos-list/traslados-huevos-list.component')
              .then(m => m.TrasladosHuevosListComponent),
            title: 'Traslados de Huevos'
          },
          {
            // `TrasladoHuevosFormComponent` (ruta 'nuevo') se retiro el 3-sep-2026: no estaba en
            // ningun `role_menus` (0 roles) ni la enlazaba ninguna pantalla — solo se llegaba
            // tecleando la URL. Duplicaba `ModalTrasladoHuevosComponent`, que es el que si edita y
            // el que usa el listado. La ruta queda como redirect por si hubiera marcadores.
            path: 'nuevo',
            redirectTo: 'lista'
          }
        ]
      },
      {
        path: 'movimientos-aves',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/movimientos-aves/movimientos-aves-routing.module')
            .then(m => m.MovimientosAvesRoutingModule)
      },
      {
        path: 'movimiento-pollo-engorde',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/movimientos-pollo-engorde/movimientos-pollo-engorde-routing.module')
            .then(m => m.MovimientosPolloEngordeRoutingModule)
      },
      {
        path: 'vacunacion',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./features/vacunacion/vacunacion-routing.module')
            .then(m => m.VacunacionRoutingModule)
      },

      // Módulo Mapas (configuraciones y ejecución)
      {
        path: 'mapas',
        canActivate: [authGuard],
        children: [
          { path: '', redirectTo: 'configuraciones', pathMatch: 'full' },
          {
            path: 'configuraciones',
            loadComponent: () =>
              import('./features/mapas/pages/mapas-configuraciones-list/mapas-configuraciones-list.component')
                .then(m => m.MapasConfiguracionesListComponent),
            title: 'Configuraciones de Mapas'
          },
          {
            path: 'configuraciones/:id',
            loadComponent: () =>
              import('./features/mapas/pages/mapa-configurar/mapa-configurar.component')
                .then(m => m.MapaConfigurarComponent),
            title: 'Configurar Mapa'
          },
          {
            path: 'ejecutar/:id',
            loadComponent: () =>
              import('./features/mapas/pages/mapa-ejecutar-placeholder/mapa-ejecutar-placeholder.component')
                .then(m => m.MapaEjecutarPlaceholderComponent),
            title: 'Ejecutar Mapa'
          }
        ]
      },

      { path: '**', redirectTo: 'login' }
    ])
  ]
};

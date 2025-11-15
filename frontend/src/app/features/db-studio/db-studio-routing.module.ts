// src/app/features/db-studio/db-studio-routing.module.ts
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { DbStudioMainComponent } from './pages/db-studio-main/db-studio-main.component';
import { ExplorerPage } from './pages/explorer/explorer.page';
import { QueryConsolePage } from './pages/query-console/query-console.page';
import { CreateTablePage } from './pages/create-table/create-table.page';
import { DataManagementPage } from './pages/data-management/data-management.page';
import { IndexManagementPage } from './pages/index-management/index-management.page';

const routes: Routes = [
  {
    path: '',
    component: DbStudioMainComponent,
    data: { title: 'DB Studio - Dashboard' }
  },
  {
    path: 'explorer',
    component: ExplorerPage,
    data: { title: 'DB Studio - Explorador' }
  },
  {
    path: 'query',
    component: QueryConsolePage,
    data: { title: 'DB Studio - Consola SQL' }
  },
  {
    path: 'query-console',
    redirectTo: 'query',
    pathMatch: 'full'
  },
  {
    path: 'create-table',
    component: CreateTablePage,
    data: { title: 'DB Studio - Crear Tabla' }
  },
  {
    path: 'data/:schema/:table',
    component: DataManagementPage,
    data: { title: 'DB Studio - Gestión de Datos' }
  },
  {
    path: 'data-management',
    component: DataManagementPage,
    data: { title: 'DB Studio - Gestión de Datos' }
  },
  {
    path: 'indexes/:schema/:table',
    component: IndexManagementPage,
    data: { title: 'DB Studio - Gestión de Índices' }
  },
  {
    path: 'index-management',
    component: IndexManagementPage,
    data: { title: 'DB Studio - Gestión de Índices' }
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class DbStudioRoutingModule {}

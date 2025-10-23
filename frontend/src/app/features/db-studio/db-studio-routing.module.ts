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
  { path: '', component: DbStudioMainComponent },
  { path: 'explorer', component: ExplorerPage },
  { path: 'query-console', component: QueryConsolePage },
  { path: 'create-table', component: CreateTablePage },
  { path: 'data-management', component: DataManagementPage },
  { path: 'index-management', component: IndexManagementPage },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class DbStudioRoutingModule {}

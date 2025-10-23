// src/app/features/db-studio/db-studio.module.ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { DbStudioRoutingModule } from './db-studio-routing.module';

// ✅ Standalone pages: se importan directamente
import { DbStudioMainComponent } from './pages/db-studio-main/db-studio-main.component';
import { ExplorerPage } from './pages/explorer/explorer.page';
import { QueryConsolePage } from './pages/query-console/query-console.page';
import { CreateTablePage } from './pages/create-table/create-table.page';

@NgModule({
  imports: [
    CommonModule,
    DbStudioRoutingModule,
    DbStudioMainComponent,
    ExplorerPage,
    QueryConsolePage,
    CreateTablePage
  ]
})
export class DbStudioModule {}

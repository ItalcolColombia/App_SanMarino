// src/app/features/db-studio/db-studio.module.ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { DbStudioRoutingModule } from './db-studio-routing.module';
import { DbStudioMainComponent } from './pages/db-studio-main/db-studio-main.component';

@NgModule({
  imports: [
    CommonModule,
    DbStudioRoutingModule,
    DbStudioMainComponent // standalone workspace: todo el módulo vive acá
  ]
})
export class DbStudioModule {}

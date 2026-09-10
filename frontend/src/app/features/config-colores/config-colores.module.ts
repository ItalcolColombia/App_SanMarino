// src/app/features/config-colores/config-colores.module.ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { ConfigColoresRoutingModule } from './config-colores-routing.module';
import { ConfigColoresMainComponent } from './pages/config-colores-main/config-colores-main.component';

@NgModule({
  imports: [
    CommonModule,
    ConfigColoresRoutingModule,
    ConfigColoresMainComponent // standalone workspace: todo el módulo vive acá
  ]
})
export class ConfigColoresModule {}

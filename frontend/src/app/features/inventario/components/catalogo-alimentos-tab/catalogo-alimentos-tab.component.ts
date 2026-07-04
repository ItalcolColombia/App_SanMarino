// src/app/features/inventario/components/catalogo-alimentos-tab/catalogo-alimentos-tab.component.ts
import { Component } from '@angular/core';

import { CatalogoAlimentosModule } from '../../../catalogo-alimentos/catalogo-alimentos.module';

@Component({
  selector: 'app-catalogo-alimentos-tab',
  standalone: true,
  imports: [CatalogoAlimentosModule], // ✅ solo el módulo
  template: `
    <!-- Usa el selector REAL del listado (ver paso 2) -->
    <app-catalogo-alimentos-list></app-catalogo-alimentos-list>
  `
})
export class CatalogoAlimentosTabComponent {}

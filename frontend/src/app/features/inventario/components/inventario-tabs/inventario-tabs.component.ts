// src/app/features/inventario/components/inventario-tabs/inventario-tabs.component.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faBoxesStacked, faRightLeft, faWarehouse, faScrewdriverWrench, faList, faClipboardCheck, faArrowsUpDown, faBook } from '@fortawesome/free-solid-svg-icons';

import { MovimientosFormComponent } from '../movimientos-form/movimientos-form.component';
import { TrasladoFormComponent } from '../traslado-form/traslado-form.component';
import { InventarioListComponent } from '../inventario-list/inventario-list.component';
import { AjusteFormComponent } from '../ajuste-form/ajuste-form.component';
import { KardexListComponent } from '../kardex-list/kardex-list.component';
import { ConteoFisicoComponent } from '../conteo-fisico/conteo-fisico.component';
import { CatalogoAlimentosTabComponent } from '../catalogo-alimentos-tab/catalogo-alimentos-tab.component';

type TabKey = 'mov' | 'tras' | 'ajuste' | 'kardex' | 'conteo' | 'stock' | 'catalogo';
@Component({
  selector: 'app-inventario-tabs',
  standalone: true,
  imports: [
    CommonModule,
    SidebarComponent,
    FontAwesomeModule,
    MovimientosFormComponent,
    TrasladoFormComponent,
    InventarioListComponent,
    AjusteFormComponent,
    KardexListComponent,
    ConteoFisicoComponent,
    CatalogoAlimentosTabComponent
],
  templateUrl: './inventario-tabs.component.html',
  styleUrls: ['./inventario-tabs.component.scss']
})

export class InventarioTabsComponent {
  faInOut   = faArrowsUpDown;
  faSwap    = faRightLeft;
  faWare    = faWarehouse;
  faWrench  = faScrewdriverWrench;
  faList    = faList;
  faClipboard = faClipboardCheck;
  faCatalog = faBook;
  title = 'Inventario de Productos';

  activeTab: TabKey = 'mov';
  // Descripciones por pestaña (SUBTÍTULO dinámico)
  private readonly subtitleMap: Record<TabKey, string> = {
    mov:    'Registra entradas y salidas de productos por granja.',
    tras:   'Traslada stock entre granjas manteniendo la trazabilidad.',
    ajuste: 'Corrige diferencias de inventario (mermas, daños, conteos).',
    kardex: 'Consulta el historial de movimientos (Kardex) por producto.',
    conteo: 'Captura conteos físicos y concilia contra el sistema.',
    stock:  'Visualiza el stock disponible por granja y producto.',
    catalogo: 'Administra el catálogo de ítems (alimentos/insumos).'
  };

  get subtitle(): string {
    return this.subtitleMap[this.activeTab];
  }

  setTab(tab: TabKey) { this.activeTab = tab; }

  refreshData(): void {
    // TODO: Implementar lógica de actualización de datos
    console.log('Actualizando datos del inventario...');
  }
}

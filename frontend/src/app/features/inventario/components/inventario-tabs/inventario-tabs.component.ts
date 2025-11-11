// src/app/features/inventario/components/inventario-tabs/inventario-tabs.component.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faBoxesStacked, faRightLeft, faWarehouse, faScrewdriverWrench, faList, faClipboardCheck, faArrowsUpDown, faBook } from '@fortawesome/free-solid-svg-icons';

import { MovimientosUnificadoFormComponent } from '../movimientos-unificado-form/movimientos-unificado-form.component';
import { InventarioListComponent } from '../inventario-list/inventario-list.component';
import { AjusteFormComponent } from '../ajuste-form/ajuste-form.component';
import { KardexListComponent } from '../kardex-list/kardex-list.component';
import { ConteoFisicoComponent } from '../conteo-fisico/conteo-fisico.component';
import { CatalogoAlimentosTabComponent } from '../catalogo-alimentos-tab/catalogo-alimentos-tab.component';

type TabKey = 'movimientos' | 'ajuste' | 'kardex' | 'conteo' | 'stock' | 'catalogo';
@Component({
  selector: 'app-inventario-tabs',
  standalone: true,
  imports: [
    CommonModule,
    SidebarComponent,
    FontAwesomeModule,
    MovimientosUnificadoFormComponent,
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
  faWare    = faWarehouse;
  faWrench  = faScrewdriverWrench;
  faList    = faList;
  faClipboard = faClipboardCheck;
  faCatalog = faBook;
  title = 'Inventario de Productos';

  activeTab: TabKey = 'movimientos';
  // Descripciones por pestaña (SUBTÍTULO dinámico)
  private readonly subtitleMap: Record<TabKey, string> = {
    movimientos: 'Registra entradas, salidas y traslados de productos entre granjas.',
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
}

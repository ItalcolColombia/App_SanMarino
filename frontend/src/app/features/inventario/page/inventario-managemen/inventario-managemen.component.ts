// src/app/features/farm/pages/farm-management/farm-management.component.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faWarehouse, faPuzzlePiece, faHome } from '@fortawesome/free-solid-svg-icons';

// Si tus listas están en "components/..."
import { NucleoListComponent } from '../../../nucleo/components/nucleo-list/nucleo-list.component';
import { GalponListComponent } from '../../../galpon/components/galpon-list/galpon-list.component';
import { InventarioListComponent } from "../../components/inventario-list/inventario-list.component";
import { CatalogoAlimentosListComponent } from "../../../catalogo-alimentos/pages/catalogo-alimentos-list/catalogo-alimentos-list.component";
// …o si están en "pages/...", usa:
// import { NucleoListComponent } from '../../../nucleo/pages/nucleo-list/nucleo-list.component';
// import { GalponListComponent } from '../../../galpon/pages/galpon-list/galpon-list.component';

type TabKey = 'granjas' | 'nucleos' | 'galpones';

@Component({
  selector: 'app-farm-management',
  standalone: true,
  imports: [
    CommonModule,
    SidebarComponent,
    FontAwesomeModule,
    InventarioListComponent,
    CatalogoAlimentosListComponent
],
  templateUrl: './inventario-managemen.html',
  styleUrls: ['./inventario-managemen.component.scss'],
})
export class InventarioManagementComponent {
  faHome = faHome;
  faPuzzle = faPuzzlePiece;
  faWarehouse = faWarehouse;

  // pestaña activa
  activeTab: TabKey = 'granjas';

  // por si quieres embeber en otra vista (quita header)
  embedded = false;

  setTab(t: TabKey) {
    this.activeTab = t;
  }

  isActive(t: TabKey) {
    return this.activeTab === t;
  }
}

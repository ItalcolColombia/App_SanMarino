import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faHome, faEgg } from '@fortawesome/free-solid-svg-icons';

// Ajusta estas rutas si tus listas viven en otra carpeta:
import { LoteListComponent } from '../../../lote/components/lote-list/lote-list.component';
import { LoteReproductoraListComponent } from '../../../lote-reproductora/pages/lote-reproductora-list/lote-reproductora-list.component';

type TabKey = 'lotes' | 'reproductoras';

@Component({
  selector: 'app-lote-management',
  standalone: true,
  imports: [
    CommonModule,
    SidebarComponent,
    FontAwesomeModule,
    LoteListComponent,
    LoteReproductoraListComponent,
  ],
  templateUrl: './lote-management.componet.html',
  styleUrls: ['./lote-management.componet.scss'],
})
export class LoteManagementComponent {
  // Íconos
  faLotes = faHome;
  faReproductoras = faEgg;

  // Vista
  embedded = false;
  activeTab: TabKey = 'lotes';

  setTab(t: TabKey) { this.activeTab = t; }
  isActive(t: TabKey) { return this.activeTab === t; }
}

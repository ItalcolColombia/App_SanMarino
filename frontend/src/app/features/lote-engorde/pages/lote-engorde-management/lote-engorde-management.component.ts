import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { LoteEngordeListComponent } from '../../components/lote-engorde-list/lote-engorde-list.component';

@Component({
  selector: 'app-lote-engorde-management',
  standalone: true,
  imports: [
    CommonModule,
    SidebarComponent,
    FontAwesomeModule,
    LoteEngordeListComponent,
  ],
  template: `
    <div class="layout">
      <app-sidebar class="layout__sidebar"></app-sidebar>
      <main class="layout__main">
        <app-lote-engorde-list></app-lote-engorde-list>
      </main>
    </div>
  `,
  styles: [`
    .layout {
      display: flex;
      min-height: 100vh;
      width: 100%;
      overflow: hidden;
    }
    .layout__sidebar {
      width: 16rem;
      flex-shrink: 0;
    }
    .layout__main {
      flex: 1;
      min-width: 0;
      overflow: auto;
    }
  `]
})
export class LoteEngordeManagementComponent {}

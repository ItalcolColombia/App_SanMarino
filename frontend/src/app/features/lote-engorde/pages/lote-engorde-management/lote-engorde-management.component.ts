import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { LoteEngordeListComponent } from '../../components/lote-engorde-list/lote-engorde-list.component';

@Component({
  selector: 'app-lote-engorde-management',
  standalone: true,
  imports: [
    CommonModule,
    FontAwesomeModule,
    LoteEngordeListComponent,
  ],
  template: `
    <div class="layout">
      <main class="layout__main">
        <app-lote-engorde-list></app-lote-engorde-list>
      </main>
    </div>
  `,
  styles: [`
    .layout {
      display: flex;
      min-height: 100vh;
      min-height: 100dvh;
      min-height: -webkit-fill-available;
      width: 100%;
      max-width: 100%;
      overflow: hidden;
    }
    .layout__sidebar {
      width: 16rem;
      flex-shrink: 0;
    }
    .layout__main {
      flex: 1;
      min-width: 0;
      min-height: 0;
      overflow: auto;
    }
  `]
})
export class LoteEngordeManagementComponent {}

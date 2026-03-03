import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faHome } from '@fortawesome/free-solid-svg-icons';

// Ajusta estas rutas si tus listas viven en otra carpeta:
import { LoteListComponent } from '../../../lote/components/lote-list/lote-list.component';

@Component({
  selector: 'app-lote-management',
  standalone: true,
  imports: [
    CommonModule,
    FontAwesomeModule,
    LoteListComponent,
  ],
  templateUrl: './lote-management.componet.html',
  styleUrls: ['./lote-management.componet.scss'],
})
export class LoteManagementComponent {
  // Íconos
  faLotes = faHome;

  // Vista
  embedded = false;
}

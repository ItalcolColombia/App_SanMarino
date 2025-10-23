// src/app/features/config/master-lists/master-lists.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { SidebarComponent } from '../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faList,
  faEye,
  faPen,
  faTrash,
  faPlus
} from '@fortawesome/free-solid-svg-icons';
import {
  MasterListService,
  MasterListDto
} from '../../../core/services/master-list/master-list.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-master-lists',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    SidebarComponent,
    FontAwesomeModule
  ],
  templateUrl: './master-lists.component.html',
  styleUrls: ['./master-lists.component.scss']
})
export class MasterListsComponent implements OnInit {
  // Icons
  faList  = faList;
  faEye   = faEye;
  faPen   = faPen;
  faTrash = faTrash;
  faPlus  = faPlus;

  // Data
  lists: MasterListDto[] = [];
  loading = false;

  // Estado de expansión por id (coincide con el HTML)
  expandedIds = new Set<number>();

  constructor(
    private svc: MasterListService,
    private router: Router,
    library: FaIconLibrary
  ) {
    library.addIcons(faList, faEye, faPen, faTrash, faPlus);
  }

  ngOnInit(): void {
    this.loadLists();
  }

  private loadLists(): void {
    this.loading = true;
    this.svc.getAll()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: data => (this.lists = data || []),
        error: err => {
          console.error('Error cargando listas maestras', err);
          this.lists = [];
        }
      });
  }

  // Navega al formulario de nueva lista
  newList(): void {
    this.router.navigate(['/config/master-lists', 'new']);
  }

  // Navega al formulario de edición
  edit(list: MasterListDto): void {
    this.router.navigate(['/config/master-lists', list.id]);
  }

  // Elimina una lista y recarga
  delete(list: MasterListDto): void {
    if (!confirm(`¿Eliminar la lista “${list.name}”?`)) return;
    this.loading = true;
    this.svc.delete(list.id)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => this.loadLists(),
        error: err => console.error('Error eliminando lista maestra', err)
      });
  }

  // Expande/colapsa opciones (coincide con el HTML)
  toggleOptions(list: MasterListDto): void {
    if (this.expandedIds.has(list.id)) {
      this.expandedIds.delete(list.id);
    } else {
      this.expandedIds.add(list.id);
    }
  }

  // trackBy para <li *ngFor="let opt of list.options; trackBy: trackByIndex">
  trackByIndex(index: number): number {
    return index;
  }
}

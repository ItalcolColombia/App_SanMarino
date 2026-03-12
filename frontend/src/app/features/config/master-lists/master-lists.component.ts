// src/app/features/config/master-lists/master-lists.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faList,
  faEye,
  faPen,
  faTrash,
  faPlus,
  faTimes
} from '@fortawesome/free-solid-svg-icons';
import {
  MasterListService,
  MasterListDto
} from '../../../core/services/master-list/master-list.service';
import { ModalEditMasterListComponent } from './modal-edit-master-list/modal-edit-master-list.component';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-master-lists',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FontAwesomeModule,
    ModalEditMasterListComponent
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
  faTimes = faTimes;

  // Data
  lists: MasterListDto[] = [];
  loading = false;

  // Modal de edición (null = cerrado)
  editListId: number | null = null;

  // Modal ver opciones (lista seleccionada o null = cerrado)
  viewList: MasterListDto | null = null;

  constructor(
    private svc: MasterListService,
    private router: Router,
    library: FaIconLibrary
  ) {
    library.addIcons(faList, faEye, faPen, faTrash, faPlus, faTimes);
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

  // Abre el modal de edición
  edit(list: MasterListDto): void {
    this.editListId = list.id;
  }

  closeEditModal(): void {
    this.editListId = null;
  }

  onEditSaved(): void {
    this.editListId = null;
    this.loadLists();
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

  // Abre el modal de ver opciones
  openViewModal(list: MasterListDto): void {
    this.viewList = list;
  }

  closeViewModal(): void {
    this.viewList = null;
  }

  trackByIndex(index: number): number {
    return index;
  }

  trackByOptionId(_index: number, opt: { id: number; value: string }): number {
    return opt.id;
  }
}

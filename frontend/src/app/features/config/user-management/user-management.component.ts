// src/app/features/config/user-management/user-management.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidebarComponent } from '../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faUserPlus, faUser, faUsers, faIdCard, faEnvelope, faPhone,
  faSave, faTimes, faTrash, faSearch, faBuilding, faEdit
} from '@fortawesome/free-solid-svg-icons';

import { TablaListaRegistroComponent } from './pages/tabla-lista-registro/tabla-lista-registro.component';
import { ModalCreateEditComponent } from './components/modal-create-edit/modal-create-edit.component';
import { AsignarUsuarioGranjaComponent } from './components/asignar-usuario-granja/asignar-usuario-granja.component';
import { UserListItem } from '../../../core/services/user/user.service';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, SidebarComponent, FontAwesomeModule, TablaListaRegistroComponent, ModalCreateEditComponent, AsignarUsuarioGranjaComponent],
  templateUrl: './user-management.component.html',
  styleUrls: ['./user-management.component.scss']
})
export class UserManagementComponent implements OnInit {
  // Iconos
  faUserPlus = faUserPlus;  faUser = faUser;  faUsers = faUsers;  faIdCard = faIdCard;
  faEnvelope = faEnvelope;  faPhone = faPhone; faSave = faSave;   faTimes = faTimes;
  faTrash = faTrash;        faSearch = faSearch; faBuilding = faBuilding; faEdit = faEdit;

  // Estado de navegación
  currentPage: 'list' | 'create' | 'edit' = 'list';
  selectedUserId: string | null = null;

  // Estado del modal
  modalOpen = false;
  editingUser: UserListItem | null = null;
  loading = false;

  // Modal de asignación de granjas
  farmModalOpen = false;
  selectedUserForFarms: UserListItem | null = null;

  constructor(private library: FaIconLibrary) {
    library.addIcons(
      faUserPlus, faUser, faUsers, faIdCard, faEnvelope, faPhone,
      faSave, faTimes, faTrash, faSearch, faBuilding, faEdit
    );
  }

  ngOnInit(): void {
    // Inicialización básica
  }

  navigateToList(): void {
    this.currentPage = 'list';
    this.selectedUserId = null;
  }

  navigateToCreate(): void {
    this.editingUser = null;
    this.modalOpen = true;
  }

  navigateToEdit(user: UserListItem): void {
    this.editingUser = user;
    this.modalOpen = true;
  }

  navigateToAssignFarms(user: UserListItem): void {
    this.selectedUserForFarms = user;
    this.farmModalOpen = true;
  }

  closeFarmModal(): void {
    this.farmModalOpen = false;
    this.selectedUserForFarms = null;
  }

  onFarmsUpdated(): void {
    console.log('Granjas actualizadas');
    // Aquí podrías recargar la lista de usuarios si es necesario
  }

  getUserCompanyId(user: UserListItem): number {
    // Por ahora usar companyId = 1 como default
    // En el futuro se podría obtener de otra fuente o agregar companyIds a UserListItem
    return 1;
  }

  openModal(user?: UserListItem): void {
    this.editingUser = user || null;
    this.modalOpen = true;
  }

  closeModal(): void {
    this.modalOpen = false;
    this.editingUser = null;
  }

  onUserSaved(user: UserListItem): void {
    console.log('Usuario guardado:', user);
    this.closeModal();
    // Aquí podrías emitir un evento para recargar la lista
  }
}
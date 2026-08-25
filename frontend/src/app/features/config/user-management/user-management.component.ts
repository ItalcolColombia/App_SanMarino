// src/app/features/config/user-management/user-management.component.ts
import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faUserPlus, faUser, faUsers, faIdCard, faEnvelope, faPhone,
  faSave, faTimes, faTrash, faSearch, faBuilding, faEdit
} from '@fortawesome/free-solid-svg-icons';

import { TablaListaRegistroComponent } from './pages/tabla-lista-registro/tabla-lista-registro.component';
import { ModalCreateEditComponent } from './components/modal-create-edit/modal-create-edit.component';
import { AsignarUsuarioGranjaComponent } from './components/asignar-usuario-granja/asignar-usuario-granja.component';
import { ModalResetPasswordComponent } from './components/modal-reset-password/modal-reset-password.component';
import { SesionesUsuarioComponent } from './components/sesiones-usuario/sesiones-usuario.component';
import { UserListItem } from '../../../core/services/user/user.service';
import { HasPermissionDirective } from '../../../core/auth/has-permission.directive';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [FontAwesomeModule, TablaListaRegistroComponent, ModalCreateEditComponent, AsignarUsuarioGranjaComponent, ModalResetPasswordComponent, SesionesUsuarioComponent, HasPermissionDirective],
  templateUrl: './user-management.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./user-management.component.scss']
})
export class UserManagementComponent implements OnInit {
  /**
   * Permiso de ESCRITURA del módulo. El botón «Nuevo Usuario» vive acá, en el componente PADRE, y
   * no en la tabla: gatear solo el hijo dejaría el alta abierta.
   */
  readonly PERM_GESTIONAR = 'usuarios.gestionar';
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

  // Modal de reset de contraseña
  resetPasswordModalOpen = false;
  selectedUserForReset: UserListItem | null = null;

  // Modal de sesiones activas (B1): es lo que se abre cuando reportan una tablet perdida.
  sesionesModalOpen = false;
  selectedUserForSesiones: UserListItem | null = null;

  /**
   * Modo SOLO LECTURA del modal de usuario. Es lo único que le queda a quien no tiene
   * `usuarios.gestionar`, y es lo que hace que «sin el permiso solo pueden ver el detalle»
   * signifique algo: hasta hoy la única forma de mirar un usuario era abrir el formulario que
   * también lo edita.
   */
  modalSoloLectura = false;

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
    this.modalSoloLectura = false;
    this.modalOpen = true;
  }

  navigateToEdit(user: UserListItem): void {
    this.editingUser = user;
    this.modalSoloLectura = false;
    this.modalOpen = true;
  }

  /** Abre el MISMO modal, pero sin poder escribir nada. Disponible para cualquier sesión. */
  verDetalleUsuario(user: UserListItem): void {
    this.editingUser = user;
    this.modalSoloLectura = true;
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
    
  }

  openResetPasswordModal(user: UserListItem): void {
    this.selectedUserForReset = user;
    this.resetPasswordModalOpen = true;
  }

  closeResetPasswordModal(): void {
    this.resetPasswordModalOpen = false;
    this.selectedUserForReset = null;
  }

  openSesionesModal(user: UserListItem): void {
    this.selectedUserForSesiones = user;
    this.sesionesModalOpen = true;
  }

  closeSesionesModal(): void {
    this.sesionesModalOpen = false;
    this.selectedUserForSesiones = null;
  }

  getUserCompanyId(user: UserListItem): number {
    // Por ahora usar companyId = 1 como default
    // En el futuro se podría obtener de otra fuente o agregar companyIds a UserListItem
    return 1;
  }

  openModal(user?: UserListItem): void {
    this.editingUser = user || null;
    this.modalSoloLectura = false;
    this.modalOpen = true;
  }

  closeModal(): void {
    this.modalOpen = false;
    this.editingUser = null;
    // Se resetea al cerrar: si no, abrir «Ver detalle» y después «Editar» dejaría el formulario
    // deshabilitado sin motivo visible.
    this.modalSoloLectura = false;
  }

  onUserSaved(user: UserListItem): void {
    
    this.closeModal();
    // Aquí podrías emitir un evento para recargar la lista
  }
}
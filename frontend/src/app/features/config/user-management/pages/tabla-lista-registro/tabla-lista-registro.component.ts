// src/app/features/config/user-management/pages/tabla-lista-registro/tabla-lista-registro.component.ts
import { Component, OnInit, OnDestroy, ChangeDetectorRef, inject, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faUserPlus, faUser, faUsers, faIdCard, faEnvelope, faPhone,
  faSave, faTimes, faTrash, faSearch, faBuilding, faEdit
} from '@fortawesome/free-solid-svg-icons';

import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';

import { UserService, UserListItem } from '../../../../../core/services/user/user.service';
import { Company, CompanyService } from '../../../../../core/services/company/company.service';
import { RoleService, Role } from '../../../../../core/services/role/role.service';
import { AsignarUsuarioGranjaComponent } from '../../components/asignar-usuario-granja/asignar-usuario-granja.component';

@Component({
  selector: 'app-tabla-lista-registro',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule, AsignarUsuarioGranjaComponent],
  templateUrl: './tabla-lista-registro.component.html',
  styleUrls: ['./tabla-lista-registro.component.scss']
})
export class TablaListaRegistroComponent implements OnInit, OnDestroy {
  @Output() createUser = new EventEmitter<void>();
  @Output() editUser = new EventEmitter<UserListItem>();
  @Output() assignFarms = new EventEmitter<UserListItem>();

  // Iconos
  faUserPlus = faUserPlus;
  faUser = faUser;
  faUsers = faUsers;
  faIdCard = faIdCard;
  faEnvelope = faEnvelope;
  faPhone = faPhone;
  faSave = faSave;
  faTimes = faTimes;
  faTrash = faTrash;
  faSearch = faSearch;
  faBuilding = faBuilding;
  faEdit = faEdit;

  // Estado
  loading = false;
  filterTerm = '';
  
  // Datos
  users: UserListItem[] = [];
  filteredUsers: UserListItem[] = [];
  
  // Modal de asignación de granjas
  asignarGranjaModalOpen = false;
  selectedUser: UserListItem | null = null;

  // Servicios
  private userService = inject(UserService);
  private companyService = inject(CompanyService);
  private roleService = inject(RoleService);
  private cdr = inject(ChangeDetectorRef);

  constructor(private library: FaIconLibrary) {
    library.addIcons(
      faUserPlus, faUser, faUsers, faIdCard, faEnvelope, faPhone,
      faSave, faTimes, faTrash, faSearch, faBuilding, faEdit
    );
  }

  ngOnInit(): void {
    this.loadUsers();
  }

  ngOnDestroy(): void {
    // Cleanup if needed
  }

  loadUsers(): void {
    this.loading = true;
    
    this.userService.getAll()
      .pipe(
        catchError(error => {
          console.error('Error loading users:', error);
          return of([]);
        }),
        finalize(() => {
          this.loading = false;
          this.cdr.detectChanges();
        })
      )
      .subscribe((users: any) => {
        this.users = users;
        this.applyFilter();
      });
  }

  applyFilter(): void {
    if (!this.filterTerm.trim()) {
      this.filteredUsers = [...this.users];
      return;
    }

    const term = this.filterTerm.toLowerCase();
    this.filteredUsers = this.users.filter(user =>
      user.firstName.toLowerCase().includes(term) ||
      (user.surName && user.surName.toLowerCase().includes(term)) ||
      user.email.toLowerCase().includes(term) ||
      (user.cedula && user.cedula.toLowerCase().includes(term))
    );
  }

  onFilterChange(): void {
    this.applyFilter();
  }

  onAssignFarmsClick(user: UserListItem): void {
    this.assignFarms.emit(user);
  }

  closeAsignarGranjaModal(): void {
    this.asignarGranjaModalOpen = false;
    this.selectedUser = null;
  }

  onGranjasUpdated(): void {
    // Recargar usuarios para obtener información actualizada
    this.loadUsers();
  }

  onCreateUserClick(): void {
    this.createUser.emit();
  }

  onEditUserClick(user: UserListItem): void {
    this.editUser.emit(user);
  }

  deleteUser(user: UserListItem): void {
    if (!confirm(`¿Está seguro de que desea eliminar al usuario ${user.firstName} ${user.surName}?`)) {
      return;
    }

    this.userService.delete(user.id)
      .pipe(
        catchError(error => {
          console.error('Error deleting user:', error);
          return of(false);
        })
      )
      .subscribe((success: any) => {
        if (success) {
          this.loadUsers();
        }
      });
  }

  getPrimaryCompany(user: UserListItem): string {
    return user.companyNames?.[0] || 'Sin compañía';
  }

  getPrimaryRole(user: UserListItem): string {
    return user.roles?.[0] || 'Sin rol';
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('es-CO');
  }

  getStatusBadgeClass(isActive: boolean): string {
    return isActive ? 'badge-success' : 'badge-danger';
  }

  getStatusText(isActive: boolean): string {
    return isActive ? 'Activo' : 'Inactivo';
  }

  trackByUserId(index: number, user: UserListItem): string {
    return user.id;
  }
}

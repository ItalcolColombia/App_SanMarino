// src/app/features/config/user-management/pages/tabla-lista-registro/tabla-lista-registro.component.ts
import { Component, OnInit, OnDestroy, ChangeDetectorRef, inject, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { ConfirmDialogService } from '../../../../../shared/services/confirm-dialog.service';

import { FormsModule } from '@angular/forms';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faUserPlus, faUser, faUsers, faIdCard, faEnvelope, faPhone,
  faSave, faTimes, faTrash, faSearch, faBuilding, faEdit, faLock, faDesktop, faEye
} from '@fortawesome/free-solid-svg-icons';

import { forkJoin, of, interval, Subject, Observable } from 'rxjs';
import { catchError, finalize, takeUntil } from 'rxjs/operators';

import { UserService, UserListItem } from '../../../../../core/services/user/user.service';
import { Company, CompanyService } from '../../../../../core/services/company/company.service';
import { RoleService, Role } from '../../../../../core/services/role/role.service';
import { AsignarUsuarioGranjaComponent } from '../../components/asignar-usuario-granja/asignar-usuario-granja.component';
import { AuthService } from '../../../../../core/auth/auth.service';
import { EmailQueueStatus } from '../../../../../core/auth/auth.models';
import { HasPermissionDirective } from '../../../../../core/auth/has-permission.directive';

@Component({
  selector: 'app-tabla-lista-registro',
  standalone: true,
  imports: [FormsModule, FontAwesomeModule, AsignarUsuarioGranjaComponent, HasPermissionDirective],
  templateUrl: './tabla-lista-registro.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabla-lista-registro.component.scss']
})
export class TablaListaRegistroComponent implements OnInit, OnDestroy {
  /**
   * Permiso que separa VER de ESCRIBIR en este módulo. Sin él la pantalla sigue siendo visible
   * (el acceso lo da `role_menus`), pero solo se puede consultar el listado y el detalle.
   */
  readonly PERM_GESTIONAR = 'usuarios.gestionar';

  /**
   * Permiso de las sesiones activas. Es una key APARTE, y a propósito: el backend
   * (`SessionController`) ya la exige para ver y revocar sesiones de otro usuario, así que meter
   * esos tres botones bajo `usuarios.gestionar` crearía dos llaves para la misma puerta y el botón
   * seguiría dando 403 para quien tuviera una y no la otra.
   */
  readonly PERM_SESIONES = 'usuarios.revocar_sesion';

  @Output() createUser = new EventEmitter<void>();
  @Output() editUser = new EventEmitter<UserListItem>();
  @Output() assignFarms = new EventEmitter<UserListItem>();
  @Output() resetPassword = new EventEmitter<UserListItem>();
  /** B1: abrir las sesiones abiertas de ese usuario para poder revocar la tablet que se perdió. */
  @Output() verSesiones = new EventEmitter<UserListItem>();
  /**
   * Ver el detalle del usuario en modo SOLO LECTURA. Es lo único que queda para quien no tiene
   * `usuarios.gestionar`, y hasta el 25-ago-2026 no existía: el módulo no tenía ninguna forma de
   * mirar un usuario sin abrir el formulario que lo edita.
   */
  @Output() verDetalle = new EventEmitter<UserListItem>();

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
  faLock = faLock;
  faDesktop = faDesktop;
  faEye = faEye;

  // Estado
  loading = false;
  filterTerm = '';

  // Datos
  users: UserListItem[] = [];
  filteredUsers: UserListItem[] = [];

  // Modal de asignación de granjas
  asignarGranjaModalOpen = false;
  selectedUser: UserListItem | null = null;

  // Estado de correos por usuario
  emailStatuses: Map<string, 'pending' | 'processing' | 'sent' | 'failed'> = new Map();
  checkingEmails: Set<string> = new Set();
  private emailStatusSubscriptions: Map<string, any> = new Map();
  private destroy$ = new Subject<void>();

  // Servicios
  private userService = inject(UserService);
  private companyService = inject(CompanyService);
  private roleService = inject(RoleService);
  private authService = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);

  constructor(private confirmDialog: ConfirmDialogService, private library: FaIconLibrary) {
    library.addIcons(
      faUserPlus, faUser, faUsers, faIdCard, faEnvelope, faPhone,
      faSave, faTimes, faTrash, faSearch, faBuilding, faEdit, faLock, faDesktop
    );
  }

  ngOnInit(): void {
    this.loadUsers();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    // Limpiar todas las suscripciones de estado de correo
    this.emailStatusSubscriptions.forEach(sub => sub.unsubscribe());
    this.emailStatusSubscriptions.clear();
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

        // Iniciar verificación de estado de correos para usuarios con emailQueueId
        users.forEach((user: UserListItem) => {
          if (user.emailQueueId && !this.emailStatuses.has(user.id)) {
            this.startEmailStatusCheck(user.id, user.emailQueueId);
          }
        });
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

  onVerDetalleClick(user: UserListItem): void {
    this.verDetalle.emit(user);
  }

  onSesionesClick(user: UserListItem): void {
    this.verSesiones.emit(user);
  }

  onResetPasswordClick(user: UserListItem): void {
    this.resetPassword.emit(user);
  }

  async deleteUser(user: UserListItem): Promise<void> {
    if (!(await this.confirmDialog.ask({ title: 'Eliminar usuario', message: `¿Está seguro de que desea eliminar al usuario ${user.firstName} ${user.surName}?`, type: 'warning', confirmText: 'Eliminar' }))) {
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

  /**
   * Inicia la verificación periódica del estado del correo para un usuario
   */
  private startEmailStatusCheck(userId: string, emailQueueId: number): void {
    // Si ya hay una suscripción activa, no crear otra
    if (this.emailStatusSubscriptions.has(userId)) return;

    // Consultar inmediatamente
    this.checkEmailStatus(userId, emailQueueId);

    // Consultar cada 5 segundos si está pendiente o procesando
    const subscription = interval(5000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        const currentStatus = this.emailStatuses.get(userId);
        if (currentStatus === 'pending' || currentStatus === 'processing') {
          this.checkEmailStatus(userId, emailQueueId);
        } else {
          // Si ya se envió o falló, detener la verificación
          this.stopEmailStatusCheck(userId);
        }
      });

    this.emailStatusSubscriptions.set(userId, subscription);
  }

  /**
   * Detiene la verificación periódica del estado del correo para un usuario
   */
  private stopEmailStatusCheck(userId: string): void {
    const subscription = this.emailStatusSubscriptions.get(userId);
    if (subscription) {
      subscription.unsubscribe();
      this.emailStatusSubscriptions.delete(userId);
    }
    this.checkingEmails.delete(userId);
  }

  /**
   * Consulta el estado actual del correo para un usuario
   */
  private checkEmailStatus(userId: string, emailQueueId: number): void {
    if (this.checkingEmails.has(userId)) return;

    this.checkingEmails.add(userId);

    this.authService.getEmailStatus(emailQueueId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (status: EmailQueueStatus) => {
          this.emailStatuses.set(userId, status.status);
          this.checkingEmails.delete(userId);
          this.cdr.detectChanges();

          // Si se envió o falló, detener la verificación
          if (status.status === 'sent' || status.status === 'failed') {
            this.stopEmailStatusCheck(userId);
          }
        },
        error: (error: unknown) => {
          console.error(`Error al consultar estado del correo para usuario ${userId}:`, error);
          this.checkingEmails.delete(userId);
        }
      });
  }

  /**
   * Obtiene el estado del correo para un usuario
   */
  getEmailStatus(userId: string): 'pending' | 'processing' | 'sent' | 'failed' | null {
    return this.emailStatuses.get(userId) || null;
  }

  /**
   * Obtiene el icono y color del estado del correo para un usuario
   */
  getEmailStatusIcon(user: UserListItem): { icon: any; color: string; tooltip: string } | null {
    // Solo mostrar icono si tiene emailQueueId (usuarios creados después de la implementación)
    if (!user.emailQueueId) {
      return null;
    }

    const status = this.getEmailStatus(user.id) || (user.emailSent ? 'sent' : 'pending');

    switch (status) {
      case 'sent':
        return {
          icon: this.faEnvelope,
          color: '#10b981', // verde
          tooltip: 'Correo enviado exitosamente'
        };
      case 'failed':
        return {
          icon: this.faEnvelope,
          color: '#ef4444', // rojo
          tooltip: 'Error al enviar el correo'
        };
      case 'pending':
      case 'processing':
        return {
          icon: this.faEnvelope,
          color: '#6b7280', // gris
          tooltip: 'Correo en proceso de envío'
        };
      default:
        return null;
    }
  }
}

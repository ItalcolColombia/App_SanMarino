// src/app/features/config/user-management/components/modal-create-edit/modal-create-edit.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidatorFn } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faUserPlus, faUser, faSave, faTimes, faEnvelope, faPhone, faIdCard, faBuilding, faUsers,
  faEye, faEyeSlash, faCheck, faExclamationTriangle
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil, forkJoin, switchMap, take } from 'rxjs';

import { UserService, UserListItem, CreateUserDto, UpdateUserDto } from '../../../../../core/services/user/user.service';
import { Company, CompanyService } from '../../../../../core/services/company/company.service';
import { RoleService, Role } from '../../../../../core/services/role/role.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { EmailQueueStatus } from '../../../../../core/auth/auth.models';
import { interval, Subscription } from 'rxjs';

// === Validador: array requerido (>=1 ítem) ===
const requiredArray: ValidatorFn = (ctrl: AbstractControl) => {
  const v = ctrl.value;
  return Array.isArray(v) && v.length > 0 ? null : { required: true };
};

// === Validador: confirmar contraseña ===
const match = (field: string): ValidatorFn => (ctrl: AbstractControl) => {
  const parent = ctrl.parent as FormGroup | null;
  if (!parent) return null;
  const target = parent.get(field);
  return target && ctrl.value === target.value ? null : { mismatch: true };
};

@Component({
  selector: 'app-modal-create-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, FontAwesomeModule],
  templateUrl: './modal-create-edit.component.html',
  styleUrls: ['./modal-create-edit.component.scss']
})
export class ModalCreateEditComponent implements OnInit, OnDestroy {
  @Input() isOpen: boolean = false;
  @Input() editingUser: UserListItem | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() userSaved = new EventEmitter<UserListItem>();

  // Iconos
  faUserPlus = faUserPlus;
  faUser = faUser;
  faSave = faSave;
  faTimes = faTimes;
  faEnvelope = faEnvelope;
  faPhone = faPhone;
  faIdCard = faIdCard;
  faBuilding = faBuilding;
  faUsers = faUsers;
  faEye = faEye;
  faEyeSlash = faEyeSlash;
  faCheck = faCheck;
  faExclamationTriangle = faExclamationTriangle;

  // Estado
  loading = false;
  saving = false;
  showPassword = false;
  showConfirmPassword = false;
  activeTab: 'personal' | 'access' | 'roles' = 'personal';

  // Estado del correo
  emailQueueId: number | null = null;
  emailStatus: 'pending' | 'processing' | 'sent' | 'failed' | null = null;
  checkingEmailStatus = false;
  private emailStatusSubscription?: Subscription;

  // Datos
  companies: Company[] = [];
  roles: Role[] = [];
  filteredRoles: Role[] = [];

  // Formulario
  userForm!: FormGroup;

  private destroy$ = new Subject<void>();
  private fb = inject(FormBuilder);
  private userService = inject(UserService);
  private companyService = inject(CompanyService);
  private roleService = inject(RoleService);
  private authService = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);

  ngOnInit(): void {
    this.initForm();
    this.loadLookups();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.stopEmailStatusCheck();
  }

  ngOnChanges(): void {
    if (this.isOpen) {
      this.loadLookups();
      if (this.editingUser) {
        this.loadUserData();
      } else {
        this.resetForm();
      }
    }
  }

  initForm(): void {
    this.userForm = this.fb.group({
      surName: ['', [Validators.required, Validators.minLength(2)]],
      firstName: ['', [Validators.required, Validators.minLength(2)]],
      cedula: ['', [Validators.required, Validators.pattern(/^\d{7,10}$/)]],
      telefono: ['', [Validators.pattern(/^\d{10}$/)]],
      ubicacion: [''],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', [Validators.required, match('password')]],
      companyIds: [[], [requiredArray]],
      roleIds: [[], [requiredArray]]
    });
  }

  loadLookups(): void {
    this.loading = true;

    // Obtener información del usuario en sesión y cargar datos según rol
    this.authService.session$
      .pipe(
        take(1), // Solo tomar el primer valor (valor actual)
        switchMap((session) => {
          const userRoles = session?.user?.roles || [];
          
          // Verificar si el usuario es admin o administrador (case-insensitive)
          const isAdmin = userRoles.some(role => 
            role && (role.toLowerCase() === 'admin' || role.toLowerCase() === 'administrador')
          );

          // Si es admin/administrador, obtener todas las empresas
          // Si no, obtener solo las empresas del usuario en sesión (ya filtradas por backend)
          const companiesObservable = isAdmin 
            ? this.companyService.getAllForAdmin()
            : this.companyService.getAll();

          return forkJoin({
            companies: companiesObservable,
            roles: this.roleService.getAll()
          });
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (data) => {
          this.companies = data.companies;
          this.roles = data.roles;
          this.filteredRoles = data.roles;
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: (error) => {
          console.error('Error loading lookups:', error);
          // Fallback: cargar empresas normales si hay error
          forkJoin({
            companies: this.companyService.getAll(),
            roles: this.roleService.getAll()
          })
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: (data) => {
              this.companies = data.companies;
              this.roles = data.roles;
              this.filteredRoles = data.roles;
              this.loading = false;
              this.cdr.detectChanges();
            },
            error: (err) => {
              console.error('Error loading lookups (fallback):', err);
              this.loading = false;
            }
          });
        }
      });
  }

  loadUserData(): void {
    if (!this.editingUser) return;

    this.loading = true;

    this.userService.getById(this.editingUser.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (userDetail) => {
          this.userForm.patchValue({
            surName: userDetail.surName,
            firstName: userDetail.firstName,
            cedula: userDetail.cedula,
            telefono: userDetail.telefono,
            ubicacion: userDetail.ubicacion,
            email: this.editingUser?.email || '', // Usar email del usuario original
            companyIds: userDetail.companyIds || [],
            roleIds: this.mapRoleNamesToIds(userDetail.roles || [])
          });

          // En edición, la contraseña no es requerida
          this.userForm.get('password')?.clearValidators();
          this.userForm.get('confirmPassword')?.clearValidators();
          this.userForm.get('password')?.updateValueAndValidity();
          this.userForm.get('confirmPassword')?.updateValueAndValidity();

          this.loading = false;
          this.cdr.detectChanges();
        },
        error: (error) => {
          console.error('Error loading user data:', error);
          this.loading = false;
        }
      });
  }

  resetForm(): void {
    this.userForm.reset({
      surName: '',
      firstName: '',
      cedula: '',
      telefono: '',
      ubicacion: '',
      email: '',
      password: '',
      confirmPassword: '',
      companyIds: [],
      roleIds: []
    });

    // En creación, la contraseña es requerida
    this.userForm.get('password')?.setValidators([Validators.required, Validators.minLength(6)]);
    this.userForm.get('confirmPassword')?.setValidators([Validators.required, match('password')]);
    this.userForm.get('password')?.updateValueAndValidity();
    this.userForm.get('confirmPassword')?.updateValueAndValidity();
  }

  mapRoleNamesToIds(roleNames: string[]): number[] {
    if (!roleNames?.length) return [];
    const mapByName = new Map<string, number>();
    for (const role of this.roles) {
      if (role?.name) mapByName.set(role.name.toLowerCase(), role.id);
    }
    const ids: number[] = [];
    for (const roleName of roleNames) {
      const id = mapByName.get((roleName || '').toLowerCase());
      if (id) ids.push(id);
    }
    return ids;
  }

  saveUser(): void {
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      return;
    }

    this.saving = true;
    const formValue = this.userForm.value;

    if (this.editingUser) {
      // Actualizar usuario existente
      const updateDto: UpdateUserDto = {
        id: this.editingUser.id,
        surName: formValue.surName,
        firstName: formValue.firstName,
        cedula: formValue.cedula,
        telefono: formValue.telefono,
        ubicacion: formValue.ubicacion,
        companyIds: formValue.companyIds,
        roleIds: formValue.roleIds
      };

      this.userService.update(this.editingUser.id, updateDto)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (result: any) => {
            this.saving = false;
            this.userSaved.emit(result);
            this.closeModal();
          },
          error: (error: any) => {
            console.error('Error updating user:', error);
            this.saving = false;
          }
        });
    } else {
      // Crear nuevo usuario
      const createDto: CreateUserDto = {
        surName: formValue.surName,
        firstName: formValue.firstName,
        lastName: formValue.surName || '', // Usar surName como lastName si no hay lastName
        cedula: formValue.cedula,
        telefono: formValue.telefono,
        ubicacion: formValue.ubicacion,
        email: formValue.email,
        password: formValue.password,
        companyIds: formValue.companyIds,
        roleIds: formValue.roleIds
      };

      this.userService.create(createDto)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (result: any) => {
            this.saving = false;

            // Si hay emailQueueId, iniciar verificación del estado del correo
            if (result?.emailQueueId) {
              this.emailQueueId = result.emailQueueId;
              this.emailStatus = result.emailSent ? 'sent' : 'pending';
              this.startEmailStatusCheck();
            }

            this.userSaved.emit(result);
            // No cerrar el modal inmediatamente si hay correo pendiente
            if (!result?.emailQueueId || result?.emailSent) {
              this.closeModal();
            }
          },
          error: (error: any) => {
            console.error('Error creating user:', error);
            this.saving = false;
          }
        });
    }
  }

  closeModal(): void {
    this.stopEmailStatusCheck();
    this.emailQueueId = null;
    this.emailStatus = null;
    this.close.emit();
  }

  /**
   * Inicia la verificación periódica del estado del correo
   */
  private startEmailStatusCheck(): void {
    if (!this.emailQueueId) return;

    this.stopEmailStatusCheck(); // Asegurar que no hay otra suscripción activa

    // Consultar inmediatamente
    this.checkEmailStatus();

    // Consultar cada 3 segundos si está pendiente o procesando
    this.emailStatusSubscription = interval(3000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (this.emailStatus === 'pending' || this.emailStatus === 'processing') {
          this.checkEmailStatus();
        } else {
          // Si ya se envió o falló, detener la verificación
          this.stopEmailStatusCheck();
        }
      });
  }

  /**
   * Detiene la verificación periódica del estado del correo
   */
  private stopEmailStatusCheck(): void {
    if (this.emailStatusSubscription) {
      this.emailStatusSubscription.unsubscribe();
      this.emailStatusSubscription = undefined;
    }
  }

  /**
   * Consulta el estado actual del correo
   */
  private checkEmailStatus(): void {
    if (!this.emailQueueId || this.checkingEmailStatus) return;

    this.checkingEmailStatus = true;

    this.authService.getEmailStatus(this.emailQueueId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (status: EmailQueueStatus) => {
          this.emailStatus = status.status;
          this.checkingEmailStatus = false;
          this.cdr.detectChanges();

          // Si se envió o falló, detener la verificación
          if (this.emailStatus === 'sent' || this.emailStatus === 'failed') {
            this.stopEmailStatusCheck();
          }
        },
        error: (error: unknown) => {
          console.error('Error al consultar estado del correo:', error);
          this.checkingEmailStatus = false;
        }
      });
  }

  /**
   * Obtiene el icono y color del estado del correo
   */
  getEmailStatusIcon(): { icon: any; color: string; tooltip: string } {
    if (!this.emailQueueId) {
      return { icon: null, color: '', tooltip: '' };
    }

    switch (this.emailStatus) {
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
        return {
          icon: this.faEnvelope,
          color: '#6b7280',
          tooltip: 'Verificando estado del correo...'
        };
    }
  }

  setActiveTab(tab: 'personal' | 'access' | 'roles'): void {
    this.activeTab = tab;
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  toggleConfirmPasswordVisibility(): void {
    this.showConfirmPassword = !this.showConfirmPassword;
  }

  toggleCompany(companyId: number): void {
    const currentIds = this.userForm.get('companyIds')?.value || [];
    const index = currentIds.indexOf(companyId);

    if (index > -1) {
      currentIds.splice(index, 1);
    } else {
      currentIds.push(companyId);
    }

    this.userForm.get('companyIds')?.setValue([...currentIds]);
    this.filterRolesByCompanies();
  }

  toggleRole(roleId: number): void {
    const currentIds = this.userForm.get('roleIds')?.value || [];
    const index = currentIds.indexOf(roleId);

    if (index > -1) {
      currentIds.splice(index, 1);
    } else {
      currentIds.push(roleId);
    }

    this.userForm.get('roleIds')?.setValue([...currentIds]);
  }

  filterRolesByCompanies(): void {
    const selectedCompanyIds = this.userForm.get('companyIds')?.value || [];

    if (selectedCompanyIds.length === 0) {
      this.filteredRoles = [];
    } else {
      this.filteredRoles = this.roles.filter(role =>
        role.companyIds.some(companyId => selectedCompanyIds.includes(companyId))
      );
    }

    // Limpiar roles seleccionados que ya no están disponibles
    const currentRoleIds = this.userForm.get('roleIds')?.value || [];
    const availableRoleIds = this.filteredRoles.map(r => r.id);
    const validRoleIds = currentRoleIds.filter((id: number) => availableRoleIds.includes(id));

    if (validRoleIds.length !== currentRoleIds.length) {
      this.userForm.get('roleIds')?.setValue(validRoleIds);
    }
  }

  getFieldError(fieldName: string): string {
    const field = this.userForm.get(fieldName);
    if (!field || !field.errors || !field.touched) return '';

    const errors = field.errors;
    if (errors['required']) return `${this.getFieldLabel(fieldName)} es requerido`;
    if (errors['minlength']) return `${this.getFieldLabel(fieldName)} debe tener al menos ${errors['minlength'].requiredLength} caracteres`;
    if (errors['email']) return 'Email inválido';
    if (errors['pattern']) return `${this.getFieldLabel(fieldName)} tiene formato inválido`;
    if (errors['mismatch']) return 'Las contraseñas no coinciden';
    if (errors['required'] && fieldName.includes('Ids')) return 'Debe seleccionar al menos una opción';

    return 'Campo inválido';
  }

  getFieldLabel(fieldName: string): string {
    const labels: { [key: string]: string } = {
      surName: 'Apellido',
      firstName: 'Nombre',
      cedula: 'Cédula',
      telefono: 'Teléfono',
      ubicacion: 'Ubicación',
      email: 'Email',
      password: 'Contraseña',
      confirmPassword: 'Confirmar contraseña',
      companyIds: 'Empresas',
      roleIds: 'Roles'
    };
    return labels[fieldName] || fieldName;
  }

  get isEditing(): boolean {
    return !!this.editingUser;
  }

  get modalTitle(): string {
    return this.isEditing ? 'Editar Usuario' : 'Crear Usuario';
  }

  get submitButtonText(): string {
    return this.isEditing ? 'Actualizar' : 'Crear';
  }

  get selectedCompanies(): Company[] {
    const selectedIds = this.userForm.get('companyIds')?.value || [];
    return this.companies.filter(c => selectedIds.includes(c.id));
  }

  get selectedRoles(): Role[] {
    const selectedIds = this.userForm.get('roleIds')?.value || [];
    return this.filteredRoles.filter(r => selectedIds.includes(r.id));
  }

  // Métodos para validación de tabs
  isTabValid(tab: 'personal' | 'access' | 'roles'): boolean {
    switch (tab) {
      case 'personal':
        return !!(this.userForm.get('firstName')?.valid &&
                 this.userForm.get('surName')?.valid &&
                 this.userForm.get('cedula')?.valid);
      case 'access':
        return !!(this.userForm.get('email')?.valid &&
                 (this.isEditing || this.userForm.get('password')?.valid));
      case 'roles':
        return !!(this.userForm.get('companyIds')?.valid &&
                 this.userForm.get('roleIds')?.valid);
      default:
        return false;
    }
  }

  getTabErrorCount(tab: 'personal' | 'access' | 'roles'): number {
    switch (tab) {
      case 'personal':
        return [this.userForm.get('firstName'), this.userForm.get('surName'), this.userForm.get('cedula')]
          .filter(field => field?.invalid && field?.touched).length;
      case 'access':
        return [this.userForm.get('email'), this.userForm.get('password'), this.userForm.get('confirmPassword')]
          .filter(field => field?.invalid && field?.touched).length;
      case 'roles':
        return [this.userForm.get('companyIds'), this.userForm.get('roleIds')]
          .filter(field => field?.invalid && field?.touched).length;
      default:
        return 0;
    }
  }
}

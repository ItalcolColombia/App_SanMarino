import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import { faUserCircle, faUserEdit, faLock, faSpinner, faSave, faExclamationCircle, faCheckCircle, faCircle } from '@fortawesome/free-solid-svg-icons';
import { TokenStorageService } from '../../core/auth/token-storage.service';
import { UserProfileService, UpdateUserDto, ChangePasswordDto } from '../../core/services/user/user-profile.service';
import { SidebarComponent } from '../../shared/components/sidebar/sidebar.component';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../shared/components/confirmation-modal/confirmation-modal.component';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, FontAwesomeModule, SidebarComponent, ConfirmationModalComponent],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss']
})
export class ProfileComponent implements OnInit {
  private fb = inject(FormBuilder);
  private storage = inject(TokenStorageService);
  private profile = inject(UserProfileService);

  constructor(library: FaIconLibrary) {
    library.addIcons(faUserCircle, faUserEdit, faLock, faSpinner, faSave, faExclamationCircle, faCheckCircle, faCircle);
  }

  // Validador personalizado para contraseñas fuertes
  private strongPasswordValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null;

    const errors: ValidationErrors = {};
    
    // Mínimo 6 caracteres
    if (value.length < 6) {
      errors['minLength'] = { requiredLength: 6, actualLength: value.length };
    }
    
    // Al menos 1 número
    if (!/\d/.test(value)) {
      errors['requireNumber'] = true;
    }
    
    // Al menos 1 letra (mayúscula o minúscula)
    if (!/[a-zA-Z]/.test(value)) {
      errors['requireLetter'] = true;
    }
    
    // Al menos 1 carácter especial (opcional pero recomendado)
    if (!/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(value)) {
      errors['requireSpecialChar'] = true;
    }

    return Object.keys(errors).length > 0 ? errors : null;
  }

  loading = signal(false);
  userId = '';

  infoForm = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    surName:   ['', [Validators.required, Validators.maxLength(100)]],
    cedula:    ['', [Validators.maxLength(50)]],
    telefono:  ['', [Validators.maxLength(50)]],
    ubicacion: ['', [Validators.maxLength(200)]]
  });

  passwordForm = this.fb.group({
    currentPassword: ['', [Validators.required, Validators.minLength(6)]],
    newPassword:     ['', [Validators.required, this.strongPasswordValidator]]
  });

  infoErr = '';
  passErr = '';
  // dentro de ProfileComponent:
  embedded = false; // o @Input() embedded = false; si lo controlas desde el padre


  // Modal properties
  showModal = signal(false);
  modalData: ConfirmationModalData = {
    title: '',
    message: '',
    confirmText: 'Entendido',
    cancelText: '',
    type: 'success'
  };

  ngOnInit(): void {
    this.storage.session$.subscribe(s => {
      if (!s?.user?.id) {
        console.log('No session or user ID found:', s);
        return;
      }
      this.userId = s.user.id;
      console.log('User ID loaded:', this.userId);
      this.loadUser();
    });
  }

  private loadUser() {
    if (!this.userId) {
      console.log('No userId available for loading user data');
      return;
    }
    console.log('Loading user data for ID:', this.userId);
    this.loading.set(true);
    this.profile.getById(this.userId).subscribe({
      next: (u) => {
        console.log('User data loaded:', u);
        this.infoForm.patchValue({
          firstName: u.firstName,
          surName:   u.surName,
          cedula:    u.cedula,
          telefono:  u.telefono,
          ubicacion: u.ubicacion
        });
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading user data:', err);
        this.loading.set(false);
        this.infoErr = 'No se pudo cargar el perfil.';
      }
    });
  }

  saveInfo() {
    if (this.infoForm.invalid || !this.userId) {
      this.infoForm.markAllAsTouched();
      return;
    }
    this.infoErr = '';
    this.loading.set(true);
    const v = this.infoForm.value;
    const dto: UpdateUserDto = {
      firstName: v.firstName ?? undefined,
      surName:   v.surName   ?? undefined,
      cedula:    v.cedula    ?? undefined,
      telefono:  v.telefono  ?? undefined,
      ubicacion: v.ubicacion ?? undefined,
    };
    
    // Actualizar datos del usuario en el backend
    console.log('🚀 Enviando datos al backend:', dto);
    this.profile.update(this.userId, dto).subscribe({
      next: (updatedUser) => {
        this.loading.set(false);
        
        console.log('✅ Backend respondió con usuario actualizado:', updatedUser);
        
        // Actualizar solo los datos del usuario en el storage local
        this.storage.updateUserData({
          firstName: updatedUser.firstName,
          surName: updatedUser.surName
        });
        
        console.log('✅ Datos del usuario actualizados en el storage local');
        this.showSuccessModal('Datos Actualizados', 'Tus datos personales han sido actualizados correctamente.');
      },
      error: (err) => {
        this.loading.set(false);
        console.error('❌ Error al actualizar usuario:', err);
        this.infoErr = err?.error?.message || 'No se pudo actualizar.';
      }
    });
  }

  changePassword() {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    this.passErr = '';
    this.loading.set(true);
    const pv = this.passwordForm.value;
    const dto: ChangePasswordDto = {
      currentPassword: pv.currentPassword || '',
      newPassword: pv.newPassword || ''
    };
    this.profile.changeMyPassword(this.userId, dto).subscribe({
      next: () => {
        this.loading.set(false);
        this.showSuccessModal('Contraseña Cambiada', 'Tu contraseña ha sido cambiada exitosamente. Por seguridad, te recomendamos cerrar sesión y volver a iniciar sesión con tu nueva contraseña.');
        this.passwordForm.reset();
      },
      error: (err) => {
        this.loading.set(false);
        this.passErr = err?.error?.message || 'No se pudo cambiar la contraseña.';
      }
    });
  }

  private showSuccessModal(title: string, message: string): void {
    this.modalData = {
      title,
      message,
      confirmText: 'Entendido',
      cancelText: '',
      type: 'success'
    };
    this.showModal.set(true);
  }

  onModalConfirmed(): void {
    // Modal confirmed - no action needed for success modals
  }

  onModalCancelled(): void {
    // Modal cancelled - no action needed for success modals
  }

  // Métodos para obtener mensajes de error específicos de contraseña
  getPasswordErrorMessage(): string {
    const newPasswordControl = this.passwordForm.get('newPassword');
    if (!newPasswordControl?.errors || !newPasswordControl.touched) {
      return '';
    }

    const errors = newPasswordControl.errors;
    
    if (errors['required']) {
      return 'La nueva contraseña es obligatoria';
    }
    
    if (errors['minLength']) {
      return `La contraseña debe tener al menos ${errors['minLength'].requiredLength} caracteres`;
    }
    
    if (errors['requireNumber']) {
      return 'La contraseña debe contener al menos 1 número';
    }
    
    if (errors['requireLetter']) {
      return 'La contraseña debe contener al menos 1 letra';
    }
    
    if (errors['requireSpecialChar']) {
      return 'La contraseña debe contener al menos 1 carácter especial (!@#$%^&*)';
    }
    
    return 'Contraseña inválida';
  }

  // Método para verificar si la contraseña es válida
  isPasswordValid(): boolean {
    const newPasswordControl = this.passwordForm.get('newPassword');
    return newPasswordControl ? newPasswordControl.valid : false;
  }

  // Métodos auxiliares para validaciones de contraseña
  hasMinLength(value: string | null | undefined): boolean {
    if (!value) return false;
    return value.length >= 6;
  }

  hasNumber(value: string | null | undefined): boolean {
    if (!value) return false;
    return /\d/.test(value);
  }

  hasLetter(value: string | null | undefined): boolean {
    if (!value) return false;
    return /[a-zA-Z]/.test(value);
  }

  hasSpecialChar(value: string | null | undefined): boolean {
    if (!value) return false;
    return /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(value);
  }

  // Método para obtener el nivel de fortaleza de la contraseña
  getPasswordStrength(): { level: number; text: string; color: string } {
    const newPasswordControl = this.passwordForm.get('newPassword');
    const value = newPasswordControl?.value || '';
    
    if (!value) {
      return { level: 0, text: '', color: '#e2e8f0' };
    }
    
    let score = 0;
    const checks = {
      length: value.length >= 6,
      number: /\d/.test(value),
      letter: /[a-zA-Z]/.test(value),
      special: /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(value),
      upper: /[A-Z]/.test(value),
      lower: /[a-z]/.test(value)
    };
    
    // Calcular puntuación
    if (checks.length) score += 1;
    if (checks.number) score += 1;
    if (checks.letter) score += 1;
    if (checks.special) score += 1;
    if (checks.upper && checks.lower) score += 1;
    
    const levels = [
      { level: 0, text: 'Muy débil', color: '#ef4444' },
      { level: 1, text: 'Débil', color: '#f97316' },
      { level: 2, text: 'Regular', color: '#eab308' },
      { level: 3, text: 'Buena', color: '#22c55e' },
      { level: 4, text: 'Fuerte', color: '#16a34a' },
      { level: 5, text: 'Muy fuerte', color: '#15803d' }
    ];
    
    return levels[Math.min(score, 5)];
  }

  onModalClosed(): void {
    this.showModal.set(false);
  }
}



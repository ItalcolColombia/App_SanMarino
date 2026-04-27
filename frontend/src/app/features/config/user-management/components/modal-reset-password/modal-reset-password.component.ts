// src/app/features/config/user-management/components/modal-reset-password/modal-reset-password.component.ts
import {
  Component, Input, Output, EventEmitter, OnChanges, SimpleChanges,
  ChangeDetectorRef, inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faLock, faTimes, faSave, faEye, faEyeSlash, faEnvelope, faCheckCircle, faExclamationTriangle
} from '@fortawesome/free-solid-svg-icons';
import { catchError, finalize, interval, Subject, takeUntil } from 'rxjs';
import { of } from 'rxjs';

import { UserListItem, UserService } from '../../../../../core/services/user/user.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { EmailQueueStatus } from '../../../../../core/auth/auth.models';

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const pwd = control.get('newPassword')?.value;
  const confirm = control.get('confirmPassword')?.value;
  return pwd && confirm && pwd !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'app-modal-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FontAwesomeModule],
  templateUrl: './modal-reset-password.component.html',
  styleUrls: ['./modal-reset-password.component.scss']
})
export class ModalResetPasswordComponent implements OnChanges {
  @Input() isOpen = false;
  @Input() user: UserListItem | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() passwordReset = new EventEmitter<void>();

  // Iconos
  faLock = faLock;
  faTimes = faTimes;
  faSave = faSave;
  faEye = faEye;
  faEyeSlash = faEyeSlash;
  faEnvelope = faEnvelope;
  faCheckCircle = faCheckCircle;
  faExclamationTriangle = faExclamationTriangle;

  form!: FormGroup;
  saving = false;
  showNewPassword = false;
  showConfirmPassword = false;

  // Resultado tras guardar
  resetDone = false;
  resultMessage = '';
  emailQueueId: number | null = null;
  emailStatus: 'pending' | 'processing' | 'sent' | 'failed' | null = null;
  private checkingEmail = false;
  private destroy$ = new Subject<void>();

  private fb = inject(FormBuilder);
  private userService = inject(UserService);
  private authService = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);

  constructor(private library: FaIconLibrary) {
    library.addIcons(faLock, faTimes, faSave, faEye, faEyeSlash, faEnvelope, faCheckCircle, faExclamationTriangle);
    this.buildForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen']) {
      if (this.isOpen) {
        this.resetState();
      } else {
        this.destroy$.next();
      }
    }
  }

  private buildForm(): void {
    this.form = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', [Validators.required]]
    }, { validators: passwordMatchValidator });
  }

  private resetState(): void {
    this.form.reset();
    this.saving = false;
    this.resetDone = false;
    this.resultMessage = '';
    this.emailQueueId = null;
    this.emailStatus = null;
    this.showNewPassword = false;
    this.showConfirmPassword = false;
    this.destroy$.next();
  }

  closeModal(): void {
    if (this.saving) return;
    this.close.emit();
  }

  toggleNew(): void { this.showNewPassword = !this.showNewPassword; }
  toggleConfirm(): void { this.showConfirmPassword = !this.showConfirmPassword; }

  getFieldError(field: string): string {
    const ctrl = this.form.get(field);
    if (!ctrl?.errors || !ctrl.touched) return '';
    if (ctrl.errors['required']) return 'Este campo es obligatorio';
    if (ctrl.errors['minlength']) return 'Mínimo 6 caracteres';
    return '';
  }

  get passwordMismatch(): boolean {
    return !!this.form.errors?.['passwordMismatch'] &&
      !!this.form.get('confirmPassword')?.touched;
  }

  save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.user) return;

    this.saving = true;
    const newPassword = this.form.value.newPassword;

    this.userService.adminResetPassword(this.user.id, newPassword)
      .pipe(
        catchError(err => {
          this.resultMessage = err?.error?.message || 'Error al restablecer la contraseña.';
          this.resetDone = false;
          return of(null);
        }),
        finalize(() => {
          this.saving = false;
          this.cdr.detectChanges();
        })
      )
      .subscribe(res => {
        if (!res) return;
        this.resetDone = true;
        this.resultMessage = res.message;
        this.emailQueueId = res.emailQueueId;
        this.emailStatus = res.emailQueued ? 'pending' : null;
        this.form.reset();
        this.passwordReset.emit();
        if (res.emailQueueId) {
          this.startEmailStatusCheck(res.emailQueueId);
        }
      });
  }

  private startEmailStatusCheck(queueId: number): void {
    this.checkEmailStatus(queueId);
    interval(4000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (this.emailStatus === 'pending' || this.emailStatus === 'processing') {
          this.checkEmailStatus(queueId);
        } else {
          this.destroy$.next();
        }
      });
  }

  private checkEmailStatus(queueId: number): void {
    if (this.checkingEmail) return;
    this.checkingEmail = true;
    this.authService.getEmailStatus(queueId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (status: EmailQueueStatus) => {
          this.emailStatus = status.status;
          this.checkingEmail = false;
          this.cdr.detectChanges();
        },
        error: () => { this.checkingEmail = false; }
      });
  }

  get emailStatusIcon(): { icon: any; color: string; tooltip: string } | null {
    if (!this.emailQueueId) return null;
    switch (this.emailStatus) {
      case 'sent':
        return { icon: this.faEnvelope, color: '#10b981', tooltip: 'Correo enviado exitosamente' };
      case 'failed':
        return { icon: this.faEnvelope, color: '#ef4444', tooltip: 'Error al enviar el correo' };
      case 'pending':
      case 'processing':
        return { icon: this.faEnvelope, color: '#6b7280', tooltip: 'Correo en proceso de envío' };
      default:
        return null;
    }
  }
}

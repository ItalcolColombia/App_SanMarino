import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PasswordRecoveryService, PasswordRecoveryRequest } from '../../../core/services/auth/password-recovery.service';
import { InputSanitizerService } from '../../../core/services/security/input-sanitizer.service';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-password-recovery',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './password-recovery.component.html',
  styleUrls: ['./password-recovery.component.scss']
})
export class PasswordRecoveryComponent implements OnInit {
  readonly appName = environment.appName;
  readonly appTagline = environment.appTagline;
  recoveryForm!: FormGroup;
  loading = false;
  success = false;
  errorMsg = '';
  today = new Date();

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private passwordRecoveryService: PasswordRecoveryService,
    private sanitizer: InputSanitizerService
  ) {}

  ngOnInit(): void {
    this.recoveryForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  onSubmit(): void {
    if (this.recoveryForm.invalid || this.loading) {
      this.recoveryForm.markAllAsTouched();
      return;
    }

    this.errorMsg = '';
    this.loading = true;
    this.success = false;

    const rawRequest: PasswordRecoveryRequest = {
      email: this.recoveryForm.get('email')?.value
    };

    const validation = this.sanitizer.validateObject(rawRequest);
    if (!validation.isValid) {
      this.loading = false;
      this.errorMsg = 'Los datos ingresados contienen caracteres no permitidos.';
      this.recoveryForm.reset();
      return;
    }

    const request: PasswordRecoveryRequest = this.sanitizer.sanitizeObject(rawRequest);

    this.passwordRecoveryService.recoverPassword(request).subscribe({
      next: (response) => {
        this.loading = false;

        if (response.success) {
          this.success = true;
        } else {
          this.errorMsg = response.message ||
            (!response.userFound
              ? 'No se encontró un usuario con ese correo. Verifica que sea el correo correcto.'
              : 'No se pudo procesar la solicitud. Intenta nuevamente.');
        }
      },
      error: (err) => {
        this.loading = false;

        if (err?.status === 0 || err?.status === undefined) {
          this.errorMsg = 'No se pudo conectar con el servidor. Verifica tu conexión e intenta nuevamente.';
        } else if (err?.status === 400) {
          const validationErrors = err?.error?.errors
            ? (Object.values(err.error.errors) as string[][]).flat().join('. ')
            : null;
          this.errorMsg = validationErrors || err?.error?.message || 'Los datos proporcionados no son válidos.';
        } else if (err?.status === 500) {
          this.errorMsg = err?.error?.message || 'Error interno del servidor. Intenta más tarde o contacta al administrador.';
        } else if (err?.status === 404) {
          this.errorMsg = 'El servicio de recuperación no está disponible. Contacta al administrador.';
        } else {
          this.errorMsg = err?.error?.message || err?.message || 'Error al procesar la solicitud. Intenta nuevamente.';
        }
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login'], { replaceUrl: true });
  }

  tryAgain(): void {
    this.success = false;
    this.errorMsg = '';
    this.recoveryForm.reset();
    this.recoveryForm.markAsUntouched();
  }
}

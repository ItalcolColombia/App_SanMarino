import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PasswordRecoveryService } from '../../../core/services/auth/password-recovery.service';
import { environment } from '../../../../environments/environment';

/**
 * Pantalla donde aterriza el enlace del correo de recuperación: canjea el token de un solo uso por
 * una contraseña nueva.
 *
 * Faltaba. Hasta el 12-ago-2026 el backend emitía el token y exponía `POST /api/Auth/reset-password`,
 * pero no había ninguna ruta en el frontend que lo consumiera — y el correo, encima, mostraba el
 * token como si fuera la contraseña. La recuperación de contraseña estaba cortada de punta a punta.
 *
 * Reusa los estilos de `password-recovery` a propósito: es la misma pantalla del mismo flujo y no
 * tiene sentido mantener dos hojas que deben verse igual.
 */
@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './reset-password.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['../password-recovery/password-recovery.component.scss']
})
export class ResetPasswordComponent implements OnInit {
  readonly appName = environment.appName;
  readonly appTagline = environment.appTagline;

  resetForm!: FormGroup;
  token = '';
  loading = false;
  success = false;
  /** El enlace no sirve: no vino token, o el backend lo rechazó por vencido/usado. */
  enlaceInvalido = false;
  errorMsg = '';
  verPassword = false;
  today = new Date();

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private passwordRecoveryService: PasswordRecoveryService
  ) {}

  ngOnInit(): void {
    this.resetForm = this.fb.group(
      {
        // Mismas reglas que valida el backend en ValidatePasswordResetTokenDto.
        newPassword: ['', [
          Validators.required,
          Validators.minLength(8),
          Validators.maxLength(100),
          Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).+$/)
        ]],
        confirmPassword: ['', [Validators.required]]
      },
      { validators: [coincidenLasContrasenas] }
    );

    this.token = (this.route.snapshot.queryParamMap.get('token') ?? '').trim();
    if (!this.token) {
      this.enlaceInvalido = true;
      this.errorMsg = 'El enlace está incompleto: no incluye el código de restablecimiento.';
    }
  }

  get passwordCtrl(): AbstractControl | null {
    return this.resetForm.get('newPassword');
  }

  get confirmCtrl(): AbstractControl | null {
    return this.resetForm.get('confirmPassword');
  }

  onSubmit(): void {
    if (this.resetForm.invalid || this.loading || !this.token) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.errorMsg = '';
    this.loading = true;

    this.passwordRecoveryService
      .resetPassword({ token: this.token, newPassword: this.passwordCtrl?.value })
      .subscribe({
        next: (response) => {
          this.loading = false;

          if (response.success) {
            this.success = true;
            return;
          }

          // Token vencido o ya usado: el backend lo informa con HTTP 200 y success=false.
          this.enlaceInvalido = true;
          this.errorMsg = response.message ||
            'El enlace de restablecimiento es inválido o ya expiró.';
        },
        error: (err) => {
          this.loading = false;

          if (err?.status === 0 || err?.status === undefined) {
            this.errorMsg = 'No se pudo conectar con el servidor. Verifica tu conexión e intenta nuevamente.';
          } else if (err?.status === 400) {
            const validationErrors = err?.error?.errors
              ? (Object.values(err.error.errors) as string[][]).flat().join('. ')
              : null;
            this.errorMsg = validationErrors || err?.error?.message || 'La contraseña no cumple los requisitos.';
          } else if (err?.status === 500) {
            this.errorMsg = err?.error?.message || 'Error interno del servidor. Intenta más tarde o contacta al administrador.';
          } else {
            this.errorMsg = err?.error?.message || err?.message || 'No se pudo restablecer la contraseña.';
          }
        }
      });
  }

  alternarVisibilidad(): void {
    this.verPassword = !this.verPassword;
  }

  goToLogin(): void {
    this.router.navigate(['/login'], { replaceUrl: true });
  }

  pedirNuevoEnlace(): void {
    this.router.navigate(['/password-recovery'], { replaceUrl: true });
  }
}

/** Las dos contraseñas tienen que coincidir. El error se cuelga del grupo, no del campo. */
function coincidenLasContrasenas(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value;
  const confirmacion = group.get('confirmPassword')?.value;

  if (!password || !confirmacion) return null;
  return password === confirmacion ? null : { noCoinciden: true };
}

// src/app/features/auth/password-recovery/password-recovery.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PasswordRecoveryService, PasswordRecoveryRequest } from '../../../core/services/auth/password-recovery.service';
import { InputSanitizerService } from '../../../core/services/security/input-sanitizer.service';

@Component({
  selector: 'app-password-recovery',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './password-recovery.component.html',
  styleUrls: ['./password-recovery.component.scss']
})
export class PasswordRecoveryComponent implements OnInit {
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

    // Validar seguridad: detectar inyección SQL y otros ataques
    const validation = this.sanitizer.validateObject(rawRequest);
    if (!validation.isValid) {
      console.error('🚫 Intento de inyección SQL detectado:', validation.errors);
      this.loading = false;
      this.errorMsg = 'Los datos ingresados contienen caracteres no permitidos. Por favor, verifica tu información.';
      // Limpiar el formulario para prevenir ataques
      this.recoveryForm.reset();
      return;
    }

    // Sanitizar los datos antes de enviarlos
    const request: PasswordRecoveryRequest = this.sanitizer.sanitizeObject(rawRequest);

    console.log('🚀 Iniciando proceso de recuperación de contraseña...');

    this.passwordRecoveryService.recoverPassword(request).subscribe({
      next: (response) => {
        console.log('✅ Respuesta de recuperación:', response);
        this.loading = false;
        if (response.success) {
          this.success = true;
        } else {
          this.errorMsg = response.message || 'No se pudo procesar la solicitud. Verifica el correo electrónico e intenta nuevamente.';
        }
      },
      error: (err) => {
        console.error('❌ Error en recuperación de contraseña:', err);
        this.loading = false;

        // Mensajes de error más descriptivos
        let errorMessage = 'Error al procesar la solicitud. Intenta nuevamente.';

        if (err?.error?.message) {
          errorMessage = err.error.message;
        } else if (err?.message) {
          errorMessage = err.message;
        } else if (typeof err === 'string') {
          errorMessage = err;
        }

        this.errorMsg = errorMessage;
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




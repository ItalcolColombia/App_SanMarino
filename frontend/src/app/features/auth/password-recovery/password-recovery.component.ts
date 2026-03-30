// src/app/features/auth/password-recovery/password-recovery.component.ts
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
        console.log('✅ Respuesta de recuperación:', {
          success: response.success,
          userFound: response.userFound,
          emailSent: response.emailSent,
          emailQueueId: response.emailQueueId,
          message: response.message
        });
        
        this.loading = false;
        
        if (response.success) {
          this.success = true;
          // Si el email no se envió pero la contraseña se generó, mostrar advertencia
          if (!response.emailSent) {
            console.warn('⚠️ Contraseña generada pero email no enviado. QueueId:', response.emailQueueId);
          }
        } else {
          // Mensajes específicos según el tipo de error
          if (!response.userFound) {
            this.errorMsg = response.message || 'No se encontró un usuario con ese correo electrónico. Verifica que el correo esté correcto.';
          } else {
            this.errorMsg = response.message || 'No se pudo procesar la solicitud. Verifica el correo electrónico e intenta nuevamente.';
          }
        }
      },
      error: (err) => {
        console.error('❌ Error en recuperación de contraseña:', {
          error: err,
          status: err?.status,
          statusText: err?.statusText,
          message: err?.message,
          errorDetails: err?.error
        });
        
        this.loading = false;

        // Mensajes de error más descriptivos según el tipo de error
        let errorMessage = 'Error al procesar la solicitud. Intenta nuevamente.';

        // Error de red/conectividad
        if (err?.status === 0 || err?.status === undefined) {
          errorMessage = 'No se pudo conectar con el servidor. Verifica tu conexión a internet e intenta nuevamente.';
          console.error('🔴 Error de conectividad:', err);
        }
        // Error 400 - Bad Request
        else if (err?.status === 400) {
          if (err?.error?.message) {
            errorMessage = err.error.message;
          } else if (err?.error?.errors) {
            // Errores de validación
            const validationErrors = Object.values(err.error.errors).flat();
            errorMessage = validationErrors.join('. ') || 'Los datos proporcionados no son válidos.';
          } else {
            errorMessage = 'Los datos proporcionados no son válidos. Verifica el formato del correo electrónico.';
          }
          console.warn('⚠️ Error de validación:', err.error);
        }
        // Error 500 - Internal Server Error
        else if (err?.status === 500) {
          errorMessage = err?.error?.message || 'Ocurrió un error interno en el servidor. Por favor, intenta nuevamente más tarde. Si el problema persiste, contacta al administrador.';
          console.error('🔴 Error del servidor:', err.error);
        }
        // Error 404 - Not Found
        else if (err?.status === 404) {
          errorMessage = 'El servicio de recuperación de contraseña no está disponible. Contacta al administrador.';
          console.error('🔴 Endpoint no encontrado:', err);
        }
        // Otros errores HTTP
        else if (err?.status) {
          errorMessage = err?.error?.message || `Error ${err.status}: ${err.statusText || 'Error desconocido'}`;
          console.error('🔴 Error HTTP:', err);
        }
        // Error con mensaje del backend
        else if (err?.error?.message) {
          errorMessage = err.error.message;
        }
        // Error con mensaje genérico
        else if (err?.message) {
          errorMessage = err.message;
        }
        // Error como string
        else if (typeof err === 'string') {
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




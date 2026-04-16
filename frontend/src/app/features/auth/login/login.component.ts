// src/app/features/auth/login/login.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule }      from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { FormsModule } from '@angular/forms';
import { RecaptchaModule, RecaptchaFormsModule } from 'ng-recaptcha';
import { environment } from '../../../../environments/environment';
import { InputSanitizerService } from '../../../core/services/security/input-sanitizer.service';


@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, FormsModule, RecaptchaModule, RecaptchaFormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  readonly appName = environment.appName;
  readonly appTagline = environment.appTagline;
  loginForm!: FormGroup;
  loading = false;
  showPassword = false;
  errorMsg = '';
  errorType: 'database' | 'network' | 'blocked' | 'credentials' | 'encryption' | 'unknown' | null = null;
  today = new Date();
  recaptchaEnabled = false;
  recaptchaSiteKey = '';
  recaptchaToken: string | null = null;

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private auth: AuthService,
    private sanitizer: InputSanitizerService
  ) {
    // Configurar reCAPTCHA solo en producción
    this.recaptchaEnabled = environment.production && (environment.recaptcha?.enabled ?? false);
    this.recaptchaSiteKey = environment.recaptcha?.siteKey || '';
  }

  ngOnInit(): void {
    this.loginForm = this.fb.group({
      email:    ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      remember: [true], // Recordarme
      companyId: [0], // si tu backend lo consume
      recaptchaToken: [null] // Token de reCAPTCHA
    });
  }

  onRecaptchaResolved(token: string | null): void {
    if (token) {
      this.recaptchaToken = token;
      this.loginForm.patchValue({ recaptchaToken: token });
    } else {
      this.recaptchaToken = null;
      this.loginForm.patchValue({ recaptchaToken: null });
    }
  }

  onRecaptchaError(): void {
    this.recaptchaToken = null;
    this.loginForm.patchValue({ recaptchaToken: null });
  }

  onSubmit(): void {
    if (this.loginForm.invalid || this.loading) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.errorMsg = '';
    this.errorType = null;
    this.loading = true;

    // Preparar datos de login incluyendo reCAPTCHA token si está habilitado
    const rawLoginData = {
      email: this.loginForm.value.email,
      password: this.loginForm.value.password,
      companyId: this.loginForm.value.companyId,
      recaptchaToken: this.recaptchaEnabled ? this.recaptchaToken : null
    };

    // Validar seguridad: detectar inyección SQL y otros ataques
    const validation = this.sanitizer.validateObject(rawLoginData);
    if (!validation.isValid) {
      console.error('🚫 Intento de inyección SQL detectado:', validation.errors);
      this.loading = false;
      this.errorMsg = 'Los datos ingresados contienen caracteres no permitidos. Por favor, verifica tu información.';
      // Limpiar el formulario para prevenir ataques
      this.loginForm.reset();
      return;
    }

    // Sanitizar los datos antes de enviarlos
    const loginData = this.sanitizer.sanitizeObject(rawLoginData);

    // Validar reCAPTCHA en producción
    if (this.recaptchaEnabled && !this.recaptchaToken) {
      this.loading = false;
      this.errorMsg = 'Por favor, completa la verificación de seguridad.';
      return;
    }

    this.auth.login(loginData, this.loginForm.value.remember).subscribe({
      next: (session) => {
        if (!session?.accessToken) {
          this.loading = false;
          this.errorMsg = 'No se recibió el token de autenticación. Por favor, intenta de nuevo.';
          return;
        }

        this.loading = false;

        setTimeout(() => {
          this.router.navigate(['/home'], { replaceUrl: true }).then(
            (success) => {
              if (!success) {
                this.router.navigate(['/'], { replaceUrl: true });
              }
            },
            () => {
              this.router.navigate(['/'], { replaceUrl: true }).catch(() => {
                window.location.href = '/home';
              });
            }
          );
        }, 200);
      },
      error: (err) => {
        this.loading = false;

        // Obtener mensaje y tipo de error
        let errorMessage = 'Error al procesar el login';
        let errorType: 'database' | 'network' | 'blocked' | 'credentials' | 'encryption' | 'unknown' = 'unknown';

        // Verificar si el error tiene información adicional del servicio
        if ((err as any)?.errorType) {
          errorType = (err as any).errorType;
          errorMessage = err.message || errorMessage;
        }
        // Verificar HttpErrorResponse directamente
        else if (err?.status !== undefined) {
          const status = err.status;
          const errorBody = err.error || {};
          
          if (status === 500) {
            const errorDetail = errorBody.message || errorBody.detail || err.message || '';
            if (errorDetail.toLowerCase().includes('database') || 
                errorDetail.toLowerCase().includes('connection') ||
                errorDetail.toLowerCase().includes('timeout') ||
                errorDetail.toLowerCase().includes('npgsql') ||
                errorDetail.toLowerCase().includes('postgresql') ||
                errorDetail.toLowerCase().includes('unreachable')) {
              errorType = 'database';
              errorMessage = 'Error de conexión a la base de datos. El servidor no puede conectarse a la base de datos en este momento. Por favor, intenta nuevamente en unos momentos o contacta al administrador.';
            } else {
              errorMessage = errorDetail || 'Error interno del servidor. Por favor, intenta nuevamente.';
            }
          }
          else if (status === 429) {
            errorType = 'blocked';
            const retryAfter = err.headers?.get('Retry-After') || errorBody.retryAfter;
            if (retryAfter) {
              errorMessage = `Tu IP ha sido bloqueada temporalmente por exceder el límite de intentos. Intenta nuevamente en ${retryAfter} segundos.`;
            } else {
              errorMessage = errorBody.message || 'Tu IP ha sido bloqueada temporalmente. Intenta nuevamente más tarde.';
            }
          }
          else if (status === 401) {
            errorType = 'credentials';
            errorMessage = errorBody.message || 'Credenciales inválidas. Verifica tu email y contraseña.';
          }
          else if (status === 0 || err.message?.toLowerCase().includes('timeout')) {
            errorType = 'network';
            errorMessage = 'Error de conexión con el servidor. Verifica tu conexión a internet e intenta nuevamente.';
          }
          else {
            errorMessage = errorBody.message || err.message || `Error ${status}`;
          }
        }
        // Error de mensaje directo
        else if (err?.message) {
          errorMessage = err.message;
          // Intentar detectar tipo desde el mensaje
          if (err.message.toLowerCase().includes('database') || err.message.toLowerCase().includes('connection')) {
            errorType = 'database';
          } else if (err.message.toLowerCase().includes('network') || err.message.toLowerCase().includes('timeout')) {
            errorType = 'network';
          } else if (err.message.toLowerCase().includes('bloqueada') || err.message.toLowerCase().includes('blocked')) {
            errorType = 'blocked';
          } else if (err.message.toLowerCase().includes('credenciales') || err.message.toLowerCase().includes('invalid')) {
            errorType = 'credentials';
          }
        }
        else if (err?.error?.message) {
          errorMessage = err.error.message;
        }
        else if (typeof err === 'string') {
          errorMessage = err;
        }

        this.errorMsg = errorMessage || 'Credenciales inválidas o error de servidor.';
        this.errorType = errorType;
      }
    });
  }

  togglePassword(): void {
    this.showPassword = !this.showPassword;
  }

  goToPasswordRecovery(): void {
    this.router.navigate(['/password-recovery']);
  }
}

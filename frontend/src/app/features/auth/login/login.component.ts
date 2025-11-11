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
  loginForm!: FormGroup;
  loading = false;
  errorMsg = '';
  remember = true; // si quieres "Recordarme"
  today = new Date(); // para el {{ today | date:'yyyy' }}
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
    this.loading = true;

    console.log('🚀 Iniciando proceso de login...');

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

    this.auth.login(loginData, this.remember).subscribe({
      next: (session) => {
        console.log('✅ Login exitoso, redirigiendo...', {
          hasSession: !!session,
          hasToken: !!session?.accessToken,
          tokenLength: session?.accessToken?.length ?? 0,
          hasMenu: session?.menu && session.menu.length > 0
        });

        if (!session?.accessToken) {
          console.error('❌ Error: Sesión sin token, no se puede continuar');
          this.loading = false;
          this.errorMsg = 'Error: No se recibió el token de autenticación. Por favor, intenta de nuevo.';
          return;
        }

        this.loading = false;

        // Pequeño delay para asegurar que el storage se guarde completamente
        setTimeout(() => {
          console.log('🔄 Redirigiendo a /home...');
          this.router.navigate(['/home'], { replaceUrl: true }).then(
            (success) => {
              if (success) {
                console.log('✅ Redirección exitosa a /home');
              } else {
                console.warn('⚠️ Redirección falló, intentando alternativa...');
                this.router.navigate(['/'], { replaceUrl: true });
              }
            },
            (error) => {
              console.error('❌ Error al redirigir:', error);
              // Fallback: intentar navegar a dashboard o raíz
              this.router.navigate(['/'], { replaceUrl: true }).catch(() => {
                console.error('❌ Error crítico al navegar');
                // Último recurso: recargar la página
                window.location.href = '/home';
              });
            }
          );
        }, 200); // Aumentado a 200ms para asegurar que el storage se guarde
      },
      error: (err) => {
        console.error('❌ Error en login:', err);
        this.loading = false;

        // Mensajes de error más descriptivos
        let errorMessage = 'Error al procesar el login';

        if (err?.message) {
          errorMessage = err.message;
        } else if (err?.error?.message) {
          errorMessage = err.error.message;
        } else if (typeof err === 'string') {
          errorMessage = err;
        }

        this.errorMsg = errorMessage || 'Credenciales inválidas o error de servidor.';
      }
    });
  }

  goToPasswordRecovery(): void {
    this.router.navigate(['/password-recovery']);
  }
}

// src/app/features/auth/login/login.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule }      from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { FormsModule } from '@angular/forms';


@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  loginForm!: FormGroup;
  loading = false;
  errorMsg = '';
  remember = true; // si quieres “Recordarme”
  today = new Date(); // para el {{ today | date:'yyyy' }}
  constructor(
    private fb: FormBuilder,
    private router: Router,
    private auth: AuthService
  ) {}

  ngOnInit(): void {
    this.loginForm = this.fb.group({
      email:    ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      companyId: [0], // si tu backend lo consume
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid || this.loading) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.errorMsg = '';
    this.loading = true;

    this.auth.login(this.loginForm.value, this.remember).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/home']); // ← redirección post-login al Home
      },
      error: (err) => {
        this.loading = false;
        this.errorMsg = (err?.error?.message) || 'Credenciales inválidas o error de servidor.';
      }
    });
  }

  goToPasswordRecovery(): void {
    this.router.navigate(['/password-recovery']);
  }
}

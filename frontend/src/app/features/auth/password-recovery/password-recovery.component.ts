// src/app/features/auth/password-recovery/password-recovery.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PasswordRecoveryService, PasswordRecoveryRequest } from '../../../core/services/auth/password-recovery.service';

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
    private passwordRecoveryService: PasswordRecoveryService
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

    const request: PasswordRecoveryRequest = {
      email: this.recoveryForm.get('email')?.value
    };

    this.passwordRecoveryService.recoverPassword(request).subscribe({
      next: (response) => {
        this.loading = false;
        if (response.success) {
          this.success = true;
        } else {
          this.errorMsg = response.message;
        }
      },
      error: (err) => {
        this.loading = false;
        this.errorMsg = err?.error?.message || 'Error al procesar la solicitud. Intenta nuevamente.';
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  tryAgain(): void {
    this.success = false;
    this.errorMsg = '';
    this.recoveryForm.reset();
  }
}




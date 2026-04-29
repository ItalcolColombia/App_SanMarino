// src/app/core/auth/auth.guard.ts
import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { TokenStorageService } from './token-storage.service';

/** Returns true when the stored JWT has not yet expired. */
function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return typeof payload.exp === 'number' && payload.exp * 1000 < Date.now();
  } catch {
    return true;
  }
}

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const storage = inject(TokenStorageService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  const token = storage.getToken();
  if (token && isTokenExpired(token)) {
    auth.logout();
    router.navigate(['/login']);
    return false;
  }

  return true;
};

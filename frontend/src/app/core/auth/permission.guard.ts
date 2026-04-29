import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { UserPermissionService } from './user-permission.service';

/**
 * Route guard that enforces permission-based access control.
 *
 * Usage in route definition:
 *   {
 *     path: 'config/users',
 *     canActivate: [authGuard, permissionGuard],
 *     data: { permissions: ['manage_users'] },   // any of these must be present
 *     ...
 *   }
 *
 * If `data.permissions` is absent or empty the guard passes silently.
 * When access is denied the user is redirected to /home (not /login — the
 * authGuard already ensures the session exists).
 */
export const permissionGuard: CanActivateFn = (route) => {
  const permService = inject(UserPermissionService);
  const router = inject(Router);

  const required: string[] = route.data?.['permissions'] ?? [];

  if (required.length === 0) return true;

  if (permService.hasAny(required)) return true;

  router.navigate(['/home']);
  return false;
};

// src/app/core/auth/auth.interceptor.ts
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TokenStorageService } from './token-storage.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const storage = inject(TokenStorageService);
  const token = storage.getToken();
  const session = storage.get();

  // Debug log para verificar el token
  if (req.url.includes('/userfarm/') || req.url.includes('/Company')) {
    console.log('AuthInterceptor - URL:', req.url);
    console.log('AuthInterceptor - Token presente:', token ? 'SÍ' : 'NO');
    console.log('AuthInterceptor - Empresa activa:', session?.activeCompany || 'Ninguna');
    if (token) {
      console.log('AuthInterceptor - Token:', token.substring(0, 50) + '...');
    }
  }

  if (token) {
    const headers: { [key: string]: string } = {
      Authorization: `Bearer ${token}`
    };

    // Agregar header de empresa activa (siempre, incluso si es null/undefined)
    // Esto permite que el backend sepa que el usuario está autenticado pero no tiene empresa activa
    headers['X-Active-Company'] = session?.activeCompany || '';

    const authReq = req.clone({
      setHeaders: headers
    });
    return next(authReq);
  }
  return next(req);
};

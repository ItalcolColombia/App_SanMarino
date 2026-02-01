// src/app/core/auth/auth.interceptor.ts
import { HttpInterceptorFn, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap } from 'rxjs';
import { TokenStorageService } from './token-storage.service';
import { EncryptionService } from './encryption.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next: HttpHandlerFn) => {
  const storage = inject(TokenStorageService);
  const encryption = inject(EncryptionService);
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

  // Obtener el SECRET_UP y encriptarlo
  const secretUpFrontend = environment.platformSecret?.secretUpFrontend;

  if (!secretUpFrontend) {
    console.error('⚠️ SECRET_UP del frontend no configurado en environment');
    return next(req); // Continuar sin SECRET_UP (será rechazado por el backend)
  }

  // Encriptar el SECRET_UP de forma asíncrona
  return from(encryption.encryptSecretUp(secretUpFrontend)).pipe(
    switchMap(encryptedSecretUp => {
      // Construir headers base
      const headers: { [key: string]: string } = {};

      // Agregar SECRET_UP encriptado en TODAS las peticiones
      headers['X-Secret-Up'] = encryptedSecretUp;

      // Agregar token de autenticación si existe
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      // Agregar header de empresa activa (nombre) - siempre, incluso si es null/undefined
      // Esto permite que el backend sepa que el usuario está autenticado pero no tiene empresa activa
      headers['X-Active-Company'] = session?.activeCompany || '';

      // Agregar header de ID de empresa activa
      if (session?.activeCompanyId) {
        headers['X-Active-Company-Id'] = session.activeCompanyId.toString();
      }

      // Agregar header de ID de país activo (siempre, incluso si es null/undefined)
      // Esto permite que el backend sepa el país del usuario que realizó login
      if (session?.activePaisId) {
        headers['X-Active-Pais'] = session.activePaisId.toString();
      }

      // Agregar header de nombre del país activo
      if (session?.activePaisNombre) {
        headers['X-Active-Pais-Nombre'] = session.activePaisNombre;
      }

      const authReq = req.clone({
        setHeaders: headers
      });

      return next(authReq);
    })
  );
};

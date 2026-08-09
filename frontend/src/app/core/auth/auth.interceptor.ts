// src/app/core/auth/auth.interceptor.ts
import { HttpInterceptorFn, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap, catchError, tap, throwError } from 'rxjs';
import { HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { TokenStorageService } from './token-storage.service';
import { EncryptionService } from './encryption.service';
import { SessionTimeoutService } from './session-timeout.service';
import { ConexionService } from '../pwa/conexion.service';
import { debeCerrarSesionPor401 } from './funciones/debe-cerrar-sesion-por-401.funcion';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next: HttpHandlerFn) => {
  const storage = inject(TokenStorageService);
  const encryption = inject(EncryptionService);
  const sessionTimeout = inject(SessionTimeoutService);
  const conexion = inject(ConexionService);
  const token = storage.getToken();
  const session = storage.get();

  // Debug log para verificar el token
  if (req.url.includes('/userfarm/') || req.url.includes('/Company')) {
    
    
    
    if (token) {
      
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

      return next(authReq).pipe(
        // Señal de conectividad REAL para el indicador de la PWA. `navigator.onLine` es
        // optimista: en la granja el wifi del galpón está conectado y no sale a ningún
        // lado, y ahí sigue diciendo `true`. Una respuesta efectiva del backend es la
        // única evidencia fuerte; un `status === 0` es la única evidencia de lo contrario.
        // No cambia el comportamiento de la petición: solo observa.
        tap(evento => {
          if (evento instanceof HttpResponse) {
            conexion.marcarExitoDeRed();
          }
        }),
        catchError((err: unknown) => {
          if (err instanceof HttpErrorResponse && err.status === 0) {
            conexion.marcarFalloDeRed();
          } else if (err instanceof HttpErrorResponse) {
            // Cualquier status del servidor prueba que el servidor contestó.
            conexion.marcarExitoDeRed();
          }

          // No todo 401 termina la sesión: el gate de plataforma (SECRET_UP) también
          // responde 401 y ahí el usuario y su token están perfectos. La regla vive
          // aislada y con tests en funciones/debe-cerrar-sesion-por-401.funcion.ts.
          if (debeCerrarSesionPor401(err, !!token)) {
            sessionTimeout.onUnauthorized();
          }
          return throwError(() => err);
        })
      );
    })
  );
};

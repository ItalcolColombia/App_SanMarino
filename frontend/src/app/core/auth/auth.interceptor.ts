// src/app/core/auth/auth.interceptor.ts
import { HttpInterceptorFn, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { from, of, switchMap, catchError, tap, throwError } from 'rxjs';
import { HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { TokenStorageService } from './token-storage.service';
import { EncryptionService } from './encryption.service';
import { SessionTimeoutService } from './session-timeout.service';
import { ConexionService } from '../pwa/conexion.service';
import { debeCerrarSesionPor401 } from './funciones/debe-cerrar-sesion-por-401.funcion';
import { resolverFirmaPlataforma } from './funciones/resolver-firma-plataforma.funcion';
import { obtenerDeviceId } from './funciones/device-id.funcion';
import { resolverDestinoApi } from './funciones/resolver-destino-api.funcion';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next: HttpHandlerFn) => {
  const documento = inject(DOCUMENT);
  const destino = resolverDestinoApi(req.url, environment.apiUrl, documento.baseURI);
  // Recursos estáticos y APIs externas no reciben credenciales ni afectan nuestra sesión.
  if (!destino.esApi) return next(req);

  const storage = inject(TokenStorageService);
  const encryption = inject(EncryptionService);
  const sessionTimeout = inject(SessionTimeoutService);
  const conexion = inject(ConexionService);
  // El login es anónimo, incluso al cambiar de cuenta con una sesión previa todavía abierta.
  const session = destino.esLogin ? null : storage.get();
  const token = session?.accessToken ?? null;

  // Firma de plataforma (X-Secret-Up):
  //  - 'derivada': la sesión trae `platformKey` (HMAC del `jti`, emitido por el backend). Va tal cual.
  //  - 'legacy':   secreto estático del bundle. Hay que cifrarlo antes de mandarlo (compat + móvil).
  const firma = destino.esLogin ? null : resolverFirmaPlataforma(session, environment.platformSecret?.secretUpFrontend);

  if (firma && !firma.valor) {
    console.error('⚠️ Firma de plataforma no disponible (ni platformKey de sesión ni secreto en environment)');
    return next(req); // Continuar sin firma (será rechazado por el backend)
  }

  const secretUp$ = !firma ? of(null) : firma.modo === 'derivada'
    ? of(firma.valor)
    : from(encryption.encryptSecretUp(firma.valor!));

  return secretUp$.pipe(
    switchMap(encryptedSecretUp => {
      // Construir headers base
      const headers: { [key: string]: string } = {};

      // Firma de plataforma solo para peticiones al backend configurado.
      if (encryptedSecretUp) headers['X-Secret-Up'] = encryptedSecretUp;

      // Identificador del equipo. El backend lo declaraba desde hace meses
      // (`RateLimitingCalculos.DeviceIdHeader`) y ningún cliente lo mandaba, así que:
      //  1. el login puede anotar QUÉ dispositivo abrió cada sesión — sin eso, revocar la tablet
      //     perdida sería elegir a ciegas entre filas idénticas;
      //  2. el rate limit de `/api/sync/*` pasa a contar POR DISPOSITIVO en vez de por IP, que es
      //     lo que ese código dice que quiere: dos tablets detrás del mismo NAT drenan su cola sin
      //     bloquearse entre sí. Ninguna otra ruta cambia (`AlcanceDeRuta` sólo usa el device en Sync).
      // Es una etiqueta, no una credencial: el servidor jamás autoriza con esto.
      headers['X-Device-Id'] = obtenerDeviceId();

      // Agregar token de autenticación si existe
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      // Agregar header de empresa activa (nombre) - siempre, incluso si es null/undefined
      // Esto permite que el backend sepa que el usuario está autenticado pero no tiene empresa activa
      if (!destino.esLogin) headers['X-Active-Company'] = session?.activeCompany || '';

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

      let peticion = req;
      if (destino.esLogin) {
        let anonimos = req.headers;
        for (const nombre of [
          'Authorization', 'X-Secret-Up', 'X-Active-Company', 'X-Active-Company-Id',
          'X-Active-Pais', 'X-Active-Pais-Nombre'
        ]) {
          anonimos = anonimos.delete(nombre);
        }
        peticion = req.clone({ headers: anonimos });
      }

      const authReq = peticion.clone({
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
          // Una respuesta tardía del usuario anterior no puede expulsar a quien acaba de entrar.
          if (token && token === storage.getToken() && debeCerrarSesionPor401(err, true)) {
            sessionTimeout.onUnauthorized();
          }
          return throwError(() => err);
        })
      );
    })
  );
};

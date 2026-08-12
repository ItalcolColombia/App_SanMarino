// src/app/core/services/auth/password-recovery.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { BaseHttpService } from '../base-http.service';

export interface PasswordRecoveryRequest {
  email: string;
}

export interface PasswordRecoveryResponse {
  success: boolean;
  message: string;
  userFound: boolean;
  emailSent: boolean;
  emailQueueId?: number | null;
}

/** Canje del token que llega por correo: token de un solo uso + la contraseña elegida. */
export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface ResetPasswordResponse {
  success: boolean;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class PasswordRecoveryService extends BaseHttpService {
  private readonly baseUrl = `${environment.apiUrl}/Auth`;

  /**
   * Solicita la recuperación de contraseña
   * @param request Datos de la solicitud de recuperación
   * @returns Observable con la respuesta del servidor
   */
  recoverPassword(request: PasswordRecoveryRequest): Observable<PasswordRecoveryResponse> {
    return this.post<PasswordRecoveryResponse>(`${this.baseUrl}/recover-password`, request, {
      context: 'PasswordRecoveryService.recoverPassword'
    });
  }

  /**
   * Canjea el token del correo por una contraseña nueva.
   *
   * El token vive 15 minutos y sirve una sola vez; el backend responde `success: false` (HTTP 200)
   * cuando ya venció o se usó, así que ese caso NO llega por el canal de error.
   */
  resetPassword(request: ResetPasswordRequest): Observable<ResetPasswordResponse> {
    return this.post<ResetPasswordResponse>(`${this.baseUrl}/reset-password`, request, {
      context: 'PasswordRecoveryService.resetPassword'
    });
  }
}




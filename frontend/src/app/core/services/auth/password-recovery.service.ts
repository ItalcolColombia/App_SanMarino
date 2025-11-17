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
}




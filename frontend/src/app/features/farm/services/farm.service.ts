// src/app/features/farm/services/farm.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { tap, catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { TokenStorageService } from '../../../core/auth/token-storage.service';
import { AuthSession } from '../../../core/auth/auth.models';

export interface FarmDto {
  id: number;                  // ← número garantizado al leer
  name: string;
  companyId: number;           // ← número garantizado al leer
  status: 'A' | 'I' | string;

  // Opcionales para UI
  regional?: string | null;
  regionalId?: number | null;
  department?: string | null;
  city?: string | null;
  departamentoId?: number | null;
  ciudadId?: number | null;

  createdAt?: string;
  updatedAt?: string;
  company?: { id: number; name: string };
}

export interface CreateFarmDto {
  name: string;
  companyId: number;
  status: 'A' | 'I';
  // opcionales
  regional?: string | '1';
  regionalId?: number | null;
  departamentoId?: number | null;
  ciudadId?: number | null;
  department?: string | null;
  city?: string | null;
}

export interface UpdateFarmDto extends CreateFarmDto {
  id: number; // ← requerido en update
}

@Injectable({ providedIn: 'root' })
export class FarmService {
  private readonly baseUrl = `${environment.apiUrl}/Farm`;
  private tokenStorage = inject(TokenStorageService);

  constructor(private http: HttpClient) {}

  getAll(): Observable<FarmDto[]> {
    // Obtener el ID del usuario de la sesión actual
    const session: AuthSession | null = this.tokenStorage.get();
    const userId = session?.user?.id;

    console.log('=== FarmService.getAll() Debug ===');
    console.log('Session completa:', session);
    console.log('User ID:', userId);
    console.log('User ID type:', typeof userId);

    // Si no hay sesión o user ID, esperar un poco y reintentar
    if (!session || !userId) {
      console.warn('⚠️ No hay sesión o user ID, esperando...');
      return new Observable(observer => {
        setTimeout(() => {
          const retrySession: AuthSession | null = this.tokenStorage.get();
          const retryUserId = retrySession?.user?.id;

          if (retrySession && retryUserId) {
            console.log('✅ Sesión encontrada en reintento:', retryUserId);
            this.getAllWithUserId(retryUserId).subscribe(observer);
          } else {
            console.error('❌ Aún no hay sesión después del reintento');
            observer.error('No hay sesión de usuario disponible');
          }
        }, 1000); // Esperar 1 segundo
      });
    }

    return this.getAllWithUserId(userId);
  }

  private getAllWithUserId(userId: string): Observable<FarmDto[]> {
    // Validar que userId sea un GUID válido
    if (!userId || userId.trim() === '') {
      console.error('❌ FarmService.getAllWithUserId() - userId vacío o inválido');
      return throwError(() => new Error('User ID no disponible'));
    }

    // Validar formato GUID básico
    const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!guidPattern.test(userId)) {
      console.warn('⚠️ FarmService.getAllWithUserId() - userId no tiene formato GUID válido:', userId);
      // Continuar de todas formas, el backend puede manejarlo
    }

    // Crear parámetros de consulta
    let params = new HttpParams();
    params = params.set('id_user_session', userId.trim());

    console.log('=== FarmService.getAllWithUserId() ===');
    console.log('User ID:', userId);
    console.log('User ID type:', typeof userId);
    console.log('User ID length:', userId.length);
    console.log('Parámetros enviados:', params.toString());
    console.log('URL base:', this.baseUrl);

    return this.http.get<FarmDto[]>(this.baseUrl, { params }).pipe(
      tap(response => {
        console.log('✅ Respuesta del backend:', response);
        console.log('Cantidad de granjas recibidas:', response.length);
        if (response.length === 0) {
          console.warn('⚠️ No se recibieron granjas. Verificar que el usuario tenga granjas asignadas.');
        }
      }),
      catchError(error => {
        console.error('❌ Error en FarmService.getAllWithUserId():', error);
        console.error('Error details:', error.error);
        console.error('Error status:', error.status);
        console.error('Error message:', error.message);
        if (error.error) {
          console.error('Error response body:', JSON.stringify(error.error, null, 2));
        }
        return throwError(() => error);
      })
    );
  }

  getById(id: number): Observable<FarmDto> {
    return this.http.get<FarmDto>(`${this.baseUrl}/${id}`);
  }

  // Método de prueba para diagnosticar problemas
  testConnection(): Observable<any> {
    console.log('=== Test Connection ===');
    console.log('Base URL:', this.baseUrl);
    console.log('Environment API URL:', environment.apiUrl);

    const session: AuthSession | null = this.tokenStorage.get();
    console.log('Session:', session);

    if (!session) {
      console.error('❌ No hay sesión de usuario');
      return throwError(() => new Error('No hay sesión de usuario'));
    }

    const userId = session.user?.id;
    console.log('User ID:', userId);

    if (!userId) {
      console.error('❌ No hay user ID en la sesión');
      return throwError(() => new Error('No hay user ID en la sesión'));
    }

    const params = new HttpParams().set('id_user_session', userId);
    const url = `${this.baseUrl}?${params.toString()}`;
    console.log('URL de prueba:', url);

    return this.http.get<any>(this.baseUrl, { params });
  }

  create(dto: CreateFarmDto): Observable<FarmDto> {
    return this.http.post<FarmDto>(this.baseUrl, dto);
  }

  update(dto: UpdateFarmDto): Observable<FarmDto> {
    return this.http.put<FarmDto>(`${this.baseUrl}/${dto.id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

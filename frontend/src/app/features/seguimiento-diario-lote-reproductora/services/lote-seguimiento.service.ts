// app/features/lote-reproductora/services/lote-seguimiento.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

/* ===========================
   DTOs alineados al componente
   =========================== */
export interface LoteSeguimientoDto {
  id: number;
  fecha: string;             // ISO (p.ej. '2025-03-10T00:00:00.000Z')
  loteId: number;            // Convertido a number desde string
  reproductoraId: string;
  mortalidadH: number;
  mortalidadM: number;
  selH: number;
  selM: number;
  errorH: number;
  errorM: number;
  tipoAlimento: string;
  consumoAlimento: number;   // Consumo para hembras (en kg)
  consumoKgMachos?: number | null; // Consumo para machos (en kg)
  observaciones?: string | null;
  pesoInicial?: number | null;
  pesoFinal?: number | null;
  ciclo?: string | null; // 'Normal' | 'Reforzado'
  // Campos de peso y uniformidad
  pesoPromH?: number | null;
  pesoPromM?: number | null;
  uniformidadH?: number | null;
  uniformidadM?: number | null;
  cvH?: number | null;
  cvM?: number | null;
  // Campos de agua (solo para Ecuador y Panamá)
  consumoAguaDiario?: number | null;
  consumoAguaPh?: number | null;
  consumoAguaOrp?: number | null;
  consumoAguaTemperatura?: number | null;
  // Metadata e items adicionales
  metadata?: any | null;
  itemsAdicionales?: any | null;
}

export interface CreateLoteSeguimientoDto extends Omit<LoteSeguimientoDto, 'id'> {}
export interface UpdateLoteSeguimientoDto extends LoteSeguimientoDto {}

@Injectable({ providedIn: 'root' })
export class LoteSeguimientoService {
  private readonly base = `${environment.apiUrl}/LoteSeguimiento`;

  constructor(private http: HttpClient) {}

  // Helpers
  private toIsoIfDateLike(v: string): string {
    // Acepta 'yyyy-MM-dd' o ISO y devuelve ISO
    const d = new Date(v);
    return isNaN(d.getTime()) ? v : d.toISOString();
  }
  private handleError = (err: HttpErrorResponse) => {
    let errorMessage = 'Error inesperado en el servidor.';
    
    if (err.error) {
      // Intentar obtener el mensaje de error del backend (incluir details si viene para diagnóstico)
      if (err.error.message) {
        errorMessage = err.error.message;
        if (err.error.details) {
          errorMessage += ` (${err.error.details})`;
        }
      } else if (err.error.detail) {
        errorMessage = err.error.detail;
      } else if (err.error.title) {
        errorMessage = err.error.title;
      } else if (typeof err.error === 'string') {
        errorMessage = err.error;
      }
    }
    
    // Mensajes específicos según el código de estado
    if (err.status === 400) {
      errorMessage = errorMessage || 'Datos inválidos. Por favor, verifique la información ingresada.';
    } else if (err.status === 404) {
      errorMessage = errorMessage || 'El recurso solicitado no fue encontrado.';
    } else if (err.status === 500) {
      errorMessage = errorMessage || 'Error interno del servidor. Por favor, intente nuevamente más tarde.';
    }
    
    return throwError(() => ({
      status: err.status,
      message: errorMessage,
      error: err.error
    }));
  };

  // ====== Queries ======
  /** Trae seguimientos por lote y reproductora (como usa el componente). */
  getByLoteYRepro(
    loteId: string, 
    reproductoraId: string, 
    desde?: string | Date | null, 
    hasta?: string | Date | null
  ): Observable<LoteSeguimientoDto[]> {
    let params = new HttpParams()
      .set('loteId', loteId)
      .set('reproductoraId', reproductoraId);
    
    if (desde) {
      params = params.set('desde', this.toIsoIfDateLike(typeof desde === 'string' ? desde : desde.toISOString()));
    }
    
    if (hasta) {
      params = params.set('hasta', this.toIsoIfDateLike(typeof hasta === 'string' ? hasta : hasta.toISOString()));
    }
    
    return this.http.get<LoteSeguimientoDto[]>(this.base, { params }).pipe(catchError(this.handleError));
  }

  get(id: number): Observable<LoteSeguimientoDto> {
    return this.http.get<LoteSeguimientoDto>(`${this.base}/${id}`).pipe(catchError(this.handleError));
  }

  // ====== CRUD ======
  create(dto: CreateLoteSeguimientoDto): Observable<LoteSeguimientoDto> {
    const payload: any = {
      ...dto,
      fecha: this.toIsoIfDateLike(dto.fecha),
      loteId: typeof dto.loteId === 'string' ? Number(dto.loteId) : dto.loteId
    };
    return this.http.post<LoteSeguimientoDto>(this.base, payload).pipe(catchError(this.handleError));
  }

  update(dto: UpdateLoteSeguimientoDto): Observable<LoteSeguimientoDto> {
    const payload: any = {
      ...dto,
      fecha: this.toIsoIfDateLike(dto.fecha),
      loteId: typeof dto.loteId === 'string' ? Number(dto.loteId) : dto.loteId
    };
    return this.http.put<LoteSeguimientoDto>(`${this.base}/${dto.id}`, payload).pipe(catchError(this.handleError));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`).pipe(catchError(this.handleError));
  }
}

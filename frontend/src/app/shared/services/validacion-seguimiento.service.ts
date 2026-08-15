// src/app/shared/services/validacion-seguimiento.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/** Módulos que participan de la doble validación. Mismos literales que el backend. */
export type ModuloSeguimiento = 'LEVANTE' | 'PRODUCCION' | 'ENGORDE' | 'ENGORDE_EC' | 'REPRODUCTORA';

/** Un registro pendiente, tal como lo devuelve el backend. */
export interface RegistroValidacion {
  modulo: string;
  seguimientoId: number;
  fecha: string;
  validado: boolean;
  /** VALIDADO | PENDIENTE | EN_RETRASO — derivado por el backend, que es quien conoce el plazo. */
  estado: string;
  fechaLimite: string;
  validadoAt?: string | null;
  validadoPor?: string | null;
}

/** Situación de validación de un lote. */
export interface PendientesValidacion {
  modulo: string;
  loteId: number;
  /** Flag de la empresa. En false el resto es informativo: nada bloquea ni se pinta. */
  requiereValidacion: boolean;
  pendientes: number;
  vencidos: number;
  bloqueaAlta: boolean;
  mensaje: string;
  registros: RegistroValidacion[];
}

/** Lo que se aplicó al validar, para poder confirmarlo con contenido. */
export interface ResultadoValidacion {
  modulo: string;
  seguimientoId: number;
  itemsAplicados: number;
  kgAplicados: number;
  avesDescontadas: number;
}

/** Respuesta neutra: es lo que se devuelve cuando la consulta falla (fail-closed). */
const SIN_PENDIENTES: PendientesValidacion = Object.freeze({
  modulo: '', loteId: 0, requiereValidacion: false,
  pendientes: 0, vencidos: 0, bloqueaAlta: false, mensaje: '', registros: []
});

/**
 * Cliente de la doble validación de los seguimientos diarios.
 *
 * <p>`pendientes()` es **fail-closed a "no pasa nada"**: si la consulta falla, devuelve todo en cero
 * y sin bloqueo. Un error de red no puede terminar mostrando una alarma roja falsa ni impidiendo que
 * el operario cargue el día — el bloqueo real lo aplica el backend al guardar, así que no se pierde
 * el control.</p>
 */
@Injectable({ providedIn: 'root' })
export class ValidacionSeguimientoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/SeguimientoValidacion`;

  /** Situación del lote: pendientes, vencidos y si bloquea el alta. */
  pendientes(modulo: ModuloSeguimiento, loteId: number): Observable<PendientesValidacion> {
    return this.http
      .get<PendientesValidacion>(`${this.baseUrl}/${modulo}/pendientes`, { params: { loteId } })
      .pipe(catchError(() => of({ ...SIN_PENDIENTES, modulo, loteId })));
  }

  /** Valida un registro: aplica el consumo de alimento y el descuento de aves. */
  validar(modulo: ModuloSeguimiento, seguimientoId: number): Observable<ResultadoValidacion> {
    return this.http.post<ResultadoValidacion>(`${this.baseUrl}/${modulo}/${seguimientoId}/validar`, {});
  }

  /** Quita la validación: devuelve alimento y aves, y deja el registro editable. */
  desvalidar(modulo: ModuloSeguimiento, seguimientoId: number): Observable<ResultadoValidacion> {
    return this.http.post<ResultadoValidacion>(`${this.baseUrl}/${modulo}/${seguimientoId}/desvalidar`, {});
  }
}

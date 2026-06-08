import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { MovimientoPolloEngordeDto } from './movimiento-pollo-engorde.service';
import { CreateVentaPanamaDespachoDto } from '../models/venta-panama.model';

/** Resultado del despacho Panamá: los movimientos creados (uno por lote). */
export interface VentaPanamaDespachoResultDto {
  movimientos: MovimientoPolloEngordeDto[];
}

/**
 * Servicio HTTP de la venta de pollo engorde Panamá (separado del genérico para aislar la lógica
 * por país). Apunta al controller `MovimientoPolloEngordePanama`.
 */
@Injectable({ providedIn: 'root' })
export class VentaPanamaPolloEngordeService {
  private readonly base = `${environment.apiUrl}/MovimientoPolloEngordePanama`;

  constructor(private http: HttpClient) {}

  /** Crea el despacho Panamá: una venta Pendiente por lote (H/M sobre mixtas), en una transacción. */
  createVentaPanamaDespacho(dto: CreateVentaPanamaDespachoDto): Observable<VentaPanamaDespachoResultDto> {
    return this.http
      .post<VentaPanamaDespachoResultDto>(`${this.base}/venta-despacho`, dto)
      .pipe(catchError((e: HttpErrorResponse) => throwError(() => new Error(e?.error?.error ?? e?.message ?? 'Error al guardar la venta Panamá.'))));
  }
}

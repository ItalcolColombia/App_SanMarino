import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface LotePosturaBaseDto {
  lotePosturaBaseId: number;
  loteNombre: string;
  codigoErp: string | null;
  descripcionErp: string | null;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  // Datos del lote
  raza: string | null;
  tipoLinea: string | null;
  fechaEncaset: string | null;   // date ISO string (yyyy-MM-dd)
  // Empresa
  companyId: number;
  companyNombre: string | null;
  // Usuario creador
  createdByUserId: number;
  // País
  paisId: number | null;
  paisNombre: string | null;
  // Granja
  farmId: number | null;
  farmNombre: string | null;
  // ERP
  erpCreate: string | null;   // date ISO string (yyyy-MM-dd)
  // Auditoría
  createdAt: string;
  /** Cantidad de lotes (tabla `lotes`) derivados de esta base, sin contar el hijo de producción. */
  totalLotes: number;
  /**
   * Existe al menos un lote de esta base sin cerrar por completo (levante + producción). Junto con
   * `totalLotes === 0` (base "sin asignar", todavía sin lotes) decide si la base se muestra por
   * defecto en el filtro Abiertos de Lote Management.
   */
  tieneLoteAbierto: boolean;
}

export interface CreateLotePosturaBaseDto {
  loteNombre: string;
  codigoErp: string | null;
  descripcionErp: string | null;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  raza: string | null;
  tipoLinea: string | null;
  fechaEncaset: string | null;   // date ISO string (yyyy-MM-dd)
  farmId: number | null;
  erpCreate: string | null;   // date ISO string (yyyy-MM-dd)
}

export interface UpdateLotePosturaBaseDto {
  loteNombre: string;
  codigoErp: string | null;
  descripcionErp: string | null;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  raza: string | null;
  tipoLinea: string | null;
  fechaEncaset: string | null;   // date ISO string (yyyy-MM-dd)
  farmId: number | null;
  erpCreate: string | null;   // date ISO string (yyyy-MM-dd)
}

@Injectable({ providedIn: 'root' })
export class LotePosturaBaseService {
  private readonly baseUrl = '/api/LotePosturaBase';
  constructor(private http: HttpClient) {}

  getAll(): Observable<LotePosturaBaseDto[]> {
    return this.http.get<LotePosturaBaseDto[]>(this.baseUrl);
  }

  getById(id: number): Observable<LotePosturaBaseDto> {
    return this.http.get<LotePosturaBaseDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: CreateLotePosturaBaseDto): Observable<LotePosturaBaseDto> {
    return this.http.post<LotePosturaBaseDto>(this.baseUrl, dto);
  }

  update(id: number, dto: UpdateLotePosturaBaseDto): Observable<LotePosturaBaseDto> {
    return this.http.put<LotePosturaBaseDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

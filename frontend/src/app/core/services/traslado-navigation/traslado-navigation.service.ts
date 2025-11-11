// src/app/core/services/traslado-navigation/traslado-navigation.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface UbicacionCompleta {
  loteId?: number;
  granjaId?: number;
  nucleoId?: string;
  galponId?: string;
  companyId?: number;
  loteNombre?: string;
  granjaNombre?: string;
  nucleoNombre?: string;
  galponNombre?: string;
  companyNombre?: string;
  regional?: string;
  departamento?: string;
  municipio?: string;
  tipoGalpon?: string;
  anchoGalpon?: string;
  largoGalpon?: string;
  raza?: string;
  linea?: string;
  tipoLinea?: string;
  codigoGuiaGenetica?: string;
  anoTablaGenetica?: number;
  tecnico?: string;
  status?: string;
  fechaEncaset?: string;
  edadInicial?: number;
}

export interface MovimientoAvesCompleto {
  id: number;
  numeroMovimiento: string;
  fechaMovimiento: string;
  tipoMovimiento: string;
  origen: UbicacionCompleta;
  destino: UbicacionCompleta;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  totalAves: number;
  estado: string;
  motivoMovimiento?: string;
  observaciones?: string;
  usuarioMovimientoId: number;
  usuarioNombre?: string;
  fechaProcesamiento?: string;
  fechaCancelacion?: string;
  createdAt: string;
  updatedAt?: string;
  esMovimientoInterno: boolean;
  esMovimientoEntreGranjas: boolean;
  tipoMovimientoDescripcion: string;
}

export interface MovimientoAvesCompletoSearchRequest {
  tipoMovimiento?: string;
  estado?: string;
  fechaDesde?: string;
  fechaHasta?: string;
  loteOrigenId?: number;
  granjaOrigenId?: number;
  nucleoOrigenId?: string;
  galponOrigenId?: string;
  companyOrigenId?: number;
  loteDestinoId?: number;
  granjaDestinoId?: number;
  nucleoDestinoId?: string;
  galponDestinoId?: string;
  companyDestinoId?: number;
  usuarioMovimientoId?: number;
  sortBy?: string;
  sortDesc?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ResumenTraslado {
  id: number;
  numeroMovimiento: string;
  fechaMovimiento: string;
  estado: string;
  origenResumen: string;
  destinoResumen: string;
  totalAves: number;
  usuarioNombre?: string;
}

export interface EstadisticasTraslado {
  totalMovimientos: number;
  movimientosPendientes: number;
  movimientosCompletados: number;
  movimientosCancelados: number;
  totalAvesTrasladadas: number;
  movimientosInternos: number;
  movimientosEntreGranjas: number;
  fechaDesde?: string;
  fechaHasta?: string;
  porGranja: EstadisticaPorGranja[];
  porTipo: EstadisticaPorTipo[];
}

export interface EstadisticaPorGranja {
  granjaId: number;
  granjaNombre: string;
  totalMovimientos: number;
  totalAves: number;
  movimientosEntrada: number;
  movimientosSalida: number;
}

export interface EstadisticaPorTipo {
  tipoMovimiento: string;
  cantidad: number;
  totalAves: number;
  porcentaje: number;
}

/**
 * DTO unificado para traslados de aves y huevos
 */
export interface TrasladoUnificado {
  id: number;
  numeroTraslado: string;
  fechaTraslado: string;
  tipoOperacion: string;
  tipoTraslado: 'Aves' | 'Huevos';

  // Lote origen
  loteIdOrigen: string;
  loteIdOrigenInt?: number;
  granjaOrigenId: number;
  granjaOrigenNombre?: string;

  // Lote destino
  loteIdDestino?: string;
  loteIdDestinoInt?: number;
  granjaDestinoId?: number;
  granjaDestinoNombre?: string;
  tipoDestino?: string;

  // Cantidades (aves)
  cantidadHembras?: number;
  cantidadMachos?: number;
  totalAves?: number;

  // Cantidades (huevos)
  totalHuevos?: number;
  cantidadLimpio?: number;
  cantidadTratado?: number;
  cantidadSucio?: number;
  cantidadDeforme?: number;
  cantidadBlanco?: number;
  cantidadDobleYema?: number;
  cantidadPiso?: number;
  cantidadPequeno?: number;
  cantidadRoto?: number;
  cantidadDesecho?: number;
  cantidadOtro?: number;

  // Estado e información
  estado: string;
  motivo?: string;
  descripcion?: string;
  observaciones?: string;

  // Usuario
  usuarioTrasladoId: number;
  usuarioNombre?: string;

  // Fechas
  fechaProcesamiento?: string;
  fechaCancelacion?: string;
  createdAt: string;
  updatedAt?: string;

  // Información del lote
  faseLote?: string; // "Levante" o "Produccion"
  tieneSeguimientoProduccion: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class TrasladoNavigationService {
  private readonly apiUrl = `${environment.apiUrl}/TrasladoNavigation`;

  constructor(private http: HttpClient) {}

  /**
   * Busca movimientos con navegación completa
   */
  searchCompleto(request: MovimientoAvesCompletoSearchRequest): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/search`, request);
  }

  /**
   * Obtiene un movimiento específico con navegación completa
   */
  getCompletoById(id: number): Observable<MovimientoAvesCompleto> {
    return this.http.get<MovimientoAvesCompleto>(`${this.apiUrl}/${id}`);
  }

  /**
   * Obtiene resúmenes de traslados recientes
   */
  getResumenesRecientes(dias: number = 7, limite: number = 10): Observable<ResumenTraslado[]> {
    return this.http.get<ResumenTraslado[]>(`${this.apiUrl}/resumenes?dias=${dias}&limite=${limite}`);
  }

  /**
   * Obtiene estadísticas completas de traslados
   */
  getEstadisticasCompletas(fechaDesde?: string, fechaHasta?: string): Observable<EstadisticasTraslado> {
    let url = `${this.apiUrl}/estadisticas`;
    const params = new URLSearchParams();

    if (fechaDesde) params.append('fechaDesde', fechaDesde);
    if (fechaHasta) params.append('fechaHasta', fechaHasta);

    if (params.toString()) {
      url += `?${params.toString()}`;
    }

    return this.http.get<EstadisticasTraslado>(url);
  }

  /**
   * Obtiene movimientos por granja
   */
  getByGranja(granjaId: number, limite: number = 50): Observable<MovimientoAvesCompleto[]> {
    return this.http.get<MovimientoAvesCompleto[]>(`${this.apiUrl}/por-granja/${granjaId}?limite=${limite}`);
  }

  /**
   * Obtiene traslados por lote (aves y huevos unificados)
   */
  getByLote(loteId: number, limite: number = 100): Observable<TrasladoUnificado[]> {
    return this.http.get<TrasladoUnificado[]>(`${this.apiUrl}/por-lote/${loteId}?limite=${limite}`);
  }

  /**
   * Obtiene movimientos pendientes
   */
  getPendientes(limite: number = 50): Observable<MovimientoAvesCompleto[]> {
    return this.http.get<MovimientoAvesCompleto[]>(`${this.apiUrl}/pendientes?limite=${limite}`);
  }

  /**
   * Obtiene movimientos por tipo
   */
  getByTipo(tipoMovimiento: string, limite: number = 50): Observable<MovimientoAvesCompleto[]> {
    return this.http.get<MovimientoAvesCompleto[]>(`${this.apiUrl}/por-tipo/${tipoMovimiento}?limite=${limite}`);
  }
}

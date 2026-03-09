import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

const API = '/api/Dashboard';

// =====================================================
// INTERFACES DEL DASHBOARD
// =====================================================

export interface DashboardEstadisticasGeneralesDto {
  totalUsuarios: number;
  usuariosActivos: number;
  totalGranjas: number;
  totalLotes: number;
  totalLotesReproductora: number;
  totalLotesProduccion: number;
  totalLotesLevante: number;
  totalMovimientosPendientes: number;
  totalMovimientosCompletados: number;
  totalInventarioAves: number;
  ultimaActualizacion: string;
}

export interface ProduccionGranjaDto {
  granjaId: number;
  granjaNombre: string;
  totalLotes: number;
  totalHuevos: number;
  totalAves: number;
  eficiencia: number;
}

export interface RegistroDiarioDto {
  fecha: string;
  registrosSeguimiento: number;
  movimientosAves: number;
  cambiosInventario: number;
  totalRegistros: number;
}

export interface ActividadRecienteDto {
  id: string;
  tipo: string;
  descripcion: string;
  fecha: string;
  usuario: string;
  icono: string;
}

export interface MortalidadDto {
  fecha: string;
  cantidadMuertas: number;
  loteId: string;
  granjaNombre: string;
  causaMuerte: string;
}

export interface DistribucionLotesDto {
  granjaId: number;
  granjaNombre: string;
  lotesReproductora: number;
  lotesProduccion: number;
  lotesLevante: number;
  totalLotes: number;
}

export interface InventarioEstadisticasDto {
  totalInventarios: number;
  totalAvesHembras: number;
  totalAvesMachos: number;
  totalAvesMixtas: number;
  inventariosBajoStock: number;
  ultimaActualizacion: string;
}

export interface MetricasRendimientoDto {
  promedioProduccionDiaria: number;
  eficienciaPromedio: number;
  tasaMortalidadPromedio: number;
  movimientosPorDia: number;
  registrosPorDia: number;
  ultimaActualizacion: string;
}

export interface TendenciaDto {
  fecha: string;
  valor: number;
  categoria: string;
}

export interface AlertaDashboardDto {
  id: string;
  tipo: 'warning' | 'error' | 'info' | 'success';
  titulo: string;
  mensaje: string;
  fechaCreacion: string;
  esLeida: boolean;
  icono: string;
}

export interface KpiResumenDto {
  nombre: string;
  valor: number;
  valorAnterior: number;
  porcentajeCambio: number;
  unidad: string;
  icono: string;
  color: string;
}

/** Parámetros opcionales para filtrar datos del dashboard (empresa, usuario, granja) */
export interface DashboardFilterParams {
  companyId?: number;
  userId?: string;
  farmIds?: number[];
}

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private http = inject(HttpClient);

  private buildParams(filters?: DashboardFilterParams, extra?: Record<string, string | number>): Record<string, string> {
    const params: Record<string, string> = { ...extra } as Record<string, string>;
    if (filters?.companyId != null) params['companyId'] = String(filters.companyId);
    if (filters?.userId) params['userId'] = filters.userId;
    if (filters?.farmIds?.length) params['farmIds'] = filters.farmIds.join(',');
    return params;
  }

  // ====== ESTADÍSTICAS GENERALES ======
  getEstadisticasGenerales(filters?: DashboardFilterParams): Observable<DashboardEstadisticasGeneralesDto> {
    return this.http.get<DashboardEstadisticasGeneralesDto>(`${API}/estadisticas-generales`, {
      params: this.buildParams(filters)
    });
  }

  // ====== PRODUCCIÓN POR GRANJA ======
  getProduccionPorGranja(
    fechaDesde?: string,
    fechaHasta?: string,
    filters?: DashboardFilterParams
  ): Observable<ProduccionGranjaDto[]> {
    const params = this.buildParams(filters);
    if (fechaDesde) params['fechaDesde'] = fechaDesde;
    if (fechaHasta) params['fechaHasta'] = fechaHasta;
    return this.http.get<ProduccionGranjaDto[]>(`${API}/produccion-por-granja`, { params });
  }

  // ====== REGISTROS DIARIOS ======
  getRegistrosDiarios(dias: number = 7, filters?: DashboardFilterParams): Observable<RegistroDiarioDto[]> {
    return this.http.get<RegistroDiarioDto[]>(`${API}/registros-diarios`, {
      params: this.buildParams(filters, { dias: dias.toString() })
    });
  }

  // ====== ACTIVIDADES RECIENTES ======
  getActividadesRecientes(limite: number = 20, filters?: DashboardFilterParams): Observable<ActividadRecienteDto[]> {
    return this.http.get<ActividadRecienteDto[]>(`${API}/actividades-recientes`, {
      params: this.buildParams(filters, { limite: limite.toString() })
    });
  }

  // ====== ESTADÍSTICAS DE MORTALIDAD ======
  getEstadisticasMortalidad(dias: number = 30, filters?: DashboardFilterParams): Observable<MortalidadDto[]> {
    return this.http.get<MortalidadDto[]>(`${API}/estadisticas-mortalidad`, {
      params: this.buildParams(filters, { dias: dias.toString() })
    });
  }

  // ====== DISTRIBUCIÓN DE LOTES ======
  getDistribucionLotes(filters?: DashboardFilterParams): Observable<DistribucionLotesDto[]> {
    return this.http.get<DistribucionLotesDto[]>(`${API}/distribucion-lotes`, {
      params: this.buildParams(filters)
    });
  }

  // ====== ESTADÍSTICAS DE INVENTARIO ======
  getEstadisticasInventario(filters?: DashboardFilterParams): Observable<InventarioEstadisticasDto> {
    return this.http.get<InventarioEstadisticasDto>(`${API}/estadisticas-inventario`, {
      params: this.buildParams(filters)
    });
  }

  // ====== MÉTRICAS DE RENDIMIENTO ======
  getMetricasRendimiento(filters?: DashboardFilterParams): Observable<MetricasRendimientoDto> {
    return this.http.get<MetricasRendimientoDto>(`${API}/metricas-rendimiento`, {
      params: this.buildParams(filters)
    });
  }

  // ====== UTILIDADES ======
  formatNumber(num: number): string {
    return num.toLocaleString('es-ES');
  }

  formatPercentage(num: number): string {
    return `${num.toFixed(1)}%`;
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('es-ES', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }

  formatDateTime(date: string): string {
    return new Date(date).toLocaleString('es-ES', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  getTimeAgo(date: string): string {
    const now = new Date();
    const past = new Date(date);
    const diffInSeconds = Math.floor((now.getTime() - past.getTime()) / 1000);

    if (diffInSeconds < 60) {
      return 'Hace un momento';
    } else if (diffInSeconds < 3600) {
      const minutes = Math.floor(diffInSeconds / 60);
      return `Hace ${minutes} minuto${minutes > 1 ? 's' : ''}`;
    } else if (diffInSeconds < 86400) {
      const hours = Math.floor(diffInSeconds / 3600);
      return `Hace ${hours} hora${hours > 1 ? 's' : ''}`;
    } else {
      const days = Math.floor(diffInSeconds / 86400);
      return `Hace ${days} día${days > 1 ? 's' : ''}`;
    }
  }

  getAlertTypeIcon(tipo: string): string {
    switch (tipo) {
      case 'warning': return '⚠️';
      case 'error': return '❌';
      case 'info': return 'ℹ️';
      case 'success': return '✅';
      default: return '📋';
    }
  }

  getAlertTypeColor(tipo: string): string {
    switch (tipo) {
      case 'warning': return '#f59e0b';
      case 'error': return '#ef4444';
      case 'info': return '#3b82f6';
      case 'success': return '#10b981';
      default: return '#6b7280';
    }
  }
}

// =====================================================
// MODELOS TYPESCRIPT PARA TRASLADOS DE AVES
// ZooSanMarino Frontend - Angular
// =====================================================

// =====================================================
// INTERFACES PRINCIPALES - INVENTARIO DE AVES
// =====================================================

/**
 * DTO principal para inventario de aves
 */
export interface InventarioAvesDto {
  id: number;
  companyId: number;
  loteId: string;
  granjaId: number;
  nucleoId: string;
  galponId?: string;
  cantidadHembras: number;
  cantidadMachos: number;
  fechaUltimoConteo: Date;
  createdAt: Date;
  updatedAt?: Date;
}

/**
 * DTO para crear nuevo inventario
 */
export interface CreateInventarioAvesDto {
  loteId: string;
  granjaId: number;
  nucleoId: string;
  galponId?: string;
  cantidadHembras: number;
  cantidadMachos: number;
  fechaUltimoConteo: Date;
}

/**
 * DTO para actualizar inventario existente
 */
export interface UpdateInventarioAvesDto extends CreateInventarioAvesDto {
  id: number;
}

/**
 * Request para búsqueda de inventarios
 */
export interface InventarioAvesSearchRequest {
  loteId?: string;
  granjaId?: number;
  nucleoId?: string;
  galponId?: string;
  estado?: string;
  fechaDesde?: Date;
  fechaHasta?: Date;
  soloActivos?: boolean;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// =====================================================
// INTERFACES PRINCIPALES - MOVIMIENTO DE AVES
// =====================================================

/**
 * DTO principal para movimiento de aves
 */
export interface MovimientoAvesDto {
  id: number;
  companyId: number;
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  tipoMovimiento: string;
  observaciones?: string;
  fechaMovimiento: Date;
  createdAt: Date;
  updatedAt?: Date;
}

/**
 * DTO para crear nuevo movimiento
 */
export interface CreateMovimientoAvesDto {
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  tipoMovimiento: string;
  observaciones?: string;
  fechaMovimiento: Date;
}

/**
 * DTO para actualizar movimiento existente
 */
export interface UpdateMovimientoAvesDto extends CreateMovimientoAvesDto {
  id: number;
}

/**
 * Request para búsqueda de movimientos
 */
export interface MovimientoAvesSearchRequest {
  numeroMovimiento?: string;
  tipoMovimiento?: string;
  estado?: string;
  loteOrigenId?: string;
  loteDestinoId?: string;
  granjaOrigenId?: number;
  granjaDestinoId?: number;
  fechaDesde?: Date;
  fechaHasta?: Date;
  usuarioMovimientoId?: number;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// =====================================================
// INTERFACES PRINCIPALES - HISTORIAL DE INVENTARIO
// =====================================================

/**
 * DTO principal para historial de inventario
 */
export interface HistorialInventarioDto {
  id: number;
  companyId: number;
  loteId: string;
  cantidadHembrasAntes: number;
  cantidadMachosAntes: number;
  cantidadHembrasDespues: number;
  cantidadMachosDespues: number;
  tipoEvento: string;
  referenciaMovimientoId?: string;
  fechaRegistro: Date;
  createdAt: Date;
  updatedAt?: Date;
}

/**
 * DTO para crear registro de historial
 */
export interface CreateHistorialInventarioDto {
  loteId: string;
  cantidadHembrasAntes: number;
  cantidadMachosAntes: number;
  cantidadHembrasDespues: number;
  cantidadMachosDespues: number;
  tipoEvento: string;
  referenciaMovimientoId?: string;
  fechaRegistro: Date;
}

/**
 * Request para búsqueda en historial
 */
export interface HistorialInventarioSearchRequest {
  inventarioId?: number;
  loteId?: string;
  tipoCambio?: string;
  movimientoId?: number;
  granjaId?: number;
  nucleoId?: string;
  galponId?: string;
  fechaDesde?: Date;
  fechaHasta?: Date;
  usuarioCambioId?: number;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// =====================================================
// INTERFACES AUXILIARES
// =====================================================

/**
 * Resultado paginado genérico
 */
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

/**
 * Resumen de inventario por ubicación
 */
export interface ResumenInventarioDto {
  granjaId: number;
  granjaNombre: string;
  nucleoId: string;
  nucleoNombre?: string;
  galponId?: string;
  galponNombre?: string;
  cantidadLotes: number;
  totalHembras: number;
  totalMachos: number;
  totalAves: number;
  fechaUltimaActualizacion: Date;
}

/**
 * Resumen general del sistema
 */
export interface ResumenGeneralInventario {
  totalLotes: number;
  totalHembras: number;
  totalMachos: number;
  totalAves: number;
  resumenPorGranja: ResumenInventarioDto[];
  ultimaActualizacion: Date;
}

/**
 * Request para traslado rápido
 */
export interface TrasladoRapidoRequest {
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  observaciones?: string;
}

/**
 * Response de traslado rápido
 */
export interface TrasladoRapidoResponse {
  success: boolean;
  message: string;
  movimientoId?: number;
  inventarioOrigenActualizado?: InventarioActualizadoDto;
  inventarioDestinoActualizado?: InventarioActualizadoDto;
  errores?: string[];
}

/**
 * DTO para inventario actualizado
 */
export interface InventarioActualizadoDto {
  loteId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  totalAves: number;
  fechaActualizacion: Date;
}

/**
 * Request para ajuste de inventario
 */
export interface AjusteInventarioRequest {
  cantidadHembras: number;
  cantidadMachos: number;
  tipoEvento: string;
  observaciones?: string;
}

/**
 * Response de ajuste de inventario
 */
export interface AjusteInventarioResponse {
  success: boolean;
  message: string;
  inventarioAnterior: {
    cantidadHembras: number;
    cantidadMachos: number;
  };
  inventarioNuevo: {
    cantidadHembras: number;
    cantidadMachos: number;
  };
  diferencia: {
    hembras: number;
    machos: number;
    total: number;
  };
}

/**
 * Evento de trazabilidad
 */
export interface EventoTrazabilidadDto {
  fecha: Date;
  tipoEvento: string;
  descripcion: string;
  cantidadHembrasAntes: number;
  cantidadMachosAntes: number;
  cantidadHembrasDespues: number;
  cantidadMachosDespues: number;
  usuario?: string;
  referenciaMovimiento?: string;
  observaciones?: string;
}

/**
 * Trazabilidad completa de un lote
 */
export interface TrazabilidadLoteDto {
  loteId: string;
  loteNombre?: string;
  granjaActual?: string;
  nucleoActual?: string;
  galponActual?: string;
  inventarioActual: {
    cantidadHembras: number;
    cantidadMachos: number;
    totalAves: number;
  };
  eventos: EventoTrazabilidadDto[];
  estadisticas: {
    totalMovimientos: number;
    totalAjustes: number;
    totalMortalidad: number;
    fechaPrimerRegistro: Date;
    fechaUltimoMovimiento: Date;
  };
}

/**
 * Validación de traslado
 */
export interface ValidacionTrasladoDto {
  esValido: boolean;
  errores: string[];
  advertencias: string[];
  inventarioOrigen?: {
    disponibleHembras: number;
    disponibleMachos: number;
    totalDisponible: number;
  };
  inventarioDestino?: {
    capacidadRestante: number;
    puedeRecibir: boolean;
  };
}

/**
 * Estadísticas de movimientos
 */
export interface EstadisticasMovimientos {
  totalMovimientos: number;
  movimientosPorTipo: { [tipo: string]: number };
  movimientosPorMes: { mes: string; cantidad: number }[];
  lotesConMasMovimientos: { loteId: string; cantidad: number }[];
  promedioAvesPorMovimiento: number;
  ultimoMovimiento: Date;
}

// =====================================================
// ENUMS Y CONSTANTES
// =====================================================

/**
 * Tipos de movimiento disponibles
 */
export enum TipoMovimiento {
  TRASLADO = 'Traslado',
  VENTA = 'Venta',
  MORTALIDAD = 'Mortalidad',
  SELECCION = 'Selección',
  AJUSTE = 'Ajuste',
  COMPRA = 'Compra',
  DEVOLUCION = 'Devolución'
}

/**
 * Estados de movimiento
 */
export enum EstadoMovimiento {
  PENDIENTE = 'Pendiente',
  PROCESADO = 'Procesado',
  COMPLETADO = 'Completado',
  CANCELADO = 'Cancelado',
  ERROR = 'Error'
}

/**
 * Tipos de evento en historial
 */
export enum TipoEvento {
  CREACION = 'Creación',
  MOVIMIENTO = 'Movimiento',
  AJUSTE = 'Ajuste',
  CONTEO = 'Conteo',
  CORRECCION = 'Corrección',
  ELIMINACION = 'Eliminación'
}

/**
 * Estados de inventario
 */
export enum EstadoInventario {
  ACTIVO = 'Activo',
  INACTIVO = 'Inactivo',
  BLOQUEADO = 'Bloqueado',
  EN_REVISION = 'En Revisión'
}

/**
 * Niveles de alerta
 */
export enum NivelAlerta {
  INFO = 'info',
  WARNING = 'warning',
  ERROR = 'error',
  SUCCESS = 'success'
}

// =====================================================
// CONSTANTES DE CONFIGURACIÓN
// =====================================================

/**
 * Configuración del sistema de traslados
 */
export const TRASLADOS_CONFIG = {
  // Límites del sistema
  MAX_AVES_POR_MOVIMIENTO: 10000,
  MIN_AVES_POR_MOVIMIENTO: 1,
  MAX_MOVIMIENTOS_POR_DIA: 50,
  
  // Paginación
  PAGE_SIZES: [10, 20, 50, 100],
  DEFAULT_PAGE_SIZE: 20,
  
  // Validaciones
  TOLERANCIA_CONTEO: 0.05, // 5% de tolerancia en conteos
  DIAS_HISTORIAL_DEFECTO: 30,
  
  // Colores para estados
  COLORES_ESTADO: {
    [EstadoMovimiento.PENDIENTE]: '#ffc107',
    [EstadoMovimiento.PROCESADO]: '#17a2b8',
    [EstadoMovimiento.COMPLETADO]: '#28a745',
    [EstadoMovimiento.CANCELADO]: '#dc3545',
    [EstadoMovimiento.ERROR]: '#fd7e14'
  },
  
  // Iconos para tipos de movimiento
  ICONOS_MOVIMIENTO: {
    [TipoMovimiento.TRASLADO]: 'fas fa-exchange-alt',
    [TipoMovimiento.VENTA]: 'fas fa-money-bill-wave',
    [TipoMovimiento.MORTALIDAD]: 'fas fa-skull-crossbones',
    [TipoMovimiento.SELECCION]: 'fas fa-filter',
    [TipoMovimiento.AJUSTE]: 'fas fa-balance-scale',
    [TipoMovimiento.COMPRA]: 'fas fa-shopping-cart',
    [TipoMovimiento.DEVOLUCION]: 'fas fa-undo'
  }
};

/**
 * Mensajes de error específicos
 */
export const TRASLADOS_ERRORS = {
  INVENTARIO_INSUFICIENTE: 'No hay suficientes aves en el lote origen',
  LOTE_NO_ENCONTRADO: 'El lote especificado no existe',
  LOTE_INACTIVO: 'El lote está inactivo y no permite movimientos',
  MISMO_LOTE: 'El lote origen y destino no pueden ser el mismo',
  CANTIDAD_INVALIDA: 'La cantidad debe ser mayor a 0',
  CAPACIDAD_EXCEDIDA: 'El lote destino no tiene capacidad suficiente',
  MOVIMIENTO_DUPLICADO: 'Ya existe un movimiento similar en proceso',
  SIN_PERMISOS: 'No tiene permisos para realizar esta operación',
  FECHA_INVALIDA: 'La fecha del movimiento no puede ser futura',
  LOTE_BLOQUEADO: 'El lote está bloqueado para movimientos'
};

/**
 * Mensajes de éxito
 */
export const TRASLADOS_SUCCESS = {
  MOVIMIENTO_CREADO: 'Movimiento registrado exitosamente',
  TRASLADO_COMPLETADO: 'Traslado realizado con éxito',
  INVENTARIO_ACTUALIZADO: 'Inventario actualizado correctamente',
  AJUSTE_REALIZADO: 'Ajuste de inventario completado',
  MOVIMIENTO_CANCELADO: 'Movimiento cancelado exitosamente'
};

// =====================================================
// UTILIDADES Y HELPERS
// =====================================================

/**
 * Clase utilitaria para traslados de aves
 */
export class TrasladosUtils {
  
  /**
   * Calcula el total de aves
   */
  static calcularTotalAves(hembras: number, machos: number): number {
    return (hembras || 0) + (machos || 0);
  }

  /**
   * Calcula el porcentaje de hembras
   */
  static calcularPorcentajeHembras(hembras: number, machos: number): number {
    const total = this.calcularTotalAves(hembras, machos);
    return total > 0 ? (hembras / total) * 100 : 0;
  }

  /**
   * Valida si un movimiento es válido
   */
  static validarMovimiento(movimiento: CreateMovimientoAvesDto): string[] {
    const errores: string[] = [];

    if (!movimiento.loteOrigenId) {
      errores.push('Lote origen es requerido');
    }

    if (!movimiento.loteDestinoId) {
      errores.push('Lote destino es requerido');
    }

    if (movimiento.loteOrigenId === movimiento.loteDestinoId) {
      errores.push(TRASLADOS_ERRORS.MISMO_LOTE);
    }

    const totalAves = this.calcularTotalAves(movimiento.cantidadHembras, movimiento.cantidadMachos);
    if (totalAves <= 0) {
      errores.push(TRASLADOS_ERRORS.CANTIDAD_INVALIDA);
    }

    if (totalAves > TRASLADOS_CONFIG.MAX_AVES_POR_MOVIMIENTO) {
      errores.push(`No se pueden mover más de ${TRASLADOS_CONFIG.MAX_AVES_POR_MOVIMIENTO} aves en un solo movimiento`);
    }

    if (movimiento.fechaMovimiento > new Date()) {
      errores.push(TRASLADOS_ERRORS.FECHA_INVALIDA);
    }

    return errores;
  }

  /**
   * Formatea un número de aves para mostrar
   */
  static formatearCantidadAves(cantidad: number): string {
    return cantidad.toLocaleString('es-ES');
  }

  /**
   * Obtiene el color para un estado de movimiento
   */
  static getColorEstado(estado: EstadoMovimiento): string {
    return TRASLADOS_CONFIG.COLORES_ESTADO[estado] || '#6c757d';
  }

  /**
   * Obtiene el icono para un tipo de movimiento
   */
  static getIconoMovimiento(tipo: TipoMovimiento): string {
    return TRASLADOS_CONFIG.ICONOS_MOVIMIENTO[tipo] || 'fas fa-exchange-alt';
  }

  /**
   * Calcula la diferencia entre dos inventarios
   */
  static calcularDiferenciaInventario(
    antes: { hembras: number; machos: number },
    despues: { hembras: number; machos: number }
  ): { hembras: number; machos: number; total: number } {
    return {
      hembras: despues.hembras - antes.hembras,
      machos: despues.machos - antes.machos,
      total: this.calcularTotalAves(despues.hembras, despues.machos) - 
             this.calcularTotalAves(antes.hembras, antes.machos)
    };
  }

  /**
   * Genera un resumen de movimientos por tipo
   */
  static generarResumenMovimientos(movimientos: MovimientoAvesDto[]): { [tipo: string]: number } {
    return movimientos.reduce((resumen, movimiento) => {
      const tipo = movimiento.tipoMovimiento;
      resumen[tipo] = (resumen[tipo] || 0) + 1;
      return resumen;
    }, {} as { [tipo: string]: number });
  }

  /**
   * Filtra movimientos por rango de fechas
   */
  static filtrarPorFechas(
    movimientos: MovimientoAvesDto[],
    fechaDesde?: Date,
    fechaHasta?: Date
  ): MovimientoAvesDto[] {
    return movimientos.filter(movimiento => {
      const fecha = new Date(movimiento.fechaMovimiento);
      
      if (fechaDesde && fecha < fechaDesde) return false;
      if (fechaHasta && fecha > fechaHasta) return false;
      
      return true;
    });
  }

  /**
   * Ordena movimientos por fecha
   */
  static ordenarPorFecha(
    movimientos: MovimientoAvesDto[],
    descendente: boolean = true
  ): MovimientoAvesDto[] {
    return [...movimientos].sort((a, b) => {
      const fechaA = new Date(a.fechaMovimiento).getTime();
      const fechaB = new Date(b.fechaMovimiento).getTime();
      
      return descendente ? fechaB - fechaA : fechaA - fechaB;
    });
  }

  /**
   * Valida si un inventario tiene capacidad suficiente
   */
  static validarCapacidad(
    inventarioActual: { hembras: number; machos: number },
    capacidadMaxima: number,
    avesAdicionales: { hembras: number; machos: number }
  ): boolean {
    const totalActual = this.calcularTotalAves(inventarioActual.hembras, inventarioActual.machos);
    const totalAdicional = this.calcularTotalAves(avesAdicionales.hembras, avesAdicionales.machos);
    
    return (totalActual + totalAdicional) <= capacidadMaxima;
  }

  /**
   * Genera estadísticas de un lote
   */
  static generarEstadisticasLote(
    loteId: string,
    movimientos: MovimientoAvesDto[],
    historial: HistorialInventarioDto[]
  ): any {
    const movimientosLote = movimientos.filter(m => 
      m.loteOrigenId === loteId || m.loteDestinoId === loteId
    );
    
    const historialLote = historial.filter(h => h.loteId === loteId);
    
    return {
      totalMovimientos: movimientosLote.length,
      movimientosEntrada: movimientosLote.filter(m => m.loteDestinoId === loteId).length,
      movimientosSalida: movimientosLote.filter(m => m.loteOrigenId === loteId).length,
      totalCambiosHistorial: historialLote.length,
      fechaPrimerMovimiento: movimientosLote.length > 0 ? 
        new Date(Math.min(...movimientosLote.map(m => new Date(m.fechaMovimiento).getTime()))) : null,
      fechaUltimoMovimiento: movimientosLote.length > 0 ? 
        new Date(Math.max(...movimientosLote.map(m => new Date(m.fechaMovimiento).getTime()))) : null
    };
  }
}

// =====================================================
// EXPORT DEFAULT
// =====================================================

export default {
  // Interfaces principales
  InventarioAvesDto,
  CreateInventarioAvesDto,
  UpdateInventarioAvesDto,
  InventarioAvesSearchRequest,
  
  MovimientoAvesDto,
  CreateMovimientoAvesDto,
  UpdateMovimientoAvesDto,
  MovimientoAvesSearchRequest,
  
  HistorialInventarioDto,
  CreateHistorialInventarioDto,
  HistorialInventarioSearchRequest,
  
  // Interfaces auxiliares
  PagedResult,
  ResumenInventarioDto,
  ResumenGeneralInventario,
  TrasladoRapidoRequest,
  TrasladoRapidoResponse,
  EventoTrazabilidadDto,
  TrazabilidadLoteDto,
  
  // Enums
  TipoMovimiento,
  EstadoMovimiento,
  TipoEvento,
  EstadoInventario,
  NivelAlerta,
  
  // Constantes
  TRASLADOS_CONFIG,
  TRASLADOS_ERRORS,
  TRASLADOS_SUCCESS,
  
  // Utilidades
  TrasladosUtils
};

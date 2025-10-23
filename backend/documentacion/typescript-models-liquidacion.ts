// =====================================================
// MODELOS TYPESCRIPT PARA LIQUIDACIÓN TÉCNICA
// ZooSanMarino Frontend - Angular
// =====================================================

// =====================================================
// INTERFACES PRINCIPALES
// =====================================================

/**
 * DTO principal para liquidación técnica
 */
export interface LiquidacionTecnicaDto {
  loteId: string;
  loteNombre: string;
  fechaEncaset?: Date;
  raza?: string;
  anoTablaGenetica?: number;
  
  // Aves encasetadas
  hembrasEncasetadas: number;
  machosEncasetados: number;
  totalAvesEncasetadas: number;
  
  // Porcentajes de mortalidad
  porcentajeMortalidadHembras: number;
  porcentajeMortalidadMachos: number;
  
  // Porcentajes de selección
  porcentajeSeleccionHembras: number;
  porcentajeSeleccionMachos: number;
  
  // Porcentajes de error de sexaje
  porcentajeErrorSexajeHembras: number;
  porcentajeErrorSexajeMachos: number;
  
  // Porcentajes de retiro total
  porcentajeRetiroTotalHembras: number;
  porcentajeRetiroTotalMachos: number;
  porcentajeRetiroTotalGeneral: number;
  porcentajeRetiroGuia?: number;
  
  // Consumo de alimento
  consumoAlimentoRealGramos: number;
  consumoAlimentoGuiaGramos?: number;
  porcentajeDiferenciaConsumo?: number;
  
  // Peso semana 25
  pesoSemana25RealHembras?: number;
  pesoSemana25GuiaHembras?: number;
  porcentajeDiferenciaPesoHembras?: number;
  
  // Uniformidad
  uniformidadRealHembras?: number;
  uniformidadGuiaHembras?: number;
  porcentajeDiferenciaUniformidadHembras?: number;
}

/**
 * DTO completo con detalles de seguimiento y guía genética
 */
export interface LiquidacionTecnicaCompletaDto {
  resumen: LiquidacionTecnicaDto;
  detallesSeguimiento: SeguimientoLoteLevanteDto[];
  detallesGuiaGenetica: ProduccionAvicolaRawDto[];
}

/**
 * Request para cálculo de liquidación
 */
export interface LiquidacionTecnicaRequest {
  loteId: string;
  fechaHasta: Date;
}

// =====================================================
// INTERFACES DE SEGUIMIENTO
// =====================================================

/**
 * DTO para seguimiento semanal del lote
 */
export interface SeguimientoLoteLevanteDto {
  id: number;
  companyId: number;
  loteId: string;
  semana: number;
  fechaRegistro: Date;
  
  // Mortalidad
  mortalidadHembras?: number;
  mortalidadMachos?: number;
  
  // Selección
  seleccionHembras?: number;
  seleccionMachos?: number;
  
  // Error de sexaje
  errorSexajeHembras?: number;
  errorSexajeMachos?: number;
  
  // Consumo y peso
  consumoAlimento?: number;
  pesoPromedioHembras?: number;
  pesoPromedioMachos?: number;
  
  // Uniformidad
  uniformidadHembras?: number;
  uniformidadMachos?: number;
  
  // Información adicional
  observaciones?: string;
  createdAt: Date;
  updatedAt?: Date;
}

// =====================================================
// INTERFACES DE GUÍA GENÉTICA
// =====================================================

/**
 * DTO para datos de guía genética (producción avícola raw)
 */
export interface ProduccionAvicolaRawDto {
  id: number;
  companyId: number;
  
  // Información básica
  anioGuia?: string;
  raza?: string;
  edad?: string;
  
  // Mortalidad semanal
  mortSemH?: string;  // % Mortalidad semanal hembras
  mortSemM?: string;  // % Mortalidad semanal machos
  
  // Retiro acumulado
  retiroAcH?: string; // Retiro acumulado hembras
  retiroAcM?: string; // Retiro acumulado machos
  
  // Consumo acumulado
  consAcH?: string;   // Consumo acumulado hembras
  consAcM?: string;   // Consumo acumulado machos
  
  // Ganancia diaria
  grAveDiaH?: string; // Gramos ave/día hembras
  grAveDiaM?: string; // Gramos ave/día machos
  
  // Peso
  pesoH?: string;     // Peso hembras
  pesoM?: string;     // Peso machos
  
  // Uniformidad
  uniformidad?: string;
  
  // Producción (para reproductoras)
  hTotalAa?: string;      // Huevos total ave alojada
  prodPorcentaje?: string; // % Producción
  hIncAa?: string;        // Huevos incubables ave alojada
  aprovSem?: string;      // % Aprovechamiento semanal
  pesoHuevo?: string;     // Peso huevo
  masaHuevo?: string;     // Masa huevo
  grasaPorcentaje?: string; // % Grasa
  nacimPorcentaje?: string; // % Nacimiento
  pollitoAa?: string;     // Pollitos ave alojada
  
  // Consumo energético
  kcalAveDiaH?: string;   // Kcal ave/día hembras
  kcalAveDiaM?: string;   // Kcal ave/día machos
  
  // Aprovechamiento
  aprovAc?: string;       // % Aprovechamiento acumulado
  
  // Pesos específicos
  grHuevoT?: string;      // Gramos/huevo total
  grHuevoInc?: string;    // Gramos/huevo incubable
  grPollito?: string;     // Gramos/pollito
  
  // Valores comerciales
  valor1000?: string;     // Valor 1000
  valor150?: string;      // Valor 150
  
  // Apareamiento
  apareo?: string;        // % Apareo
  pesoMh?: string;        // Peso M/H
}

// =====================================================
// INTERFACES AUXILIARES
// =====================================================

/**
 * Resultado de validación de lotes
 */
export interface ValidacionLotesResult {
  [loteId: string]: boolean;
}

/**
 * Indicador para comparación real vs guía
 */
export interface IndicadorComparativo {
  concepto: string;
  real: number | null;
  guia: number | null;
  diferencia: number | null;
  unidad: string;
  estado: 'success' | 'warning' | 'danger' | 'info';
  tolerancia?: number;
}

/**
 * Resumen de indicadores clave
 */
export interface ResumenIndicadores {
  mortalidad: IndicadorComparativo;
  consumo: IndicadorComparativo;
  peso: IndicadorComparativo;
  uniformidad: IndicadorComparativo;
  retiroTotal: IndicadorComparativo;
}

/**
 * Configuración de rangos normales
 */
export interface RangosNormales {
  mortalidadHembras: { min: number; max: number };
  mortalidadMachos: { min: number; max: number };
  consumoAlimento: { min: number; max: number };
  pesoSemana25: { min: number; max: number };
  uniformidad: { min: number; max: number };
  retiroTotal: { min: number; max: number };
}

// =====================================================
// INTERFACES PARA COMPONENTES
// =====================================================

/**
 * Estado del componente de liquidación
 */
export interface LiquidacionState {
  loading: boolean;
  error: string | null;
  liquidacion: LiquidacionTecnicaDto | null;
  liquidacionCompleta: LiquidacionTecnicaCompletaDto | null;
}

/**
 * Filtros para búsqueda de liquidaciones
 */
export interface LiquidacionFiltros {
  loteId?: string;
  raza?: string;
  fechaDesde?: Date;
  fechaHasta?: Date;
  granjaId?: number;
}

/**
 * Opciones de exportación
 */
export interface ExportOptions {
  formato: 'pdf' | 'excel' | 'csv';
  incluirDetalles: boolean;
  incluirGraficos: boolean;
  fechaGeneracion: Date;
}

// =====================================================
// ENUMS Y CONSTANTES
// =====================================================

/**
 * Estados posibles de un indicador
 */
export enum EstadoIndicador {
  EXCELENTE = 'success',
  BUENO = 'info', 
  REGULAR = 'warning',
  MALO = 'danger'
}

/**
 * Tipos de movimiento en liquidación
 */
export enum TipoMovimientoLiquidacion {
  MORTALIDAD = 'mortalidad',
  SELECCION = 'seleccion',
  ERROR_SEXAJE = 'error_sexaje',
  RETIRO_TOTAL = 'retiro_total'
}

/**
 * Razas disponibles
 */
export enum RazasDisponibles {
  COBB_500 = 'Cobb 500',
  ROSS_308 = 'Ross 308',
  ARBOR_ACRES = 'Arbor Acres',
  HUBBARD = 'Hubbard'
}

// =====================================================
// CONSTANTES DE CONFIGURACIÓN
// =====================================================

/**
 * Configuración por defecto del sistema
 */
export const LIQUIDACION_CONFIG = {
  SEMANA_MAXIMA: 25,
  TOLERANCIA_DEFECTO: 5, // Porcentaje
  RANGOS_NORMALES: {
    mortalidadHembras: { min: 2.0, max: 5.0 },
    mortalidadMachos: { min: 3.0, max: 6.0 },
    consumoAlimento: { min: 2700, max: 2900 },
    pesoSemana25: { min: 2300, max: 2500 },
    uniformidad: { min: 85, max: 92 },
    retiroTotal: { min: 4.0, max: 6.0 }
  } as RangosNormales,
  COLORES_ESTADO: {
    success: '#28a745',
    info: '#17a2b8',
    warning: '#ffc107',
    danger: '#dc3545'
  }
};

/**
 * Mensajes de error comunes
 */
export const LIQUIDACION_ERRORS = {
  LOTE_NO_ENCONTRADO: 'Lote no encontrado o sin datos para liquidación',
  PARAMETROS_INVALIDOS: 'Parámetros inválidos para el cálculo',
  ERROR_SERVIDOR: 'Error interno del servidor',
  SIN_AUTORIZACION: 'No autorizado. Inicie sesión nuevamente',
  ERROR_RED: 'Error de conexión. Verifique su conexión a internet'
};

// =====================================================
// UTILIDADES Y HELPERS
// =====================================================

/**
 * Clase utilitaria para cálculos de liquidación
 */
export class LiquidacionUtils {
  
  /**
   * Calcula el estado de un indicador basado en rangos
   */
  static calcularEstadoIndicador(
    real: number, 
    guia: number | null, 
    tolerancia: number = LIQUIDACION_CONFIG.TOLERANCIA_DEFECTO
  ): EstadoIndicador {
    if (!guia) return EstadoIndicador.BUENO;
    
    const diferencia = Math.abs(((real - guia) / guia) * 100);
    
    if (diferencia <= tolerancia) return EstadoIndicador.EXCELENTE;
    if (diferencia <= tolerancia * 1.5) return EstadoIndicador.BUENO;
    if (diferencia <= tolerancia * 2) return EstadoIndicador.REGULAR;
    return EstadoIndicador.MALO;
  }

  /**
   * Formatea un porcentaje para mostrar
   */
  static formatearPorcentaje(valor: number | null, decimales: number = 2): string {
    return valor !== null ? `${valor.toFixed(decimales)}%` : '-';
  }

  /**
   * Formatea un peso para mostrar
   */
  static formatearPeso(valor: number | null, decimales: number = 1): string {
    return valor !== null ? `${valor.toFixed(decimales)}g` : '-';
  }

  /**
   * Formatea un número para mostrar
   */
  static formatearNumero(valor: number | null, decimales: number = 0): string {
    return valor !== null ? valor.toLocaleString('es-ES', { 
      minimumFractionDigits: decimales,
      maximumFractionDigits: decimales 
    }) : '-';
  }

  /**
   * Valida si un lote ID tiene formato válido
   */
  static validarLoteId(loteId: string): boolean {
    return /^[A-Z]\d{3,}$/.test(loteId.trim());
  }

  /**
   * Genera indicadores comparativos desde liquidación
   */
  static generarIndicadores(liquidacion: LiquidacionTecnicaDto): IndicadorComparativo[] {
    return [
      {
        concepto: 'Mortalidad Hembras',
        real: liquidacion.porcentajeMortalidadHembras,
        guia: null,
        diferencia: null,
        unidad: '%',
        estado: 'info'
      },
      {
        concepto: 'Mortalidad Machos',
        real: liquidacion.porcentajeMortalidadMachos,
        guia: null,
        diferencia: null,
        unidad: '%',
        estado: 'info'
      },
      {
        concepto: 'Consumo Alimento',
        real: liquidacion.consumoAlimentoRealGramos,
        guia: liquidacion.consumoAlimentoGuiaGramos || null,
        diferencia: liquidacion.porcentajeDiferenciaConsumo || null,
        unidad: 'g',
        estado: this.calcularEstadoIndicador(
          liquidacion.consumoAlimentoRealGramos,
          liquidacion.consumoAlimentoGuiaGramos || null
        )
      },
      {
        concepto: 'Peso Semana 25',
        real: liquidacion.pesoSemana25RealHembras || 0,
        guia: liquidacion.pesoSemana25GuiaHembras || null,
        diferencia: liquidacion.porcentajeDiferenciaPesoHembras || null,
        unidad: 'g',
        estado: this.calcularEstadoIndicador(
          liquidacion.pesoSemana25RealHembras || 0,
          liquidacion.pesoSemana25GuiaHembras || null
        )
      },
      {
        concepto: 'Uniformidad',
        real: liquidacion.uniformidadRealHembras || 0,
        guia: liquidacion.uniformidadGuiaHembras || null,
        diferencia: liquidacion.porcentajeDiferenciaUniformidadHembras || null,
        unidad: '%',
        estado: this.calcularEstadoIndicador(
          liquidacion.uniformidadRealHembras || 0,
          liquidacion.uniformidadGuiaHembras || null
        )
      },
      {
        concepto: 'Retiro Total',
        real: liquidacion.porcentajeRetiroTotalGeneral,
        guia: liquidacion.porcentajeRetiroGuia || null,
        diferencia: liquidacion.porcentajeRetiroGuia ? 
          ((liquidacion.porcentajeRetiroTotalGeneral - liquidacion.porcentajeRetiroGuia) / liquidacion.porcentajeRetiroGuia) * 100 : null,
        unidad: '%',
        estado: this.calcularEstadoIndicador(
          liquidacion.porcentajeRetiroTotalGeneral,
          liquidacion.porcentajeRetiroGuia || null
        )
      }
    ];
  }
}

// =====================================================
// EXPORT DEFAULT PARA FACILITAR IMPORTACIÓN
// =====================================================

export default {
  // Interfaces principales
  LiquidacionTecnicaDto,
  LiquidacionTecnicaCompletaDto,
  LiquidacionTecnicaRequest,
  
  // Interfaces de datos
  SeguimientoLoteLevanteDto,
  ProduccionAvicolaRawDto,
  
  // Interfaces auxiliares
  IndicadorComparativo,
  ResumenIndicadores,
  RangosNormales,
  
  // Enums
  EstadoIndicador,
  TipoMovimientoLiquidacion,
  RazasDisponibles,
  
  // Constantes
  LIQUIDACION_CONFIG,
  LIQUIDACION_ERRORS,
  
  // Utilidades
  LiquidacionUtils
};

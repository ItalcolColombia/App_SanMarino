// =====================================================
// MODELOS TYPESCRIPT PARA GUÍA GENÉTICA
// ZooSanMarino Frontend - Angular
// =====================================================

// =====================================================
// INTERFACES PRINCIPALES
// =====================================================

/**
 * DTO principal para guía genética (ProduccionAvicolaRaw)
 */
export interface ProduccionAvicolaRawDto {
  id: number;
  companyId: number;
  
  // Información básica
  anioGuia?: string;
  raza?: string;
  edad?: string;
  
  // Mortalidad semanal
  mortSemH?: string;      // % Mortalidad semanal hembras
  mortSemM?: string;      // % Mortalidad semanal machos
  
  // Retiro acumulado
  retiroAcH?: string;     // Retiro acumulado hembras
  retiroAcM?: string;     // Retiro acumulado machos
  
  // Consumo acumulado
  consAcH?: string;       // Consumo acumulado hembras (gramos)
  consAcM?: string;       // Consumo acumulado machos (gramos)
  
  // Ganancia diaria
  grAveDiaH?: string;     // Gramos ave/día hembras
  grAveDiaM?: string;     // Gramos ave/día machos
  
  // Peso
  pesoH?: string;         // Peso hembras (gramos)
  pesoM?: string;         // Peso machos (gramos)
  
  // Uniformidad
  uniformidad?: string;   // % Uniformidad
  
  // Producción (para reproductoras)
  hTotalAa?: string;      // Huevos total ave alojada
  prodPorcentaje?: string; // % Producción
  hIncAa?: string;        // Huevos incubables ave alojada
  aprovSem?: string;      // % Aprovechamiento semanal
  pesoHuevo?: string;     // Peso huevo (gramos)
  masaHuevo?: string;     // Masa huevo (gramos)
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

/**
 * DTO para crear nuevo registro de guía genética
 */
export interface CreateProduccionAvicolaRawDto {
  companyId: number;
  anioGuia?: string;
  raza?: string;
  edad?: string;
  mortSemH?: string;
  retiroAcH?: string;
  mortSemM?: string;
  retiroAcM?: string;
  consAcH?: string;
  consAcM?: string;
  grAveDiaH?: string;
  grAveDiaM?: string;
  pesoH?: string;
  pesoM?: string;
  uniformidad?: string;
  hTotalAa?: string;
  prodPorcentaje?: string;
  hIncAa?: string;
  aprovSem?: string;
  pesoHuevo?: string;
  masaHuevo?: string;
  grasaPorcentaje?: string;
  nacimPorcentaje?: string;
  pollitoAa?: string;
  kcalAveDiaH?: string;
  kcalAveDiaM?: string;
  aprovAc?: string;
  grHuevoT?: string;
  grHuevoInc?: string;
  grPollito?: string;
  valor1000?: string;
  valor150?: string;
  apareo?: string;
  pesoMh?: string;
}

/**
 * DTO para actualizar registro existente
 */
export interface UpdateProduccionAvicolaRawDto extends CreateProduccionAvicolaRawDto {
  id: number;
}

/**
 * Request para búsqueda paginada
 */
export interface ProduccionAvicolaRawSearchRequest {
  anioGuia?: string;
  raza?: string;
  edad?: string;
  companyId?: number;
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDesc?: boolean;
}

// =====================================================
// INTERFACES DE EXCEL
// =====================================================

/**
 * Resultado de importación Excel
 */
export interface ExcelImportResultDto {
  success: boolean;
  message: string;
  totalRowsProcessed: number;
  totalRowsImported: number;
  totalRowsFailed: number;
  errors: string[];
}

/**
 * Información del template Excel
 */
export interface ExcelTemplateInfoDto {
  tableName: string;
  requiredColumns: string[];
  optionalColumns: string[];
  allPossibleHeaders: string[];
}

/**
 * Mapeo de columnas Excel
 */
export interface ExcelColumnMapping {
  excelHeader: string;
  dtoProperty: string;
  required: boolean;
  dataType: 'string' | 'number' | 'percentage';
  description: string;
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
 * Filtros para la vista de lista
 */
export interface GuiaGeneticaFiltros {
  anioGuia?: string;
  raza?: string;
  edadDesde?: number;
  edadHasta?: number;
  busquedaTexto?: string;
}

/**
 * Opciones de visualización
 */
export interface VisualizacionOpciones {
  mostrarSoloReproductoras: boolean;
  mostrarSoloLevante: boolean;
  agruparPorRaza: boolean;
  mostrarGraficos: boolean;
}

/**
 * Estadísticas de guía genética
 */
export interface EstadisticasGuiaGenetica {
  totalRegistros: number;
  razasDisponibles: number;
  aniosDisponibles: number;
  edadMinima: number;
  edadMaxima: number;
  ultimaActualizacion: Date;
}

/**
 * Comparación entre guías
 */
export interface ComparacionGuias {
  raza1: string;
  raza2: string;
  edad: string;
  diferencias: DiferenciaIndicador[];
}

/**
 * Diferencia entre indicadores
 */
export interface DiferenciaIndicador {
  indicador: string;
  valor1: number;
  valor2: number;
  diferencia: number;
  porcentajeDiferencia: number;
  unidad: string;
}

// =====================================================
// ENUMS Y CONSTANTES
// =====================================================

/**
 * Razas disponibles en el sistema
 */
export enum RazasDisponibles {
  COBB_500 = 'Cobb 500',
  ROSS_308 = 'Ross 308',
  ARBOR_ACRES = 'Arbor Acres',
  HUBBARD = 'Hubbard',
  COBB_700 = 'Cobb 700',
  ROSS_AP95 = 'Ross AP95'
}

/**
 * Tipos de ave según su propósito
 */
export enum TipoAve {
  LEVANTE = 'levante',
  REPRODUCTORA = 'reproductora',
  ENGORDE = 'engorde'
}

/**
 * Indicadores principales de la guía genética
 */
export enum IndicadoresGuia {
  MORTALIDAD_H = 'mortSemH',
  MORTALIDAD_M = 'mortSemM',
  CONSUMO_H = 'consAcH',
  CONSUMO_M = 'consAcM',
  PESO_H = 'pesoH',
  PESO_M = 'pesoM',
  UNIFORMIDAD = 'uniformidad',
  PRODUCCION = 'prodPorcentaje'
}

/**
 * Estados de validación de datos
 */
export enum EstadoValidacion {
  VALIDO = 'valid',
  ADVERTENCIA = 'warning',
  ERROR = 'error',
  PENDIENTE = 'pending'
}

// =====================================================
// CONSTANTES DE CONFIGURACIÓN
// =====================================================

/**
 * Configuración del sistema de guía genética
 */
export const GUIA_GENETICA_CONFIG = {
  // Rangos de edad
  EDAD_MINIMA: 1,
  EDAD_MAXIMA: 100,
  SEMANAS_LEVANTE: 25,
  SEMANAS_PRODUCCION: 65,
  
  // Validaciones
  TOLERANCIA_VALORES: 0.1,
  VALORES_MAXIMOS: {
    mortalidad: 10.0,
    consumo: 5000,
    peso: 5000,
    uniformidad: 100,
    produccion: 100
  },
  
  // Formato de archivos
  EXCEL_EXTENSIONS: ['.xlsx', '.xls'],
  MAX_FILE_SIZE: 10 * 1024 * 1024, // 10MB
  
  // Paginación
  PAGE_SIZES: [10, 20, 50, 100],
  DEFAULT_PAGE_SIZE: 20,
  
  // Colores para gráficos
  COLORES_RAZAS: {
    'Cobb 500': '#FF6384',
    'Ross 308': '#36A2EB',
    'Arbor Acres': '#FFCE56',
    'Hubbard': '#4BC0C0',
    'Cobb 700': '#9966FF',
    'Ross AP95': '#FF9F40'
  }
};

/**
 * Mapeo de columnas Excel a propiedades DTO
 */
export const EXCEL_COLUMN_MAPPINGS: ExcelColumnMapping[] = [
  {
    excelHeader: 'AÑOGUÍA',
    dtoProperty: 'anioGuia',
    required: true,
    dataType: 'string',
    description: 'Año de la guía genética'
  },
  {
    excelHeader: 'RAZA',
    dtoProperty: 'raza',
    required: true,
    dataType: 'string',
    description: 'Raza de las aves'
  },
  {
    excelHeader: 'Edad',
    dtoProperty: 'edad',
    required: true,
    dataType: 'number',
    description: 'Edad en semanas'
  },
  {
    excelHeader: '%MortSemH',
    dtoProperty: 'mortSemH',
    required: false,
    dataType: 'percentage',
    description: 'Porcentaje mortalidad semanal hembras'
  },
  {
    excelHeader: 'RetiroAcH',
    dtoProperty: 'retiroAcH',
    required: false,
    dataType: 'percentage',
    description: 'Retiro acumulado hembras'
  },
  {
    excelHeader: '%MortSemM',
    dtoProperty: 'mortSemM',
    required: false,
    dataType: 'percentage',
    description: 'Porcentaje mortalidad semanal machos'
  },
  {
    excelHeader: 'RetiroAcM',
    dtoProperty: 'retiroAcM',
    required: false,
    dataType: 'percentage',
    description: 'Retiro acumulado machos'
  },
  {
    excelHeader: 'ConsAcH',
    dtoProperty: 'consAcH',
    required: false,
    dataType: 'number',
    description: 'Consumo acumulado hembras (gramos)'
  },
  {
    excelHeader: 'ConsAcM',
    dtoProperty: 'consAcM',
    required: false,
    dataType: 'number',
    description: 'Consumo acumulado machos (gramos)'
  },
  {
    excelHeader: 'PesoH',
    dtoProperty: 'pesoH',
    required: false,
    dataType: 'number',
    description: 'Peso hembras (gramos)'
  },
  {
    excelHeader: 'PesoM',
    dtoProperty: 'pesoM',
    required: false,
    dataType: 'number',
    description: 'Peso machos (gramos)'
  },
  {
    excelHeader: '%Uniform',
    dtoProperty: 'uniformidad',
    required: false,
    dataType: 'percentage',
    description: 'Porcentaje de uniformidad'
  }
];

/**
 * Mensajes de error específicos
 */
export const GUIA_GENETICA_ERRORS = {
  ARCHIVO_INVALIDO: 'Archivo Excel inválido o corrupto',
  COLUMNAS_FALTANTES: 'Faltan columnas requeridas en el archivo',
  DATOS_INVALIDOS: 'Los datos contienen valores inválidos',
  RAZA_NO_RECONOCIDA: 'Raza no reconocida en el sistema',
  EDAD_FUERA_RANGO: 'Edad fuera del rango permitido (1-100 semanas)',
  VALORES_NEGATIVOS: 'Los valores no pueden ser negativos',
  PORCENTAJE_INVALIDO: 'Porcentaje debe estar entre 0 y 100',
  DUPLICADO: 'Ya existe un registro para esta raza, año y edad',
  SIN_PERMISOS: 'No tiene permisos para realizar esta acción'
};

// =====================================================
// UTILIDADES Y HELPERS
// =====================================================

/**
 * Clase utilitaria para guía genética
 */
export class GuiaGeneticaUtils {
  
  /**
   * Convierte string a número, manejando valores nulos/vacíos
   */
  static parseNumericValue(value: string | undefined | null): number | null {
    if (!value || value.trim() === '') return null;
    const parsed = parseFloat(value.replace(',', '.'));
    return isNaN(parsed) ? null : parsed;
  }

  /**
   * Formatea un valor numérico para mostrar
   */
  static formatNumericValue(value: string | number | null, decimales: number = 1): string {
    if (value === null || value === undefined || value === '') return '-';
    const numValue = typeof value === 'string' ? this.parseNumericValue(value) : value;
    return numValue !== null ? numValue.toFixed(decimales) : '-';
  }

  /**
   * Formatea un porcentaje para mostrar
   */
  static formatPercentage(value: string | number | null, decimales: number = 2): string {
    const formatted = this.formatNumericValue(value, decimales);
    return formatted !== '-' ? `${formatted}%` : '-';
  }

  /**
   * Valida si una raza es válida
   */
  static isValidRaza(raza: string): boolean {
    return Object.values(RazasDisponibles).includes(raza as RazasDisponibles);
  }

  /**
   * Valida si una edad está en rango válido
   */
  static isValidEdad(edad: string | number): boolean {
    const numEdad = typeof edad === 'string' ? this.parseNumericValue(edad) : edad;
    return numEdad !== null && 
           numEdad >= GUIA_GENETICA_CONFIG.EDAD_MINIMA && 
           numEdad <= GUIA_GENETICA_CONFIG.EDAD_MAXIMA;
  }

  /**
   * Valida si un porcentaje está en rango válido
   */
  static isValidPercentage(value: string | number): boolean {
    const numValue = typeof value === 'string' ? this.parseNumericValue(value) : value;
    return numValue !== null && numValue >= 0 && numValue <= 100;
  }

  /**
   * Determina el tipo de ave según la edad
   */
  static getTipoAve(edad: string | number): TipoAve {
    const numEdad = typeof edad === 'string' ? this.parseNumericValue(edad) : edad;
    if (!numEdad) return TipoAve.LEVANTE;
    
    if (numEdad <= GUIA_GENETICA_CONFIG.SEMANAS_LEVANTE) {
      return TipoAve.LEVANTE;
    } else if (numEdad <= GUIA_GENETICA_CONFIG.SEMANAS_PRODUCCION) {
      return TipoAve.REPRODUCTORA;
    } else {
      return TipoAve.ENGORDE;
    }
  }

  /**
   * Obtiene el color para una raza específica
   */
  static getColorRaza(raza: string): string {
    return GUIA_GENETICA_CONFIG.COLORES_RAZAS[raza] || '#999999';
  }

  /**
   * Valida un registro completo de guía genética
   */
  static validateGuiaRecord(guia: CreateProduccionAvicolaRawDto): string[] {
    const errors: string[] = [];

    // Validar campos requeridos
    if (!guia.anioGuia) errors.push('Año de guía es requerido');
    if (!guia.raza) errors.push('Raza es requerida');
    if (!guia.edad) errors.push('Edad es requerida');

    // Validar raza
    if (guia.raza && !this.isValidRaza(guia.raza)) {
      errors.push(GUIA_GENETICA_ERRORS.RAZA_NO_RECONOCIDA);
    }

    // Validar edad
    if (guia.edad && !this.isValidEdad(guia.edad)) {
      errors.push(GUIA_GENETICA_ERRORS.EDAD_FUERA_RANGO);
    }

    // Validar porcentajes
    const percentageFields = ['mortSemH', 'mortSemM', 'uniformidad', 'prodPorcentaje'];
    percentageFields.forEach(field => {
      const value = guia[field as keyof CreateProduccionAvicolaRawDto];
      if (value && !this.isValidPercentage(value)) {
        errors.push(`${field}: ${GUIA_GENETICA_ERRORS.PORCENTAJE_INVALIDO}`);
      }
    });

    // Validar valores no negativos
    const numericFields = ['consAcH', 'consAcM', 'pesoH', 'pesoM'];
    numericFields.forEach(field => {
      const value = guia[field as keyof CreateProduccionAvicolaRawDto];
      if (value) {
        const numValue = this.parseNumericValue(value);
        if (numValue !== null && numValue < 0) {
          errors.push(`${field}: ${GUIA_GENETICA_ERRORS.VALORES_NEGATIVOS}`);
        }
      }
    });

    return errors;
  }

  /**
   * Genera estadísticas desde una lista de guías
   */
  static generateEstadisticas(guias: ProduccionAvicolaRawDto[]): EstadisticasGuiaGenetica {
    const razas = new Set(guias.map(g => g.raza).filter(r => r));
    const anios = new Set(guias.map(g => g.anioGuia).filter(a => a));
    const edades = guias.map(g => this.parseNumericValue(g.edad)).filter(e => e !== null) as number[];

    return {
      totalRegistros: guias.length,
      razasDisponibles: razas.size,
      aniosDisponibles: anios.size,
      edadMinima: edades.length > 0 ? Math.min(...edades) : 0,
      edadMaxima: edades.length > 0 ? Math.max(...edades) : 0,
      ultimaActualizacion: new Date()
    };
  }

  /**
   * Filtra guías por criterios específicos
   */
  static filterGuias(
    guias: ProduccionAvicolaRawDto[], 
    filtros: GuiaGeneticaFiltros
  ): ProduccionAvicolaRawDto[] {
    return guias.filter(guia => {
      // Filtro por año
      if (filtros.anioGuia && guia.anioGuia !== filtros.anioGuia) {
        return false;
      }

      // Filtro por raza
      if (filtros.raza && guia.raza !== filtros.raza) {
        return false;
      }

      // Filtro por rango de edad
      const edad = this.parseNumericValue(guia.edad);
      if (filtros.edadDesde && edad && edad < filtros.edadDesde) {
        return false;
      }
      if (filtros.edadHasta && edad && edad > filtros.edadHasta) {
        return false;
      }

      // Filtro por texto (busca en raza y año)
      if (filtros.busquedaTexto) {
        const texto = filtros.busquedaTexto.toLowerCase();
        const coincide = 
          (guia.raza && guia.raza.toLowerCase().includes(texto)) ||
          (guia.anioGuia && guia.anioGuia.toLowerCase().includes(texto));
        if (!coincide) return false;
      }

      return true;
    });
  }

  /**
   * Ordena guías por criterio específico
   */
  static sortGuias(
    guias: ProduccionAvicolaRawDto[], 
    sortBy: string, 
    sortDesc: boolean = false
  ): ProduccionAvicolaRawDto[] {
    return [...guias].sort((a, b) => {
      let valueA: any = a[sortBy as keyof ProduccionAvicolaRawDto];
      let valueB: any = b[sortBy as keyof ProduccionAvicolaRawDto];

      // Convertir a números si es necesario
      if (sortBy === 'edad' || sortBy.includes('peso') || sortBy.includes('cons')) {
        valueA = this.parseNumericValue(valueA) || 0;
        valueB = this.parseNumericValue(valueB) || 0;
      }

      // Comparar valores
      let comparison = 0;
      if (valueA < valueB) comparison = -1;
      if (valueA > valueB) comparison = 1;

      return sortDesc ? -comparison : comparison;
    });
  }
}

// =====================================================
// EXPORT DEFAULT
// =====================================================

export default {
  // Interfaces principales
  ProduccionAvicolaRawDto,
  CreateProduccionAvicolaRawDto,
  UpdateProduccionAvicolaRawDto,
  ProduccionAvicolaRawSearchRequest,
  
  // Interfaces Excel
  ExcelImportResultDto,
  ExcelTemplateInfoDto,
  ExcelColumnMapping,
  
  // Interfaces auxiliares
  PagedResult,
  GuiaGeneticaFiltros,
  VisualizacionOpciones,
  EstadisticasGuiaGenetica,
  ComparacionGuias,
  DiferenciaIndicador,
  
  // Enums
  RazasDisponibles,
  TipoAve,
  IndicadoresGuia,
  EstadoValidacion,
  
  // Constantes
  GUIA_GENETICA_CONFIG,
  EXCEL_COLUMN_MAPPINGS,
  GUIA_GENETICA_ERRORS,
  
  // Utilidades
  GuiaGeneticaUtils
};

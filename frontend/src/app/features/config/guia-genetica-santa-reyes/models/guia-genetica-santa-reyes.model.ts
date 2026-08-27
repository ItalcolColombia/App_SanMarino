// src/app/features/config/guia-genetica-santa-reyes/models/guia-genetica-santa-reyes.model.ts
/**
 * Contrato de la guía genética **reducida** (`guia_genetica_santa_reyes`).
 *
 * Espejo exacto de `Application/DTOs/GuiaGeneticaSantaReyesDtos.cs` y de
 * `API/Controllers/GuiaGeneticaSantaReyesController.cs` (`api/guia-genetica-santa-reyes`).
 *
 * 🔴 **No confundir con las otras dos guías.** El repo tiene TRES tablas de guía genética,
 * separadas a propósito, y cada una con su pantalla:
 *
 * | Pantalla | Ruta | Tabla | Modelo |
 * |---|---|---|---|
 * | Pollo Engorde | `/config/guia-genetica-ecuador` | `guia_genetica_ecuador_header/_detalle` | eje DÍA, sexo por fila |
 * | Sanmarino | `/config/guia-genetica` | `guia_genetica_sanmarino_colombia` | ~50 columnas, todo `text` |
 * | **Santa Reyes (ésta)** | `/config/guia-genetica-santa-reyes` | `guia_genetica_santa_reyes` | plana: 3 métricas numéricas |
 *
 * Los tipos de acá son **numéricos** (`decimal` en base), no `string` como los de la tabla
 * compartida: reutilizar aquel DTO haría que el grid ordene «10» antes que «9».
 */

/** Una línea de la guía tal como la devuelve el backend. */
export interface GuiaGeneticaSantaReyesDto {
  id: number;
  companyId: number;
  raza: string;
  anioGuia: string;
  /** Semana de vida. La guía sembrada cubre 18–140 (arranca en producción). */
  edad: number;
  /**
   * % de producción de la semana. `null` significa «la línea no tiene dato para esa semana»,
   * que **no** es 0: la raza Criolla tiene 40 semanas legítimamente nulas (101–140).
   */
  prodPorcentaje: number | null;
  /** % de mortalidad ACUMULADA de hembras a esa semana (no semanal). */
  retiroAcH: number | null;
  /** Consumo en gramos/ave/día de hembras a esa semana. */
  grAveDiaH: number | null;
  /** Clave natural derivada `Raza+AnioGuia+Edad`. El front la muestra; **no** la edita. */
  codigoGuiaGenetica: string | null;
  createdAt: string;
  updatedAt: string | null;
}

/**
 * Alta de una línea. **La raza es texto libre**, no un `select` alimentado por lo que ya existe:
 * ése es el *deadlock de arranque* que hoy vuelve inservible la pantalla de Ecuador (sin guía
 * cargada no hay raza que elegir ⇒ no se puede crear la primera).
 */
export interface CreateGuiaGeneticaSantaReyesDto {
  raza: string;
  anioGuia: string;
  edad: number;
  prodPorcentaje: number | null;
  retiroAcH: number | null;
  grAveDiaH: number | null;
}

/** Edición. Cambiar raza/año/semana **recalcula** el código en el backend. */
export interface UpdateGuiaGeneticaSantaReyesDto extends CreateGuiaGeneticaSantaReyesDto {
  id: number;
}

/** Filtros + paginación del listado (viajan por query string a un `GET`). */
export interface GuiaGeneticaSantaReyesFiltros {
  /** Coincidencia parcial, case-insensitive. */
  raza?: string;
  /** Coincidencia parcial. */
  anioGuia?: string;
  /** Semana mínima, inclusive. */
  edadDesde?: number | null;
  /** Semana máxima, inclusive. */
  edadHasta?: number | null;
  page: number;
  pageSize: number;
  /** `raza` | `anioGuia` | `edad` | `prodPorcentaje` | `retiroAcH` | `grAveDiaH`. */
  sortBy?: string;
  sortDesc?: boolean;
}

/** Columnas por las que el backend sabe ordenar (`AplicarOrden` del service). */
export type ColumnaOrdenGuia =
  | 'raza'
  | 'anioGuia'
  | 'edad'
  | 'prodPorcentaje'
  | 'retiroAcH'
  | 'grAveDiaH';

/** `Application/DTOs/Common/PagedResult<T>`. */
export interface PagedResultGuia<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

/** Una fila del Excel que no se pudo importar, con el número TAL COMO SE VE en Excel. */
export interface GuiaGeneticaSantaReyesImportErrorDto {
  fila: number;
  motivo: string;
}

/**
 * Resultado del import.
 *
 * El import es **idempotente** por `codigo = Raza+AnioGuia+Edad`: reimportar el mismo archivo
 * actualiza lo que cambió y no duplica nada. Una fila idéntica a lo guardado cae en `omitidos`,
 * **no** en `actualizados` (reescribirla ensuciaría `updated_at` de toda la guía).
 */
export interface GuiaGeneticaSantaReyesImportResultDto {
  /** El archivo entró **completo**: ninguna fila quedó rechazada. */
  success: boolean;
  totalFilas: number;
  insertados: number;
  actualizados: number;
  /** Idénticas a lo guardado + las filas en blanco que Excel arrastra al final de la hoja. */
  omitidos: number;
  errores: GuiaGeneticaSantaReyesImportErrorDto[];
}

/** Fila ya lista para pintar en el grid (valores formateados, sin lógica en el template). */
export interface FilaGuiaGeneticaSantaReyes {
  id: number;
  raza: string;
  anioGuia: string;
  edad: number;
  /** Semana formateada para la columna (`S 18`). */
  edadTexto: string;
  prodPorcentajeTexto: string;
  retiroAcHTexto: string;
  grAveDiaHTexto: string;
  codigoGuiaGenetica: string;
  /** `true` si la semana cae fuera de 18–140: se marca, no se rechaza. */
  fueraDeCobertura: boolean;
  /** El DTO original, para el modal de edición (evita un GET por fila). */
  origen: GuiaGeneticaSantaReyesDto;
}

/** Estado del formulario de alta/edición (lo que el usuario tipea, todo `string`). */
export interface FormularioGuiaGeneticaSantaReyes {
  id: number | null;
  raza: string;
  anioGuia: string;
  edad: string;
  prodPorcentaje: string;
  retiroAcH: string;
  grAveDiaH: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// Cobertura de la guía
// ─────────────────────────────────────────────────────────────────────────────

/** Primera semana que cubre la guía de producción. */
export const SEMANA_COBERTURA_MIN = 18;

/** Última semana que cubre la guía de producción. */
export const SEMANA_COBERTURA_MAX = 140;

/**
 * Nota que la pantalla muestra **siempre**, no escondida en un tooltip: la guía arranca en
 * producción y los reportes de levante cubren un tramo distinto. Que el usuario lo lea acá evita
 * que lo descubra por un reporte a medias.
 */
export const NOTA_COBERTURA_GUIA =
  'Esta guía cubre semanas 18 a 140 (producción). Los reportes de levante cubren semanas 1 a 25.';

/** Límites de las columnas en base (`raza varchar(80)`, `anio_guia varchar(10)`). */
export const MAX_LARGO_RAZA = 80;

/** @see MAX_LARGO_RAZA */
export const MAX_LARGO_ANIO_GUIA = 10;

/**
 * Encabezados del Excel, en el orden de la plantilla que genera el backend
 * (`GuiaGeneticaSantaReyesCalculos.ColumnasPlantilla`). El export los usa **tal cual** para que el
 * archivo exportado sea un archivo importable.
 */
export const COLUMNAS_PLANTILLA_GUIA = [
  'raza',
  'anio_guia',
  'edad',
  'prod_porcentaje',
  'retiro_ac_h',
  'gr_ave_dia_h'
] as const;

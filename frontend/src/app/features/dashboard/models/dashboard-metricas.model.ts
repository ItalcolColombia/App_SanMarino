// features/dashboard/models/dashboard-metricas.model.ts
//
// Vocabulario COMPARTIDO de los paneles: período, filtros, estado de carga y las formas que las
// funciones de gráfica consumen.
//
// ⚠️ Los DTO concretos de cada panel (qué trae exactamente el endpoint de postura, el de engorde…)
// viven con su panel y se agregan en la fase que construye ese endpoint. Acá no se declaran a
// futuro: un contrato inventado antes de tener el endpoint es una suposición disfrazada de tipo.

/** Ventana de tiempo que el usuario eligió mirar. */
export interface PeriodoDashboard {
  /** Fecha inicial inclusive, `YYYY-MM-DD`. */
  desde: string;
  /** Fecha final inclusive, `YYYY-MM-DD`. */
  hasta: string;
}

/**
 * Filtros de la página. **No llevan `companyId`**: la empresa la resuelve el backend desde la sesión
 * (`ICurrentUser.CompanyId`, ya validada por `ActiveCompanyMiddleware`) y mandarla desde el cliente
 * sería confiar en el header crudo, que es justo lo que el repo prohíbe.
 */
export interface FiltrosDashboard {
  periodo: PeriodoDashboard;
  /** Granja concreta, o `null` para todas las del alcance del usuario. */
  farmId: number | null;
}

/**
 * Estado de un panel. Los tres casos son EXCLUYENTES y se distinguen a propósito:
 * `cargando`, `error` y «cargó y no hay datos» se ven distinto en pantalla — un panel vacío que
 * parece un cero es peor que un mensaje.
 */
export interface EstadoPanel<T> {
  cargando: boolean;
  error: string | null;
  datos: T | null;
}

/** Estado inicial de cualquier panel: todavía no se pidió nada. */
export function estadoInicial<T>(): EstadoPanel<T> {
  return { cargando: false, error: null, datos: null };
}

/**
 * Un punto de una serie temporal.
 *
 * `valor: null` significa **no hay dato ese día** y se dibuja como hueco. No es cero: un día sin
 * registro y un día con mortalidad cero son hechos distintos y la gráfica no puede confundirlos.
 */
export interface PuntoSerie {
  /** `YYYY-MM-DD`. */
  fecha: string;
  valor: number | null;
}

/** Rol semántico de una serie. Decide el color; el componente no elige colores a mano. */
export type RolSerie =
  /** El dato principal del gráfico (producción, peso, stock). */
  | 'principal'
  /** Una segunda magnitud comparable. */
  | 'secundaria'
  /** La referencia contra la que se compara (guía genética, meta). Se dibuja punteada. */
  | 'referencia'
  /** Algo que está mal y hay que mirar (mortalidad, descuadre). */
  | 'alerta'
  /** Algo que está bien (cumplimiento, validado). */
  | 'exito';

/** Una serie temporal con nombre. */
export interface SerieTiempo {
  etiqueta: string;
  rol: RolSerie;
  puntos: readonly PuntoSerie[];
}

/** Una porción de una distribución (torta/dona) o una barra categórica. */
export interface ItemDistribucion {
  etiqueta: string;
  valor: number;
}

/**
 * Un día tal como viene del backend (`PuntoDiaDto`).
 *
 * El servidor emite **sólo los días con registro**; el hueco lo arma el front al construir el eje
 * con `rangoDiario`. Por eso `valor` acá es `number` y no `number | null`: un punto que llegó tiene
 * dato por definición.
 */
export interface PuntoDiaDto {
  fecha: string;
  valor: number;
}

/** Una categoría tal como viene del backend (`CategoriaDto`). Misma forma que {@link ItemDistribucion}. */
export type CategoriaDto = ItemDistribucion;

/** Una tarjeta de indicador. */
export interface Kpi {
  etiqueta: string;
  valor: string;
  /** Texto chico bajo el valor (contexto, unidad, comparación). Opcional. */
  detalle?: string | null;
  /** Marca la tarjeta cuando el número pide atención. */
  tono?: 'neutro' | 'alerta' | 'exito';
}

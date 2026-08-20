// Catálogo de los flags de comportamiento por empresa que se administran desde la pantalla de
// Empresas.
//
// POR QUÉ ES UN CATÁLOGO Y NO 14 BLOQUES DE HTML
// Cada feature por empresa nacía como columna en `companies` y se encendía a mano por SQL: el
// formulario solo exponía UNO de los trece. Eso obliga a pedirle a desarrollo cada cambio de
// configuración y deja la operación sin forma de ver qué tiene prendido.
//
// Con el catálogo, agregar un flag nuevo es UNA línea acá: el formulario arma su control, lo
// rellena al editar y lo manda en el payload solo. Si en vez de esto se escribieran los checkboxes
// a mano, el próximo flag volvería a olvidarse en alguno de los tres lugares —que es exactamente
// cómo `seguimiento_engorde_mixto` y `reporte_costos_alimento_desde_fuentes_reales` terminaron
// existiendo solo en la base de datos—.
//
// La `key` es el nombre del campo en el DTO (camelCase) y tiene que coincidir con el backend.

/** Áreas en las que se agrupan los flags dentro del formulario. */
export type GrupoFlagEmpresa = 'Inventario y alimento' | 'Postura' | 'Pollo engorde' | 'Operación';

export interface FlagEmpresa {
  /** Campo del DTO. Debe coincidir con el nombre en `CompanyDto` del backend. */
  key: string;
  /** Etiqueta corta, en el lenguaje de la operación (no el nombre de la columna). */
  titulo: string;
  /** Qué cambia al encenderlo y qué pasa si se deja apagado. */
  descripcion: string;
  grupo: GrupoFlagEmpresa;
}

/**
 * Los flags administrables, en el orden en que se muestran.
 *
 * Cada descripción dice qué pasa con el flag APAGADO, porque el default de todos es `false` y esa
 * es la pregunta real de quien configura una empresa nueva.
 */
export const FLAGS_EMPRESA: readonly FlagEmpresa[] = Object.freeze([
  // ── Inventario y alimento ────────────────────────────────────────────────
  {
    key: 'manejaAlimentoPorGalpon',
    titulo: 'Manejar el alimento a nivel galpón',
    descripcion: 'Apagado, el alimento se maneja a nivel granja. Cada granja puede cambiarlo por su cuenta.',
    grupo: 'Inventario y alimento'
  },
  {
    key: 'manejaInventarioPorSilo',
    titulo: 'Ubicar el alimento en silos',
    descripcion: 'La existencia vive en el silo o la bodega en vez del galpón, y un silo puede alimentar a varios galpones. Apagado, la ubicación sigue siendo núcleo/galpón.',
    grupo: 'Inventario y alimento'
  },
  {
    key: 'reportesAlimentoDesdeInventarioUnificado',
    titulo: 'Reportes de alimento desde el inventario unificado',
    descripcion: 'Los reportes contable y técnico leen el inventario nuevo. Apagado, siguen leyendo el inventario anterior y pueden mostrar de menos.',
    grupo: 'Inventario y alimento'
  },
  {
    key: 'reporteCostosAlimentoDesdeFuentesReales',
    titulo: 'Costos de alimento desde las fuentes reales',
    descripcion: 'El reporte de costos toma el alimento de los movimientos reales de inventario en vez del consumo declarado.',
    grupo: 'Inventario y alimento'
  },
  {
    key: 'limitaTiposInventarioAlimentoYAves',
    titulo: 'Limitar tipos de ítem a Alimento y Aves',
    descripcion: 'El catálogo de ítems de inventario solo ofrece Alimento y Aves al crear/editar/filtrar. Apagado, siguen los 6 tipos de siempre (alimento, medicamento, accesorio, biológico, consumible, otro).',
    grupo: 'Inventario y alimento'
  },

  // ── Postura ──────────────────────────────────────────────────────────────
  {
    key: 'clasificacionHuevoPorItems',
    titulo: 'Clasificar el huevo por ítems del catálogo',
    descripcion: 'El desglose se captura con ítems (Primera, Pnc…) en vez de las 11 columnas fijas. El total sigue guardándose igual, así que no se rompen espejos ni indicadores.',
    grupo: 'Postura'
  },
  {
    key: 'capturaHuevosEnLevante',
    titulo: 'Capturar huevos en levante',
    descripcion: 'Desde la semana 14 el levante registra huevos, y al liquidar el acumulado se arrastra al primer día de producción. Apagado, levante no captura huevos.',
    grupo: 'Postura'
  },
  {
    key: 'permiteTrasladoAvesCrossEtapa',
    titulo: 'Permitir traslado de aves entre etapas',
    descripcion: 'Habilita mover aves de Levante a Producción conservando su edad. El sentido inverso nunca se permite.',
    grupo: 'Postura'
  },
  {
    key: 'consumoAlimentoSoloHembras',
    titulo: 'Consumo de alimento solo hembras',
    descripcion: 'El seguimiento diario de producción y levante deja de pedir consumo de machos. Apagado, sigue pidiendo hembras y machos por separado.',
    grupo: 'Postura'
  },
  {
    key: 'ocultaMachosEnPostura',
    titulo: 'Ocultar machos en postura',
    descripcion: 'Oculta la columna Machos en mortalidad, selección, peso, uniformidad, traslados y ventas, y retira el error de sexaje del registro diario. Solo cambia la UI: el dato sigue existiendo en el modelo.',
    grupo: 'Postura'
  },
  {
    key: 'semanasCicloPosturaPorRaza',
    titulo: 'Etapas del ciclo por raza',
    descripcion: 'La etapa del seguimiento diario (alistamiento/levante/levante en producción/postura) se calcula por semana de vida y por raza, en vez de los cortes fijos. Apagado, sigue mostrando Etapa 1/2/3 como siempre.',
    grupo: 'Postura'
  },

  // ── Pollo engorde ────────────────────────────────────────────────────────
  {
    key: 'seguimientoEngordeMixto',
    titulo: 'Seguimiento de engorde en modo mixto',
    descripcion: 'El formulario diario muestra una sola columna Mixto en vez de hembras y machos por separado.',
    grupo: 'Pollo engorde'
  },
  {
    key: 'ventaEngordePesoDiferido',
    titulo: 'Peso de venta diferido (báscula)',
    descripcion: 'La venta se registra sin el peso y este se completa después con el dato de la báscula.',
    grupo: 'Pollo engorde'
  },
  {
    key: 'programacionLotesEngorde',
    titulo: 'Programación de lotes de engorde',
    descripcion: 'Permite crear el lote antes del encasetamiento y cargarle gastos mientras está programado.',
    grupo: 'Pollo engorde'
  },
  {
    key: 'nombreLoteIncluyeCorrida',
    titulo: 'El nombre del lote incluye la corrida',
    descripcion: 'Al crear un lote, el nombre se arma automáticamente con el número de corrida del galpón.',
    grupo: 'Pollo engorde'
  },

  // ── Operación ────────────────────────────────────────────────────────────
  {
    key: 'requiereValidacionSeguimientoDiario',
    titulo: 'Doble validación del seguimiento diario',
    descripcion: 'Guardar ya no descuenta: separa el alimento y las aves. El descuento se aplica al VALIDAR el registro, y hasta entonces se puede editar y eliminar. Los registros vencidos bloquean días nuevos. Apagado, el descuento ocurre al guardar, como siempre.',
    grupo: 'Operación'
  },
  {
    key: 'primerRegistroSegunHoraLlegada',
    titulo: 'Primer registro según la hora de llegada',
    descripcion: 'Si las aves llegan a las 13:00 o después, el primer día con seguimiento pasa al día siguiente del encasetamiento.',
    grupo: 'Operación'
  },
  {
    key: 'manejaCodigosErpAvicola',
    titulo: 'Códigos ERP avícolas',
    descripcion: 'Muestra bodega, centro de operación, instalación, ubicación y centro de costo en granja, núcleo, galpón y lote.',
    grupo: 'Operación'
  }
]);

/** Los grupos, en orden de aparición y sin repetir. */
export const GRUPOS_FLAGS_EMPRESA: readonly GrupoFlagEmpresa[] =
  Object.freeze([...new Set(FLAGS_EMPRESA.map(f => f.grupo))]);

/** Los flags de un grupo. */
export function flagsDelGrupo(grupo: GrupoFlagEmpresa): readonly FlagEmpresa[] {
  return FLAGS_EMPRESA.filter(f => f.grupo === grupo);
}

/**
 * Controles del formulario reactivo, todos en `false`.
 *
 * `false` y no `null` a propósito: el checkbox de un valor nulo se ve igual que apagado pero manda
 * `null`, y el backend interpreta null como «no lo toques». Un flag que el usuario apagó a mano
 * quedaría encendido sin que nada lo avise.
 */
export function controlesDeFlags(): Record<string, [boolean]> {
  return FLAGS_EMPRESA.reduce((acc, f) => {
    acc[f.key] = [false];
    return acc;
  }, {} as Record<string, [boolean]>);
}

/** Valores de los flags a partir de una empresa, para rellenar el formulario al editar. */
export function valoresDeFlags(empresa: Record<string, unknown> | null | undefined): Record<string, boolean> {
  return FLAGS_EMPRESA.reduce((acc, f) => {
    acc[f.key] = empresa?.[f.key] === true;
    return acc;
  }, {} as Record<string, boolean>);
}

/** Los flags del formulario, listos para mezclar en el payload de guardado. */
export function flagsDelFormulario(valor: Record<string, unknown> | null | undefined): Record<string, boolean> {
  return FLAGS_EMPRESA.reduce((acc, f) => {
    acc[f.key] = valor?.[f.key] === true;
    return acc;
  }, {} as Record<string, boolean>);
}

/** Cuántos flags tiene encendidos una empresa. Alimenta el contador de la lista. */
export function contarFlagsActivos(empresa: Record<string, unknown> | null | undefined): number {
  return FLAGS_EMPRESA.filter(f => empresa?.[f.key] === true).length;
}

// src/app/features/reportes-tecnicos/funciones/columnas-huevo-reporte.funcion.ts
/**
 * Función PURA (sin `this`, sin DI, sin service/estado).
 *
 * Qué columnas van bajo la cabecera «Huevos» de las tablas de producción, y con qué `colspan`.
 *
 * **Por qué existe.** Las empresas con `clasificacion_huevo_por_items` no cargan las 11 columnas
 * fijas ni el huevo incubable: el backend escribe `huevo_inc = 0` a propósito («postura comercial,
 * no incuba») y el desglose real vive en `metadata.huevoItems` como Primera / Pnc. Pintarles una
 * columna «Inc» siempre en 0 y un «%Incubables» en 0,00% no es un hueco de dato: es un número
 * inventado que parece real.
 *
 * Con el flag OFF devuelve exactamente las columnas de siempre (`Tot`, `Inc`), así que el reporte
 * de las demás empresas no cambia.
 */

/** Columnas posibles del grupo «Huevos», en orden de pintado. */
export type ColumnaHuevo = 'tot' | 'inc' | 'primera' | 'pnc' | 'otros';

export interface ColumnasHuevoReporte {
  columnas: ColumnaHuevo[];
  /** `colspan` del `<th>` agrupador «Huevos». */
  colspan: number;
  /** ¿Se pinta la columna «%Incubables»? Deriva de `huevo_inc`, así que muere con ella. */
  mostrarPorcentajeIncubables: boolean;
}

/**
 * @param clasificacionPorItems `companies.clasificacion_huevo_por_items` de la empresa del reporte.
 * @param hayOtros             ¿algún registro trae ítems con `tipoHuevo` desconocido y cantidad > 0?
 *                             La columna «Otros» sólo aparece si hay algo que mostrar ahí: una
 *                             columna en cero permanente es justamente lo que este cambio evita.
 */
export function columnasHuevoReporte(
  clasificacionPorItems: boolean,
  hayOtros = false
): ColumnasHuevoReporte {
  if (!clasificacionPorItems) {
    return { columnas: ['tot', 'inc'], colspan: 2, mostrarPorcentajeIncubables: true };
  }

  const columnas: ColumnaHuevo[] = hayOtros
    ? ['tot', 'primera', 'pnc', 'otros']
    : ['tot', 'primera', 'pnc'];

  return { columnas, colspan: columnas.length, mostrarPorcentajeIncubables: false };
}

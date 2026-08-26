// src/app/features/config/guia-genetica-santa-reyes/funciones/exportar-guia-excel.funcion.ts
/**
 * Export a `.xlsx` de la guía genética reducida.
 *
 * Usa el helper central `shared/utils/excel/exportar-tabla-excel.funcion.ts` (regla del repo:
 * prohibido volver a escribir `book_new / aoa_to_sheet / writeFile` a mano). La única parte propia
 * del módulo es **qué** columnas y **qué** filas, que es lógica de dominio.
 */
import {
  ExcelCell,
  exportarTablaExcel
} from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import {
  COLUMNAS_PLANTILLA_GUIA,
  GuiaGeneticaSantaReyesDto
} from '../models/guia-genetica-santa-reyes.model';

/**
 * Filas del `.xlsx`, en el orden de la plantilla del import. Función pura y testeable sin descargar.
 *
 * Los valores van **crudos** (números, no texto formateado) y una métrica nula sale como celda
 * **vacía**, no como 0 — que es exactamente lo que el import vuelve a leer como `NULL`.
 */
export function construirFilasExportGuia(
  items: readonly GuiaGeneticaSantaReyesDto[] | null | undefined
): ExcelCell[][] {
  return (items ?? []).map(item => [
    item.raza ?? '',
    item.anioGuia ?? '',
    item.edad,
    item.prodPorcentaje ?? null,
    item.retiroAcH ?? null,
    item.grAveDiaH ?? null
  ]);
}

/**
 * Descarga la guía como `.xlsx`.
 *
 * 🔴 **El archivo sale sin fila de título ni de filtros, a propósito.** Los encabezados quedan en
 * la fila 1 con los mismos nombres que la plantilla del backend (`raza`, `anio_guia`, `edad`,
 * `prod_porcentaje`, `retiro_ac_h`, `gr_ave_dia_h`), así que **lo que se exporta se puede volver a
 * importar**: bajar la guía, corregirla en Excel y subirla es el camino real de trabajo de este
 * módulo, y el import lee la fila 1 como encabezados. Meterle un título decorativo arriba
 * convertiría el export en un archivo que su propia pantalla no puede leer.
 *
 * Los filtros aplicados van en el **nombre del archivo**, no adentro, por el mismo motivo.
 */
export function exportarGuiaExcel(
  items: readonly GuiaGeneticaSantaReyesDto[] | null | undefined,
  sufijoNombre?: string
): void {
  const base = sufijoNombre?.trim()
    ? `guia_genetica_santa_reyes_${sufijoNombre.trim()}`
    : 'guia_genetica_santa_reyes';

  exportarTablaExcel(
    [...COLUMNAS_PLANTILLA_GUIA],
    construirFilasExportGuia(items),
    { filenameBase: base, sheetName: 'GuiaGenetica' }
  );
}

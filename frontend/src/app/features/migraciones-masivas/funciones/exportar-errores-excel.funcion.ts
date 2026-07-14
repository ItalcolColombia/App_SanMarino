// features/migraciones-masivas/funciones/exportar-errores-excel.funcion.ts
/**
 * Exporta el detalle de errores/advertencias de una corrida de migración a `.xlsx`. Delega en el
 * helper compartido de `shared/utils/excel` para el ensamblado libro/hoja/descarga; esta función
 * solo arma las filas-objeto con las columnas del reporte (prohibido `XLSX.write*` inline).
 */
import { MigracionError } from '../models/migracion.model';
import { exportarObjetosExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';

export function exportarErroresExcel(errores: MigracionError[], nombreBase: string): void {
  const rows = errores.map((e) => ({
    Fila: e.fila,
    Columna: e.columna,
    Valor: e.valor ?? '',
    Mensaje: e.mensaje,
    Severidad: e.severidad
  }));

  exportarObjetosExcel(rows, {
    sheetName: 'Errores',
    filenameBase: `Errores_${nombreBase}`
  });
}

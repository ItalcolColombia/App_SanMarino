/** Exportación a Excel del reporte comparativo de cumplimiento de vacunación. Función pura. */
import { VacunacionCumplimientoLoteDto, LINEA_PRODUCTIVA_LABEL } from '../models/vacunacion.model';
import { exportarTablaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';

const HEADERS = [
  'Granja', 'Lote', 'Línea', 'Total programadas', 'A tiempo', '% a tiempo',
  'Tardío leve', 'Incumplido (rojo)', '% tardío', 'No aplicado', '% no aplicado',
  'Pendiente', 'Promedio días atraso',
];

export function exportarCumplimientoExcel(filas: VacunacionCumplimientoLoteDto[]): void {
  const data = filas.map((f) => [
    f.granjaNombre ?? '',
    f.loteNombre,
    LINEA_PRODUCTIVA_LABEL[f.lineaProductiva] ?? f.lineaProductiva,
    f.totalProgramadas,
    f.totalATiempo,
    f.porcentajeATiempo,
    f.totalTardio1Semana,
    f.totalTardio2MasSemanas,
    f.porcentajeTardio,
    f.totalNoAplicado,
    f.porcentajeNoAplicado,
    f.totalPendiente,
    f.promedioDiasAtraso ?? '',
  ]);

  exportarTablaExcel(HEADERS, data, {
    filenameBase: 'Reporte_cumplimiento_vacunacion',
    sheetName: 'Cumplimiento',
    title: 'Reporte de cumplimiento de vacunación',
  });
}

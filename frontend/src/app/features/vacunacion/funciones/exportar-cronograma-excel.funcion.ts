/** Exportación a Excel del cronograma completo de un lote. Función pura sin estado de Angular. */
import { VacunacionCronogramaItemDto, LINEA_PRODUCTIVA_LABEL } from '../models/vacunacion.model';
import { fechaCorta } from '../../../shared/utils/format';
import { exportarTablaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';

const HEADERS = [
  'Línea', 'Vacuna', 'Unidad', 'Semana/Día', 'Fecha objetivo',
  'Franja inicio', 'Franja fin', 'Estado', 'Fecha aplicación', 'Días desviación', 'Notas',
];

export function exportarCronogramaExcel(items: VacunacionCronogramaItemDto[], loteNombre: string): void {
  const data = items.map((i) => [
    LINEA_PRODUCTIVA_LABEL[i.lineaProductiva] ?? i.lineaProductiva,
    i.itemInventarioNombre,
    i.unidadObjetivo,
    i.unidadObjetivo === 'Fecha' ? '' : (i.valorObjetivo ?? ''),
    i.unidadObjetivo === 'Fecha' ? fechaCorta(i.fechaObjetivo) : '',
    fechaCorta(i.fechaInicioFranja),
    fechaCorta(i.fechaFinFranja),
    i.registro?.estado ?? 'Pendiente',
    i.registro?.fechaAplicacion ? fechaCorta(i.registro.fechaAplicacion) : '',
    i.registro?.diasDesviacion ?? '',
    i.notas ?? '',
  ]);

  exportarTablaExcel(HEADERS, data, {
    filenameBase: `Cronograma_vacunacion_${loteNombre || 'lote'}`,
    sheetName: 'Cronograma',
    title: `Cronograma de vacunación — Lote: ${loteNombre}`,
  });
}

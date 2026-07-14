/** Exportación a Excel del historial de aplicaciones (solo ítems ya registrados). Función pura. */
import { VacunacionCronogramaItemDto, LINEA_PRODUCTIVA_LABEL } from '../models/vacunacion.model';
import { fechaCorta } from '../../../shared/utils/format';
import { exportarTablaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';

const HEADERS = [
  'Lote', 'Línea', 'Vacuna', 'Estado', 'Fecha aplicación', 'Días desviación', 'Incumplido',
  'Motivo', 'Aplicado por', 'Registrado por (id)',
];

export function exportarHistorialExcel(items: VacunacionCronogramaItemDto[], contexto: string): void {
  const aplicados = items.filter((i) => i.registro && i.registro.estado !== 'Pendiente');
  const data = aplicados.map((i) => [
    i.loteNombre,
    LINEA_PRODUCTIVA_LABEL[i.lineaProductiva] ?? i.lineaProductiva,
    i.itemInventarioNombre,
    i.registro!.estado,
    i.registro!.fechaAplicacion ? fechaCorta(i.registro!.fechaAplicacion) : '',
    i.registro!.diasDesviacion ?? '',
    i.registro!.incumplido ? 'Sí' : 'No',
    i.registro!.motivoDescripcion ?? '',
    i.registro!.aplicadoPorUserNombre ?? i.registro!.aplicadoPorNombreLibre ?? '',
    i.registro!.usuarioRegistraId,
  ]);

  exportarTablaExcel(HEADERS, data, {
    filenameBase: `Historial_aplicaciones_vacunacion_${contexto || 'lote'}`,
    sheetName: 'Historial',
    title: `Historial de aplicaciones — ${contexto}`,
  });
}

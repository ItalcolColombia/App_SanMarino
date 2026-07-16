/**
 * Exportación a Excel MULTI-HOJA del reporte de cumplimiento de vacunación:
 * Resumen (KPIs globales) + Cumplimiento por lote + Detalle por vacuna (si se consultó).
 * Función pura (la descarga es el efecto esperado del helper compartido).
 */
import {
  VacunacionCumplimientoLoteDto,
  VacunacionCumplimientoDetalleDto,
  LINEA_PRODUCTIVA_LABEL,
} from '../models/vacunacion.model';
import { fechaCorta } from '../../../shared/utils/format';
import { exportarMultiHojaExcel, HojaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { calcularKpisCumplimiento } from './calcular-kpis-cumplimiento.funcion';

const HEADERS_LOTE = [
  'Granja', 'Lote', 'Línea', 'Total programadas', 'A tiempo', '% a tiempo',
  'Tardío leve', 'Incumplido (rojo)', '% tardío', 'No aplicado', '% no aplicado',
  'Pendiente', 'Promedio días atraso',
];

const HEADERS_DETALLE = [
  'Granja', 'Lote', 'Línea', 'Vacuna', 'Programado', 'Franja inicio', 'Franja fin',
  'Estado', 'Fecha aplicación', 'Días desviación', 'Incumplido', 'Motivo',
  'Aplicado por', 'Registrado por', 'Notas',
];

function programadoLegible(d: VacunacionCumplimientoDetalleDto): string {
  if (d.unidadObjetivo === 'Semana') return `Semana ${d.valorObjetivo ?? ''}`;
  if (d.unidadObjetivo === 'Dia') return `Día ${d.valorObjetivo ?? ''}`;
  return d.fechaObjetivoEfectiva ? fechaCorta(d.fechaObjetivoEfectiva) : '';
}

export function exportarCumplimientoExcel(
  filas: VacunacionCumplimientoLoteDto[],
  detalle: VacunacionCumplimientoDetalleDto[] = [],
  filtrosTexto: string[] = [],
): void {
  const kpis = calcularKpisCumplimiento(filas);

  const hojaResumen: HojaExcel = {
    sheetName: 'Resumen',
    title: 'Reporte de cumplimiento de vacunación — Resumen',
    subtitles: filtrosTexto,
    headers: ['Indicador', 'Valor'],
    rows: [
      ['Lotes en el reporte', kpis.lotes],
      ['Vacunas programadas', kpis.totalProgramadas],
      ['Aplicadas a tiempo', kpis.totalATiempo],
      ['% a tiempo', kpis.porcentajeATiempo ?? ''],
      ['Tardías (total)', kpis.totalTardias],
      ['Incumplidas (rojo)', kpis.totalIncumplidas],
      ['% tardío', kpis.porcentajeTardio ?? ''],
      ['No aplicadas', kpis.totalNoAplicadas],
      ['% no aplicado', kpis.porcentajeNoAplicado ?? ''],
      ['Pendientes', kpis.totalPendientes],
      ['Promedio días de atraso (ponderado)', kpis.promedioDiasAtraso ?? ''],
    ],
  };

  const hojaLotes: HojaExcel = {
    sheetName: 'Cumplimiento',
    title: 'Cumplimiento por lote',
    headers: HEADERS_LOTE,
    rows: filas.map((f) => [
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
    ]),
  };

  const hojas: HojaExcel[] = [hojaResumen, hojaLotes];

  if (detalle.length) {
    hojas.push({
      sheetName: 'Detalle',
      title: 'Detalle por vacuna programada',
      headers: HEADERS_DETALLE,
      rows: detalle.map((d) => [
        d.granjaNombre ?? '',
        d.loteNombre ?? '',
        LINEA_PRODUCTIVA_LABEL[d.lineaProductiva] ?? d.lineaProductiva,
        d.vacunaNombre,
        programadoLegible(d),
        d.fechaInicioFranja ? fechaCorta(d.fechaInicioFranja) : '',
        d.fechaFinFranja ? fechaCorta(d.fechaFinFranja) : '',
        d.estado,
        d.fechaAplicacion ? fechaCorta(d.fechaAplicacion) : '',
        d.diasDesviacion ?? '',
        d.incumplido ? 'Sí' : 'No',
        d.motivo ?? '',
        d.aplicadoPor ?? '',
        d.registradoPor ?? '',
        d.notas ?? '',
      ]),
    });
  }

  exportarMultiHojaExcel(hojas, {
    filenameBase: 'Reporte_cumplimiento_vacunacion',
    sheetName: 'Resumen',
  });
}

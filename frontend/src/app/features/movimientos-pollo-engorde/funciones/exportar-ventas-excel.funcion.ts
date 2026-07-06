/**
 * Exportación a Excel del listado de ventas de pollo engorde.
 *
 * Encapsula el armado de cabeceras, filas, título, hoja y descarga del `.xlsx`. El componente solo
 * valida que haya filas, arma la metadata de contexto (granja + filtros aplicados) y muestra el
 * toast; toda la construcción del libro vive aquí. Función pura sin estado de Angular.
 */
import { MovimientoPolloEngordeDto } from '../services/movimiento-pollo-engorde.service';
import { fechaCorta } from './formato.funcion';
import { exportarTablaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';

/** Contexto de exportación: nombre de granja (título/archivo) y filtros legibles aplicados. */
export interface ExportarVentasExcelMeta {
  granjaNombre: string;
  filtros: string[];
}

const HEADERS = [
  'Número movimiento',
  'Despacho',
  'Fecha',
  'Tipo',
  'Estado',
  'Granja origen',
  'Lote origen',
  'Granja destino',
  'Lote destino',
  'Total aves',
  'Hembras',
  'Machos',
  'Mixtas',
  'Placa',
  'Hora salida',
  'Guía Agrocalidad',
  'Conductor',
  'Peso bruto',
  'Peso tara',
  'Peso neto',
  'Prom. peso/ave',
  'Observaciones'
];

/** Construye y descarga el Excel de ventas con las filas y el contexto dados. */
export function exportarVentasExcel(rows: MovimientoPolloEngordeDto[], meta: ExportarVentasExcelMeta): void {
  const data = rows.map((m) => {
    const pesoBruto = m.pesoBruto ?? null;
    const pesoTara = m.pesoTara ?? null;
    const pesoNeto = pesoBruto != null && pesoTara != null ? pesoBruto - pesoTara : null;
    const promPesoAve = pesoNeto != null && (m.totalAves ?? 0) > 0 ? pesoNeto / m.totalAves : null;
    return [
      m.numeroMovimiento ?? '',
      (m.numeroDespacho ?? '').trim(),
      fechaCorta(m.fechaMovimiento),
      m.tipoMovimiento ?? '',
      m.estado ?? '',
      m.granjaOrigenNombre ?? '',
      m.loteOrigenNombre ?? '',
      m.granjaDestinoNombre ?? '',
      m.loteDestinoNombre ?? '',
      m.totalAves ?? 0,
      m.cantidadHembras ?? 0,
      m.cantidadMachos ?? 0,
      m.cantidadMixtas ?? 0,
      m.placa ?? '',
      m.horaSalida ? String(m.horaSalida).slice(0, 5) : '',
      m.guiaAgrocalidad ?? '',
      m.conductor ?? '',
      pesoBruto ?? '',
      pesoTara ?? '',
      pesoNeto ?? '',
      promPesoAve != null ? Math.round(promPesoAve * 1000) / 1000 : '',
      (m.observaciones ?? '').trim()
    ];
  });

  const titleBase = 'Venta de Pollo Engorde';
  const granja = (meta.granjaNombre || '').trim();
  const title = granja ? `${titleBase} — Granja: ${granja}` : titleBase;

  exportarTablaExcel(HEADERS, data, {
    filenameBase: `Venta_pollo_engorde_${granja || 'granja'}`,
    sheetName: 'Ventas',
    title,
    subtitles: meta.filtros.length ? [`Filtros: ${meta.filtros.join(' · ')}`] : undefined,
  });
}

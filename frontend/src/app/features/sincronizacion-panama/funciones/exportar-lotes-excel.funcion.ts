// features/sincronizacion-panama/funciones/exportar-lotes-excel.funcion.ts
/**
 * Exporta a `.xlsx` la tabla por lote de una corrida (previsualización o sincronización). Delega en
 * el helper compartido de `shared/utils/excel` para el ensamblado libro/hoja/descarga; esta función
 * solo arma headers/filas y los subtítulos de contexto (prohibido `XLSX.write*` inline).
 */
import { ResultadoSincronizacionDto } from '../models/sincronizacion-panama.model';
import { exportarTablaExcel, ExcelCell } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { fechaCorta } from '../../../shared/utils/format';

const HEADERS: ExcelCell[] = [
  'Lote', 'Granja', 'Galpón', 'Fecha inicio', 'Aves encasetadas',
  '# Seguimientos', '# Reproductoras', '# Seg. reproductora', '# Lesiones', 'Estado', 'Mensaje'
];

export function exportarLotesSincronizacion(r: ResultadoSincronizacionDto): void {
  const rows: ExcelCell[][] = r.lotes.map((l) => [
    l.lote,
    l.granja,
    l.galpon,
    fechaCorta(l.fechaInicio),
    l.avesEncasetadas,
    l.seguimientos,
    l.reproductoras,
    l.seguimientosReproductora,
    l.lesiones ?? 0,
    l.estado,
    l.mensaje ?? ''
  ]);

  const modo = r.dryRun ? 'Previsualización' : 'Sincronización';
  const subtitles: string[] = [
    `Modo: ${modo}`,
    `Estado: ${r.estado}`,
    r.anio != null ? `Año: ${r.anio}` : 'Año: todos',
    `Empresa (companyId): ${r.companyId}`,
    `Duración: ${r.duracionMs} ms`
  ];

  exportarTablaExcel(HEADERS, rows, {
    filenameBase: `Sincronizacion_Panama_${r.dryRun ? 'preview' : 'sync'}`,
    sheetName: 'Lotes',
    title: 'Sincronización Panamá — Pollo Engorde',
    subtitles
  });
}

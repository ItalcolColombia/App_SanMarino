/**
 * Exportación a Excel del plan de vacunación de la empresa. PURA (sin estado de Angular).
 *
 * Dos hojas porque son dos preguntas distintas: «qué planes tengo» (cabeceras) y «qué vacuna va
 * cuándo» (el detalle de todas). Una sola hoja obligaría a repetir la cabecera en cada fila o a
 * perder una de las dos vistas.
 */
import { LINEA_PRODUCTIVA_LABEL } from '../models/vacunacion.model';
import { VacunacionPlantillaDto, VacunacionPlantillaDetalleDto } from '../models/vacunacion-plantilla.model';
import { fechaCorta } from '../../../shared/utils/format';
import { exportarAoaMultiHojaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { describirObjetivo } from './describir-plantilla.funcion';

const HEADERS_PLANTILLAS = ['Plantilla', 'Línea', 'Raza', 'Vigente desde', 'Activa', 'Vacunas', 'Notas'];
const HEADERS_ITEMS = ['Plantilla', 'Línea', 'Raza', 'Orden', 'Vacuna', 'Programado', 'Días antes', 'Días después', 'Notas'];

export function exportarPlantillasExcel(
  plantillas: VacunacionPlantillaDto[],
  detalles: VacunacionPlantillaDetalleDto[]
): void {
  const filasPlantillas = plantillas.map((p) => [
    p.nombre,
    LINEA_PRODUCTIVA_LABEL[p.lineaProductiva] ?? p.lineaProductiva,
    p.raza ?? 'Todas',
    p.vigenteDesde ? fechaCorta(p.vigenteDesde) : '',
    p.activa ? 'Sí' : 'No',
    p.cantidadItems,
    p.notas ?? '',
  ]);

  const filasItems = detalles.flatMap((d) =>
    d.items.map((i) => [
      d.nombre,
      LINEA_PRODUCTIVA_LABEL[d.lineaProductiva] ?? d.lineaProductiva,
      d.raza ?? 'Todas',
      i.orden,
      i.itemInventarioNombre,
      describirObjetivo(i.unidadObjetivo, i.valorObjetivo),
      i.rangoDiasAntes,
      i.rangoDiasDespues,
      i.notas ?? '',
    ])
  );

  exportarAoaMultiHojaExcel(
    [
      { sheetName: 'Plantillas', aoa: [HEADERS_PLANTILLAS, ...filasPlantillas] },
      { sheetName: 'Vacunas del plan', aoa: [HEADERS_ITEMS, ...filasItems] },
    ],
    { filenameBase: 'Plan_vacunacion_plantillas' }
  );
}

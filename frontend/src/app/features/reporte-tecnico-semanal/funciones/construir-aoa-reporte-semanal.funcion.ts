// Función PURA: arma las hojas AOA del export a Excel del Reporte Técnico
// Semanal (una hoja por galpón + consolidado), a partir de la especificación
// única de columnas (columnas-reporte-semanal.funcion.ts). Sin this/DI/toast.
import { ExcelCell, HojaAoaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import {
  ReporteSemanalAlimentoFase,
  ReporteSemanalAlimentoPorFase,
  ReporteSemanalTabHeader,
  ReporteTecnicoSemanalLevanteResponse,
  ReporteTecnicoSemanalProduccionResponse
} from '../models/reporte-tecnico-semanal.model';
import {
  agruparColumnas,
  ColumnaReporte,
  COLUMNAS_ALIMENTO_FASE,
  COLUMNAS_CLASIFICACION_HUEVO,
  COLUMNAS_LEVANTE,
  COLUMNAS_PRODUCCION
} from './columnas-reporte-semanal.funcion';

const fechaYmd = (iso: string | null): string => (iso ? String(iso).slice(0, 10) : '');

function redondear(v: number, dec: number): number {
  const factor = Math.pow(10, dec);
  return Math.round(v * factor) / factor;
}

function filaCabeceraInfo(header: ReporteSemanalTabHeader, titulo: string): ExcelCell[][] {
  return [
    [titulo],
    [
      'Lote:', header.loteNombre,
      'Granja:', header.granjaNombre ?? '',
      'Municipio:', header.municipio ?? '',
      'Núcleo:', header.nucleoNombre ?? header.nucleoId ?? '',
      'Galpón:', header.galponNombre ?? header.galponId ?? ''
    ],
    [
      'Raza:', header.raza ?? '',
      'Año guía:', header.anioGuia ?? '',
      'Fecha encaset:', fechaYmd(header.fechaEncaset),
      'Aves H:', header.baseHembras,
      'Aves M:', header.baseMachos,
      'Técnico:', header.tecnico ?? ''
    ],
    []
  ];
}

function construirHoja<T>(
  sheetName: string,
  titulo: string,
  header: ReporteSemanalTabHeader,
  semanas: T[],
  columnas: ColumnaReporte<T>[]
): HojaAoaExcel {
  const grupos = agruparColumnas(columnas);
  const filaGrupos: ExcelCell[] = [];
  for (const g of grupos) {
    filaGrupos.push(g.titulo);
    for (let i = 1; i < g.span; i++) filaGrupos.push('');
  }
  const filaTitulos: ExcelCell[] = columnas.map(c => c.titulo);

  const cuerpo: ExcelCell[][] = semanas.map(s =>
    columnas.map(c => {
      const v = c.valor(s);
      if (v == null) return '';
      return typeof v === 'number' ? redondear(v, c.dec) : v;
    })
  );

  return {
    sheetName,
    aoa: [...filaCabeceraInfo(header, titulo), filaGrupos, filaTitulos, ...cuerpo],
    colWidths: columnas.map(c => Math.max(9, c.titulo.length + 2))
  };
}

/** Nombre de hoja Excel: máx 31 chars, sin caracteres inválidos, único. */
function nombreHoja(base: string, usados: Set<string>): string {
  let limpio = base.replace(/[\\/:*?\[\]]/g, ' ').trim().slice(0, 31) || 'Hoja';
  let candidato = limpio;
  let n = 2;
  while (usados.has(candidato)) {
    const sufijo = ` (${n++})`;
    candidato = limpio.slice(0, 31 - sufijo.length) + sufijo;
  }
  usados.add(candidato);
  return candidato;
}

/**
 * Hoja «ALIMLev»: las cuatro tablas de energía/proteína por fase, una debajo de
 * otra en la MISMA hoja (igual que el archivo original, que no las reparte).
 */
function construirHojaAlimento(
  sheetName: string,
  titulo: string,
  header: ReporteSemanalTabHeader,
  bloque: ReporteSemanalAlimentoPorFase | undefined
): HojaAoaExcel | null {
  if (!bloque) return null;

  const tablas: { nombre: string; filas: ReporteSemanalAlimentoFase[] }[] = [
    { nombre: 'ENERGÍA POR FASE ALIMENTO - HEMBRAS (kcal/ave)', filas: bloque.energiaHembras },
    { nombre: 'ENERGÍA POR FASE ALIMENTO - MACHOS (kcal/ave)', filas: bloque.energiaMachos },
    { nombre: 'PROTEÍNA POR FASE ALIMENTO - HEMBRAS (g/ave)', filas: bloque.proteinaHembras },
    { nombre: 'PROTEÍNA POR FASE ALIMENTO - MACHOS (g/ave)', filas: bloque.proteinaMachos }
  ].filter(t => t.filas && t.filas.length > 0);

  if (tablas.length === 0) return null;

  const aoa: ExcelCell[][] = [...filaCabeceraInfo(header, titulo)];
  for (const tabla of tablas) {
    aoa.push([tabla.nombre]);
    aoa.push(COLUMNAS_ALIMENTO_FASE.map(c => c.titulo));
    for (const f of tabla.filas) {
      aoa.push(COLUMNAS_ALIMENTO_FASE.map(c => {
        const v = c.valor(f);
        if (v == null) return '';
        return typeof v === 'number' ? redondear(v, c.dec) : v;
      }));
    }
    aoa.push([]);
  }
  aoa.push(['La fase de cada semana la fija la guía genética. En machos el alimento real no se ' +
            'captura: se usa la energía/proteína nominal de la fase, así que su desviación ' +
            'refleja consumo, no formulación.']);

  return {
    sheetName,
    aoa,
    colWidths: COLUMNAS_ALIMENTO_FASE.map(c => Math.max(12, c.titulo.length + 2))
  };
}

export function construirHojasLevante(
  respuesta: ReporteTecnicoSemanalLevanteResponse
): HojaAoaExcel[] {
  const hojas: HojaAoaExcel[] = [];
  const usados = new Set<string>();
  const titulo = `Resumen Semanal de Levante — ${respuesta.loteBaseNombre}`;

  if (respuesta.consolidado) {
    hojas.push(construirHoja(
      nombreHoja(`Gral ${respuesta.loteBaseNombre}`, usados),
      titulo, respuesta.consolidado.header, respuesta.consolidado.semanas, COLUMNAS_LEVANTE));
  }
  for (const tab of respuesta.tabs) {
    hojas.push(construirHoja(
      nombreHoja(tab.header.loteNombre, usados),
      titulo, tab.header, tab.semanas, COLUMNAS_LEVANTE));
  }

  // Hoja «ALIMLev»: una por tab, después de las de semanas, igual que el archivo.
  const alimentoConsolidado = respuesta.consolidado
    ? construirHojaAlimento(nombreHoja(`ALIMLev Gral`, usados), titulo,
        respuesta.consolidado.header, respuesta.consolidado.alimentoPorFase)
    : null;
  if (alimentoConsolidado) hojas.push(alimentoConsolidado);
  for (const tab of respuesta.tabs) {
    const hoja = construirHojaAlimento(
      nombreHoja(`ALIMLev ${tab.header.loteNombre}`, usados), titulo, tab.header, tab.alimentoPorFase);
    if (hoja) hojas.push(hoja);
  }
  return hojas;
}

export function construirHojasProduccion(
  respuesta: ReporteTecnicoSemanalProduccionResponse
): HojaAoaExcel[] {
  const hojas: HojaAoaExcel[] = [];
  const usados = new Set<string>();
  const titulo = `Resumen Semanal de Producción — ${respuesta.loteBaseNombre}`;

  if (respuesta.consolidado) {
    hojas.push(construirHoja(
      nombreHoja(`Gral ${respuesta.loteBaseNombre}`, usados),
      titulo, respuesta.consolidado.header, respuesta.consolidado.semanas, COLUMNAS_PRODUCCION));
  }
  for (const tab of respuesta.tabs) {
    hojas.push(construirHoja(
      nombreHoja(tab.header.loteNombre, usados),
      titulo, tab.header, tab.semanas, COLUMNAS_PRODUCCION));
  }

  // Hoja «CLAS Huevo»: los mismos tabs con las columnas de clasificación.
  const tituloClas = `Clasificación de huevo — ${respuesta.loteBaseNombre}`;
  if (respuesta.consolidado) {
    hojas.push(construirHoja(
      nombreHoja(`CLAS Gral`, usados), tituloClas,
      respuesta.consolidado.header, respuesta.consolidado.semanas, COLUMNAS_CLASIFICACION_HUEVO));
  }
  for (const tab of respuesta.tabs) {
    hojas.push(construirHoja(
      nombreHoja(`CLAS ${tab.header.loteNombre}`, usados), tituloClas,
      tab.header, tab.semanas, COLUMNAS_CLASIFICACION_HUEVO));
  }
  return hojas;
}

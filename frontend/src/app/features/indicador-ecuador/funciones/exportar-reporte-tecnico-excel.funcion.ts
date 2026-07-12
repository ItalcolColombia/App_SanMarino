/**
 * Armado de las hojas (AoA) del Reporte Técnico Pollo Engorde para Excel — función PURA.
 *
 * Construye la matriz consolidada + una hoja por lote y devuelve el arreglo de hojas. La DESCARGA
 * (`exportarAoaMultiHojaExcel`) queda en el componente. Réplica exacta del `exportarExcel` original:
 * mismos labels, orden de filas, decimales y variantes "o vacío" para la merma no registrada.
 */
import { HojaAoaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import {
  IndicadorEcuadorDto,
  LiquidacionPolloEngordeReporteDto
} from '../services/indicador-ecuador.service';
import { formatearFechaLote, formatearNumero, formatearPorcentaje, sanitizarNombreHoja } from './formato.funcion';
import { ajusteAvesDe, porcentajeAjusteAvesDe } from './liquidacion-totales.funcion';
import { etiquetaColumnaLiquidacion } from './etiquetas.funcion';

/** Construye las hojas del Reporte Técnico (Consolidado + una por lote). `null` si no hay items. */
export function construirHojasReporteTecnico(
  datos: LiquidacionPolloEngordeReporteDto | null,
  tot: IndicadorEcuadorDto | null
): HojaAoaExcel[] | null {
  if (!datos?.items?.length) return null;

  const hojas: HojaAoaExcel[] = [];

  const fn = (v: number | null | undefined, d: number) => formatearNumero(v, d);
  const fp = (v: number | null | undefined) => formatearPorcentaje(v);
  const n0 = (v: number | null | undefined) => (v ?? 0).toLocaleString('es-EC');
  // R1: variantes "o vacío" — merma no registrada ⇒ celda vacía en el Excel.
  const nV = (v: number | null | undefined) => (v == null ? '' : v.toLocaleString('es-EC'));
  const fnV = (v: number | null | undefined, d: number) => (v == null ? '' : fn(v, d));
  const fpV = (v: number | null | undefined) => (v == null ? '' : fp(v));
  const fecha = (v: string | null | undefined) => (v ? formatearFechaLote(v) : '—');

  // ── Hoja Consolidado ──────────────────────────────────────────
  const encCols = datos.items.map(it => etiquetaColumnaLiquidacion(it));
  const fila = (label: string, getter: (r: IndicadorEcuadorDto) => string, totVal: string): string[] =>
    [label, ...datos.items.map(it => getter(it.indicador)), totVal];

  const first = datos.items[0]?.indicador;
  const pesoAj = fn(first?.pesoAjusteVariable, 1);
  const divAj  = fn(first?.divisorAjusteVariable, 1);

  const rowsConsolidado: string[][] = [
    ['ECUADOR ITALCOL — Liquidación Técnica Pollo Engorde'],
    ['Indicador', ...encCols, 'TOTAL'],
    fila('Granja', r => r.granjaNombre, tot?.granjaNombre ?? ''),
    fila('Fecha alistamiento',          r => fecha(r.fechaAlistamiento),      fecha(tot?.fechaAlistamiento)),
    fila('Fecha encasetamiento',        r => fecha(r.fechaInicioLote),        fecha(tot?.fechaInicioLote)),
    fila('Fecha liquidación',           r => fecha(r.fechaLiquidacion),       fecha(tot?.fechaLiquidacion)),
    fila('Aves encasetadas',           r => n0(r.avesEncasetadas),           n0(tot?.avesEncasetadas)),
    fila('Aves vendidas / despacho',   r => n0(r.avesSacrificadas),          n0(tot?.avesSacrificadas)),
    fila('Aves agregadas de más (sobrante)', r => n0(r.avesSobrante ?? 0),    n0(tot?.avesSobrante ?? 0)),
    fila('Mortalidad (unidades)',       r => n0(r.mortalidad),                n0(tot?.mortalidad)),
    fila('Mortalidad (%)',              r => fp(r.mortalidadPorcentaje),      fp(tot?.mortalidadPorcentaje)),
    fila('Merma (unidades)',            r => nV(r.mermaUnidades),             nV(tot?.mermaUnidades)),
    fila('Merma (%)',                   r => fpV(r.mermaPorcentaje),          fpV(tot?.mermaPorcentaje)),
    fila('Ajuste en aves',              r => n0(ajusteAvesDe(r)),             tot ? n0(ajusteAvesDe(tot)) : ''),
    fila('Porcentaje de ajuste (%)',    r => fp(porcentajeAjusteAvesDe(r)),   tot ? fp(porcentajeAjusteAvesDe(tot)) : ''),
    fila('Supervivencia (%)',           r => fp(r.supervivenciaPorcentaje),   fp(tot?.supervivenciaPorcentaje)),
    fila('Consumo total alimento (kg)', r => fn(r.consumoTotalAlimentoKg, 2), fn(tot?.consumoTotalAlimentoKg, 2)),
    fila('Consumo ave (g)',             r => fn(r.consumoAveGramos, 2),       fn(tot?.consumoAveGramos, 2)),
    fila('Producción kilo en pie (kg)', r => fn(r.produccionKiloEnPie ?? r.kgCarnePollos, 2), fn(tot?.produccionKiloEnPie ?? tot?.kgCarnePollos, 2)),
    fila('Merma (kilos)',              r => fnV(r.mermaKilos, 2),            fnV(tot?.mermaKilos, 2)),
    fila('Total kg despachados a cliente', r => fnV(r.totalKilosDespachadosCliente, 2), fnV(tot?.totalKilosDespachadosCliente, 2)),
    fila('Kg carne pollo',             r => fn(r.kgCarnePollos, 2),          fn(tot?.kgCarnePollos, 2)),
    fila('Peso promedio (kg)',          r => fn(r.pesoPromedioKilos, 3),      fn(tot?.pesoPromedioKilos, 3)),
    fila('Conversión',                 r => fn(r.conversion, 3),             fn(tot?.conversion, 3)),
    fila(`Conv. ajustada (${pesoAj}/${divAj})`, r => fn(r.conversionAjustada2700, 3), fn(tot?.conversionAjustada2700, 3)),
    fila('Edad (días, ciclo)',         r => fn(r.edadPromedio, 1),           fn(tot?.edadPromedio, 1)),
    fila('Días de engorde',            r => n0(r.diasEngorde ?? 0),          n0(Math.round(tot?.diasEngorde ?? 0))),
    fila('Metros cuadrados',           r => fn(r.metrosCuadrados, 2),        fn(tot?.metrosCuadrados, 2)),
    fila('Aves / m²',                  r => fn(r.avesPorMetroCuadrado, 2),   fn(tot?.avesPorMetroCuadrado, 2)),
    fila('Kg / m²',                    r => fn(r.kgPorMetroCuadrado, 2),     fn(tot?.kgPorMetroCuadrado, 2)),
    fila('Eficiencia americana',        r => fn(r.eficienciaAmericana, 2),    fn(tot?.eficienciaAmericana, 2)),
    fila('Eficiencia europea',          r => fn(r.eficienciaEuropea, 2),      fn(tot?.eficienciaEuropea, 2)),
    fila('Í. Productividad',           r => fn(r.indiceProductividad, 2),    fn(tot?.indiceProductividad, 2)),
    fila('Ganancia / día (g)',          r => fn(r.gananciaDia, 2),            fn(tot?.gananciaDia, 2)),
    fila('Conv. tabla según peso (guía)', _ => '—',                          '—'),
  ];

  hojas.push({ sheetName: 'Consolidado', aoa: rowsConsolidado });

  // ── Hojas individuales por lote ───────────────────────────────
  for (const it of datos.items) {
    const ind = it.indicador;
    const rowsLote: string[][] = [
      ['Indicador', 'Valor'],
      ['Granja',                       ind.granjaNombre],
      ['Galpón',                       ind.galponNombre || ind.galponId || '—'],
      ['Fecha alistamiento',           ind.fechaAlistamiento ? formatearFechaLote(ind.fechaAlistamiento) : '—'],
      ['Fecha encasetamiento',         ind.fechaInicioLote ? formatearFechaLote(ind.fechaInicioLote) : '—'],
      ['Fecha liquidación',            ind.fechaLiquidacion ? formatearFechaLote(ind.fechaLiquidacion) : '—'],
      ['Fecha cierre',                 ind.fechaCierreLote ? formatearFechaLote(ind.fechaCierreLote) : '—'],
      ['Días de engorde',              n0(ind.diasEngorde ?? 0)],
      ['Aves encasetadas',             n0(ind.avesEncasetadas)],
      ['Aves vendidas / despacho',     n0(ind.avesSacrificadas)],
      ['Aves agregadas de más (sobrante)', n0(ind.avesSobrante ?? 0)],
      ['Mortalidad (unidades)',         n0(ind.mortalidad)],
      ['Mortalidad (%)',               fp(ind.mortalidadPorcentaje)],
      ['Merma (unidades)',             nV(ind.mermaUnidades)],
      ['Merma (%)',                    fpV(ind.mermaPorcentaje)],
      ['Ajuste en aves',               n0(ajusteAvesDe(ind))],
      ['Porcentaje de ajuste (%)',     fp(porcentajeAjusteAvesDe(ind))],
      ['Supervivencia (%)',            fp(ind.supervivenciaPorcentaje)],
      ['Consumo total alimento (kg)', fn(ind.consumoTotalAlimentoKg, 2)],
      ['Consumo ave (g)',              fn(ind.consumoAveGramos, 2)],
      ['Producción kilo en pie (kg)', fn(ind.produccionKiloEnPie ?? ind.kgCarnePollos, 2)],
      ['Merma (kilos)',               fnV(ind.mermaKilos, 2)],
      ['Total kilos despachados a cliente (kg)', fnV(ind.totalKilosDespachadosCliente, 2)],
      ['Kg carne pollo',              fn(ind.kgCarnePollos, 2)],
      ['Peso promedio (kg)',           fn(ind.pesoPromedioKilos, 3)],
      ['Conversión',                  fn(ind.conversion, 3)],
      [`Conv. ajustada (${fn(ind.pesoAjusteVariable, 1)}/${fn(ind.divisorAjusteVariable, 1)})`, fn(ind.conversionAjustada2700, 3)],
      ['Edad (días, ciclo)',          fn(ind.edadPromedio, 1)],
      ['Metros cuadrados',            fn(ind.metrosCuadrados, 2)],
      ['Aves / m²',                   fn(ind.avesPorMetroCuadrado, 2)],
      ['Kg / m²',                     fn(ind.kgPorMetroCuadrado, 2)],
      ['Eficiencia americana',         fn(ind.eficienciaAmericana, 2)],
      ['Eficiencia europea',           fn(ind.eficienciaEuropea, 2)],
      ['Í. Productividad',            fn(ind.indiceProductividad, 2)],
      ['Ganancia / día (g)',           fn(ind.gananciaDia, 2)],
      ['Conv. tabla según peso (guía)', '—'],
    ];

    const sheetName = sanitizarNombreHoja(
      `${ind.galponNombre || ind.galponId || 'Gal'} ${it.loteNombre || it.loteAveEngordeId}`
    );
    hojas.push({ sheetName, aoa: rowsLote });
  }

  return hojas;
}

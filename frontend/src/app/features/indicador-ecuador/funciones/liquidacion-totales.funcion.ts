/**
 * Cálculo de la fila TOTAL de la liquidación Pollo Engorde y ajustes por lote — funciones PURAS.
 *
 * Réplica EXACTA de la aritmética previa del componente (`liquidacionTotales`, `ajusteDe`,
 * `porcentajeAjusteDe`), incluyendo orden de operaciones, redondeos y manejo de null/merma.
 * Este cálculo espeja el backend `IndicadorEcuadorService` — NO alterar la matemática.
 */
import {
  IndicadorEcuadorDto,
  LiquidacionPolloEngordeItemDto
} from '../services/indicador-ecuador.service';

/** Ajuste de aves = encasetadas − vendidas − (mortalidad + selección). Aplica a TODOS los lotes. */
export function ajusteAvesDe(ind: IndicadorEcuadorDto): number {
  return ind.avesEncasetadas - ind.avesSacrificadas - ind.mortalidad;
}

/** % de ajuste = (ajuste / encasetadas) × 100. Se calcula para todos los lotes. */
export function porcentajeAjusteAvesDe(ind: IndicadorEcuadorDto): number {
  return ind.avesEncasetadas > 0 ? (ajusteAvesDe(ind) / ind.avesEncasetadas) * 100 : 0;
}

/** Fila TOTAL: agrega cantidades y recalcula ratios como en consolidado. `null` si no hay items. */
export function calcularLiquidacionTotales(
  items: LiquidacionPolloEngordeItemDto[] | null | undefined
): IndicadorEcuadorDto | null {
  if (!items?.length) return null;
  const R = items.map(i => i.indicador);
  let enc = 0;
  let sac = 0;
  let mort = 0;
  let cons = 0;
  let kg = 0;
  let m2 = 0;
  let mermaUni = 0;
  let mermaKg = 0;
  let prodKg = 0;
  let sobrante = 0;
  let diasEng = 0;
  let lotesConDias = 0;
  // R1: la merma se digita UNA vez por corrida (queda en un solo lote). El total a cliente
  // se calcula a nivel corrida (prodKg − mermaKg), NO por lote, para no excluir lotes sin merma.
  // Si NINGÚN lote tiene merma, el total a cliente va null ⇒ la matriz muestra el campo vacío.
  let lotesConMerma = 0;
  for (const r of R) {
    enc += r.avesEncasetadas;
    sac += r.avesSacrificadas;
    mort += r.mortalidad;
    cons += r.consumoTotalAlimentoKg;
    kg += r.kgCarnePollos;
    m2 += r.metrosCuadrados;
    prodKg += r.produccionKiloEnPie ?? r.kgCarnePollos;
    sobrante += r.avesSobrante ?? 0;
    if ((r.diasEngorde ?? 0) > 0) {
      diasEng += r.diasEngorde!;
      lotesConDias++;
    }
    if (r.mermaUnidades != null || r.mermaKilos != null) {
      lotesConMerma++;
      mermaUni += r.mermaUnidades ?? 0;
      mermaKg += r.mermaKilos ?? 0;
    }
  }
  const hayMerma = lotesConMerma > 0;
  const first = R[0];
  // Fechas del TOTAL = las del "primer galpón" (el que encasetó más temprano en la
  // corrida), no un promedio ni vacío — así lo pide Costos para reconciliar contra su Excel.
  const primerGalpon = R.reduce((min, r) => {
    if (!r.fechaInicioLote) return min;
    if (!min || !min.fechaInicioLote) return r;
    return new Date(r.fechaInicioLote).getTime() < new Date(min.fechaInicioLote).getTime() ? r : min;
  }, null as IndicadorEcuadorDto | null) ?? first;
  const pesoAj = first.pesoAjusteVariable;
  const divAj = first.divisorAjusteVariable;
  const mortPct = enc > 0 ? (mort / enc) * 100 : 0;
  const supPct = enc > 0 ? ((enc - mort) / enc) * 100 : 0;
  const consAveG = enc > 0 ? (cons * 1000) / enc : 0;
  const pesoProm = kg > 0 ? R.reduce((s, r) => s + r.pesoPromedioKilos * r.kgCarnePollos, 0) / kg : 0;
  const conv = kg > 0 ? cons / kg : 0;
  const convAdj = conv > 0 ? conv + (pesoAj - pesoProm) / divAj : 0;
  const edad = enc > 0 ? R.reduce((s, r) => s + r.edadPromedio * r.avesEncasetadas, 0) / enc : 0;
  const avM2 = m2 > 0 ? enc / m2 : 0;
  const kgM2 = m2 > 0 ? kg / m2 : 0;
  const w = (fn: (x: IndicadorEcuadorDto) => number) =>
    enc > 0 ? R.reduce((s, r) => s + fn(r) * r.avesEncasetadas, 0) / enc : 0;
  return {
    granjaId: first.granjaId,
    granjaNombre: first.granjaNombre,
    loteId: null,
    loteNombre: 'TOTAL',
    galponId: null,
    galponNombre: null,
    avesEncasetadas: enc,
    avesSacrificadas: sac,
    mortalidad: mort,
    mortalidadPorcentaje: mortPct,
    supervivenciaPorcentaje: supPct,
    consumoTotalAlimentoKg: cons,
    consumoAveGramos: consAveG,
    kgCarnePollos: kg,
    pesoPromedioKilos: pesoProm,
    conversion: conv,
    conversionAjustada2700: convAdj,
    pesoAjusteVariable: pesoAj,
    divisorAjusteVariable: divAj,
    edadPromedio: edad,
    metrosCuadrados: m2,
    avesPorMetroCuadrado: avM2,
    kgPorMetroCuadrado: kgM2,
    eficienciaAmericana: w(r => r.eficienciaAmericana),
    eficienciaEuropea: w(r => r.eficienciaEuropea),
    indiceProductividad: w(r => r.indiceProductividad),
    gananciaDia: w(r => r.gananciaDia),
    mermaUnidades: hayMerma ? mermaUni : null,
    mermaKilos: hayMerma ? mermaKg : null,
    mermaPorcentaje: hayMerma && sac > 0 ? (mermaUni / sac) * 100 : hayMerma ? 0 : null,
    // Ajuste y % de ajuste: SIEMPRE (no dependen de merma) = encasetadas − vendidas − (mort + sel).
    ajusteAves: enc - sac - mort,
    porcentajeAjuste: enc > 0 ? ((enc - sac - mort) / enc) * 100 : 0,
    produccionKiloEnPie: prodKg,
    // Total a cliente de la CORRIDA: producción total (todos los lotes) − merma única.
    // R1b: SIEMPRE se muestra. Sin merma en la corrida => = producción total (kg carne pollo);
    //      con merma => producción total − merma. Coincide con la suma de los totales por lote.
    totalKilosDespachadosCliente: hayMerma ? (prodKg - mermaKg) : prodKg,
    diasEngorde: lotesConDias > 0 ? diasEng / lotesConDias : 0,
    avesSobrante: sobrante,
    fechaAlistamiento: primerGalpon.fechaAlistamiento ?? null,
    fechaInicioLote: primerGalpon.fechaInicioLote ?? null,
    fechaLiquidacion: primerGalpon.fechaLiquidacion ?? null,
    fechaCierreLote: null,
    loteCerrado: true
  };
}

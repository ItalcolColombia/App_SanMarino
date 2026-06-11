// Función PURA: arma los bloques «primera semana» (H y M por lote reproductora)
// a partir de los lotes reproductora y sus seguimientos diarios (máx. 7).
// Sin `this`, sin DI, sin servicios — recibe datos, devuelve resultado.
//
// Reglas (Excel «LOTES DE POLLITOS PRIMERA SEMANA», validadas con negocio):
//   · edad = fechaRegistro − fechaEncasetamiento (días, fechas UTC); solo edades 1..7.
//   · saldo día d = avesInicio − Σ(mort + sel + error) de edades ≤ d.
//   · qqReal = consumoKg ÷ 45.36 · grs/ave = consumoKg × 1000 ÷ saldo inicio del día.
//   · ganancia = peso_d − peso_(d−1); día 1 = peso − peso de llegada.
//   · Conv. = grs/ave ÷ ganancia; DÍA 1 = grs/ave ÷ peso de llegada (criterio confirmado).
//   · %Norm / %Sel = muertos|sel ÷ saldo inicio del día (×100).
//   · Fila Total = sumas (los % se suman, semántica del Excel).
//   · Guía genética (consumoTablaGr / qqTabla): queda mapeada en el modelo pero
//     se entrega null en fase 1 (columnas ocultas; pendiente conectar la guía).

import {
  BloquePrimeraSemana,
  FilaDiaBloque,
  InsumoBloqueReproductora,
  LoteReproductoraPrimeraSemanaLike,
  REPRODUCTORA_QQ_TO_KG,
  SeguimientoPrimeraSemanaLike,
  TotalesBloque
} from '../models/reproductora-primera-semana.model';

const MAX_DIAS = 7;

function round2(n: number): number {
  return Math.round(n * 100) / 100;
}

/** Fecha ISO → días UTC (ignora hora y zona horaria: usa solo YYYY-MM-DD). */
function diasUtc(iso: string): number | null {
  const ymd = (iso ?? '').slice(0, 10);
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(ymd);
  if (!m) return null;
  return Math.floor(Date.UTC(+m[1], +m[2] - 1, +m[3]) / 86400000);
}

/** Edad en días de vida (día siguiente al encaset = 1). */
function edadDia(fechaRegistro: string, fechaEncaset: string | null): number | null {
  const reg = diasUtc(fechaRegistro);
  const enc = fechaEncaset ? diasUtc(fechaEncaset) : null;
  if (reg == null || enc == null) return null;
  return reg - enc;
}

/** 'H-34 / M-34' → ['H-34', 'M-34']; sin código → fallback con el nombre del lote. */
function titulosBloques(lote: LoteReproductoraPrimeraSemanaLike): { h: string; m: string } {
  const partes = (lote.codigoReproductora ?? '')
    .split('/')
    .map(s => s.trim())
    .filter(Boolean);
  if (partes.length === 2) return { h: partes[0], m: partes[1] };
  return { h: `H — ${lote.nombreLote}`, m: `M — ${lote.nombreLote}` };
}

interface DatosSexoDia {
  consumoKg: number | null;
  pesoG: number | null;
  muertos: number;
  sel: number;
  error: number;
  conRegistro: boolean;
}

function datosDelSexo(s: SeguimientoPrimeraSemanaLike | undefined, sexo: 'H' | 'M'): DatosSexoDia {
  if (!s) return { consumoKg: null, pesoG: null, muertos: 0, sel: 0, error: 0, conRegistro: false };
  return sexo === 'H'
    ? {
        consumoKg: s.consumoKgHembras ?? null,
        pesoG: s.pesoPromH ?? null,
        muertos: s.mortalidadHembras ?? 0,
        sel: s.selH ?? 0,
        error: s.errorSexajeHembras ?? 0,
        conRegistro: true
      }
    : {
        consumoKg: s.consumoKgMachos ?? null,
        pesoG: s.pesoPromM ?? null,
        muertos: s.mortalidadMachos ?? 0,
        sel: s.selM ?? 0,
        error: s.errorSexajeMachos ?? 0,
        conRegistro: true
      };
}

function construirBloqueSexo(
  lote: LoteReproductoraPrimeraSemanaLike,
  porEdad: Map<number, SeguimientoPrimeraSemanaLike>,
  sexo: 'H' | 'M',
  titulo: string
): BloquePrimeraSemana {
  const pollitos = sexo === 'H'
    ? (lote.avesInicioHembras ?? lote.h ?? 0)
    : (lote.avesInicioMachos ?? lote.m ?? 0);
  const pesoLlegada = sexo === 'H' ? (lote.pesoInicialH ?? null) : (lote.pesoInicialM ?? null);

  const filas: FilaDiaBloque[] = [];
  const totales: TotalesBloque = { qqReal: 0, consumoKg: 0, grsAve: 0, muertosNorm: 0, muertosSel: 0, pctNorm: 0, pctSel: 0 };

  let saldo = pollitos;
  let pesoAnterior = pesoLlegada;
  let peso7Dias: number | null = null;

  for (let dia = 1; dia <= MAX_DIAS; dia++) {
    const d = datosDelSexo(porEdad.get(dia), sexo);
    const saldoInicio = saldo;
    saldo = saldoInicio - d.muertos - d.sel - d.error;

    const qqReal = d.consumoKg != null ? round2(d.consumoKg / REPRODUCTORA_QQ_TO_KG) : null;
    const grsAve = d.consumoKg != null && saldoInicio > 0 ? round2((d.consumoKg * 1000) / saldoInicio) : null;

    let ganancia: number | null = null;
    if (d.pesoG != null && pesoAnterior != null) ganancia = round2(d.pesoG - pesoAnterior);

    // Día 1 divide por el peso de llegada; los demás días por la ganancia.
    let conv: number | null = null;
    if (grsAve != null) {
      const divisor = dia === 1 ? pesoLlegada : ganancia;
      if (divisor != null && divisor !== 0) conv = round2(grsAve / divisor);
    }

    const pctNorm = d.conRegistro && saldoInicio > 0 ? round2((d.muertos / saldoInicio) * 10000) / 100 : null;
    const pctSel = d.conRegistro && saldoInicio > 0 ? round2((d.sel / saldoInicio) * 10000) / 100 : null;

    if (dia === MAX_DIAS && d.pesoG != null) peso7Dias = d.pesoG;
    if (d.pesoG != null) pesoAnterior = d.pesoG;

    filas.push({
      dia,
      conRegistro: d.conRegistro,
      consumoTablaGr: null, // fase 2: guía genética
      qqTabla: null,        // fase 2: guía genética
      qqReal,
      consumoKg: d.consumoKg != null ? round2(d.consumoKg) : null,
      grsAve,
      pesoG: d.pesoG,
      gananciaG: ganancia,
      conv,
      muertosNorm: d.muertos,
      muertosSel: d.sel,
      saldo,
      pctNorm,
      pctSel
    });

    totales.qqReal += qqReal ?? 0;
    totales.consumoKg += d.consumoKg ?? 0;
    totales.grsAve += grsAve ?? 0;
    totales.muertosNorm += d.muertos;
    totales.muertosSel += d.sel;
    totales.pctNorm += pctNorm ?? 0;
    totales.pctSel += pctSel ?? 0;
  }

  totales.qqReal = round2(totales.qqReal);
  totales.consumoKg = round2(totales.consumoKg);
  totales.grsAve = round2(totales.grsAve);
  totales.pctNorm = round2(totales.pctNorm * 100) / 100;
  totales.pctSel = round2(totales.pctSel * 100) / 100;

  return {
    loteReproductoraId: lote.id,
    nombreLote: lote.nombreLote,
    titulo,
    sexo,
    fechaEncaset: lote.fechaEncasetamiento,
    pollitos,
    pesoLlegadaG: pesoLlegada,
    filas,
    totales,
    saldoFinal: saldo,
    peso7DiasG: peso7Dias
  };
}

/**
 * Construye los bloques primera semana: por cada lote reproductora, un bloque
 * Hembras y un bloque Machos (orden: H, M — igual que el Excel).
 */
export function construirBloquesReproductora(
  insumos: ReadonlyArray<InsumoBloqueReproductora>
): BloquePrimeraSemana[] {
  const bloques: BloquePrimeraSemana[] = [];

  for (const { lote, seguimientos } of insumos) {
    const porEdad = new Map<number, SeguimientoPrimeraSemanaLike>();
    for (const s of seguimientos ?? []) {
      const edad = edadDia(s.fechaRegistro, lote.fechaEncasetamiento);
      if (edad != null && edad >= 1 && edad <= MAX_DIAS) porEdad.set(edad, s);
    }
    const titulos = titulosBloques(lote);
    bloques.push(construirBloqueSexo(lote, porEdad, 'H', titulos.h));
    bloques.push(construirBloqueSexo(lote, porEdad, 'M', titulos.m));
  }

  return bloques;
}

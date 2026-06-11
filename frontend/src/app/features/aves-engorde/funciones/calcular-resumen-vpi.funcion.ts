// Función PURA: resumen superior del tab «R. Reproductora» (cabecera del Excel).
// Fórmulas confirmadas con negocio (2026-06-10):
//   · Cantidad × Peso      = cantidad inicial × peso de llegada (g).
//   · Peso 7 días          = peso prom registrado en la edad 7 (g); sin pesaje → 0/—.
//   · Cantidad × 7 días    = cantidad inicial × peso 7 días.
//   · VPI (fila)           = peso 7 días ÷ peso de llegada.
//   · VPI (total)          = Σ(cantidad × 7 días) ÷ Σ(cantidad × peso llegada).

import {
  BloquePrimeraSemana,
  ResumenReproductora,
  ResumenReproductoraFila
} from '../models/reproductora-primera-semana.model';

function round2(n: number): number {
  return Math.round(n * 100) / 100;
}

export function calcularResumenVpi(bloques: ReadonlyArray<BloquePrimeraSemana>): ResumenReproductora {
  const filas: ResumenReproductoraFila[] = [];
  let totalCantidad = 0;
  let totalCantidadXPeso = 0;
  let totalCantidadX7Dias = 0;

  let grupoAnterior: number | null = null;
  for (const b of bloques) {
    const cantidadXPeso = b.pesoLlegadaG != null ? round2(b.pollitos * b.pesoLlegadaG) : null;
    const cantidadX7Dias = b.peso7DiasG != null ? round2(b.pollitos * b.peso7DiasG) : null;
    const vpi = b.peso7DiasG != null && b.pesoLlegadaG ? round2(b.peso7DiasG / b.pesoLlegadaG) : null;

    filas.push({
      grupo: b.nombreLote,
      esPrimeraDelGrupo: b.loteReproductoraId !== grupoAnterior,
      lote: b.titulo,
      cantidad: b.pollitos,
      pesoLlegadaG: b.pesoLlegadaG,
      cantidadXPeso,
      peso7DiasG: b.peso7DiasG,
      cantidadX7Dias,
      vpi
    });

    totalCantidad += b.pollitos;
    totalCantidadXPeso += cantidadXPeso ?? 0;
    totalCantidadX7Dias += cantidadX7Dias ?? 0;
    grupoAnterior = b.loteReproductoraId;
  }

  return {
    filas,
    totalCantidad,
    totalCantidadXPeso: round2(totalCantidadXPeso),
    totalCantidadX7Dias: round2(totalCantidadX7Dias),
    vpiTotal: totalCantidadXPeso > 0 ? round2(totalCantidadX7Dias / totalCantidadXPeso) : null
  };
}

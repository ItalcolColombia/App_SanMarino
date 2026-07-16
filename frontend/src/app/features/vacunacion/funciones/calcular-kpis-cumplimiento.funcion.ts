/**
 * KPIs globales del reporte de cumplimiento: agregados PONDERADOS sobre los lotes filtrados
 * (se suma por conteos, no se promedian porcentajes por lote). Función pura.
 */
import { VacunacionCumplimientoLoteDto } from '../models/vacunacion.model';

export interface KpisCumplimiento {
  lotes: number;
  totalProgramadas: number;
  totalATiempo: number;
  totalTardias: number;
  totalIncumplidas: number;
  totalNoAplicadas: number;
  totalPendientes: number;
  porcentajeATiempo: number | null;
  porcentajeTardio: number | null;
  porcentajeNoAplicado: number | null;
  /** Promedio de días de atraso ponderado por la cantidad de tardías de cada lote. */
  promedioDiasAtraso: number | null;
}

export function calcularKpisCumplimiento(filas: VacunacionCumplimientoLoteDto[]): KpisCumplimiento {
  let totalProgramadas = 0;
  let totalATiempo = 0;
  let totalTardias = 0;
  let totalIncumplidas = 0;
  let totalNoAplicadas = 0;
  let totalPendientes = 0;
  let sumaAtraso = 0;
  let tardiasConPromedio = 0;

  for (const f of filas) {
    const tardias = f.totalTardio1Semana + f.totalTardio2MasSemanas;
    totalProgramadas += f.totalProgramadas;
    totalATiempo += f.totalATiempo;
    totalTardias += tardias;
    totalIncumplidas += f.totalTardio2MasSemanas;
    totalNoAplicadas += f.totalNoAplicado;
    totalPendientes += f.totalPendiente;
    if (f.promedioDiasAtraso != null && tardias > 0) {
      sumaAtraso += f.promedioDiasAtraso * tardias;
      tardiasConPromedio += tardias;
    }
  }

  const pct = (n: number): number | null =>
    totalProgramadas ? Math.round((1000 * n) / totalProgramadas) / 10 : null;

  return {
    lotes: filas.length,
    totalProgramadas,
    totalATiempo,
    totalTardias,
    totalIncumplidas,
    totalNoAplicadas,
    totalPendientes,
    porcentajeATiempo: pct(totalATiempo),
    porcentajeTardio: pct(totalTardias),
    porcentajeNoAplicado: pct(totalNoAplicadas),
    promedioDiasAtraso: tardiasConPromedio ? Math.round((10 * sumaAtraso) / tardiasConPromedio) / 10 : null,
  };
}

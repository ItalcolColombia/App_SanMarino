// Spec ÚNICA de columnas de la hoja «RESUMEN SEMANAL» del Informe RA Pesadas.
// Alimenta la tabla EN PANTALLA y el export a Excel: si una columna se agrega
// acá, aparece en los dos lados. Función PURA (sin `this`, sin DI).
//
// El orden y los títulos replican el archivo original:
//   Levante:    RAZA | Edad | Lote | GRANJA | PART | Saldo H | Saldo M |
//               %MortH | %RetiroH | RetiroHGUIA | %DifConsH | %DifPesoH |
//               UniformH | %CVH | %MortM | %RetAcM | RetiroMGUIA |
//               DifConsM | %DifPesoM | UniformM | %CVM
//   Producción: RAZA | Edad | LoteRA | Granja | PART | Saldo H | Saldo M |
//               %Prod | Dif%Prod | HTAA | DifHTAA | HIAA | DifHIAA |
//               %AprovSem | Dif%AprovSem | GrHuevoInc | %MortH | %RetiroH |
//               RetiroHGUIA | %MortM | %RetiroM | RetiroMGUIA | PesoM/H
import {
  ResumenSemanalLevanteFila,
  ResumenSemanalProduccionFila
} from '../models/resumen-semanal-ra-pesadas.model';

export interface ColumnaResumen<T> {
  /** Cabecera de grupo (celdas contiguas con el mismo grupo se combinan). */
  grupo: string;
  titulo: string;
  dec: number;
  /** Clave del indicador en `totales.ponderados`; sin clave no hay total. */
  totalKey?: string;
  /** Alinea a la izquierda (texto). Por defecto los números van a la derecha. */
  texto?: boolean;
  valor: (fila: T) => number | string | null;
}

export interface GrupoCabeceraResumen {
  titulo: string;
  span: number;
}

/** Agrupa columnas contiguas del mismo grupo para la fila superior de la tabla. */
export function agruparColumnasResumen<T>(columnas: ColumnaResumen<T>[]): GrupoCabeceraResumen[] {
  const grupos: GrupoCabeceraResumen[] = [];
  for (const col of columnas) {
    const ultimo = grupos[grupos.length - 1];
    if (ultimo && ultimo.titulo === col.grupo) ultimo.span++;
    else grupos.push({ titulo: col.grupo, span: 1 });
  }
  return grupos;
}

/** Ubicación como la muestra el archivo: la granja incluye el núcleo/módulo. */
export function ubicacionResumen(fila: { granjaNombre: string | null; nucleoNombre: string | null }): string {
  const granja = fila.granjaNombre?.trim() || '—';
  const nucleo = fila.nucleoNombre?.trim();
  return nucleo ? `${granja} · ${nucleo}` : granja;
}

export const COLUMNAS_RESUMEN_LEVANTE: ColumnaResumen<ResumenSemanalLevanteFila>[] = [
  { grupo: 'Lote', titulo: 'Raza', dec: 0, texto: true, valor: f => f.raza },
  { grupo: 'Lote', titulo: 'Edad', dec: 0, valor: f => f.edadSemana },
  { grupo: 'Lote', titulo: 'Lote', dec: 0, texto: true, valor: f => f.loteNombre },
  { grupo: 'Lote', titulo: 'Granja', dec: 0, texto: true, valor: f => ubicacionResumen(f) },
  { grupo: 'Lote', titulo: 'Regional', dec: 0, texto: true, valor: f => f.regional },

  { grupo: 'Aves', titulo: 'Part %', dec: 2, valor: f => (f.part == null ? null : f.part * 100) },
  { grupo: 'Aves', titulo: 'Saldo H', dec: 0, totalKey: '__saldoHembras', valor: f => f.saldoHembras },
  { grupo: 'Aves', titulo: 'Saldo M', dec: 0, totalKey: '__saldoMachos', valor: f => f.saldoMachos },

  { grupo: 'Hembras', titulo: '% Mort', dec: 3, totalKey: 'mortHembrasPct', valor: f => f.mortHembrasPct },
  { grupo: 'Hembras', titulo: '% Retiro', dec: 3, totalKey: 'retiroAcumHembrasPct', valor: f => f.retiroAcumHembrasPct },
  { grupo: 'Hembras', titulo: 'Retiro Guía', dec: 3, totalKey: 'retiroAcumHembrasGuia', valor: f => f.retiroAcumHembrasGuia },
  { grupo: 'Hembras', titulo: '% Dif Cons', dec: 2, totalKey: 'difConsumoHembrasPct', valor: f => f.difConsumoHembrasPct },
  { grupo: 'Hembras', titulo: '% Dif Peso', dec: 2, totalKey: 'difPesoHembrasPct', valor: f => f.difPesoHembrasPct },
  { grupo: 'Hembras', titulo: 'Unif', dec: 1, totalKey: 'uniformidadHembras', valor: f => f.uniformidadHembras },
  { grupo: 'Hembras', titulo: '% CV', dec: 1, totalKey: 'cvHembras', valor: f => f.cvHembras },

  { grupo: 'Machos', titulo: '% Mort', dec: 3, totalKey: 'mortMachosPct', valor: f => f.mortMachosPct },
  { grupo: 'Machos', titulo: '% Ret Ac', dec: 3, totalKey: 'retiroAcumMachosPct', valor: f => f.retiroAcumMachosPct },
  { grupo: 'Machos', titulo: 'Retiro Guía', dec: 3, totalKey: 'retiroAcumMachosGuia', valor: f => f.retiroAcumMachosGuia },
  { grupo: 'Machos', titulo: 'Dif Cons', dec: 2, totalKey: 'difConsumoMachosPct', valor: f => f.difConsumoMachosPct },
  { grupo: 'Machos', titulo: '% Dif Peso', dec: 2, totalKey: 'difPesoMachosPct', valor: f => f.difPesoMachosPct },
  { grupo: 'Machos', titulo: 'Unif', dec: 1, totalKey: 'uniformidadMachos', valor: f => f.uniformidadMachos },
  { grupo: 'Machos', titulo: '% CV', dec: 1, totalKey: 'cvMachos', valor: f => f.cvMachos }
];

export const COLUMNAS_RESUMEN_PRODUCCION: ColumnaResumen<ResumenSemanalProduccionFila>[] = [
  { grupo: 'Lote', titulo: 'Raza', dec: 0, texto: true, valor: f => f.raza },
  { grupo: 'Lote', titulo: 'Edad', dec: 0, valor: f => f.edadSemana },
  { grupo: 'Lote', titulo: 'Lote', dec: 0, texto: true, valor: f => f.loteNombre },
  { grupo: 'Lote', titulo: 'Granja', dec: 0, texto: true, valor: f => ubicacionResumen(f) },
  { grupo: 'Lote', titulo: 'Regional', dec: 0, texto: true, valor: f => f.regional },
  { grupo: 'Lote', titulo: 'Ciclo', dec: 0, texto: true, valor: f => f.cicloProduccion },
  { grupo: 'Lote', titulo: 'Nido', dec: 0, texto: true, valor: f => f.tipoNido },

  { grupo: 'Aves', titulo: 'Part %', dec: 2, valor: f => (f.part == null ? null : f.part * 100) },
  { grupo: 'Aves', titulo: 'Saldo H', dec: 0, totalKey: '__saldoHembras', valor: f => f.saldoHembras },
  { grupo: 'Aves', titulo: 'Saldo M', dec: 0, totalKey: '__saldoMachos', valor: f => f.saldoMachos },

  { grupo: 'Producción', titulo: '% Prod', dec: 2, totalKey: 'produccionPct', valor: f => f.produccionPct },
  { grupo: 'Producción', titulo: '% Guía', dec: 2, totalKey: 'produccionPctGuia', valor: f => f.produccionPctGuia },
  { grupo: 'Producción', titulo: 'Dif %', dec: 2, totalKey: 'difProduccionPct', valor: f => f.difProduccionPct },

  { grupo: 'Huevos por ave alojada', titulo: 'HTAA', dec: 2, totalKey: 'htaa', valor: f => f.htaa },
  { grupo: 'Huevos por ave alojada', titulo: 'Guía', dec: 2, totalKey: 'htaaGuia', valor: f => f.htaaGuia },
  { grupo: 'Huevos por ave alojada', titulo: 'Dif', dec: 2, valor: f => f.difHtaa },
  { grupo: 'Huevos por ave alojada', titulo: 'HIAA', dec: 2, totalKey: 'hiaa', valor: f => f.hiaa },
  { grupo: 'Huevos por ave alojada', titulo: 'Guía', dec: 2, totalKey: 'hiaaGuia', valor: f => f.hiaaGuia },
  { grupo: 'Huevos por ave alojada', titulo: 'Dif', dec: 2, valor: f => f.difHiaa },

  { grupo: 'Aprovechamiento', titulo: '% Sem', dec: 2, totalKey: 'aprovSemPct', valor: f => f.aprovSemPct },
  { grupo: 'Aprovechamiento', titulo: '% Guía', dec: 2, totalKey: 'aprovSemPctGuia', valor: f => f.aprovSemPctGuia },
  { grupo: 'Aprovechamiento', titulo: 'Dif %', dec: 2, valor: f => f.difAprovSemPct },
  { grupo: 'Aprovechamiento', titulo: 'gr/H.Inc', dec: 1, totalKey: 'grHuevoInc', valor: f => f.grHuevoInc },

  { grupo: 'Hembras', titulo: '% Mort', dec: 3, totalKey: 'mortHembrasPct', valor: f => f.mortHembrasPct },
  { grupo: 'Hembras', titulo: '% Retiro', dec: 3, totalKey: 'retiroAcumHembrasPct', valor: f => f.retiroAcumHembrasPct },
  { grupo: 'Hembras', titulo: 'Retiro Guía', dec: 3, totalKey: 'retiroAcumHembrasGuia', valor: f => f.retiroAcumHembrasGuia },

  { grupo: 'Machos', titulo: '% Mort', dec: 3, totalKey: 'mortMachosPct', valor: f => f.mortMachosPct },
  { grupo: 'Machos', titulo: '% Retiro', dec: 3, totalKey: 'retiroAcumMachosPct', valor: f => f.retiroAcumMachosPct },
  { grupo: 'Machos', titulo: 'Retiro Guía', dec: 3, totalKey: 'retiroAcumMachosGuia', valor: f => f.retiroAcumMachosGuia },
  { grupo: 'Machos', titulo: 'Peso M/H %', dec: 2, totalKey: 'pesoMachoSobreHembra', valor: f => f.pesoMachoSobreHembra }
];

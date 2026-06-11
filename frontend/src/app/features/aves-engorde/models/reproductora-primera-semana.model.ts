// Tipos del tab «R. Reproductora» (lotes de pollitos primera semana).
// Estructurales: aceptan los DTOs reales (LoteReproductoraAveEngordeDto /
// SeguimientoLoteLevanteDto) sin acoplar las funciones puras a los services.

/** 1 quintal (QQ) = 45.36 kg — misma constante que el modal de seguimiento reproductora. */
export const REPRODUCTORA_QQ_TO_KG = 45.36;

/** Subconjunto del lote reproductora que necesitan los cálculos. */
export interface LoteReproductoraPrimeraSemanaLike {
  readonly id: number;
  readonly nombreLote: string;
  readonly codigoReproductora?: string | null;
  readonly fechaEncasetamiento: string | null;
  readonly h: number | null;
  readonly m: number | null;
  readonly avesInicioHembras?: number;
  readonly avesInicioMachos?: number;
  readonly pesoInicialH: number | null;
  readonly pesoInicialM: number | null;
}

/** Subconjunto del seguimiento diario que necesitan los cálculos. */
export interface SeguimientoPrimeraSemanaLike {
  readonly fechaRegistro: string;
  readonly mortalidadHembras: number | null;
  readonly mortalidadMachos: number | null;
  readonly selH: number | null;
  readonly selM: number | null;
  readonly errorSexajeHembras?: number | null;
  readonly errorSexajeMachos?: number | null;
  readonly consumoKgHembras: number | null;
  readonly consumoKgMachos?: number | null;
  readonly pesoPromH?: number | null;
  readonly pesoPromM?: number | null;
}

/** Lote reproductora + sus seguimientos, insumo de construirBloquesReproductora. */
export interface InsumoBloqueReproductora {
  readonly lote: LoteReproductoraPrimeraSemanaLike;
  readonly seguimientos: ReadonlyArray<SeguimientoPrimeraSemanaLike>;
}

/** Fila de un día (edad 1..7) dentro de un bloque H o M. */
export interface FilaDiaBloque {
  dia: number;
  /** true si existe registro de seguimiento para esta edad. */
  conRegistro: boolean;
  /** Guía genética (gr/ave/día) — mapeado para fase 2, hoy oculto en UI. */
  consumoTablaGr: number | null;
  /** Guía genética en quintales — mapeado para fase 2, hoy oculto en UI. */
  qqTabla: number | null;
  qqReal: number | null;
  consumoKg: number | null;
  /** Consumo g/ave/día = kg × 1000 ÷ saldo al inicio del día. */
  grsAve: number | null;
  pesoG: number | null;
  /** vs día anterior; día 1 vs peso de llegada. */
  gananciaG: number | null;
  /** Conv. = grs/ave ÷ ganancia del día; día 1 = grs/ave ÷ peso de llegada (criterio Excel). */
  conv: number | null;
  muertosNorm: number;
  muertosSel: number;
  /** Saldo de aves al cierre del día. */
  saldo: number;
  /** % = muertos ÷ saldo al inicio del día (×100). */
  pctNorm: number | null;
  pctSel: number | null;
}

/** Totales del bloque (fila «Total» — el Excel la titula «Promedio» pero suma). */
export interface TotalesBloque {
  qqReal: number;
  consumoKg: number;
  grsAve: number;
  muertosNorm: number;
  muertosSel: number;
  /** Suma de los % diarios (semántica del Excel). */
  pctNorm: number;
  pctSel: number;
}

/** Bloque primera semana de un sexo (H o M) de un lote reproductora. */
export interface BloquePrimeraSemana {
  loteReproductoraId: number;
  nombreLote: string;
  /** Título estilo Excel: 'H-34' / 'M-34' (de codigoReproductora) o fallback. */
  titulo: string;
  sexo: 'H' | 'M';
  fechaEncaset: string | null;
  pollitos: number;
  pesoLlegadaG: number | null;
  filas: FilaDiaBloque[];
  totales: TotalesBloque;
  /** Saldo de aves al cierre del día 7. */
  saldoFinal: number;
  /** Peso prom registrado en la edad 7 (g) — base del VPI. */
  peso7DiasG: number | null;
}

/** Fila del resumen superior (una por bloque H/M). */
export interface ResumenReproductoraFila {
  /** Nombre del lote reproductora — solo se muestra en la primera fila del par. */
  grupo: string;
  esPrimeraDelGrupo: boolean;
  lote: string;
  cantidad: number;
  pesoLlegadaG: number | null;
  cantidadXPeso: number | null;
  peso7DiasG: number | null;
  cantidadX7Dias: number | null;
  /** VPI = peso 7 días ÷ peso llegada. */
  vpi: number | null;
}

export interface ResumenReproductora {
  filas: ResumenReproductoraFila[];
  totalCantidad: number;
  totalCantidadXPeso: number;
  totalCantidadX7Dias: number;
  /** VPI total = Σ(cantidad×peso7) ÷ Σ(cantidad×pesoLlegada). */
  vpiTotal: number | null;
}

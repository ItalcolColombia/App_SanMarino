// Especificación PURA de columnas del Reporte Técnico Semanal (Sanmarino).
// Una sola fuente de verdad para la tabla en pantalla y el export a Excel:
// cada columna define grupo (cabecera nivel 1), título (nivel 2), extractor
// del valor crudo y decimales de presentación.
import {
  ReporteSemanalLevanteSemana,
  ReporteSemanalProduccionSemana
} from '../models/reporte-tecnico-semanal.model';

export interface ColumnaReporte<T> {
  grupo: string;
  titulo: string;
  dec: number;                        // decimales de presentación (0 = entero)
  valor: (s: T) => number | string | null;
}

export interface GrupoCabecera {
  titulo: string;
  span: number;
}

/** Cabecera nivel 1: agrupa columnas contiguas con el mismo grupo. */
export function agruparColumnas<T>(columnas: ColumnaReporte<T>[]): GrupoCabecera[] {
  const grupos: GrupoCabecera[] = [];
  for (const col of columnas) {
    const ultimo = grupos[grupos.length - 1];
    if (ultimo && ultimo.titulo === col.grupo) ultimo.span++;
    else grupos.push({ titulo: col.grupo, span: 1 });
  }
  return grupos;
}

const fechaYmd = (iso: string | null): string | null => (iso ? String(iso).slice(0, 10) : null);

// ─────────────────────────────────────────────────────────────────────────────
// LEVANTE — columnas del "Resumen Semanal Galpón de Levante".
// ─────────────────────────────────────────────────────────────────────────────
export const COLUMNAS_LEVANTE: ColumnaReporte<ReporteSemanalLevanteSemana>[] = [
  { grupo: 'Semana', titulo: 'Sem', dec: 0, valor: s => s.semana },
  { grupo: 'Semana', titulo: 'Fecha fin', dec: 0, valor: s => fechaYmd(s.fechaFinSemana) },
  { grupo: 'Aves', titulo: 'Hembras', dec: 0, valor: s => s.avesHembrasFin },
  { grupo: 'Aves', titulo: 'Machos', dec: 0, valor: s => s.avesMachosFin },
  { grupo: 'Aves', titulo: 'M:H %', dec: 2, valor: s => s.relacionMachosHembrasPct },

  { grupo: 'H · Mortalidad', titulo: 'Sem', dec: 0, valor: s => s.mortalidadHembras },
  { grupo: 'H · Mortalidad', titulo: '%', dec: 3, valor: s => s.mortalidadHembrasPct },
  { grupo: 'H · Mortalidad', titulo: 'Acum %', dec: 3, valor: s => s.mortalidadHembrasAcumPct },
  { grupo: 'H · Descarte', titulo: 'Sem', dec: 0, valor: s => s.seleccionHembras },
  { grupo: 'H · Descarte', titulo: '%', dec: 3, valor: s => s.seleccionHembrasPct },
  { grupo: 'H · Descarte', titulo: 'Acum %', dec: 3, valor: s => s.seleccionHembrasAcumPct },
  { grupo: 'H · Descarte', titulo: 'M+D % Guía', dec: 3, valor: s => s.mortSelHembrasGuiaPct },
  { grupo: 'H · Error sexaje', titulo: 'Sem', dec: 0, valor: s => s.errorHembras },
  { grupo: 'H · Error sexaje', titulo: '%', dec: 3, valor: s => s.errorHembrasPct },
  { grupo: 'H · Error sexaje', titulo: 'Acum %', dec: 3, valor: s => s.errorHembrasAcumPct },
  { grupo: 'H · Retiro acum %', titulo: 'Real', dec: 3, valor: s => s.retiroAcumHembrasPct },
  { grupo: 'H · Retiro acum %', titulo: 'Guía', dec: 3, valor: s => s.retiroAcumHembrasGuiaPct },

  { grupo: 'H · Alimento', titulo: 'Sem Kg', dec: 1, valor: s => s.consumoKgHembras },
  { grupo: 'H · Alimento', titulo: 'Acum Kg', dec: 1, valor: s => s.consumoKgHembrasAcum },
  { grupo: 'H · Alimento', titulo: 'gr/a/d', dec: 1, valor: s => s.grAveDiaHembras },
  { grupo: 'H · Alimento', titulo: 'gr/a/d Guía', dec: 1, valor: s => s.grAveDiaHembrasGuia },
  { grupo: 'H · Alimento', titulo: 'Increm', dec: 1, valor: s => s.incrementoGrAveDiaHembras },
  { grupo: 'H · Alimento', titulo: 'Increm Guía', dec: 1, valor: s => s.incrementoGrAveDiaHembrasGuia },
  { grupo: 'H · Alimento', titulo: 'Acum gr/ave', dec: 0, valor: s => s.consumoAcumGrAveHembras },
  { grupo: 'H · Alimento', titulo: 'Acum Guía', dec: 0, valor: s => s.consumoAcumGrAveHembrasGuia },

  { grupo: 'H · Peso', titulo: 'Real', dec: 0, valor: s => s.pesoHembras },
  { grupo: 'H · Peso', titulo: 'Guía', dec: 0, valor: s => s.pesoHembrasGuia },
  { grupo: 'H · Peso', titulo: 'Gananc', dec: 0, valor: s => s.gananciaHembras },
  { grupo: 'H · Peso', titulo: '% Desv', dec: 2, valor: s => s.desviacionPesoHembrasPct },

  { grupo: 'Uniformidad', titulo: 'U% Real', dec: 1, valor: s => s.uniformidadHembras },
  { grupo: 'Uniformidad', titulo: 'U% Guía', dec: 1, valor: s => s.uniformidadGuia },
  { grupo: 'Uniformidad', titulo: 'C.V.%', dec: 1, valor: s => s.cvHembras },

  { grupo: 'Nutrición', titulo: 'Kcal', dec: 0, valor: s => s.kcalAlimentoHembras },
  { grupo: 'Nutrición', titulo: '% Prot', dec: 1, valor: s => s.protAlimentoHembras },
  { grupo: 'Nutrición', titulo: 'Kcal acum/ave', dec: 1, valor: s => s.kcalAveAcumHembras },
  { grupo: 'Nutrición', titulo: 'Prot gr acum/ave', dec: 1, valor: s => s.protAveAcumHembras },

  { grupo: 'M · Mortalidad', titulo: 'Sem', dec: 0, valor: s => s.mortalidadMachos },
  { grupo: 'M · Mortalidad', titulo: '%', dec: 3, valor: s => s.mortalidadMachosPct },
  { grupo: 'M · Mortalidad', titulo: 'Acum %', dec: 3, valor: s => s.mortalidadMachosAcumPct },
  { grupo: 'M · Descarte', titulo: 'Sem', dec: 0, valor: s => s.seleccionMachos },
  { grupo: 'M · Descarte', titulo: '%', dec: 3, valor: s => s.seleccionMachosPct },
  { grupo: 'M · Descarte', titulo: 'Acum %', dec: 3, valor: s => s.seleccionMachosAcumPct },
  { grupo: 'M · Descarte', titulo: 'M % Guía', dec: 3, valor: s => s.mortSelMachosGuiaPct },
  { grupo: 'M · Error sexaje', titulo: 'Sem', dec: 0, valor: s => s.errorMachos },
  { grupo: 'M · Error sexaje', titulo: '%', dec: 3, valor: s => s.errorMachosPct },
  { grupo: 'M · Error sexaje', titulo: 'Acum %', dec: 3, valor: s => s.errorMachosAcumPct },
  { grupo: 'M · Retiro acum %', titulo: 'Real', dec: 3, valor: s => s.retiroAcumMachosPct },
  { grupo: 'M · Retiro acum %', titulo: 'Guía', dec: 3, valor: s => s.retiroAcumMachosGuiaPct },

  { grupo: 'M · Alimento', titulo: 'Sem Kg', dec: 1, valor: s => s.consumoKgMachos },
  { grupo: 'M · Alimento', titulo: 'Acum Kg', dec: 1, valor: s => s.consumoKgMachosAcum },
  { grupo: 'M · Alimento', titulo: 'gr/a/d', dec: 1, valor: s => s.grAveDiaMachos },
  { grupo: 'M · Alimento', titulo: 'gr/a/d Guía', dec: 1, valor: s => s.grAveDiaMachosGuia },
  { grupo: 'M · Alimento', titulo: 'Increm', dec: 1, valor: s => s.incrementoGrAveDiaMachos },
  { grupo: 'M · Alimento', titulo: 'Increm Guía', dec: 1, valor: s => s.incrementoGrAveDiaMachosGuia },
  { grupo: 'M · Alimento', titulo: 'Acum gr/ave', dec: 0, valor: s => s.consumoAcumGrAveMachos },
  { grupo: 'M · Alimento', titulo: 'Acum Guía', dec: 0, valor: s => s.consumoAcumGrAveMachosGuia },

  { grupo: 'M · Peso', titulo: 'Real', dec: 0, valor: s => s.pesoMachos },
  { grupo: 'M · Peso', titulo: 'Guía', dec: 0, valor: s => s.pesoMachosGuia },
  { grupo: 'M · Peso', titulo: 'Gananc', dec: 0, valor: s => s.gananciaMachos },
  { grupo: 'M · Peso', titulo: '% Desv', dec: 2, valor: s => s.desviacionPesoMachosPct }
];

// ─────────────────────────────────────────────────────────────────────────────
// PRODUCCIÓN — columnas del "Resumen Semanal de Producción".
// ─────────────────────────────────────────────────────────────────────────────
export const COLUMNAS_PRODUCCION: ColumnaReporte<ReporteSemanalProduccionSemana>[] = [
  { grupo: 'Semana', titulo: 'Sem', dec: 0, valor: s => s.semana },
  { grupo: 'Semana', titulo: 'Fecha fin', dec: 0, valor: s => fechaYmd(s.fechaFinSemana) },
  { grupo: 'Aves', titulo: 'Hembras', dec: 0, valor: s => s.avesHembrasFin },
  { grupo: 'Aves', titulo: 'Machos', dec: 0, valor: s => s.avesMachosFin },
  { grupo: 'Apareo M:H %', titulo: 'Real', dec: 2, valor: s => s.apareoPct },
  { grupo: 'Apareo M:H %', titulo: 'Guía', dec: 2, valor: s => s.apareoGuiaPct },

  { grupo: 'H · Mort-Descarte', titulo: 'Mort', dec: 0, valor: s => s.mortalidadHembras },
  { grupo: 'H · Mort-Descarte', titulo: 'Desc', dec: 0, valor: s => s.seleccionHembras },
  { grupo: 'H · Mort-Descarte', titulo: '% Mort', dec: 3, valor: s => s.mortalidadHembrasPct },
  { grupo: 'H · Mort-Descarte', titulo: '% Guía', dec: 3, valor: s => s.mortalidadHembrasGuiaPct },
  { grupo: 'H · Mort-Descarte', titulo: '% Mrt Ac Guía', dec: 3, valor: s => s.mortalidadHembrasAcumGuiaPct },
  { grupo: 'H · Mort-Descarte', titulo: '% M+D Ac', dec: 3, valor: s => s.mortSelHembrasAcumPct },
  { grupo: 'H · Mort-Descarte', titulo: 'Guía Ac', dec: 3, valor: s => s.retiroAcumHembrasGuiaPct },

  { grupo: 'M · Mort-Descarte', titulo: 'Mort', dec: 0, valor: s => s.mortalidadMachos },
  { grupo: 'M · Mort-Descarte', titulo: 'Desc', dec: 0, valor: s => s.seleccionMachos },
  { grupo: 'M · Mort-Descarte', titulo: '% Mort', dec: 3, valor: s => s.mortalidadMachosPct },
  { grupo: 'M · Mort-Descarte', titulo: '% Guía', dec: 3, valor: s => s.mortalidadMachosGuiaPct },
  { grupo: 'M · Mort-Descarte', titulo: '% M+D Ac', dec: 3, valor: s => s.mortSelMachosAcumPct },
  { grupo: 'M · Mort-Descarte', titulo: 'Guía Ac', dec: 3, valor: s => s.retiroAcumMachosGuiaPct },

  { grupo: 'Producción huevos', titulo: 'Semana', dec: 0, valor: s => s.huevosTotales },
  { grupo: 'Producción huevos', titulo: 'Acum', dec: 0, valor: s => s.huevosTotalesAcum },
  { grupo: 'Producción huevos', titulo: 'H.T.A.A', dec: 2, valor: s => s.htaa },
  { grupo: 'Producción huevos', titulo: 'Guía', dec: 2, valor: s => s.htaaGuia },
  { grupo: 'Producción huevos', titulo: '% a/d', dec: 2, valor: s => s.porcentajeProduccion },
  { grupo: 'Producción huevos', titulo: '% Guía', dec: 2, valor: s => s.porcentajeProduccionGuia },

  { grupo: 'Huevos incubables', titulo: 'Semana', dec: 0, valor: s => s.huevosIncubables },
  { grupo: 'Huevos incubables', titulo: 'Acum', dec: 0, valor: s => s.huevosIncubablesAcum },
  { grupo: 'Huevos incubables', titulo: '% H.I', dec: 2, valor: s => s.porcentajeIncubables },
  { grupo: 'Huevos incubables', titulo: '% Guía', dec: 2, valor: s => s.porcentajeIncubablesGuia },
  { grupo: 'Huevos incubables', titulo: '% H.I Ac', dec: 2, valor: s => s.porcentajeIncubablesAcum },
  { grupo: 'Huevos incubables', titulo: '% Ac Guía', dec: 2, valor: s => s.porcentajeIncubablesAcumGuia },
  { grupo: 'Huevos incubables', titulo: 'H.I.A.A', dec: 2, valor: s => s.hiaa },
  { grupo: 'Huevos incubables', titulo: 'Guía', dec: 2, valor: s => s.hiaaGuia },

  { grupo: 'H · Alimento', titulo: 'Sem Kg', dec: 1, valor: s => s.consumoKgHembras },
  { grupo: 'H · Alimento', titulo: 'Acum Kg', dec: 1, valor: s => s.consumoKgHembrasAcum },
  { grupo: 'H · Alimento', titulo: 'gr/a/d', dec: 1, valor: s => s.grAveDiaHembras },
  { grupo: 'H · Alimento', titulo: 'Guía', dec: 1, valor: s => s.grAveDiaHembrasGuia },
  { grupo: 'H · Alimento', titulo: 'Increm', dec: 1, valor: s => s.incrementoGrAveDiaHembras },

  { grupo: 'M · Alimento', titulo: 'Sem Kg', dec: 1, valor: s => s.consumoKgMachos },
  { grupo: 'M · Alimento', titulo: 'Acum Kg', dec: 1, valor: s => s.consumoKgMachosAcum },
  { grupo: 'M · Alimento', titulo: 'gr/a/d', dec: 1, valor: s => s.grAveDiaMachos },
  { grupo: 'M · Alimento', titulo: 'Guía', dec: 1, valor: s => s.grAveDiaMachosGuia },

  { grupo: 'Conversión', titulo: 'gr/H.I', dec: 1, valor: s => s.conversionGrHuevoInc },
  { grupo: 'Conversión', titulo: 'Guía', dec: 1, valor: s => s.conversionGrHuevoIncGuia },

  { grupo: 'Peso huevo', titulo: 'Real', dec: 1, valor: s => s.pesoHuevo },
  { grupo: 'Peso huevo', titulo: 'Stdar', dec: 1, valor: s => s.pesoHuevoGuia },
  { grupo: 'Peso huevo', titulo: 'Masa lote', dec: 2, valor: s => s.masaHuevoLote },
  { grupo: 'Peso huevo', titulo: 'Masa guía', dec: 2, valor: s => s.masaHuevoGuia },

  { grupo: 'H · Peso', titulo: 'Real', dec: 0, valor: s => s.pesoHembras },
  { grupo: 'H · Peso', titulo: 'Guía', dec: 0, valor: s => s.pesoHembrasGuia },
  { grupo: 'H · Peso', titulo: 'Desv %', dec: 2, valor: s => s.desviacionPesoHembrasPct },
  { grupo: 'M · Peso', titulo: 'Real', dec: 0, valor: s => s.pesoMachos },
  { grupo: 'M · Peso', titulo: 'Guía', dec: 0, valor: s => s.pesoMachosGuia },
  { grupo: 'M · Peso', titulo: 'Desv %', dec: 2, valor: s => s.desviacionPesoMachosPct },

  { grupo: 'Uniformidad', titulo: 'U%', dec: 1, valor: s => s.uniformidad },
  { grupo: 'Uniformidad', titulo: 'U% Guía', dec: 1, valor: s => s.uniformidadGuia },
  { grupo: 'Uniformidad', titulo: 'C.V.%', dec: 1, valor: s => s.coeficienteVariacion },

  // Bloque POLLITOS del Excel. "HI Cargado" es real (traslado_huevos a planta);
  // nacimientos y pollitos reales no se capturan en el sistema → solo guía.
  { grupo: 'Pollitos', titulo: 'HI Cargado', dec: 0, valor: s => s.huevosCargadosPlanta },
  { grupo: 'Pollitos', titulo: 'HI Cargado Acum', dec: 0, valor: s => s.huevosCargadosPlantaAcum },
  { grupo: 'Pollitos', titulo: '% Enviado', dec: 1, valor: s => s.porcentajeCargaSobreIncubables },
  { grupo: 'Pollitos', titulo: 'Nacim % Guía', dec: 1, valor: s => s.nacimientoGuiaPct },
  { grupo: 'Pollitos', titulo: 'Pollito/ave Guía', dec: 2, valor: s => s.pollitosAveGuia }
];

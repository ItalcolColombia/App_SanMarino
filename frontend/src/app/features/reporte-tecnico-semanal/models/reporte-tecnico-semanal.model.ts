// Modelos del Reporte Técnico Semanal (Sanmarino postura).
// Espejo 1:1 de los DTOs del backend (ReporteTecnicoSemanalDtos.cs).

export interface ReporteTecnicoSemanalRequest {
  lotePosturaBaseId: number;
  semanaDesde?: number | null;
  semanaHasta?: number | null;
}

export interface ReporteSemanalTabHeader {
  loteId: number | null;
  lotePosturaProduccionId: number | null;
  loteNombre: string;
  esConsolidado: boolean;
  granjaId: number | null;
  granjaNombre: string | null;
  municipio: string | null;
  nucleoId: string | null;
  nucleoNombre: string | null;
  galponId: string | null;
  galponNombre: string | null;
  tecnico: string | null;
  raza: string | null;
  anioGuia: number | null;
  fechaEncaset: string | null;
  fechaInicioProduccion: string | null;
  baseHembras: number;
  baseMachos: number;
  pesoInicialHembras: number | null;
  mortCajasHembras: number | null;
  mortCajasMachos: number | null;
}

export interface ReporteSemanalLevanteSemana {
  semana: number;
  fechaFinSemana: string | null;
  diasConRegistro: number;
  avesHembrasFin: number;
  avesMachosFin: number;
  relacionMachosHembrasPct: number | null;

  mortalidadHembras: number;
  mortalidadHembrasPct: number | null;
  mortalidadHembrasAcumPct: number | null;
  seleccionHembras: number;
  seleccionHembrasPct: number | null;
  seleccionHembrasAcumPct: number | null;
  mortSelHembrasGuiaPct: number | null;
  errorHembras: number;
  errorHembrasPct: number | null;
  errorHembrasAcumPct: number | null;
  retiroAcumHembrasPct: number | null;
  retiroAcumHembrasGuiaPct: number | null;

  consumoKgHembras: number;
  consumoKgHembrasAcum: number;
  grAveDiaHembras: number | null;
  grAveDiaHembrasGuia: number | null;
  incrementoGrAveDiaHembras: number | null;
  incrementoGrAveDiaHembrasGuia: number | null;
  consumoAcumGrAveHembras: number | null;
  consumoAcumGrAveHembrasGuia: number | null;

  pesoHembras: number | null;
  pesoHembrasGuia: number | null;
  gananciaHembras: number | null;
  desviacionPesoHembrasPct: number | null;

  uniformidadHembras: number | null;
  uniformidadGuia: number | null;
  cvHembras: number | null;

  kcalAlimentoHembras: number | null;
  protAlimentoHembras: number | null;
  kcalAveAcumHembras: number | null;
  protAveAcumHembras: number | null;

  mortalidadMachos: number;
  mortalidadMachosPct: number | null;
  mortalidadMachosAcumPct: number | null;
  seleccionMachos: number;
  seleccionMachosPct: number | null;
  seleccionMachosAcumPct: number | null;
  mortSelMachosGuiaPct: number | null;
  errorMachos: number;
  errorMachosPct: number | null;
  errorMachosAcumPct: number | null;
  retiroAcumMachosPct: number | null;
  retiroAcumMachosGuiaPct: number | null;

  consumoKgMachos: number;
  consumoKgMachosAcum: number;
  grAveDiaMachos: number | null;
  grAveDiaMachosGuia: number | null;
  incrementoGrAveDiaMachos: number | null;
  incrementoGrAveDiaMachosGuia: number | null;
  consumoAcumGrAveMachos: number | null;
  consumoAcumGrAveMachosGuia: number | null;

  pesoMachos: number | null;
  pesoMachosGuia: number | null;
  gananciaMachos: number | null;
  desviacionPesoMachosPct: number | null;
}

export interface ReporteSemanalLevanteTab {
  header: ReporteSemanalTabHeader;
  semanas: ReporteSemanalLevanteSemana[];
}

export interface ReporteTecnicoSemanalLevanteResponse {
  lotePosturaBaseId: number;
  loteBaseNombre: string;
  raza: string | null;
  anioGuia: number | null;
  tieneGuia: boolean;
  tabs: ReporteSemanalLevanteTab[];
  consolidado: ReporteSemanalLevanteTab | null;
}

export interface ReporteSemanalProduccionSemana {
  semana: number;
  fechaInicioSemana: string | null;
  fechaFinSemana: string | null;
  diasConRegistro: number;

  avesHembrasFin: number;
  avesMachosFin: number;
  apareoPct: number | null;
  apareoGuiaPct: number | null;

  mortalidadHembras: number;
  seleccionHembras: number;
  mortalidadHembrasPct: number | null;
  mortalidadHembrasGuiaPct: number | null;
  mortalidadHembrasAcumGuiaPct: number | null;
  mortSelHembrasAcumPct: number | null;
  retiroAcumHembrasGuiaPct: number | null;

  mortalidadMachos: number;
  seleccionMachos: number;
  mortalidadMachosPct: number | null;
  mortalidadMachosGuiaPct: number | null;
  mortSelMachosAcumPct: number | null;
  retiroAcumMachosGuiaPct: number | null;

  huevosTotales: number;
  huevosTotalesAcum: number;
  htaa: number | null;
  htaaGuia: number | null;
  porcentajeProduccion: number | null;
  porcentajeProduccionGuia: number | null;

  huevosIncubables: number;
  huevosIncubablesAcum: number;
  porcentajeIncubables: number | null;
  porcentajeIncubablesGuia: number | null;
  porcentajeIncubablesAcum: number | null;
  porcentajeIncubablesAcumGuia: number | null;
  hiaa: number | null;
  hiaaGuia: number | null;

  consumoKgHembras: number;
  consumoKgHembrasAcum: number;
  grAveDiaHembras: number | null;
  grAveDiaHembrasGuia: number | null;
  incrementoGrAveDiaHembras: number | null;

  consumoKgMachos: number;
  consumoKgMachosAcum: number;
  grAveDiaMachos: number | null;
  grAveDiaMachosGuia: number | null;

  conversionGrHuevoInc: number | null;
  conversionGrHuevoIncGuia: number | null;

  pesoHuevo: number | null;
  pesoHuevoGuia: number | null;
  masaHuevoLote: number | null;
  masaHuevoGuia: number | null;

  pesoHembras: number | null;
  pesoHembrasGuia: number | null;
  desviacionPesoHembrasPct: number | null;
  pesoMachos: number | null;
  pesoMachosGuia: number | null;
  desviacionPesoMachosPct: number | null;

  uniformidad: number | null;
  uniformidadGuia: number | null;
  coeficienteVariacion: number | null;

  /** "HI Cargado": incubables enviados a planta en la semana (traslado_huevos). */
  huevosCargadosPlanta: number;
  huevosCargadosPlantaAcum: number;
  porcentajeCargaSobreIncubables: number | null;
  /** Nacimientos/pollitos reales no se capturan en el sistema: solo valor de guía. */
  nacimientoGuiaPct: number | null;
  pollitosAveGuia: number | null;
}

export interface ReporteSemanalProduccionTab {
  header: ReporteSemanalTabHeader;
  semanas: ReporteSemanalProduccionSemana[];
}

export interface ReporteTecnicoSemanalProduccionResponse {
  lotePosturaBaseId: number;
  loteBaseNombre: string;
  raza: string | null;
  anioGuia: number | null;
  tieneGuia: boolean;
  tabs: ReporteSemanalProduccionTab[];
  consolidado: ReporteSemanalProduccionTab | null;
}

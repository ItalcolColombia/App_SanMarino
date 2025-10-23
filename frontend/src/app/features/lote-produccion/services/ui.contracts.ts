// src/app/features/lote-produccion/services/ui.contracts.ts
export interface FiltrosVM {
  granjaId: number | null;
  nucleoId: string | null;
  galponId: string | null;
  loteId: number | null;
}

export interface TablaItemVM {
  id: number;
  fechaRegistro: string; // DD/MM/YYYY
  mortalidadH: number;
  mortalidadM: number;
  consumoKg: number;
  huevosTotales: number;
  huevosIncubables: number;
  pesoHuevo: number;
  observaciones?: string;
}

export interface IndicadoresVM {
  totalMortalidadH: number;
  totalMortalidadM: number;
  totalConsumoKg: number;
  totalHuevosTotales: number;
  totalHuevosIncubables: number;
  promedioPesoHuevo: number;
  promedioConsumoDiario: number;
  promedioHuevosDiarios: number;
}

export interface SerieVM {
  name: string;
  data: number[];
  type: 'line' | 'bar';
  color?: string;
}

export interface LoteVM {
  loteId: number;
  loteNombre: string;
  fechaInicio: string; // DD/MM/YYYY
  avesInicialesH: number;
  avesInicialesM: number;
  observaciones?: string;
}

export interface ProduccionQuery {
  loteId: number;
  desde?: string; // ISO date
  hasta?: string; // ISO date
  page?: number;
  size?: number;
}
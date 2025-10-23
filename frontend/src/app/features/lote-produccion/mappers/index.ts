// src/app/features/lote-produccion/mappers/index.ts
// Exportaciones centralizadas de todos los mappers

// Mappers principales
export * from './produccion.mappers';

// Utilidades
export * from './mapper.utils';

// Re-exportar tipos importantes para conveniencia
export type {
  FiltrosVM,
  TablaItemVM,
  IndicadoresVM,
  SerieVM,
  LoteVM
} from '../services/ui.contracts';

export type {
  CrearProduccionLoteRequest,
  CrearSeguimientoRequest,
  ProduccionLoteDetalleDto,
  SeguimientoItemDto,
  ExisteProduccionLoteResponse,
  ListaSeguimientoResponse
} from '../services/produccion.service';

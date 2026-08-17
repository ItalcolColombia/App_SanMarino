// src/app/features/vacunacion/models/vacunacion-plantilla.model.ts
// Tipos 1:1 con backend/src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionPlantillaDtos.cs
import { LineaProductiva } from './vacunacion.model';

/** La plantilla no admite 'Fecha': una fecha fija sería la misma para lotes de meses distintos. */
export type UnidadObjetivoPlantilla = 'Semana' | 'Dia';

export interface VacunacionPlantillaDto {
  id: number;
  nombre: string;
  lineaProductiva: LineaProductiva;
  raza: string | null;
  vigenteDesde: string | null;
  activa: boolean;
  notas: string | null;
  cantidadItems: number;
}

export interface VacunacionPlantillaItemDto {
  id: number;
  plantillaId: number;
  itemInventarioId: number;
  itemInventarioNombre: string;
  unidadObjetivo: UnidadObjetivoPlantilla;
  valorObjetivo: number;
  rangoDiasAntes: number;
  rangoDiasDespues: number;
  orden: number;
  notas: string | null;
}

export interface VacunacionPlantillaDetalleDto {
  id: number;
  nombre: string;
  lineaProductiva: LineaProductiva;
  raza: string | null;
  vigenteDesde: string | null;
  activa: boolean;
  notas: string | null;
  items: VacunacionPlantillaItemDto[];
}

export interface VacunacionPlantillaCreateRequest {
  nombre: string;
  lineaProductiva: LineaProductiva;
  raza: string | null;
  vigenteDesde: string | null;
  notas: string | null;
}

export interface VacunacionPlantillaUpdateRequest {
  nombre: string;
  lineaProductiva: LineaProductiva;
  raza: string | null;
  vigenteDesde: string | null;
  activa: boolean;
  notas: string | null;
}

export interface VacunacionPlantillaItemCreateRequest {
  itemInventarioId: number;
  unidadObjetivo: UnidadObjetivoPlantilla;
  valorObjetivo: number;
  rangoDiasAntes: number;
  rangoDiasDespues: number;
  orden?: number;
  notas?: string | null;
}

export interface VacunacionPlantillaItemUpdateRequest {
  itemInventarioId: number;
  unidadObjetivo: UnidadObjetivoPlantilla;
  valorObjetivo: number;
  rangoDiasAntes: number;
  rangoDiasDespues: number;
  orden: number;
  notas: string | null;
}

/**
 * Qué plantilla le tocaría a un lote y **por qué**. `plantilla` en null significa «este lote no tiene
 * cronograma automático», y `motivo` dice cuál de las causas posibles es — que es lo que el usuario
 * necesita para saber dónde corregir.
 */
export interface VacunacionPlantillaEfectivaDto {
  lineaProductiva: LineaProductiva;
  loteId: number;
  loteNombre: string | null;
  raza: string | null;
  fechaEncaset: string | null;
  plantilla: VacunacionPlantillaDetalleDto | null;
  motivo: string;
}

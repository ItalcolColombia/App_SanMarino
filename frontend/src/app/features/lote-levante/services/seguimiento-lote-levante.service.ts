import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface SeguimientoLoteLevanteDto {
  id: number;
  fechaRegistro: string;          // ISO
  loteId: string;
  /** ID de lote_postura_levante. Solo aplica para seguimiento tipo levante. */
  lotePosturaLevanteId?: number | null;

  mortalidadHembras: number;
  mortalidadMachos: number;
  selH: number;
  selM: number;
  errorSexajeHembras: number;
  errorSexajeMachos: number;

  tipoAlimento: string;
  consumoKgHembras: number; // Consumo convertido a kg (para cálculos)

  // Opcionales nuevos
  consumoKgMachos?: number | null;
  pesoPromH?: number | null;
  pesoPromM?: number | null;
  uniformidadH?: number | null;
  uniformidadM?: number | null;
  cvH?: number | null;
  cvM?: number | null;

  // Metadata JSONB para campos adicionales/extras
  metadata?: any | null; // JSON object con consumo original y otros campos adicionales
  
  // Items adicionales JSONB para otros tipos de ítems (vacunas, medicamentos, etc.)
  // que NO son alimentos. Los alimentos se mantienen en campos tradicionales.
  itemsAdicionales?: any | null; // JSON object con itemsHembras e itemsMachos (solo no-alimentos)

  // Campos de agua (solo para Ecuador y Panamá)
  consumoAguaDiario?: number | null;
  consumoAguaPh?: number | null;
  consumoAguaOrp?: number | null;
  consumoAguaTemperatura?: number | null;

  observaciones?: string;
  kcalAlH?: number | null;
  protAlH?: number | null;
  kcalAveH?: number | null;
  protAveH?: number | null;
  ciclo: string;                  // "Normal" | "Reforzado"
  tipoAlimentoHembras?: number | null; // (calculo interno, no se envía en create/update)
  tipoAlimentoMachos?: number | null;  // (calculo interno, no se envía en create/update)
  /** Saldo de alimento (kg) al cierre del día en bodega; solo pollo de engorde (API). */
  saldoAlimentoKg?: number | null;
}

// Representa un ítem individual en el seguimiento
export interface ItemSeguimientoDto {
  tipoItem: string; // "alimento", "vacuna", "medicamento", etc.
  catalogItemId: number; // ID del ítem del inventario (en Ecuador/Panamá = item_inventario_ecuador id)
  /** Ecuador/Panamá: ID de item_inventario_ecuador para aplicar consumo en inventario-gestion. */
  itemInventarioEcuadorId?: number | null;
  cantidad: number; // Cantidad utilizada
  unidad: string; // "kg", "g", "unidades", etc.
}

export interface CreateSeguimientoLoteLevanteDto {
  fechaRegistro: string;          // ISO
  loteId: string;
  /** ID de lote_postura_levante. Solo aplica para seguimiento tipo levante. */
  lotePosturaLevanteId?: number | null;

  mortalidadHembras: number;
  mortalidadMachos: number;
  selH: number;
  selM: number;
  errorSexajeHembras: number;
  errorSexajeMachos: number;

  tipoAlimento: string;
  
  // NUEVO: Arrays de ítems para permitir múltiples ítems por género
  // NOTA: Los alimentos se procesan y van a campos tradicionales,
  // los otros ítems van a itemsAdicionales JSONB
  itemsHembras?: ItemSeguimientoDto[] | null;
  itemsMachos?: ItemSeguimientoDto[] | null;
  
  // Items adicionales JSONB (solo para ítems que NO son alimentos)
  // Se calcula automáticamente desde itemsHembras/itemsMachos filtrando los no-alimentos
  itemsAdicionales?: {
    itemsHembras?: ItemSeguimientoDto[];
    itemsMachos?: ItemSeguimientoDto[];
  } | null;
  
  // DEPRECATED: Mantener para compatibilidad hacia atrás
  consumoHembras?: number | null;
  unidadConsumoHembras?: string; // "kg" o "g" - default "kg"
  consumoMachos?: number | null;
  unidadConsumoMachos?: string; // "kg" o "g" - default "kg"
  
  // Mantener para compatibilidad (deprecated - usar consumoHembras/Machos con unidad)
  consumoKgHembras?: number;
  consumoKgMachos?: number | null;

  pesoPromH?: number | null;
  pesoPromM?: number | null;
  uniformidadH?: number | null;
  uniformidadM?: number | null;
  cvH?: number | null;
  cvM?: number | null;

  observaciones?: string;
  kcalAlH?: number | null;
  protAlH?: number | null;
  kcalAveH?: number | null;
  protAveH?: number | null;
  ciclo: string;
  tipoAlimentoHembras?: number | null;
  tipoAlimentoMachos?: number | null;
  // Tipo de ítem (alimento, medicamento, etc.) - se guarda en Metadata (DEPRECATED)
  tipoItemHembras?: string | null;
  tipoItemMachos?: string | null;
  // Cantidad de unidades (para tipos de ítem que no sean alimento) (DEPRECATED)
  cantidadUnidadesHembras?: number | null;
  cantidadUnidadesMachos?: number | null;
  // Campos de agua (solo para Ecuador y Panamá)
  consumoAguaDiario?: number | null;
  consumoAguaPh?: number | null;
  consumoAguaOrp?: number | null;
  consumoAguaTemperatura?: number | null;

  /** ID del usuario en sesión (desde storage). Se envía al backend para guardar en seguimiento_diario.created_by_user_id. */
  createdByUserId?: string | null;
  /** Tipo de seguimiento: siempre "levante" para este módulo. */
  tipoSeguimiento?: 'levante' | null;
}

export interface UpdateSeguimientoLoteLevanteDto extends CreateSeguimientoLoteLevanteDto {
  id: number;
}

export interface ResultadoLevanteItemDto {
  fecha: string;            // "2025-09-08T00:00:00"
  edadDias: number | null;  // Cambiado de edadSemana a edadDias
  edadSemana?: number | null; // @deprecated - mantener para compatibilidad

  hembraViva: number | null;
  mortH: number; selH: number; errH: number;
  consKgH: number | null; pesoH: number | null; unifH: number | null; cvH: number | null;
  mortHPct: number | null; selHPct: number | null; errHPct: number | null;
  msEhH: number | null; acMortH: number | null; acSelH: number | null; acErrH: number | null;
  acConsKgH: number | null; consAcGrH: number | null; grAveDiaH: number | null;
  difConsHPct: number | null; difPesoHPct: number | null; retiroHPct: number | null; retiroHAcPct: number | null;

  machoVivo: number | null;
  mortM: number; selM: number; errM: number;
  consKgM: number | null; pesoM: number | null; unifM: number | null; cvM: number | null;
  mortMPct: number | null; selMPct: number | null; errMPct: number | null;
  msEmM: number | null; acMortM: number | null; acSelM: number | null; acErrM: number | null;
  acConsKgM: number | null; consAcGrM: number | null; grAveDiaM: number | null;
  difConsMPct: number | null; difPesoMPct: number | null; retiroMPct: number | null; retiroMAcPct: number | null;

  relMHPct: number | null;

  pesoHGuia: number | null; unifHGuia: number | null; consAcGrHGuia: number | null; grAveDiaHGuia: number | null; mortHPctGuia: number | null;
  pesoMGuia: number | null; unifMGuia: number | null; consAcGrMGuia: number | null; grAveDiaMGuia: number | null; mortMPctGuia: number | null;
  alimentoHGuia: string | null; alimentoMGuia: string | null;
}

export interface ResultadoLevanteResponse {
  loteId: string;
  desde: string | null;
  hasta: string | null;
  total: number;
  items: ResultadoLevanteItemDto[];
}

@Injectable({ providedIn: 'root' })
export class SeguimientoLoteLevanteService {
  /** Nota: environment.apiUrl debe incluir `/api` (ej: http://localhost:5002/api) */
  private readonly baseUrl = `${environment.apiUrl}/SeguimientoLoteLevante`;

  constructor(private http: HttpClient) {}

  /** GET general usando el endpoint de filtro sin parámetros */
  getAll(): Observable<SeguimientoLoteLevanteDto[]> {
    return this.http.get<SeguimientoLoteLevanteDto[]>(`${this.baseUrl}/filtro`);
  }

  /**
   * Obtener un registro por ID.
   */
  getById(id: number): Observable<SeguimientoLoteLevanteDto> {
    return this.http.get<SeguimientoLoteLevanteDto>(`${this.baseUrl}/${id}`);
  }

  /** GET por LoteId */
  getByLoteId(loteId: number): Observable<SeguimientoLoteLevanteDto[]> {  // Changed from string to number
    return this.http.get<SeguimientoLoteLevanteDto[]>(
      `${this.baseUrl}/por-lote/${encodeURIComponent(loteId.toString())}`  // Convert to string for URL
    );
  }

  /** Filtro (loteId, desde, hasta) en ISO */
  filter(params: { loteId?: string; desde?: string | Date; hasta?: string | Date }): Observable<SeguimientoLoteLevanteDto[]> {
    let hp = new HttpParams();
    if (params.loteId) hp = hp.set('loteId', params.loteId);
    if (params.desde)  hp = hp.set('desde', this.toIso(params.desde));
    if (params.hasta)  hp = hp.set('hasta', this.toIso(params.hasta));
    return this.http.get<SeguimientoLoteLevanteDto[]>(`${this.baseUrl}/filtro`, { params: hp });
  }

  /** Crear */
  create(dto: CreateSeguimientoLoteLevanteDto): Observable<SeguimientoLoteLevanteDto> {
    return this.http.post<SeguimientoLoteLevanteDto>(this.baseUrl, dto);
  }

  /** Actualizar */
  update(dto: UpdateSeguimientoLoteLevanteDto): Observable<SeguimientoLoteLevanteDto> {
    return this.http.put<SeguimientoLoteLevanteDto>(`${this.baseUrl}/${dto.id}`, dto);
  }

  /** Eliminar */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Helpers
  private toIso(d: string | Date): string {
    const dd = typeof d === 'string' ? new Date(d) : d;
    return dd.toISOString();
    // Si el back requiriera fecha sin hora: return dd.toISOString().substring(0, 10);
  }

  getResultado(params: {
    loteId: number;  // Changed from string to number
    desde?: string | Date;
    hasta?: string | Date;
    recalcular?: boolean;     // default true
  }): Observable<ResultadoLevanteResponse> {
    const { loteId } = params;
    let hp = new HttpParams()
      .set('recalcular', String(params.recalcular ?? true));
    if (params.desde) hp = hp.set('desde', this.toIso(params.desde));
    if (params.hasta) hp = hp.set('hasta', this.toIso(params.hasta));
    const url = `${this.baseUrl}/por-lote/${encodeURIComponent(loteId)}/resultado`;
    return this.http.get<ResultadoLevanteResponse>(url, { params: hp });
  }

 
}

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface LotePosturaProduccionDto {
  lotePosturaProduccionId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId?: string | null;
  galponId?: string | null;
  regional?: string | null;
  fechaEncaset?: string | null;
  hembrasL?: number | null;
  machosL?: number | null;
  pesoInicialH?: number | null;
  pesoInicialM?: number | null;
  raza?: string | null;
  linea?: string | null;
  tecnico?: string | null;
  avesEncasetadas?: number | null;
  edadInicial?: number | null;
  loteErp?: string | null;
  paisId?: number | null;
  paisNombre?: string | null;
  empresaNombre?: string | null;
  companyId: number;
  createdAt?: string | null;
  fechaInicioProduccion?: string | null;
  hembrasInicialesProd?: number | null;
  machosInicialesProd?: number | null;
  lotePosturaLevanteId?: number | null;
  /** Feature 14 — referencia al Lote base (tabla lotes), necesario para resumen-mortalidad. */
  loteId?: number | null;
  avesHInicial?: number | null;
  avesMInicial?: number | null;
  avesHActual?: number | null;
  avesMActual?: number | null;
  /** Acumulados de traslado en FASE PRODUCCIÓN (Feature 14). */
  produccionTrasladoIngresoHembras?: number | null;
  produccionTrasladoIngresoMachos?: number | null;
  produccionTrasladoSalidaHembras?: number | null;
  produccionTrasladoSalidaMachos?: number | null;
  estado?: string | null;
  etapa?: string | null;
  edad?: number | null;
  /** Abierta = producción activa. Cerrada = producción finalizada. */
  estadoCierre?: string | null;
  farm?: {
    id: number;
    name: string;
  } | null;
  nucleo?: {
    nucleoId: string;
    nucleoNombre?: string | null;
    granjaId?: number | null;
  } | null;
  galpon?: {
    galponId: string;
    galponNombre?: string | null;
    nucleoId?: string | null;
    granjaId?: number | null;
  } | null;
}

/**
 * Resumen para el modal «Cerrar/Abrir lote» de Seguimiento Diario de Producción.
 *
 * Cerrar no borra nada: bloquea el alta, la edición y el borrado de seguimiento diario del lote
 * para que nadie toque un ciclo ya liquidado. Es reversible.
 */
export interface CierreLoteProduccionResumenDto {
  lotePosturaProduccionId: number;
  loteNombre: string;
  estaCerrado: boolean;
  avesHembrasActuales: number;
  avesMachosActuales: number;
  registrosSeguimiento: number;
  primerRegistro?: string | null;
  ultimoRegistro?: string | null;
  fechaInicioProduccion?: string | null;
  /** Motivo y fecha del último cambio de estado (null si nunca se cerró). */
  ultimoMotivo?: string | null;
  ultimoCambioEstado?: string | null;
}

@Injectable({ providedIn: 'root' })
export class LotePosturaProduccionService {
  private readonly baseUrl = `${environment.apiUrl}/LotePosturaProduccion`;
  private readonly http = inject(HttpClient);

  /** paraDestino=true: catálogo para elegir DESTINO de traslados (omite el alcance granular del usuario). */
  getAll(paraDestino = false): Observable<LotePosturaProduccionDto[]> {
    const qs = paraDestino ? '?paraDestino=true' : '';
    return this.http.get<LotePosturaProduccionDto[]>(`${this.baseUrl}${qs}`);
  }

  getByLoteId(loteId: number): Observable<LotePosturaProduccionDto[]> {
    return this.http.get<LotePosturaProduccionDto[]>(`${this.baseUrl}/por-lote/${loteId}`);
  }

  /** Feature 14 — Obtiene un LPP completo (con aves actuales y acumulados de traslado). */
  getById(id: number): Observable<LotePosturaProduccionDto> {
    return this.http.get<LotePosturaProduccionDto>(`${this.baseUrl}/${id}`);
  }

  /** Resumen para el modal «Cerrar/Abrir lote» de Seguimiento Diario de Producción. */
  getResumenCierre(lotePosturaProduccionId: number): Observable<CierreLoteProduccionResumenDto> {
    return this.http.get<CierreLoteProduccionResumenDto>(
      `${this.baseUrl}/${encodeURIComponent(String(lotePosturaProduccionId))}/resumen-cierre`
    );
  }

  /**
   * Cierra el lote de producción: bloquea crear, editar y eliminar seguimiento diario.
   * No borra ni modifica registros — es reversible con {@link abrirLote}.
   */
  cerrarLote(
    lotePosturaProduccionId: number,
    body: { motivo: string; closedByUserId: string }
  ): Observable<LotePosturaProduccionDto> {
    return this.http.post<LotePosturaProduccionDto>(
      `${this.baseUrl}/${encodeURIComponent(String(lotePosturaProduccionId))}/cerrar`,
      body
    );
  }

  /** Reabre un lote de producción cerrado y vuelve a habilitar la captura diaria. */
  abrirLote(
    lotePosturaProduccionId: number,
    body: { motivo: string; openedByUserId: string }
  ): Observable<LotePosturaProduccionDto> {
    return this.http.post<LotePosturaProduccionDto>(
      `${this.baseUrl}/${encodeURIComponent(String(lotePosturaProduccionId))}/abrir`,
      body
    );
  }
}

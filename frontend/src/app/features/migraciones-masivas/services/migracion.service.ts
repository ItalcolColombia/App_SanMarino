// features/migraciones-masivas/services/migracion.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  TipoMigracionInfo,
  TipoMigracionCodigo,
  MigracionResult,
  MigracionError,
  LoteElegible,
  MigracionHistorialPaged,
  MigracionContexto
} from '../models/migracion.model';

/** Filtros/paginación de `GET historial`. */
export interface HistorialOpciones {
  tipo?: string;
  page?: number;
  pageSize?: number;
  incluirValidaciones?: boolean;
}

@Injectable({ providedIn: 'root' })
export class MigracionService {
  private readonly baseUrl = `${environment.apiUrl}/Migracion`;
  private readonly http = inject(HttpClient);

  /** Catálogo de tipos de migración soportados. */
  getTipos(): Observable<TipoMigracionInfo[]> {
    return this.http.get<TipoMigracionInfo[]>(`${this.baseUrl}/tipos`);
  }

  /** Historial paginado de auditoría de la empresa activa (filtro opcional por tipo). */
  getHistorial(opts: HistorialOpciones = {}): Observable<MigracionHistorialPaged> {
    const params: Record<string, string> = {
      page: String(opts.page ?? 1),
      pageSize: String(opts.pageSize ?? 20),
      incluirValidaciones: String(opts.incluirValidaciones ?? true)
    };
    if (opts.tipo) params['tipo'] = opts.tipo;
    return this.http.get<MigracionHistorialPaged>(`${this.baseUrl}/historial`, { params });
  }

  /** Errores/advertencias guardados de una corrida puntual del historial (404 si es de otra empresa). */
  getErrores(id: number): Observable<MigracionError[]> {
    return this.http.get<MigracionError[]>(`${this.baseUrl}/historial/${id}/errores`);
  }

  /** Lotes elegibles para migración de históricos según las reglas de fase. */
  getElegibles(tipo: TipoMigracionCodigo, ctx: MigracionContexto): Observable<LoteElegible[]> {
    return this.http.get<LoteElegible[]>(`${this.baseUrl}/elegibles`, { params: this.ctxParams(tipo, ctx) });
  }

  /** Descarga la plantilla .xlsx generada por el sistema para el tipo indicado. */
  descargarPlantilla(tipo: TipoMigracionCodigo, ctx: MigracionContexto): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/plantilla`, { params: this.ctxParams(tipo, ctx), responseType: 'blob' });
  }

  /** Valida el archivo (dry-run): no inserta, devuelve el reporte de errores. */
  validar(tipo: TipoMigracionCodigo, file: File, ctx: MigracionContexto): Observable<MigracionResult> {
    return this.http.post<MigracionResult>(`${this.baseUrl}/validar`, this.buildForm(tipo, file, ctx));
  }

  /**
   * Importa el archivo: valida y, solo si no hay errores, inserta masivamente.
   * `permitirParcial` viaja en el form SOLO si es `true` (el backend por default es all-or-nothing).
   */
  importar(tipo: TipoMigracionCodigo, file: File, ctx: MigracionContexto, permitirParcial: boolean): Observable<MigracionResult> {
    const form = this.buildForm(tipo, file, ctx);
    if (permitirParcial) form.append('permitirParcial', 'true');
    return this.http.post<MigracionResult>(`${this.baseUrl}/importar`, form);
  }

  private ctxParams(tipo: string, ctx: MigracionContexto): Record<string, string> {
    const p: Record<string, string> = { tipo };
    if (ctx.granjaId != null) p['granjaId'] = String(ctx.granjaId);
    if (ctx.nucleoId) p['nucleoId'] = ctx.nucleoId;
    if (ctx.galponId) p['galponId'] = ctx.galponId;
    if (ctx.loteId != null) p['loteId'] = String(ctx.loteId);
    return p;
  }

  private buildForm(tipo: string, file: File, ctx: MigracionContexto): FormData {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('tipo', tipo);
    if (ctx.granjaId != null) form.append('granjaId', String(ctx.granjaId));
    if (ctx.nucleoId) form.append('nucleoId', ctx.nucleoId);
    if (ctx.galponId) form.append('galponId', ctx.galponId);
    if (ctx.loteId != null) form.append('loteId', String(ctx.loteId));
    return form;
  }
}

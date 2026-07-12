// features/migraciones-masivas/services/migracion.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  TipoMigracionInfo,
  TipoMigracionCodigo,
  MigracionResult,
  LoteElegible,
  MigracionHistorial,
  MigracionContexto
} from '../models/migracion.model';

@Injectable({ providedIn: 'root' })
export class MigracionService {
  private readonly baseUrl = `${environment.apiUrl}/Migracion`;
  private readonly http = inject(HttpClient);

  /** Catálogo de tipos de migración soportados. */
  getTipos(): Observable<TipoMigracionInfo[]> {
    return this.http.get<TipoMigracionInfo[]>(`${this.baseUrl}/tipos`);
  }

  /** Historial de auditoría de la empresa activa (opcionalmente por tipo). */
  getHistorial(tipo?: string): Observable<MigracionHistorial[]> {
    const options = tipo ? { params: { tipo } } : {};
    return this.http.get<MigracionHistorial[]>(`${this.baseUrl}/historial`, options);
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

  /** Importa el archivo: valida y, solo si no hay errores, inserta masivamente. */
  importar(tipo: TipoMigracionCodigo, file: File, ctx: MigracionContexto): Observable<MigracionResult> {
    return this.http.post<MigracionResult>(`${this.baseUrl}/importar`, this.buildForm(tipo, file, ctx));
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

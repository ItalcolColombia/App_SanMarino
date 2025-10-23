// app/features/lote-reproductora/services/lote-reproductora.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

/* ===========================
   Tipos base
   =========================== */
export interface FarmDto {
  readonly id: number;
  readonly name: string;
  readonly companyId: number;
  readonly regionalId: number;
  readonly granjaId: number;
  readonly status: boolean;
  readonly zoneId: number;
}

export interface NucleoDto {
  readonly nucleoId: string;
  readonly granjaId: number;
  readonly nucleoNombre: string;
}

export interface LoteDto {
  readonly loteId: string;
  readonly loteNombre: string;
  readonly granjaId: number;
  readonly nucleoId: string;
  readonly galponId: string;
  readonly fechaEncaset: string; // ISO
}

export interface LoteDtoExtendido {
  readonly loteId: string;
  readonly loteNombre: string;
  readonly granjaId: number;
  readonly nucleoId?: number;
  readonly galponId?: number;
  readonly regional?: string;
  readonly fechaEncaset?: string;
  readonly hembrasL?: number;
  readonly machosL?: number;
  readonly mixtas?: number;
  readonly avesEncasetadas?: number;
  readonly pesoInicialM?: number;
  readonly pesoInicialH?: number;
  readonly pesoMixto?: number | null;
}

export interface LoteReproductoraDto {
  readonly loteId: string;
  readonly reproductoraId: string;
  readonly nombreLote: string;
  readonly fechaEncasetamiento: string | null; // ISO o null
  readonly m: number | null;
  readonly h: number | null;
  readonly mixtas: number | null;
  readonly mortCajaH: number | null;
  readonly mortCajaM: number | null;
  readonly unifH: number | null;
  readonly unifM: number | null;
  readonly pesoInicialM: number | null;
  readonly pesoInicialH: number | null;
  readonly pesoMixto?: number | null;
}

export type CreateLoteReproductoraDto = LoteReproductoraDto;
export type UpdateLoteReproductoraDto = LoteReproductoraDto;

/* ===========================
   Búsqueda paginada (opcional)
   =========================== */
export interface LoteReproductoraSearchRequest {
  loteId?: string;
  reproductoraId?: string;
  fromDate?: string | Date;
  toDate?: string | Date;
  page?: number;
  pageSize?: number;
  orderBy?: string;
  desc?: boolean;
}

export interface PagedResult<T> {
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly items: ReadonlyArray<T>;
}

/** ASP.NET Core ProblemDetails */
export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly instance?: string;
  readonly errors?: Record<string, string[]>;
}

/* ===========================
   Service
   =========================== */
@Injectable({ providedIn: 'root' })
export class LoteReproductoraService {
  private readonly base = `${environment.apiUrl}`;
  private readonly resourceBase = `${this.base}/LoteReproductora`;

  constructor(private http: HttpClient) {}

  // ---------- Helpers ----------
  private buildParams(obj: Record<string, unknown>): HttpParams {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(obj)) {
      if (v === undefined || v === null || v === '') continue;
      if (Array.isArray(v)) {
        v.filter(x => x !== undefined && x !== null && x !== '')
         .forEach(x => params = params.append(k, this.valToString(x)));
        continue;
      }
      params = params.set(k, this.valToString(v));
    }
    return params;
  }

  private valToString(v: unknown): string {
    if (v instanceof Date) return v.toISOString();
    if (typeof v === 'string') return v;
    return String(v);
  }

  /** Acepta ISO o 'yyyy-MM-dd' y devuelve ISO/UTC o null */
  private toIsoOrNull(d?: string | Date | null): string | null {
    if (!d) return null;
    if (d instanceof Date) return d.toISOString();
    const parsed = new Date(d);
    return isNaN(parsed.getTime()) ? d : parsed.toISOString();
  }

  private handleError = (err: HttpErrorResponse) => {
    const problem: ProblemDetails | undefined = err.error;
    const message =
      problem?.detail ||
      problem?.title ||
      (typeof err.error === 'string' ? err.error : 'Error inesperado en el servidor.');
    return throwError(() => ({ status: err.status, message, problem }));
  };

  // ---------- Catálogos ----------
  getGranjas(): Observable<ReadonlyArray<FarmDto>> {
    return this.http.get<FarmDto[]>(`${this.base}/Farm`).pipe(catchError(this.handleError));
  }

  /**
   * Núcleos por granja:
   * - Envía `granjaId` y también `farmId` (por compatibilidad de API).
   * - Fallback: filtra en cliente por `granjaId`.
   */
  getNucleosPorGranja(granjaId: number): Observable<ReadonlyArray<NucleoDto>> {
    const gid = Number(granjaId);
    const params = this.buildParams({ granjaId: gid, farmId: gid });
    return this.http
      .get<NucleoDto[]>(`${this.base}/Nucleo`, { params })
      .pipe(
        map(rows => {
          const list = [...(rows ?? [])];
          return list.filter(n => Number(n.granjaId) === gid);
        }),
        catchError(this.handleError)
      );
  }

  getLotes(): Observable<ReadonlyArray<LoteDto>> {
    return this.http.get<LoteDto[]>(`${this.base}/Lote`).pipe(catchError(this.handleError));
  }

  // ---------- Lote Reproductora ----------
  getAll(): Observable<ReadonlyArray<LoteReproductoraDto>>;
  getAll(loteId: string): Observable<ReadonlyArray<LoteReproductoraDto>>;
  getAll(loteId?: string): Observable<ReadonlyArray<LoteReproductoraDto>> {
    const params = this.buildParams({ loteId });
    return this.http
      .get<LoteReproductoraDto[]>(this.resourceBase, { params })
      .pipe(catchError(this.handleError));
  }

  getByLoteId(loteId: string): Observable<ReadonlyArray<LoteReproductoraDto>> {
    return this.getAll(loteId);
  }

  getById(loteId: string, repId: string): Observable<LoteReproductoraDto> {
    return this.http
      .get<LoteReproductoraDto>(`${this.resourceBase}/${encodeURIComponent(loteId)}/${encodeURIComponent(repId)}`)
      .pipe(catchError(this.handleError));
  }

  create(dto: CreateLoteReproductoraDto): Observable<LoteReproductoraDto> {
    const payload: CreateLoteReproductoraDto = {
      ...dto,
      fechaEncasetamiento: this.toIsoOrNull(dto.fechaEncasetamiento),
    };
    return this.http.post<LoteReproductoraDto>(this.resourceBase, payload).pipe(catchError(this.handleError));
  }

  update(dto: UpdateLoteReproductoraDto): Observable<LoteReproductoraDto> {
    const payload: UpdateLoteReproductoraDto = {
      ...dto,
      fechaEncasetamiento: this.toIsoOrNull(dto.fechaEncasetamiento),
    };
    return this.http
      .put<LoteReproductoraDto>(
        `${this.resourceBase}/${encodeURIComponent(dto.loteId)}/${encodeURIComponent(dto.reproductoraId)}`,
        payload
      )
      .pipe(catchError(this.handleError));
  }

  delete(loteId: string, repId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.resourceBase}/${encodeURIComponent(loteId)}/${encodeURIComponent(repId)}`)
      .pipe(catchError(this.handleError));
  }

  // ---------- Bulk ----------
  createMany(dtos: CreateLoteReproductoraDto[]): Observable<ReadonlyArray<LoteReproductoraDto>> {
    const payload = (dtos ?? []).map(d => ({
      ...d,
      fechaEncasetamiento: this.toIsoOrNull(d.fechaEncasetamiento),
    }));
    return this.http
      .post<LoteReproductoraDto[]>(`${this.resourceBase}/bulk`, payload)
      .pipe(catchError(this.handleError));
  }

  // ---------- Search (si está expuesto en el backend) ----------
  search(req: LoteReproductoraSearchRequest): Observable<PagedResult<LoteReproductoraDto>> {
    const params = this.buildParams({
      loteId: req.loteId,
      reproductoraId: req.reproductoraId,
      fromDate: this.toIsoOrNull(req.fromDate ?? null),
      toDate: this.toIsoOrNull(req.toDate ?? null),
      page: req.page ?? 1,
      pageSize: req.pageSize ?? 50,
      orderBy: req.orderBy ?? 'FechaEncasetamiento',
      desc: req.desc ?? true,
    });

    return this.http.get<any>(`${this.resourceBase}/search`, { params }).pipe(
      map((res) => {
        if ('items' in res && 'total' in res) return res as PagedResult<LoteReproductoraDto>;
        return {
          total: res.total ?? res?.data?.length ?? 0,
          page: res.page ?? 1,
          pageSize: res.pageSize ?? (res?.data?.length ?? 0),
          items: (res.data ?? res.items ?? []) as LoteReproductoraDto[],
        } as PagedResult<LoteReproductoraDto>;
      }),
      catchError(this.handleError)
    );
  }

  // ---------- Actualiza Lote (metadatos del lote padre) ----------
  updateLote(dto: LoteDtoExtendido): Observable<unknown> {
    return this.http
      .put(`${this.base}/Lote/${encodeURIComponent(dto.loteId)}`, dto)
      .pipe(catchError(this.handleError));
  }
}

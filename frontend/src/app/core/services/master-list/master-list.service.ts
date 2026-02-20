// src/app/core/services/master-list.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { TokenStorageService } from '../../auth/token-storage.service';

/** Opción de lista maestra con id para guardar en registros (ej. granja). */
export interface MasterListOptionItemDto {
  id: number;
  value: string;
}

export interface MasterListDto {
  id: number;
  key: string;
  name: string;
  /** Opciones con id y value (para guardar en registros). */
  options: MasterListOptionItemDto[];
  /** Solo textos (options.map(o => o.value)); compatibilidad con consumidores que esperan string[]. */
  optionValues?: string[];
  companyId?: number | null;
  companyName?: string | null;
  countryId?: number | null;
  countryName?: string | null;
}

export interface CreateMasterListDto {
  key: string;
  name: string;
  options: string[];
  companyId?: number | null;
  countryId?: number | null;
}

export interface UpdateMasterListDto {
  id: number;
  key: string;
  name: string;
  options: string[];
  companyId?: number | null;
  countryId?: number | null;
}

@Injectable({ providedIn: 'root' })
export class MasterListService {
  private readonly baseUrl = `${environment.apiUrl}/MasterList`;
  private storage = inject(TokenStorageService);

  constructor(private http: HttpClient) {}

  private getCompanyAndCountryIds(): { companyId: number | null; countryId: number | null } {
    const session = this.storage.get();
    return {
      companyId: session?.activeCompanyId ?? null,
      countryId: session?.activePaisId ?? null
    };
  }

  getAll(companyId?: number, countryId?: number): Observable<MasterListDto[]> {
    const { companyId: defaultCompanyId, countryId: defaultCountryId } = this.getCompanyAndCountryIds();
    const effectiveCompanyId = companyId ?? defaultCompanyId;
    const effectiveCountryId = countryId ?? defaultCountryId;

    let params = new HttpParams();
    if (effectiveCompanyId) {
      params = params.set('companyId', effectiveCompanyId.toString());
    }
    if (effectiveCountryId) {
      params = params.set('countryId', effectiveCountryId.toString());
    }

    return this.http.get<MasterListDto[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<MasterListDto> {
    return this.http.get<MasterListDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: CreateMasterListDto): Observable<MasterListDto> {
    // Si no se proporcionan companyId o countryId, usar los de la sesión
    const { companyId: defaultCompanyId, countryId: defaultCountryId } = this.getCompanyAndCountryIds();
    const finalDto: CreateMasterListDto = {
      ...dto,
      companyId: dto.companyId ?? defaultCompanyId,
      countryId: dto.countryId ?? defaultCountryId
    };
    return this.http.post<MasterListDto>(this.baseUrl, finalDto);
  }

  /** ← NUEVO: trae por key */
  getByKey(key: string, companyId?: number, countryId?: number): Observable<MasterListDto> {
    const { companyId: defaultCompanyId, countryId: defaultCountryId } = this.getCompanyAndCountryIds();
    const effectiveCompanyId = companyId ?? defaultCompanyId;
    const effectiveCountryId = countryId ?? defaultCountryId;

    let params = new HttpParams();
    if (effectiveCompanyId) {
      params = params.set('companyId', effectiveCompanyId.toString());
    }
    if (effectiveCountryId) {
      params = params.set('countryId', effectiveCountryId.toString());
    }

    return this.http.get<MasterListDto>(`${this.baseUrl}/byKey/${key}`, { params });
  }

  update(dto: UpdateMasterListDto): Observable<MasterListDto> {
    // Si no se proporcionan companyId o countryId, usar los de la sesión
    const { companyId: defaultCompanyId, countryId: defaultCountryId } = this.getCompanyAndCountryIds();
    const finalDto: UpdateMasterListDto = {
      ...dto,
      companyId: dto.companyId ?? defaultCompanyId,
      countryId: dto.countryId ?? defaultCountryId
    };
    return this.http.put<MasterListDto>(`${this.baseUrl}/${dto.id}`, finalDto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

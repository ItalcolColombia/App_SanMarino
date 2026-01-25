import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface ProduccionAvicolaRawDto {
  id: number;
  companyId: number;
  codigoGuiaGenetica?: string;
  anioGuia?: string;
  raza?: string;
  edad?: string;

  mortSemH?: string;
  retiroAcH?: string;
  mortSemM?: string;
  retiroAcM?: string;
  hembras?: string;
  machos?: string;
  consAcH?: string;
  consAcM?: string;
  grAveDiaH?: string;
  grAveDiaM?: string;
  pesoH?: string;
  pesoM?: string;
  uniformidad?: string;
  hTotalAa?: string;
  prodPorcentaje?: string;
  hIncAa?: string;
  aprovSem?: string;
  pesoHuevo?: string;
  masaHuevo?: string;
  grasaPorcentaje?: string;
  nacimPorcentaje?: string;
  pollitoAa?: string;
  alimH?: string;
  kcalAveDiaH?: string;
  kcalAveDiaM?: string;
  kcalH?: string;
  protH?: string;
  alimM?: string;
  kcalM?: string;
  protM?: string;
  kcalSemH?: string;
  protHSem?: string;
  kcalSemM?: string;
  protSemM?: string;
  aprovAc?: string;
  grHuevoT?: string;
  grHuevoInc?: string;
  grPollito?: string;
  valor1000?: string;
  valor150?: string;
  apareo?: string;
  pesoMh?: string;

  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateProduccionAvicolaRawDto {
  codigoGuiaGenetica?: string;
  anioGuia?: string;
  raza?: string;
  edad?: string;
  mortSemH?: string;
  retiroAcH?: string;
  mortSemM?: string;
  retiroAcM?: string;
  hembras?: string;
  machos?: string;
  consAcH?: string;
  consAcM?: string;
  grAveDiaH?: string;
  grAveDiaM?: string;
  pesoH?: string;
  pesoM?: string;
  uniformidad?: string;
  hTotalAa?: string;
  prodPorcentaje?: string;
  hIncAa?: string;
  aprovSem?: string;
  pesoHuevo?: string;
  masaHuevo?: string;
  grasaPorcentaje?: string;
  nacimPorcentaje?: string;
  pollitoAa?: string;
  alimH?: string;
  kcalAveDiaH?: string;
  kcalAveDiaM?: string;
  kcalH?: string;
  protH?: string;
  alimM?: string;
  kcalM?: string;
  protM?: string;
  kcalSemH?: string;
  protHSem?: string;
  kcalSemM?: string;
  protSemM?: string;
  aprovAc?: string;
  grHuevoT?: string;
  grHuevoInc?: string;
  grPollito?: string;
  valor1000?: string;
  valor150?: string;
  apareo?: string;
  pesoMh?: string;
}

export interface UpdateProduccionAvicolaRawDto extends CreateProduccionAvicolaRawDto {
  id: number;
}

export interface ProduccionAvicolaRawSearchRequest {
  anioGuia?: string;
  raza?: string;
  edad?: string;
  companyId?: number;
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ExcelImportResultDto {
  success: boolean;
  totalRows: number;
  processedRows: number;
  errorRows: number;
  errors: string[];
  importedData: ProduccionAvicolaRawDto[];
}

export interface ProduccionAvicolaRawFilterOptionsDto {
  anioGuias: string[];
  razas: string[];
}

@Injectable({ providedIn: 'root' })
export class GuiaGeneticaAdminService {
  private readonly baseUrl = `${environment.apiUrl}/ProduccionAvicolaRaw`;
  private readonly excelUrl = `${environment.apiUrl}/ExcelImport`;

  constructor(private http: HttpClient) {}

  search(req: ProduccionAvicolaRawSearchRequest): Observable<PagedResult<ProduccionAvicolaRawDto>> {
    return this.http.post<PagedResult<ProduccionAvicolaRawDto>>(`${this.baseUrl}/search`, req);
  }

  getById(id: number): Observable<ProduccionAvicolaRawDto> {
    return this.http.get<ProduccionAvicolaRawDto>(`${this.baseUrl}/${id}`);
  }

  getFilters(): Observable<ProduccionAvicolaRawFilterOptionsDto> {
    return this.http.get<ProduccionAvicolaRawFilterOptionsDto>(`${this.baseUrl}/filters`);
  }

  create(dto: CreateProduccionAvicolaRawDto): Observable<ProduccionAvicolaRawDto> {
    return this.http.post<ProduccionAvicolaRawDto>(this.baseUrl, dto);
  }

  update(dto: UpdateProduccionAvicolaRawDto): Observable<ProduccionAvicolaRawDto> {
    return this.http.put<ProduccionAvicolaRawDto>(`${this.baseUrl}/${dto.id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  importExcel(file: File): Observable<ExcelImportResultDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ExcelImportResultDto>(`${this.excelUrl}/produccion-avicola`, form);
  }

  validateExcel(file: File): Observable<ProduccionAvicolaRawDto[]> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ProduccionAvicolaRawDto[]>(`${this.excelUrl}/validate-produccion-avicola`, form);
  }

  downloadTemplate(): Observable<Blob> {
    return this.http.get(`${this.excelUrl}/download-template`, {
      responseType: 'blob'
    });
  }
}


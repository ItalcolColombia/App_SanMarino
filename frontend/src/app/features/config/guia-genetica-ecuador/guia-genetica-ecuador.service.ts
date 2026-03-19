import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface GuiaGeneticaEcuadorFiltersDto {
  razas: string[];
  anos: number[];
}

export interface GuiaGeneticaEcuadorDetalleDto {
  sexo: string;
  dia: number;
  pesoCorporalG: number;
  gananciaDiariaG: number;
  promedioGananciaDiariaG: number;
  cantidadAlimentoDiarioG: number;
  alimentoAcumuladoG: number;
  ca: number;
  mortalidadSeleccionDiaria: number;
}

export interface GuiaGeneticaEcuadorImportResultDto {
  success: boolean;
  totalFilasProcesadas: number;
  totalDetallesInsertados: number;
  errorFilas: number;
  errors: string[];
}

export interface GuiaGeneticaEcuadorDetalleInputDto {
  dia: number;
  pesoCorporalG: number;
  gananciaDiariaG: number;
  promedioGananciaDiariaG: number;
  cantidadAlimentoDiarioG: number;
  alimentoAcumuladoG: number;
  ca: number;
  mortalidadSeleccionDiaria: number;
}

export interface GuiaGeneticaEcuadorManualRequestDto {
  raza: string;
  anioGuia: number;
  sexo: string;
  estado: string;
  items: GuiaGeneticaEcuadorDetalleInputDto[];
}

export interface GuiaGeneticaEcuadorHeaderDto {
  id: number;
  raza: string;
  anioGuia: number;
  estado: string;
}

@Injectable({ providedIn: 'root' })
export class GuiaGeneticaEcuadorService {
  private readonly base = `${environment.apiUrl}/guia-genetica-ecuador`;

  constructor(private http: HttpClient) {}

  getFilters(): Observable<GuiaGeneticaEcuadorFiltersDto> {
    return this.http.get<GuiaGeneticaEcuadorFiltersDto>(`${this.base}/filters`);
  }

  getAnosPorRaza(raza: string): Observable<number[]> {
    const p = new HttpParams().set('raza', raza);
    return this.http.get<number[]>(`${this.base}/anos`, { params: p });
  }

  getSexos(raza: string, anioGuia: number): Observable<string[]> {
    const p = new HttpParams().set('raza', raza).set('anioGuia', String(anioGuia));
    return this.http.get<string[]>(`${this.base}/sexos`, { params: p });
  }

  getDatos(raza: string, anioGuia: number, sexo: string): Observable<GuiaGeneticaEcuadorDetalleDto[]> {
    const p = new HttpParams().set('raza', raza).set('anioGuia', String(anioGuia)).set('sexo', sexo);
    return this.http.get<GuiaGeneticaEcuadorDetalleDto[]>(`${this.base}/datos`, { params: p });
  }

  importExcel(file: File, raza: string, anioGuia: number, estado: string): Observable<GuiaGeneticaEcuadorImportResultDto> {
    const fd = new FormData();
    fd.append('file', file, file.name);
    fd.append('raza', raza);
    fd.append('anioGuia', String(anioGuia));
    fd.append('estado', estado);
    return this.http.post<GuiaGeneticaEcuadorImportResultDto>(`${this.base}/import`, fd);
  }

  manual(body: GuiaGeneticaEcuadorManualRequestDto): Observable<GuiaGeneticaEcuadorHeaderDto> {
    return this.http.post<GuiaGeneticaEcuadorHeaderDto>(`${this.base}/manual`, body);
  }
}

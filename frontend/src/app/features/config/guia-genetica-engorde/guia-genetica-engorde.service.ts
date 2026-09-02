import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface GuiaGeneticaEngordeFiltersDto {
  razas: string[];
  anos: number[];
}

export interface GuiaGeneticaEngordeDetalleDto {
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

export interface GuiaGeneticaEngordeImportResultDto {
  success: boolean;
  totalFilasProcesadas: number;
  totalDetallesInsertados: number;
  errorFilas: number;
  errors: string[];
}

export interface GuiaGeneticaEngordeDetalleInputDto {
  dia: number;
  pesoCorporalG: number;
  gananciaDiariaG: number;
  promedioGananciaDiariaG: number;
  cantidadAlimentoDiarioG: number;
  alimentoAcumuladoG: number;
  ca: number;
  mortalidadSeleccionDiaria: number;
}

export interface GuiaGeneticaEngordeManualRequestDto {
  raza: string;
  anioGuia: number;
  sexo: string;
  estado: string;
  items: GuiaGeneticaEngordeDetalleInputDto[];
}

export interface GuiaGeneticaEngordeHeaderDto {
  id: number;
  raza: string;
  anioGuia: number;
  estado: string;
}

function pickNumGuia(raw: Record<string, unknown>, ...keys: string[]): number {
  for (const k of keys) {
    const v = raw[k];
    if (v != null && v !== '') {
      const n = typeof v === 'number' ? v : Number(v);
      if (Number.isFinite(n)) {
        return n;
      }
    }
  }
  return 0;
}

function pickIntGuia(raw: Record<string, unknown>, ...keys: string[]): number {
  return Math.trunc(pickNumGuia(raw, ...keys));
}

/** Acepta camelCase o PascalCase (y CA/ca) para que la tabla de indicadores siempre reciba números. */
export function normalizeGuiaGeneticaEngordeDetalleApiRow(raw: unknown): GuiaGeneticaEngordeDetalleDto {
  if (raw == null || typeof raw !== 'object') {
    return {
      sexo: 'mixto',
      dia: 0,
      pesoCorporalG: 0,
      gananciaDiariaG: 0,
      promedioGananciaDiariaG: 0,
      cantidadAlimentoDiarioG: 0,
      alimentoAcumuladoG: 0,
      ca: 0,
      mortalidadSeleccionDiaria: 0
    };
  }
  const o = raw as Record<string, unknown>;
  return {
    sexo: String(o['sexo'] ?? o['Sexo'] ?? 'mixto'),
    dia: pickIntGuia(o, 'dia', 'Dia'),
    pesoCorporalG: pickNumGuia(o, 'pesoCorporalG', 'PesoCorporalG'),
    gananciaDiariaG: pickNumGuia(o, 'gananciaDiariaG', 'GananciaDiariaG'),
    promedioGananciaDiariaG: pickNumGuia(o, 'promedioGananciaDiariaG', 'PromedioGananciaDiariaG'),
    cantidadAlimentoDiarioG: pickNumGuia(o, 'cantidadAlimentoDiarioG', 'CantidadAlimentoDiarioG'),
    alimentoAcumuladoG: pickNumGuia(o, 'alimentoAcumuladoG', 'AlimentoAcumuladoG'),
    ca: pickNumGuia(o, 'ca', 'cA', 'CA', 'Ca'),
    mortalidadSeleccionDiaria: pickNumGuia(o, 'mortalidadSeleccionDiaria', 'MortalidadSeleccionDiaria')
  };
}

@Injectable({ providedIn: 'root' })
export class GuiaGeneticaEngordeService {
  private readonly base = `${environment.apiUrl}/guia-genetica-engorde`;

  constructor(private http: HttpClient) {}

  getFilters(): Observable<GuiaGeneticaEngordeFiltersDto> {
    return this.http.get<GuiaGeneticaEngordeFiltersDto>(`${this.base}/filters`);
  }

  getAnosPorRaza(raza: string): Observable<number[]> {
    const p = new HttpParams().set('raza', raza);
    return this.http.get<number[]>(`${this.base}/anos`, { params: p });
  }

  getSexos(raza: string, anioGuia: number): Observable<string[]> {
    const p = new HttpParams().set('raza', raza).set('anioGuia', String(anioGuia));
    return this.http.get<string[]>(`${this.base}/sexos`, { params: p });
  }

  getDatos(raza: string, anioGuia: number, sexo: string): Observable<GuiaGeneticaEngordeDetalleDto[]> {
    const p = new HttpParams().set('raza', raza).set('anioGuia', String(anioGuia)).set('sexo', sexo);
    return this.http.get<unknown[]>(`${this.base}/datos`, { params: p }).pipe(
      map(rows => (rows ?? []).map(r => normalizeGuiaGeneticaEngordeDetalleApiRow(r)))
    );
  }

  importExcel(file: File, raza: string, anioGuia: number, estado: string): Observable<GuiaGeneticaEngordeImportResultDto> {
    const fd = new FormData();
    fd.append('file', file, file.name);
    fd.append('raza', raza);
    fd.append('anioGuia', String(anioGuia));
    fd.append('estado', estado);
    return this.http.post<GuiaGeneticaEngordeImportResultDto>(`${this.base}/import`, fd);
  }

  manual(body: GuiaGeneticaEngordeManualRequestDto): Observable<GuiaGeneticaEngordeHeaderDto> {
    return this.http.post<GuiaGeneticaEngordeHeaderDto>(`${this.base}/manual`, body);
  }
}

import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, timeout } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CumplirTareaCampoRequest,
  GestionVeterinariaInicioDto,
  TareaCampoDto,
  TareaCampoRequest,
  VeterinariaGranjaDto,
  VeterinariaResumenDto,
  VisitaTecnicaDto,
  VisitaTecnicaRequest,
} from '../models/gestion-veterinaria.models';

const TIMEOUT_MS = 30_000;

@Injectable({ providedIn: 'root' })
export class GestionVeterinariaService {
  private readonly base = `${environment.apiUrl}/GestionVeterinaria`;
  constructor(private http: HttpClient) {}

  private conTimeout<T>(source: Observable<T>): Observable<T> {
    return source.pipe(timeout({ first: TIMEOUT_MS }));
  }

  getMiMapa(): Observable<VeterinariaGranjaDto[]> {
    return this.conTimeout(this.http.get<VeterinariaGranjaDto[]>(`${this.base}/mi-mapa`));
  }
  getResumen(): Observable<VeterinariaResumenDto> {
    return this.conTimeout(this.http.get<VeterinariaResumenDto>(`${this.base}/resumen`));
  }
  getVisitas(): Observable<VisitaTecnicaDto[]> {
    return this.conTimeout(this.http.get<VisitaTecnicaDto[]>(`${this.base}/visitas`));
  }
  createVisita(req: VisitaTecnicaRequest): Observable<VisitaTecnicaDto> {
    return this.conTimeout(this.http.post<VisitaTecnicaDto>(`${this.base}/visitas`, req));
  }
  updateVisita(id: number, req: VisitaTecnicaRequest): Observable<VisitaTecnicaDto> {
    return this.conTimeout(this.http.put<VisitaTecnicaDto>(`${this.base}/visitas/${id}`, req));
  }
  realizarVisita(id: number, observaciones: string | null): Observable<VisitaTecnicaDto> {
    return this.conTimeout(this.http.post<VisitaTecnicaDto>(`${this.base}/visitas/${id}/realizar`, { observaciones }));
  }
  cancelarVisita(id: number): Observable<VisitaTecnicaDto> {
    return this.conTimeout(this.http.post<VisitaTecnicaDto>(`${this.base}/visitas/${id}/cancelar`, {}));
  }
  getTareasCreadas(): Observable<TareaCampoDto[]> {
    return this.conTimeout(this.http.get<TareaCampoDto[]>(`${this.base}/tareas`));
  }
  getMisTareas(incluirCerradas = false): Observable<TareaCampoDto[]> {
    return this.conTimeout(this.http.get<TareaCampoDto[]>(`${this.base}/mis-tareas`, {
      params: { incluirCerradas },
    }));
  }
  getInicio(): Observable<GestionVeterinariaInicioDto> {
    return this.conTimeout(this.http.get<GestionVeterinariaInicioDto>(`${this.base}/mis-tareas/inicio`));
  }
  getTarea(id: number): Observable<TareaCampoDto> {
    return this.conTimeout(this.http.get<TareaCampoDto>(`${this.base}/tareas/${id}`));
  }
  createTarea(req: TareaCampoRequest): Observable<TareaCampoDto> {
    return this.conTimeout(this.http.post<TareaCampoDto>(`${this.base}/tareas`, req));
  }
  updateTarea(id: number, req: TareaCampoRequest): Observable<TareaCampoDto> {
    return this.conTimeout(this.http.put<TareaCampoDto>(`${this.base}/tareas/${id}`, req));
  }
  cumplirTarea(id: number, req: CumplirTareaCampoRequest): Observable<TareaCampoDto> {
    return this.conTimeout(this.http.post<TareaCampoDto>(`${this.base}/tareas/${id}/cumplir`, req));
  }
  reabrirTarea(id: number): Observable<TareaCampoDto> {
    return this.conTimeout(this.http.post<TareaCampoDto>(`${this.base}/tareas/${id}/reabrir`, {}));
  }
  cancelarTarea(id: number): Observable<TareaCampoDto> {
    return this.conTimeout(this.http.post<TareaCampoDto>(`${this.base}/tareas/${id}/cancelar`, {}));
  }
}

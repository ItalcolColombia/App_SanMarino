// src/app/features/config/guia-genetica-santa-reyes/guia-genetica-santa-reyes.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CreateGuiaGeneticaSantaReyesDto,
  GuiaGeneticaSantaReyesDto,
  GuiaGeneticaSantaReyesFiltros,
  GuiaGeneticaSantaReyesImportResultDto,
  PagedResultGuia,
  UpdateGuiaGeneticaSantaReyesDto
} from './models/guia-genetica-santa-reyes.model';

/**
 * Cliente HTTP de `api/guia-genetica-santa-reyes` (guía genética **reducida**).
 *
 * 🔴 **No reutiliza `GuiaGeneticaAdminService` a propósito**: aquél pega a
 * `ProduccionAvicolaRaw` / `ExcelImport`, que son la tabla ANCHA compartida
 * (`guia_genetica_sanmarino_colombia`). Son dos tablas distintas con dos modelos distintos; que
 * las dos pantallas se parezcan no las hace la misma pantalla.
 *
 * La empresa no viaja en la URL: la resuelve el backend desde la sesión (`ActiveCompanyMiddleware`
 * + `GetEffectiveCompanyIdAsync`). Mandarla desde el front sería el header crudo que el repo
 * prohíbe.
 *
 * **Escritura vs lectura:** listar, ver y descargar la plantilla están abiertos; crear, editar,
 * dar de baja e importar exigen el permiso `guia_genetica.gestionar` **y** que la empresa activa
 * tenga perfil de guía `reducida` — si no, el backend responde **403 con cuerpo**
 * (`{ message, error }`), que es lo que la pantalla muestra en el toast.
 */
@Injectable({ providedIn: 'root' })
export class GuiaGeneticaSantaReyesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/guia-genetica-santa-reyes`;

  /**
   * Listado paginado. Es un `GET` con query string (no un `POST /search`), así que sólo se mandan
   * los parámetros que realmente tienen valor: un `raza=` vacío en la URL ensucia la caché del
   * navegador sin filtrar nada.
   */
  search(filtros: GuiaGeneticaSantaReyesFiltros): Observable<PagedResultGuia<GuiaGeneticaSantaReyesDto>> {
    let params = new HttpParams()
      .set('page', String(filtros.page))
      .set('pageSize', String(filtros.pageSize));

    if (filtros.raza?.trim()) params = params.set('raza', filtros.raza.trim());
    if (filtros.anioGuia?.trim()) params = params.set('anioGuia', filtros.anioGuia.trim());
    if (filtros.edadDesde != null) params = params.set('edadDesde', String(filtros.edadDesde));
    if (filtros.edadHasta != null) params = params.set('edadHasta', String(filtros.edadHasta));
    if (filtros.sortBy) params = params.set('sortBy', filtros.sortBy);
    if (filtros.sortDesc) params = params.set('sortDesc', 'true');

    return this.http.get<PagedResultGuia<GuiaGeneticaSantaReyesDto>>(this.baseUrl, { params });
  }

  getById(id: number): Observable<GuiaGeneticaSantaReyesDto> {
    return this.http.get<GuiaGeneticaSantaReyesDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: CreateGuiaGeneticaSantaReyesDto): Observable<GuiaGeneticaSantaReyesDto> {
    return this.http.post<GuiaGeneticaSantaReyesDto>(this.baseUrl, dto);
  }

  update(dto: UpdateGuiaGeneticaSantaReyesDto): Observable<GuiaGeneticaSantaReyesDto> {
    return this.http.put<GuiaGeneticaSantaReyesDto>(`${this.baseUrl}/${dto.id}`, dto);
  }

  /** Baja **suave** en el backend (`deleted_at`): la línea deja de listarse pero no se pierde. */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Import idempotente. El campo del `FormData` se llama `file` (el `IFormFile file` del controller). */
  importExcel(file: File): Observable<GuiaGeneticaSantaReyesImportResultDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<GuiaGeneticaSantaReyesImportResultDto>(`${this.baseUrl}/import`, form);
  }

  /** Plantilla del import (6 columnas + 2 filas de ejemplo). Es una LECTURA: no pide permiso. */
  downloadTemplate(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/plantilla`, { responseType: 'blob' });
  }
}

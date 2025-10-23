import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

// ==================== INTERFACES ====================

export interface ProduccionAvicolaRawDto {
  id: number;
  companyId: number;
  anioGuia?: string;
  raza?: string;
  edad?: string;
  
  // Mortalidad y retiro
  mortSemH?: string;      // % Mortalidad semanal hembras
  retiroAcH?: string;     // Retiro acumulado hembras
  mortSemM?: string;      // % Mortalidad semanal machos
  retiroAcM?: string;     // Retiro acumulado machos
  
  // Consumo
  consAcH?: string;       // Consumo acumulado hembras
  consAcM?: string;       // Consumo acumulado machos
  
  // Ganancia diaria
  grAveDiaH?: string;     // Gramos ave/día hembras
  grAveDiaM?: string;     // Gramos ave/día machos
  
  // Peso
  pesoH?: string;         // Peso hembras
  pesoM?: string;         // Peso machos
  
  // Uniformidad
  uniformidad?: string;   // % Uniformidad
  
  // Producción (reproductoras)
  hTotalAa?: string;      // Huevos total ave alojada
  prodPorcentaje?: string; // % Producción
  hIncAa?: string;        // Huevos incubables ave alojada
  aprovSem?: string;      // % Aprovechamiento semanal
  pesoHuevo?: string;     // Peso huevo
  masaHuevo?: string;     // Masa huevo
  grasaPorcentaje?: string; // % Grasa
  nacimPorcentaje?: string; // % Nacimiento
  pollitoAa?: string;     // Pollitos ave alojada
  
  // Consumo energético
  kcalAveDiaH?: string;   // Kcal ave/día hembras
  kcalAveDiaM?: string;   // Kcal ave/día machos
  
  // Aprovechamiento
  aprovAc?: string;       // % Aprovechamiento acumulado
  
  // Pesos específicos
  grHuevoT?: string;      // Gramos/huevo total
  grHuevoInc?: string;    // Gramos/huevo incubable
  grPollito?: string;     // Gramos/pollito
  
  // Valores comerciales
  valor1000?: string;     // Valor 1000
  valor150?: string;      // Valor 150
  
  // Apareamiento
  apareo?: string;        // % Apareo
  pesoMh?: string;        // Peso M/H
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

// Interfaz para opciones de raza y línea genética
export interface RazaOption {
  raza: string;
  aniosDisponibles: string[];
}

export interface LineaGeneticaOption {
  id: number;
  raza: string;
  anioGuia: string;
  descripcion: string; // Combinación de raza + año para mostrar
}

@Injectable({
  providedIn: 'root'
})
export class GuiaGeneticaService {
  private baseUrl = `${environment.apiUrl}/guia-genetica`;
  private useMock = false; // Cambiar a false cuando la API esté disponible

  constructor(
    private http: HttpClient
  ) {}

  // =====================================================
  // MÉTODOS PARA LOTES - RAZAS Y LÍNEAS GENÉTICAS
  // =====================================================

  /**
   * Obtener todas las razas disponibles
   */
  getRazasDisponibles(): Observable<string[]> {
    console.log('=== GuiaGeneticaService.getRazasDisponibles() ===');
    console.log('URL:', `${this.baseUrl}/razas`);
    
    return this.http.get<string[]>(`${this.baseUrl}/razas`).pipe(
      tap(razas => {
        console.log('✅ Razas recibidas del backend:', razas);
      }),
      catchError(error => {
        console.error('❌ Error obteniendo razas:', error);
        return this.handleError(error);
      })
    );
  }

  /**
   * Obtener años disponibles para una raza específica
   */
  getAniosPorRaza(raza: string): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/anos?raza=${encodeURIComponent(raza)}`).pipe(
      catchError(this.handleError)
    );
  }

  /**
   * Obtener información completa de una raza (incluyendo años disponibles)
   */
  obtenerInformacionRaza(raza: string): Observable<{esValida: boolean, anosDisponibles: number[]}> {
    return this.http.get<{esValida: boolean, anosDisponibles: number[]}>(`${this.baseUrl}/info-raza?raza=${encodeURIComponent(raza)}`).pipe(
      catchError(this.handleError)
    );
  }

  /**
   * Obtener líneas genéticas disponibles para una raza
   */
  getLineasGeneticasPorRaza(raza: string): Observable<LineaGeneticaOption[]> {
    const request: ProduccionAvicolaRawSearchRequest = {
      raza: raza,
      page: 1,
      pageSize: 1000,
      sortBy: 'anioGuia',
      sortDesc: true
    };

    return this.search(request).pipe(
      map(result => {
        const lineasMap = new Map<string, LineaGeneticaOption>();
        
        result.items.forEach(item => {
          if (item.raza && item.anioGuia) {
            const key = `${item.raza}-${item.anioGuia}`;
            if (!lineasMap.has(key)) {
              lineasMap.set(key, {
                id: item.id,
                raza: item.raza,
                anioGuia: item.anioGuia,
                descripcion: `${item.raza} - ${item.anioGuia}`
              });
            }
          }
        });

        return Array.from(lineasMap.values()).sort((a, b) => 
          b.anioGuia.localeCompare(a.anioGuia) // Más reciente primero
        );
      }),
      catchError(this.handleError)
    );
  }

  /**
   * Obtener datos completos de una línea genética específica
   */
  getDatosLineaGenetica(raza: string, anioGuia: string): Observable<ProduccionAvicolaRawDto[]> {
    const request: ProduccionAvicolaRawSearchRequest = {
      raza: raza,
      anioGuia: anioGuia,
      page: 1,
      pageSize: 1000,
      sortBy: 'edad',
      sortDesc: false
    };

    return this.search(request).pipe(
      map(result => result.items.sort((a, b) => {
        const edadA = parseInt(a.edad || '0');
        const edadB = parseInt(b.edad || '0');
        return edadA - edadB;
      })),
      catchError(this.handleError)
    );
  }

  // =====================================================
  // CRUD BÁSICO
  // =====================================================

  /**
   * Búsqueda con filtros
   */
  search(request: ProduccionAvicolaRawSearchRequest): Observable<PagedResult<ProduccionAvicolaRawDto>> {
    return this.http.post<PagedResult<ProduccionAvicolaRawDto>>(`${this.baseUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  /**
   * Obtener todos los registros
   */
  getAll(): Observable<ProduccionAvicolaRawDto[]> {
    return this.http.get<ProduccionAvicolaRawDto[]>(this.baseUrl)
      .pipe(catchError(this.handleError));
  }

  /**
   * Obtener por ID
   */
  getById(id: number): Observable<ProduccionAvicolaRawDto> {
    return this.http.get<ProduccionAvicolaRawDto>(`${this.baseUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // MANEJO DE ERRORES
  // =====================================================

  private handleError(error: HttpErrorResponse): Observable<never> {
    let errorMessage = 'Error desconocido';
    
    if (error.error instanceof ErrorEvent) {
      errorMessage = `Error: ${error.error.message}`;
    } else {
      switch (error.status) {
        case 400:
          errorMessage = 'Datos inválidos en la solicitud';
          break;
        case 401:
          errorMessage = 'No autorizado. Inicie sesión nuevamente';
          break;
        case 404:
          errorMessage = 'Datos de guía genética no encontrados';
          break;
        case 500:
          errorMessage = 'Error interno del servidor';
          break;
        default:
          errorMessage = `Error ${error.status}: ${error.message}`;
      }
    }
    
    console.error('Error en GuiaGeneticaService:', error);
    return throwError(() => new Error(errorMessage));
  }
}

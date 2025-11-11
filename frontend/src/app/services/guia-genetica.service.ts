import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface GuiaGeneticaDto {
  edad: number;
  consumoHembras: number;      // GrAveDiaH - Gramos por ave por día hembras
  consumoMachos: number;       // GrAveDiaM - Gramos por ave por día machos
  pesoHembras: number;         // PesoH - Peso esperado hembras (gramos)
  pesoMachos: number;          // PesoM - Peso esperado machos (gramos)
  mortalidadHembras: number;   // MortSemH - Mortalidad semanal hembras (%)
  mortalidadMachos: number;    // MortSemM - Mortalidad semanal machos (%)
  retiroAcumuladoHembras: number;  // RetiroAcH - Retiro acumulado hembras (%)
  retiroAcumuladoMachos: number;   // RetiroAcM - Retiro acumulado machos (%)
  uniformidad: number;         // Uniformidad - Único valor no separado por sexo (%)
  pisoTermicoRequerido: boolean; // Si requiere piso térmico
  observaciones?: string;      // Observaciones adicionales
}

export interface GuiaGeneticaRequest {
  raza: string;
  anoTabla: number;
  edad: number;
}

export interface GuiaGeneticaResponse {
  existe: boolean;
  datos?: GuiaGeneticaDto;
  mensaje?: string;
}

@Injectable({
  providedIn: 'root'
})
export class GuiaGeneticaService {
  private readonly apiUrl = `${environment.apiUrl}/guia-genetica`;

  constructor(private http: HttpClient) {}

  /**
   * Obtiene datos de guía genética para una edad específica
   */
  obtenerGuiaGenetica(raza: string, anoTabla: number, edad: number): Observable<GuiaGeneticaResponse> {
    const params = new HttpParams()
      .set('raza', raza)
      .set('anoTabla', anoTabla.toString())
      .set('edad', edad.toString());

    return this.http.get<GuiaGeneticaResponse>(`${this.apiUrl}/obtener`, { params });
  }

  /**
   * Obtiene datos de guía genética para un rango de edades
   */
  obtenerGuiaGeneticaRango(raza: string, anoTabla: number, edadDesde: number, edadHasta: number): Observable<GuiaGeneticaDto[]> {
    const params = new HttpParams()
      .set('raza', raza)
      .set('anoTabla', anoTabla.toString())
      .set('edadDesde', edadDesde.toString())
      .set('edadHasta', edadHasta.toString());

    return this.http.get<GuiaGeneticaDto[]>(`${this.apiUrl}/rango`, { params });
  }

  /**
   * Verifica si existe una guía genética
   */
  existeGuiaGenetica(raza: string, anoTabla: number): Observable<boolean> {
    const params = new HttpParams()
      .set('raza', raza)
      .set('anoTabla', anoTabla.toString());

    return this.http.get<boolean>(`${this.apiUrl}/existe`, { params });
  }

  /**
   * Obtiene las razas disponibles en las guías genéticas
   * Consume directamente desde la base de datos produccion_avicola_raw
   */
  obtenerRazasDisponibles(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/razas`).pipe(
      map(razas => {
        // Filtrar solo las razas válidas de la guía genética
        const razasValidas = razas.filter(raza => {
          const razaTrimmed = raza?.trim();
          // Solo incluir razas que parecen ser códigos de guía genética válidos
          return razaTrimmed &&
                 razaTrimmed !== '' &&
                 razaTrimmed !== 'AP' && // Excluir datos hardcodeados
                 razaTrimmed !== 'C500' && // Excluir datos hardcodeados
                 razaTrimmed.length >= 2; // Al menos 2 caracteres
        });

        return razasValidas
          .map(raza => raza.trim())
          .sort((a, b) => a.localeCompare(b));
      }),
      catchError(error => {
        console.error('Error obteniendo razas disponibles:', error);
        return of([]); // Retornar array vacío en caso de error
      })
    );
  }

  /**
   * Obtiene los años disponibles para una raza específica
   * Consume directamente desde la base de datos produccion_avicola_raw
   */
  obtenerAnosDisponibles(raza: string): Observable<number[]> {
    if (!raza || raza.trim() === '') {
      return of([]);
    }

    const params = new HttpParams().set('raza', raza.trim());
    return this.http.get<number[]>(`${this.apiUrl}/anos`, { params }).pipe(
      map(anos => {
        // Filtrar años válidos y ordenar descendente (más recientes primero)
        return anos
          .filter(ano => ano && ano > 2000 && ano <= new Date().getFullYear() + 1)
          .sort((a, b) => b - a);
      }),
      catchError(error => {
        console.error('Error obteniendo años disponibles:', error);
        return of([]); // Retornar array vacío en caso de error
      })
    );
  }

  /**
   * Valida si una combinación de raza y año es válida
   */
  validarCombinacionRazaAno(raza: string, anoTabla: number): Observable<boolean> {
    if (!raza || raza.trim() === '' || !anoTabla || anoTabla <= 0) {
      return of(false);
    }

    return this.existeGuiaGenetica(raza.trim(), anoTabla);
  }

  /**
   * Obtiene información completa de una raza (años disponibles y validación)
   */
  obtenerInformacionRaza(raza: string): Observable<{
    raza: string;
    anosDisponibles: number[];
    esValida: boolean;
  }> {
    if (!raza || raza.trim() === '') {
      return of({
        raza: '',
        anosDisponibles: [],
        esValida: false
      });
    }

    return this.obtenerAnosDisponibles(raza).pipe(
      map(anos => ({
        raza: raza.trim(),
        anosDisponibles: anos,
        esValida: anos.length > 0
      })),
      catchError(error => {
        console.error('Error obteniendo información de raza:', error);
        return of({
          raza: raza.trim(),
          anosDisponibles: [],
          esValida: false
        });
      })
    );
  }

  /**
   * Método de conveniencia para obtener consumo esperado
   */
  async obtenerConsumoEsperado(raza: string, anoTabla: number, edad: number): Promise<number> {
    try {
      const response = await this.obtenerGuiaGenetica(raza, anoTabla, edad).toPromise();

      if (response?.existe && response.datos) {
        // Promedio entre hembras y machos
        return (response.datos.consumoHembras + response.datos.consumoMachos) / 2;
      }

      return 0; // Valor por defecto si no existe la guía
    } catch (error) {
      console.error('Error obteniendo consumo esperado:', error);
      return 0;
    }
  }

  /**
   * Método de conveniencia para verificar si requiere piso térmico
   */
  async requierePisoTermico(raza: string, anoTabla: number, edad: number): Promise<boolean> {
    try {
      const response = await this.obtenerGuiaGenetica(raza, anoTabla, edad).toPromise();

      if (response?.existe && response.datos) {
        return response.datos.pisoTermicoRequerido;
      }

      return edad <= 3; // Valor por defecto: primeras 3 semanas
    } catch (error) {
      console.error('Error verificando piso térmico:', error);
      return edad <= 3;
    }
  }

  /**
   * Método de conveniencia para obtener peso esperado
   */
  async obtenerPesoEsperado(raza: string, anoTabla: number, edad: number): Promise<{ pesoHembras: number; pesoMachos: number }> {
    try {
      const response = await this.obtenerGuiaGenetica(raza, anoTabla, edad).toPromise();

      if (response?.existe && response.datos) {
        return {
          pesoHembras: response.datos.pesoHembras,
          pesoMachos: response.datos.pesoMachos
        };
      }

      return { pesoHembras: 0, pesoMachos: 0 };
    } catch (error) {
      console.error('Error obteniendo peso esperado:', error);
      return { pesoHembras: 0, pesoMachos: 0 };
    }
  }
}

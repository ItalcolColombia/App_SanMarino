import { Injectable } from '@angular/core';
import { Observable, of, delay } from 'rxjs';
import { LineaGeneticaOption } from './guia-genetica.service';
import { MOCK_GENETIC_DATA } from './mock-genetic-data';

@Injectable({
  providedIn: 'root'
})
export class MockGuiaGeneticaService {
  
  constructor() {}

  /**
   * Obtener todas las razas disponibles (mock)
   */
  getRazasDisponibles(): Observable<string[]> {
    return of(MOCK_GENETIC_DATA.razas).pipe(delay(500));
  }

  /**
   * Obtener años disponibles para una raza específica (mock)
   */
  getAniosPorRaza(raza: string): Observable<string[]> {
    const anios = MOCK_GENETIC_DATA.lineasGeneticas
      .filter(linea => linea.raza === raza)
      .map(linea => linea.anioGuia)
      .filter((anio, index, array) => array.indexOf(anio) === index) // Remove duplicates
      .sort((a, b) => b.localeCompare(a)); // Más reciente primero
    
    return of(anios).pipe(delay(300));
  }

  /**
   * Obtener líneas genéticas disponibles para una raza (mock)
   */
  getLineasGeneticasPorRaza(raza: string): Observable<LineaGeneticaOption[]> {
    const lineas = MOCK_GENETIC_DATA.lineasGeneticas
      .filter(linea => linea.raza === raza)
      .sort((a, b) => b.anioGuia.localeCompare(a.anioGuia)); // Más reciente primero
    
    return of(lineas).pipe(delay(300));
  }

  /**
   * Obtener datos completos de una línea genética específica (mock)
   */
  getDatosLineaGenetica(raza: string, anioGuia: string): Observable<any[]> {
    const datos = MOCK_GENETIC_DATA.produccionAvicolaRaw
      .filter(item => item.raza === raza && item.anioGuia === anioGuia)
      .sort((a, b) => parseInt(a.edad) - parseInt(b.edad));
    
    return of(datos).pipe(delay(400));
  }
}


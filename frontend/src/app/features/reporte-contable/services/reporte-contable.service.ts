// src/app/features/reporte-contable/services/reporte-contable.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface ConsumoDiarioContableDto {
  fecha: string;
  loteId: number;
  loteNombre: string;
  consumoAlimento: number;
  consumoAgua: number;
  consumoMedicamento: number;
  consumoVacuna: number;
  otrosConsumos: number;
  totalConsumo: number;
}

export interface DatoDiarioContableDto {
  fecha: string;
  loteId: number;
  loteNombre: string;
  
  // AVES
  entradasHembras: number;
  entradasMachos: number;
  mortalidadHembras: number;
  mortalidadMachos: number;
  seleccionHembras: number;
  seleccionMachos: number;
  ventasHembras: number;
  ventasMachos: number;
  trasladosHembras: number;
  trasladosMachos: number;
  saldoHembras: number;
  saldoMachos: number;
  
  // CONSUMO (Kg)
  consumoAlimentoHembras: number;
  consumoAlimentoMachos: number;
  consumoAgua: number;
  consumoMedicamento: number;
  consumoVacuna: number;
  
  // BULTO
  saldoBultosAnterior: number;
  trasladosBultos: number;
  entradasBultos: number;
  retirosBultos: number;
  consumoBultosHembras: number;
  consumoBultosMachos: number;
  saldoBultos: number;
}

export interface SeccionReporteContableDto {
  tipoSeccion: string; // "INICIO" o "LEVANTE"
  fechaInicio: string;
  fechaFin: string;
  saldoBultosAnterior: number;
  trasladosBultos: number;
  entradasBultos: number;
  consumoBultosHembras: number;
  consumoBultosMachos: number;
  saldoBultosFinal: number;
  datosDiarios: DatoDiarioContableDto[];
}

export interface ReporteContableSemanalDto {
  semanaContable: number;
  fechaInicio: string;
  fechaFin: string;
  lotePadreId: number;
  lotePadreNombre: string;
  sublotes: string[];
  
  // AVES - Saldo Semana Anterior
  saldoAnteriorHembras: number;
  saldoAnteriorMachos: number;
  
  // AVES - Entradas
  entradasHembras: number;
  entradasMachos: number;
  totalEntradas: number;
  
  // AVES - Mortalidad
  mortalidadHembrasSemanal: number;
  mortalidadMachosSemanal: number;
  mortalidadTotalSemanal: number;
  
  // AVES - Selección
  seleccionHembrasSemanal: number;
  seleccionMachosSemanal: number;
  totalSeleccionSemanal: number;
  
  // AVES - Ventas y Traslados
  ventasHembrasSemanal: number;
  ventasMachosSemanal: number;
  trasladosHembrasSemanal: number;
  trasladosMachosSemanal: number;
  totalVentasSemanal: number;
  totalTrasladosSemanal: number;
  
  // AVES - Saldo Final
  saldoFinHembras: number;
  saldoFinMachos: number;
  totalAvesVivas: number;
  
  // BULTO - Resumen Semanal
  saldoBultosAnterior: number;
  trasladosBultosSemanal: number;
  entradasBultosSemanal: number;
  retirosBultosSemanal: number;
  consumoBultosHembrasSemanal: number;
  consumoBultosMachosSemanal: number;
  saldoBultosFinal: number;
  
  // CONSUMO (Kg) - Resumen Semanal
  consumoTotalAlimento: number;
  consumoTotalAgua: number;
  consumoTotalMedicamento: number;
  consumoTotalVacuna: number;
  otrosConsumos: number;
  totalGeneral: number;
  
  // Secciones INICIO y LEVANTE
  seccionInicio?: SeccionReporteContableDto;
  seccionLevante?: SeccionReporteContableDto;
  
  // Detalle diario
  datosDiarios: DatoDiarioContableDto[];
  consumosDiarios: ConsumoDiarioContableDto[];
}

export interface ReporteContableCompletoDto {
  lotePadreId: number;
  lotePadreNombre: string;
  granjaId: number;
  granjaNombre: string;
  nucleoId: string;
  nucleoNombre: string;
  galponId?: string;
  galponNombre?: string;
  fechaPrimeraLlegada: string;
  semanaContableActual: number;
  fechaInicioSemanaActual: string;
  fechaFinSemanaActual: string;
  reportesSemanales: ReporteContableSemanalDto[];
}

export interface GenerarReporteContableRequestDto {
  lotePadreId: number;
  semanaContable?: number;
  fechaInicio?: string;
  fechaFin?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ReporteContableService {
  private readonly apiUrl = `${environment.apiUrl}/ReporteContable`;

  constructor(private http: HttpClient) {}

  /**
   * Genera el reporte contable para un lote padre
   */
  generarReporte(request: GenerarReporteContableRequestDto): Observable<ReporteContableCompletoDto> {
    let params = new HttpParams()
      .set('lotePadreId', request.lotePadreId.toString());

    if (request.semanaContable) {
      params = params.set('semanaContable', request.semanaContable.toString());
    }
    if (request.fechaInicio) {
      params = params.set('fechaInicio', request.fechaInicio);
    }
    if (request.fechaFin) {
      params = params.set('fechaFin', request.fechaFin);
    }

    return this.http.get<ReporteContableCompletoDto>(`${this.apiUrl}/generar`, { params });
  }

  /**
   * Obtiene las semanas contables disponibles para un lote padre
   */
  obtenerSemanasContables(lotePadreId: number): Observable<number[]> {
    return this.http.get<number[]>(`${this.apiUrl}/semanas-contables/${lotePadreId}`);
  }

  /**
   * Exporta el reporte contable a Excel
   */
  exportarExcel(request: GenerarReporteContableRequestDto): Observable<Blob> {
    let params = new HttpParams()
      .set('lotePadreId', request.lotePadreId.toString());

    if (request.semanaContable) {
      params = params.set('semanaContable', request.semanaContable.toString());
    }
    if (request.fechaInicio) {
      params = params.set('fechaInicio', request.fechaInicio);
    }
    if (request.fechaFin) {
      params = params.set('fechaFin', request.fechaFin);
    }

    return this.http.get(`${this.apiUrl}/exportar/excel`, {
      params,
      responseType: 'blob'
    });
  }
}


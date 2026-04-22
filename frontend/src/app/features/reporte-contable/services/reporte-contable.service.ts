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
  /** "Levante" o "Produccion" */
  faseDelLote: string;
  semanaContable?: number;
  fechaInicio?: string;
  fechaFin?: string;
}

export interface LoteBaseFiltroContableDto {
  loteId: number;
  loteNombre: string;
  lotePosturaBaseId?: number | null;
  codigoErp?: string | null;
}

export interface GalponFiltroContableDto {
  galponId?: string | null;
  galponNombre: string;
  lotesBase: LoteBaseFiltroContableDto[];
}

export interface NucleoFiltroContableDto {
  nucleoId?: string | null;
  nucleoNombre: string;
  galpones: GalponFiltroContableDto[];
}

export interface GranjaFiltroContableDto {
  granjaId: number;
  granjaNombre: string;
  nucleos: NucleoFiltroContableDto[];
}

export interface FiltrosContablesDto {
  granjas: GranjaFiltroContableDto[];
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
      .set('lotePadreId', request.lotePadreId.toString())
      .set('faseLote', request.faseDelLote);

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
      .set('lotePadreId', request.lotePadreId.toString())
      .set('faseLote', request.faseDelLote);

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

  /**
   * Retorna la jerarquía granjas → núcleos → galpones → lotes base para los filtros del reporte
   */
  getFiltrosDisponibles(): Observable<FiltrosContablesDto> {
    return this.http.get<FiltrosContablesDto>(`${this.apiUrl}/filtros-disponibles`);
  }

  /**
   * Obtiene el reporte de movimientos de huevos
   */
  obtenerReporteMovimientosHuevos(request: GenerarReporteContableRequestDto): Observable<ReporteMovimientosHuevosDto> {
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

    return this.http.get<ReporteMovimientosHuevosDto>(`${this.apiUrl}/movimientos-huevos`, { params });
  }
}

export interface MovimientoHuevoDiarioDto {
  fecha: string;
  loteId: string;
  loteNombre: string;
  postura: number;
  hvtoFertil: number;
  hvoComercial: number;
  huevoDesecho: number;
  limpio: number;
  tratado: number;
  sucio: number;
  deforme: number;
  blanco: number;
  dobleYema: number;
  piso: number;
  pequeno: number;
  roto: number;
  otro: number;
  entrada: number;
  capturaInfo: number;
  venta: number;
  salida: number;
  trasladoAPlanta: number;
  descarte: number;
  tecnicoAAA?: number;
  tecnicoAA?: number;
  tecnicoB?: number;
  tecnicoC?: number;
  picadoPecoso?: number;
  picadoManchado?: number;
  picadoSucio?: number;
}

export interface ReporteMovimientosHuevosDto {
  lotePadreId: number;
  lotePadreNombre: string;
  semanaContable?: number;
  fechaInicio?: string;
  fechaFin?: string;
  movimientosDiarios: MovimientoHuevoDiarioDto[];
  totalPostura: number;
  totalHvtoFertil: number;
  totalHvoComercial: number;
  totalHuevoDesecho: number;
  totalEntrada: number;
  totalVenta: number;
  totalSalida: number;
  totalTrasladoAPlanta: number;
  totalDescarte: number;
}


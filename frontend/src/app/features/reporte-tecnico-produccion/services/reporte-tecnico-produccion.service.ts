// src/app/features/reporte-tecnico-produccion/services/reporte-tecnico-produccion.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

// ========== DTOs para Reporte Técnico Producción ==========

export interface ReporteTecnicoProduccionDiarioDto {
  dia: number;
  semana: number;
  fecha: string;
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
  huevosTotales: number;
  porcentajePostura: number;
  kilosAlimentoHembras: number;
  kilosAlimentoMachos: number;
  huevosEnviadosPlanta: number;
  porcentajeEnviadoPlanta: number;
  huevosIncubables: number;
  huevosCargados: number;
  porcentajeNacimientos?: number | null;
  ventaHuevo?: number | null;
  pollitosVendidos?: number | null;
  pesoHembra?: number | null;
  pesoMachos?: number | null;
  pesoHuevo: number;
  porcentajeGrasaCorporal?: number | null;
  // Desglose de tipos de huevos
  huevoLimpio: number;
  huevoTratado: number;
  huevoSucio: number;
  huevoDeforme: number;
  huevoBlanco: number;
  huevoDobleYema: number;
  huevoPiso: number;
  huevoPequeno: number;
  huevoRoto: number;
  huevoDesecho: number;
  huevoOtro: number;
  // Porcentajes de tipos de huevos
  porcentajeLimpio?: number | null;
  porcentajeTratado?: number | null;
  porcentajeSucio?: number | null;
  porcentajeDeforme?: number | null;
  porcentajeBlanco?: number | null;
  porcentajeDobleYema?: number | null;
  porcentajePiso?: number | null;
  porcentajePequeno?: number | null;
  porcentajeRoto?: number | null;
  porcentajeDesecho?: number | null;
  porcentajeOtro?: number | null;
  // Transferencias de huevos
  huevosTrasladadosTotal: number;
  huevosTrasladadosLimpio: number;
  huevosTrasladadosTratado: number;
  huevosTrasladadosSucio: number;
  huevosTrasladadosDeforme: number;
  huevosTrasladadosBlanco: number;
  huevosTrasladadosDobleYema: number;
  huevosTrasladadosPiso: number;
  huevosTrasladadosPequeno: number;
  huevosTrasladadosRoto: number;
  huevosTrasladadosDesecho: number;
  huevosTrasladadosOtro: number;
}

export interface ReporteTecnicoProduccionSemanalDto {
  semana: number;
  fechaInicioSemana: string;
  fechaFinSemana: string;
  edadInicioSemanas: number;
  edadFinSemanas: number;
  mortalidadHembrasSemanal: number;
  mortalidadMachosSemanal: number;
  seleccionHembrasSemanal: number;
  seleccionMachosSemanal: number;
  ventasHembrasSemanal: number;
  ventasMachosSemanal: number;
  trasladosHembrasSemanal: number;
  trasladosMachosSemanal: number;
  saldoInicioHembras: number;
  saldoFinHembras: number;
  saldoInicioMachos: number;
  saldoFinMachos: number;
  huevosTotalesSemanal: number;
  porcentajePosturaPromedio: number;
  kilosAlimentoHembrasSemanal: number;
  kilosAlimentoMachosSemanal: number;
  huevosEnviadosPlantaSemanal: number;
  porcentajeEnviadoPlantaPromedio: number;
  huevosIncubablesSemanal: number;
  huevosCargadosSemanal: number;
  porcentajeNacimientosPromedio?: number | null;
  ventaHuevoSemanal?: number | null;
  pollitosVendidosSemanal?: number | null;
  pesoHembraPromedio?: number | null;
  pesoMachosPromedio?: number | null;
  pesoHuevoPromedio: number;
  porcentajeGrasaCorporalPromedio?: number | null;
  // Desglose de tipos de huevos semanal
  huevoLimpioSemanal: number;
  huevoTratadoSemanal: number;
  huevoSucioSemanal: number;
  huevoDeformeSemanal: number;
  huevoBlancoSemanal: number;
  huevoDobleYemaSemanal: number;
  huevoPisoSemanal: number;
  huevoPequenoSemanal: number;
  huevoRotoSemanal: number;
  huevoDesechoSemanal: number;
  huevoOtroSemanal: number;
  // Porcentajes promedio de tipos de huevos
  porcentajeLimpioPromedio?: number | null;
  porcentajeTratadoPromedio?: number | null;
  porcentajeSucioPromedio?: number | null;
  porcentajeDeformePromedio?: number | null;
  porcentajeBlancoPromedio?: number | null;
  porcentajeDobleYemaPromedio?: number | null;
  porcentajePisoPromedio?: number | null;
  porcentajePequenoPromedio?: number | null;
  porcentajeRotoPromedio?: number | null;
  porcentajeDesechoPromedio?: number | null;
  porcentajeOtroPromedio?: number | null;
  // Transferencias de huevos semanal
  huevosTrasladadosTotalSemanal: number;
  huevosTrasladadosLimpioSemanal: number;
  huevosTrasladadosTratadoSemanal: number;
  huevosTrasladadosSucioSemanal: number;
  huevosTrasladadosDeformeSemanal: number;
  huevosTrasladadosBlancoSemanal: number;
  huevosTrasladadosDobleYemaSemanal: number;
  huevosTrasladadosPisoSemanal: number;
  huevosTrasladadosPequenoSemanal: number;
  huevosTrasladadosRotoSemanal: number;
  huevosTrasladadosDesechoSemanal: number;
  huevosTrasladadosOtroSemanal: number;
  detalleDiario: ReporteTecnicoProduccionDiarioDto[];
}

export interface ReporteTecnicoProduccionLoteInfoDto {
  loteId: number;
  loteNombre: string;
  raza?: string | null;
  linea?: string | null;
  fechaInicioProduccion?: string | null;
  numeroHembrasIniciales?: number | null;
  numeroMachosIniciales?: number | null;
  galpon?: number | null;
  tecnico?: string | null;
  granjaNombre?: string | null;
  nucleoNombre?: string | null;
}

export interface ReporteTecnicoProduccionCompletoDto {
  loteInfo: ReporteTecnicoProduccionLoteInfoDto;
  datosDiarios: ReporteTecnicoProduccionDiarioDto[];
  datosSemanales: ReporteTecnicoProduccionSemanalDto[];
}

@Injectable({ providedIn: 'root' })
export class ReporteTecnicoProduccionService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/ReporteTecnicoProduccion`;

  /**
   * Genera reporte técnico diario de producción para un lote específico
   */
  generarReporteDiario(
    loteId: number,
    fechaInicio?: string | null,
    fechaFin?: string | null,
    consolidarSublotes: boolean = false
  ): Observable<ReporteTecnicoProduccionCompletoDto> {
    let params = new HttpParams()
      .set('consolidarSublotes', consolidarSublotes.toString());

    if (fechaInicio) {
      params = params.set('fechaInicio', fechaInicio);
    }
    if (fechaFin) {
      params = params.set('fechaFin', fechaFin);
    }

    return this.http.get<ReporteTecnicoProduccionCompletoDto>(
      `${this.baseUrl}/diario/${loteId}`,
      { params }
    );
  }

  /**
   * Obtiene la lista de sublotes para un lote base
   */
  obtenerSublotes(loteNombreBase: string): Observable<string[]> {
    const params = new HttpParams().set('loteNombreBase', loteNombreBase);
    return this.http.get<string[]>(`${this.baseUrl}/sublotes`, { params });
  }

  /**
   * Genera reporte técnico "Cuadro" semanal con valores de guía genética (amarillos)
   */
  generarReporteCuadro(
    loteId: number,
    fechaInicio?: string | null,
    fechaFin?: string | null,
    consolidarSublotes: boolean = false
  ): Observable<ReporteTecnicoProduccionCuadroCompletoDto> {
    let params = new HttpParams()
      .set('consolidarSublotes', consolidarSublotes.toString());

    if (fechaInicio) {
      params = params.set('fechaInicio', fechaInicio);
    }
    if (fechaFin) {
      params = params.set('fechaFin', fechaFin);
    }

    return this.http.get<ReporteTecnicoProduccionCuadroCompletoDto>(
      `${this.baseUrl}/cuadro/${loteId}`,
      { params }
    );
  }

  /**
   * Genera reporte de clasificación de huevos comercio semanal con valores de guía genética (amarillos)
   */
  generarReporteClasificacionHuevoComercio(
    loteId: number,
    fechaInicio?: string | null,
    fechaFin?: string | null,
    consolidarSublotes: boolean = false
  ): Observable<ReporteClasificacionHuevoComercioCompletoDto> {
    let params = new HttpParams()
      .set('consolidarSublotes', consolidarSublotes.toString());

    if (fechaInicio) {
      params = params.set('fechaInicio', fechaInicio);
    }
    if (fechaFin) {
      params = params.set('fechaFin', fechaFin);
    }

    return this.http.get<ReporteClasificacionHuevoComercioCompletoDto>(
      `${this.baseUrl}/clasificacion-huevo-comercio/${loteId}`,
      { params }
    );
  }

  /**
   * Exporta todos los reportes de producción a Excel (múltiples hojas)
   */
  exportarExcelCompleto(
    loteId: number,
    fechaInicio?: string | null,
    fechaFin?: string | null,
    consolidarSublotes: boolean = false
  ): Observable<Blob> {
    let params = new HttpParams()
      .set('consolidarSublotes', consolidarSublotes.toString());

    if (fechaInicio) {
      params = params.set('fechaInicio', fechaInicio);
    }
    if (fechaFin) {
      params = params.set('fechaFin', fechaFin);
    }

    return this.http.get(
      `${this.baseUrl}/exportar/excel/${loteId}`,
      { 
        params,
        responseType: 'blob'
      }
    );
  }
}

// ========== DTOs para Reporte Cuadro ==========

export interface ReporteTecnicoProduccionCuadroDto {
  semana: number;
  fecha: string;
  edadProduccionSemanas: number;
  avesFinHembras: number;
  avesFinMachos: number;
  // MORTALIDAD HEMBRAS
  mortalidadHembrasN: number;
  mortalidadHembrasDescPorcentajeSem: number;
  mortalidadHembrasPorcentajeAcum: number;
  mortalidadHembrasStandarM?: number | null; // AMARILLO
  mortalidadHembrasAcumStandar?: number | null; // AMARILLO
  // MORTALIDAD MACHOS
  mortalidadMachosN: number;
  mortalidadMachosDescPorcentajeSem: number;
  mortalidadMachosPorcentajeAcum: number;
  mortalidadMachosStandarM?: number | null; // AMARILLO
  mortalidadMachosAcumStandar?: number | null; // AMARILLO
  // PRODUCCION TOTAL DE HUEVOS
  huevosVentaSemana: number;
  huevosAcum: number;
  porcentajeSem: number;
  porcentajeRoss?: number | null; // AMARILLO
  taa: number;
  taaRoss?: number | null; // AMARILLO
  // HUEVOS ENVIADOS PLANTA
  enviadosPlanta: number;
  acumEnviaP: number;
  porcentajeEnviaP: number;
  porcentajeHala?: number | null; // AMARILLO
  // HUEVO INCUBABLE
  huevosIncub: number;
  porcentajeDescarte: number;
  porcentajeAcumIncub: number;
  laa: number;
  stdRoss?: number | null; // AMARILLO
  // HUEVOS CARGADOS Y POLLITOS
  hCarga: number;
  hCargaAcu: number;
  vHuevo: number;
  vHuevoPollitos: number;
  pollAcum: number;
  paa: number;
  paaRoss?: number | null; // AMARILLO
  // CONSUMO DE ALIMENTO HEMBRA
  kgSemHembra: number;
  acumHembra: number;
  acumAaHembra: number;
  stAcumHembra?: number | null; // AMARILLO
  loteHembra?: number | null;
  stGrHembra?: number | null; // AMARILLO
  // CONSUMO DE ALIMENTO MACHO
  kgSemMachos: number;
  acumMachos: number;
  acumAaMachos: number;
  stAcumMachos?: number | null; // AMARILLO
  grDiaMachos: number;
  stGrMachos?: number | null; // AMARILLO
  // PESOS
  pesoHembraKg?: number | null;
  pesoHembraStd?: number | null; // AMARILLO
  pesoMachosKg?: number | null;
  pesoMachosStd?: number | null; // AMARILLO
  pesoHuevoSem: number;
  pesoHuevoStd?: number | null; // AMARILLO
  masaSem: number;
  masaStd?: number | null; // AMARILLO
  // % APROV
  porcentajeAprovSem?: number | null;
  porcentajeAprovStd?: number | null; // AMARILLO
  // TIPO DE ALIMENTO
  tipoAlimento?: string | null;
  // OBSERVACIONES
  observaciones?: string | null;
}

export interface ReporteTecnicoProduccionCuadroCompletoDto {
  loteInfo: ReporteTecnicoProduccionLoteInfoDto;
  datosCuadro: ReporteTecnicoProduccionCuadroDto[];
}

// ========== DTOs para Reporte Clasificación Huevo Comercio ==========

export interface ReporteClasificacionHuevoComercioDto {
  semana: number;
  fechaInicioSemana: string;
  fechaFinSemana: string;
  loteNombre: string;
  // Datos reales
  incubableLimpio: number;
  huevoTratado: number;
  porcentajeTratado: number;
  huevoDY: number;
  porcentajeDY: number;
  huevoRoto: number;
  porcentajeRoto: number;
  huevoDeforme: number;
  porcentajeDeforme: number;
  huevoPiso: number;
  porcentajePiso: number;
  huevoDesecho: number;
  porcentajeDesecho: number;
  huevoPIP: number;
  porcentajePIP: number;
  huevoSucioDeBanda: number;
  porcentajeSucioDeBanda: number;
  totalPN: number;
  porcentajeTotal: number;
  // Valores de guía genética (amarillos)
  incubableLimpioGuia?: number | null;
  huevoTratadoGuia?: number | null;
  porcentajeTratadoGuia?: number | null;
  huevoDYGuia?: number | null;
  porcentajeDYGuia?: number | null;
  huevoRotoGuia?: number | null;
  porcentajeRotoGuia?: number | null;
  huevoDeformeGuia?: number | null;
  porcentajeDeformeGuia?: number | null;
  huevoPisoGuia?: number | null;
  porcentajePisoGuia?: number | null;
  huevoDesechoGuia?: number | null;
  porcentajeDesechoGuia?: number | null;
  huevoPIPGuia?: number | null;
  porcentajePIPGuia?: number | null;
  huevoSucioDeBandaGuia?: number | null;
  porcentajeSucioDeBandaGuia?: number | null;
  totalPNGuia?: number | null;
  porcentajeTotalGuia?: number | null;
}

export interface ReporteClasificacionHuevoComercioCompletoDto {
  loteInfo: ReporteTecnicoProduccionLoteInfoDto;
  datosClasificacion: ReporteClasificacionHuevoComercioDto[];
}

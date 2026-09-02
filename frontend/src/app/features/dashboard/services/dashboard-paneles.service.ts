import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { CategoriaDto, PuntoDiaDto } from '../models/dashboard-metricas.model';

/** Espejo de `DashboardResumenDto` (backend). */
export interface DashboardResumen {
  granjas: number;
  lotesPosturaActivos: number;
  lotesPosturaTotal: number;
  lotesEngordeActivos: number;
  lotesEngordeTotal: number;
  /** El usuario tiene al menos una granja con alcance granular: está viendo una parte. */
  alcanceRestringido: boolean;
  generadoAt: string;
}

/** Espejo de `DashboardPosturaDto`. */
export interface DashboardPostura {
  mortalidadDiaria: PuntoDiaDto[];
  huevoDiario: PuntoDiaDto[];
  lotesPorGranja: CategoriaDto[];
  totalMortalidad: number;
  totalHuevo: number;
  diasConRegistro: number;
  /** La empresa no maneja machos en postura: la serie viene sólo de hembras. */
  ocultaMachos: boolean;
}

/** Espejo de `DashboardEngordeDto`. */
export interface DashboardEngorde {
  mortalidadDiaria: PuntoDiaDto[];
  consumoDiarioKg: PuntoDiaDto[];
  pesoPromedioDiario: PuntoDiaDto[];
  lotesPorGranja: CategoriaDto[];
  totalMortalidad: number;
  totalConsumoKg: number;
  diasConRegistro: number;
}

/** Espejo de `DescuadreGalponDto`. Las dos señales van SEPARADAS y no se suman. */
export interface DescuadreGalpon {
  granjaNombre: string;
  galponId: string;
  /** KILOS que faltan o sobran. */
  descuadreKg: number;
  /** DÍAS que cerraron en rojo con el total perfecto. Problema distinto al de arriba. */
  filasNegativas: number;
  ciclosDelGalpon: number;
}

/** Espejo de `DashboardInventarioDto`. */
export interface DashboardInventario {
  stockPorGranja: CategoriaDto[];
  descuadres: DescuadreGalpon[];
  galponesConKilos: number;
  galponesConDiasEnRojo: number;
}

/** Espejo de `DashboardCumplimientoDto`. */
export interface DashboardCumplimiento {
  vacunacionVencida: number;
  vacunacionProxima: number;
  cuadresSinResolver: number;
  vacunacionPorGranja: CategoriaDto[];
}

/**
 * Datos de los paneles del dashboard.
 *
 * ## Lo que este servicio NO manda, y no es un olvido
 *
 * **No manda `companyId`, `userId` ni `farmIds`.** El servidor resuelve el alcance contra la sesión
 * (`ICurrentUser.CompanyId`, ya validado por `ActiveCompanyMiddleware`, + el alcance de ubicación del
 * usuario). Hasta el 1-sep-2026 el dashboard viejo **sí** los mandaba y el backend **los ignoraba**:
 * los tres viajaban en la URL y ninguna acción del controller los declaraba. Mandarlos ahora sería
 * pedirle al servidor que confíe en lo que dice el cliente sobre qué empresa es.
 *
 * **Un endpoint por panel.** Es lo que hace posible la carga perezosa de verdad: el panel que no se
 * dibuja no se pide.
 */
@Injectable({ providedIn: 'root' })
export class DashboardPanelesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/Dashboard`;

  /** Conteos generales. Un usuario sin granjas visibles recibe ceros, no la empresa entera. */
  resumen(): Observable<DashboardResumen> {
    return this.http.get<DashboardResumen>(`${this.base}/resumen`);
  }

  /** Panel de postura para el período `desde`–`hasta` (fechas puras `YYYY-MM-DD`). */
  postura(desde: string, hasta: string): Observable<DashboardPostura> {
    return this.http.get<DashboardPostura>(`${this.base}/postura`, { params: periodo(desde, hasta) });
  }

  /** Panel de pollo engorde para el período. */
  engorde(desde: string, hasta: string): Observable<DashboardEngorde> {
    return this.http.get<DashboardEngorde>(`${this.base}/engorde`, { params: periodo(desde, hasta) });
  }

  /** Panel de alimento e inventario. Foto del estado actual: no lleva período. */
  inventario(): Observable<DashboardInventario> {
    return this.http.get<DashboardInventario>(`${this.base}/inventario`);
  }

  /** Panel de cumplimiento. Pendientes de hoy: no lleva período. */
  cumplimiento(): Observable<DashboardCumplimiento> {
    return this.http.get<DashboardCumplimiento>(`${this.base}/cumplimiento`);
  }
}

function periodo(desde: string, hasta: string): HttpParams {
  return new HttpParams().set('desde', desde).set('hasta', hasta);
}

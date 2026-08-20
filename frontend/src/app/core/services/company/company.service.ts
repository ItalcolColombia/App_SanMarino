// src/app/core/services/company/company.service.ts
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { BaseHttpService } from '../base-http.service';

export interface Company {
  id?: number;
  name: string;
  identifier: string;
  documentType: string;
  logoDataUrl?: string | null;
  address?: string;
  phone?: string;
  email?: string;
  // antiguos (fallback visual)
  country?: string;
  state?: string;
  city?: string;
  // nuevos preferidos
  countryId?: number;
  departamentoId?: number;
  municipioId?: number;

  roleIds?: number[];
  visualPermissions?: string[];
  mobileAccess?: boolean;
  // Default GLOBAL de la empresa: ¿el alimento se maneja a nivel GALPÓN? (cada granja puede overridear)
  manejaAlimentoPorGalpon?: boolean;
  /** Flag por empresa: maneja códigos ERP avícolas (bodega / C.O. / instalación / centro de costo). */
  manejaCodigosErpAvicola?: boolean;
  /** Flag por empresa: clasifica los huevos por ÍTEM del catálogo (Primera/Pnc) en vez de las 11 columnas fijas. */
  clasificacionHuevoPorItems?: boolean;
  /** Flag por empresa: permite trasladar aves entre etapas (Levante → Producción) registrando cohorte con la edad de origen. */
  permiteTrasladoAvesCrossEtapa?: boolean;
  /** Días ANTES del encasetamiento cuyo alimento cuenta como «ingreso inicial del ciclo» en el reporte diario de engorde. Rango 0-30, default 10. */
  diasAlimentoPrevioEncaset?: number;

  // ── Resto de los flags de comportamiento por empresa ──
  // Se administran desde la pantalla de Empresas (ver funciones/flags-empresa.funcion.ts). Antes
  // vivían solo en la base de datos y había que encenderlos por SQL.
  /** El alimento se ubica en silos/bodega en vez del galpón. */
  manejaInventarioPorSilo?: boolean;
  /** Los reportes de alimento leen el inventario unificado. */
  reportesAlimentoDesdeInventarioUnificado?: boolean;
  /** El reporte de costos toma el alimento de los movimientos reales. */
  reporteCostosAlimentoDesdeFuentesReales?: boolean;
  /** Levante captura huevos desde la semana 14 y los arrastra al liquidar. */
  capturaHuevosEnLevante?: boolean;
  /** Pollo engorde con una sola columna Mixto en vez de hembras/machos. */
  seguimientoEngordeMixto?: boolean;
  /** La venta de engorde se registra sin peso y se completa con la báscula. */
  ventaEngordePesoDiferido?: boolean;
  /** Permite crear lotes de engorde antes del encasetamiento. */
  programacionLotesEngorde?: boolean;
  /** El nombre del lote se arma con el número de corrida del galpón. */
  nombreLoteIncluyeCorrida?: boolean;
  /** Doble validación: guardar separa; el descuento se aplica al validar. */
  requiereValidacionSeguimientoDiario?: boolean;
  /** El primer día con registro depende de la hora de llegada de las aves. */
  primerRegistroSegunHoraLlegada?: boolean;
  /** El seguimiento diario no captura consumo de alimento de machos (producción ni levante). */
  consumoAlimentoSoloHembras?: boolean;
  /** Oculta la columna Machos en mortalidad/selección/peso/uniformidad/traslados/ventas y retira error de sexaje del registro diario. */
  ocultaMachosEnPostura?: boolean;
  /** Última semana con huevo de primera postura habilitado. Sin valor = la empresa no usa el concepto. */
  huevoPrimeraPosturaHastaSemana?: number | null;
}

@Injectable({ providedIn: 'root' })
export class CompanyService extends BaseHttpService {
  private readonly baseUrl = `${environment.apiUrl}/Company`;

  /** Trae todas las empresas */
  getAll(): Observable<Company[]> {
    return this.get<Company[]>(this.baseUrl, { context: 'CompanyService.getAll' });
  }

  /** Trae una empresa por ID (incluye logo para edición) */
  getById(id: number): Observable<Company> {
    return this.get<Company>(`${this.baseUrl}/${id}`, { context: 'CompanyService.getById' });
  }

  /** Trae TODAS las empresas sin filtro para administración */
  // Ruta "global" (no "admin"): AWS WAF AdminProtection bloquea cualquier path con /admin.
  getAllForAdmin(): Observable<Company[]> {
    return this.get<Company[]>(`${this.baseUrl}/global`, { context: 'CompanyService.getAllForAdmin' });
  }

  /** Crea una nueva */
  create(c: Company): Observable<Company> {
    return this.post<Company>(this.baseUrl, c, { context: 'CompanyService.create' });
  }

  /** Actualiza existentes */
  update(c: Company): Observable<Company> {
    return this.put<Company>(`${this.baseUrl}/${c.id}`, c, { context: 'CompanyService.update' });
  }

  /** Elimina por id */
  delete(id: number): Observable<void> {
    return this.deleteRequest<void>(`${this.baseUrl}/${id}`, { context: 'CompanyService.delete' });
  }
}

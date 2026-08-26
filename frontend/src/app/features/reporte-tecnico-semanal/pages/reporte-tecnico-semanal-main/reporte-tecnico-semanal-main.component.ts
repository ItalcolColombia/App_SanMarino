// Página orquestadora del Reporte Técnico Semanal (Sanmarino postura):
// un módulo, dos opciones (Levante / Producción) por lote base, tabs por
// galpón + consolidado, comparación contra guía genética y export multi-hoja.
// La lógica grande vive en funciones/ (puras) y en el backend.
import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgChartsModule } from 'ng2-charts';
import { firstValueFrom } from 'rxjs';

import { ToastService } from '../../../../shared/services/toast.service';
import { exportarAoaMultiHojaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { dateStampCompact, sanitizeFileName } from '../../../../shared/utils/format';
import { ReporteTecnicoLevanteFilterService } from '../../../reportes-tecnicos/services/reporte-tecnico-levante-filter.service';
import { ReporteTecnicoSemanalService } from '../../services/reporte-tecnico-semanal.service';
import {
  agruparColumnas,
  ColumnaReporte,
  COLUMNAS_ALIMENTO_FASE,
  COLUMNAS_CLASIFICACION_HUEVO,
  COLUMNAS_LEVANTE,
  COLUMNAS_PRODUCCION,
  GrupoCabecera
} from '../../funciones/columnas-reporte-semanal.funcion';
import {
  construirHojasLevante,
  construirHojasProduccion
} from '../../funciones/construir-aoa-reporte-semanal.funcion';
import {
  construirGraficasLevante,
  construirGraficasProduccion,
  GraficaReporteSemanal
} from '../../funciones/construir-graficas-reporte-semanal.funcion';
import {
  ReporteSemanalAlimentoFase,
  ReporteSemanalAlimentoPorFase,
  ReporteSemanalLevanteTab,
  ReporteSemanalProduccionSemana,
  ReporteSemanalTabHeader,
  ReporteTecnicoSemanalLevanteResponse,
  ReporteTecnicoSemanalProduccionResponse
} from '../../models/reporte-tecnico-semanal.model';
import { filtrarColumnasMachos } from '../../funciones/filtrar-columnas-machos.funcion';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';

type TipoReporte = 'LEVANTE' | 'PRODUCCION';
/**
 * Vistas del tab. `alimento` es la hoja «ALIMLev» (solo levante) y
 * `clasificacion` la hoja «CLAS Huevo» (solo producción): no se ofrecen en la
 * etapa donde no aplican.
 */
type VistaTab = 'tabla' | 'graficas' | 'alimento' | 'clasificacion';

/** Una de las cuatro tablas de la hoja ALIMLev, ya formateada. */
interface BloqueAlimentoView {
  titulo: string;
  filas: string[][];
}

/** Tab de vista precalculada (referencias estables — evita NG0103). */
interface TabView {
  nombre: string;
  esConsolidado: boolean;
  header: ReporteSemanalTabHeader;
  infoChips: { label: string; valor: string }[];
  filas: string[][];
  graficas: GraficaReporteSemanal[];
  /** Hoja ALIMLev (levante) — vacío en producción. */
  alimento: BloqueAlimentoView[];
  /** Hoja CLAS Huevo (producción) — vacío en levante. */
  clasificacion: string[][];
}

@Component({
  selector: 'app-reporte-tecnico-semanal-main',
  standalone: true,
  imports: [FormsModule, NgChartsModule],
  templateUrl: './reporte-tecnico-semanal-main.component.html',
  styleUrls: ['./reporte-tecnico-semanal-main.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  // Instancia PROPIA del servicio de filtros: no comparte selección con la
  // pantalla de Reporte Técnico existente (el servicio es stateful).
  providers: [ReporteTecnicoLevanteFilterService]
})
export class ReporteTecnicoSemanalMainComponent implements OnInit {
  /** Empresas sin machos en postura: sus columnas no se pintan ni se exportan (SR-DEF-1). */
  ocultaMachosEnPostura = false;
  /**
   * Flag `companies.clasificacion_huevo_por_items`: la hoja «CLAS Huevo» reproduce las 11
   * columnas fijas de `seguimiento_diario_produccion`, que para estas empresas quedan siempre en
   * 0 (el desglose real vive en `metadata.huevoItems`, que este reporte no lee) — la pestaña
   * entera no aplica, mismo tratamiento que ya tiene la pestaña «Clasificación» del Reporte
   * Técnico Producción. FAIL-CLOSED: sin flag, pestaña intacta.
   */
  clasificacionHuevoPorItems = false;

  /**
   * Columnas que la empresa realmente ve. Se filtra sobre el array de definiciones, que es la
   * MISMA fuente de la cabecera, las celdas y el Excel: filtrando aca los tres quedan consistentes
   * por construccion. Con el flag apagado devuelve la misma referencia.
   */
  private columnasVisibles<T extends { grupo: string; titulo: string }>(columnas: readonly T[]): T[] {
    return filtrarColumnasMachos(columnas, this.ocultaMachosEnPostura) as T[];
  }

  readonly filtros = inject(ReporteTecnicoLevanteFilterService);
  private readonly service = inject(ReporteTecnicoSemanalService);
  private readonly toast = inject(ToastService);
  private readonly companyConfig = inject(ActiveCompanyConfigService);

  tipoReporte: TipoReporte = 'LEVANTE';
  vista: VistaTab = 'tabla';

  loading = false;
  error: string | null = null;

  // Resultado crudo (para export) + vista precalculada.
  private respuestaLevante: ReporteTecnicoSemanalLevanteResponse | null = null;
  private respuestaProduccion: ReporteTecnicoSemanalProduccionResponse | null = null;

  generado = false;
  loteBaseNombre = '';
  tieneGuia = true;
  grupos: GrupoCabecera[] = [];
  titulos: string[] = [];
  gruposClasificacion: GrupoCabecera[] = [];
  titulosClasificacion: string[] = [];
  titulosAlimento: string[] = [];
  tabs: TabView[] = [];
  tabActiva = 0;

  ngOnInit(): void {
    this.companyConfig.getFlags().subscribe({
      next: f => {
        this.ocultaMachosEnPostura = !!f?.ocultaMachosEnPostura;
        this.clasificacionHuevoPorItems = !!f?.clasificacionHuevoPorItems;
      },
      error: () => {
        this.ocultaMachosEnPostura = false;
        this.clasificacionHuevoPorItems = false;
      }
    });
    this.filtros.loadFilterData();
  }

  get tabView(): TabView | null {
    return this.tabs[this.tabActiva] ?? null;
  }

  get hayResultado(): boolean {
    return this.generado && this.tabs.length > 0;
  }

  cambiarTipo(tipo: TipoReporte): void {
    if (this.tipoReporte === tipo) return;
    this.tipoReporte = tipo;
    // `alimento` y `clasificacion` son propias de una etapa: al cambiar se
    // vuelve a Tabla para no dejar seleccionada una vista que ya no existe.
    this.vista = 'tabla';
    this.limpiarResultado();
    this.filtros.setEtapa(tipo);
  }

  seleccionarTab(i: number): void {
    this.tabActiva = i;
  }

  async generar(): Promise<void> {
    const loteBaseId = this.filtros.selectedLoteBaseId();
    if (!loteBaseId) {
      this.error = 'Seleccioná el lote base para generar el reporte.';
      return;
    }
    this.loading = true;
    this.error = null;
    this.limpiarResultado();
    try {
      if (this.tipoReporte === 'LEVANTE') {
        const resp = await firstValueFrom(this.service.generarLevante({ lotePosturaBaseId: loteBaseId }));
        this.respuestaLevante = resp;
        this.aplicarResultado(resp.loteBaseNombre, resp.tieneGuia, resp.consolidado, resp.tabs,
          this.columnasVisibles(COLUMNAS_LEVANTE), construirGraficasLevante);
      } else {
        const resp = await firstValueFrom(this.service.generarProduccion({ lotePosturaBaseId: loteBaseId }));
        this.respuestaProduccion = resp;
        this.aplicarResultado(resp.loteBaseNombre, resp.tieneGuia, resp.consolidado, resp.tabs,
          this.columnasVisibles(COLUMNAS_PRODUCCION), construirGraficasProduccion);
      }
      this.generado = true;
      if (this.tabs.every(t => t.filas.length === 0)) {
        this.toast.info('El lote base no tiene seguimientos para esta etapa todavía.');
      }
    } catch (err: any) {
      this.error = err?.error?.message || err?.message || 'Error al generar el reporte técnico semanal.';
    } finally {
      this.loading = false;
    }
  }

  /** Precalcula TODA la vista una sola vez por generación (referencias estables). */
  private aplicarResultado<T, TTab extends { header: ReporteSemanalTabHeader; semanas: T[] }>(
    loteBaseNombre: string,
    tieneGuia: boolean,
    consolidado: TTab | null,
    tabs: TTab[],
    columnas: ColumnaReporte<T>[],
    graficasDe: (tab: TTab) => GraficaReporteSemanal[]
  ): void {
    this.loteBaseNombre = loteBaseNombre;
    this.tieneGuia = tieneGuia;
    this.grupos = agruparColumnas(columnas);
    this.titulos = columnas.map(c => c.titulo);
    this.gruposClasificacion = agruparColumnas(COLUMNAS_CLASIFICACION_HUEVO);
    this.titulosClasificacion = COLUMNAS_CLASIFICACION_HUEVO.map(c => c.titulo);
    this.titulosAlimento = COLUMNAS_ALIMENTO_FASE.map(c => c.titulo);

    const construirTab = (tab: TTab, nombre: string, esConsolidado: boolean): TabView => ({
      nombre,
      esConsolidado,
      header: tab.header,
      infoChips: this.armarChips(tab.header),
      filas: tab.semanas.map(s => columnas.map(c => this.fmtCelda(c.valor(s), c.dec))),
      graficas: graficasDe(tab),
      // `alimentoPorFase` solo viene en levante y la clasificación solo en
      // producción: cada bloque se arma únicamente en la etapa que lo trae.
      alimento: this.tipoReporte === 'LEVANTE'
        ? this.armarAlimento((tab as unknown as ReporteSemanalLevanteTab).alimentoPorFase)
        : [],
      clasificacion: this.tipoReporte === 'PRODUCCION'
        ? (tab.semanas as unknown as ReporteSemanalProduccionSemana[])
            .map(s => COLUMNAS_CLASIFICACION_HUEVO.map(c => this.fmtCelda(c.valor(s), c.dec)))
        : []
    });

    const vistas: TabView[] = [];
    if (consolidado) vistas.push(construirTab(consolidado, 'Consolidado', true));
    for (const tab of tabs) vistas.push(construirTab(tab, tab.header.loteNombre, false));
    this.tabs = vistas;
    this.tabActiva = 0;
  }

  /**
   * Las cuatro tablas de la hoja ALIMLev, ya formateadas. Se precalculan acá
   * (no en el template) para no alocar por ciclo de change detection.
   */
  private armarAlimento(bloque: ReporteSemanalAlimentoPorFase | undefined): BloqueAlimentoView[] {
    if (!bloque) return [];
    const tabla = (titulo: string, filas: ReporteSemanalAlimentoFase[] | undefined): BloqueAlimentoView | null => {
      if (!filas || filas.length === 0) return null;
      return {
        titulo,
        filas: filas.map(f => COLUMNAS_ALIMENTO_FASE.map(c => this.fmtCelda(c.valor(f), c.dec)))
      };
    };
    return [
      tabla('Energía por fase — Hembras (kcal/ave)', bloque.energiaHembras),
      tabla('Energía por fase — Machos (kcal/ave)', bloque.energiaMachos),
      tabla('Proteína por fase — Hembras (g/ave)', bloque.proteinaHembras),
      tabla('Proteína por fase — Machos (g/ave)', bloque.proteinaMachos)
    ].filter((x): x is BloqueAlimentoView => x !== null);
  }

  private armarChips(h: ReporteSemanalTabHeader): { label: string; valor: string }[] {
    const chips: { label: string; valor: string }[] = [];
    const agregar = (label: string, valor: string | number | null | undefined) => {
      if (valor !== null && valor !== undefined && `${valor}`.trim() !== '') chips.push({ label, valor: `${valor}` });
    };
    agregar('Granja', h.granjaNombre);
    agregar('Municipio', h.municipio);
    agregar('Núcleo', h.nucleoNombre ?? h.nucleoId);
    agregar('Galpón', h.galponNombre ?? h.galponId);
    agregar('Raza', h.raza);
    agregar('Año guía', h.anioGuia);
    agregar('Encaset', h.fechaEncaset ? String(h.fechaEncaset).slice(0, 10) : null);
    agregar('Aves H', h.baseHembras);
    agregar('Aves M', h.baseMachos);
    agregar('Técnico', h.tecnico);
    return chips;
  }

  private fmtCelda(v: number | string | null, dec: number): string {
    if (v == null || v === '') return '—';
    if (typeof v === 'string') return v;
    return v.toLocaleString('es-CO', { maximumFractionDigits: dec });
  }

  limpiar(): void {
    this.filtros.resetSeleccion();
    this.error = null;
    this.limpiarResultado();
  }

  private limpiarResultado(): void {
    this.generado = false;
    this.respuestaLevante = null;
    this.respuestaProduccion = null;
    this.loteBaseNombre = '';
    this.tieneGuia = true;
    this.grupos = [];
    this.titulos = [];
    this.gruposClasificacion = [];
    this.titulosClasificacion = [];
    this.titulosAlimento = [];
    this.tabs = [];
    this.tabActiva = 0;
  }

  exportarExcel(): void {
    const hojas = this.tipoReporte === 'LEVANTE'
      ? (this.respuestaLevante ? construirHojasLevante(this.respuestaLevante) : [])
      : (this.respuestaProduccion ? construirHojasProduccion(this.respuestaProduccion, this.clasificacionHuevoPorItems) : []);

    if (hojas.length === 0 || this.tabs.every(t => t.filas.length === 0)) {
      this.toast.info('No hay datos para exportar con los filtros actuales.');
      return;
    }

    const tipo = this.tipoReporte === 'LEVANTE' ? 'Levante' : 'Produccion';
    exportarAoaMultiHojaExcel(hojas, {
      filenameFull: `Reporte_Tecnico_Semanal_${tipo}_${sanitizeFileName(this.loteBaseNombre)}_${dateStampCompact()}.xlsx`
    });
  }

  trackTab = (i: number, t: TabView) => t.nombre;
  trackFila = (i: number, _f: string[]) => i;
}

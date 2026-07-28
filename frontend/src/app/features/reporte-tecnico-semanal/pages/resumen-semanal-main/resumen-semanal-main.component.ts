// Página orquestadora de la hoja «RESUMEN SEMANAL» del Informe RA Pesadas.
// Contracara del Detalle: N lotes de UNA semana calendario.
// La lógica grande vive en funciones/ (puras) y en la BD; acá solo estado,
// HTTP y armado de la vista.
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ToastService } from '../../../../shared/services/toast.service';
import { exportarAoaMultiHojaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { dateStampCompact } from '../../../../shared/utils/format';
import { ReporteTecnicoSemanalService } from '../../services/reporte-tecnico-semanal.service';
import {
  agruparColumnasResumen,
  ColumnaResumen,
  COLUMNAS_RESUMEN_LEVANTE,
  COLUMNAS_RESUMEN_PRODUCCION,
  GrupoCabeceraResumen
} from '../../funciones/columnas-resumen-ra-pesadas.funcion';
import {
  construirHojaResumenLevante,
  construirHojaResumenProduccion
} from '../../funciones/construir-aoa-resumen-ra-pesadas.funcion';
import {
  EtapaResumen,
  ResumenSemanalRaPesadasLevanteResponse,
  ResumenSemanalRaPesadasProduccionResponse,
  ResumenSemanalTotales
} from '../../models/resumen-semanal-ra-pesadas.model';
import { semanaExcel } from '../../funciones/semana-excel.funcion';

/** Celda ya formateada; `texto` alinea a la izquierda. */
interface CeldaView {
  valor: string;
  texto: boolean;
}

@Component({
  selector: 'app-resumen-semanal-main',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './resumen-semanal-main.component.html',
  styleUrls: ['./resumen-semanal-main.component.scss'],
  // Angular 22: omitir esto = OnPush ⇒ la tabla se quedaría en «Cargando…»
  // aunque la request devuelva 200. Estado mutable + subscribe ⇒ Eager.
  changeDetection: ChangeDetectionStrategy.Eager
})
export class ResumenSemanalMainComponent implements OnInit {
  private readonly service = inject(ReporteTecnicoSemanalService);
  private readonly toast = inject(ToastService);

  etapa: EtapaResumen = 'levante';
  anio = new Date().getFullYear();
  // Arranca en la semana ANTERIOR, no en la actual: la semana en curso está
  // incompleta (o sin cargar todavía) y el reporte abriría vacío, que se lee
  // como si estuviera roto. En la semana 1 no se retrocede de año a propósito:
  // el filtro de año quedaría peleado con el de semana.
  semanaAnio = Math.max(1, semanaExcel(new Date()) - 1);
  regional: string | null = null;
  ciclo: string | null = null;
  excluirTrasladados = false;

  loading = false;
  error: string | null = null;
  generado = false;

  anios: number[] = [];
  semanas: number[] = Array.from({ length: 53 }, (_, i) => i + 1);
  /** Opciones tomadas del último resultado SIN filtrar (para no ofrecer vacíos). */
  opcionesRegional: string[] = [];
  opcionesCiclo: string[] = [];

  grupos: GrupoCabeceraResumen[] = [];
  titulos: string[] = [];
  alineacion: boolean[] = [];
  filas: CeldaView[][] = [];
  filaTotales: CeldaView[] = [];
  totales: ResumenSemanalTotales | null = null;
  rangoSemana = '';

  private respLevante: ResumenSemanalRaPesadasLevanteResponse | null = null;
  private respProduccion: ResumenSemanalRaPesadasProduccionResponse | null = null;

  ngOnInit(): void {
    const actual = new Date().getFullYear();
    this.anios = Array.from({ length: 7 }, (_, i) => actual - i);
    void this.generar();
  }

  get hayResultado(): boolean {
    return this.generado && this.filas.length > 0;
  }

  cambiarEtapa(etapa: EtapaResumen): void {
    if (this.etapa === etapa) return;
    this.etapa = etapa;
    // Los filtros secundarios son propios de cada etapa: se limpian al cambiar.
    this.regional = null;
    this.ciclo = null;
    this.excluirTrasladados = false;
    this.opcionesRegional = [];
    this.opcionesCiclo = [];
    void this.generar();
  }

  async generar(): Promise<void> {
    this.loading = true;
    this.error = null;
    try {
      const request = {
        anio: this.anio,
        semanaAnio: this.semanaAnio,
        etapa: this.etapa,
        regional: this.regional || null,
        ciclo: this.ciclo || null,
        excluirTrasladados: this.excluirTrasladados
      };

      if (this.etapa === 'levante') {
        const resp = await firstValueFrom(this.service.generarResumenLevante(request));
        this.respLevante = resp;
        this.respProduccion = null;
        this.aplicar(COLUMNAS_RESUMEN_LEVANTE, resp.filas, resp.totales,
          resp.fechaInicioSemana, resp.fechaFinSemana);
        this.recordarOpciones(resp.filas.map(f => f.regional), []);
      } else {
        const resp = await firstValueFrom(this.service.generarResumenProduccion(request));
        this.respProduccion = resp;
        this.respLevante = null;
        this.aplicar(COLUMNAS_RESUMEN_PRODUCCION, resp.filas, resp.totales,
          resp.fechaInicioSemana, resp.fechaFinSemana);
        this.recordarOpciones(resp.filas.map(f => f.regional), resp.filas.map(f => f.cicloProduccion));
      }
      this.generado = true;
      if (this.filas.length === 0) {
        this.toast.info('No hay lotes con seguimiento en esa semana.');
      }
    } catch (err: any) {
      this.error = err?.error?.message || err?.message || 'Error al generar el resumen semanal.';
      this.limpiarResultado();
    } finally {
      this.loading = false;
    }
  }

  /**
   * Precalcula TODA la tabla una sola vez por generación: el template no puede
   * llamar funciones que aloquen por ciclo (NG0103 / change detection).
   */
  private aplicar<T>(
    columnas: ColumnaResumen<T>[],
    filas: T[],
    totales: ResumenSemanalTotales,
    desde: string | null,
    hasta: string | null
  ): void {
    this.grupos = agruparColumnasResumen(columnas);
    this.titulos = columnas.map(c => c.titulo);
    this.alineacion = columnas.map(c => !!c.texto);
    this.filas = filas.map(f => columnas.map(c => ({
      valor: this.fmt(c.valor(f), c.dec),
      texto: !!c.texto
    })));
    this.totales = totales;
    this.filaTotales = this.armarTotales(columnas, totales);
    this.rangoSemana = desde && hasta ? `${String(desde).slice(0, 10)} al ${String(hasta).slice(0, 10)}` : '';
  }

  /**
   * Fila de totales: saldos suman, indicadores vienen del ponderado del backend.
   * Las columnas sin `totalKey` van en blanco: promediar una Edad o un Dif no
   * significa nada.
   */
  private armarTotales<T>(columnas: ColumnaResumen<T>[], totales: ResumenSemanalTotales): CeldaView[] {
    return columnas.map((col, i) => {
      if (i === 0) return { valor: `TOTAL (${totales.lotes})`, texto: true };
      if (!col.totalKey) return { valor: '', texto: false };
      if (col.totalKey === '__saldoHembras') return { valor: this.fmt(totales.saldoHembras, col.dec), texto: false };
      if (col.totalKey === '__saldoMachos') return { valor: this.fmt(totales.saldoMachos, col.dec), texto: false };
      return { valor: this.fmt(totales.ponderados?.[col.totalKey] ?? null, col.dec), texto: false };
    });
  }

  private recordarOpciones(regionales: (string | null)[], ciclos: (string | null)[]): void {
    const limpiar = (xs: (string | null)[]) =>
      Array.from(new Set(xs.filter((x): x is string => !!x && x.trim() !== ''))).sort();
    // Solo se amplían con el resultado SIN filtrar: si el usuario ya filtró por
    // regional, la lista no debe encogerse hasta dejar una sola opción.
    if (!this.regional) this.opcionesRegional = limpiar(regionales);
    if (!this.ciclo) this.opcionesCiclo = limpiar(ciclos);
  }

  private fmt(v: number | string | null, dec: number): string {
    if (v === null || v === undefined || v === '') return '—';
    if (typeof v === 'string') return v;
    return v.toLocaleString('es-CO', { minimumFractionDigits: dec, maximumFractionDigits: dec });
  }

  private limpiarResultado(): void {
    this.generado = false;
    this.grupos = [];
    this.titulos = [];
    this.alineacion = [];
    this.filas = [];
    this.filaTotales = [];
    this.totales = null;
    this.rangoSemana = '';
    this.respLevante = null;
    this.respProduccion = null;
  }

  exportarExcel(): void {
    const hojas = this.etapa === 'levante'
      ? (this.respLevante ? construirHojaResumenLevante(this.respLevante) : [])
      : (this.respProduccion ? construirHojaResumenProduccion(this.respProduccion) : []);

    if (hojas.length === 0 || this.filas.length === 0) {
      this.toast.info('No hay datos para exportar con los filtros actuales.');
      return;
    }

    const etapa = this.etapa === 'levante' ? 'Levante' : 'Produccion';
    exportarAoaMultiHojaExcel(hojas, {
      filenameFull: `Resumen_Semanal_RA_Pesadas_${etapa}_${this.anio}-S${this.semanaAnio}_${dateStampCompact()}.xlsx`
    });
  }

  trackFila = (i: number, _f: CeldaView[]) => i;
}

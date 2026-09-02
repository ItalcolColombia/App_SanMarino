import { ChangeDetectionStrategy, Component, Input, OnChanges, inject } from '@angular/core';
import { ChartData } from 'chart.js';
import { NgChartsModule } from 'ng2-charts';

import { formatearNumero } from '../../../../shared/utils/format';
import { construirDistribucion, opcionesDona } from '../../funciones/construir-distribucion.funcion';
import {
  construirSerieTiempo,
  opcionesLinea,
  rangoDiario
} from '../../funciones/construir-serie-tiempo.funcion';
import { FiltrosDashboard, Kpi } from '../../models/dashboard-metricas.model';
import { BloqueId } from '../../models/dashboard-panel.model';
import { DashboardEngorde, DashboardPanelesService } from '../../services/dashboard-paneles.service';
import { TarjetaKpiComponent } from '../tarjeta-kpi/tarjeta-kpi.component';

/**
 * Panel de POLLO ENGORDE: mortalidad, consumo y peso promedio por día, y lotes por granja.
 *
 * `Eager` porque tiene `subscribe` y estado mutable (regla de change detection del repo).
 */
@Component({
  selector: 'app-panel-engorde',
  standalone: true,
  imports: [NgChartsModule, TarjetaKpiComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['../panel.shared.scss'],
  templateUrl: './panel-engorde.component.html'
})
export class PanelEngordeComponent implements OnChanges {
  private readonly svc = inject(DashboardPanelesService);

  @Input({ required: true }) filtros!: FiltrosDashboard;
  @Input() bloques: readonly BloqueId[] = [];

  cargando = false;
  error: string | null = null;
  datos: DashboardEngorde | null = null;

  // Campos, NO getters: un getter que arma el ChartData devuelve una referencia nueva por ciclo y
  // dispara NG0103 (detección de cambios infinita).
  kpis: Kpi[] = [];
  serieMortalidad: ChartData<'line', (number | null)[], string> = { labels: [], datasets: [] };
  serieConsumo: ChartData<'line', (number | null)[], string> = { labels: [], datasets: [] };
  seriePeso: ChartData<'line', (number | null)[], string> = { labels: [], datasets: [] };
  lotesPorGranja: ChartData<'doughnut', number[], string> = { labels: [], datasets: [] };

  readonly opcionesMortalidad = opcionesLinea('Aves');
  readonly opcionesConsumo = opcionesLinea('Kg');
  readonly opcionesPeso = opcionesLinea('Gramos');
  readonly opcionesTorta = opcionesDona();

  ngOnChanges(): void {
    this.cargar();
  }

  ve(bloque: BloqueId): boolean {
    return this.bloques.includes(bloque);
  }

  cargar(): void {
    if (!this.filtros) return;
    this.cargando = true;
    this.error = null;

    const { desde, hasta } = this.filtros.periodo;

    this.svc.engorde(desde, hasta).subscribe({
      next: d => {
        this.datos = d;
        this.aplicar(d);
        this.cargando = false;
      },
      error: () => {
        this.datos = null;
        this.error = 'No se pudo cargar el panel de engorde.';
        this.cargando = false;
      }
    });
  }

  /** ¿El servidor respondió, pero no hay nada que mostrar? Distinto de «error» y de «cargando». */
  get sinDatos(): boolean {
    return !this.cargando && !this.error && !!this.datos && this.datos.diasConRegistro === 0;
  }

  private aplicar(d: DashboardEngorde): void {
    // El eje son TODOS los días del período: así un día sin seguimiento se ve como hueco.
    const eje = rangoDiario(this.filtros.periodo.desde, this.filtros.periodo.hasta);

    this.serieMortalidad = construirSerieTiempo(
      [{ etiqueta: 'Mortalidad', rol: 'alerta', puntos: d.mortalidadDiaria }], eje);
    this.serieConsumo = construirSerieTiempo(
      [{ etiqueta: 'Consumo (kg)', rol: 'principal', puntos: d.consumoDiarioKg }], eje);
    this.seriePeso = construirSerieTiempo(
      [{ etiqueta: 'Peso promedio (g)', rol: 'secundaria', puntos: d.pesoPromedioDiario }], eje);
    this.lotesPorGranja = construirDistribucion(d.lotesPorGranja, 'Lotes activos');

    const dias = d.diasConRegistro || 1;
    this.kpis = [
      {
        etiqueta: 'Mortalidad del período',
        valor: formatearNumero(Math.round(d.totalMortalidad)),
        detalle: `${formatearNumero(Math.round(d.totalMortalidad / dias))} aves/día en promedio`,
        tono: d.totalMortalidad > 0 ? 'alerta' : 'neutro'
      },
      {
        etiqueta: 'Alimento consumido',
        valor: `${formatearNumero(Math.round(d.totalConsumoKg))} kg`,
        detalle: `${formatearNumero(Math.round(d.totalConsumoKg / dias))} kg/día en promedio`
      },
      {
        etiqueta: 'Días con registro',
        valor: `${d.diasConRegistro}`,
        detalle: `de ${eje.length} del período`,
        tono: d.diasConRegistro < eje.length ? 'alerta' : 'exito'
      }
    ];
  }
}

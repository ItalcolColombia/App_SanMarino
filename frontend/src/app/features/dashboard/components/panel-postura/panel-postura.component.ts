import { ChangeDetectionStrategy, Component, Input, OnChanges, inject } from '@angular/core';
import { ChartData } from 'chart.js';
import { NgChartsModule } from 'ng2-charts';

import { formatearNumero } from '../../../../shared/utils/format';
import {
  construirDistribucion,
  opcionesDona
} from '../../funciones/construir-distribucion.funcion';
import {
  construirSerieTiempo,
  opcionesLinea,
  rangoDiario
} from '../../funciones/construir-serie-tiempo.funcion';
import { FiltrosDashboard, Kpi } from '../../models/dashboard-metricas.model';
import { BloqueId } from '../../models/dashboard-panel.model';
import { DashboardPanelesService, DashboardPostura } from '../../services/dashboard-paneles.service';
import { TarjetaKpiComponent } from '../tarjeta-kpi/tarjeta-kpi.component';

/**
 * Panel de POSTURA: mortalidad y huevo por día, y lotes activos por granja.
 *
 * `Eager` y no `OnPush`: tiene `subscribe` y estado mutable. Con `OnPush` (el default de Angular 22)
 * el spinner quedaría colgado aunque la request devolviera 200 — el bug recurrente del repo.
 */
@Component({
  selector: 'app-panel-postura',
  standalone: true,
  imports: [NgChartsModule, TarjetaKpiComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['../panel.shared.scss'],
  templateUrl: './panel-postura.component.html'
})
export class PanelPosturaComponent implements OnChanges {
  private readonly svc = inject(DashboardPanelesService);

  @Input({ required: true }) filtros!: FiltrosDashboard;
  @Input() bloques: readonly BloqueId[] = [];

  cargando = false;
  error: string | null = null;
  datos: DashboardPostura | null = null;

  // Campos, NO getters: un getter que arma el ChartData en cada ciclo devuelve una referencia nueva
  // por ciclo y dispara NG0103 (detección de cambios infinita).
  kpis: Kpi[] = [];
  serieMortalidad: ChartData<'line', (number | null)[], string> = { labels: [], datasets: [] };
  serieHuevo: ChartData<'line', (number | null)[], string> = { labels: [], datasets: [] };
  lotesPorGranja: ChartData<'doughnut', number[], string> = { labels: [], datasets: [] };

  readonly opcionesMortalidad = opcionesLinea('Aves');
  readonly opcionesHuevo = opcionesLinea('Huevos');
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

    this.svc.postura(desde, hasta).subscribe({
      next: d => {
        this.datos = d;
        this.aplicar(d);
        this.cargando = false;
      },
      error: () => {
        this.datos = null;
        this.error = 'No se pudo cargar el panel de postura.';
        this.cargando = false;
      }
    });
  }

  /** ¿El servidor respondió, pero no hay nada que mostrar? Es distinto de «error» y de «cargando». */
  get sinDatos(): boolean {
    return !this.cargando && !this.error && !!this.datos && this.datos.diasConRegistro === 0;
  }

  private aplicar(d: DashboardPostura): void {
    // El eje son TODOS los días del período, no sólo los que tienen dato: así un día sin
    // seguimiento cargado se ve como hueco y no desaparece del gráfico.
    const eje = rangoDiario(this.filtros.periodo.desde, this.filtros.periodo.hasta);

    this.serieMortalidad = construirSerieTiempo(
      [{ etiqueta: 'Mortalidad', rol: 'alerta', puntos: d.mortalidadDiaria }],
      eje
    );

    this.serieHuevo = construirSerieTiempo(
      [{ etiqueta: 'Huevo total', rol: 'principal', puntos: d.huevoDiario }],
      eje
    );

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
        etiqueta: 'Huevo del período',
        valor: formatearNumero(Math.round(d.totalHuevo)),
        detalle: `${formatearNumero(Math.round(d.totalHuevo / dias))} por día en promedio`
      },
      {
        etiqueta: 'Días con registro',
        valor: `${d.diasConRegistro}`,
        detalle: `de ${eje.length} del período`,
        // Faltan días cargados: no es un error del sistema, es trabajo pendiente en la granja.
        tono: d.diasConRegistro < eje.length ? 'alerta' : 'exito'
      }
    ];
  }
}

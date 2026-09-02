import { ChangeDetectionStrategy, Component, Input, OnInit, inject } from '@angular/core';
import { ChartData } from 'chart.js';
import { NgChartsModule } from 'ng2-charts';

import { formatearNumero } from '../../../../shared/utils/format';
import { construirBarras, opcionesBarras } from '../../funciones/construir-distribucion.funcion';
import { Kpi } from '../../models/dashboard-metricas.model';
import { BloqueId } from '../../models/dashboard-panel.model';
import {
  DashboardInventario,
  DashboardPanelesService
} from '../../services/dashboard-paneles.service';
import { TarjetaKpiComponent } from '../tarjeta-kpi/tarjeta-kpi.component';

/**
 * Panel de ALIMENTO E INVENTARIO: existencias por granja y galpones descuadrados.
 *
 * 🔴 **Las dos señales del cuadre van separadas.** `descuadreKg` son KILOS que faltan o sobran;
 * `filasNegativas` son DÍAS que cerraron en rojo con el total perfecto (está mal el orden o la fecha
 * de los ingresos). Mostrar un solo número que las mezcle es el error documentado en CLAUDE.md: la
 * consulta que las unía daba 23 galpones cuando los que tenían kilos eran 8.
 *
 * No recibe período: el stock y el cuadre son fotos del estado ACTUAL, no de una ventana de tiempo.
 * Por eso implementa `OnInit` y no `OnChanges`.
 */
@Component({
  selector: 'app-panel-alimento',
  standalone: true,
  imports: [NgChartsModule, TarjetaKpiComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['../panel.shared.scss'],
  templateUrl: './panel-alimento.component.html'
})
export class PanelAlimentoComponent implements OnInit {
  private readonly svc = inject(DashboardPanelesService);

  @Input() bloques: readonly BloqueId[] = [];

  cargando = false;
  error: string | null = null;
  datos: DashboardInventario | null = null;

  kpis: Kpi[] = [];
  stockPorGranja: ChartData<'bar', number[], string> = { labels: [], datasets: [] };

  readonly opcionesStock = opcionesBarras('Kg');

  ngOnInit(): void {
    this.cargar();
  }

  ve(bloque: BloqueId): boolean {
    return this.bloques.includes(bloque);
  }

  cargar(): void {
    this.cargando = true;
    this.error = null;

    this.svc.inventario().subscribe({
      next: d => {
        this.datos = d;
        this.aplicar(d);
        this.cargando = false;
      },
      error: () => {
        this.datos = null;
        this.error = 'No se pudo cargar el panel de inventario.';
        this.cargando = false;
      }
    });
  }

  private aplicar(d: DashboardInventario): void {
    this.stockPorGranja = construirBarras(d.stockPorGranja, 'Alimento (kg)');

    const totalKg = d.stockPorGranja.reduce((acc, g) => acc + g.valor, 0);

    this.kpis = [
      {
        etiqueta: 'Alimento en existencia',
        valor: `${formatearNumero(Math.round(totalKg))} kg`,
        detalle: `${d.stockPorGranja.length} granja(s) con existencias`
      },
      {
        etiqueta: 'Galpones descuadrados en KILOS',
        valor: `${d.galponesConKilos}`,
        detalle: 'Faltan o sobran kilos en el saldo',
        tono: d.galponesConKilos > 0 ? 'alerta' : 'exito'
      },
      {
        etiqueta: 'Galpones con días en rojo',
        valor: `${d.galponesConDiasEnRojo}`,
        // Es un problema DISTINTO al de arriba, no un subconjunto.
        detalle: 'El total cuadra; está mal el orden o la fecha de los ingresos',
        tono: d.galponesConDiasEnRojo > 0 ? 'alerta' : 'exito'
      }
    ];
  }
}

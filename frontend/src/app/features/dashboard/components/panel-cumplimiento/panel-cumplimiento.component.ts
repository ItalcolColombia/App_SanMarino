import { ChangeDetectionStrategy, Component, Input, OnInit, inject } from '@angular/core';
import { ChartData } from 'chart.js';
import { NgChartsModule } from 'ng2-charts';

import { construirBarras, opcionesBarras } from '../../funciones/construir-distribucion.funcion';
import { Kpi } from '../../models/dashboard-metricas.model';
import { BloqueId } from '../../models/dashboard-panel.model';
import {
  DashboardCumplimiento,
  DashboardPanelesService
} from '../../services/dashboard-paneles.service';
import { TarjetaKpiComponent } from '../tarjeta-kpi/tarjeta-kpi.component';

/**
 * Panel de CUMPLIMIENTO Y PENDIENTES: vacunación vencida/próxima y cuadres offline sin resolver.
 *
 * Los conteos de vacunación salen de `fn_vacunacion_pendientes`, que ya resuelve el alcance granular
 * y ya clasifica la situación. Acá no se recuenta nada: una segunda fórmula daría dos números
 * distintos para lo mismo.
 *
 * No recibe período: son pendientes de HOY, no de una ventana.
 */
@Component({
  selector: 'app-panel-cumplimiento',
  standalone: true,
  imports: [NgChartsModule, TarjetaKpiComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['../panel.shared.scss'],
  templateUrl: './panel-cumplimiento.component.html'
})
export class PanelCumplimientoComponent implements OnInit {
  private readonly svc = inject(DashboardPanelesService);

  @Input() bloques: readonly BloqueId[] = [];

  cargando = false;
  error: string | null = null;
  datos: DashboardCumplimiento | null = null;

  kpis: Kpi[] = [];
  porGranja: ChartData<'bar', number[], string> = { labels: [], datasets: [] };

  readonly opcionesPorGranja = opcionesBarras('Pendientes');

  ngOnInit(): void {
    this.cargar();
  }

  ve(bloque: BloqueId): boolean {
    return this.bloques.includes(bloque);
  }

  cargar(): void {
    this.cargando = true;
    this.error = null;

    this.svc.cumplimiento().subscribe({
      next: d => {
        this.datos = d;
        this.aplicar(d);
        this.cargando = false;
      },
      error: () => {
        this.datos = null;
        this.error = 'No se pudo cargar el panel de cumplimiento.';
        this.cargando = false;
      }
    });
  }

  /** Nada pendiente: se dice explícito, no se deja el panel en blanco. */
  get todoAlDia(): boolean {
    const d = this.datos;
    return !!d && d.vacunacionVencida === 0 && d.vacunacionProxima === 0 && d.cuadresSinResolver === 0;
  }

  private aplicar(d: DashboardCumplimiento): void {
    this.porGranja = construirBarras(d.vacunacionPorGranja, 'Pendientes de vacunación');

    const kpis: Kpi[] = [];

    if (this.ve('cumplimiento.vacunacion-pendiente')) {
      kpis.push(
        {
          etiqueta: 'Vacunación vencida o en franja',
          valor: `${d.vacunacionVencida}`,
          detalle: 'La fecha ya llegó y no se registró la aplicación',
          tono: d.vacunacionVencida > 0 ? 'alerta' : 'exito'
        },
        {
          etiqueta: 'Vacunación próxima',
          valor: `${d.vacunacionProxima}`,
          detalle: 'Vence dentro de los próximos 7 días'
        }
      );
    }

    if (this.ve('cumplimiento.cuadres-offline')) {
      kpis.push({
        etiqueta: 'Cuadres sin resolver',
        valor: `${d.cuadresSinResolver}`,
        detalle: 'Capturas sin red que quedaron con diferencia de stock',
        tono: d.cuadresSinResolver > 0 ? 'alerta' : 'exito'
      });
    }

    this.kpis = kpis;
  }
}

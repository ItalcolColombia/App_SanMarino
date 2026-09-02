import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

import { Kpi } from '../../models/dashboard-metricas.model';

/**
 * Una tarjeta de indicador.
 *
 * `OnPush` acá es correcto y deliberado: el componente es 100 % presentacional — se alimenta sólo de
 * `@Input()` y no tiene `subscribe`, timers ni estado mutable. Es la única excepción que la regla del
 * repo admite; todo lo que carga datos va en `Eager`.
 */
@Component({
  selector: 'app-tarjeta-kpi',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./tarjeta-kpi.component.scss'],
  template: `
    <article class="kpi" [class.kpi--alerta]="kpi.tono === 'alerta'" [class.kpi--exito]="kpi.tono === 'exito'">
      <p class="kpi__etiqueta">{{ kpi.etiqueta }}</p>
      <p class="kpi__valor">{{ kpi.valor }}</p>
      @if (kpi.detalle) {
        <p class="kpi__detalle">{{ kpi.detalle }}</p>
      }
    </article>
  `
})
export class TarjetaKpiComponent {
  @Input({ required: true }) kpi!: Kpi;
}

import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

import { fechaCortaSinTz } from '../../utils/format';
import type { CapturaPendienteResumen } from '../../offline/models/outbox.model';

/**
 * Fila de una captura hecha **sin red y todavía sin enviar**, para las tablas de seguimiento diario.
 *
 * ## Por qué es una fila aparte y no una fila más de la tabla
 *
 * Después de guardar sin red la fila es invisible: la pantalla recarga desde la caché de lectura,
 * que no la tiene. En F3 se decidió **no** meterla en el arreglo `seguimientos` porque ese arreglo
 * viaja a los indicadores, a la gráfica y al Excel, que **no pueden distinguirla** de una guardada —
 * y el servidor nunca la vio: un indicador calculado con ella es un número inventado.
 *
 * Esta fila resuelve la pregunta del galponero («¿dónde quedó lo que acabo de registrar?») sin
 * reabrir ese problema: **no muestra ni un solo número capturado**, sólo el día y el estado. No hay
 * nada que se pueda confundir con un dato confirmado, ni nada que copiar a una exportación.
 *
 * ## Uso
 *
 * Selector de atributo para no romper el `<table>`:
 * ```html
 * @for (captura of capturasPendientes; track captura.clientOpId) {
 *   <tr app-fila-captura-pendiente [captura]="captura"></tr>
 * }
 * ```
 *
 * `changeDetection: Eager` — convención del repo. El componente es presentacional, pero el costo de
 * equivocarse acá (una fila que no se repinta al confirmarse la captura) es exactamente el síntoma
 * que esta fila vino a eliminar.
 */
@Component({
  selector: 'tr[app-fila-captura-pendiente]',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './fila-captura-pendiente.component.html',
  styleUrls: ['./fila-captura-pendiente.component.scss'],
  host: { class: 'fila-captura-pendiente' }
})
export class FilaCapturaPendienteComponent {
  @Input({ required: true }) captura!: CapturaPendienteResumen;

  /**
   * `colspan` deliberadamente alto: estas tablas tienen entre 15 y 30 columnas y varias son
   * condicionales (empresa, género, flags). Los navegadores lo recortan al ancho real, así que un
   * número grande es correcto y no hay que mantenerlo sincronizado con cada columna nueva.
   */
  readonly COLSPAN = 99;

  /**
   * `fechaCortaSinTz`, no `fechaCorta`: la fecha del registro es una **fecha pura** (`YYYY-MM-DD`) y
   * `new Date('2026-08-12')` la interpreta como medianoche UTC ⇒ en Colombia se dibujaría 11/08.
   */
  get fecha(): string {
    return fechaCortaSinTz(this.captura?.fecha);
  }
}

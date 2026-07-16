// features/sincronizacion-panama/components/resultado-sincronizacion/resultado-sincronizacion.component.ts
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResultadoSincronizacionDto } from '../../models/sincronizacion-panama.model';
import { construirResumen } from '../../funciones/construir-resumen.funcion';
import { badgeEstadoLote, badgeEstadoResultado } from '../../funciones/estado-lote.funcion';
import { exportarLotesSincronizacion } from '../../funciones/exportar-lotes-excel.funcion';
import { formatearNumero, fechaCorta } from '../../../../shared/utils/format';

/**
 * Presentacional (sin DI de servicios): renderiza el resultado de una corrida — badge de estado +
 * duración, tarjetas de contadores, tabla por lote y lista de mensajes/advertencias. Se reutiliza
 * tal cual para la Previsualización (dry-run) y la Sincronización real. Todo derivado va por
 * `computed` (memoizado) para no reasignar arreglos/objetos por ciclo de change detection.
 */
@Component({
  selector: 'app-resultado-sincronizacion',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './resultado-sincronizacion.component.html',
  styleUrl: './resultado-sincronizacion.component.scss'
})
export class ResultadoSincronizacionComponent {
  readonly resultado = input.required<ResultadoSincronizacionDto>();

  /** Título contextual: dry-run vs real. */
  readonly titulo = computed(() => (this.resultado().dryRun ? 'Previsualización (dry-run)' : 'Sincronización'));
  readonly badge = computed(() => badgeEstadoResultado(this.resultado().estado));
  readonly tarjetas = computed(() => construirResumen(this.resultado()));

  /** Filas de la tabla con su badge precomputado (referencia estable hasta que cambie el resultado). */
  readonly filas = computed(() =>
    this.resultado().lotes.map((l) => ({ l, badge: badgeEstadoLote(l.estado) }))
  );

  readonly mensajes = computed(() => this.resultado().mensajes ?? []);

  /** Formato numérico central (delegación; devuelve string → sin riesgo de CD). */
  num(v: number): string { return formatearNumero(v ?? 0); }
  fecha(iso: string | null | undefined): string { return fechaCorta(iso); }

  exportar(): void {
    exportarLotesSincronizacion(this.resultado());
  }
}

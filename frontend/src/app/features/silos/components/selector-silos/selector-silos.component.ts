// src/app/features/silos/components/selector-silos/selector-silos.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FarmSiloDto } from '../../services/silos.service';

/**
 * Selector múltiple de silos/bodegas. Lo comparten la asignación al GALPÓN y la asignación al LOTE:
 * las dos son «elegí un subconjunto de los silos de la granja».
 *
 * Es 100 % presentacional (se alimenta de `@Input()` y emite por `@Output()`, sin HTTP ni estado
 * propio), así que acá `OnPush` es correcto — el criterio de CLAUDE.md, no una excepción.
 */
@Component({
  selector: 'app-selector-silos',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './selector-silos.component.html',
  styleUrls: ['./selector-silos.component.scss']
})
export class SelectorSilosComponent {
  /** Silos elegibles (ya vienen ordenados: bodegas primero, luego por número). */
  @Input() disponibles: FarmSiloDto[] = [];

  /** Ids seleccionados. Se reemplaza entero en cada cambio (referencia nueva ⇒ OnPush repinta). */
  @Input() seleccionados: number[] = [];

  @Input() disabled = false;
  @Input() cargando = false;

  /** Texto cuando no hay ninguno disponible (distinto según lo llame el galpón o el lote). */
  @Input() mensajeVacio = 'La granja todavía no tiene silos ni bodegas configurados.';

  @Output() seleccionadosChange = new EventEmitter<number[]>();

  estaSeleccionado(id: number): boolean {
    return this.seleccionados.includes(id);
  }

  alternar(id: number): void {
    if (this.disabled) return;
    const siguiente = this.estaSeleccionado(id)
      ? this.seleccionados.filter(x => x !== id)
      : [...this.seleccionados, id];
    this.seleccionadosChange.emit(siguiente);
  }

  seleccionarTodos(): void {
    if (this.disabled) return;
    this.seleccionadosChange.emit(this.disponibles.map(s => s.id));
  }

  limpiar(): void {
    if (this.disabled) return;
    this.seleccionadosChange.emit([]);
  }

  /** Etiqueta de la fila: la bodega no lleva número. */
  etiqueta(s: FarmSiloDto): string {
    return s.codigoErpUbicacion ? `${s.nombre} · ${s.codigoErpUbicacion}` : s.nombre;
  }

  trackById = (_: number, s: FarmSiloDto) => s.id;
}

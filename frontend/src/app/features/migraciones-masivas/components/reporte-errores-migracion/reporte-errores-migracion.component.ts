// features/migraciones-masivas/components/reporte-errores-migracion/reporte-errores-migracion.component.ts
import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MigracionError } from '../../models/migracion.model';
import { exportarErroresExcel } from '../../funciones/exportar-errores-excel.funcion';

const PAGINA = 200;

/**
 * Reporte de errores/advertencias de una corrida (validación o importación). Reutilizado tanto por
 * el panel de carga (resultado vigente) como por el historial (errores de una corrida pasada).
 * Render acotado a `PAGINA` filas con "Mostrar más"; el cap real del servidor (300) se comunica
 * aparte vía `totalErrores` cuando el caller lo tiene (el endpoint de historial no lo expone, ya
 * que la nota de truncado ya viaja como fila dentro del propio arreglo).
 */
@Component({
  selector: 'app-reporte-errores-migracion',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div *ngIf="errores().length" class="mt-4 rounded-lg border overflow-hidden"
         [ngClass]="hayErroresReales() ? 'border-red-200 bg-red-50/60' : 'border-amber-200 bg-amber-50/60'">
      <!-- Solo-advertencias NO es un fallo: el archivo pasó la validación. Decir "no se insertó
           ninguna fila con error" en ese caso hacía leer como rechazada una carga que está OK. -->
      <div class="px-4 py-2 flex flex-wrap items-center justify-between gap-2 text-sm font-semibold border-b"
           [ngClass]="hayErroresReales() ? 'text-red-700 border-red-200' : 'text-amber-800 border-amber-200'">
        <span *ngIf="hayErroresReales()">
          {{ conteoErrores() }} error(es) — no se insertó ninguna fila con error
        </span>
        <span *ngIf="!hayErroresReales()">
          ✅ Sin errores — {{ errores().length }} aviso(s) para revisar antes de importar
        </span>
        <button type="button" class="export-btn" (click)="exportar()">⬇️ Exportar detalle (.xlsx)</button>
      </div>

      <p *ngIf="excedeCap()" class="px-4 py-1.5 text-xs text-red-600 bg-red-50 border-b border-red-100">
        El servidor reportó {{ totalReportado() }} problema(s); se muestran los primeros {{ errores().length }}.
      </p>

      <div class="max-h-72 overflow-auto">
        <table class="w-full text-sm" aria-label="Errores y advertencias de la migración">
          <thead class="text-left text-gray-500 bg-white/70 sticky top-0">
            <tr>
              <th scope="col" class="px-3 py-2 font-medium">Fila</th>
              <th scope="col" class="px-3 py-2 font-medium">Columna</th>
              <th scope="col" class="px-3 py-2 font-medium">Valor</th>
              <th scope="col" class="px-3 py-2 font-medium">Mensaje</th>
              <th scope="col" class="px-3 py-2 font-medium">Severidad</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let e of erroresVisibles()" class="border-t"
                [ngClass]="hayErroresReales() ? 'border-red-100/70' : 'border-amber-100/70'">
              <!-- Fila 0 = mensaje del ARCHIVO, no de una fila (encabezados, saldo de inventario
                   proyectado, stock insuficiente). Mostrar "0" hacía buscar una fila que no existe. -->
              <td class="px-3 py-1.5 tabular-nums text-gray-700">{{ e.fila > 0 ? e.fila : '—' }}</td>
              <td class="px-3 py-1.5 text-gray-700">{{ e.columna }}</td>
              <td class="px-3 py-1.5 text-gray-500">{{ e.valor }}</td>
              <td class="px-3 py-1.5" [class.text-red-700]="e.severidad !== 'Advertencia'"
                  [class.text-gray-700]="e.severidad === 'Advertencia'">{{ e.mensaje }}</td>
              <td class="px-3 py-1.5">
                <span
                  class="px-2 py-0.5 rounded-full text-xs font-semibold"
                  [class.bg-amber-100]="e.severidad === 'Advertencia'"
                  [class.text-amber-700]="e.severidad === 'Advertencia'"
                  [class.bg-red-100]="e.severidad !== 'Advertencia'"
                  [class.text-red-700]="e.severidad !== 'Advertencia'">
                  {{ e.severidad }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div *ngIf="hayMas()" class="px-4 py-2 border-t text-center"
           [ngClass]="hayErroresReales() ? 'border-red-100' : 'border-amber-100'">
        <button type="button" class="mostrar-mas-btn" (click)="mostrarMas()">
          Mostrar más ({{ errores().length - erroresVisibles().length }} restante(s))
        </button>
      </div>
    </div>
  `,
  styles: [`
    .export-btn {
      display: inline-flex; align-items: center; gap: 0.3rem;
      padding: 0.3rem 0.65rem; border-radius: 0.6rem; font-size: 0.78rem; font-weight: 600;
      background: #fff; color: #b3261e; border: 1px solid rgba(179,38,30,0.35); cursor: pointer;
    }
    .export-btn:hover { background: #fdecec; }
    .mostrar-mas-btn {
      font-size: 0.82rem; font-weight: 600; color: var(--ital-orange-dark, #C85A0E);
      background: transparent; border: none; cursor: pointer; text-decoration: underline;
    }
    .mostrar-mas-btn:hover { color: var(--ital-orange, #F5821F); }
  `]
})
export class ReporteErroresMigracionComponent {
  readonly errores = input<MigracionError[]>([]);
  /** Total real (incluye lo que el back capó a 300); si no se pasa, se asume igual a `errores.length`. */
  readonly totalErrores = input<number | undefined>(undefined);
  /** Nombre base del archivo exportado (sin extensión); el caller le da contexto (tipo, corrida, etc.). */
  readonly nombreBase = input<string>('migracion');

  readonly limite = signal(PAGINA);
  readonly erroresVisibles = computed(() => this.errores().slice(0, this.limite()));
  readonly hayMas = computed(() => this.errores().length > this.limite());
  readonly totalReportado = computed(() => this.totalErrores() ?? this.errores().length);
  readonly excedeCap = computed(() => this.totalReportado() > this.errores().length);

  /** Cuántos registros son errores de verdad (el resto son avisos que NO impiden importar). */
  readonly conteoErrores = computed(() =>
    this.errores().filter((e) => e.severidad !== 'Advertencia').length
  );
  /**
   * Un reporte de SOLO advertencias no es un fallo: el archivo pasó la validación y se puede
   * importar. Mientras el panel decía "no se insertó ninguna fila con error" en ese caso, una carga
   * correcta se leía como rechazada.
   */
  readonly hayErroresReales = computed(() => this.conteoErrores() > 0);

  constructor() {
    // Cada vez que llega un arreglo de errores nuevo (nueva corrida) se reinicia la paginación local.
    effect(() => {
      this.errores();
      this.limite.set(PAGINA);
    });
  }

  mostrarMas(): void {
    this.limite.update((l) => l + PAGINA);
  }

  exportar(): void {
    exportarErroresExcel(this.errores(), this.nombreBase());
  }
}

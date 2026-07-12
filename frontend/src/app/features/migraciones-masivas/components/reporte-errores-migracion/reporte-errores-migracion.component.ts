// features/migraciones-masivas/components/reporte-errores-migracion/reporte-errores-migracion.component.ts
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MigracionError } from '../../models/migracion.model';

@Component({
  selector: 'app-reporte-errores-migracion',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div *ngIf="errores?.length" class="mt-4 rounded-lg border border-red-200 bg-red-50/60 overflow-hidden">
      <div class="px-4 py-2 text-sm font-semibold text-red-700 border-b border-red-200">
        {{ errores.length }} error(es) — no se insertó ninguna fila
      </div>
      <div class="max-h-72 overflow-auto">
        <table class="w-full text-sm">
          <thead class="text-left text-gray-500 bg-white/70 sticky top-0">
            <tr>
              <th class="px-3 py-2 font-medium">Fila</th>
              <th class="px-3 py-2 font-medium">Columna</th>
              <th class="px-3 py-2 font-medium">Valor</th>
              <th class="px-3 py-2 font-medium">Mensaje</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let e of errores" class="border-t border-red-100/70">
              <td class="px-3 py-1.5 tabular-nums text-gray-700">{{ e.fila }}</td>
              <td class="px-3 py-1.5 text-gray-700">{{ e.columna }}</td>
              <td class="px-3 py-1.5 text-gray-500">{{ e.valor }}</td>
              <td class="px-3 py-1.5 text-red-700">{{ e.mensaje }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class ReporteErroresMigracionComponent {
  @Input() errores: MigracionError[] = [];
}

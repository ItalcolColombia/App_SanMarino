// src/app/features/vacunacion/components/modal-item-cronograma/modal-item-cronograma.component.ts
import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import {
  LineaProductiva,
  UnidadObjetivo,
  UNIDAD_OBJETIVO_POR_LINEA,
  VacunacionCronogramaItemDto,
  VacunacionVacunaOpcionDto,
} from '../../models/vacunacion.model';

@Component({
  selector: 'app-modal-item-cronograma',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div *ngIf="open" class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div class="w-full max-w-lg rounded-lg bg-white shadow-xl">
        <div class="flex items-center justify-between border-b px-5 py-3">
          <h3 class="text-base font-semibold text-gray-800">
            {{ itemEditar ? 'Editar' : 'Nueva' }} vacuna del cronograma
          </h3>
          <button type="button" class="text-gray-400 hover:text-gray-600" (click)="cerrar()">✕</button>
        </div>

        <div class="space-y-4 px-5 py-4">
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700">Vacuna</label>
            <select class="w-full rounded border-gray-300 text-sm" [(ngModel)]="itemInventarioId" [disabled]="!!itemEditar">
              <option [ngValue]="null">Seleccione…</option>
              <option *ngFor="let v of vacunas" [ngValue]="v.id">{{ v.nombre }} ({{ v.codigo }})</option>
            </select>
          </div>

          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700">Programar por</label>
            <div class="flex gap-3 text-sm">
              <label class="flex items-center gap-1">
                <input type="radio" name="unidad" value="Semana" [(ngModel)]="unidadObjetivo" [disabled]="lineaProductiva === 'Engorde'" /> Semana
              </label>
              <label class="flex items-center gap-1">
                <input type="radio" name="unidad" value="Dia" [(ngModel)]="unidadObjetivo" /> Día (edad)
              </label>
              <label class="flex items-center gap-1">
                <input type="radio" name="unidad" value="Fecha" [(ngModel)]="unidadObjetivo" /> Fecha fija
              </label>
            </div>
          </div>

          <div *ngIf="unidadObjetivo !== 'Fecha'">
            <label class="mb-1 block text-sm font-medium text-gray-700">
              {{ unidadObjetivo === 'Semana' ? 'Semana de vida (N)' : 'Día de edad (N)' }}
            </label>
            <input type="number" min="1" class="w-full rounded border-gray-300 text-sm" [(ngModel)]="valorObjetivo" />
          </div>
          <div *ngIf="unidadObjetivo === 'Fecha'">
            <label class="mb-1 block text-sm font-medium text-gray-700">Fecha objetivo</label>
            <input type="date" class="w-full rounded border-gray-300 text-sm" [(ngModel)]="fechaObjetivo" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700">Franja: días antes</label>
              <input type="number" min="0" class="w-full rounded border-gray-300 text-sm" [(ngModel)]="rangoDiasAntes" />
            </div>
            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700">Franja: días después</label>
              <input type="number" min="0" class="w-full rounded border-gray-300 text-sm" [(ngModel)]="rangoDiasDespues" />
            </div>
          </div>

          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700">Notas (opcional)</label>
            <textarea rows="2" class="w-full rounded border-gray-300 text-sm" [(ngModel)]="notas"></textarea>
          </div>
        </div>

        <div class="flex justify-end gap-2 border-t px-5 py-3">
          <button type="button" class="rounded border px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-50" (click)="cerrar()">
            Cancelar
          </button>
          <button
            type="button"
            class="rounded px-3 py-1.5 text-sm font-medium text-white"
            style="background-color:#e85c25"
            [disabled]="guardando"
            (click)="guardar()"
          >
            {{ guardando ? 'Guardando…' : 'Guardar' }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ModalItemCronogramaComponent implements OnChanges {
  @Input() open = false;
  @Input() lineaProductiva: LineaProductiva = 'Levante';
  @Input() loteId!: number;
  @Input() vacunas: VacunacionVacunaOpcionDto[] = [];
  @Input() itemEditar: VacunacionCronogramaItemDto | null = null;

  @Output() cerrado = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  itemInventarioId: number | null = null;
  unidadObjetivo: UnidadObjetivo = 'Semana';
  valorObjetivo: number | null = 1;
  fechaObjetivo: string | null = null;
  rangoDiasAntes = 0;
  rangoDiasDespues = 6;
  notas = '';
  guardando = false;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) {
      this.resetForm();
    }
  }

  private resetForm(): void {
    if (this.itemEditar) {
      this.itemInventarioId = this.itemEditar.itemInventarioId;
      this.unidadObjetivo = this.itemEditar.unidadObjetivo;
      this.valorObjetivo = this.itemEditar.valorObjetivo;
      this.fechaObjetivo = this.itemEditar.fechaObjetivo ? this.itemEditar.fechaObjetivo.slice(0, 10) : null;
      this.rangoDiasAntes = this.itemEditar.rangoDiasAntes;
      this.rangoDiasDespues = this.itemEditar.rangoDiasDespues;
      this.notas = this.itemEditar.notas ?? '';
    } else {
      this.itemInventarioId = null;
      this.unidadObjetivo = UNIDAD_OBJETIVO_POR_LINEA[this.lineaProductiva];
      this.valorObjetivo = 1;
      this.fechaObjetivo = null;
      this.rangoDiasAntes = 0;
      this.rangoDiasDespues = this.unidadObjetivo === 'Semana' ? 6 : 1;
      this.notas = '';
    }
  }

  cerrar(): void {
    this.cerrado.emit();
  }

  async guardar(): Promise<void> {
    if (!this.itemInventarioId) {
      this.toast.warning('Seleccione una vacuna.');
      return;
    }
    if (this.unidadObjetivo !== 'Fecha' && (!this.valorObjetivo || this.valorObjetivo < 1)) {
      this.toast.warning('Indique la semana/día (mayor a 0).');
      return;
    }
    if (this.unidadObjetivo === 'Fecha' && !this.fechaObjetivo) {
      this.toast.warning('Indique la fecha objetivo.');
      return;
    }

    this.guardando = true;
    try {
      if (this.itemEditar) {
        await firstValueFrom(
          this.vacunacionSvc.actualizarItem(this.itemEditar.id, {
            itemInventarioId: this.itemInventarioId,
            unidadObjetivo: this.unidadObjetivo,
            valorObjetivo: this.unidadObjetivo === 'Fecha' ? null : this.valorObjetivo,
            fechaObjetivo: this.unidadObjetivo === 'Fecha' ? this.fechaObjetivo : null,
            rangoDiasAntes: this.rangoDiasAntes,
            rangoDiasDespues: this.rangoDiasDespues,
            orden: this.itemEditar.orden,
            activo: this.itemEditar.activo,
            notas: this.notas || null,
          })
        );
        this.toast.success('Ítem del cronograma actualizado.');
      } else {
        await firstValueFrom(
          this.vacunacionSvc.crearItem({
            lineaProductiva: this.lineaProductiva,
            loteId: this.loteId,
            itemInventarioId: this.itemInventarioId,
            unidadObjetivo: this.unidadObjetivo,
            valorObjetivo: this.unidadObjetivo === 'Fecha' ? null : this.valorObjetivo,
            fechaObjetivo: this.unidadObjetivo === 'Fecha' ? this.fechaObjetivo : null,
            rangoDiasAntes: this.rangoDiasAntes,
            rangoDiasDespues: this.rangoDiasDespues,
            notas: this.notas || null,
          })
        );
        this.toast.success('Vacuna agregada al cronograma.');
      }
      this.guardado.emit();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo guardar el ítem del cronograma.');
    } finally {
      this.guardando = false;
    }
  }
}

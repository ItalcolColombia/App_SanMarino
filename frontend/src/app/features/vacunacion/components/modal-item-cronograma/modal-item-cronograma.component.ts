// src/app/features/vacunacion/components/modal-item-cronograma/modal-item-cronograma.component.ts
import { Component, EventEmitter, HostListener, Input, Output, OnChanges, SimpleChanges } from '@angular/core';
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
    <div
      *ngIf="open"
      class="fixed inset-0 z-50 flex items-center justify-center p-4"
      style="background: rgba(0, 0, 0, 0.48); backdrop-filter: blur(2px)"
      (click)="cerrar()"
    >
      <div
        class="flex max-h-[90vh] w-full max-w-lg flex-col overflow-hidden rounded-2xl bg-white shadow-xl"
        style="border: 1px solid var(--ital-green-100)"
        role="dialog"
        aria-modal="true"
        aria-labelledby="titulo-modal-item"
        (click)="$event.stopPropagation()"
      >
        <div class="flex items-center justify-between border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <h3 id="titulo-modal-item" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
            {{ itemEditar ? 'Editar' : 'Nueva' }} vacuna del cronograma
          </h3>
          <button
            type="button"
            class="icon-btn"
            aria-label="Cerrar"
            (click)="cerrar()"
          >
            ✕
          </button>
        </div>

        <div class="flex-1 space-y-4 overflow-y-auto px-5 py-4">
          <div>
            <label class="form-label" for="mic-vacuna">Vacuna</label>
            <input
              *ngIf="!itemEditar && vacunas.length > 8"
              type="text"
              class="form-input mb-2"
              placeholder="🔍 Buscar vacuna…"
              [(ngModel)]="filtroVacuna"
              (ngModelChange)="aplicarFiltroVacunas()"
              aria-label="Buscar vacuna por nombre o código"
            />
            <select id="mic-vacuna" class="form-input" [(ngModel)]="itemInventarioId" [disabled]="!!itemEditar">
              <option [ngValue]="null">Seleccione…</option>
              <option *ngFor="let v of vacunasFiltradas" [ngValue]="v.id">{{ v.nombre }} ({{ v.codigo }})</option>
            </select>
            <p *ngIf="itemEditar" class="mt-1 text-xs" style="color: var(--ital-muted)">
              La vacuna no se cambia al editar; ajustá la programación o eliminá el ítem y creá otro.
            </p>
          </div>

          <div>
            <span class="form-label">Programar por</span>
            <div class="flex flex-wrap gap-3 text-sm" style="color: var(--ital-text)">
              <label class="flex items-center gap-1.5">
                <input type="radio" name="unidad" value="Semana" [(ngModel)]="unidadObjetivo" [disabled]="lineaProductiva === 'Engorde'" />
                Semana de vida
              </label>
              <label class="flex items-center gap-1.5">
                <input type="radio" name="unidad" value="Dia" [(ngModel)]="unidadObjetivo" /> Día (edad)
              </label>
              <label class="flex items-center gap-1.5">
                <input type="radio" name="unidad" value="Fecha" [(ngModel)]="unidadObjetivo" /> Fecha fija
              </label>
            </div>
          </div>

          <div *ngIf="unidadObjetivo !== 'Fecha'">
            <label class="form-label" for="mic-valor">
              {{ unidadObjetivo === 'Semana' ? 'Semana de vida (N)' : 'Día de edad (N)' }}
            </label>
            <input id="mic-valor" type="number" min="1" class="form-input" [(ngModel)]="valorObjetivo" />
          </div>
          <div *ngIf="unidadObjetivo === 'Fecha'">
            <label class="form-label" for="mic-fecha">Fecha objetivo</label>
            <input id="mic-fecha" type="date" class="form-input" [(ngModel)]="fechaObjetivo" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="form-label" for="mic-antes">Franja: días antes</label>
              <input id="mic-antes" type="number" min="0" class="form-input" [(ngModel)]="rangoDiasAntes" />
            </div>
            <div>
              <label class="form-label" for="mic-despues">Franja: días después</label>
              <input id="mic-despues" type="number" min="0" class="form-input" [(ngModel)]="rangoDiasDespues" />
            </div>
          </div>
          <p class="text-xs" style="color: var(--ital-muted)">
            La franja define la ventana válida de aplicación alrededor del objetivo; fuera de ella el registro exige motivo.
          </p>

          <div>
            <label class="form-label" for="mic-notas">Notas (opcional)</label>
            <textarea id="mic-notas" rows="2" class="form-input" [(ngModel)]="notas"></textarea>
          </div>
        </div>

        <div class="flex justify-end gap-2 border-t px-5 py-4" style="border-color: var(--ital-green-100); background: var(--ital-cream)">
          <button type="button" class="btn-ghost text-sm" (click)="cerrar()">Cancelar</button>
          <button type="button" class="btn-primary text-sm" [disabled]="guardando" (click)="guardar()">
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
  filtroVacuna = '';
  /** Lista memoizada (referencia estable): se recalcula solo al tipear, no por ciclo de CD. */
  vacunasFiltradas: VacunacionVacunaOpcionDto[] = [];
  guardando = false;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open && !this.guardando) this.cerrar();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['vacunas']) this.aplicarFiltroVacunas();
    if (changes['open'] && this.open) {
      this.resetForm();
    }
  }

  aplicarFiltroVacunas(): void {
    const q = this.filtroVacuna.trim().toLowerCase();
    this.vacunasFiltradas = !q
      ? this.vacunas
      : this.vacunas.filter((v) => v.nombre.toLowerCase().includes(q) || v.codigo.toLowerCase().includes(q));
  }

  private resetForm(): void {
    this.filtroVacuna = '';
    this.aplicarFiltroVacunas();
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

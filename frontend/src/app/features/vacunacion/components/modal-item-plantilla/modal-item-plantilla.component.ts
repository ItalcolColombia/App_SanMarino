// src/app/features/vacunacion/components/modal-item-plantilla/modal-item-plantilla.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, HostListener, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { LineaProductiva, VacunacionVacunaOpcionDto } from '../../models/vacunacion.model';
import { UnidadObjetivoPlantilla, VacunacionPlantillaItemDto } from '../../models/vacunacion-plantilla.model';

@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-modal-item-plantilla',
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
        aria-labelledby="titulo-modal-item-plantilla"
        (click)="$event.stopPropagation()"
      >
        <div class="flex items-center justify-between border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <h3 id="titulo-modal-item-plantilla" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
            {{ itemEditar ? 'Editar' : 'Agregar' }} vacuna del plan
          </h3>
          <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
        </div>

        <div class="flex-1 space-y-4 overflow-y-auto px-5 py-4">
          <div>
            <label class="form-label" for="mip-vacuna">Vacuna</label>
            <input
              *ngIf="vacunas.length > 8"
              type="text"
              class="form-input mb-2"
              placeholder="🔍 Buscar vacuna…"
              [(ngModel)]="filtroVacuna"
              (ngModelChange)="aplicarFiltroVacunas()"
              aria-label="Buscar vacuna por nombre o código"
            />
            <select id="mip-vacuna" class="form-input" [(ngModel)]="itemInventarioId">
              <option [ngValue]="null">Seleccione…</option>
              <option *ngFor="let v of vacunasFiltradas; trackBy: trackByVacuna" [ngValue]="v.id">
                {{ v.nombre }} ({{ v.codigo }})
              </option>
            </select>
          </div>

          <div>
            <span class="form-label">Programar por</span>
            <div class="flex flex-wrap gap-3 text-sm" style="color: var(--ital-text)">
              <label class="flex items-center gap-1.5">
                <input
                  type="radio"
                  name="unidad-plantilla"
                  value="Semana"
                  [(ngModel)]="unidadObjetivo"
                  [disabled]="lineaProductiva === 'Engorde'"
                />
                Semana de vida
              </label>
              <label class="flex items-center gap-1.5">
                <input type="radio" name="unidad-plantilla" value="Dia" [(ngModel)]="unidadObjetivo" /> Día de edad
              </label>
            </div>
            <p *ngIf="lineaProductiva === 'Engorde'" class="mt-1 text-xs" style="color: var(--ital-muted)">
              En Engorde va por día: el ciclo entero dura menos de 7 semanas y una franja semanal no distinguiría nada.
            </p>
            <p class="mt-1 text-xs" style="color: var(--ital-muted)">
              Una <b>fecha fija</b> no se puede plantillar —sería la misma para lotes encasetados en meses distintos—;
              esas siguen siendo ítems manuales del cronograma del lote.
            </p>
          </div>

          <div>
            <label class="form-label" for="mip-valor">
              {{ unidadObjetivo === 'Semana' ? 'Semana de vida (N)' : 'Día de edad (N)' }}
            </label>
            <input id="mip-valor" type="number" min="0" class="form-input" [(ngModel)]="valorObjetivo" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="form-label" for="mip-antes">Franja: días antes</label>
              <input id="mip-antes" type="number" min="0" class="form-input" [(ngModel)]="rangoDiasAntes" />
            </div>
            <div>
              <label class="form-label" for="mip-despues">Franja: días después</label>
              <input id="mip-despues" type="number" min="0" class="form-input" [(ngModel)]="rangoDiasDespues" />
            </div>
          </div>
          <p class="text-xs" style="color: var(--ital-muted)">
            La franja define la ventana válida de aplicación alrededor del objetivo; fuera de ella el registro exige motivo.
          </p>

          <div>
            <label class="form-label" for="mip-orden">Orden en el plan</label>
            <input id="mip-orden" type="number" min="0" class="form-input" [(ngModel)]="orden" />
          </div>

          <div>
            <label class="form-label" for="mip-notas">Notas (opcional)</label>
            <textarea id="mip-notas" rows="2" class="form-input" maxlength="2000" [(ngModel)]="notas"></textarea>
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
export class ModalItemPlantillaComponent implements OnChanges {
  @Input() open = false;
  @Input() plantillaId!: number;
  @Input() lineaProductiva: LineaProductiva = 'Levante';
  @Input() vacunas: VacunacionVacunaOpcionDto[] = [];
  @Input() itemEditar: VacunacionPlantillaItemDto | null = null;
  /** Cuántos ítems tiene ya la plantilla: el orden por defecto va al final, no encima del primero. */
  @Input() cantidadItems = 0;

  @Output() cerrado = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  readonly trackByVacuna = (_: number, v: VacunacionVacunaOpcionDto): number => v.id;

  itemInventarioId: number | null = null;
  unidadObjetivo: UnidadObjetivoPlantilla = 'Semana';
  valorObjetivo = 1;
  rangoDiasAntes = 0;
  rangoDiasDespues = 6;
  orden = 0;
  notas = '';
  filtroVacuna = '';
  /** Lista memoizada (referencia estable): se recalcula al tipear, no por ciclo de CD. */
  vacunasFiltradas: VacunacionVacunaOpcionDto[] = [];
  guardando = false;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open && !this.guardando) this.cerrar();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['vacunas']) this.aplicarFiltroVacunas();
    if (changes['open'] && this.open) this.resetForm();
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

    const i = this.itemEditar;
    if (i) {
      this.itemInventarioId = i.itemInventarioId;
      this.unidadObjetivo = i.unidadObjetivo;
      this.valorObjetivo = i.valorObjetivo;
      this.rangoDiasAntes = i.rangoDiasAntes;
      this.rangoDiasDespues = i.rangoDiasDespues;
      this.orden = i.orden;
      this.notas = i.notas ?? '';
      return;
    }

    this.itemInventarioId = null;
    this.unidadObjetivo = this.lineaProductiva === 'Engorde' ? 'Dia' : 'Semana';
    this.valorObjetivo = 1;
    this.rangoDiasAntes = 0;
    this.rangoDiasDespues = this.unidadObjetivo === 'Semana' ? 6 : 1;
    this.orden = this.cantidadItems;
    this.notas = '';
  }

  cerrar(): void {
    this.cerrado.emit();
  }

  async guardar(): Promise<void> {
    if (!this.itemInventarioId) {
      this.toast.warning('Seleccione una vacuna.');
      return;
    }
    if (this.valorObjetivo === null || this.valorObjetivo < 0) {
      this.toast.warning('Indique la semana/día (no puede ser negativo).');
      return;
    }

    const base = {
      itemInventarioId: this.itemInventarioId,
      unidadObjetivo: this.unidadObjetivo,
      valorObjetivo: this.valorObjetivo,
      rangoDiasAntes: this.rangoDiasAntes,
      rangoDiasDespues: this.rangoDiasDespues,
      orden: this.orden,
      notas: this.notas.trim() || null,
    };

    this.guardando = true;
    try {
      if (this.itemEditar) {
        await firstValueFrom(this.vacunacionSvc.actualizarItemPlantilla(this.plantillaId, this.itemEditar.id, base));
        this.toast.success('Vacuna del plan actualizada.');
      } else {
        await firstValueFrom(this.vacunacionSvc.crearItemPlantilla(this.plantillaId, base));
        this.toast.success('Vacuna agregada al plan.');
      }
      this.guardado.emit();
    } catch (err: any) {
      // Carga doble, unidad que no corresponde a la línea, vacuna de otra empresa: el motivo viene del backend.
      this.toast.error(err?.error?.error ?? 'No se pudo guardar la vacuna del plan.');
    } finally {
      this.guardando = false;
    }
  }
}

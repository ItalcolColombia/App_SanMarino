// src/app/features/vacunacion/components/modal-plantilla/modal-plantilla.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, HostListener, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { LineaProductiva, LINEA_PRODUCTIVA_LABEL } from '../../models/vacunacion.model';
import { VacunacionPlantillaDetalleDto } from '../../models/vacunacion-plantilla.model';

@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-modal-plantilla',
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
        aria-labelledby="titulo-modal-plantilla"
        (click)="$event.stopPropagation()"
      >
        <div class="flex items-center justify-between border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <h3 id="titulo-modal-plantilla" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
            {{ plantillaEditar ? 'Editar' : 'Nueva' }} plantilla del plan
          </h3>
          <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
        </div>

        <div class="flex-1 space-y-4 overflow-y-auto px-5 py-4">
          <div>
            <label class="form-label" for="mp-nombre">Nombre</label>
            <input
              id="mp-nombre"
              type="text"
              class="form-input"
              maxlength="200"
              placeholder="Ej.: Plan sanitario levante Ross 308"
              [(ngModel)]="nombre"
            />
          </div>

          <div>
            <label class="form-label" for="mp-linea">Línea productiva</label>
            <select id="mp-linea" class="form-input" [(ngModel)]="lineaProductiva">
              <option *ngFor="let l of lineas" [ngValue]="l">{{ lineaLabel[l] }}</option>
            </select>
            <p class="mt-1 text-xs" style="color: var(--ital-muted)">
              Postura se programa por semana de vida y Engorde por día: el ciclo de engorde entero dura menos de 7 semanas.
            </p>
          </div>

          <div>
            <label class="form-label" for="mp-raza">Raza (opcional)</label>
            <input
              id="mp-raza"
              type="text"
              class="form-input"
              maxlength="100"
              placeholder="Vacío = aplica a todas las razas de la línea"
              [(ngModel)]="raza"
            />
            <p class="mt-1 text-xs" style="color: var(--ital-muted)">
              Con raza, le gana a la plantilla general. Un lote <b>sin raza cargada</b> no puede tomar una plantilla de
              raza: adivinarla sería inventarle un plan sanitario.
            </p>
          </div>

          <div>
            <label class="form-label" for="mp-vigente">Vigente desde (opcional)</label>
            <input id="mp-vigente" type="date" class="form-input" [(ngModel)]="vigenteDesde" />
            <p class="mt-1 text-xs" style="color: var(--ital-muted)">
              Aplica a lotes encasetados desde esa fecha. Permite cambiar el plan sin reescribir el de los lotes que ya
              venían con el anterior.
            </p>
          </div>

          <div *ngIf="plantillaEditar">
            <label class="flex items-center gap-2 text-sm" style="color: var(--ital-text)">
              <input type="checkbox" [(ngModel)]="activa" /> Activa
            </label>
            <p class="mt-1 text-xs" style="color: var(--ital-muted)">
              Apagada, ningún lote la toma. Es la forma de retirar un plan sin perder su historia.
            </p>
          </div>

          <div>
            <label class="form-label" for="mp-notas">Notas (opcional)</label>
            <textarea id="mp-notas" rows="2" class="form-input" maxlength="2000" [(ngModel)]="notas"></textarea>
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
export class ModalPlantillaComponent implements OnChanges {
  @Input() open = false;
  @Input() plantillaEditar: VacunacionPlantillaDetalleDto | null = null;

  @Output() cerrado = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<VacunacionPlantillaDetalleDto>();

  readonly lineas: LineaProductiva[] = ['Levante', 'Produccion', 'Engorde'];
  readonly lineaLabel = LINEA_PRODUCTIVA_LABEL;

  nombre = '';
  lineaProductiva: LineaProductiva = 'Levante';
  raza = '';
  vigenteDesde: string | null = null;
  activa = true;
  notas = '';
  guardando = false;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open && !this.guardando) this.cerrar();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) this.resetForm();
  }

  private resetForm(): void {
    const p = this.plantillaEditar;
    this.nombre = p?.nombre ?? '';
    this.lineaProductiva = p?.lineaProductiva ?? 'Levante';
    this.raza = p?.raza ?? '';
    this.vigenteDesde = p?.vigenteDesde ? p.vigenteDesde.slice(0, 10) : null;
    this.activa = p?.activa ?? true;
    this.notas = p?.notas ?? '';
  }

  cerrar(): void {
    this.cerrado.emit();
  }

  async guardar(): Promise<void> {
    if (!this.nombre.trim()) {
      this.toast.warning('Indique el nombre de la plantilla.');
      return;
    }

    const base = {
      nombre: this.nombre.trim(),
      lineaProductiva: this.lineaProductiva,
      raza: this.raza.trim() || null,
      vigenteDesde: this.vigenteDesde || null,
      notas: this.notas.trim() || null,
    };

    this.guardando = true;
    try {
      const dto = this.plantillaEditar
        ? await firstValueFrom(
            this.vacunacionSvc.actualizarPlantilla(this.plantillaEditar.id, { ...base, activa: this.activa })
          )
        : await firstValueFrom(this.vacunacionSvc.crearPlantilla(base));

      this.toast.success(this.plantillaEditar ? 'Plantilla actualizada.' : 'Plantilla creada.');
      this.guardado.emit(dto);
    } catch (err: any) {
      // El backend explica por qué (duplicada, línea incompatible con sus ítems…): se muestra tal cual.
      this.toast.error(err?.error?.error ?? 'No se pudo guardar la plantilla.');
    } finally {
      this.guardando = false;
    }
  }
}

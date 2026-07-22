// src/app/features/implementacion/components/modal-firmar/modal-firmar.component.ts
// El participante VE el detalle de lo realizado y responde: firma digitada del recibido (con
// observación opcional) o "no firmo" → novedad con motivo obligatorio (la página luego lo guía a
// crear un ticket con ese motivo).
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { mensajeErrorHttp } from '../../funciones/resumen-firmas.funcion';
import { ImplementacionMiFirmaDto } from '../../models/implementacion.models';

export type ResultadoFirma = { accion: 'firmada' | 'rechazada'; firma: ImplementacionMiFirmaDto };

@Component({
  selector: 'app-modal-firmar-implementacion',
  standalone: true,
  imports: [CommonModule, FormsModule],
  styleUrls: ['../../styles/implementacion-shared.scss'],
  template: `
    <div
      *ngIf="open && item"
      class="fixed inset-0 z-50 flex items-center justify-center p-4"
      style="background: rgba(0, 0, 0, 0.48); backdrop-filter: blur(2px)"
      (click)="cerrar()"
    >
      <div
        class="flex max-h-[90vh] w-full max-w-lg flex-col overflow-hidden rounded-2xl bg-white shadow-xl"
        style="border: 1px solid var(--ital-green-100)"
        role="dialog"
        aria-modal="true"
        aria-labelledby="titulo-modal-firmar"
        (click)="$event.stopPropagation()"
      >
        <div class="border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <div class="flex items-center justify-between">
            <h3 id="titulo-modal-firmar" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
              {{ modoNovedad ? 'Registrar novedad' : 'Firmar recibido' }}
            </h3>
            <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
          </div>
        </div>

        <div class="flex-1 space-y-3 overflow-y-auto px-5 py-4 text-sm">
          <!-- Detalle de lo realizado -->
          <div class="rounded-xl border p-3" style="border-color: var(--ital-green-100); background: var(--ital-cream)">
            <p class="text-xs font-bold uppercase" style="color: var(--ital-muted)">
              {{ item!.planNombre }} · {{ item!.categoria }}
            </p>
            <p class="mt-0.5 font-semibold" style="color: var(--ital-text)">{{ item!.tareaTitulo }}</p>
            <p *ngIf="item!.tareaDescripcion" class="mt-1 text-xs" style="color: var(--ital-muted)">
              {{ item!.tareaDescripcion }}
            </p>
            <div class="mt-2 space-y-0.5 text-xs" style="color: var(--ital-muted)">
              <p *ngIf="item!.fechaProgramada">📅 Programado: {{ item!.fechaProgramada | date: 'dd/MM/yyyy' }}</p>
              <p *ngIf="item!.fechaCompletada">
                ✔️ Realizado el {{ item!.fechaCompletada | date: 'dd/MM/yyyy HH:mm' }}
                <ng-container *ngIf="item!.completadaPorNombre"> por {{ item!.completadaPorNombre }}</ng-container>
              </p>
              <p *ngIf="item!.implementadorNombre">👤 Encargado de la implementación: {{ item!.implementadorNombre }}</p>
            </div>
          </div>

          <!-- Modo firma -->
          <ng-container *ngIf="!modoNovedad">
            <div>
              <label class="form-label" for="mf-firma">Tu firma (digitá tu nombre completo) *</label>
              <input
                id="mf-firma"
                type="text"
                class="input-italfoods"
                maxlength="300"
                placeholder="Ej. Juan Pérez"
                [(ngModel)]="firmaTexto"
              />
              <p class="mt-1 text-xs" style="color: var(--ital-muted)">
                Al firmar confirmás que estuviste/recibiste este punto. Queda registrado con tu usuario,
                tu correo y la fecha.
              </p>
            </div>
            <div>
              <label class="form-label" for="mf-nota">Nota u observación (opcional)</label>
              <textarea id="mf-nota" rows="2" class="input-italfoods" maxlength="2000" [(ngModel)]="nota"></textarea>
            </div>
          </ng-container>

          <!-- Modo novedad -->
          <ng-container *ngIf="modoNovedad">
            <div
              class="rounded-xl border p-3 text-xs"
              style="border-color: var(--danger); color: var(--danger); background: color-mix(in srgb, var(--danger) 6%, transparent)"
            >
              Vas a registrar que <strong>no firmás</strong> este punto. Contanos el motivo: queda como
              novedad para el encargado y al guardar te guiamos a crear un ticket con ese detalle.
            </div>
            <div>
              <label class="form-label" for="mf-motivo">Motivo de la novedad *</label>
              <textarea
                id="mf-motivo"
                rows="3"
                class="input-italfoods"
                maxlength="2000"
                placeholder="Ej. No recibí la capacitación de este módulo / el punto quedó incompleto porque…"
                [(ngModel)]="motivo"
              ></textarea>
            </div>
          </ng-container>
        </div>

        <div
          class="flex flex-wrap justify-between gap-2 border-t px-5 py-4"
          style="border-color: var(--ital-green-100); background: var(--ital-cream)"
        >
          <button
            *ngIf="!modoNovedad"
            type="button"
            class="btn-italfoods-secondary text-sm"
            style="border-color: var(--danger); color: var(--danger)"
            (click)="modoNovedad = true"
          >
            No firmo · tengo una novedad
          </button>
          <button *ngIf="modoNovedad" type="button" class="btn-italfoods-secondary text-sm" (click)="modoNovedad = false">
            ← Volver a firmar
          </button>

          <div class="flex gap-2">
            <button type="button" class="btn-italfoods-secondary text-sm" (click)="cerrar()">Cancelar</button>
            <button *ngIf="!modoNovedad" type="button" class="btn-italfoods-primary text-sm" [disabled]="guardando" (click)="firmar()">
              {{ guardando ? 'Firmando…' : '✍️ Firmar recibido' }}
            </button>
            <button *ngIf="modoNovedad" type="button" class="btn-danger text-sm" [disabled]="guardando" (click)="rechazar()">
              {{ guardando ? 'Guardando…' : 'Registrar novedad' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class ModalFirmarImplementacionComponent implements OnChanges {
  @Input() open = false;
  @Input() item: ImplementacionMiFirmaDto | null = null;

  @Output() cerrado = new EventEmitter<void>();
  @Output() respondido = new EventEmitter<ResultadoFirma>();

  modoNovedad = false;
  firmaTexto = '';
  nota = '';
  motivo = '';
  guardando = false;

  constructor(private svc: ImplementacionService, private toast: ToastService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue) {
      this.modoNovedad = false;
      this.firmaTexto = '';
      this.nota = '';
      this.motivo = '';
      this.guardando = false;
    }
  }

  cerrar(): void {
    this.cerrado.emit();
  }

  async firmar(): Promise<void> {
    if (!this.item) return;
    if (this.firmaTexto.trim().length < 3) {
      this.toast.warning('Digitá tu nombre completo como firma (mínimo 3 caracteres).');
      return;
    }
    this.guardando = true;
    try {
      const firma = await firstValueFrom(
        this.svc.firmarTarea(this.item.tareaId, {
          firmaTexto: this.firmaTexto.trim(),
          nota: this.nota.trim() || null,
        })
      );
      this.respondido.emit({ accion: 'firmada', firma });
    } catch (err: any) {
      this.toast.error(mensajeErrorHttp(err, 'No se pudo registrar tu firma.'));
    } finally {
      this.guardando = false;
    }
  }

  async rechazar(): Promise<void> {
    if (!this.item) return;
    if (this.motivo.trim().length < 5) {
      this.toast.warning('Contanos el motivo de la novedad (mínimo 5 caracteres).');
      return;
    }
    this.guardando = true;
    try {
      const firma = await firstValueFrom(
        this.svc.rechazarTarea(this.item.tareaId, { motivo: this.motivo.trim() })
      );
      this.respondido.emit({ accion: 'rechazada', firma });
    } catch (err: any) {
      this.toast.error(mensajeErrorHttp(err, 'No se pudo registrar la novedad.'));
    } finally {
      this.guardando = false;
    }
  }
}

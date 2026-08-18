// src/app/features/vacunacion/components/modal-aplicar-plantilla/modal-aplicar-plantilla.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, HostListener, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { VacunacionPlantillaDto } from '../../models/vacunacion-plantilla.model';
import { VacunacionMaterializacionMasivaDto } from '../../models/vacunacion-materializador.model';
import {
  resumirImpactoLote,
  resumirImpactoMasivo,
} from '../../funciones/resumir-impacto-materializacion.funcion';

/**
 * Vista previa obligatoria antes de bajar una plantilla a los cronogramas de sus lotes.
 *
 * <p>El botón no aparece antes que el impacto: es la única pantalla del módulo que escribe sobre
 * lotes vivos, y lo que se muestra acá es literalmente lo que se va a escribir —el backend calcula
 * preview y aplicación con la misma función—. Después de aplicar, el mismo cuadro se queda en
 * pantalla como informe de lo que se hizo.</p>
 */
@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-modal-aplicar-plantilla',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      *ngIf="open"
      class="fixed inset-0 z-50 flex items-center justify-center p-4"
      style="background: rgba(0, 0, 0, 0.48); backdrop-filter: blur(2px)"
      (click)="cerrar()"
    >
      <div
        class="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl"
        style="border: 1px solid var(--ital-green-100)"
        role="dialog"
        aria-modal="true"
        aria-labelledby="titulo-modal-aplicar"
        (click)="$event.stopPropagation()"
      >
        <div class="flex items-center justify-between border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <div>
            <h3 id="titulo-modal-aplicar" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
              {{ informe?.lotes?.length && aplicado ? 'Plan aplicado' : 'Aplicar el plan a los lotes' }}
            </h3>
            <p class="text-xs" style="color: var(--ital-muted)">{{ plantilla?.nombre }}</p>
          </div>
          <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
        </div>

        <div class="flex-1 overflow-y-auto px-5 py-4">
          <p *ngIf="cargando" class="py-8 text-center text-sm" style="color: var(--ital-muted)">
            Calculando el impacto…
          </p>

          <p *ngIf="!cargando && error" class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
            {{ error }}
          </p>

          <ng-container *ngIf="!cargando && !error && informe as inf">
            <div
              class="rounded-lg border px-4 py-3 text-sm"
              [style.background]="aplicado ? 'var(--ital-green-50, #f0f7f2)' : 'var(--ital-cream)'"
              style="border-color: var(--ital-green-100); color: var(--ital-text)"
            >
              {{ resumen }}
            </div>

            <p *ngIf="!aplicado && inf.conteos.sobrantes > 0" class="mt-3 text-xs" style="color: var(--ital-muted)">
              Los <b>sobrantes</b> son vacunas que ya se habían programado y que hoy no están en el plan. No se borran:
              pueden tener aplicación registrada. Si de verdad sobran, se quitan desde el cronograma del lote.
            </p>

            <p *ngIf="inf.lotesConError > 0" class="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
              {{ inf.lotesConError }} lote(s) no se pudieron aplicar. Los demás sí — cada lote se aplica por separado.
            </p>

            <table *ngIf="inf.lotes.length" class="mt-4 w-full text-sm">
              <thead>
                <tr class="border-b text-left text-xs uppercase" style="border-color: var(--ital-green-100); color: var(--ital-muted)">
                  <th class="py-2 pr-3 font-semibold">Lote</th>
                  <th class="py-2 pr-3 font-semibold">Galpón</th>
                  <th class="py-2 font-semibold">{{ aplicado ? 'Qué se hizo' : 'Qué pasaría' }}</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let lote of inf.lotes" class="border-b align-top" style="border-color: var(--ital-green-50, #eef4f0)">
                  <td class="py-2 pr-3 font-medium" style="color: var(--ital-text)">{{ lote.loteNombre || lote.loteId }}</td>
                  <td class="py-2 pr-3" style="color: var(--ital-muted)">{{ lote.galponId || '—' }}</td>
                  <td class="py-2" style="color: var(--ital-muted)">
                    <span *ngIf="!lote.error">{{ resumirLote(lote.conteos) }}</span>
                    <span *ngIf="lote.error" class="text-red-700">{{ lote.error }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </ng-container>
        </div>

        <div
          class="flex justify-end gap-2 border-t px-5 py-4"
          style="border-color: var(--ital-green-100); background: var(--ital-cream)"
        >
          <button type="button" class="btn-ghost text-sm" (click)="cerrar()">
            {{ aplicado ? 'Cerrar' : 'Cancelar' }}
          </button>
          <button
            *ngIf="!aplicado"
            type="button"
            class="btn-primary text-sm"
            [disabled]="cargando || aplicando || !puedeAplicar"
            (click)="aplicar()"
          >
            {{ aplicando ? 'Aplicando…' : textoBotonAplicar }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ModalAplicarPlantillaComponent implements OnChanges {
  @Input() open = false;
  @Input() plantilla: VacunacionPlantillaDto | null = null;

  @Output() cerrado = new EventEmitter<void>();
  /** Se emite sólo si se escribió algo, para que la pantalla que lo abrió refresque. */
  @Output() aplicadoOk = new EventEmitter<VacunacionMaterializacionMasivaDto>();

  informe: VacunacionMaterializacionMasivaDto | null = null;
  resumen = '';
  cargando = false;
  aplicando = false;
  aplicado = false;
  error: string | null = null;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open && !this.aplicando) this.cerrar();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) void this.cargarPreview();
  }

  get puedeAplicar(): boolean {
    return !!this.informe?.conteos.escribeAlgo;
  }

  get textoBotonAplicar(): string {
    const n = this.informe?.lotesQueEscriben ?? 0;
    if (!this.puedeAplicar) return 'No hay nada para escribir';
    return n === 1 ? 'Aplicar a 1 lote' : `Aplicar a ${n} lotes`;
  }

  resumirLote = resumirImpactoLote;

  private async cargarPreview(): Promise<void> {
    this.informe = null;
    this.resumen = '';
    this.error = null;
    this.aplicado = false;

    const id = this.plantilla?.id;
    if (!id) return;

    this.cargando = true;
    try {
      this.informe = await firstValueFrom(this.vacunacionSvc.previewMaterializacionPlantilla(id));
      this.resumen = resumirImpactoMasivo(this.informe);
    } catch (err: any) {
      this.error = err?.error?.error ?? 'No se pudo calcular el impacto.';
    } finally {
      this.cargando = false;
    }
  }

  async aplicar(): Promise<void> {
    const id = this.plantilla?.id;
    if (!id || !this.puedeAplicar) return;

    this.aplicando = true;
    try {
      this.informe = await firstValueFrom(this.vacunacionSvc.aplicarMaterializacionPlantilla(id));
      this.resumen = resumirImpactoMasivo(this.informe);
      this.aplicado = true;

      if (this.informe.lotesConError > 0) {
        this.toast.warning(`Se aplicó el plan, con ${this.informe.lotesConError} lote(s) fallidos.`);
      } else {
        this.toast.success('El plan quedó aplicado a los lotes.');
      }
      this.aplicadoOk.emit(this.informe);
    } catch (err: any) {
      this.error = err?.error?.error ?? 'No se pudo aplicar el plan.';
      this.toast.error(this.error!);
    } finally {
      this.aplicando = false;
    }
  }

  cerrar(): void {
    this.cerrado.emit();
  }
}

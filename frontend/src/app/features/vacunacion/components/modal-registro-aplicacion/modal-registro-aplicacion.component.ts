// src/app/features/vacunacion/components/modal-registro-aplicacion/modal-registro-aplicacion.component.ts
import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { VacunacionCronogramaItemDto } from '../../models/vacunacion.model';

@Component({
  selector: 'app-modal-registro-aplicacion',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div *ngIf="open && item" class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div class="w-full max-w-lg rounded-lg bg-white shadow-xl">
        <div class="flex items-center justify-between border-b px-5 py-3">
          <h3 class="text-base font-semibold text-gray-800">Registrar aplicación — {{ item.itemInventarioNombre }}</h3>
          <button type="button" class="text-gray-400 hover:text-gray-600" (click)="cerrar()">✕</button>
        </div>

        <div class="space-y-4 px-5 py-4 text-sm">
          <p class="text-gray-600">
            Lote <strong>{{ item.loteNombre }}</strong> — franja programada:
            <strong>{{ item.fechaInicioFranja | date: 'dd/MM/yyyy' }} — {{ item.fechaFinFranja | date: 'dd/MM/yyyy' }}</strong>
          </p>

          <div class="flex gap-3">
            <label class="flex items-center gap-1">
              <input type="radio" name="modo" [value]="'aplicado'" [(ngModel)]="modo" /> Aplicado
            </label>
            <label class="flex items-center gap-1">
              <input type="radio" name="modo" [value]="'no-aplicado'" [(ngModel)]="modo" /> No aplicado
            </label>
          </div>

          <div *ngIf="modo === 'aplicado'" class="space-y-3">
            <p class="text-xs text-gray-500">
              La fecha de aplicación es la de hoy (la fija el sistema, no es editable).
            </p>
            <div>
              <label class="mb-1 block font-medium text-gray-700">Aplicado por</label>
              <div class="flex gap-3">
                <label class="flex items-center gap-1">
                  <input type="radio" name="aplicadoPorModo" value="libre" [(ngModel)]="aplicadoPorModo" /> Nombre libre
                </label>
                <label class="flex items-center gap-1">
                  <input type="radio" name="aplicadoPorModo" value="usuario" [(ngModel)]="aplicadoPorModo" /> Usuario del sistema
                </label>
              </div>
            </div>
            <input
              *ngIf="aplicadoPorModo === 'libre'"
              type="text"
              class="w-full rounded border-gray-300 text-sm"
              placeholder="Nombre del responsable"
              [(ngModel)]="aplicadoPorNombreLibre"
            />
            <input
              *ngIf="aplicadoPorModo === 'usuario'"
              type="number"
              class="w-full rounded border-gray-300 text-sm"
              placeholder="ID de usuario del sistema"
              [(ngModel)]="aplicadoPorUserId"
            />
          </div>

          <div>
            <label class="mb-1 block font-medium text-gray-700">
              Motivo {{ requiereMotivoSiempre() ? '(obligatorio)' : '(obligatorio solo si quedó fuera de la franja programada)' }}
            </label>
            <textarea rows="3" class="w-full rounded border-gray-300 text-sm" [(ngModel)]="motivo"></textarea>
          </div>
        </div>

        <div class="flex justify-end gap-2 border-t px-5 py-3">
          <button type="button" class="rounded border px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-50" (click)="cerrar()">
            Cancelar
          </button>
          <button
            type="button"
            class="rounded px-3 py-1.5 text-sm font-medium text-white"
            style="background-color:#2d7a3e"
            [disabled]="guardando"
            (click)="confirmar()"
          >
            {{ guardando ? 'Guardando…' : 'Confirmar' }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ModalRegistroAplicacionComponent implements OnChanges {
  @Input() open = false;
  @Input() item: VacunacionCronogramaItemDto | null = null;

  @Output() cerrado = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  modo: 'aplicado' | 'no-aplicado' = 'aplicado';
  aplicadoPorModo: 'libre' | 'usuario' = 'libre';
  aplicadoPorNombreLibre = '';
  aplicadoPorUserId: number | null = null;
  motivo = '';
  guardando = false;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) {
      this.modo = 'aplicado';
      this.aplicadoPorModo = 'libre';
      this.aplicadoPorNombreLibre = '';
      this.aplicadoPorUserId = null;
      this.motivo = '';
    }
  }

  requiereMotivoSiempre(): boolean {
    return this.modo === 'no-aplicado';
  }

  cerrar(): void {
    this.cerrado.emit();
  }

  async confirmar(): Promise<void> {
    if (!this.item) return;

    if (this.modo === 'no-aplicado' && !this.motivo.trim()) {
      this.toast.warning('El motivo es obligatorio para marcar "no aplicado".');
      return;
    }
    if (this.modo === 'aplicado') {
      const tieneLibre = this.aplicadoPorModo === 'libre' && this.aplicadoPorNombreLibre.trim().length > 0;
      const tieneUsuario = this.aplicadoPorModo === 'usuario' && !!this.aplicadoPorUserId;
      if (!tieneLibre && !tieneUsuario) {
        this.toast.warning('Indique quién aplicó la vacuna (nombre libre o usuario del sistema).');
        return;
      }
    }

    this.guardando = true;
    try {
      if (this.modo === 'aplicado') {
        await firstValueFrom(
          this.vacunacionSvc.registrarAplicado(this.item.id, {
            motivoDescripcion: this.motivo.trim() || null,
            aplicadoPorUserId: this.aplicadoPorModo === 'usuario' ? this.aplicadoPorUserId : null,
            aplicadoPorNombreLibre: this.aplicadoPorModo === 'libre' ? this.aplicadoPorNombreLibre.trim() : null,
          })
        );
        this.toast.success('Aplicación registrada.');
      } else {
        await firstValueFrom(
          this.vacunacionSvc.registrarNoAplicado(this.item.id, { motivoDescripcion: this.motivo.trim() })
        );
        this.toast.success('Registrado como no aplicado.');
      }
      this.guardado.emit();
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo registrar la aplicación.');
    } finally {
      this.guardando = false;
    }
  }
}

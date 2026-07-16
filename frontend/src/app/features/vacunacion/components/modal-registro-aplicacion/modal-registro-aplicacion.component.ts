// src/app/features/vacunacion/components/modal-registro-aplicacion/modal-registro-aplicacion.component.ts
import { Component, EventEmitter, HostListener, Input, Output, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { VacunacionCronogramaItemDto, VacunacionUsuarioOpcionDto } from '../../models/vacunacion.model';

@Component({
  selector: 'app-modal-registro-aplicacion',
  standalone: true,
  imports: [CommonModule, FormsModule],
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
        aria-labelledby="titulo-modal-registro"
        (click)="$event.stopPropagation()"
      >
        <div class="flex items-center justify-between border-b px-5 py-4" style="border-color: var(--ital-green-100)">
          <h3 id="titulo-modal-registro" class="text-base font-extrabold" style="color: var(--ital-orange-dark)">
            Registrar aplicación — {{ item.itemInventarioNombre }}
          </h3>
          <button type="button" class="icon-btn" aria-label="Cerrar" (click)="cerrar()">✕</button>
        </div>

        <div class="flex-1 space-y-4 overflow-y-auto px-5 py-4 text-sm">
          <div class="rounded-xl px-3 py-2" style="background: var(--ital-cream); color: var(--ital-text)">
            Lote <strong>{{ item.loteNombre }}</strong> · franja programada:
            <strong>{{ item.fechaInicioFranja | date: 'dd/MM/yyyy' }} — {{ item.fechaFinFranja | date: 'dd/MM/yyyy' }}</strong>
          </div>

          <div>
            <span class="form-label">Resultado</span>
            <div class="flex flex-wrap gap-3" style="color: var(--ital-text)">
              <label class="flex items-center gap-1.5">
                <input type="radio" name="modo" [value]="'aplicado'" [(ngModel)]="modo" /> Aplicado
              </label>
              <label class="flex items-center gap-1.5">
                <input type="radio" name="modo" [value]="'no-aplicado'" [(ngModel)]="modo" /> No aplicado
              </label>
            </div>
          </div>

          <div *ngIf="modo === 'aplicado'" class="space-y-3">
            <p class="text-xs" style="color: var(--ital-muted)">
              La fecha de aplicación es la de hoy (la fija el sistema, no es editable).
            </p>
            <div>
              <span class="form-label">Aplicado por</span>
              <div class="flex flex-wrap gap-3" style="color: var(--ital-text)">
                <label class="flex items-center gap-1.5">
                  <input type="radio" name="aplicadoPorModo" value="usuario" [(ngModel)]="aplicadoPorModo" [disabled]="!usuarios.length" />
                  Usuario del sistema
                </label>
                <label class="flex items-center gap-1.5">
                  <input type="radio" name="aplicadoPorModo" value="libre" [(ngModel)]="aplicadoPorModo" /> Nombre libre
                </label>
              </div>
            </div>
            <select
              *ngIf="aplicadoPorModo === 'usuario'"
              class="form-input"
              [(ngModel)]="aplicadoPorUserId"
              aria-label="Usuario del sistema que aplicó la vacuna"
            >
              <option [ngValue]="null">Seleccione el usuario…</option>
              <option *ngFor="let u of usuarios" [ngValue]="u.id">{{ u.nombre ?? 'Usuario ' + u.id }}</option>
            </select>
            <input
              *ngIf="aplicadoPorModo === 'libre'"
              type="text"
              class="form-input"
              placeholder="Nombre del responsable"
              [(ngModel)]="aplicadoPorNombreLibre"
              aria-label="Nombre libre del responsable"
            />
          </div>

          <div>
            <label class="form-label" for="mra-motivo">
              Motivo {{ modo === 'no-aplicado' ? '(obligatorio)' : '(obligatorio solo si quedó fuera de la franja)' }}
            </label>
            <textarea id="mra-motivo" rows="3" class="form-input" [(ngModel)]="motivo"></textarea>
          </div>
        </div>

        <div class="flex justify-end gap-2 border-t px-5 py-4" style="border-color: var(--ital-green-100); background: var(--ital-cream)">
          <button type="button" class="btn-ghost text-sm" (click)="cerrar()">Cancelar</button>
          <button type="button" class="btn-primary text-sm" [disabled]="guardando" (click)="confirmar()">
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
  @Input() usuarios: VacunacionUsuarioOpcionDto[] = [];

  @Output() cerrado = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  modo: 'aplicado' | 'no-aplicado' = 'aplicado';
  aplicadoPorModo: 'libre' | 'usuario' = 'usuario';
  aplicadoPorNombreLibre = '';
  aplicadoPorUserId: number | null = null;
  motivo = '';
  guardando = false;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open && !this.guardando) this.cerrar();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) {
      this.modo = 'aplicado';
      this.aplicadoPorModo = this.usuarios.length ? 'usuario' : 'libre';
      this.aplicadoPorNombreLibre = '';
      this.aplicadoPorUserId = null;
      this.motivo = '';
    }
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
        this.toast.warning('Indique quién aplicó la vacuna (usuario del sistema o nombre libre).');
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

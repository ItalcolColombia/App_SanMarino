// src/app/features/implementacion/components/panel-pendientes-firma/panel-pendientes-firma.component.ts
// Panel desplegable del inicio: lo que el usuario tiene pendiente de aceptar (capacitaciones y
// entregas que el encargado ya dio por realizadas). Cada punto se abre, se lee y se firma a mano.
// Si no hay nada pendiente el panel no se dibuja: el inicio no se ensucia con una tarjeta vacía.
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ImplementacionService } from '../../services/implementacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ModalFirmarImplementacionComponent, ResultadoFirma } from '../modal-firmar/modal-firmar.component';
import { ImplementacionMiFirmaDto, TIPO_PLAN_LABEL } from '../../models/implementacion.models';

@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-panel-pendientes-firma',
  standalone: true,
  imports: [CommonModule, RouterModule, ModalFirmarImplementacionComponent],
  // Las clases .pendientes-* se mudaron a shared/ al sumarse la bandeja de Vacunación (W3):
  // mismas declaraciones, un solo dueño.
  styleUrls: ['../../../../shared/styles/pendientes-panel.scss'],
  template: `
    <section *ngIf="cargando || pendientes.length" class="pendientes-panel">
      <button type="button" class="pendientes-panel__head" [attr.aria-expanded]="abierto" (click)="abierto = !abierto">
        <span class="pendientes-panel__chevron" [class.pendientes-panel__chevron--abierto]="abierto" aria-hidden="true">▸</span>
        <span class="pendientes-panel__titulo">
          Pendientes de tu aceptación
          <span *ngIf="!cargando" class="pendientes-panel__badge">{{ pendientes.length }}</span>
        </span>
        <span class="pendientes-panel__sub">
          {{ cargando ? 'Buscando…' : 'Firmá lo que ya se te entregó o capacitó' }}
        </span>
      </button>

      <div *ngIf="abierto && !cargando" class="pendientes-panel__body">
        <article *ngFor="let p of pendientes; trackBy: trackByFirma" class="pendientes-item">
          <div class="pendientes-item__info">
            <p class="pendientes-item__meta">{{ p.planNombre }} · {{ etiqueta(p) }}</p>
            <p class="pendientes-item__titulo">{{ p.tareaTitulo }}</p>
            <p *ngIf="p.tareaDescripcion" class="pendientes-item__desc">{{ p.tareaDescripcion }}</p>
            <p class="pendientes-item__fecha">
              <ng-container *ngIf="p.fechaCompletada">
                Realizado el {{ p.fechaCompletada | date: 'dd/MM/yyyy' }}
                <ng-container *ngIf="p.completadaPorNombre"> por {{ p.completadaPorNombre }}</ng-container>
              </ng-container>
              <ng-container *ngIf="!p.fechaCompletada">Listo para tu aceptación</ng-container>
            </p>
          </div>
          <button type="button" class="btn-italfoods-primary text-sm whitespace-nowrap" (click)="abrirFirma(p)">
            ✍️ Ver y firmar
          </button>
        </article>

        <a routerLink="/implementacion/mis-tareas" class="pendientes-panel__link">Ver todo mi historial de firmas →</a>
      </div>
    </section>

    <app-modal-firmar-implementacion
      [open]="modalAbierto"
      [item]="seleccionado"
      (cerrado)="cerrarFirma()"
      (respondido)="onRespondido($event)"
    ></app-modal-firmar-implementacion>
  `,
})
export class PanelPendientesFirmaComponent implements OnInit {
  readonly tipoLabel = TIPO_PLAN_LABEL;
  readonly trackByFirma = (_: number, f: ImplementacionMiFirmaDto): number => f.firmaId;

  pendientes: ImplementacionMiFirmaDto[] = [];
  cargando = true;
  /** Arranca abierto: si hay algo pendiente, es lo primero que el usuario tiene que ver. */
  abierto = true;

  modalAbierto = false;
  seleccionado: ImplementacionMiFirmaDto | null = null;

  /**
   * Tipo del plan + categoría del punto, evitando repetirlo cuando coinciden (el caso más común:
   * un plan de capacitación cuya categoría también es "Capacitación" se leía "Capacitación ·
   * Capacitación"). No memoiza porque devuelve un string: no aloca objetos por ciclo.
   */
  etiqueta(p: ImplementacionMiFirmaDto): string {
    const tipo = this.tipoLabel[p.planTipo] ?? p.planTipo;
    const cat = (p.categoria ?? '').trim();
    return cat && cat.toLowerCase() !== tipo.toLowerCase() ? `${tipo} · ${cat}` : tipo;
  }

  constructor(private svc: ImplementacionService, private toast: ToastService) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  /**
   * Un fallo acá no molesta al usuario con un toast: el inicio no es el lugar para pelear con la
   * red, y el módulo de Implementación sigue siendo el camino completo. El panel simplemente no
   * aparece (queda en 0 pendientes).
   */
  private async cargar(): Promise<void> {
    this.cargando = true;
    try {
      this.pendientes = await firstValueFrom(this.svc.getMisPendientesFirma());
    } catch {
      this.pendientes = [];
    } finally {
      this.cargando = false;
    }
  }

  abrirFirma(p: ImplementacionMiFirmaDto): void {
    this.seleccionado = p;
    this.modalAbierto = true;
  }

  cerrarFirma(): void {
    this.modalAbierto = false;
    this.seleccionado = null;
  }

  onRespondido(res: ResultadoFirma): void {
    this.cerrarFirma();
    this.pendientes = this.pendientes.filter((p) => p.firmaId !== res.firma.firmaId);
    this.toast.success(
      res.accion === 'firmada'
        ? 'Firma registrada. Gracias por confirmar.'
        : 'Novedad registrada; el encargado la va a revisar.'
    );
  }
}

// src/app/features/vacunacion/components/panel-pendientes-vacunacion/panel-pendientes-vacunacion.component.ts
// Panel desplegable del inicio: las vacunas que faltan aplicar en los lotes del usuario —vencidas,
// las de hoy y las que abren esta semana—. Sin esto había que abrir lote por lote para saberlo.
// Si no hay nada pendiente el panel no se dibuja: el inicio no se ensucia con una tarjeta vacía.
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { VacunacionPendienteDto } from '../../models/vacunacion.model';
import {
  describirPendiente,
  objetivoDePendiente,
  PendienteVisual,
  trackByPendiente,
  ubicacionDePendiente,
} from '../../funciones/describir-pendiente.funcion';

/** Fila ya resuelta para la vista: el visual se calcula UNA vez, no por ciclo de change detection. */
interface FilaPendiente {
  p: VacunacionPendienteDto;
  visual: PendienteVisual;
  ubicacion: string;
  objetivo: string;
}

@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-panel-pendientes-vacunacion',
  standalone: true,
  imports: [CommonModule, RouterModule, HasPermissionDirective],
  styleUrls: ['../../../../shared/styles/pendientes-panel.scss'],
  template: `
    <section *appHasPermission="'vacunacion.registro.aplicar'">
      <section *ngIf="cargando || filas.length" class="pendientes-panel">
        <button type="button" class="pendientes-panel__head" [attr.aria-expanded]="abierto" (click)="abierto = !abierto">
          <span class="pendientes-panel__chevron" [class.pendientes-panel__chevron--abierto]="abierto" aria-hidden="true">▸</span>
          <span class="pendientes-panel__titulo">
            Vacunas pendientes
            <span *ngIf="!cargando" class="pendientes-panel__badge">{{ filas.length }}</span>
          </span>
          <span class="pendientes-panel__sub">
            {{ cargando ? 'Buscando…' : subtitulo }}
          </span>
        </button>

        <div *ngIf="abierto && !cargando" class="pendientes-panel__body">
          <article *ngFor="let f of filas; trackBy: trackByPendiente" class="pendientes-item">
            <div class="pendientes-item__info">
              <p class="pendientes-item__meta">{{ f.ubicacion }}</p>
              <p class="pendientes-item__titulo">
                {{ f.p.itemInventarioNombre }}
                <span class="ml-1.5 rounded-full px-2 py-0.5 text-[0.7rem] font-bold" [ngClass]="f.visual.claseBadge">
                  {{ f.visual.etiqueta }}
                </span>
              </p>
              <p class="pendientes-item__fecha">
                Lote <strong>{{ f.p.loteNombre }}</strong> · {{ f.objetivo }} · franja
                {{ f.p.fechaInicioFranja | date: 'dd/MM/yyyy' }} — {{ f.p.fechaFinFranja | date: 'dd/MM/yyyy' }}
              </p>
            </div>
            <a
              class="btn-italfoods-primary text-sm whitespace-nowrap"
              routerLink="/vacunacion/registro"
              [queryParams]="{ linea: f.p.lineaProductiva, loteId: f.p.loteId }"
            >
              💉 Registrar
            </a>
          </article>

          <a routerLink="/vacunacion/registro" class="pendientes-panel__link">Ir al registro de aplicación →</a>
        </div>
      </section>
    </section>
  `,
})
export class PanelPendientesVacunacionComponent implements OnInit {
  readonly trackByPendiente = (_: number, f: FilaPendiente): number => trackByPendiente(_, f.p);

  filas: FilaPendiente[] = [];
  cargando = true;
  /** Arranca abierto: si hay vacunas vencidas, es lo primero que tiene que ver el usuario. */
  abierto = true;
  subtitulo = '';

  constructor(private svc: VacunacionService) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  /**
   * Igual que el panel de Implementación: un fallo acá NO molesta con un toast. El inicio no es el
   * lugar para pelear con la red y el módulo de Vacunación sigue siendo el camino completo; el
   * panel simplemente no aparece.
   */
  private async cargar(): Promise<void> {
    this.cargando = true;
    try {
      const pendientes = await firstValueFrom(this.svc.getPendientes());
      this.filas = pendientes.map((p) => ({
        p,
        visual: describirPendiente(p.situacion, p.dias),
        ubicacion: ubicacionDePendiente(p),
        objetivo: objetivoDePendiente(p),
      }));
      this.subtitulo = this.armarSubtitulo(pendientes);
    } catch {
      this.filas = [];
    } finally {
      this.cargando = false;
    }
  }

  /** Lo urgente primero: si hay vencidas, el subtítulo las nombra; si no, habla de hoy. */
  private armarSubtitulo(pendientes: VacunacionPendienteDto[]): string {
    const vencidas = pendientes.filter((p) => p.situacion === 'Vencido').length;
    const hoy = pendientes.filter((p) => p.situacion === 'EnFranja').length;

    if (vencidas) return `${vencidas} vencida${vencidas === 1 ? '' : 's'} · ${hoy} para hoy`;
    if (hoy) return `${hoy} para aplicar hoy`;
    return 'Nada vencido: esto es lo que viene';
  }
}

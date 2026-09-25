import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TareaCampoDto } from '../../models/gestion-veterinaria.models';
import { GestionVeterinariaService } from '../../services/gestion-veterinaria.service';

@Component({
  selector: 'app-panel-tareas-campo', standalone: true, imports: [CommonModule, RouterLink],
  styleUrls: ['../../../../shared/styles/pendientes-panel.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <section *ngIf="cargando || tareas.length" class="pendientes-panel">
      <button type="button" class="pendientes-panel__head" [attr.aria-expanded]="abierto" (click)="abierto = !abierto">
        <span class="pendientes-panel__chevron" [class.pendientes-panel__chevron--abierto]="abierto" aria-hidden="true">▸</span>
        <span class="pendientes-panel__titulo">Tareas de campo <span *ngIf="!cargando" class="pendientes-panel__badge">{{ tareas.length }}</span></span>
        <span class="pendientes-panel__sub">{{ cargando ? 'Consultando tu jornada…' : vencidas ? vencidas + ' vencida(s) requieren atención' : 'Actividades asignadas en tus granjas' }}</span>
      </button>
      <div *ngIf="abierto && !cargando" class="pendientes-panel__body">
        <div *ngFor="let tarea of tareas; trackBy: trackById" class="pendientes-item">
          <div class="pendientes-item__info"><div class="pendientes-item__meta">{{ tarea.estadoTemporal }} · {{ tarea.farmNombre }}</div><div class="pendientes-item__titulo">{{ tarea.titulo }}</div><div class="pendientes-item__desc">{{ ubicacion(tarea) }}</div><div class="pendientes-item__fecha">Hasta {{ tarea.fechaFin | date:'dd MMM yyyy' }}{{ tarea.requiereFoto ? ' · Foto requerida' : '' }}</div></div>
          <a class="pendientes-panel__link" [routerLink]="['/gestion-veterinaria/tareas', tarea.id, 'cumplir']">Realizar →</a>
        </div>
        <a routerLink="/gestion-veterinaria" class="pendientes-panel__link">Ver toda mi gestión veterinaria →</a>
      </div>
    </section>`,
})
export class PanelTareasCampoComponent implements OnInit {
  cargando = true;
  abierto = true;
  tareas: TareaCampoDto[] = [];
  vencidas = 0;
  constructor(private service: GestionVeterinariaService) {}
  ngOnInit(): void { this.service.getInicio().subscribe({ next: (inicio) => { this.tareas = inicio.tareas; this.vencidas = inicio.resumen.tareasVencidas; this.cargando = false; }, error: () => this.cargando = false }); }
  trackById(_: number, item: TareaCampoDto): number { return item.id; }
  ubicacion(tarea: TareaCampoDto): string { return [tarea.nucleoNombre, tarea.galponNombre, tarea.loteNombre].filter(Boolean).join(' · ') || 'Toda la granja'; }
}

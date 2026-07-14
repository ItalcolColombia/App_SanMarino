// features/migraciones-masivas/components/selector-tipo-migracion/selector-tipo-migracion.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TipoMigracionInfo, TipoMigracionCodigo } from '../../models/migracion.model';
import { esTipoPolloEngorde } from '../../funciones/agrupar-tipo-migracion.funcion';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';

/** Permisos que habilitan cada línea del módulo (control interino por rol, ver plan). */
const PERMISO_POLLO_ENGORDE = 'carga_masiva_pollo_engorde';
const PERMISO_POSTURA = 'carga_masiva_postura';

@Component({
  selector: 'app-selector-tipo-migracion',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tiles">
      <button
        *ngFor="let t of tipos"
        type="button"
        class="tile"
        [class.tile--active]="seleccionado === t.codigo"
        [class.tile--soon]="!t.disponible"
        [class.tile--locked]="t.disponible && !tienePermiso(t.codigo)"
        [disabled]="!t.disponible || !tienePermiso(t.codigo)"
        [title]="!t.disponible ? '' : !tienePermiso(t.codigo) ? mensajeSinPermiso(t.codigo) : ''"
        (click)="onClick(t)">
        <span class="tile__icon">{{ icono(t.codigo) }}</span>
        <span class="tile__body">
          <span class="tile__name">{{ t.nombre }}</span>
          <span class="tile__desc">{{ t.descripcion }}</span>
        </span>
        <span class="tile__meta">
          <span class="tile__phase">Fase {{ t.fase }}</span>
          <span class="tile__soon" *ngIf="!t.disponible">Próximamente</span>
          <span class="tile__locked" *ngIf="t.disponible && !tienePermiso(t.codigo)">{{ mensajeSinPermiso(t.codigo) }}</span>
        </span>
      </button>
    </div>
  `,
  styles: [`
    .tiles {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(230px, 1fr));
      gap: 0.85rem;
    }
    .tile {
      display: flex;
      align-items: flex-start;
      gap: 0.7rem;
      text-align: left;
      padding: 0.95rem 1rem;
      border-radius: 1rem;
      border: 1.5px solid #eef0f3;
      background: #fff;
      cursor: pointer;
      transition: transform .15s ease, border-color .15s ease, box-shadow .15s ease, background .15s ease;
    }
    .tile:hover {
      border-color: rgba(245, 130, 31, 0.4);
      transform: translateY(-2px);
      box-shadow: 0 10px 22px rgba(245, 130, 31, 0.12);
    }
    .tile--active {
      border-color: var(--ital-orange, #F5821F);
      background: var(--ital-orange-50, rgba(245,130,31,0.08));
      box-shadow: 0 10px 22px rgba(245, 130, 31, 0.18);
    }
    .tile--soon { opacity: 0.7; }
    .tile--locked {
      cursor: not-allowed;
      opacity: 0.55;
      filter: grayscale(0.6);
    }
    .tile--locked:hover {
      border-color: #eef0f3;
      transform: none;
      box-shadow: none;
    }
    .tile__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2.4rem; height: 2.4rem;
      flex-shrink: 0;
      font-size: 1.25rem;
      border-radius: 0.8rem;
      background: linear-gradient(135deg, rgba(245,130,31,0.14), rgba(251,176,64,0.18));
    }
    .tile__body { display: flex; flex-direction: column; gap: 0.15rem; min-width: 0; flex: 1; }
    .tile__name { font-weight: 700; color: var(--ital-text, #1f2937); font-size: 0.95rem; }
    .tile__desc { font-size: 0.78rem; color: var(--ital-muted, #6b7280); line-height: 1.35; }
    .tile__meta { display: flex; flex-direction: column; align-items: flex-end; gap: 0.3rem; flex-shrink: 0; }
    .tile__phase { font-size: 0.62rem; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; color: #b8bcc4; }
    .tile__soon {
      font-size: 0.6rem; font-weight: 700;
      padding: 0.12rem 0.4rem; border-radius: 999px;
      background: #f1f2f4; color: #8a8f98; white-space: nowrap;
    }
    .tile__locked {
      font-size: 0.6rem; font-weight: 700; text-align: right;
      padding: 0.12rem 0.4rem; border-radius: 999px;
      background: #f1f2f4; color: #b3261e; white-space: nowrap;
    }
  `]
})
export class SelectorTipoMigracionComponent {
  @Input() tipos: TipoMigracionInfo[] = [];
  @Input() seleccionado: string | null = null;
  @Output() seleccionar = new EventEmitter<TipoMigracionInfo>();

  private readonly permService = inject(UserPermissionService);

  private readonly iconos: Record<TipoMigracionCodigo, string> = {
    Granjas: '🏡',
    Nucleos: '🧩',
    Galpones: '🏭',
    SeguimientoLevante: '🐥',
    SeguimientoProduccion: '🥚',
    Ventas: '💵',
    MovimientoAves: '🐔',
    MovimientoHuevos: '📦',
    LotesPolloEngorde: '🐔',
    SeguimientoPolloEngorde: '📋',
    VentaPolloEngorde: '🧾'
  };

  icono(codigo: string): string {
    return this.iconos[codigo as TipoMigracionCodigo] ?? '📄';
  }

  /** Control interino por línea: sin el permiso de su línea, el tile queda deshabilitado. */
  tienePermiso(codigo: TipoMigracionCodigo): boolean {
    return this.permService.has(esTipoPolloEngorde(codigo) ? PERMISO_POLLO_ENGORDE : PERMISO_POSTURA);
  }

  mensajeSinPermiso(codigo: TipoMigracionCodigo): string {
    return esTipoPolloEngorde(codigo) ? 'Sin permisos' : 'Sin permiso para carga masiva';
  }

  /** Refuerzo del `[disabled]` del template (un tile bloqueado no debe poder seleccionarse). */
  onClick(t: TipoMigracionInfo): void {
    if (!t.disponible || !this.tienePermiso(t.codigo)) return;
    this.seleccionar.emit(t);
  }
}

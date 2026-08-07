// features/migraciones-masivas/components/selector-tipo-migracion/selector-tipo-migracion.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TipoMigracionInfo, TipoMigracionCodigo } from '../../models/migracion.model';

/**
 * Grilla de tiles del paso 1. Es 100% PRESENTACIONAL: pinta exactamente los tipos que recibe.
 * El filtrado (estructura, no implementados y permisos del usuario) lo hace la página con
 * `filtrarTiposVisibles`, así que acá ya no hay estado de "deshabilitado" ni "sin permisos".
 */
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
        (click)="seleccionar.emit(t)">
        <span class="tile__icon">{{ icono(t.codigo) }}</span>
        <span class="tile__body">
          <span class="tile__name">{{ t.nombre }}</span>
          <span class="tile__desc">{{ t.descripcion }}</span>
          <span class="tile__meta">
            <span class="tile__phase">Fase {{ t.fase }}</span>
          </span>
        </span>
      </button>
    </div>
  `,
  styles: [`
    .tiles {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
      gap: 0.85rem;
    }
    /*
      Icono a la izquierda y TODO el texto en una sola columna que ocupa el resto del ancho: los
      metadatos (Fase / Próximamente / Sin permiso) van al pie del cuerpo, no en una tercera columna.
      Como columna, el badge largo "Sin permiso para carga masiva" (nowrap) se llevaba más de la
      mitad del tile y dejaba la descripción en una palabra por línea, montada sobre el título.
    */
    .tile {
      display: flex;
      align-items: stretch;
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
    .tile__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      align-self: flex-start;
      width: 2.4rem; height: 2.4rem;
      flex-shrink: 0;
      font-size: 1.25rem;
      border-radius: 0.8rem;
      background: linear-gradient(135deg, rgba(245,130,31,0.14), rgba(251,176,64,0.18));
    }
    .tile__body { display: flex; flex-direction: column; gap: 0.15rem; min-width: 0; flex: 1; }
    .tile__name { font-weight: 700; color: var(--ital-text, #1f2937); font-size: 0.95rem; }
    .tile__desc { font-size: 0.78rem; color: var(--ital-muted, #6b7280); line-height: 1.35; }
    /* Fila de chips al pie del cuerpo; el margin-top auto los alinea entre tiles de distinto alto. */
    .tile__meta {
      display: flex; flex-wrap: wrap; align-items: center;
      gap: 0.3rem 0.4rem; margin-top: auto; padding-top: 0.45rem;
    }
    .tile__phase { font-size: 0.62rem; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; color: #b8bcc4; }
  `]
})
export class SelectorTipoMigracionComponent {
  @Input() tipos: TipoMigracionInfo[] = [];
  @Input() seleccionado: string | null = null;
  @Output() seleccionar = new EventEmitter<TipoMigracionInfo>();

  private readonly iconos: Record<TipoMigracionCodigo, string> = {
    Granjas: '🏡',
    Nucleos: '🧩',
    Galpones: '🏭',
    SeguimientoLevante: '🐥',
    SeguimientoProduccion: '🥚',
    LotesPolloEngorde: '🐔',
    SeguimientoPolloEngorde: '📋',
    SeguimientoReproductoraEngorde: '🐣',
    VentaPolloEngorde: '🧾'
  };

  icono(codigo: string): string {
    return this.iconos[codigo as TipoMigracionCodigo] ?? '📄';
  }
}

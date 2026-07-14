// features/migraciones-masivas/components/historial-migraciones/historial-migraciones.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MigracionService } from '../../services/migracion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ReporteErroresMigracionComponent } from '../reporte-errores-migracion/reporte-errores-migracion.component';
import { MigracionHistorial, MigracionError, TipoMigracionInfo } from '../../models/migracion.model';
import { construirBadgeEstado, formatearDuracion } from '../../funciones/construir-resumen-resultado.funcion';
import { fechaHoraCorta } from '../../../../shared/utils/format';

const PAGE_SIZE = 20;

/**
 * Historial de auditoría de migraciones (validaciones + importaciones) de la empresa activa.
 * Vive al final de la página, independiente del tipo seleccionado (es la vista de auditoría
 * completa). Signals + OnPush; un único `effect` re-consulta reactivamente ante cambios de
 * página/filtro/refresh (manual o disparado por la página tras una importación real).
 */
@Component({
  selector: 'app-historial-migraciones',
  standalone: true,
  imports: [CommonModule, ReporteErroresMigracionComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hist">
      <div class="hist__toolbar">
        <label class="hist__check">
          <input type="checkbox" [checked]="incluirValidaciones()" (change)="onToggleIncluirValidaciones($event)" />
          Incluir validaciones
        </label>
        <button type="button" class="hist__btn" (click)="refrescar()" [disabled]="cargando()">
          🔄 Refrescar
        </button>
        <span *ngIf="cargando()" class="hist__loading"><span class="spinner"></span> Cargando…</span>
      </div>

      <div class="hist__tablewrap">
        <table class="w-full text-sm" aria-label="Historial de migraciones">
          <thead class="text-left text-gray-500 border-b border-gray-200">
            <tr>
              <th scope="col" class="px-3 py-2 font-medium">Fecha</th>
              <th scope="col" class="px-3 py-2 font-medium">Tipo</th>
              <th scope="col" class="px-3 py-2 font-medium">Archivo</th>
              <th scope="col" class="px-3 py-2 font-medium">Estado</th>
              <th scope="col" class="px-3 py-2 font-medium text-right">Total</th>
              <th scope="col" class="px-3 py-2 font-medium text-right">Procesadas</th>
              <th scope="col" class="px-3 py-2 font-medium text-right">Omitidas</th>
              <th scope="col" class="px-3 py-2 font-medium text-right">Con error</th>
              <th scope="col" class="px-3 py-2 font-medium text-right">Duración</th>
              <th scope="col" class="px-3 py-2 font-medium">&nbsp;</th>
            </tr>
          </thead>
          <tbody>
            <ng-container *ngFor="let f of filas()">
              <tr class="border-t border-gray-100">
                <td class="px-3 py-1.5 text-gray-700 whitespace-nowrap">{{ f.fechaTexto }}</td>
                <td class="px-3 py-1.5 text-gray-700">{{ f.tipoTexto }}</td>
                <td class="px-3 py-1.5 text-gray-700 max-w-[16rem] truncate" [title]="f.item.nombreArchivo">{{ f.item.nombreArchivo }}</td>
                <td class="px-3 py-1.5 whitespace-nowrap">
                  <span class="badge" [class]="'tono--' + f.badge.tono">{{ f.badge.etiqueta }}</span>
                  <span *ngIf="f.item.fueDryRun" class="badge tono--neutro">Validación</span>
                </td>
                <td class="px-3 py-1.5 text-right tabular-nums text-gray-700">{{ f.item.filasTotales }}</td>
                <td class="px-3 py-1.5 text-right tabular-nums text-gray-700">{{ f.item.filasProcesadas }}</td>
                <td class="px-3 py-1.5 text-right tabular-nums text-gray-700">{{ f.item.filasOmitidas }}</td>
                <td class="px-3 py-1.5 text-right tabular-nums text-gray-700">{{ f.item.filasError }}</td>
                <td class="px-3 py-1.5 text-right tabular-nums text-gray-500">{{ f.duracionTexto }}</td>
                <td class="px-3 py-1.5 text-right">
                  <button *ngIf="f.item.tieneErrores" type="button" class="hist__link" (click)="verErrores(f.item)">
                    {{ filaExpandida() === f.item.id ? 'Ocultar' : 'Ver errores' }}
                  </button>
                </td>
              </tr>
              <tr *ngIf="filaExpandida() === f.item.id">
                <td colspan="10" class="px-3 py-2 bg-gray-50/60">
                  <span *ngIf="cargandoErrores()" class="hist__loading"><span class="spinner"></span> Cargando errores…</span>
                  <app-reporte-errores-migracion
                    *ngIf="!cargandoErrores()"
                    [errores]="erroresFila()"
                    [nombreBase]="'Historial_' + f.item.id">
                  </app-reporte-errores-migracion>
                </td>
              </tr>
            </ng-container>
            <tr *ngIf="!cargando() && filas().length === 0">
              <td colspan="10" class="px-3 py-6 text-center text-gray-500">Sin corridas registradas todavía.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="hist__paginacion">
        <button type="button" class="hist__btn" (click)="anterior()" [disabled]="cargando() || page() <= 1">← Anterior</button>
        <span class="hist__pagina">Página {{ page() }} de {{ totalPaginas() }} ({{ total() }} registro(s))</span>
        <button type="button" class="hist__btn" (click)="siguiente()" [disabled]="cargando() || page() >= totalPaginas()">Siguiente →</button>
      </div>
    </div>
  `,
  styles: [`
    .hist { display: flex; flex-direction: column; gap: 0.75rem; }
    .hist__toolbar { display: flex; flex-wrap: wrap; align-items: center; gap: 0.85rem; }
    .hist__check { display: inline-flex; align-items: center; gap: 0.4rem; font-size: 0.85rem; color: var(--ital-text, #1f2937); cursor: pointer; }
    .hist__btn {
      display: inline-flex; align-items: center; gap: 0.35rem;
      padding: 0.4rem 0.8rem; border-radius: 0.6rem; font-size: 0.82rem; font-weight: 600;
      background: #f7f8fa; color: #3f4551; border: 1px solid #e7e9ee; cursor: pointer;
    }
    .hist__btn:hover:not(:disabled) { background: #eef0f3; }
    .hist__btn:disabled { opacity: 0.5; cursor: not-allowed; }
    .hist__loading { display: inline-flex; align-items: center; gap: 0.4rem; font-size: 0.82rem; color: #6b7280; }
    .spinner {
      width: 0.9rem; height: 0.9rem; border: 2px solid rgba(245,130,31,0.25);
      border-top-color: var(--ital-orange, #F5821F); border-radius: 999px; animation: hist-spin .7s linear infinite;
    }
    @keyframes hist-spin { to { transform: rotate(360deg); } }
    .hist__tablewrap { overflow-x: auto; border: 1px solid #eef0f3; border-radius: 0.9rem; }
    .hist__link {
      font-size: 0.8rem; font-weight: 600; color: var(--ital-orange-dark, #C85A0E);
      background: transparent; border: none; cursor: pointer; text-decoration: underline; white-space: nowrap;
    }
    .hist__link:hover { color: var(--ital-orange, #F5821F); }
    .badge {
      display: inline-block; padding: 0.15rem 0.55rem; border-radius: 999px;
      font-size: 0.7rem; font-weight: 700; margin-right: 0.3rem; white-space: nowrap;
    }
    .tono--neutro { background: #f1f2f4; color: #6b7280; }
    .tono--ok { background: #eafaf0; color: #1c7a45; }
    .tono--alerta { background: #fff8ef; color: #9a5b16; }
    .tono--peligro { background: #fdecec; color: #b3261e; }
    .hist__paginacion { display: flex; flex-wrap: wrap; align-items: center; justify-content: center; gap: 0.85rem; }
    .hist__pagina { font-size: 0.82rem; color: #6b7280; }
  `]
})
export class HistorialMigracionesComponent {
  /** Catálogo de tipos (opcional), solo para mostrar el nombre legible en vez del código crudo. */
  readonly tipos = input<TipoMigracionInfo[]>([]);
  /** Bump externo (p. ej. tras una importación real en el panel) para forzar un refresco desde la página. */
  readonly refrescarTrigger = input<number>(0);

  private readonly svc = inject(MigracionService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly items = signal<MigracionHistorial[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly incluirValidaciones = signal(true);
  readonly cargando = signal(false);
  readonly filaExpandida = signal<number | null>(null);
  readonly erroresFila = signal<MigracionError[]>([]);
  readonly cargandoErrores = signal(false);

  /** Botón "Refrescar" manual (interno); se suma como dependencia del efecto de carga. */
  private readonly refrescarManual = signal(0);

  private readonly nombrePorCodigo = computed(() => {
    const map = new Map<string, string>();
    this.tipos().forEach((t) => map.set(t.codigo, t.nombre));
    return map;
  });

  readonly totalPaginas = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  /** Fila por fila, toda la presentación derivada y memoizada (nada de métodos que alocan en el template). */
  readonly filas = computed(() =>
    this.items().map((item) => ({
      item,
      badge: construirBadgeEstado(item.estado),
      duracionTexto: item.duracionMs != null ? formatearDuracion(item.duracionMs) : '—',
      fechaTexto: fechaHoraCorta(item.fechaProceso),
      tipoTexto: this.nombrePorCodigo().get(item.tipo) ?? item.tipo
    }))
  );

  constructor() {
    effect(() => {
      const page = this.page();
      const incluir = this.incluirValidaciones();
      this.refrescarTrigger();
      this.refrescarManual();
      this.cargar(page, incluir);
    });
  }

  private cargar(page: number, incluirValidaciones: boolean): void {
    this.cargando.set(true);
    this.filaExpandida.set(null);
    this.svc.getHistorial({ page, pageSize: PAGE_SIZE, incluirValidaciones })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (p) => { this.items.set(p.items); this.total.set(p.total); this.cargando.set(false); },
        error: () => { this.cargando.set(false); this.toast.error('No se pudo cargar el historial de migraciones.'); }
      });
  }

  onToggleIncluirValidaciones(ev: Event): void {
    this.incluirValidaciones.set((ev.target as HTMLInputElement).checked);
    this.page.set(1);
  }

  anterior(): void {
    if (this.page() > 1) this.page.update((p) => p - 1);
  }

  siguiente(): void {
    if (this.page() < this.totalPaginas()) this.page.update((p) => p + 1);
  }

  refrescar(): void {
    this.refrescarManual.update((n) => n + 1);
  }

  verErrores(item: MigracionHistorial): void {
    if (this.filaExpandida() === item.id) { this.filaExpandida.set(null); return; }

    const id = item.id;
    this.filaExpandida.set(id);
    this.cargandoErrores.set(true);
    this.erroresFila.set([]);
    this.svc.getErrores(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (errs) => {
          if (this.filaExpandida() !== id) return; // el usuario ya cambió de fila mientras cargaba
          this.erroresFila.set(errs);
          this.cargandoErrores.set(false);
        },
        error: () => {
          if (this.filaExpandida() !== id) return;
          this.cargandoErrores.set(false);
          this.filaExpandida.set(null);
          this.toast.error('No se pudieron cargar los errores de esa corrida.');
        }
      });
  }
}

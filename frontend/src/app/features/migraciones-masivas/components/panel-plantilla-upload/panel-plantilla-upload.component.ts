// features/migraciones-masivas/components/panel-plantilla-upload/panel-plantilla-upload.component.ts
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MigracionService } from '../../services/migracion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ReporteErroresMigracionComponent } from '../reporte-errores-migracion/reporte-errores-migracion.component';
import { TipoMigracionInfo, MigracionContexto, MigracionResult } from '../../models/migracion.model';

/**
 * Panel: descargar plantilla → subir archivo → validar (dry-run) / importar.
 * Signals + OnPush para refrescar la vista aunque la respuesta HTTP emita fuera de la zona de Angular
 * (el interceptor cifra con Web Crypto).
 */
@Component({
  selector: 'app-panel-plantilla-upload',
  standalone: true,
  imports: [CommonModule, ReporteErroresMigracionComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="panel">
      <div *ngIf="!tipo().disponible" class="soon">
        La migración de <strong>{{ tipo().nombre }}</strong> se habilita en la Fase {{ tipo().fase }}. La estructura del módulo ya está lista.
      </div>

      <ng-container *ngIf="tipo().disponible">
        <div class="actions">
          <button type="button" class="btn btn--ghost" (click)="descargarPlantilla()" [disabled]="cargando()">
            <span class="btn__ic">⬇️</span> Descargar plantilla
          </button>

          <label class="file" [class.file--set]="archivo()">
            <input type="file" accept=".xlsx,.xls" (change)="onFile($event)" />
            <span class="btn__ic">📎</span>
            <span class="file__name">{{ archivo()?.name || 'Seleccionar archivo…' }}</span>
          </label>

          <button type="button" class="btn btn--outline" (click)="validar()" [disabled]="cargando() || !archivo()">
            Validar
          </button>
          <button type="button" class="btn btn--primary" (click)="importar()" [disabled]="cargando() || !archivo()">
            Importar
          </button>

          <span *ngIf="cargando()" class="proc"><span class="spinner"></span> Procesando…</span>
        </div>

        <div *ngIf="ultimoResultado() as r" class="result" [class.result--ok]="r.exito" [class.result--err]="!r.exito">
          <span class="result__dot"></span>
          {{ r.fueDryRun ? 'Validación' : 'Importación' }}:
          <strong>{{ r.filasProcesadas }}/{{ r.filasTotales }}</strong> filas ·
          {{ r.filasError }} con error · estado {{ r.estado }}
        </div>

        <app-reporte-errores-migracion [errores]="errores()"></app-reporte-errores-migracion>
      </ng-container>
    </div>
  `,
  styles: [`
    .panel { display: flex; flex-direction: column; gap: 0.35rem; }
    .soon {
      border: 1px solid #fde3c4; background: #fff8ef; color: #9a5b16;
      border-radius: 0.9rem; padding: 0.85rem 1rem; font-size: 0.9rem;
    }
    .actions { display: flex; flex-wrap: wrap; align-items: center; gap: 0.6rem; }
    .btn {
      display: inline-flex; align-items: center; gap: 0.4rem;
      padding: 0.55rem 0.95rem; border-radius: 0.75rem; font-weight: 600; font-size: 0.88rem;
      cursor: pointer; border: 1.5px solid transparent; transition: filter .15s ease, background .15s ease, border-color .15s ease;
    }
    .btn:disabled { opacity: 0.5; cursor: not-allowed; }
    .btn__ic { font-size: 0.95rem; }
    .btn--primary { background: var(--ital-orange, #F5821F); color: #fff; box-shadow: 0 6px 16px rgba(245,130,31,0.28); }
    .btn--primary:hover:not(:disabled) { filter: brightness(1.05); }
    .btn--outline { background: #fff; color: var(--ital-orange-dark, #C85A0E); border-color: rgba(245,130,31,0.4); }
    .btn--outline:hover:not(:disabled) { background: var(--ital-orange-50, rgba(245,130,31,0.08)); }
    .btn--ghost { background: #f7f8fa; color: #3f4551; border-color: #e7e9ee; }
    .btn--ghost:hover:not(:disabled) { background: #eef0f3; }
    .file {
      display: inline-flex; align-items: center; gap: 0.4rem;
      padding: 0.55rem 0.95rem; border-radius: 0.75rem; font-size: 0.88rem;
      border: 1.5px dashed #d7dae0; color: #6b7280; cursor: pointer; max-width: 280px;
    }
    .file:hover { border-color: rgba(245,130,31,0.5); }
    .file--set { border-style: solid; border-color: rgba(245,130,31,0.4); color: var(--ital-text, #1f2937); }
    .file input { display: none; }
    .file__name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .proc { display: inline-flex; align-items: center; gap: 0.4rem; font-size: 0.85rem; color: #6b7280; }
    .spinner {
      width: 1rem; height: 1rem; border: 2.5px solid rgba(245,130,31,0.25);
      border-top-color: var(--ital-orange, #F5821F); border-radius: 999px; animation: p-spin .7s linear infinite;
    }
    @keyframes p-spin { to { transform: rotate(360deg); } }
    .result {
      display: inline-flex; align-items: center; gap: 0.5rem; margin-top: 0.85rem;
      font-size: 0.88rem; padding: 0.5rem 0.8rem; border-radius: 0.7rem; width: fit-content;
    }
    .result__dot { width: 0.6rem; height: 0.6rem; border-radius: 999px; }
    .result--ok { background: #eafaf0; color: #1c7a45; }
    .result--ok .result__dot { background: #22a558; }
    .result--err { background: #fdecec; color: #b3261e; }
    .result--err .result__dot { background: #e5484d; }
  `]
})
export class PanelPlantillaUploadComponent {
  readonly tipo = input.required<TipoMigracionInfo>();
  readonly contexto = input<MigracionContexto>({});
  readonly resultado = output<MigracionResult>();

  private readonly svc = inject(MigracionService);
  private readonly toast = inject(ToastService);

  readonly archivo = signal<File | null>(null);
  readonly cargando = signal(false);
  readonly ultimoResultado = signal<MigracionResult | null>(null);
  readonly errores = computed(() => this.ultimoResultado()?.errores ?? []);

  onFile(ev: Event): void {
    const el = ev.target as HTMLInputElement;
    this.archivo.set(el.files?.[0] ?? null);
    this.ultimoResultado.set(null);
  }

  descargarPlantilla(): void {
    this.svc.descargarPlantilla(this.tipo().codigo, this.contexto()).subscribe({
      next: (blob) => this.guardarBlob(blob, `${this.tipo().nombre}.xlsx`),
      error: (e) => this.toast.error(this.msg(e, 'No se pudo generar la plantilla.'))
    });
  }

  validar(): void { this.ejecutar(true); }
  importar(): void { this.ejecutar(false); }

  private ejecutar(dryRun: boolean): void {
    const file = this.archivo();
    if (!file) { this.toast.warning('Seleccioná un archivo Excel primero.'); return; }
    this.cargando.set(true);
    const obs = dryRun
      ? this.svc.validar(this.tipo().codigo, file, this.contexto())
      : this.svc.importar(this.tipo().codigo, file, this.contexto());
    obs.subscribe({
      next: (r) => {
        this.cargando.set(false);
        this.ultimoResultado.set(r);
        this.resultado.emit(r);
        if (r.exito) this.toast.success(dryRun ? 'Validación sin errores.' : 'Importación completada.');
        else this.toast.warning(`Se encontraron ${r.filasError} fila(s) con error. No se insertó nada.`);
      },
      error: (e) => { this.cargando.set(false); this.toast.error(this.msg(e, 'Error al procesar el archivo.')); }
    });
  }

  private guardarBlob(blob: Blob, nombre: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = nombre;
    a.click();
    URL.revokeObjectURL(url);
  }

  private msg(e: unknown, fallback: string): string {
    const err = e as { error?: { message?: string } };
    return err?.error?.message ?? fallback;
  }
}

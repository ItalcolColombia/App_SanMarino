// features/migraciones-masivas/components/panel-plantilla-upload/panel-plantilla-upload.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MigracionService } from '../../services/migracion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ReporteErroresMigracionComponent } from '../reporte-errores-migracion/reporte-errores-migracion.component';
import { TipoMigracionInfo, MigracionContexto, MigracionResult } from '../../models/migracion.model';
import { validarArchivoCliente } from '../../funciones/validar-archivo-cliente.funcion';
import { construirResumenResultado, construirBadgeEstado } from '../../funciones/construir-resumen-resultado.funcion';

/**
 * Panel: descargar plantilla → subir archivo → validar (dry-run) / importar.
 * Signals + OnPush para refrescar la vista aunque la respuesta HTTP emita fuera de la zona de Angular
 * (el interceptor cifra con Web Crypto). Importar solo se habilita tras una validación exitosa del
 * MISMO archivo/tipo, o con el checkbox de "solo filas válidas" si la validación dio errores reales.
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
            <input type="file" accept=".xlsx" (change)="onFile($event)" />
            <span class="btn__ic">📎</span>
            <span class="file__name">{{ archivo()?.name || 'Seleccionar archivo…' }}</span>
          </label>

          <button type="button" class="btn btn--outline" (click)="validar()" [disabled]="cargando() || !archivo()">
            Validar
          </button>
          <button type="button" class="btn btn--primary" (click)="importar()" [disabled]="!puedeImportar()">
            Importar
          </button>

          <span *ngIf="cargando()" class="proc"><span class="spinner"></span> Procesando…</span>
        </div>

        <label *ngIf="hayErroresReales()" class="checkbox-parcial">
          <input type="checkbox" [checked]="permitirParcial()" (change)="onTogglePermitirParcial($event)" />
          Importar solo las filas válidas (omite las filas con error)
        </label>

        <ng-container *ngIf="vista() as v">
          <div class="result" [class]="'tono--' + v.badge.tono">
            <span class="result__dot"></span>
            {{ v.r.fueDryRun ? 'Validación' : 'Importación' }} · <strong>{{ v.badge.etiqueta }}</strong>
          </div>

          <div class="tarjetas">
            <div *ngFor="let item of v.resumen" class="tarjeta" [class]="'tono--' + item.tono">
              <span class="tarjeta__valor">{{ item.valor }}</span>
              <span class="tarjeta__etiqueta">{{ item.etiqueta }}</span>
            </div>
          </div>

          <app-reporte-errores-migracion
            [errores]="v.r.errores"
            [totalErrores]="v.r.totalErrores"
            [nombreBase]="tipo().codigo">
          </app-reporte-errores-migracion>
        </ng-container>
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
    .checkbox-parcial {
      display: inline-flex; align-items: center; gap: 0.45rem; width: fit-content;
      font-size: 0.85rem; color: var(--ital-text, #1f2937); margin-top: 0.35rem; cursor: pointer;
    }
    .checkbox-parcial input { cursor: pointer; }
    .result {
      display: inline-flex; align-items: center; gap: 0.5rem; margin-top: 0.85rem;
      font-size: 0.88rem; padding: 0.5rem 0.8rem; border-radius: 0.7rem; width: fit-content;
    }
    .result__dot { width: 0.6rem; height: 0.6rem; border-radius: 999px; }
    .tarjetas { display: flex; flex-wrap: wrap; gap: 0.6rem; margin-top: 0.7rem; }
    .tarjeta {
      display: flex; flex-direction: column; gap: 0.1rem; min-width: 6.5rem;
      padding: 0.55rem 0.85rem; border-radius: 0.75rem; border: 1px solid transparent;
    }
    .tarjeta__valor { font-size: 1.15rem; font-weight: 800; line-height: 1.1; }
    .tarjeta__etiqueta { font-size: 0.72rem; font-weight: 600; opacity: 0.85; }
    /* Tonos compartidos (badge de estado + tarjetas de resumen) */
    .tono--neutro { background: #f7f8fa; color: #3f4551; border-color: #e7e9ee; }
    .tono--ok { background: #eafaf0; color: #1c7a45; border-color: rgba(34,165,88,0.35); }
    .tono--ok .result__dot { background: #22a558; }
    .tono--alerta { background: #fff8ef; color: #9a5b16; border-color: #fde3c4; }
    .tono--alerta .result__dot { background: #9a5b16; }
    .tono--peligro { background: #fdecec; color: #b3261e; border-color: rgba(229,72,77,0.35); }
    .tono--peligro .result__dot { background: #e5484d; }
  `]
})
export class PanelPlantillaUploadComponent {
  readonly tipo = input.required<TipoMigracionInfo>();
  readonly contexto = input<MigracionContexto>({});
  readonly resultado = output<MigracionResult>();
  /** Se emite solo tras una IMPORTACIÓN real (no en dry-run), para que la página refresque el historial. */
  readonly importado = output<void>();

  private readonly svc = inject(MigracionService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly destroyRef = inject(DestroyRef);

  readonly archivo = signal<File | null>(null);
  readonly cargando = signal(false);
  readonly ultimoResultado = signal<MigracionResult | null>(null);
  readonly permitirParcial = signal(false);

  /** Archivo/tipo al que corresponde `ultimoResultado` (invalida el gate si el usuario los cambia). */
  private archivoValidado: File | null = null;
  private tipoValidado: string | null = null;

  /** `ultimoResultado`, pero solo si sigue correspondiendo al archivo/tipo actualmente seleccionados. */
  private readonly resultadoVigente = computed<MigracionResult | null>(() => {
    const r = this.ultimoResultado();
    if (!r) return null;
    return this.archivoValidado === this.archivo() && this.tipoValidado === this.tipo().codigo ? r : null;
  });

  private readonly validadoOk = computed(() => this.resultadoVigente()?.exito === true);

  readonly hayErroresReales = computed(() => {
    const r = this.resultadoVigente();
    return !!r && !r.exito && r.filasError > 0;
  });

  readonly puedeImportar = computed(() =>
    !!this.archivo() && !this.cargando() && (this.validadoOk() || (this.hayErroresReales() && this.permitirParcial()))
  );

  /** Vista agregada (resultado + badge + tarjetas) para el template: un único computed memoizado. */
  readonly vista = computed(() => {
    const r = this.resultadoVigente();
    if (!r) return null;
    return { r, badge: construirBadgeEstado(r.estado), resumen: construirResumenResultado(r) };
  });

  onFile(ev: Event): void {
    const el = ev.target as HTMLInputElement;
    const file = el.files?.[0] ?? null;
    if (file) {
      const error = validarArchivoCliente(file);
      if (error) {
        this.toast.warning(error);
        el.value = '';
        return;
      }
    }
    this.archivo.set(file);
    this.ultimoResultado.set(null);
    this.permitirParcial.set(false);
  }

  onTogglePermitirParcial(ev: Event): void {
    this.permitirParcial.set((ev.target as HTMLInputElement).checked);
  }

  descargarPlantilla(): void {
    this.svc.descargarPlantilla(this.tipo().codigo, this.contexto())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (blob) => this.guardarBlob(blob, `${this.tipo().nombre}.xlsx`),
        error: (e) => this.toast.error(this.mensajeError(e, 'No se pudo generar la plantilla.'))
      });
  }

  validar(): void {
    const file = this.archivo();
    if (!file) { this.toast.warning('Seleccioná un archivo Excel primero.'); return; }
    this.cargando.set(true);
    this.svc.validar(this.tipo().codigo, file, this.contexto())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => this.onResultado(r, file, true),
        error: (e) => this.onError(e)
      });
  }

  async importar(): Promise<void> {
    const file = this.archivo();
    if (!file || !this.puedeImportar()) return;

    const parcial = this.hayErroresReales() && this.permitirParcial();
    const detalle = parcial ? ' Se omitirán las filas con error.' : '';
    const ok = await this.confirmDialog.ask({
      title: 'Confirmar importación',
      message: `Se importará "${file.name}" como ${this.tipo().nombre}.${detalle}`,
      type: 'warning',
      confirmText: 'Importar'
    });
    if (!ok) return;

    this.cargando.set(true);
    this.svc.importar(this.tipo().codigo, file, this.contexto(), parcial)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => this.onResultado(r, file, false),
        error: (e) => this.onError(e)
      });
  }

  private onResultado(r: MigracionResult, file: File, dryRun: boolean): void {
    this.cargando.set(false);
    this.archivoValidado = file;
    this.tipoValidado = this.tipo().codigo;
    this.ultimoResultado.set(r);
    this.resultado.emit(r);
    if (!dryRun) this.importado.emit();
    this.mostrarToastResultado(r, dryRun);
  }

  private onError(e: unknown): void {
    this.cargando.set(false);
    this.toast.error(this.mensajeError(e, 'Error al procesar el archivo.'));
  }

  private mostrarToastResultado(r: MigracionResult, dryRun: boolean): void {
    if (dryRun) {
      if (r.exito) this.toast.success('Validación sin errores bloqueantes.');
      else this.toast.warning(`Se encontraron ${r.filasError} fila(s) con error. Revisá el detalle antes de importar.`);
      return;
    }
    switch (r.estado) {
      case 'Procesado':
        this.toast.success('Importación completada.');
        break;
      case 'ProcesadoParcial':
        this.toast.warning(`Importación parcial: ${r.filasProcesadas} de ${r.filasTotales} fila(s) procesadas (se omitieron ${r.filasError} con error).`);
        break;
      case 'Fallido':
        this.toast.error('La importación falló. Revisá el detalle de errores.');
        break;
      default:
        this.toast.warning(`Se encontraron ${r.filasError} fila(s) con error. No se insertó nada.`);
    }
  }

  private guardarBlob(blob: Blob, nombre: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = nombre;
    a.click();
    URL.revokeObjectURL(url);
  }

  /** Mensajes específicos por status HTTP; si el back mandó `error.message` (400), se prioriza. */
  private mensajeError(e: unknown, fallback: string): string {
    const err = e as { status?: number; error?: { message?: string } };
    if (err?.status === 401) return 'Sesión expirada. Iniciá sesión nuevamente.';
    if (err?.status === 413) return 'El archivo supera el tamaño máximo permitido (10 MB).';
    if (err?.status === 0) return 'Sin conexión con el servidor. Verificá tu red e intentá de nuevo.';
    if (err?.status === 400 && err.error?.message) return err.error.message;
    if (err?.status === 500) return 'Error interno del servidor al procesar el archivo.';
    return err?.error?.message ?? fallback;
  }
}

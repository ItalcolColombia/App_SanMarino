// src/app/features/silos/components/modal-silos-granja/modal-silos-granja.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPen, faTimes } from '@fortawesome/free-solid-svg-icons';
import { forkJoin, finalize } from 'rxjs';

import { ToastService } from '../../../../shared/services/toast.service';
import { FarmSiloDto, SiloCatalogoDto, SilosService } from '../../services/silos.service';
import { SelectorSilosComponent } from '../selector-silos/selector-silos.component';

/**
 * «Silos de la granja»: elige del catálogo cuáles tiene esta granja y permite completar sus códigos
 * ERP (que son POR GRANJA: el «Silo 1» de una granja tiene otra ubicación que el de otra).
 *
 * `Eager` obligatorio: carga por `subscribe` y escribe estado desde el callback.
 */
@Component({
  selector: 'app-modal-silos-granja',
  standalone: true,
  imports: [FormsModule, FontAwesomeModule, SelectorSilosComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './modal-silos-granja.component.html',
  styleUrls: ['./modal-silos-granja.component.scss']
})
export class ModalSilosGranjaComponent implements OnInit {
  private readonly svc = inject(SilosService);
  private readonly toast = inject(ToastService);

  @Input({ required: true }) granjaId!: number;
  @Input() granjaNombre = '';

  @Output() cerrar = new EventEmitter<void>();
  /** Se emite tras guardar, para que la pantalla que abrió el modal refresque su vista. */
  @Output() guardado = new EventEmitter<void>();

  readonly faTimes = faTimes;
  readonly faPen = faPen;

  catalogo: SiloCatalogoDto[] = [];
  asignados: FarmSiloDto[] = [];
  seleccionados: number[] = [];

  crearBodega = false;
  nombreBodega = 'Bodega';
  tieneBodega = false;

  loading = false;
  saving = false;

  /** Silo cuyos códigos ERP se están editando (null = no hay edición abierta). */
  editandoErp: FarmSiloDto | null = null;
  erpUbicacion = '';
  erpCentroOperacion = '';
  erpCodigoBodega = '';
  erpDescripcion = '';

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.loading = true;
    forkJoin({
      catalogo: this.svc.getCatalogo(true),
      asignados: this.svc.getSilosDeGranja(this.granjaId)
    })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: ({ catalogo, asignados }) => {
          this.catalogo = catalogo;
          this.disponiblesComoSilos = this.adaptarCatalogo(catalogo);
          this.asignados = asignados;
          // Preselección = lo que la granja ya tiene del catálogo (las bodegas no salen de ahí).
          this.seleccionados = asignados
            .filter(a => a.siloCatalogoId != null)
            .map(a => a.siloCatalogoId!);
          this.tieneBodega = asignados.some(a => a.siloCatalogoId == null);
          this.crearBodega = !this.tieneBodega;
        },
        error: e => this.toast.error(this.mensajeError(e, 'No se pudieron cargar los silos de la granja.'))
      });
  }

  /**
   * Catálogo adaptado al shape que consume el selector compartido. Es un CAMPO, no un getter: un
   * getter que arma el array devolvería una referencia nueva en cada ciclo de change detection y
   * rompería el `OnPush` del selector (el bug NG0103 del repo). Se recalcula solo al cargar.
   */
  disponiblesComoSilos: FarmSiloDto[] = [];

  private adaptarCatalogo(catalogo: SiloCatalogoDto[]): FarmSiloDto[] {
    return catalogo.map(c => ({
      id: c.id,
      companyId: c.companyId,
      granjaId: this.granjaId,
      granjaNombre: null,
      siloCatalogoId: c.id,
      numero: c.numero,
      nombre: c.nombre,
      tipo: 'Silo',
      codigoErpUbicacion: null,
      descripcion: c.descripcion,
      centroOperacion: null,
      codigoBodega: null,
      activo: c.activo,
      galponesAsignados: 0,
      lotesAsignados: 0
    }));
  }

  onSeleccionChange(ids: number[]): void {
    this.seleccionados = ids;
  }

  guardar(): void {
    if (this.saving) return;
    this.saving = true;
    this.svc.asignarSilosAGranja({
      granjaId: this.granjaId,
      siloCatalogoIds: this.seleccionados,
      crearBodega: this.crearBodega && !this.tieneBodega,
      nombreBodega: this.nombreBodega.trim() || null
    })
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: silos => {
          this.asignados = silos;
          this.tieneBodega = silos.some(s => s.siloCatalogoId == null);
          this.crearBodega = false;
          this.toast.success('Silos de la granja actualizados.');
          this.guardado.emit();
        },
        error: e => this.toast.error(this.mensajeError(e, 'No se pudieron guardar los silos.'))
      });
  }

  // ── Códigos ERP por silo ─────────────────────────────────────────────────

  abrirErp(s: FarmSiloDto): void {
    this.editandoErp = s;
    this.erpUbicacion = s.codigoErpUbicacion ?? '';
    this.erpCentroOperacion = s.centroOperacion ?? '';
    this.erpCodigoBodega = s.codigoBodega ?? '';
    this.erpDescripcion = s.descripcion ?? '';
  }

  cerrarErp(): void {
    this.editandoErp = null;
  }

  guardarErp(): void {
    if (!this.editandoErp || this.saving) return;
    this.saving = true;
    this.svc.actualizarSiloGranja(this.editandoErp.id, {
      codigoErpUbicacion: this.erpUbicacion.trim(),
      centroOperacion: this.erpCentroOperacion.trim(),
      codigoBodega: this.erpCodigoBodega.trim(),
      descripcion: this.erpDescripcion.trim()
    })
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => { this.toast.success('Códigos ERP actualizados.'); this.cerrarErp(); this.cargar(); },
        error: e => this.toast.error(this.mensajeError(e, 'No se pudieron guardar los códigos ERP.'))
      });
  }

  onCerrar(): void {
    this.cerrar.emit();
  }

  private mensajeError(e: unknown, fallback: string): string {
    const msg = (e as { error?: { message?: string } } | null)?.error?.message;
    return typeof msg === 'string' && msg.trim() ? msg : fallback;
  }

  trackById = (_: number, s: FarmSiloDto) => s.id;
}

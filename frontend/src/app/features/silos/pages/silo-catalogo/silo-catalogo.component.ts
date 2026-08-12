// src/app/features/silos/pages/silo-catalogo/silo-catalogo.component.ts
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FaIconLibrary, FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faLayerGroup, faPen, faPlus, faTimes, faTrash, faWarehouse } from '@fortawesome/free-solid-svg-icons';
import { finalize } from 'rxjs';

import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { SiloCatalogoDto, SilosService } from '../../services/silos.service';

/**
 * Lista MAESTRA de silos de la empresa (el «voy a crear una lista de silos del 1 al 100»). De acá
 * salen los silos que después se asignan a cada granja.
 *
 * `changeDetection: Eager` es obligatorio: el componente escribe estado desde callbacks de
 * `subscribe` y con OnPush (el default de Angular 22) la vista se quedaría en «Cargando…».
 */
@Component({
  selector: 'app-silo-catalogo',
  standalone: true,
  imports: [FormsModule, FontAwesomeModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './silo-catalogo.component.html',
  styleUrls: ['./silo-catalogo.component.scss']
})
export class SiloCatalogoComponent implements OnInit {
  private readonly svc = inject(SilosService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly companyConfig = inject(ActiveCompanyConfigService);

  readonly faPlus = faPlus;
  readonly faPen = faPen;
  readonly faTrash = faTrash;
  readonly faTimes = faTimes;
  readonly faWarehouse = faWarehouse;
  readonly faLayerGroup = faLayerGroup;

  silos: SiloCatalogoDto[] = [];
  loading = false;
  saving = false;

  /** Fail-closed: hasta que el backend confirme el flag, la pantalla se muestra deshabilitada. */
  manejaInventarioPorSilo = false;

  // ── Modal de alta/edición ────────────────────────────────────────────────
  modalAbierto = false;
  editando: SiloCatalogoDto | null = null;
  formNumero: number | null = null;
  formNombre = '';
  formDescripcion = '';
  formActivo = true;

  // ── Modal de generar rango ───────────────────────────────────────────────
  rangoAbierto = false;
  rangoDesde = 1;
  rangoHasta = 100;

  constructor(library: FaIconLibrary) {
    library.addIcons(faPlus, faPen, faTrash, faTimes, faWarehouse, faLayerGroup);
  }

  ngOnInit(): void {
    this.companyConfig.manejaInventarioPorSilo$.subscribe(v => (this.manejaInventarioPorSilo = v));
    this.cargar();
  }

  cargar(): void {
    this.loading = true;
    this.svc.getCatalogo()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: s => (this.silos = s),
        error: e => this.toast.error(this.mensajeError(e, 'No se pudo cargar la lista de silos.'))
      });
  }

  // ── Alta / edición ───────────────────────────────────────────────────────

  nuevo(): void {
    this.editando = null;
    this.formNumero = this.siguienteNumeroLibre();
    this.formNombre = '';
    this.formDescripcion = '';
    this.formActivo = true;
    this.modalAbierto = true;
  }

  editar(s: SiloCatalogoDto): void {
    this.editando = s;
    this.formNumero = s.numero;
    this.formNombre = s.nombre;
    this.formDescripcion = s.descripcion ?? '';
    this.formActivo = s.activo;
    this.modalAbierto = true;
  }

  cerrarModal(): void {
    this.modalAbierto = false;
    this.editando = null;
  }

  guardar(): void {
    if (this.saving) return;

    if (this.editando) {
      this.saving = true;
      this.svc.actualizarCatalogo(this.editando.id, {
        nombre: this.formNombre.trim() || null,
        descripcion: this.formDescripcion.trim(),
        activo: this.formActivo
      })
        .pipe(finalize(() => (this.saving = false)))
        .subscribe({
          next: () => { this.toast.success('Silo actualizado.'); this.cerrarModal(); this.cargar(); },
          error: e => this.toast.error(this.mensajeError(e, 'No se pudo actualizar el silo.'))
        });
      return;
    }

    if (this.formNumero == null) {
      this.toast.warning('Indique el número del silo.');
      return;
    }

    this.saving = true;
    this.svc.crearCatalogo({
      numero: this.formNumero,
      nombre: this.formNombre.trim() || null,
      descripcion: this.formDescripcion.trim() || null,
      activo: this.formActivo
    })
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => { this.toast.success('Silo creado.'); this.cerrarModal(); this.cargar(); },
        error: e => this.toast.error(this.mensajeError(e, 'No se pudo crear el silo.'))
      });
  }

  async eliminar(s: SiloCatalogoDto): Promise<void> {
    // El backend rechaza el borrado si el silo está asignado a alguna granja; el aviso acá es
    // para que el usuario no descubra el bloqueo recién después de confirmar.
    const enUso = s.granjasAsignadas > 0;
    const mensaje = enUso
      ? `«${s.nombre}» está asignado a ${s.granjasAsignadas} granja(s) y no se podrá eliminar. Quítelo de las granjas o márquelo como inactivo.`
      : `¿Eliminar «${s.nombre}» de la lista maestra?`;

    const ok = await this.confirmDialog.ask({
      title: enUso ? 'Silo en uso' : 'Eliminar silo',
      message: mensaje,
      type: enUso ? 'warning' : 'error',
      confirmText: enUso ? 'Intentar de todos modos' : 'Eliminar'
    });
    if (!ok) return;

    this.svc.eliminarCatalogo(s.id).subscribe({
      next: () => { this.toast.success('Silo eliminado.'); this.cargar(); },
      error: e => this.toast.error(this.mensajeError(e, 'No se pudo eliminar el silo.'))
    });
  }

  // ── Generar rango ────────────────────────────────────────────────────────

  abrirRango(): void {
    this.rangoDesde = 1;
    this.rangoHasta = 100;
    this.rangoAbierto = true;
  }

  cerrarRango(): void {
    this.rangoAbierto = false;
  }

  generarRango(): void {
    if (this.saving) return;
    this.saving = true;
    this.svc.generarRango({ desde: this.rangoDesde, hasta: this.rangoHasta })
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: r => {
          this.silos = r.silos;
          this.toast.success(
            r.omitidos > 0
              ? `${r.creados} silo(s) creado(s); ${r.omitidos} ya existían.`
              : `${r.creados} silo(s) creado(s).`
          );
          this.cerrarRango();
        },
        error: e => this.toast.error(this.mensajeError(e, 'No se pudo generar el rango.'))
      });
  }

  // ── Helpers ──────────────────────────────────────────────────────────────

  /** Primer número no usado, para prellenar el alta sin que el usuario tenga que buscarlo. */
  private siguienteNumeroLibre(): number {
    const usados = new Set(this.silos.map(s => s.numero));
    let n = 1;
    while (usados.has(n)) n++;
    return n;
  }

  private mensajeError(e: unknown, fallback: string): string {
    const msg = (e as { error?: { message?: string } } | null)?.error?.message;
    return typeof msg === 'string' && msg.trim() ? msg : fallback;
  }

  trackById = (_: number, s: SiloCatalogoDto) => s.id;
}

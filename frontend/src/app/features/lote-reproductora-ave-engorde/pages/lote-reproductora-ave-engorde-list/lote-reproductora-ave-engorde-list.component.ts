import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faEye, faFilter, faLayerGroup, faPlusCircle, faMinusCircle, faTimes } from '@fortawesome/free-solid-svg-icons';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import {
  LoteReproductoraAveEngordeService,
  LoteReproductoraAveEngordeDto,
  CreateLoteReproductoraAveEngordeDto,
  LoteAveEngordeFilterItemDto,
  AvesDisponiblesDto
} from '../../services/lote-reproductora-ave-engorde.service';
import { LoteEngordeService } from '../../../lote-engorde/services/lote-engorde.service';
import type { LoteAveEngordeDto } from '../../../lote-engorde/services/lote-engorde.service';
import { ymdSinTz } from '../../../../shared/utils/format';
// ShowIfCountryDirective removed — not used in this component

@Component({
  selector: 'app-lote-reproductora-ave-engorde-list',
  standalone: true,
  templateUrl: './lote-reproductora-ave-engorde-list.component.html',
  styleUrls: ['./lote-reproductora-ave-engorde-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    FontAwesomeModule,
    ConfirmationModalComponent,
    HasPermissionDirective
]
})
export class LoteReproductoraAveEngordeListComponent implements OnInit {
  faPlus = faPlus;
  faPen = faPen;
  faTrash = faTrash;
  faEye = faEye;
  faFilter = faFilter;
  faLayerGroup = faLayerGroup;
  faPlusCircle = faPlusCircle;
  faMinusCircle = faMinusCircle;
  faTimes = faTimes;

  allNucleos: Array<{ nucleoId: string; granjaId: number; nucleoNombre: string }> = [];
  allGalpones: Array<{ galponId: string; galponNombre: string; nucleoId: string; granjaId: number }> = [];
  allLotesAveEngorde: LoteAveEngordeFilterItemDto[] = [];

  granjas: Array<{ id: number; name: string }> = [];
  nucleos: Array<{ nucleoId: string; nucleoNombre: string }> = [];
  galpones: Array<{ galponId: string; galponNombre: string }> = [];
  lotesAveEngorde: LoteAveEngordeFilterItemDto[] = [];

  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteAveEngordeId: number | null = null;

  registros: LoteReproductoraAveEngordeDto[] = [];
  avesDisponibles: AvesDisponiblesDto | null = null;
  loteSeleccionado: LoteAveEngordeFilterItemDto | null = null;
  /** Detalle completo del lote aves de engorde seleccionado (raza, aves, etc.). */
  loteDetalle: LoteAveEngordeDto | null = null;

  loading = false;
  modalOpen = false;
  editing: LoteReproductoraAveEngordeDto | null = null;
  detalleOpen = false;
  detalleData: LoteReproductoraAveEngordeDto | null = null;
  bulkModalOpen = false;

  form!: FormGroup;
  confirmOpen = false;
  confirmData: ConfirmationModalData = { title: 'Eliminar', message: '¿Está seguro?', type: 'warning', confirmText: 'Eliminar', cancelText: 'Cancelar', showCancel: true };
  pendingDeleteId: number | null = null;

  /** Permission keys de los botones (patrón "modulo.accion"). */
  readonly PERM_EDITAR = 'lote_reproductora_engorde.editar';
  readonly PERM_ELIMINAR = 'lote_reproductora_engorde.eliminar';

  constructor(
    private fb: FormBuilder,
    private svc: LoteReproductoraAveEngordeService,
    private loteEngordeSvc: LoteEngordeService,
    private toast: ToastService,
    private permSvc: UserPermissionService
  ) {}

  /** True si el usuario puede editar la reproductora. */
  get canEditarPerm(): boolean {
    return this.permSvc.has(this.PERM_EDITAR);
  }
  /** True si el usuario puede eliminar la reproductora. */
  get canEliminarPerm(): boolean {
    return this.permSvc.has(this.PERM_ELIMINAR);
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      nombreLote: ['', [Validators.required, Validators.maxLength(200)]],
      reproductoraId: ['', [Validators.required, Validators.maxLength(100)]],
      codigoReproductora: [null as string | null, [Validators.maxLength(100)]],
      fechaEncasetamiento: [''],
      m: [0, [Validators.min(0)]],
      h: [0, [Validators.min(0)]],
      mixtas: [0, [Validators.min(0)]],
      mortCajaH: [0, [Validators.min(0)]],
      mortCajaM: [0, [Validators.min(0)]],
      unifH: [null as number | null, [Validators.min(0), Validators.max(100)]],
      unifM: [null as number | null, [Validators.min(0), Validators.max(100)]],
      pesoInicialM: [null as number | null, [Validators.min(0)]],
      pesoInicialH: [null as number | null, [Validators.min(0)]],
      pesoMixto: [null as number | null, [Validators.min(0)]],
      incubadoras: this.fb.array([])
    });

    this.loading = true;
    this.svc.getFilterData().subscribe({
      next: (data) => {
        this.granjas = [...(data.farms ?? [])].map(f => ({ id: f.id, name: f.name }));
        this.allNucleos = [...(data.nucleos ?? [])];
        this.allGalpones = [...(data.galpones ?? [])];
        this.allLotesAveEngorde = [...(data.lotesAveEngorde ?? [])];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('No se pudieron cargar los filtros', 'Error');
      }
    });
  }

  get incubadoras(): FormArray<FormGroup> {
    return this.form.get('incubadoras') as FormArray<FormGroup>;
  }

  /** Lote actualmente seleccionado en los filtros, expuesto al `<app-lesion-tab>`. */
  get loteIdActual(): number | undefined {
    return this.selectedLoteAveEngordeId ?? undefined;
  }

  /** Granja actualmente seleccionada en los filtros, expuesta al `<app-lesion-tab>`. */
  get farmIdActual(): number | undefined {
    return this.selectedGranjaId ?? undefined;
  }

  /** Galpón actualmente seleccionado en los filtros, expuesto al `<app-lesion-tab>`. */
  get galponIdActual(): string | undefined {
    return this.selectedGalponId ?? undefined;
  }

  /** Total hembras (H) que se usarán en los registros del formulario actual. */
  get totalHInForm(): number {
    return this.incubadoras.controls.reduce((sum, c) => sum + (Number(c.get('h')?.value) || 0), 0);
  }

  /** Total machos (M) que se usarán en los registros del formulario actual. */
  get totalMInForm(): number {
    return this.incubadoras.controls.reduce((sum, c) => sum + (Number(c.get('m')?.value) || 0), 0);
  }

  /** Hembras que quedarían disponibles después de crear los registros actuales del modal. */
  get remainingHembras(): number {
    if (!this.avesDisponibles) return 0;
    return Math.max(0, this.avesDisponibles.hembrasDisponibles - this.totalHInForm);
  }

  /** Machos que quedarían disponibles después de crear los registros actuales del modal. */
  get remainingMachos(): number {
    if (!this.avesDisponibles) return 0;
    return Math.max(0, this.avesDisponibles.machosDisponibles - this.totalMInForm);
  }

  /** No hay aves disponibles para agregar otro registro (hembras y machos restantes en 0). */
  get cannotAddMoreAves(): boolean {
    return this.remainingHembras <= 0 && this.remainingMachos <= 0;
  }

  /** True si TODOS los lotes reproductora del lote engorde completaron los 7 días de seguimiento. */
  get sieteDiasCompletosLote(): boolean {
    return this.registros.length > 0 && this.registros.every(r => r.sieteDiasCompletos === true);
  }

  private createBulkRow(initialCode = ''): FormGroup {
    // Nombre del lote: obligatorio pero SIN prellenar con el nombre del lote principal → el usuario lo asigna.
    return this.fb.group({
      reproductoraId: [initialCode, [Validators.required, Validators.maxLength(100)]],
      nombreLote: ['', [Validators.required, Validators.maxLength(200)]],
      fechaEncasetamiento: [''],
      m: [0, [Validators.min(0)]],
      h: [0, [Validators.min(0)]],
      mixtas: [0, [Validators.min(0)]],
      mortCajaH: [0, [Validators.min(0)]],
      mortCajaM: [0, [Validators.min(0)]],
      unifH: [null as number | null, [Validators.min(0), Validators.max(100)]],
      unifM: [null as number | null, [Validators.min(0), Validators.max(100)]],
      pesoInicialH: [null as number | null, [Validators.min(0)]],
      pesoInicialM: [null as number | null, [Validators.min(0)]],
      pesoMixto: [null as number | null, [Validators.min(0)]]
    });
  }

  /** Único punto de entrada para crear: uno o varios registros en el mismo modal. */
  openCreateModal(): void {
    if (this.selectedLoteAveEngordeId == null) return;
    if (!this.canCreateMore()) {
      this.toast.warning('No hay aves disponibles para asignar.', 'Sin aves');
      return;
    }
    this.incubadoras.clear();
    this.incubadoras.push(this.createBulkRow());
    this.bulkModalOpen = true;
    const lid = this.selectedLoteAveEngordeId;
    this.svc.getNewReproductoraCode(lid).subscribe({
      next: code => this.incubadoras.at(0)?.patchValue({ reproductoraId: code }),
      error: () => this.toast.error('No se pudo generar el código. Intente de nuevo.', 'Error')
    });
  }

  /** Códigos reproductora ya usados en el formulario (para no repetir al pedir uno nuevo). */
  private getCurrentReproductoraCodes(): string[] {
    return this.incubadoras.controls
      .map((c: FormGroup) => (c.get('reproductoraId')?.value ?? '').toString().trim())
      .filter(s => s.length > 0);
  }

  addBulkRow(): void {
    if (this.selectedLoteAveEngordeId == null) return;
    const exclude = this.getCurrentReproductoraCodes();
    this.svc.getNewReproductoraCode(this.selectedLoteAveEngordeId, exclude).subscribe({
      next: code => this.incubadoras.push(this.createBulkRow(code)),
      error: () => this.toast.error('No se pudo generar el código. Intente de nuevo.', 'Error')
    });
  }

  removeBulkRow(index: number): void {
    if (this.incubadoras.length <= 1) return;
    this.incubadoras.removeAt(index);
  }

  saveBulk(): void {
    if (this.incubadoras.length === 0) {
      this.toast.warning('Agregue al menos un registro.', 'Crear varios');
      return;
    }
    if (this.incubadoras.invalid) {
      this.incubadoras.markAllAsTouched();
      this.toast.warning('Complete los campos requeridos en todas las filas.', 'Crear varios');
      return;
    }
    if (this.avesDisponibles) {
      if (this.totalHInForm > this.avesDisponibles.hembrasDisponibles) {
        this.toast.warning(
          `Hembras en este registro (${this.totalHInForm}) superan las disponibles (${this.avesDisponibles.hembrasDisponibles}).`,
          'Aves insuficientes'
        );
        return;
      }
      if (this.totalMInForm > this.avesDisponibles.machosDisponibles) {
        this.toast.warning(
          `Machos en este registro (${this.totalMInForm}) superan los disponibles (${this.avesDisponibles.machosDisponibles}).`,
          'Aves insuficientes'
        );
        return;
      }
    }
    const loteAveEngordeId = this.selectedLoteAveEngordeId!;
    const dtos: CreateLoteReproductoraAveEngordeDto[] = this.incubadoras.controls.map((row: FormGroup) => {
      const v = row.value;
      return {
        loteAveEngordeId,
        reproductoraId: (v.reproductoraId ?? '').trim(),
        nombreLote: (v.nombreLote ?? '').trim(),
        fechaEncasetamiento: v.fechaEncasetamiento || null, // YYYY-MM-DD crudo: el service lo ancla a mediodía UTC
        m: v.m ?? 0,
        h: v.h ?? 0,
        mixtas: v.mixtas ?? 0,
        mortCajaH: v.mortCajaH ?? null,
        mortCajaM: v.mortCajaM ?? null,
        unifH: v.unifH ?? null,
        unifM: v.unifM ?? null,
        pesoInicialH: v.pesoInicialH ?? null,
        pesoInicialM: v.pesoInicialM ?? null,
        pesoMixto: v.pesoMixto ?? null
      };
    });
    this.loading = true;
    this.svc.createBulk(dtos).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => {
        this.bulkModalOpen = false;
        this.toast.success(dtos.length === 1 ? 'Registro creado.' : `Se crearon ${dtos.length} registros.`, 'OK');
        this.onLoteAveEngordeChange();
      },
      error: err => this.toast.error(err?.message ?? 'Error al crear en lote', 'Error')
    });
  }

  canCreateMore(): boolean {
    if (this.sieteDiasCompletosLote) return false;
    if (!this.avesDisponibles) return false;
    return (this.avesDisponibles.hembrasDisponibles + this.avesDisponibles.machosDisponibles) > 0;
  }

  onGranjaChange(): void {
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteAveEngordeId = null;
    this.nucleos = [];
    this.galpones = [];
    this.lotesAveEngorde = [];
    this.registros = [];
    this.avesDisponibles = null;
    this.loteSeleccionado = null;
    this.loteDetalle = null;
    if (!this.selectedGranjaId) return;
    this.nucleos = this.allNucleos.filter(n => n.granjaId === this.selectedGranjaId).map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }

  onNucleoChange(): void {
    this.selectedGalponId = null;
    this.selectedLoteAveEngordeId = null;
    this.galpones = [];
    this.lotesAveEngorde = [];
    this.registros = [];
    this.avesDisponibles = null;
    this.loteSeleccionado = null;
    this.loteDetalle = null;
    if (!this.selectedNucleoId) return;
    this.galpones = this.allGalpones
      .filter(g => g.granjaId === this.selectedGranjaId && g.nucleoId === this.selectedNucleoId)
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  onGalponChange(): void {
    this.selectedLoteAveEngordeId = null;
    this.lotesAveEngorde = [];
    this.registros = [];
    this.avesDisponibles = null;
    this.loteSeleccionado = null;
    this.loteDetalle = null;
    if (!this.selectedGalponId) return;
    this.lotesAveEngorde = this.allLotesAveEngorde.filter(
      l => l.granjaId === this.selectedGranjaId && (l.nucleoId ?? '') === (this.selectedNucleoId ?? '') && (l.galponId ?? '') === (this.selectedGalponId ?? '')
    );
  }

  onLoteAveEngordeChange(): void {
    this.registros = [];
    this.avesDisponibles = null;
    this.loteSeleccionado = null;
    this.loteDetalle = null;
    if (this.selectedLoteAveEngordeId == null) return;
    this.loteSeleccionado = this.lotesAveEngorde.find(l => l.loteAveEngordeId === this.selectedLoteAveEngordeId) ?? null;
    this.loading = true;
    this.svc.getAll(this.selectedLoteAveEngordeId).pipe(finalize(() => this.loading = false)).subscribe({
      next: r => this.registros = [...(r ?? [])],
      error: () => this.registros = []
    });
    this.svc.getAvesDisponibles(this.selectedLoteAveEngordeId).subscribe({
      next: a => this.avesDisponibles = a,
      error: () => this.avesDisponibles = null
    });
    this.loteEngordeSvc.getById(this.selectedLoteAveEngordeId).subscribe({
      next: d => this.loteDetalle = d,
      error: () => this.loteDetalle = null
    });
  }

  edit(r: LoteReproductoraAveEngordeDto): void {
    if (!this.canEditarPerm) {
      this.toast.warning('No tiene permiso para editar la reproductora.', 'Permiso requerido');
      return;
    }
    this.editing = r;
    this.incubadoras.clear();
    this.form.patchValue({
      nombreLote: r.nombreLote ?? '',
      reproductoraId: r.reproductoraId ?? '',
      codigoReproductora: r.codigoReproductora ?? null,
      fechaEncasetamiento: ymdSinTz(r.fechaEncasetamiento) ?? '',
      m: r.m ?? 0, h: r.h ?? 0, mixtas: r.mixtas ?? 0,
      mortCajaH: r.mortCajaH ?? 0, mortCajaM: r.mortCajaM ?? 0,
      unifH: r.unifH ?? null, unifM: r.unifM ?? null,
      pesoInicialM: r.pesoInicialM ?? null, pesoInicialH: r.pesoInicialH ?? null, pesoMixto: r.pesoMixto ?? null
    });
    this.modalOpen = true;
  }

  view(r: LoteReproductoraAveEngordeDto): void {
    this.detalleData = r;
    this.detalleOpen = true;
  }

  closeDetalle(): void {
    this.detalleOpen = false;
    this.detalleData = null;
  }

  deleteRegistro(r: LoteReproductoraAveEngordeDto): void {
    if (!this.canEliminarPerm) {
      this.toast.warning('No tiene permiso para eliminar la reproductora.', 'Permiso requerido');
      return;
    }
    // No se puede eliminar una reproductora que ya tiene registros de seguimiento: hay que eliminarlos primero.
    // (El backend también lo bloquea con 400; esto es la validación inmediata en UI.)
    if ((r.numRegistros ?? 0) > 0) {
      this.toast.warning(
        `Esta reproductora tiene ${r.numRegistros} registro(s) de seguimiento. Elimine primero esos registros para poder eliminarla.`,
        'No se puede eliminar'
      );
      return;
    }
    this.pendingDeleteId = r.id;
    this.confirmData = { ...this.confirmData, title: 'Eliminar lote reproductora', message: `¿Eliminar "${r.nombreLote}" (${r.reproductoraId})?` };
    this.confirmOpen = true;
  }

  onConfirmDelete(): void {
    if (this.pendingDeleteId == null) return;
    this.loading = true;
    this.svc.delete(this.pendingDeleteId).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => {
        this.toast.success('Registro eliminado.', 'OK');
        this.confirmOpen = false;
        this.pendingDeleteId = null;
        this.onLoteAveEngordeChange();
      },
      error: err => {
        this.toast.error(err?.message ?? 'Error al eliminar', 'Error');
        this.confirmOpen = false;
        this.pendingDeleteId = null;
      }
    });
  }

  onCancelConfirm(): void {
    this.confirmOpen = false;
    this.pendingDeleteId = null;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.warning('Complete los campos requeridos.', 'Formulario');
      return;
    }
    const v = this.form.value;
    const loteAveEngordeId = this.selectedLoteAveEngordeId!;

    if (this.editing) {
      const dto: CreateLoteReproductoraAveEngordeDto = {
        loteAveEngordeId,
        reproductoraId: (v.reproductoraId ?? this.editing.reproductoraId ?? '').trim(),
        codigoReproductora: v.codigoReproductora?.trim() || null,
        nombreLote: (v.nombreLote ?? '').trim(),
        fechaEncasetamiento: v.fechaEncasetamiento || null, // YYYY-MM-DD crudo: el service lo ancla a mediodía UTC
        m: v.m ?? null, h: v.h ?? null, mixtas: v.mixtas ?? null,
        mortCajaH: v.mortCajaH ?? null, mortCajaM: v.mortCajaM ?? null,
        unifH: v.unifH ?? null, unifM: v.unifM ?? null,
        pesoInicialM: v.pesoInicialM ?? null, pesoInicialH: v.pesoInicialH ?? null, pesoMixto: v.pesoMixto ?? null
      };
      this.loading = true;
      this.svc.update(this.editing.id, dto).pipe(finalize(() => this.loading = false)).subscribe({
        next: (updated) => {
          this.modalOpen = false;
          this.editing = null;
          this.toast.success('Actualizado.', 'OK');
          const idx = this.registros.findIndex(r => r.id === updated.id);
          if (idx !== -1) {
            this.registros = [...this.registros.slice(0, idx), updated, ...this.registros.slice(idx + 1)];
          } else {
            this.registros = [...this.registros, updated];
          }
          if (this.selectedLoteAveEngordeId != null) {
            this.svc.getAvesDisponibles(this.selectedLoteAveEngordeId).subscribe({
              next: a => this.avesDisponibles = a,
              error: () => {}
            });
          }
        },
        error: err => this.toast.error(err?.message ?? 'Error', 'Error')
      });
      return;
    }

    const dto: CreateLoteReproductoraAveEngordeDto = {
      loteAveEngordeId,
      reproductoraId: (v.reproductoraId ?? '').trim(),
      nombreLote: (v.nombreLote ?? '').trim(),
      fechaEncasetamiento: v.fechaEncasetamiento ? new Date(v.fechaEncasetamiento).toISOString() : null,
      m: v.m ?? null, h: v.h ?? null, mixtas: v.mixtas ?? null,
      mortCajaH: v.mortCajaH ?? null, mortCajaM: v.mortCajaM ?? null,
      unifH: v.unifH ?? null, unifM: v.unifM ?? null,
      pesoInicialM: v.pesoInicialM ?? null, pesoInicialH: v.pesoInicialH ?? null, pesoMixto: v.pesoMixto ?? null
    };
    this.loading = true;
    this.svc.create(dto).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => {
        this.modalOpen = false;
        this.toast.success('Lote reproductora creado.', 'OK');
        this.onLoteAveEngordeChange();
      },
      error: err => this.toast.error(err?.message ?? 'Error', 'Error')
    });
  }

  formatNum(n: number | null | undefined): string {
    return n == null ? '—' : n.toLocaleString('es');
  }

  formatDate(s: string | null | undefined): string {
    // Fecha "pura": extraer el día intencional sin restar un día por zona horaria
    const ymd = ymdSinTz(s);
    if (!ymd) return '—';
    return new Date(ymd + 'T00:00:00').toLocaleDateString('es');
  }
}

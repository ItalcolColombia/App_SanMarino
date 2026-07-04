// src/app/features/lesiones/components/lesion-tab/lesion-tab.component.ts
import {
  Component,
  Input,
  OnChanges,
  OnInit,
  SimpleChanges,
  computed,
  inject,
  signal,
  DestroyRef
} from '@angular/core';

import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { LesionService } from '../../services/lesion.service';
import {
  LesionDto,
  CreateLesionRequest,
  UpdateLesionRequest,
  LesionResumenDto,
  ModuloOrigenLesion,
  TIPOS_LESION
} from '../../models/lesion.models';
import { ToastService } from '../../../../shared/services/toast.service';

type TabKey = 'listado' | 'resumen';

/**
 * Tab reutilizable para registrar y consultar lesiones aviares.
 * Pensado para integrarse dentro de los flujos de Seguimiento Diario
 * (Reproductora, Apoyo, Engorde) cuando la zona de operación es Panamá.
 *
 * Se filtra automáticamente por el `moduloOrigen` recibido, y por la
 * granja / lote / galpón si se proveen. El formulario de creación toma
 * estos mismos valores como contexto.
 */
@Component({
  selector: 'app-lesion-tab',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './lesion-tab.component.html',
  styleUrls: ['./lesion-tab.component.scss']
})
export class LesionTabComponent implements OnInit, OnChanges {
  // ── Inputs ──────────────────────────────────────────────────────────────
  @Input({ required: true }) moduloOrigen!: ModuloOrigenLesion;
  @Input() farmId?: number | null | undefined;
  @Input() clienteId?: number | null | undefined;
  @Input() loteId?: number | null | undefined;
  @Input() galponId?: string | null | undefined;
  @Input() loteReproductoraId?: number | string | null | undefined;
  /** Nombre del lote reproductora para mostrar en el modal/header */
  @Input() loteNombre?: string | null | undefined;
  /** Nombres de contexto para el banner del modal */
  @Input() granjaNombre?: string | null;
  @Input() nucleoNombre?: string | null;
  @Input() galponNombre?: string | null;
  @Input() loteEngorde?: string | null;
  /** Fecha de encasetamiento del lote reproductora — para calcular edad automáticamente */
  @Input() fechaEncasetamiento?: string | Date | null;

  // ── Dependencies ────────────────────────────────────────────────────────
  private readonly svc = inject(LesionService);
  private readonly fb = inject(FormBuilder);
  private readonly toastSvc = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  // ── Catalogs ────────────────────────────────────────────────────────────
  readonly tiposLesion = TIPOS_LESION;

  // ── State signals ───────────────────────────────────────────────────────
  readonly activeTab = signal<TabKey>('listado');
  readonly loading = signal(false);
  readonly loadingResumen = signal(false);
  readonly saving = signal(false);

  readonly items = signal<LesionDto[]>([]);
  readonly resumen = signal<LesionResumenDto[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(10);

  readonly editing = signal<LesionDto | null>(null);
  readonly modalOpen = signal(false);

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.total() / this.pageSize()))
  );

  readonly resumenTotal = computed(() =>
    this.resumen().reduce((acc, r) => acc + r.totalAves, 0)
  );

  // ── Form ────────────────────────────────────────────────────────────────
  form!: FormGroup;

  // ─────────────────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.buildForm();
    this.loadAll();
    // Suscribirse a señales globales del servicio (abrir modal / refrescar listado)
    this.svc.openCreate$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.openCreate());
    this.svc.refresh$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.loadAll());
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Si cambia el contexto (granja, lote, módulo) recargamos.
    if (!this.form) return;
    const ctxChanged =
      changes['moduloOrigen'] ||
      changes['farmId'] ||
      changes['loteId'] ||
      changes['clienteId'] ||
      changes['galponId'] ||
      changes['loteReproductoraId'];
    if (ctxChanged) {
      this.page.set(1);
      this.loadAll();
    }
  }

  // ── Form helpers ────────────────────────────────────────────────────────
  private buildForm(): void {
    const today = new Date().toISOString().slice(0, 10);
    this.form = this.fb.group({
      fechaRegistro: [today, Validators.required],
      tipoLesion: ['', [Validators.required, Validators.maxLength(100)]],
      galponId: [this.galponId ?? ''],
      loteId: [this.loteId ?? null],
      loteReproductoraId: [this.loteReproductoraId ?? ''],
      edadDias: [this.calcularEdadDias(today), [Validators.min(0)]],
      avesMacho: [null, [Validators.min(0)]],
      avesHembra: [null, [Validators.min(0)]],
      avesMixtas: [null, [Validators.min(0)]],
      observaciones: ['', Validators.maxLength(500)],
      status: ['A']
    });

    // Auto-calcular edad al cambiar la fecha de registro
    this.form.get('fechaRegistro')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(fecha => {
        const edad = this.calcularEdadDias(fecha);
        if (edad !== null) {
          this.form.get('edadDias')?.setValue(edad, { emitEvent: false });
        }
      });
  }

  /**
   * Calcula la edad en días desde la fecha de encasetamiento hasta fechaRegistro.
   * Usa solo la parte YYYY-MM-DD de ambas fechas y compara en UTC para evitar
   * problemas de timezone (ej: backend devuelve "2025-05-28T00:00:00" sin Z,
   * el input del form devuelve "2025-05-29" como UTC — sin esta normalización
   * la diferencia horaria puede producir Math.floor un día menor).
   *
   * Ejemplo: encasetamiento=28-05-2025, registro=29-05-2025 → 1 día.
   */
  calcularEdadDias(fechaRegistro?: string | null): number | null {
    if (!this.fechaEncasetamiento) return null;
    const encasetUtc = this.toUtcDateOnly(this.fechaEncasetamiento);
    const regUtc = fechaRegistro
      ? this.toUtcDateOnly(fechaRegistro)
      : this.toUtcDateOnly(new Date().toISOString().slice(0, 10));
    if (!encasetUtc || !regUtc) return null;
    const dias = Math.floor((regUtc - encasetUtc) / (1000 * 60 * 60 * 24));
    return dias >= 0 ? dias : null;
  }

  /**
   * Extrae solo la parte YYYY-MM-DD de cualquier formato de fecha y retorna
   * un timestamp UTC representando ese día a medianoche UTC.
   * Soporta: "2025-05-28", "2025-05-28T00:00:00", "2025-05-28T05:00:00Z", Date.
   */
  private toUtcDateOnly(date: string | Date): number | null {
    let ymd: string;
    if (date instanceof Date) {
      ymd = date.toISOString().slice(0, 10);
    } else {
      ymd = String(date).slice(0, 10);
    }
    const [y, m, d] = ymd.split('-').map(Number);
    if (!y || !m || !d) return null;
    return Date.UTC(y, m - 1, d);
  }

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  // ── Data loading ────────────────────────────────────────────────────────
  loadAll(): void {
    this.loadListado();
    this.loadResumen();
  }

  loadListado(): void {
    if (!this.moduloOrigen) return;
    this.loading.set(true);
    this.svc
      .search({
        moduloOrigen: this.moduloOrigen,
        farmId: this.farmId ?? undefined,
        loteId: this.loteId ?? undefined,
        clienteId: this.clienteId ?? undefined,
        galponId: this.galponId ?? undefined,
        loteReproductoraId: this.loteReproductoraId == null ? undefined : String(this.loteReproductoraId),
        page: this.page(),
        pageSize: this.pageSize(),
        sortBy: 'fechaRegistro',
        sortDesc: true
      })
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (res) => {
          this.items.set(res.items ?? []);
          this.total.set(res.total ?? 0);
        },
        error: () =>
          this.toastSvc.error('No se pudieron cargar las lesiones.', 'Error')
      });
  }

  loadResumen(): void {
    if (!this.moduloOrigen) return;
    this.loadingResumen.set(true);
    this.svc
      .getResumen({
        moduloOrigen: this.moduloOrigen,
        farmId: this.farmId ?? undefined,
        loteId: this.loteId ?? undefined,
        clienteId: this.clienteId ?? undefined,
        galponId: this.galponId ?? undefined,
        loteReproductoraId: this.loteReproductoraId == null ? undefined : String(this.loteReproductoraId)
      })
      .pipe(
        finalize(() => this.loadingResumen.set(false)),
        takeUntilDestroyed(this.destroyRef),
        catchError(() => {
          this.toastSvc.error('No se pudo cargar el resumen.', 'Error');
          return of([] as LesionResumenDto[]);
        })
      )
      .subscribe((res) => this.resumen.set(res ?? []));
  }

  // ── Pagination ──────────────────────────────────────────────────────────
  prevPage(): void {
    if (this.page() > 1) {
      this.page.update((p) => p - 1);
      this.loadListado();
    }
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.page.update((p) => p + 1);
      this.loadListado();
    }
  }

  setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
    this.loadListado();
  }

  // ── Modal create/edit ───────────────────────────────────────────────────
  openCreate(): void {
    this.editing.set(null);
    const today = new Date().toISOString().slice(0, 10);
    this.form.reset({
      fechaRegistro: today,
      tipoLesion: '',
      galponId: this.galponId ?? '',
      loteId: this.loteId ?? null,
      loteReproductoraId: this.loteReproductoraId ?? '',
      edadDias: this.calcularEdadDias(today),
      avesMacho: null,
      avesHembra: null,
      avesMixtas: null,
      observaciones: '',
      status: 'A'
    });
    this.modalOpen.set(true);
  }

  openEdit(item: LesionDto): void {
    this.editing.set(item);
    this.form.patchValue({
      fechaRegistro: item.fechaRegistro?.slice(0, 10) ?? '',
      tipoLesion: item.tipoLesion,
      galponId: item.galponId ?? '',
      loteId: item.loteId,
      loteReproductoraId: item.loteReproductoraId ?? '',
      edadDias: item.edadDias,
      avesMacho: item.avesMacho,
      avesHembra: item.avesHembra,
      avesMixtas: item.avesMixtas,
      observaciones: item.observaciones ?? '',
      status: item.status ?? 'A'
    });
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.editing.set(null);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toastSvc.error('Revisa los campos obligatorios.', 'Formulario inválido');
      return;
    }
    if (this.farmId === undefined || this.farmId === null) {
      this.toastSvc.error('No hay granja asociada al contexto.', 'Error');
      return;
    }

    const v = this.form.getRawValue() as Record<string, unknown>;
    const payload: CreateLesionRequest = {
      clienteId: this.clienteId ?? null,
      farmId: this.farmId,
      galponId: this.toStringOrNull(v['galponId']),
      loteId: this.toNumberOrNull(v['loteId']),
      loteReproductoraId: this.toStringOrNull(v['loteReproductoraId']),
      edadDias: this.toNumberOrNull(v['edadDias']),
      avesMacho: this.toNumberOrNull(v['avesMacho']),
      avesHembra: this.toNumberOrNull(v['avesHembra']),
      avesMixtas: this.toNumberOrNull(v['avesMixtas']),
      tipoLesion: String(v['tipoLesion'] ?? '').trim(),
      observaciones: this.toStringOrNull(v['observaciones']),
      fechaRegistro: String(v['fechaRegistro'] ?? ''),
      moduloOrigen: this.moduloOrigen
    };

    this.saving.set(true);
    const current = this.editing();

    if (current) {
      const updatePayload: UpdateLesionRequest = {
        ...payload,
        status: String(v['status'] ?? 'A')
      };
      this.svc
        .update(current.id, updatePayload)
        .pipe(
          finalize(() => this.saving.set(false)),
          takeUntilDestroyed(this.destroyRef)
        )
        .subscribe({
          next: () => {
            this.toastSvc.success('Lesión actualizada.', 'Listo');
            this.closeModal();
            this.loadAll();
          },
          error: (err: { error?: { message?: string } }) =>
            this.toastSvc.error(
              err?.error?.message ?? 'Error al actualizar la lesión.',
              'Error'
            )
        });
    } else {
      this.svc
        .create(payload)
        .pipe(
          finalize(() => this.saving.set(false)),
          takeUntilDestroyed(this.destroyRef)
        )
        .subscribe({
          next: () => {
            this.toastSvc.success('Lesión registrada.', 'Listo');
            this.closeModal();
            this.loadAll();
          },
          error: (err: { error?: { message?: string } }) =>
            this.toastSvc.error(
              err?.error?.message ?? 'Error al registrar la lesión.',
              'Error'
            )
        });
    }
  }

  // ── Delete ──────────────────────────────────────────────────────────────
  confirmDelete(item: LesionDto): void {
    const ok = window.confirm(
      `¿Eliminar la lesión "${item.tipoLesion}" registrada el ${this.formatDate(item.fechaRegistro)}?`
    );
    if (!ok) return;

    this.svc
      .delete(item.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toastSvc.success('Lesión eliminada.', 'Listo');
          this.loadAll();
        },
        error: (err: { error?: { message?: string } }) =>
          this.toastSvc.error(
            err?.error?.message ?? 'Error al eliminar la lesión.',
            'Error'
          )
      });
  }

  // ── Template helpers ────────────────────────────────────────────────────
  setTab(tab: TabKey): void {
    this.activeTab.set(tab);
    if (tab === 'resumen') this.loadResumen();
  }

  formatDate(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleDateString('es-PA', {
      year: 'numeric',
      month: 'short',
      day: '2-digit'
    });
  }

  avesTotal(item: LesionDto): number {
    return (item.avesMacho ?? 0) + (item.avesHembra ?? 0) + (item.avesMixtas ?? 0);
  }

  trackById(_: number, item: LesionDto): number {
    return item.id;
  }

  trackByTipo(_: number, item: LesionResumenDto): string {
    return item.tipoLesion;
  }

  private toNumberOrNull(v: unknown): number | null {
    if (v === null || v === undefined || v === '') return null;
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }

  private toStringOrNull(v: unknown): string | null {
    if (v === null || v === undefined) return null;
    const s = String(v).trim();
    return s === '' ? null : s;
  }
}

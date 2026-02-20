// app/features/nucleo/components/nucleo-list/nucleo-list.component.ts
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  Input,
  OnDestroy,
  OnInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { Subject, of } from 'rxjs';
import { catchError, finalize, takeUntil, tap } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPen, faPlus, faTrash, faSearch } from '@fortawesome/free-solid-svg-icons';

import {
  CreateNucleoDto,
  NucleoDto,
  NucleoService,
  UpdateNucleoDto
} from '../../services/nucleo.service';
import { FarmDto, FarmService } from '../../../farm/services/farm.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';

type NucleoForm = {
  nucleoId: string | number;
  granjaId: number | null;
  nucleoNombre: string;
};

@Component({
  selector: 'app-nucleo-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    FontAwesomeModule,
    ConfirmationModalComponent,
  ],
  templateUrl: './nucleo-list.component.html',
  styleUrls: ['./nucleo-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.embedded]': 'embedded' }
})
export class NucleoListComponent implements OnInit, OnDestroy {
  // Icons
  protected readonly faPlus = faPlus;
  protected readonly faSearch = faSearch;
  protected readonly faPen = faPen;
  protected readonly faTrash = faTrash;

  @Input() embedded = false;

  // Filtros (modelo de UI)
  filtro = '';
  selectedCompanyId: number | null = null;
  selectedFarmId: number | null = null;

  // Datos: solo núcleos al cargar la tabla; farms se cargan al abrir el modal
  nucleos: NucleoDto[] = [];
  viewNucleos: NucleoDto[] = [];
  farms: FarmDto[] = [];

  /** Opciones de filtro cacheadas (misma referencia hasta que cambien datos) para evitar NG0103 con OnPush. */
  filterCompanyOptions: { id: number; name: string }[] = [];
  filterFarmOptions: { id: number; name: string }[] = [];
  farmsFiltered: FarmDto[] = [];

  loadingModal = false;

  loading = false;
  modalOpen = false;
  editing: NucleoDto | null = null;

  confirmOpen = false;
  confirmData: ConfirmationModalData = {
    title: 'Eliminar núcleo',
    message: 'Esta acción no se puede deshacer.',
    confirmText: 'Eliminar',
    cancelText: 'Cancelar',
    type: 'warning',
    showCancel: true,
  };
  pendingDelete: NucleoDto | null = null;

  // Formulario
  form!: FormGroup;

  // lifecycle
  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly fb: FormBuilder,
    private readonly nucleoSvc: NucleoService,
    private readonly farmSvc: FarmService,
    private readonly cdr: ChangeDetectorRef,
    private readonly toastSvc: ToastService,
  ) {}

  // ======== Lifecycle ========
  ngOnInit(): void {
    this.buildForm();
    this.loadNucleos();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ======== Form ========
  private buildForm(): void {
    this.form = this.fb.group<NucleoForm>({
      nucleoId: ['', Validators.required],
      granjaId: [null, Validators.required],
      nucleoNombre: ['', Validators.required]
    } as any);
  }

  // ======== UI helpers ========
  trackByNucleo = (_: number, n: NucleoDto) => `${n.nucleoId}|${n.granjaId}`;

  onCompanyChange(val: number | null): void {
    this.selectedCompanyId = val != null && !isNaN(Number(val)) ? Number(val) : null;
    this.recompute();
    if (
      this.selectedFarmId != null &&
      !this.filterFarmOptions.some(f => f.id === this.selectedFarmId)
    ) {
      this.selectedFarmId = null;
      this.recompute();
    }
  }

  onFarmFilterChange(val: number | null): void {
    this.selectedFarmId = val != null && !isNaN(Number(val)) ? Number(val) : null;
    this.recompute();
  }

  onFiltroChange(val: string): void {
    this.filtro = val ?? '';
    this.recompute();
  }

  resetFilters(): void {
    this.filtro = '';
    this.selectedCompanyId = null;
    this.selectedFarmId = null;
    this.recompute();
  }

  /** Solo carga la lista de núcleos. Farms se cargan al abrir el modal (crear/editar). */
  private loadNucleos(): void {
    this.loading = true;
    this.nucleoSvc
      .getAll()
      .pipe(
        tap(list => {
          this.nucleos = list ?? [];
          this.recompute();
        }),
        catchError(err => {
          console.error('[Nucleos] load error', err);
          this.toastSvc.error('No se pudieron cargar los núcleos. Intente de nuevo.', 'Error');
          this.nucleos = [];
          this.viewNucleos = [];
          return of([]);
        }),
        finalize(() => {
          this.loading = false;
          this.cdr.markForCheck();
        }),
        takeUntil(this.destroy$)
      )
      .subscribe();
  }

  // ======== CRUD modal ========
  openModal(n?: NucleoDto): void {
    this.editing = n ?? null;
    this.modalOpen = true;
    this.loadFarmsForModal(n);
    this.cdr.markForCheck();
  }

  /** Carga granjas solo al abrir el modal; luego rellena el form (crear o editar). */
  private loadFarmsForModal(n?: NucleoDto): void {
    if (this.farms.length > 0) {
      this.farmsFiltered = this.selectedCompanyId == null
        ? this.farms
        : this.farms.filter(f => f.companyId === this.selectedCompanyId);
      this.applyFormInModal(n);
      return;
    }
    this.loadingModal = true;
    this.farmSvc
      .getAll()
      .pipe(
        takeUntil(this.destroy$),
        catchError(err => {
          console.error('[Farms] load for modal', err);
          this.toastSvc.warning('No se pudieron cargar las granjas.', 'Aviso');
          return of([]);
        }),
        finalize(() => {
          this.loadingModal = false;
          this.cdr.markForCheck();
        })
      )
      .subscribe(list => {
        this.farms = list ?? [];
        this.farmsFiltered = this.selectedCompanyId == null
          ? this.farms
          : this.farms.filter(f => f.companyId === this.selectedCompanyId);
        this.applyFormInModal(n);
      });
  }

  private applyFormInModal(n?: NucleoDto): void {
    if (n) {
      this.form.reset({
        nucleoId: n.nucleoId,
        granjaId: n.granjaId,
        nucleoNombre: n.nucleoNombre
      });
    } else {
      const newId = this.generateUniqueId6(this.nucleos);
      this.form.reset({
        nucleoId: newId,
        granjaId: null,
        nucleoNombre: ''
      });
    }
    this.cdr.markForCheck();
  }

  closeModal(): void {
    this.modalOpen = false;
    this.editing = null;
    this.cdr.markForCheck();
  }

  save(): void {
    if (this.form.invalid) return;

    // Verificación rápida de unicidad (solo al crear)
    if (!this.editing) {
      const id = String(this.form.value['nucleoId']);
      if (this.nucleos.some(n => String(n.nucleoId) === id)) {
        const newId = this.generateUniqueId6(this.nucleos);
        this.form.get('nucleoId')?.setValue(newId);
      }
    }

    const payload = this.form.value as NucleoDto;
    const req$ = this.editing
      ? this.nucleoSvc.update(payload as UpdateNucleoDto)
      : this.nucleoSvc.create(payload as CreateNucleoDto);

    this.loading = true;
    req$
      .pipe(
        tap(saved => {
          this.upsertNucleo(saved);
          this.recompute();
          this.closeModal();
          this.toastSvc.success(this.editing ? 'Núcleo actualizado correctamente.' : 'Núcleo creado correctamente.', 'Listo');
        }),
        catchError(err => {
          console.error('[Nucleo] save error', err);
          const msg = err?.error?.message || err?.error?.detail || 'No se pudo guardar el núcleo.';
          this.toastSvc.error(msg, 'Error');
          return of(null);
        }),
        finalize(() => {
          this.loading = false;
          this.cdr.markForCheck();
        }),
        takeUntil(this.destroy$)
      )
      .subscribe();
  }

  delete(n: NucleoDto): void {
    this.pendingDelete = n;
    this.confirmData = {
      title: 'Eliminar núcleo',
      message: `¿Eliminar el núcleo "${n.nucleoNombre}"? Esta acción no se puede deshacer.`,
      confirmText: 'Eliminar',
      cancelText: 'Cancelar',
      type: 'warning',
      showCancel: true,
    };
    this.confirmOpen = true;
    this.cdr.markForCheck();
  }

  onConfirmDelete(): void {
    const n = this.pendingDelete;
    if (!n) {
      this.confirmOpen = false;
      this.pendingDelete = null;
      this.cdr.markForCheck();
      return;
    }
    this.confirmOpen = false;
    this.pendingDelete = null;
    this.loading = true;
    this.cdr.markForCheck();

    this.nucleoSvc
      .delete(n.nucleoId, n.granjaId)
      .pipe(
        tap(() => {
          this.removeNucleo(n);
          this.recompute();
          this.toastSvc.success('Núcleo eliminado correctamente.', 'Listo');
        }),
        catchError(err => {
          console.error('[Nucleo] delete error', err);
          const msg = err?.error?.message || err?.error?.detail || 'No se pudo eliminar el núcleo.';
          this.toastSvc.error(msg, 'Error');
          return of(null);
        }),
        finalize(() => {
          this.loading = false;
          this.cdr.markForCheck();
        }),
        takeUntil(this.destroy$)
      )
      .subscribe();
  }

  onCancelConfirm(): void {
    this.confirmOpen = false;
    this.pendingDelete = null;
    this.cdr.markForCheck();
  }

  // ======== Estado local ========
  private upsertNucleo(n: NucleoDto): void {
    const i = this.nucleos.findIndex(
      x => String(x.nucleoId) === String(n.nucleoId) && x.granjaId === n.granjaId
    );
    if (i >= 0) {
      // Reemplazo inmutable para que OnPush detecte el cambio
      this.nucleos = [
        ...this.nucleos.slice(0, i),
        n,
        ...this.nucleos.slice(i + 1)
      ];
    } else {
      this.nucleos = [n, ...this.nucleos];
    }
  }

  private removeNucleo(n: NucleoDto): void {
    this.nucleos = this.nucleos.filter(
      x => !(String(x.nucleoId) === String(n.nucleoId) && x.granjaId === n.granjaId)
    );
  }

  /** Actualiza las listas cacheadas de opciones de filtro (evita NG0103 con OnPush). */
  private updateFilterOptions(): void {
    const companyMap = new Map<number, string>();
    (this.nucleos ?? []).forEach(n => {
      const id = n.companyId != null ? Number(n.companyId) : null;
      if (id == null || isNaN(id)) return;
      const name = (n.companyNombre ?? '').trim() || `Compañía ${id}`;
      companyMap.set(id, name);
    });
    this.filterCompanyOptions = Array.from(companyMap.entries())
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));

    const selCompany = this.selectedCompanyId != null ? Number(this.selectedCompanyId) : null;
    const farmMap = new Map<number, string>();
    (this.nucleos ?? []).forEach(n => {
      if (selCompany != null && Number(n.companyId) !== selCompany) return;
      const id = Number(n.granjaId);
      if (isNaN(id)) return;
      const name = (n.granjaNombre ?? '').trim() || `Granja ${id}`;
      farmMap.set(id, name);
    });
    this.filterFarmOptions = Array.from(farmMap.entries())
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  recompute(): void {
    this.updateFilterOptions();

    const term = (this.filtro ?? '').trim();
    const termNorm = this.normalize(term);
    let res = [...this.nucleos];

    const selCompany = this.selectedCompanyId != null ? Number(this.selectedCompanyId) : null;
    const selFarm = this.selectedFarmId != null ? Number(this.selectedFarmId) : null;

    if (selCompany != null && !isNaN(selCompany)) {
      res = res.filter(n => Number(n.companyId) === selCompany);
    }
    if (selFarm != null && !isNaN(selFarm)) {
      res = res.filter(n => Number(n.granjaId) === selFarm);
    }
    if (termNorm) {
      res = res.filter(n => {
        const haystack = [
          String(n.nucleoId ?? ''),
          this.safe(n.nucleoNombre),
          this.safe(n.granjaNombre),
          this.safe(n.companyNombre)
        ]
          .map(this.normalize)
          .join(' ');
        return haystack.includes(termNorm);
      });
    }

    this.viewNucleos = res;
    this.cdr.markForCheck();
  }

  // ======== Utils ========
  private generateUniqueId6(existing: Array<NucleoDto>): string {
    const used = new Set(existing.map(x => String(x.nucleoId)));
    let tries = 0;
    while (tries < 100) {
      const rnd = Math.floor(100000 + Math.random() * 900000); // 100000..999999
      const id = String(rnd);
      if (!used.has(id)) return id;
      tries++;
    }
    // Fallback improbable: sufijo incremental
    let seq = 100000;
    while (used.has(String(seq)) && seq <= 999999) seq++;
    return String(seq);
  }

  /** Nombre de granja: del DTO o fallback si no viene (p. ej. detalle). */
  getFarmName(n: NucleoDto): string {
    return (n.granjaNombre ?? '').trim() || '–';
  }

  /** Nombre de compañía: del DTO o fallback. */
  getCompanyName(n: NucleoDto): string {
    return (n.companyNombre ?? '').trim() || '–';
  }

  private normalize(s: string): string {
    return (s || '')
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '');
  }

  private safe(s: unknown): string {
    return s == null ? '' : String(s);
  }
}

// src/app/features/galpon/components/galpon-list/galpon-list.component.ts
import { Component, OnInit, Input, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormsModule
} from '@angular/forms';
import { finalize, forkJoin, of } from 'rxjs';
import { catchError, takeUntil } from 'rxjs/operators';
import { Subject } from 'rxjs';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faEye } from '@fortawesome/free-solid-svg-icons';

import { GalponService } from '../../services/galpon.service';
import { GalponDetailDto, CreateGalponDto, UpdateGalponDto } from '../../models/galpon.models';

import { NucleoService, NucleoDto } from '../../../nucleo/services/nucleo.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { MasterListService } from '../../../../core/services/master-list/master-list.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';

interface NucleoOption { id: string; label: string; granjaId: number; }

@Component({
  selector: 'app-galpon-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FontAwesomeModule,
    FormsModule,
    ConfirmationModalComponent,
  ],
  templateUrl: './galpon-list.component.html',
  styleUrls: ['./galpon-list.component.scss']
})
export class GalponListComponent implements OnInit, OnDestroy {
  @Input() embedded = false;
  faPlus = faPlus; faPen = faPen; faTrash = faTrash; faEye = faEye;

  loading = false;
  filtro = '';

  /** Solo galpones al cargar la tabla; farms/nucleos/master se cargan al abrir el modal. */
  allGalpones: GalponDetailDto[] = [];
  viewGalpones: GalponDetailDto[] = [];

  /** Opciones de filtro derivadas de allGalpones (misma referencia hasta recomputeList). */
  filterCompanyOptions: { id: number; name: string }[] = [];
  filterFarmOptions: { id: number; name: string }[] = [];
  filterNucleoOptions: { id: string; name: string }[] = [];

  selectedCompanyId: number | null = null;
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;

  modalOpen = false;
  form!: FormGroup;
  editing: GalponDetailDto | null = null;
  loadingModal = false;

  detailOpen = false;
  selectedDetail: GalponDetailDto | null = null;

  confirmOpen = false;
  confirmData: ConfirmationModalData = {
    title: 'Eliminar galpón',
    message: 'Esta acción no se puede deshacer.',
    confirmText: 'Eliminar',
    cancelText: 'Cancelar',
    type: 'warning',
    showCancel: true,
  };
  pendingDeleteId: string | null = null;

  farms: FarmDto[] = [];
  allNucleos: NucleoDto[] = [];
  nucleoOptions: NucleoOption[] = [];
  typegarponOptions: string[] = [];

  private destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private svc: GalponService,
    private nucleoSvc: NucleoService,
    private farmSvc: FarmService,
    private mlSvc: MasterListService,
    private toastSvc: ToastService,
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      galponId:     ['', Validators.required],
      galponNombre: ['', Validators.required],
      nucleoId:     ['', Validators.required],
      granjaId:     [null, Validators.required],
      ancho:        [''],
      largo:        [''],
      tipoGalpon:   ['']
    });

    this.form.get('nucleoId')!.valueChanges.pipe(takeUntil(this.destroy$)).subscribe((id: string) => {
      const sel = this.allNucleos.find(x => x.nucleoId === id);
      this.form.patchValue({ granjaId: sel?.granjaId ?? null }, { emitEvent: false });
    });

    this.loadGalpones();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /** Solo carga galpones; filtros se derivan de estos datos. Actualiza allGalpones y viewGalpones. */
  private loadGalpones(): void {
    this.loading = true;
    this.svc.getAll()
      .pipe(
        takeUntil(this.destroy$),
        catchError(err => {
          console.error('[Galpon] load error', err);
          this.toastSvc.error('No se pudieron cargar los galpones. Intente de nuevo.', 'Error');
          return of([]);
        }),
        finalize(() => (this.loading = false))
      )
      .subscribe(list => {
        this.allGalpones = list ?? [];
        this.recomputeList();
      });
  }

  private updateFilterOptions(): void {
    const companyMap = new Map<number, string>();
    const farmMap = new Map<number, string>();
    const nucleoMap = new Map<string, string>();

    const selCompany = this.selectedCompanyId != null ? Number(this.selectedCompanyId) : null;
    const selFarm = this.selectedFarmId != null ? Number(this.selectedFarmId) : null;

    (this.allGalpones ?? []).forEach(g => {
      if (g.company?.id != null) {
        const id = Number(g.company.id);
        if (!companyMap.has(id)) companyMap.set(id, g.company.name ?? `Empresa ${id}`);
      }
      if (selCompany != null && Number(g.company?.id) !== selCompany) return;
      if (g.farm?.id != null) {
        const id = Number(g.farm.id);
        if (!farmMap.has(id)) farmMap.set(id, g.farm.name ?? `Granja ${id}`);
      }
      if (selFarm != null && Number(g.farm?.id) !== selFarm) return;
      if (g.nucleoId && g.nucleo?.nucleoNombre) {
        nucleoMap.set(g.nucleoId, g.nucleo.nucleoNombre);
      }
    });

    this.filterCompanyOptions = Array.from(companyMap.entries()).map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
    this.filterFarmOptions = Array.from(farmMap.entries()).map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
    this.filterNucleoOptions = Array.from(nucleoMap.entries()).map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  }

  onCompanyChangeList(companyId: number | null): void {
    this.selectedCompanyId = companyId != null && !isNaN(Number(companyId)) ? Number(companyId) : null;
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.recomputeList();
  }

  onFarmChangeList(farmId: number | null): void {
    this.selectedFarmId = farmId != null && !isNaN(Number(farmId)) ? Number(farmId) : null;
    this.selectedNucleoId = null;
    this.recomputeList();
  }

  onNucleoChangeList(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId ?? null;
    this.recomputeList();
  }

  onFiltroChange(val: string): void {
    this.filtro = val ?? '';
    this.recomputeList();
  }

  resetListFilters(): void {
    this.selectedCompanyId = null;
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.filtro = '';
    this.recomputeList();
  }

  recomputeList(): void {
    this.updateFilterOptions();

    const q = (this.filtro ?? '').toLowerCase().trim();
    const selCompany = this.selectedCompanyId != null ? Number(this.selectedCompanyId) : null;
    const selFarm = this.selectedFarmId != null ? Number(this.selectedFarmId) : null;

    this.viewGalpones = this.allGalpones.filter(g => {
      if (selCompany != null && Number(g.company?.id) !== selCompany) return false;
      if (selFarm != null && Number(g.farm?.id) !== selFarm) return false;
      if (this.selectedNucleoId != null && g.nucleoId !== this.selectedNucleoId) return false;
      if (!q) return true;
      const hay = [
        g.galponId ?? '',
        g.galponNombre ?? '',
        g.nucleo?.nucleoNombre ?? '',
        g.farm?.name ?? '',
        g.company?.name ?? '',
        g.tipoGalpon ?? ''
      ].join(' ').toLowerCase();
      return hay.includes(q);
    });
  }

  // ======================
  // Acciones Tabla
  // ======================
  showDetail(g: GalponDetailDto): void {
    this.selectedDetail = g;
    this.detailOpen = true;
  }

  delete(id: string): void {
    const g = this.allGalpones.find(x => x.galponId === id);
    const name = g?.galponNombre || id;
    this.pendingDeleteId = id;
    this.confirmData = {
      title: 'Eliminar galpón',
      message: `¿Eliminar el galpón "${name}"? Esta acción no se puede deshacer.`,
      confirmText: 'Eliminar',
      cancelText: 'Cancelar',
      type: 'warning',
      showCancel: true,
    };
    this.confirmOpen = true;
  }

  onConfirmDelete(): void {
    const id = this.pendingDeleteId;
    if (!id) {
      this.confirmOpen = false;
      return;
    }
    this.confirmOpen = false;
    this.pendingDeleteId = null;
    this.loading = true;

    this.svc.delete(id).pipe(
      takeUntil(this.destroy$),
      finalize(() => (this.loading = false))
    ).subscribe({
      next: () => {
        if (this.selectedDetail?.galponId === id) {
          this.detailOpen = false;
          this.selectedDetail = null;
        }
        this.toastSvc.success('Galpón eliminado correctamente.', 'Listo');
        this.loadGalponesAgain();
      },
      error: err => {
        const msg = err?.error?.message || err?.error?.detail || 'No se pudo eliminar el galpón.';
        this.toastSvc.error(msg, 'Error');
      },
    });
  }

  onCancelConfirm(): void {
    this.confirmOpen = false;
    this.pendingDeleteId = null;
  }


  private loadGalponesAgain(): void {
    this.loadGalpones();
  }

  openModal(g?: GalponDetailDto): void {
    this.editing = g ?? null;
    this.modalOpen = true;
    this.loadModalData(g);
  }

  /** Carga farms, nucleos y tipo galpón solo al abrir el modal (crear/editar). */
  private loadModalData(g?: GalponDetailDto): void {
    if (this.farms.length > 0 && this.allNucleos.length > 0 && this.typegarponOptions.length > 0) {
      this.applyFormInModal(g);
      return;
    }
    this.loadingModal = true;
    forkJoin({
      farms: this.farmSvc.getAll().pipe(catchError(() => of([]))),
      nucleos: this.nucleoSvc.getAll().pipe(catchError(() => of([]))),
      typeGalpon: this.mlSvc.getByKey('type_galpon').pipe(catchError(() => of(null)))
    }).pipe(
      finalize(() => (this.loadingModal = false)),
      takeUntil(this.destroy$)
    ).subscribe(({ farms, nucleos, typeGalpon }) => {
      this.farms = farms ?? [];
      this.allNucleos = nucleos ?? [];
      this.typegarponOptions = typeGalpon?.optionValues ?? (Array.isArray(typeGalpon?.options)
        ? (typeGalpon.options as { value?: string }[]).map(o => o?.value ?? '').filter(Boolean)
        : []);
      const farmNameById = new Map(this.farms.map(f => [f.id, f.name]));
      this.nucleoOptions = this.allNucleos.map(n => ({
        id: n.nucleoId,
        granjaId: n.granjaId,
        label: `${n.nucleoNombre} (Granja ${farmNameById.get(n.granjaId) ?? '#' + n.granjaId})`
      }));
      this.applyFormInModal(g);
    });
  }

  private applyFormInModal(g?: GalponDetailDto): void {
    if (g) {
      this.form.reset({
        galponId: g.galponId,
        galponNombre: g.galponNombre,
        nucleoId: g.nucleoId,
        granjaId: g.granjaId,
        ancho: g.ancho ?? '',
        largo: g.largo ?? '',
        tipoGalpon: g.tipoGalpon ?? ''
      });
      this.form.get('galponId')?.disable();
    } else {
      const lastNum = this.allGalpones
        .map(x => parseInt(String(x.galponId || '').replace(/\D/g, ''), 10))
        .filter(n => !isNaN(n))
        .reduce((m, c) => Math.max(m, c), 0);
      const newId = `G${(lastNum + 1).toString().padStart(4, '0')}`;
      this.form.reset({
        galponId: newId,
        galponNombre: '',
        nucleoId: '',
        granjaId: null,
        ancho: '',
        largo: '',
        tipoGalpon: ''
      });
      this.form.get('galponId')?.enable();
    }
  }

  save(): void {
    if (this.form.invalid) return;

    const raw = this.form.getRawValue(); // incluye galponId si estaba disabled
    const payload: CreateGalponDto | UpdateGalponDto = {
      galponId:     raw.galponId,
      galponNombre: raw.galponNombre,
      nucleoId:     raw.nucleoId,
      granjaId:     raw.granjaId,
      ancho:        raw.ancho || null,
      largo:        raw.largo || null,
      tipoGalpon:   raw.tipoGalpon || null
    };

    this.loading = true;
    const call$ = this.editing
      ? this.svc.update(payload as UpdateGalponDto)
      : this.svc.create(payload as CreateGalponDto);

    call$
      .pipe(
        takeUntil(this.destroy$),
        catchError(err => {
          const msg = err?.error?.message || err?.error?.detail || 'No se pudo guardar el galpón.';
          this.toastSvc.error(msg, 'Error');
          return of(null);
        }),
        finalize(() => {
          this.loading = false;
          this.modalOpen = false;
          this.loadGalponesAgain();
        })
      )
      .subscribe(res => {
        if (res) {
          this.toastSvc.success(this.editing ? 'Galpón actualizado correctamente.' : 'Galpón creado correctamente.', 'Listo');
        }
      });
  }

  // ======================
  // Helpers de vista
  // ======================
  getArea(g: GalponDetailDto | null): string {
    if (!g?.ancho || !g?.largo) return '–';
    const a = parseFloat(String(g.ancho));
    const l = parseFloat(String(g.largo));
    if (isNaN(a) || isNaN(l)) return '–';
    return (a * l).toFixed(2);
  }

  getGranjaNombreByNucleoId(nucleoId: string | null | undefined): string {
    if (!nucleoId) return '–';
    const n = this.allNucleos.find(x => x.nucleoId === nucleoId);
    const f = this.farms.find(y => y.id === n?.granjaId);
    return f?.name ?? (n ? `#${n.granjaId}` : '–');
  }
}

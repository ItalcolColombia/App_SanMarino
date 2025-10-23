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
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPen, faPlus, faTrash, faSearch } from '@fortawesome/free-solid-svg-icons';

import {
  CreateNucleoDto,
  NucleoDto,
  NucleoService,
  UpdateNucleoDto
} from '../../services/nucleo.service';
import { FarmDto, FarmService } from '../../../farm/services/farm.service';
import { Company, CompanyService } from '../../../../core/services/company/company.service';

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
    SidebarComponent,
    FontAwesomeModule
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

  // Datos (estado local)
  nucleos: NucleoDto[] = [];
  viewNucleos: NucleoDto[] = [];
  farms: FarmDto[] = [];
  companies: Company[] = [];

  // Mapas auxiliares para lookups
  farmMap: Record<number, string> = {};
  companyMap: Record<number, string> = {};

  // UI state
  loading = false;
  modalOpen = false;
  editing: NucleoDto | null = null;

  // Formulario
  form!: FormGroup;

  // lifecycle
  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly fb: FormBuilder,
    private readonly nucleoSvc: NucleoService,
    private readonly farmSvc: FarmService,
    private readonly companySvc: CompanyService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  // ======== Lifecycle ========
  ngOnInit(): void {
    this.buildForm();

    // Cargar granjas y compañías en paralelo y luego recomputar
    this.loading = true;

    this.farmSvc
      .getAll()
      .pipe(
        tap(list => {
          this.farms = list ?? [];
          this.farmMap = {};
          this.farms.forEach(f => (this.farmMap[f.id] = f.name));
        }),
        catchError(err => {
          console.error('[Farms] load error', err);
          this.farms = [];
          this.farmMap = {};
          return of([]);
        })
      )
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        // Luego compañías
        this.companySvc
          .getAll()
          .pipe(
            tap(clist => {
              this.companies = clist ?? [];
              this.companyMap = {};
              this.companies.forEach(c => {
                if (c.id !== undefined && c.id !== null) {
                  this.companyMap[c.id] = c.name ?? '';
                }
              });
            }),
            catchError(err => {
              console.error('[Companies] load error', err);
              this.companies = [];
              this.companyMap = {};
              return of([]);
            }),
            finalize(() => {
              // Cuando ya hay farms + companies, carga núcleos
              this.loadNucleos();
            }),
            takeUntil(this.destroy$)
          )
          .subscribe();
      });
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

  get farmsFiltered(): FarmDto[] {
    if (this.selectedCompanyId == null) return this.farms;
    return this.farms.filter(f => f.companyId === this.selectedCompanyId);
  }

  onCompanyChange(val: number | null): void {
    this.selectedCompanyId = val;

    // Si la granja seleccionada no pertenece a la compañía actual, limpiar
    if (
      this.selectedFarmId != null &&
      !this.farmsFiltered.some(f => f.id === this.selectedFarmId)
    ) {
      this.selectedFarmId = null;
    }
    this.recompute();
  }

  resetFilters(): void {
    this.filtro = '';
    this.selectedCompanyId = null;
    this.selectedFarmId = null;
    this.recompute();
  }

  // ======== Data load ========
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

    if (n) {
      // Editar
      this.form.reset({
        nucleoId: n.nucleoId,
        granjaId: n.granjaId,
        nucleoNombre: n.nucleoNombre
      });
    } else {
      // Crear: generar ID de 6 dígitos único basado en lo cargado
      const newId = this.generateUniqueId6(this.nucleos);
      this.form.reset({
        nucleoId: newId,
        granjaId: null,
        nucleoNombre: ''
      });
    }

    this.modalOpen = true;
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
          // Mantener estado local sin recargar
          this.upsertNucleo(saved);
          this.recompute();
          this.closeModal();
        }),
        catchError(err => {
          console.error('[Nucleo] save error', err);
          // Aquí podrías emitir una notificación/toast
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
    if (!confirm(`¿Eliminar el núcleo “${n.nucleoNombre}”?`)) return;

    this.loading = true;
    this.nucleoSvc
      .delete(n.nucleoId, n.granjaId)
      .pipe(
        tap(() => {
          this.removeNucleo(n);
          this.recompute();
        }),
        catchError(err => {
          console.error('[Nucleo] delete error', err);
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

  recompute(): void {
    const term = this.normalize(this.filtro);
    let res = [...this.nucleos];

    // Filtro por compañía
    if (this.selectedCompanyId != null) {
      res = res.filter(n => {
        const farm = this.farms.find(f => f.id === n.granjaId);
        return farm ? farm.companyId === this.selectedCompanyId : false;
      });
    }

    // Filtro por granja
    if (this.selectedFarmId != null) {
      res = res.filter(n => n.granjaId === this.selectedFarmId);
    }

    // Filtro por texto
    if (term) {
      res = res.filter(n => {
        const haystack = [
          String(n.nucleoId ?? ''),
          this.safe(n.nucleoNombre),
          this.safe(this.getFarmName(n.granjaId))
        ]
          .map(this.normalize)
          .join(' ');
        return haystack.includes(term);
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

  getFarmName(granjaId: number): string {
    return this.farmMap[granjaId] || '–';
  }

  getCompanyName(granjaId: number): string {
    const farm = this.farms.find(f => f.id === granjaId);
    return farm ? this.companyMap[farm.companyId] || '–' : '–';
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

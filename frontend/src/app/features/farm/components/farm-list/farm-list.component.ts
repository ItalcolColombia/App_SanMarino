// src/app/features/farm/components/farm-list/farm-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { finalize, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faMagnifyingGlass, faEye, faTimes } from '@fortawesome/free-solid-svg-icons';

import { FarmService, FarmDto, UpdateFarmDto } from '../../services/farm.service';
import { Company, CompanyService } from '../../../../core/services/company/company.service';
import { RegionalService, RegionalDto } from '../../services/regional.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { MasterListService } from '../../../../core/services/master-list/master-list.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';

import { DepartamentoService, DepartamentoDto } from '../../services/departamento.service';
import { CiudadService, CiudadDto } from '../../services/ciudad.service';
import { PaisService, PaisDto } from '../../services/pais.service';

@Component({
  selector: 'app-farm-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    FontAwesomeModule,
    ConfirmationModalComponent,
  ],
  templateUrl: './farm-list.component.html',
  styleUrls: ['./farm-list.component.scss'],
})
export class FarmListComponent implements OnInit {
  // Icons
  faPlus = faPlus;
  faPen = faPen;
  faTrash = faTrash;
  faMagnifyingGlass = faMagnifyingGlass;
  faEye = faEye;
  faTimes = faTimes;

  // Estado general
  loading = false;
  loadingTable = false; // Estado de carga específico para la tabla
  embedded = false;

  // Datos base (solo lo esencial para la tabla)
  farms: FarmDto[] = [];
  companies: Company[] = [];
  allRegionales: RegionalDto[] = [];  // Todas las regionales
  regionalesByCompany: Map<number, RegionalDto[]> = new Map(); // Regionales por compañía
  
  // Datos para el modal (se cargan solo cuando se abre)
  paises: PaisDto[] = [];
  departamentos: DepartamentoDto[] = [];
  ciudades: CiudadDto[] = [];

  // Vista filtrada
  viewFarms: FarmDto[] = [];

  // Filtros (sobre los registros cargados)
  selectedRegional: string | null = null;
  selectedDepartamento: string | null = null;
  selectedCiudad: string | null = null;
  filtroNombre = '';
  filtroEstado: 'A' | 'I' | '' = '';
  filtroTexto = '';

  // Modal / form
  modalOpen = false;
  form!: FormGroup;
  editing: FarmDto | null = null;
  loadingModal = false; // Carga de datos del modal
  
  // Modal detalle
  detailOpen = false;
  selectedDetail: FarmDto | null = null;
  loadingDetail = false;

  // Modal confirmación eliminar
  confirmOpen = false;
  confirmData: ConfirmationModalData = {
    title: 'Eliminar granja',
    message: 'Esta acción no se puede deshacer.',
    confirmText: 'Eliminar',
    cancelText: 'Cancelar',
    type: 'warning',
    showCancel: true,
  };
  pendingDeleteId: number | null = null;

  // Opciones de Regional desde lista maestra (region_option_key); cada opción tiene id y value para enviar al guardar
  regionalOptionItems: { id: number; value: string }[] = [];

  // País deshabilitado en el modal (siempre viene del storage)
  paisDisabled = true;

  // índices rápidos por ID
  private dptoById = new Map<number, DepartamentoDto>();
  private ciudadById = new Map<number, CiudadDto>();
  private regionalById = new Map<number, RegionalDto>(); // Mapa regionalId -> RegionalDto
  private regionalByName = new Map<string, RegionalDto>(); // Mapa nombre -> RegionalDto

  constructor(
    private readonly fb: FormBuilder,
    private readonly farmSvc: FarmService,
    private readonly companySvc: CompanyService,
    private readonly regionalSvc: RegionalService,
    private readonly dptoSvc: DepartamentoService,
    private readonly ciudadSvc: CiudadService,
    private readonly paisSvc: PaisService,
    private readonly toastSvc: ToastService,
    private readonly storage: TokenStorageService,
    private readonly masterListSvc: MasterListService
  ) {}

  // ================
  // Ciclo de vida
  // ================
  ngOnInit(): void {
    this.buildForm();
    this.loadAll();
  }

  // ==================
  // Inicialización
  // ==================
  private buildForm(): void {
    this.form = this.fb.group({
      id: [null],
      companyId: [null, Validators.required],
      companyDisplayName: [''], // Solo lectura: nombre de la empresa desde storage
      name: ['', [Validators.required, Validators.maxLength(200)]],
      status: ['A', Validators.required],
      regionalOptionId: [null as number | null], // Id de la opción en lista maestra (region_option_key); backend lo resuelve a regional_id
      paisId: [null],
      departamentoId: [null],
      ciudadId: [null],
      department: [''],
      city: [''],
    });
  }

  /** Solo carga la lista de granjas. Companies, regionales, países, etc. se cargan al abrir el modal (crear/editar) o el detalle. */
  private loadAll(): void {
    this.loading = true;
    this.loadingTable = true;
    this.farmSvc.getAll()
      .pipe(finalize(() => {
        this.loading = false;
        this.loadingTable = false;
      }))
      .subscribe({
        next: (farms) => {
          this.farms = farms ?? [];
          this.farms.forEach(f => {
            f.department = f.departamentoNombre ?? null;
            f.city = f.ciudadNombre ?? null;
            f.regional = f.regionalNombre ?? null;
          });
          this.recomputeList();
        },
        error: (err) => {
          console.error('Error cargando granjas:', err);
          this.toastSvc.error('No se pudieron cargar las granjas. Intente de nuevo.', 'Error');
          this.loadingTable = false;
        }
      });
  }

  /** Carga países del modal; al terminar, con el país en sesión dispara la carga de departamentos (y así la cascada Departamento → Ciudad). */
  private loadModalData(): void {
    if (this.paises.length > 0) {
      this.dispararCascadaPaisSesion();
      return;
    }

    this.loadingModal = true;
    this.paisSvc.getAll().pipe(
      catchError(err => {
        console.error('Error cargando países:', err);
        return [[]];
      }),
      finalize(() => (this.loadingModal = false))
    ).subscribe({
      next: (paises) => {
        this.paises = paises ?? [];
        this.dispararCascadaPaisSesion();
      },
      error: (err) => {
        console.error('Error cargando datos del modal:', err);
        this.toastSvc.warning('No se pudieron cargar los países.', 'Aviso');
      }
    });
  }

  /** Con el país en sesión (storage), carga departamentos; las ciudades se cargan al elegir departamento. */
  private dispararCascadaPaisSesion(): void {
    const session = this.storage.get();
    const paisId = session?.activePaisId ?? null;
    if (paisId != null) {
      this.loadDepartamentosByPaisId(paisId);
    } else {
      this.departamentos = [];
      this.ciudades = [];
    }
  }

  /** Carga opciones de Regional desde lista maestra (region_option_key); cada opción tiene id y value. Opcional: callback con las opciones (p. ej. para rellenar form en edición). */
  private updateRegionalesDisponibles(companyId: number | null, onLoaded?: (options: { id: number; value: string }[]) => void): void {
    if (companyId == null) {
      this.regionalOptionItems = [];
      onLoaded?.([]);
      return;
    }
    this.masterListSvc.getByKey('region_option_key', companyId).pipe(
      catchError(() => of({ id: 0, key: '', name: '', options: [] as { id: number; value: string }[] }))
    ).subscribe(ml => {
      this.regionalOptionItems = (ml?.options ?? []).map(o => ({ id: o.id, value: (o.value ?? '').trim() })).filter(o => o.value !== '');
      const currentId = this.form.get('regionalOptionId')?.value;
      if (currentId != null && !this.regionalOptionItems.some(o => o.id === currentId)) {
        this.form.patchValue({ regionalOptionId: null });
      }
      onLoaded?.(this.regionalOptionItems);
    });
  }

  // =========================
  // Filtros / Vista tabla
  // =========================
  recomputeList(): void {
    const nameFilter = (this.filtroNombre || '').trim().toLowerCase();
    const text = (this.filtroTexto || '').trim().toLowerCase();
    const regionalFilter = (this.selectedRegional ?? '').trim().toLowerCase();
    const deptFilter = (this.selectedDepartamento ?? '').trim().toLowerCase();
    const cityFilter = (this.selectedCiudad ?? '').trim().toLowerCase();
    const estado = this.filtroEstado;

    this.viewFarms = (this.farms ?? []).filter((f) => {
      const regionalTxt = (f.regionalNombre ?? f.regional ?? '').trim().toLowerCase();
      const deptTxt = (f.departamentoNombre ?? f.department ?? '').trim().toLowerCase();
      const cityTxt = (f.ciudadNombre ?? f.city ?? '').trim().toLowerCase();
      const nombreTxt = (f.name ?? '').toLowerCase();
      const companyTxt = (f.companyNombre ?? this.companyName(f.companyId) ?? '').toLowerCase();

      const okRegional = regionalFilter ? regionalTxt === regionalFilter : true;
      const okDepartamento = deptFilter ? deptTxt === deptFilter : true;
      const okCiudad = cityFilter ? cityTxt === cityFilter : true;
      const okNombre = nameFilter ? nombreTxt.includes(nameFilter) : true;
      const okEstado = estado ? f.status === estado : true;

      const okText = text
        ? (nombreTxt.includes(text) ||
            regionalTxt.includes(text) ||
            companyTxt.includes(text) ||
            deptTxt.includes(text) ||
            cityTxt.includes(text))
        : true;

      return okRegional && okDepartamento && okCiudad && okNombre && okEstado && okText;
    });
  }

  resetFilters(): void {
    this.selectedRegional = null;
    this.selectedDepartamento = null;
    this.selectedCiudad = null;
    this.filtroNombre = '';
    this.filtroEstado = '';
    this.filtroTexto = '';
    this.recomputeList();
  }

  /** Opciones únicas para filtros, derivadas de los registros cargados (farms). */
  get filterRegionalOptions(): string[] {
    const set = new Set(
      (this.farms ?? [])
        .map(f => (f.regionalNombre ?? f.regional ?? '').trim())
        .filter(Boolean)
    );
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  }

  get filterDepartamentoOptions(): string[] {
    const set = new Set(
      (this.farms ?? [])
        .map(f => (f.departamentoNombre ?? f.department ?? '').trim())
        .filter(Boolean)
    );
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  }

  get filterCiudadOptions(): string[] {
    const set = new Set(
      (this.farms ?? [])
        .map(f => (f.ciudadNombre ?? f.city ?? '').trim())
        .filter(Boolean)
    );
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  }

  // =============
  // Modal
  // =============
  openModal(farm?: FarmDto): void {
    this.loadModalData();
    this.editing = farm ?? null;

    const session = this.storage.get();
    const companyId = session?.activeCompanyId ?? null;
    const companyName = session?.companyPaises?.find(p => p.companyId === companyId)?.companyName ?? '';
    const paisId = session?.activePaisId ?? null;

    if (farm) {
      this.loadingModal = true;
      this.farmSvc.getById(farm.id).pipe(
        finalize(() => (this.loadingModal = false)),
        catchError(err => {
          console.error('Error cargando granja:', err);
          this.toastSvc.error('No se pudieron cargar los datos de la granja.', 'Error');
          return [farm];
        })
      ).subscribe({
        next: (farmData) => {
          const regionalName = (farmData.regionalNombre ?? '').trim().toLowerCase();
          this.updateRegionalesDisponibles(companyId ?? farmData.companyId ?? null, (opts) => {
            const matchingOptionId = regionalName && opts.length
              ? opts.find(o => (o.value || '').trim().toLowerCase() === regionalName)?.id ?? null
              : null;
            this.form.reset({
              id: farmData.id ?? null,
              companyId: companyId ?? farmData.companyId ?? null,
              companyDisplayName: companyName || this.companyName(farmData.companyId),
              name: farmData.name ?? '',
              regionalOptionId: matchingOptionId ?? null,
              status: farmData.status ?? 'A',
              paisId: paisId ?? null,
              departamentoId: farmData.departamentoId ?? null,
              ciudadId: farmData.ciudadId ?? null,
              department: farmData.department ?? '',
              city: farmData.city ?? '',
            });
          });
          if (paisId != null) {
            this.loadDepartamentosByPaisId(paisId).then(() => {
              if (farmData.departamentoId) {
                this.loadCiudadesByDepartamentoId(farmData.departamentoId);
              }
            });
          } else if (farmData.departamentoId) {
            const dpto = this.dptoById.get(farmData.departamentoId);
            const pid = (dpto as any)?.paisId ?? null;
            if (pid != null) {
              this.form.patchValue({ paisId: pid });
              this.loadDepartamentosByPaisId(pid).then(() => this.loadCiudadesByDepartamentoId(farmData.departamentoId!));
            }
          }
        }
      });
    } else {
      const nextId = this.getNextFarmId();
      this.form.reset({
        id: nextId,
        companyId: companyId ?? null,
        companyDisplayName: companyName,
        name: '',
        regionalOptionId: null,
        status: 'A',
        paisId: paisId ?? null,
        departamentoId: null,
        ciudadId: null,
        department: '',
        city: '',
      });
      this.updateRegionalesDisponibles(companyId);
      this.departamentos = [];
      this.ciudades = [];
      if (paisId != null) {
        this.loadDepartamentosByPaisId(paisId);
      }
    }

    this.modalOpen = true;
  }

  private loadDepartamentosByPaisId(paisId: number): Promise<void> {
    return new Promise((resolve) => {
      this.dptoSvc.getByPaisId(paisId).subscribe({
        next: ds => {
          this.departamentos = ds ?? [];
          ds?.forEach(d => this.dptoById.set(d.departamentoId, d));
          resolve();
        },
        error: () => {
          this.departamentos = [];
          resolve();
        }
      });
    });
  }

  private loadCiudadesByDepartamentoId(departamentoId: number): void {
    this.ciudadSvc.getByDepartamentoId(departamentoId).subscribe({
      next: cs => {
        this.ciudades = cs ?? [];
        cs?.forEach(c => this.ciudadById.set(c.municipioId, c));
      },
      error: () => { this.ciudades = []; }
    });
  }

  cancel(): void {
    this.modalOpen = false;
    this.form.reset();
  }

  // =========================
  // Cascada País → Dpto → Ciudad
  // =========================
  onPaisChange(): void {
    const paisId = this.form.get('paisId')?.value;
    // limpiar descendientes
    this.form.patchValue({ departamentoId: null, ciudadId: null });
    this.departamentos = [];
    this.ciudades = [];
    
    // Asegurar que los datos del modal estén cargados
    if (this.departamentos.length === 0) {
      this.loadModalData();
    }
    
    if (paisId != null) {
      // Filtrar de los datos ya cargados si están disponibles
      if (this.departamentos.length > 0) {
        this.departamentos = this.departamentos.filter(d => (d as any)?.paisId === paisId);
      } else {
        // Si no están cargados, hacer petición
        this.dptoSvc.getByPaisId(+paisId).subscribe({
          next: ds => {
            this.departamentos = ds ?? [];
            // Actualizar índice
            ds?.forEach(d => this.dptoById.set(d.departamentoId, d));
          },
          error: err => {
            console.error('Error cargando departamentos:', err);
            this.departamentos = [];
          }
        });
      }
    }
  }

  onDepartamentoChange(): void {
    const dptoId = this.form.get('departamentoId')?.value;
    this.form.patchValue({ ciudadId: null, city: '' });
    this.ciudades = [];
    
    if (dptoId != null) {
      // Filtrar de los datos ya cargados si están disponibles
      if (this.ciudades.length > 0) {
        this.ciudades = this.ciudades.filter(c => c.departamentoId === dptoId);
      } else {
        // Si no están cargados, hacer petición
        this.ciudadSvc.getByDepartamentoId(+dptoId).subscribe({
          next: cs => {
            this.ciudades = cs ?? [];
            // Actualizar índice
            cs?.forEach(c => this.ciudadById.set(c.municipioId, c));
          },
          error: err => {
            console.error('Error cargando ciudades:', err);
            this.ciudades = [];
          }
        });
      }
    }
  }

  // =============
  // Persistencia
  // =============
  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toastSvc.warning('Complete todos los campos requeridos.', 'Formulario incompleto');
      return;
    }

    const raw = this.form.getRawValue();

    // Normaliza status a 'A' | 'I'
    const status: 'A' | 'I' = (String(raw.status ?? 'A').toUpperCase() === 'I' ? 'I' : 'A');

    // El valor seleccionado en el select es el id de la opción (lista maestra); se envía como regionalId para que se guarde
    const regionalId = raw?.regionalOptionId != null && raw?.regionalOptionId !== '' ? Number(raw.regionalOptionId) : null;

    const dtoBase = {
      name: (raw.name ?? '').trim(),
      companyId: Number(raw.companyId ?? 1),
      status,
      regionalId,
      departamentoId: raw?.departamentoId != null && raw?.departamentoId !== '' ? Number(raw.departamentoId) : null,
      ciudadId: raw?.ciudadId != null && raw?.ciudadId !== '' ? Number(raw.ciudadId) : null,
    };

    this.loading = true;

    if (this.editing) {
      const dto = { id: this.editing.id, ...dtoBase };
      this.farmSvc
        .update(dto)
        .pipe(
          finalize(() => (this.loading = false)),
          catchError(err => {
            const errorMsg = err?.error?.message || err?.error?.detail || 'Error al actualizar la granja';
            this.toastSvc.error(errorMsg, 'Error');
            throw err;
          })
        )
        .subscribe({
          next: () => {
            this.modalOpen = false;
            this.toastSvc.success('Granja actualizada correctamente.', 'Listo');
            this.loadAll();
          }
        });
    } else {
      this.farmSvc
        .create(dtoBase)
        .pipe(
          finalize(() => (this.loading = false)),
          catchError(err => {
            const errorMsg = err?.error?.message || err?.error?.detail || 'Error al crear la granja';
            this.toastSvc.error(errorMsg, 'Error');
            throw err;
          })
        )
        .subscribe({
          next: () => {
            this.modalOpen = false;
            this.toastSvc.success('Granja creada correctamente.', 'Listo');
            this.loadAll();
          }
        });
    }
  }

  delete(id: number): void {
    const farm = this.farms.find(f => f.id === id);
    const farmName = farm?.name || 'esta granja';
    this.pendingDeleteId = id;
    this.confirmData = {
      title: 'Eliminar granja',
      message: `¿Eliminar la granja "${farmName}"? Esta acción no se puede deshacer.`,
      confirmText: 'Eliminar',
      cancelText: 'Cancelar',
      type: 'warning',
      showCancel: true,
    };
    this.confirmOpen = true;
  }

  onConfirmDelete(): void {
    const id = this.pendingDeleteId;
    if (id == null) {
      this.confirmOpen = false;
      return;
    }
    this.confirmOpen = false;
    this.pendingDeleteId = null;

    this.loading = true;
    this.farmSvc
      .delete(id)
      .pipe(
        finalize(() => (this.loading = false)),
        catchError(err => {
          const errorMsg = err?.error?.message || err?.error?.detail || 'No se pudo eliminar la granja.';
          this.toastSvc.error(errorMsg, 'Error');
          throw err;
        })
      )
      .subscribe({
        next: () => {
          this.toastSvc.success('Granja eliminada correctamente.', 'Listo');
          this.loadAll();
        },
      });
  }

  onCancelConfirm(): void {
    this.confirmOpen = false;
    this.pendingDeleteId = null;
  }

  showDetail(farm: FarmDto): void {
    this.detailOpen = true;
    this.loadingDetail = true;
    this.selectedDetail = null; // Limpiar mientras carga

    // Cargar datos completos por ID
    this.farmSvc.getById(farm.id).pipe(
      finalize(() => (this.loadingDetail = false)),
      catchError(err => {
        console.error('Error cargando detalle de granja:', err);
        this.toastSvc.error('No se pudo cargar el detalle.', 'Error');
        // Usar datos básicos como fallback
        this.selectedDetail = farm;
        return [farm];
      })
    ).subscribe({
      next: (farmData) => {
        // El backend ya devuelve regionalNombre, companyNombre, etc.
        farmData.regional = farmData.regionalNombre ?? farmData.regional ?? null;
        this.selectedDetail = farmData;
      }
    });
  }

  closeDetail(): void {
    this.detailOpen = false;
    this.selectedDetail = null;
  }

  // =============
  // Helpers
  // =============
  /** Calcula el siguiente ID consecutivo a partir de la lista cargada (máximo + 1). */
  private getNextFarmId(): number {
    const ids = (this.farms ?? []).map(f => Number(f.id)).filter(n => Number.isFinite(n));
    if (!ids.length) return 1;
    return Math.max(...ids) + 1;
    // Si quieres evitar “huecos”, aquí podrías buscar el menor entero no usado.
  }

  companyName(id: number | null | undefined): string {
    if (id == null) return '';
    return this.companies.find((c) => c.id === id)?.name ?? '';
  }

  getRegionalName(regionalId: number | null | undefined): string {
    if (regionalId == null) return '—';
    return this.regionalById.get(regionalId)?.regionalNombre ?? '—';
  }

  trackByFarm = (_: number, f: FarmDto) => f.id;
}

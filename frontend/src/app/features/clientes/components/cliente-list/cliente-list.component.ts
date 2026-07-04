import {
  Component, OnInit, signal, computed, inject, DestroyRef
} from '@angular/core';

import {
  ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormsModule
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs/operators';

import { ClienteService } from '../../services/cliente.service';
import {
  ClienteDto,
  CreateClienteRequest,
  UpdateClienteRequest,
  TIPOS_DOCUMENTO,
  TIPOS_CLIENTE,
  ZONAS_PANAMA
} from '../../models/cliente.models';
import { ToastService } from '../../../../shared/services/toast.service';
import {
  ConfirmationModalComponent,
  ConfirmationModalData
} from '../../../../shared/components/confirmation-modal/confirmation-modal.component';

import { PaisService, PaisDto } from '../../../farm/services/pais.service';
import { DepartamentoService, DepartamentoDto } from '../../../farm/services/departamento.service';
import { CiudadService, CiudadDto } from '../../../farm/services/ciudad.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';

@Component({
  selector: 'app-cliente-list',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, ConfirmationModalComponent],
  templateUrl: './cliente-list.component.html',
  styleUrls: ['./cliente-list.component.scss']
})
export class ClienteListComponent implements OnInit {

  // ── Injected dependencies ────────────────────────────────────────────────
  private readonly svc        = inject(ClienteService);
  private readonly fb         = inject(FormBuilder);
  private readonly toastSvc   = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly paisSvc    = inject(PaisService);
  private readonly deptoSvc   = inject(DepartamentoService);
  private readonly ciudadSvc  = inject(CiudadService);
  private readonly tokenSvc   = inject(TokenStorageService);

  // ── Catalogs ─────────────────────────────────────────────────────────────
  readonly tiposDocumento = TIPOS_DOCUMENTO;
  readonly tiposCliente   = TIPOS_CLIENTE;
  readonly zonas          = ZONAS_PANAMA;

  // Listas dinámicas para la cascada País → Provincia → Distrito.
  readonly paises     = signal<PaisDto[]>([]);
  readonly provincias = signal<DepartamentoDto[]>([]);
  readonly distritos  = signal<CiudadDto[]>([]);

  // ── Loading states ────────────────────────────────────────────────────────
  readonly loading      = signal(false);
  readonly loadingModal = signal(false);

  // ── Data ──────────────────────────────────────────────────────────────────
  readonly allClientes = signal<ClienteDto[]>([]);

  // ── Filter signals ────────────────────────────────────────────────────────
  readonly filtro            = signal('');
  readonly filterTipoCliente = signal('');
  readonly filterPais        = signal('');

  // ── Derived filtered list (computed) ─────────────────────────────────────
  readonly filteredClientes = computed(() => {
    let result = this.allClientes();

    const q = this.filtro().toLowerCase().trim();
    if (q) {
      result = result.filter(c =>
        c.nombre.toLowerCase().includes(q) ||
        c.numeroIdentificacion.toLowerCase().includes(q) ||
        (c.correo?.toLowerCase().includes(q) ?? false)
      );
    }

    const tipo = this.filterTipoCliente();
    if (tipo) {
      result = result.filter(c => c.tipoCliente === tipo);
    }

    const pais = this.filterPais().toLowerCase().trim();
    if (pais) {
      result = result.filter(c => c.pais?.toLowerCase().includes(pais));
    }

    return result;
  });

  readonly hasActiveFilters = computed(() =>
    !!(this.filtro() || this.filterTipoCliente() || this.filterPais())
  );

  // ── Modal visibility signals ──────────────────────────────────────────────
  readonly modalOpen   = signal(false);
  readonly detailOpen  = signal(false);
  readonly confirmOpen = signal(false);

  // ── Selection signals ─────────────────────────────────────────────────────
  readonly editing         = signal<ClienteDto | null>(null);
  readonly selectedDetail  = signal<ClienteDto | null>(null);
  readonly pendingDeleteId = signal<number | null>(null);

  // ── Reactive form ─────────────────────────────────────────────────────────
  form!: FormGroup;

  // ── Confirm modal data ────────────────────────────────────────────────────
  confirmData: ConfirmationModalData = {
    title:       'Eliminar cliente',
    message:     '',
    confirmText: 'Eliminar',
    cancelText:  'Cancelar',
    type:        'warning',
    showCancel:  true
  };

  // ─────────────────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.buildForm();
    this.loadClientes();
    this.loadLookups();
  }

  // ── Private helpers ───────────────────────────────────────────────────────
  private buildForm(): void {
    this.form = this.fb.group({
      tipoDocumento:        ['', Validators.required],
      numeroIdentificacion: ['', [Validators.required, Validators.maxLength(100)]],
      nombre:               ['', [Validators.required, Validators.maxLength(200)]],
      correo:               ['', [Validators.email, Validators.maxLength(200)]],
      telefono:             ['', Validators.maxLength(50)],
      tipoCliente:          [''],
      pais:                 ['', Validators.maxLength(100)],
      provincia:            ['', Validators.maxLength(100)],
      distrito:             ['', Validators.maxLength(100)],
      planta:               ['', Validators.maxLength(100)],
      zona:                 ['', Validators.maxLength(100)],
      status:               ['A']
    });
  }

  /**
   * Carga la lista de países y, si la sesión tiene país activo,
   * lo preselecciona en el form y dispara la cascada de provincias.
   */
  private loadLookups(): void {
    this.paisSvc.getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.paises.set(list ?? []);
          this.preselectActivePais();
        },
        error: () => {
          this.paises.set([]);
          this.toastSvc.error('No se pudieron cargar los países.', 'Error');
        }
      });
  }

  /**
   * Si la sesión activa tiene `activePaisNombre`, busca el país en la lista
   * por nombre (case-insensitive), setea el control `pais` con el nombre
   * canónico y dispara la carga de provincias para ese país.
   */
  private preselectActivePais(): void {
    const session = this.tokenSvc.get();
    const active  = session?.activePaisNombre?.trim();
    if (!active) return;

    const match = this.paises().find(
      p => p.paisNombre.localeCompare(active, undefined, { sensitivity: 'base' }) === 0
    );
    if (!match) return;

    this.form?.patchValue({ pais: match.paisNombre });
    this.loadProvincias(match.paisId);
  }

  /** Carga provincias (departamentos) para un país dado. */
  private loadProvincias(paisId: number): void {
    this.deptoSvc.getByPaisId(paisId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => this.provincias.set(list ?? []),
        error: () => {
          this.provincias.set([]);
          this.toastSvc.error('No se pudieron cargar las provincias.', 'Error');
        }
      });
  }

  /** Carga distritos (municipios) para una provincia dada. */
  private loadDistritos(departamentoId: number): void {
    this.ciudadSvc.getByDepartamentoId(departamentoId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => this.distritos.set(list ?? []),
        error: () => {
          this.distritos.set([]);
          this.toastSvc.error('No se pudieron cargar los distritos.', 'Error');
        }
      });
  }

  // ── Cascade handlers (País → Provincia → Distrito) ───────────────────────
  onPaisChange(paisNombre: string): void {
    // Resetear dependientes
    this.provincias.set([]);
    this.distritos.set([]);
    this.form.patchValue({ provincia: '', distrito: '' });

    if (!paisNombre) return;

    const match = this.paises().find(p => p.paisNombre === paisNombre);
    if (!match) return;

    this.loadProvincias(match.paisId);
  }

  onProvinciaChange(provinciaNombre: string): void {
    // Resetear dependientes
    this.distritos.set([]);
    this.form.patchValue({ distrito: '' });

    if (!provinciaNombre) return;

    const match = this.provincias().find(d => d.departamentoNombre === provinciaNombre);
    if (!match) return;

    this.loadDistritos(match.departamentoId);
  }

  // ── Data loading ──────────────────────────────────────────────────────────
  loadClientes(): void {
    this.loading.set(true);
    this.svc.getAll()
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next:  data  => this.allClientes.set(data),
        error: ()    => this.toastSvc.error('No se pudieron cargar los clientes.', 'Error')
      });
  }

  // ── Filter actions ────────────────────────────────────────────────────────
  clearFilters(): void {
    this.filtro.set('');
    this.filterTipoCliente.set('');
    this.filterPais.set('');
  }

  // ── Modal: create / edit ──────────────────────────────────────────────────
  openModal(cliente?: ClienteDto): void {
    this.editing.set(cliente ?? null);
    this.form.reset({ status: 'A' });
    // Limpiar cascadas (se recargan abajo si aplica).
    this.provincias.set([]);
    this.distritos.set([]);

    if (cliente) {
      this.form.patchValue({
        tipoDocumento:        cliente.tipoDocumento,
        numeroIdentificacion: cliente.numeroIdentificacion,
        nombre:               cliente.nombre,
        correo:               cliente.correo    ?? '',
        telefono:             cliente.telefono  ?? '',
        tipoCliente:          cliente.tipoCliente ?? '',
        pais:                 cliente.pais      ?? '',
        provincia:            cliente.provincia ?? '',
        distrito:             cliente.distrito  ?? '',
        planta:               cliente.planta    ?? '',
        zona:                 cliente.zona      ?? '',
        status:               cliente.status
      });

      // Si el cliente tiene país, rehidratar las cascadas en orden.
      this.rehydrateCascade(cliente.pais, cliente.provincia);
    } else {
      // En modo "nuevo", preseleccionar país activo de la sesión si está disponible.
      this.preselectActivePais();
    }
    this.modalOpen.set(true);
  }

  /**
   * Al editar un cliente existente, recarga provincias y distritos para
   * que los selects muestren las opciones correctas (sin perder el valor seleccionado).
   */
  private rehydrateCascade(paisNombre: string | null, provinciaNombre: string | null): void {
    if (!paisNombre) return;
    const pais = this.paises().find(p => p.paisNombre === paisNombre);
    if (!pais) return;

    this.deptoSvc.getByPaisId(pais.paisId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (provs) => {
          this.provincias.set(provs ?? []);
          if (!provinciaNombre) return;
          const prov = (provs ?? []).find(d => d.departamentoNombre === provinciaNombre);
          if (!prov) return;
          this.ciudadSvc.getByDepartamentoId(prov.departamentoId)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: (dists) => this.distritos.set(dists ?? []),
              error: () => this.distritos.set([])
            });
        },
        error: () => this.provincias.set([])
      });
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.editing.set(null);
    this.form.reset({ status: 'A' });
    this.provincias.set([]);
    this.distritos.set([]);
  }

  saveCliente(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loadingModal.set(true);
    const val = this.form.getRawValue() as Record<string, string>;

    const payload: CreateClienteRequest = {
      tipoDocumento:        val['tipoDocumento'],
      numeroIdentificacion: val['numeroIdentificacion'],
      nombre:               val['nombre'],
      correo:               val['correo']      || null,
      telefono:             val['telefono']    || null,
      tipoCliente:          val['tipoCliente'] || null,
      pais:                 val['pais']        || null,
      provincia:            val['provincia']   || null,
      distrito:             val['distrito']    || null,
      planta:               val['planta']      || null,
      zona:                 val['zona']        || null
    };

    const current = this.editing();

    if (current) {
      const updatePayload: UpdateClienteRequest = { ...payload, status: val['status'] };
      this.svc.update(current.id, updatePayload)
        .pipe(
          finalize(() => this.loadingModal.set(false)),
          takeUntilDestroyed(this.destroyRef)
        )
        .subscribe({
          next: updated => {
            this.allClientes.update(list => list.map(c => c.id === updated.id ? updated : c));
            this.toastSvc.success('Cliente actualizado correctamente.', 'Listo');
            this.closeModal();
          },
          error: (err: { error?: { message?: string } }) => {
            this.toastSvc.error(err?.error?.message ?? 'Error al actualizar el cliente.', 'Error');
          }
        });
    } else {
      this.svc.create(payload)
        .pipe(
          finalize(() => this.loadingModal.set(false)),
          takeUntilDestroyed(this.destroyRef)
        )
        .subscribe({
          next: created => {
            this.allClientes.update(list => [created, ...list]);
            this.toastSvc.success('Cliente creado correctamente.', 'Listo');
            this.closeModal();
          },
          error: (err: { error?: { message?: string } }) => {
            this.toastSvc.error(err?.error?.message ?? 'Error al crear el cliente.', 'Error');
          }
        });
    }
  }

  // ── Modal: detail ─────────────────────────────────────────────────────────
  openDetail(cliente: ClienteDto): void {
    this.selectedDetail.set(cliente);
    this.detailOpen.set(true);
  }

  closeDetail(): void {
    this.detailOpen.set(false);
    this.selectedDetail.set(null);
  }

  // ── Modal: delete confirmation ────────────────────────────────────────────
  confirmDelete(id: number, nombre: string): void {
    this.pendingDeleteId.set(id);
    this.confirmData = {
      ...this.confirmData,
      message: `¿Eliminar el cliente "${nombre}"? Esta acción no se puede deshacer.`
    };
    this.confirmOpen.set(true);
  }

  onConfirmDelete(): void {
    const id = this.pendingDeleteId();
    if (id === null) return;
    this.confirmOpen.set(false);

    this.svc.delete(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.allClientes.update(list => list.filter(c => c.id !== id));
          this.toastSvc.success('Cliente eliminado correctamente.', 'Listo');
          this.pendingDeleteId.set(null);
        },
        error: (err: { error?: { message?: string } }) => {
          this.toastSvc.error(err?.error?.message ?? 'Error al eliminar el cliente.', 'Error');
          this.pendingDeleteId.set(null);
        }
      });
  }

  onCancelDelete(): void {
    this.confirmOpen.set(false);
    this.pendingDeleteId.set(null);
  }

  // ── Template helpers ──────────────────────────────────────────────────────
  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  getInitials(nombre: string): string {
    const parts = nombre.trim().split(/\s+/);
    if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
    return nombre.trim().slice(0, 2).toUpperCase();
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('es-CO', {
      year: 'numeric', month: 'short', day: 'numeric'
    });
  }

  trackById(_: number, c: ClienteDto): number { return c.id; }
}

import {
  Component, OnInit, signal, computed, inject, DestroyRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
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
  PAISES
} from '../../models/cliente.models';
import { ToastService } from '../../../../shared/services/toast.service';
import {
  ConfirmationModalComponent,
  ConfirmationModalData
} from '../../../../shared/components/confirmation-modal/confirmation-modal.component';

@Component({
  selector: 'app-cliente-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, ConfirmationModalComponent],
  templateUrl: './cliente-list.component.html',
  styleUrls: ['./cliente-list.component.scss']
})
export class ClienteListComponent implements OnInit {

  // ── Injected dependencies ────────────────────────────────────────────────
  private readonly svc        = inject(ClienteService);
  private readonly fb         = inject(FormBuilder);
  private readonly toastSvc   = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  // ── Catalogs ─────────────────────────────────────────────────────────────
  readonly tiposDocumento = TIPOS_DOCUMENTO;
  readonly tiposCliente   = TIPOS_CLIENTE;
  readonly paises         = PAISES;

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
    }
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.editing.set(null);
    this.form.reset({ status: 'A' });
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

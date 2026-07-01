// src/app/features/tickets/pages/ticket-create/ticket-create.component.ts
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TicketService } from '../../services/ticket.service';
import { TicketPerfilService, TipoPermitidoDto, AsignableDto } from '../../services/ticket-perfil.service';
import { CreateTicketRequest, TipoTicket, TicketImagenInput, UsuarioNotificableDto } from '../../models/ticket.models';
import { ImageDropzoneComponent } from '../../components/image-dropzone/image-dropzone.component';
import { TicketAdjuntosInputComponent, AdjuntosInputState } from '../../components/ticket-adjuntos-input/ticket-adjuntos-input.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { forkJoin, of } from 'rxjs';

/** Formulario de creación de ticket. Tipos y asignados filtrados por perfil del usuario y país. */
@Component({
  selector: 'app-ticket-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterLink, ImageDropzoneComponent, TicketAdjuntosInputComponent],
  templateUrl: './ticket-create.component.html',
})
export class TicketCreateComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly svc = inject(TicketService);
  private readonly perfilSvc = inject(TicketPerfilService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly saving = signal(false);
  readonly loadingTipos = signal(false);

  tiposPermitidos: TipoPermitidoDto[] = [];
  asignablesActuales: AsignableDto[] = [];
  private imagenes: TicketImagenInput[] = [];
  private adjuntosStaged: AdjuntosInputState = { archivos: [], links: [] };

  // ── Notificados (copiados) ───────────────────────────────────
  /** Catálogo completo de usuarios notificables (cargado una vez en ngOnInit). */
  private notificablesTodos: UsuarioNotificableDto[] = [];
  /** Texto del buscador de notificados. */
  notificadosBusqueda = '';
  /** Resultados filtrados a mostrar en el desplegable (excluye ya seleccionados). */
  readonly notificadosResultados = signal<UsuarioNotificableDto[]>([]);
  /** Seleccionados actuales, mostrados como chips. */
  readonly notificadosSeleccionados = signal<UsuarioNotificableDto[]>([]);

  readonly form = this.fb.nonNullable.group({
    titulo: ['', [Validators.required, Validators.maxLength(160)]],
    tipo: ['' as TipoTicket | '', Validators.required],
    asignadoGuid: ['', Validators.required],
    descripcion: ['', [Validators.required]],
  });

  get f() { return this.form.controls; }

  ngOnInit(): void {
    this.loadingTipos.set(true);
    this.perfilSvc.getTiposPermitidos()
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loadingTipos.set(false)))
      .subscribe({
        next: tipos => {
          this.tiposPermitidos = tipos;
          if (tipos.length === 1) {
            this.f.tipo.setValue(tipos[0].tipo as TipoTicket);
            this.onTipoChange(tipos[0].tipo);
          }
        },
        error: () => this.toast.error('No se pudieron cargar los tipos disponibles.'),
      });

    this.svc.getNotificables()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: usuarios => this.notificablesTodos = usuarios,
        // Error suave: la sección de notificados es opcional, no debe romper el form.
        error: () => this.toast.warning('No se pudo cargar la lista de usuarios a notificar.'),
      });
  }

  onTipoChange(tipo: string): void {
    const found = this.tiposPermitidos.find(t => t.tipo === tipo);
    this.asignablesActuales = found?.asignables ?? [];
    this.f.asignadoGuid.setValue('');
  }

  onImages(imgs: TicketImagenInput[]): void { this.imagenes = imgs; }

  onAdjuntosChange(state: AdjuntosInputState): void { this.adjuntosStaged = state; }

  // ── Notificados (copiados) ───────────────────────────────────

  /** Filtra el catálogo por nombre/email; excluye ya seleccionados. Se llama al escribir en el buscador. */
  onNotificadosBusquedaChange(texto: string): void {
    this.notificadosBusqueda = texto;
    const q = texto.trim().toLowerCase();
    if (!q) { this.notificadosResultados.set([]); return; }
    const seleccionadosGuids = new Set(this.notificadosSeleccionados().map(u => u.guid));
    const resultados = this.notificablesTodos.filter(u =>
      !seleccionadosGuids.has(u.guid) &&
      (u.nombre.toLowerCase().includes(q) || u.email.toLowerCase().includes(q)),
    );
    this.notificadosResultados.set(resultados.slice(0, 8));
  }

  /** Agrega un usuario a los seleccionados (sin duplicados) y limpia el buscador. */
  seleccionarNotificado(u: UsuarioNotificableDto): void {
    if (this.notificadosSeleccionados().some(s => s.guid === u.guid)) return;
    this.notificadosSeleccionados.update(actuales => [...actuales, u]);
    this.notificadosBusqueda = '';
    this.notificadosResultados.set([]);
  }

  /** Quita un usuario de los seleccionados (botón × del chip). */
  quitarNotificado(guid: string): void {
    this.notificadosSeleccionados.update(actuales => actuales.filter(u => u.guid !== guid));
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.warning('Completá todos los campos requeridos, incluyendo el resolutor.');
      return;
    }

    const v = this.form.getRawValue();
    const notificarUserGuids = this.notificadosSeleccionados().map(u => u.guid);
    const req: CreateTicketRequest = {
      titulo: v.titulo.trim(),
      tipo: v.tipo as TipoTicket,
      descripcion: v.descripcion.trim(),
      assignedToUserGuid: v.asignadoGuid as unknown as string,
      imagenes: this.imagenes.length ? this.imagenes : null,
      ...(notificarUserGuids.length ? { notificarUserGuids } : {}),
    } as any;

    this.saving.set(true);
    this.svc.crear(req)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: t => {
          const ticketId = t.id;
          const { archivos, links } = this.adjuntosStaged;
          const calls = [
            ...archivos.map(a => this.svc.addDocumento(ticketId, {
              base64: a.base64, fileName: a.fileName,
              contentType: a.contentType, sizeBytes: a.sizeBytes,
            })),
            ...links.map(l => this.svc.addLink(ticketId, { url: l.url, titulo: l.titulo || null })),
          ];
          const post$ = calls.length ? forkJoin(calls) : of([]);
          post$.subscribe({
            next: () => {
              this.toast.success(`Ticket ${t.codigo ?? ''} creado correctamente.`);
              this.router.navigate(['/tickets']);
            },
            error: () => {
              this.toast.warning(`Ticket ${t.codigo ?? ''} creado, pero algunos adjuntos no se pudieron subir.`);
              this.router.navigate(['/tickets', t.id]);
            },
          });
        },
        error: (err) => {
          const msg = err?.error || 'No se pudo crear el ticket.';
          this.toast.error(typeof msg === 'string' ? msg : 'Error al crear el ticket.');
        },
      });
  }
}

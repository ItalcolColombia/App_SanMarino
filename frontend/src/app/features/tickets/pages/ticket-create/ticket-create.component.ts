// src/app/features/tickets/pages/ticket-create/ticket-create.component.ts
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TicketService } from '../../services/ticket.service';
import { TicketPerfilService, TipoPermitidoDto, AsignableDto } from '../../services/ticket-perfil.service';
import { CreateTicketRequest, TipoTicket, TicketImagenInput } from '../../models/ticket.models';
import { ImageDropzoneComponent } from '../../components/image-dropzone/image-dropzone.component';
import { ToastService } from '../../../../shared/services/toast.service';

/** Formulario de creación de ticket. Tipos y asignados filtrados por perfil del usuario y país. */
@Component({
  selector: 'app-ticket-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, ImageDropzoneComponent],
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
  }

  onTipoChange(tipo: string): void {
    const found = this.tiposPermitidos.find(t => t.tipo === tipo);
    this.asignablesActuales = found?.asignables ?? [];
    this.f.asignadoGuid.setValue('');
  }

  onImages(imgs: TicketImagenInput[]): void { this.imagenes = imgs; }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.warning('Completá todos los campos requeridos, incluyendo el resolutor.');
      return;
    }

    const v = this.form.getRawValue();
    const req: CreateTicketRequest = {
      titulo: v.titulo.trim(),
      tipo: v.tipo as TipoTicket,
      descripcion: v.descripcion.trim(),
      assignedToUserGuid: v.asignadoGuid as unknown as string,
      imagenes: this.imagenes.length ? this.imagenes : null,
    } as any;

    this.saving.set(true);
    this.svc.crear(req)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: t => {
          this.toast.success(`Ticket ${t.codigo ?? ''} creado correctamente.`);
          this.router.navigate(['/tickets']);
        },
        error: (err) => {
          const msg = err?.error || 'No se pudo crear el ticket.';
          this.toast.error(typeof msg === 'string' ? msg : 'Error al crear el ticket.');
        },
      });
  }
}

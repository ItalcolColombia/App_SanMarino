// src/app/features/tickets/pages/ticket-create/ticket-create.component.ts
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { TicketService } from '../../services/ticket.service';
import { CreateTicketRequest, TipoTicket, TIPOS_TICKET, TicketImagenInput } from '../../models/ticket.models';
import { ImageDropzoneComponent } from '../../components/image-dropzone/image-dropzone.component';
import { ToastService } from '../../../../shared/services/toast.service';

/** Formulario de creación de ticket (Perfil A: Solicitante). */
@Component({
  selector: 'app-ticket-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, ImageDropzoneComponent],
  templateUrl: './ticket-create.component.html',
})
export class TicketCreateComponent {
  private readonly fb = inject(FormBuilder);
  private readonly svc = inject(TicketService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly tipos = TIPOS_TICKET;
  readonly saving = signal(false);

  private imagenes: TicketImagenInput[] = [];

  readonly form = this.fb.nonNullable.group({
    titulo: ['', [Validators.required, Validators.maxLength(160)]],
    tipo: ['SOPORTE' as TipoTicket, Validators.required],
    descripcion: ['', [Validators.required]],
  });

  get f() { return this.form.controls; }

  onImages(imgs: TicketImagenInput[]): void {
    this.imagenes = imgs;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.warning('Completá los campos requeridos.');
      return;
    }

    const v = this.form.getRawValue();
    const req: CreateTicketRequest = {
      titulo: v.titulo.trim(),
      tipo: v.tipo,
      descripcion: v.descripcion.trim(),
      imagenes: this.imagenes.length ? this.imagenes : null,
    };

    this.saving.set(true);
    this.svc.crear(req)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: t => {
          this.toast.success(`Ticket ${t.codigo ?? ''} creado correctamente.`);
          this.router.navigate(['/tickets']);
        },
        error: () => this.toast.error('No se pudo crear el ticket. Intentá de nuevo.'),
      });
  }
}

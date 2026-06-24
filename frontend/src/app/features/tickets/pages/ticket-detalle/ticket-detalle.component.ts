// src/app/features/tickets/pages/ticket-detalle/ticket-detalle.component.ts
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketService } from '../../services/ticket.service';
import { TicketPerfilService, AsignableDto } from '../../services/ticket-perfil.service';
import {
  TicketDetail, TicketAdjunto, EstadoTicket, ESTADO_LABEL, TIPO_LABEL, TRANSICIONES, TICKET_PERMS,
} from '../../models/ticket.models';
import { TicketStepperComponent } from '../../components/ticket-stepper/ticket-stepper.component';
import { TicketEstadoBadgeComponent } from '../../components/ticket-estado-badge/ticket-estado-badge.component';
import { ImageLightboxComponent } from '../../components/image-lightbox/image-lightbox.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';

/** Detalle del ticket: stepper + línea de tiempo + galería lazy + gestión (resolutor). */
@Component({
  selector: 'app-ticket-detalle',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    TicketStepperComponent, TicketEstadoBadgeComponent, ImageLightboxComponent,
    // Nota: TicketPerfilEditorComponent NO se importa aquí; el editor va en Usuarios/Roles
  ],
  templateUrl: './ticket-detalle.component.html',
})
export class TicketDetalleComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly svc = inject(TicketService);
  private readonly perfilSvc = inject(TicketPerfilService);
  private readonly toast = inject(ToastService);
  private readonly perm = inject(UserPermissionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly id = Number(this.route.snapshot.paramMap.get('id'));
  readonly ticket = signal<TicketDetail | null>(null);
  readonly loading = signal(false);
  readonly savingNota = signal(false);
  readonly savingEstado = signal(false);
  readonly lightboxIndex = signal<number | null>(null);

  notaTexto = '';
  // Transferir
  mostrarTransferir = false;
  transferirAsignado = '';
  transferirNota = '';
  readonly savingTransferir = signal(false);
  asignablesDesarrollo: AsignableDto[] = [];

  // Solución (modal al marcar SOLUCIONADO)
  mostrarSolucion = false;
  solucionTexto = '';
  readonly savingSolucion = signal(false);

  // Cierre / reapertura por el solicitante
  readonly savingCierre = signal(false);

  // Adjuntos (documentos + links)
  nuevoLinkUrl = '';
  nuevoLinkTitulo = '';
  readonly subiendoAdjunto = signal(false);

  readonly estadoLabel = ESTADO_LABEL;
  readonly tipoLabel = TIPO_LABEL;

  readonly esAdmin       = this.perm.has(TICKET_PERMS.admin);
  readonly esResolutor   = this.perm.has(TICKET_PERMS.gestionar);

  /** Ruta de vuelta según el rol del usuario (para el botón "Volver"). */
  readonly volverRuta: string = (() => {
    if (this.esAdmin)     return '/tickets/admin';
    if (this.esResolutor) return '/tickets/gestion';
    return '/tickets';
  })();

  /** Tiene permiso de gestión (resolutor/admin). */
  readonly tienePermisoGestion = this.perm.hasAny([TICKET_PERMS.gestionar, TICKET_PERMS.admin]);

  /**
   * El panel de gestión se muestra a quien ATIENDE el ticket.
   * El admin puede gestionar cualquier ticket (incluido los que creó).
   * El resolutor solo los que no creó él mismo.
   */
  puedeGestionarTicket(t: TicketDetail): boolean {
    if (this.esAdmin) return true;
    return this.esResolutor && !t.soyCreador;
  }

  /** Transiciones de estado válidas desde el estado actual. */
  readonly transiciones = computed<EstadoTicket[]>(() => {
    const t = this.ticket();
    return t ? (TRANSICIONES[t.estado] ?? []) : [];
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.svc.getById(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: t => this.ticket.set(t),
        error: () => this.toast.error('No se pudo cargar el ticket.'),
      });
  }

  tomar(): void {
    this.savingEstado.set(true);
    this.svc.tomar(this.id)
      .pipe(finalize(() => this.savingEstado.set(false)))
      .subscribe({
        next: t => { this.ticket.set(t); this.toast.success('Tomaste el ticket.'); },
        error: () => this.toast.error('No se pudo tomar el ticket.'),
      });
  }

  /** Intercepta SOLUCIONADO para pedir la descripción de la solución. */
  cambiarEstado(estado: EstadoTicket): void {
    if (estado === 'SOLUCIONADO') { this.mostrarSolucion = true; this.solucionTexto = ''; return; }
    this.aplicarEstado(estado);
  }

  private aplicarEstado(estado: EstadoTicket, solucionDescripcion?: string): void {
    this.savingEstado.set(true);
    this.svc.cambiarEstado(this.id, { estado, solucionDescripcion: solucionDescripcion ?? null })
      .pipe(finalize(() => this.savingEstado.set(false)))
      .subscribe({
        next: t => { this.ticket.set(t); this.toast.success(`Estado: ${ESTADO_LABEL[estado]}.`); },
        error: (e) => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo cambiar el estado.'),
      });
  }

  /** Confirma SOLUCIONADO con la descripción de la solución (obligatoria). */
  confirmarSolucion(): void {
    const desc = this.solucionTexto.trim();
    if (!desc) { this.toast.warning('Escribí la descripción de la solución.'); return; }
    this.savingSolucion.set(true);
    this.svc.cambiarEstado(this.id, { estado: 'SOLUCIONADO', solucionDescripcion: desc })
      .pipe(finalize(() => this.savingSolucion.set(false)))
      .subscribe({
        next: t => {
          this.ticket.set(t);
          this.mostrarSolucion = false;
          this.toast.success(t.notificadoCorreo ? 'Solucionado y notificado por correo.' : 'Ticket solucionado.');
        },
        error: (e) => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo solucionar.'),
      });
  }

  /** El solicitante confirma el cierre (SOLUCIONADO → CERRADO). */
  confirmarCierre(): void {
    this.savingCierre.set(true);
    this.svc.confirmarCierre(this.id)
      .pipe(finalize(() => this.savingCierre.set(false)))
      .subscribe({
        next: t => { this.ticket.set(t); this.toast.success('Cierre confirmado. El caso quedó cerrado por ambas partes.'); },
        error: (e) => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo confirmar el cierre.'),
      });
  }

  /** El solicitante reabre si no está conforme (SOLUCIONADO → EN_ANALISIS). */
  reabrir(): void {
    this.aplicarEstado('EN_ANALISIS');
  }

  // ── Adjuntos ──────────────────────────────────────────────
  onDocumentoSeleccionado(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (file.size > 8 * 1024 * 1024) { this.toast.warning('El archivo supera los 8 MB.'); input.value = ''; return; }

    const reader = new FileReader();
    reader.onload = () => {
      const base64 = (reader.result as string).split(',')[1] ?? '';
      this.subiendoAdjunto.set(true);
      this.svc.addDocumento(this.id, { base64, fileName: file.name, contentType: file.type, sizeBytes: file.size })
        .pipe(finalize(() => this.subiendoAdjunto.set(false)))
        .subscribe({
          next: () => { this.toast.success('Documento adjuntado.'); this.load(); },
          error: (e) => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo adjuntar el documento.'),
        });
    };
    reader.readAsDataURL(file);
    input.value = '';
  }

  agregarLink(): void {
    const url = this.nuevoLinkUrl.trim();
    if (!url) { this.toast.warning('Ingresá la URL del documento.'); return; }
    this.subiendoAdjunto.set(true);
    this.svc.addLink(this.id, { url, titulo: this.nuevoLinkTitulo.trim() || null })
      .pipe(finalize(() => this.subiendoAdjunto.set(false)))
      .subscribe({
        next: () => { this.nuevoLinkUrl = ''; this.nuevoLinkTitulo = ''; this.toast.success('Link agregado.'); this.load(); },
        error: (e) => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo agregar el link.'),
      });
  }

  descargarAdjunto(adj: TicketAdjunto): void {
    if (adj.tipo === 'LINK') { if (adj.url) window.open(adj.url, '_blank', 'noopener'); return; }
    this.svc.descargarDocumento(this.id, adj.id).subscribe({
      next: doc => {
        const a = document.createElement('a');
        a.href = `data:${doc.contentType || 'application/octet-stream'};base64,${doc.contenidoBase64}`;
        a.download = doc.fileName || adj.fileName || 'documento';
        document.body.appendChild(a);
        a.click();
        a.remove();
      },
      error: () => this.toast.error('No se pudo descargar el documento.'),
    });
  }

  eliminarAdjunto(adj: TicketAdjunto): void {
    this.svc.deleteAdjunto(this.id, adj.id).subscribe({
      next: () => { this.toast.success('Adjunto eliminado.'); this.load(); },
      error: () => this.toast.error('No se pudo eliminar el adjunto.'),
    });
  }

  addNota(): void {
    const nota = this.notaTexto.trim();
    if (!nota) return;
    this.savingNota.set(true);
    this.svc.addNota(this.id, { nota })
      .pipe(finalize(() => this.savingNota.set(false)))
      .subscribe({
        next: () => { this.notaTexto = ''; this.toast.success('Comentario agregado.'); this.load(); },
        error: () => this.toast.error('No se pudo agregar el comentario.'),
      });
  }

  openLightbox(i: number): void { this.lightboxIndex.set(i); }
  closeLightbox(): void { this.lightboxIndex.set(null); }

  abrirTransferir(t: TicketDetail): void {
    this.mostrarTransferir = true;
    this.transferirAsignado = '';
    this.transferirNota = '';
    this.perfilSvc.getAsignables('DESARROLLO', t.paisId)
      .subscribe({ next: a => this.asignablesDesarrollo = a, error: () => {} });
  }

  transferir(): void {
    if (!this.transferirAsignado) { this.toast.warning('Seleccioná el nuevo resolutor de Desarrollo.'); return; }
    this.savingTransferir.set(true);
    this.svc.transferir(this.id, { nuevoAsignadoGuid: this.transferirAsignado, nota: this.transferirNota || null })
      .pipe(finalize(() => this.savingTransferir.set(false)))
      .subscribe({
        next: t => {
          this.ticket.set(t);
          this.mostrarTransferir = false;
          this.toast.success('Ticket transferido a Desarrollo.');
        },
        error: (e) => this.toast.error(e?.error ?? 'No se pudo transferir el ticket.'),
      });
  }

  formatKb(bytes: number | null): string {
    if (!bytes) return '';
    return bytes >= 1024 * 1024
      ? `${(bytes / (1024 * 1024)).toFixed(1)} MB`
      : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  /** Iniciales (máx 2) a partir del nombre completo, para el avatar de la nota. */
  iniciales(nombre: string | null): string {
    if (!nombre) return '';
    return nombre.trim().split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }
}

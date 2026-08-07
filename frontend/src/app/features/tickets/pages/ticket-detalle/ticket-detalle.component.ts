// src/app/features/tickets/pages/ticket-detalle/ticket-detalle.component.ts
import { Component, DestroyRef, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
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
import {
  PRIORIDADES, PRIORIDAD_LABEL, PrioridadTicket, TicketTimelineEvento,
} from '../../models/ticket-tarea.models';
import { TicketStepperComponent } from '../../components/ticket-stepper/ticket-stepper.component';
import { TicketEstadoBadgeComponent } from '../../components/ticket-estado-badge/ticket-estado-badge.component';
import { ImageLightboxComponent } from '../../components/image-lightbox/image-lightbox.component';
import { TicketTimelineComponent } from '../../components/ticket-timeline/ticket-timeline.component';
import { TicketPrioridadBadgeComponent } from '../../components/ticket-prioridad-badge/ticket-prioridad-badge.component';
import { TicketSlaChipComponent } from '../../components/ticket-sla-chip/ticket-sla-chip.component';
import { TareasPanelComponent } from '../../components/tareas-panel/tareas-panel.component';
import { WorklogPanelComponent } from '../../components/worklog-panel/worklog-panel.component';
import { OpcionAsignable } from '../../components/tarea-modal/tarea-modal.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';

/**
 * Pestañas de la columna del caso. La conversación NO es una pestaña: vive en su propia
 * columna para poder leerla en paralelo con el caso en pantalla ancha.
 */
type Pestana = 'actividad' | 'tareas' | 'tiempos';

/** Detalle del caso con layout tipo Jira: contenido + pestañas y sidebar de gestión. */
@Component({
  selector: 'app-ticket-detalle',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    TicketStepperComponent, TicketEstadoBadgeComponent, ImageLightboxComponent,
    TicketTimelineComponent, TicketPrioridadBadgeComponent, TicketSlaChipComponent,
    TareasPanelComponent, WorklogPanelComponent,
    // Nota: TicketPerfilEditorComponent NO se importa aquí; el editor va en Usuarios/Roles
  ],
  changeDetection: ChangeDetectionStrategy.Eager,
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

  // Línea de tiempo
  readonly timeline = signal<TicketTimelineEvento[]>([]);
  readonly loadingTimeline = signal(false);

  /** Pestaña activa de la columna principal. */
  pestana: Pestana = 'actividad';

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

  // ── Gestión tipo tablero ──────────────────────────────────────
  readonly prioridades = PRIORIDADES;
  readonly prioridadLabel = PRIORIDAD_LABEL;
  readonly savingGestion = signal(false);

  /** Responsables candidatos del tipo/país del caso (para reasignar y para las tareas). */
  asignablesDelCaso: AsignableDto[] = [];
  /** Adaptación al contrato del modal de tareas. */
  readonly opcionesAsignable = signal<OpcionAsignable[]>([]);

  mostrarReasignar = false;
  reasignarGuid = '';

  mostrarPlanificacion = false;
  planFechaInicio = '';
  planFechaFin = '';
  planFechaLimite = '';
  planHorasEstimadas: number | null = null;

  readonly estadoLabel = ESTADO_LABEL;
  readonly tipoLabel = TIPO_LABEL;

  readonly esAdmin       = this.perm.has(TICKET_PERMS.admin);
  readonly esResolutor   = this.perm.has(TICKET_PERMS.gestionar);

  /** Ruta de vuelta según el rol del usuario (para el botón "Volver"). */
  readonly volverRuta: string = (() => {
    if (this.esAdmin)     return '/italjira/tablero';
    if (this.esResolutor) return '/tickets/gestion';
    return '/tickets';
  })();

  /** Tiene permiso de gestión (resolutor/admin). */
  readonly tienePermisoGestion = this.perm.hasAny([TICKET_PERMS.gestionar, TICKET_PERMS.admin]);

  /**
   * El panel de gestión se muestra SOLO a quien ATIENDE el caso (resolutor o admin) y NUNCA al
   * solicitante sobre su propio caso — ni siquiera si es admin. Con solicitante delegado, el
   * admin que lo registró NO es el solicitante, así que sí puede gestionarlo: es justamente
   * para lo que registró el caso a nombre de otro.
   */
  puedeGestionarTicket(t: TicketDetail): boolean {
    if (t.soySolicitante) return false;
    return this.esResolutor || this.esAdmin;
  }

  /** Transiciones de estado válidas desde el estado actual. */
  readonly transiciones = computed<EstadoTicket[]>(() => {
    const t = this.ticket();
    return t ? (TRANSICIONES[t.estado] ?? []) : [];
  });

  ngOnInit(): void {
    this.load();
    this.cargarTimeline();
  }

  load(): void {
    this.loading.set(true);
    this.svc.getById(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: t => {
          this.ticket.set(t);
          this.sincronizarPlanificacion(t);
          if (this.puedeGestionarTicket(t)) this.cargarAsignables(t);
        },
        error: () => this.toast.error('No se pudo cargar el ticket.'),
      });
  }

  /** Recarga detalle + línea de tiempo tras una acción que cambia el estado del caso. */
  private recargarTodo(t: TicketDetail): void {
    this.ticket.set(t);
    this.sincronizarPlanificacion(t);
    this.cargarTimeline();
  }

  cargarTimeline(): void {
    this.loadingTimeline.set(true);
    this.svc.timeline(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loadingTimeline.set(false)))
      .subscribe({
        next: e => this.timeline.set(e),
        // La línea de tiempo es complementaria: si falla, el detalle sigue usable.
        error: () => this.timeline.set([]),
      });
  }

  private cargarAsignables(t: TicketDetail): void {
    this.perfilSvc.getAsignables(t.tipo, t.paisId).subscribe({
      next: a => {
        this.asignablesDelCaso = a;
        this.opcionesAsignable.set(a.map(x => ({ guid: x.userId, nombre: x.nombreCompleto })));
      },
      error: () => {},
    });
  }

  private sincronizarPlanificacion(t: TicketDetail): void {
    this.planFechaInicio = t.fechaInicioPlan ? t.fechaInicioPlan.slice(0, 10) : '';
    this.planFechaFin = t.fechaFinPlan ? t.fechaFinPlan.slice(0, 10) : '';
    this.planFechaLimite = t.fechaLimite ? t.fechaLimite.slice(0, 10) : '';
    this.planHorasEstimadas = t.horasEstimadas;
  }

  tomar(): void {
    this.savingEstado.set(true);
    this.svc.tomar(this.id)
      .pipe(finalize(() => this.savingEstado.set(false)))
      .subscribe({
        next: t => { this.recargarTodo(t); this.toast.success('Tomaste el ticket.'); },
        error: e => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo tomar el ticket.'),
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
        next: t => { this.recargarTodo(t); this.toast.success(`Estado: ${ESTADO_LABEL[estado]}.`); },
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
          this.recargarTodo(t);
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
        next: t => { this.recargarTodo(t); this.toast.success('Cierre confirmado. El caso quedó cerrado por ambas partes.'); },
        error: (e) => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo confirmar el cierre.'),
      });
  }

  /** El solicitante reabre si no está conforme (SOLUCIONADO → EN_ANALISIS). */
  reabrir(): void {
    this.aplicarEstado('EN_ANALISIS');
  }

  // ── Gestión tipo tablero ──────────────────────────────────────

  cambiarPrioridad(prioridad: PrioridadTicket): void {
    const t = this.ticket();
    if (!t || t.prioridad === prioridad) return;

    this.savingGestion.set(true);
    this.svc.cambiarPrioridad(this.id, { prioridad })
      .pipe(finalize(() => this.savingGestion.set(false)))
      .subscribe({
        next: d => { this.recargarTodo(d); this.toast.success(`Prioridad: ${PRIORIDAD_LABEL[prioridad]}.`); },
        error: e => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo cambiar la prioridad.'),
      });
  }

  abrirReasignar(): void {
    this.mostrarReasignar = true;
    this.reasignarGuid = this.ticket()?.assignedToUserGuid ?? '';
  }

  reasignar(): void {
    if (!this.reasignarGuid) { this.toast.warning('Elegí el nuevo responsable.'); return; }
    this.savingGestion.set(true);
    this.svc.cambiarAsignado(this.id, { asignadoUserGuid: this.reasignarGuid })
      .pipe(finalize(() => this.savingGestion.set(false)))
      .subscribe({
        next: d => {
          this.recargarTodo(d);
          this.mostrarReasignar = false;
          this.toast.success('Responsable actualizado.');
        },
        error: e => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo reasignar el caso.'),
      });
  }

  guardarPlanificacion(): void {
    if (this.planFechaInicio && this.planFechaFin && this.planFechaFin < this.planFechaInicio) {
      this.toast.warning('La fecha de fin no puede ser anterior a la de inicio.');
      return;
    }

    this.savingGestion.set(true);
    this.svc.actualizarPlanificacion(this.id, {
      fechaInicioPlan: this.planFechaInicio || null,
      fechaFinPlan: this.planFechaFin || null,
      // El compromiso se guarda al final del día elegido: vencer a medianoche sería contraintuitivo.
      fechaLimite: this.planFechaLimite ? `${this.planFechaLimite}T23:59:00Z` : null,
      horasEstimadas: this.planHorasEstimadas ?? null,
      limpiarFechaInicioPlan: !this.planFechaInicio,
      limpiarFechaFinPlan: !this.planFechaFin,
      limpiarFechaLimite: !this.planFechaLimite,
      limpiarHorasEstimadas: this.planHorasEstimadas === null,
    })
      .pipe(finalize(() => this.savingGestion.set(false)))
      .subscribe({
        next: d => {
          this.recargarTodo(d);
          this.mostrarPlanificacion = false;
          this.toast.success('Planificación actualizada.');
        },
        error: e => this.toast.error(typeof e?.error === 'string' ? e.error : 'No se pudo guardar la planificación.'),
      });
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
          next: () => { this.toast.success('Documento adjuntado.'); this.load(); this.cargarTimeline(); },
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
        next: () => {
          this.nuevoLinkUrl = ''; this.nuevoLinkTitulo = '';
          this.toast.success('Link agregado.');
          this.load(); this.cargarTimeline();
        },
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
        next: () => {
          this.notaTexto = '';
          this.toast.success('Comentario agregado.');
          this.load(); this.cargarTimeline();
        },
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
          this.recargarTodo(t);
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

  /** «2 h», «1,5 d» — para los tiempos del panel de métricas. */
  humanizarHoras(horas: number | null | undefined): string {
    if (horas === null || horas === undefined) return '—';
    if (horas >= 24) return `${(horas / 24).toFixed(1).replace('.0', '')} d`;
    if (horas >= 1) return `${Math.round(horas)} h`;
    return `${Math.max(1, Math.round(horas * 60))} min`;
  }
}

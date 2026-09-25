import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { TareaFormComponent } from '../../components/tarea-form/tarea-form.component';
import { VisitaFormComponent } from '../../components/visita-form/visita-form.component';
import { etiquetaUbicacion, mensajeErrorHttp } from '../../funciones/ubicacion.funcion';
import {
  TareaCampoDto,
  VeterinariaGranjaDto,
  VeterinariaResumenDto,
  VisitaTecnicaDto,
} from '../../models/gestion-veterinaria.models';
import { GestionVeterinariaService } from '../../services/gestion-veterinaria.service';

type TabVeterinaria = 'granjas' | 'agenda' | 'planes' | 'mis-tareas';

@Component({
  selector: 'app-gestion-veterinaria-page', standalone: true,
  imports: [CommonModule, RouterLink, VisitaFormComponent, TareaFormComponent],
  templateUrl: './gestion-veterinaria.page.html',
  styleUrls: ['../../styles/gestion-veterinaria.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GestionVeterinariaPage implements OnInit {
  loading = true;
  error: string | null = null;
  tab: TabVeterinaria = 'granjas';
  granjas: VeterinariaGranjaDto[] = [];
  visitas: VisitaTecnicaDto[] = [];
  tareasCreadas: TareaCampoDto[] = [];
  misTareas: TareaCampoDto[] = [];
  resumen: VeterinariaResumenDto = { granjasAsignadas: 0, visitasProximas: 0, tareasPendientes: 0, tareasVencidas: 0 };
  granjaAbierta: number | null = null;
  visitaFormOpen = false;
  tareaFormOpen = false;
  visitaEditando: VisitaTecnicaDto | null = null;
  visitaParaTarea: VisitaTecnicaDto | null = null;
  tareaEditando: TareaCampoDto | null = null;

  readonly etiquetaUbicacion = etiquetaUbicacion;

  constructor(
    private service: GestionVeterinariaService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService,
  ) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true; this.error = null;
    forkJoin({
      granjas: this.service.getMiMapa(), resumen: this.service.getResumen(), visitas: this.service.getVisitas(),
      tareasCreadas: this.service.getTareasCreadas(), misTareas: this.service.getMisTareas(),
    }).pipe(finalize(() => this.loading = false)).subscribe({
      next: (data) => Object.assign(this, data),
      error: (error) => this.error = mensajeErrorHttp(error, 'No fue posible cargar la gestión veterinaria.'),
    });
  }

  abrirVisita(visita: VisitaTecnicaDto | null = null): void {
    this.visitaEditando = visita; this.visitaFormOpen = true;
  }
  abrirTarea(visita: VisitaTecnicaDto | null = null, tarea: TareaCampoDto | null = null): void {
    this.visitaParaTarea = visita; this.tareaEditando = tarea; this.tareaFormOpen = true;
  }
  savedVisita(): void { this.visitaFormOpen = false; this.load(); }
  savedTarea(): void { this.load(); }

  async realizarVisita(visita: VisitaTecnicaDto): Promise<void> {
    const observaciones = await this.confirmDialog.askText({
      title: 'Cerrar visita de campo', message: 'Registrá los hallazgos principales. Podés dejarlos vacíos si no hubo novedades.',
      type: 'info', confirmText: 'Marcar realizada',
      input: { label: 'Observaciones de la visita', value: visita.observaciones ?? '', placeholder: 'Hallazgos y recomendaciones' },
    });
    if (observaciones === null) return;
    this.service.realizarVisita(visita.id, observaciones || null).subscribe({
      next: () => { this.toast.success('Visita marcada como realizada.'); this.load(); },
      error: (error) => this.toast.error(mensajeErrorHttp(error, 'No fue posible cerrar la visita.')),
    });
  }

  async cancelarVisita(visita: VisitaTecnicaDto): Promise<void> {
    if (!(await this.confirmDialog.ask({ title: 'Cancelar visita', message: `Se cancelará “${visita.titulo}”.`, type: 'error', confirmText: 'Cancelar visita' }))) return;
    this.service.cancelarVisita(visita.id).subscribe({ next: () => { this.toast.success('Visita cancelada.'); this.load(); },
      error: (error) => this.toast.error(mensajeErrorHttp(error, 'No fue posible cancelar la visita.')) });
  }

  async cancelarTarea(tarea: TareaCampoDto): Promise<void> {
    if (!(await this.confirmDialog.ask({ title: 'Cancelar tarea', message: `La tarea “${tarea.titulo}” dejará de aparecer a los responsables.`, type: 'error', confirmText: 'Cancelar tarea' }))) return;
    this.service.cancelarTarea(tarea.id).subscribe({ next: () => { this.toast.success('Tarea cancelada.'); this.load(); },
      error: (error) => this.toast.error(mensajeErrorHttp(error, 'No fue posible cancelar la tarea.')) });
  }

  reabrirTarea(tarea: TareaCampoDto): void {
    this.service.reabrirTarea(tarea.id).subscribe({ next: () => { this.toast.success('Tarea reabierta.'); this.load(); },
      error: (error) => this.toast.error(mensajeErrorHttp(error, 'No fue posible reabrir la tarea.')) });
  }

  visitasProgramadas(): VisitaTecnicaDto[] { return this.visitas.filter((item) => item.estado === 'PROGRAMADA'); }
  setTab(tab: TabVeterinaria): void { this.tab = tab; }
  toggleGranja(id: number): void { this.granjaAbierta = this.granjaAbierta === id ? null : id; }
}

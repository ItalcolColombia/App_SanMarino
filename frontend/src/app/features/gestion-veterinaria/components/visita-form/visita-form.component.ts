import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ToastService } from '../../../../shared/services/toast.service';
import {
  galponesDeNucleo,
  lotesDeUbicacion,
  mensajeErrorHttp,
  nucleosDeGranja,
} from '../../funciones/ubicacion.funcion';
import { VeterinariaGranjaDto, VisitaTecnicaDto, VisitaTecnicaRequest } from '../../models/gestion-veterinaria.models';
import { GestionVeterinariaService } from '../../services/gestion-veterinaria.service';

@Component({
  selector: 'app-visita-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './visita-form.component.html',
  styleUrls: ['../../styles/gestion-veterinaria.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class VisitaFormComponent implements OnChanges {
  @Input() open = false;
  @Input() granjas: VeterinariaGranjaDto[] = [];
  @Input() visita: VisitaTecnicaDto | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<VisitaTecnicaDto>();

  saving = false;
  model: VisitaTecnicaRequest = this.emptyModel();

  constructor(private service: GestionVeterinariaService, private toast: ToastService) {}

  ngOnChanges(): void {
    if (!this.open) return;
    this.model = this.visita ? {
      farmId: this.visita.farmId,
      nucleoId: this.visita.nucleoId ?? null,
      galponId: this.visita.galponId ?? null,
      loteId: this.visita.loteId ?? null,
      titulo: this.visita.titulo,
      objetivo: this.visita.objetivo ?? null,
      fechaProgramada: this.toLocalInput(this.visita.fechaProgramada),
    } : this.emptyModel();
  }

  get nucleos() { return nucleosDeGranja(this.granjas, this.model.farmId || null); }
  get galpones() { return galponesDeNucleo(this.granjas, this.model.farmId || null, this.model.nucleoId); }
  get lotes() { return lotesDeUbicacion(this.granjas, this.model); }

  changeFarm(): void { this.model.nucleoId = null; this.model.galponId = null; this.model.loteId = null; }
  changeNucleo(): void { this.model.galponId = null; this.model.loteId = null; }
  changeGalpon(): void { this.model.loteId = null; }

  save(): void {
    if (!this.model.farmId || !this.model.titulo.trim() || !this.model.fechaProgramada) {
      this.toast.warning('Seleccioná una granja y completá el título y la fecha de la visita.');
      return;
    }
    this.saving = true;
    const req = {
      ...this.model,
      titulo: this.model.titulo.trim(),
      objetivo: this.model.objetivo?.trim() || null,
      // datetime-local no incluye zona; el contrato timestamptz del backend sí requiere un instante.
      fechaProgramada: new Date(this.model.fechaProgramada).toISOString(),
    };
    const action = this.visita
      ? this.service.updateVisita(this.visita.id, req)
      : this.service.createVisita(req);
    action.pipe(finalize(() => this.saving = false)).subscribe({
      next: (visita) => {
        this.toast.success(this.visita ? 'Visita actualizada.' : 'Visita programada.');
        this.saved.emit(visita);
      },
      error: (error) => this.toast.error(mensajeErrorHttp(error, 'No fue posible guardar la visita.')),
    });
  }

  private emptyModel(): VisitaTecnicaRequest {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    tomorrow.setHours(8, 0, 0, 0);
    return { farmId: 0, nucleoId: null, galponId: null, loteId: null, titulo: '', objetivo: null,
      fechaProgramada: this.toLocalInput(tomorrow.toISOString()) };
  }

  private toLocalInput(value: string): string {
    const date = new Date(value);
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 16);
  }
}

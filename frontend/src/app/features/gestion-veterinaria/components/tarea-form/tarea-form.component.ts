import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ToastService } from '../../../../shared/services/toast.service';
import { galponesDeNucleo, lotesDeUbicacion, mensajeErrorHttp, nucleosDeGranja } from '../../funciones/ubicacion.funcion';
import { TareaCampoDto, TareaCampoRequest, VeterinariaGranjaDto, VisitaTecnicaDto } from '../../models/gestion-veterinaria.models';
import { GestionVeterinariaService } from '../../services/gestion-veterinaria.service';

@Component({
  selector: 'app-tarea-form', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './tarea-form.component.html', styleUrls: ['../../styles/gestion-veterinaria.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class TareaFormComponent implements OnChanges {
  @Input() open = false;
  @Input() granjas: VeterinariaGranjaDto[] = [];
  @Input() visitas: VisitaTecnicaDto[] = [];
  @Input() visitaInicial: VisitaTecnicaDto | null = null;
  @Input() tarea: TareaCampoDto | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<TareaCampoDto>();

  saving = false;
  model: TareaCampoRequest = this.emptyModel();

  constructor(private service: GestionVeterinariaService, private toast: ToastService) {}

  ngOnChanges(): void {
    if (!this.open) return;
    this.model = this.tarea ? {
      visitaId: this.tarea.visitaId ?? null, farmId: this.tarea.farmId,
      nucleoId: this.tarea.nucleoId ?? null, galponId: this.tarea.galponId ?? null,
      loteId: this.tarea.loteId ?? null, titulo: this.tarea.titulo,
      instrucciones: this.tarea.instrucciones ?? null,
      fechaInicio: this.tarea.fechaInicio.slice(0, 10), fechaFin: this.tarea.fechaFin.slice(0, 10),
      requiereObservacion: this.tarea.requiereObservacion, requiereFoto: this.tarea.requiereFoto,
    } : this.fromVisit(this.visitaInicial);
  }

  get nucleos() { return nucleosDeGranja(this.granjas, this.model.farmId || null); }
  get galpones() { return galponesDeNucleo(this.granjas, this.model.farmId || null, this.model.nucleoId); }
  get lotes() { return lotesDeUbicacion(this.granjas, this.model); }

  changeVisita(): void {
    const visita = this.visitas.find((item) => item.id === this.model.visitaId);
    if (visita) Object.assign(this.model, { farmId: visita.farmId, nucleoId: visita.nucleoId ?? null,
      galponId: visita.galponId ?? null, loteId: visita.loteId ?? null });
  }
  changeFarm(): void { this.model.visitaId = null; this.model.nucleoId = null; this.model.galponId = null; this.model.loteId = null; }
  changeNucleo(): void { this.model.galponId = null; this.model.loteId = null; }
  changeGalpon(): void { this.model.loteId = null; }

  save(createAnother = false): void {
    if (!this.model.farmId || !this.model.titulo.trim() || !this.model.fechaInicio || !this.model.fechaFin) {
      this.toast.warning('Completá la ubicación, el título y el periodo de la tarea.'); return;
    }
    if (this.model.fechaFin < this.model.fechaInicio) { this.toast.warning('La fecha final no puede ser anterior a la inicial.'); return; }
    this.saving = true;
    const req = { ...this.model, titulo: this.model.titulo.trim(), instrucciones: this.model.instrucciones?.trim() || null };
    const action = this.tarea ? this.service.updateTarea(this.tarea.id, req) : this.service.createTarea(req);
    action.pipe(finalize(() => this.saving = false)).subscribe({
      next: (tarea) => {
        this.toast.success(this.tarea ? 'Tarea actualizada.' : 'Tarea agregada al cronograma.');
        this.saved.emit(tarea);
        if (createAnother && !this.tarea) {
          this.model = { ...this.model, titulo: '', instrucciones: null, requiereObservacion: false, requiereFoto: false };
        } else {
          this.closed.emit();
        }
      }, error: (error) => this.toast.error(mensajeErrorHttp(error, 'No fue posible guardar la tarea.')),
    });
  }

  private fromVisit(visita: VisitaTecnicaDto | null): TareaCampoRequest {
    const model = this.emptyModel();
    return visita ? { ...model, visitaId: visita.id, farmId: visita.farmId, nucleoId: visita.nucleoId ?? null,
      galponId: visita.galponId ?? null, loteId: visita.loteId ?? null } : model;
  }
  private emptyModel(): TareaCampoRequest {
    const today = new Date().toISOString().slice(0, 10);
    return { visitaId: null, farmId: 0, nucleoId: null, galponId: null, loteId: null, titulo: '',
      instrucciones: null, fechaInicio: today, fechaFin: today, requiereObservacion: false, requiereFoto: false };
  }
}

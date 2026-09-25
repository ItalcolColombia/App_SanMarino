import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ToastService } from '../../../../shared/services/toast.service';
import { comprimirFoto } from '../../funciones/comprimir-foto.funcion';
import { etiquetaUbicacion, mensajeErrorHttp } from '../../funciones/ubicacion.funcion';
import { TareaCampoDto, TareaCampoEvidenciaInput } from '../../models/gestion-veterinaria.models';
import { GestionVeterinariaService } from '../../services/gestion-veterinaria.service';

@Component({
  selector: 'app-cumplir-tarea-page', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './cumplir-tarea.page.html', styleUrls: ['../../styles/gestion-veterinaria.scss', './cumplir-tarea.page.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class CumplirTareaPage implements OnInit {
  loading = true;
  saving = false;
  processingPhoto = false;
  error: string | null = null;
  tarea: TareaCampoDto | null = null;
  observacion = '';
  evidencias: TareaCampoEvidenciaInput[] = [];
  readonly etiquetaUbicacion = etiquetaUbicacion;

  constructor(private route: ActivatedRoute, private router: Router,
    private service: GestionVeterinariaService, private toast: ToastService) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(id) || id <= 0) { this.loading = false; this.error = 'La tarea solicitada no es válida.'; return; }
    this.service.getTarea(id).pipe(finalize(() => this.loading = false)).subscribe({
      next: (tarea) => this.tarea = tarea,
      error: (error) => this.error = mensajeErrorHttp(error, 'No fue posible abrir la tarea.'),
    });
  }

  async addPhotos(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (!files.length) return;
    if (this.evidencias.length + files.length > 3) { this.toast.warning('Podés adjuntar hasta 3 fotografías.'); input.value = ''; return; }
    this.processingPhoto = true;
    try {
      for (const file of files) this.evidencias.push(await comprimirFoto(file));
    } catch (error) {
      this.toast.error(error instanceof Error ? error.message : 'No fue posible preparar la fotografía.');
    } finally { this.processingPhoto = false; input.value = ''; }
  }

  removePhoto(index: number): void { this.evidencias.splice(index, 1); }

  submit(): void {
    if (!this.tarea || this.saving) return;
    if (this.tarea.requiereObservacion && !this.observacion.trim()) { this.toast.warning('Esta tarea requiere una observación.'); return; }
    if (this.tarea.requiereFoto && !this.evidencias.length) { this.toast.warning('Esta tarea requiere al menos una fotografía.'); return; }
    this.saving = true;
    this.service.cumplirTarea(this.tarea.id, { observacion: this.observacion.trim() || null, evidencias: this.evidencias })
      .pipe(finalize(() => this.saving = false)).subscribe({
        next: () => { this.toast.success('Tarea realizada y evidencia guardada.'); this.router.navigate(['/home']); },
        error: (error) => this.toast.error(mensajeErrorHttp(error, 'No fue posible completar la tarea.')),
      });
  }
}

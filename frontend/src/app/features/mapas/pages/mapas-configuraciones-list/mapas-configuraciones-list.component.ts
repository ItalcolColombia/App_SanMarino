import { Component, OnInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MapasService, MapaListDto, CreateMapaDto, UpdateMapaDto, MAPA_PLANTILLAS } from '../../services/mapas.service';
import { MapaEjecutarModalComponent } from '../../components/mapa-ejecutar-modal/mapa-ejecutar-modal.component';

@Component({
  selector: 'app-mapas-configuraciones-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MapaEjecutarModalComponent],
  templateUrl: './mapas-configuraciones-list.component.html',
  styleUrls: ['./mapas-configuraciones-list.component.scss']
})
export class MapasConfiguracionesListComponent implements OnInit {
  list: MapaListDto[] = [];
  loading = false;
  error: string | null = null;
  showModal = false;
  editingId: number | null = null;
  showEjecutarModal = false;
  ejecutarMapaId: number | null = null;
  formNombre = '';
  formDescripcion = '';
  formCodigoPlantilla = '';
  formIsActive = true;
  saving = false;
  readonly plantillas = MAPA_PLANTILLAS;

  constructor(private mapasService: MapasService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.mapasService.getAll().subscribe({
      next: (data) => {
        this.list = data;
        this.loading = false;
      },
      error: (err) => {
        this.error = err?.error?.message || err?.message || 'Error al cargar mapas';
        this.loading = false;
      }
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.formNombre = '';
    this.formDescripcion = '';
    this.formCodigoPlantilla = '';
    this.formIsActive = true;
    this.showModal = true;
  }

  openEdit(m: MapaListDto): void {
    this.editingId = m.id;
    this.formNombre = m.nombre;
    this.formDescripcion = m.descripcion ?? '';
    this.formCodigoPlantilla = m.codigoPlantilla ?? '';
    this.formIsActive = m.isActive;
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.editingId = null;
  }

  openEjecutarModal(m: MapaListDto): void {
    this.ejecutarMapaId = m.id;
    this.showEjecutarModal = true;
  }

  closeEjecutarModal(): void {
    this.showEjecutarModal = false;
    this.ejecutarMapaId = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.showEjecutarModal) this.closeEjecutarModal();
    else if (this.showModal) this.closeModal();
  }

  save(): void {
    const nombre = (this.formNombre || '').trim();
    if (!nombre) {
      this.error = 'El nombre es requerido';
      return;
    }
    this.saving = true;
    this.error = null;
    if (this.editingId != null) {
      const dto: UpdateMapaDto = {
        nombre,
        descripcion: this.formDescripcion.trim() || null,
        codigoPlantilla: this.formCodigoPlantilla.trim() || null,
        isActive: this.formIsActive
      };
      this.mapasService.update(this.editingId, dto).subscribe({
        next: () => {
          this.saving = false;
          this.closeModal();
          this.load();
        },
        error: (err) => {
          this.error = err?.error?.message || err?.message || 'Error al actualizar';
          this.saving = false;
        }
      });
    } else {
      const dto: CreateMapaDto = {
        nombre,
        descripcion: this.formDescripcion.trim() || null,
        codigoPlantilla: this.formCodigoPlantilla.trim() || null,
        isActive: this.formIsActive
      };
      this.mapasService.create(dto).subscribe({
        next: () => {
          this.saving = false;
          this.closeModal();
          this.load();
        },
        error: (err) => {
          this.error = err?.error?.message || err?.message || 'Error al crear';
          this.saving = false;
        }
      });
    }
  }

  deleteMapa(m: MapaListDto, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (!confirm(`¿Eliminar el mapa "${m.nombre}"?`)) return;
    this.mapasService.delete(m.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message || err?.message || 'Error al eliminar')
    });
  }

  formatDate(s: string | null): string {
    if (!s) return '—';
    try {
      const d = new Date(s);
      return isNaN(d.getTime()) ? s : d.toLocaleDateString();
    } catch {
      return s;
    }
  }
}

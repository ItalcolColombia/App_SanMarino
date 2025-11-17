// src/app/features/lote/components/modal-traslado-lote/modal-traslado-lote.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faTimes, faArrowRight, faExclamationTriangle, faCheckCircle, 
  faBuilding, faMapMarkerAlt, faWarehouse, faComment
} from '@fortawesome/free-solid-svg-icons';

import { LoteDto } from '../../services/lote.service';
import { FarmDto } from '../../../farm/services/farm.service';
import { NucleoDto } from '../../../nucleo/services/nucleo.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { GalponService } from '../../../galpon/services/galpon.service';
import { NucleoService } from '../../../nucleo/services/nucleo.service';

export interface TrasladoLoteData {
  loteId: number;
  loteNombre: string;
  granjaOrigenId: number;
  granjaOrigenNombre: string;
}

@Component({
  selector: 'app-modal-traslado-lote',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './modal-traslado-lote.component.html',
  styleUrls: ['./modal-traslado-lote.component.scss']
})
export class ModalTrasladoLoteComponent implements OnInit, OnChanges {
  @Input() isOpen = false;
  @Input() lote: LoteDto | null = null;
  @Input() farms: FarmDto[] = [];
  @Input() nucleos: NucleoDto[] = [];
  @Input() loading = false;

  @Output() closed = new EventEmitter<void>();
  @Output() confirm = new EventEmitter<{
    loteId: number;
    granjaDestinoId: number;
    nucleoDestinoId?: string | null;
    galponDestinoId?: string | null;
    observaciones?: string | null;
  }>();

  // Iconos
  faTimes = faTimes;
  faArrowRight = faArrowRight;
  faExclamationTriangle = faExclamationTriangle;
  faCheckCircle = faCheckCircle;
  faBuilding = faBuilding;
  faMapMarkerAlt = faMapMarkerAlt;
  faWarehouse = faWarehouse;
  faComment = faComment;

  // Estado del formulario
  granjaDestinoId: number | null = null;
  nucleoDestinoId: string | null = null;
  galponDestinoId: string | null = null;
  observaciones: string = '';

  // Datos filtrados
  granjasDisponibles: FarmDto[] = [];
  nucleosFiltrados: NucleoDto[] = [];
  galponesFiltrados: GalponDetailDto[] = [];

  // Estados
  showConfirmacion = false;
  pasoActual: 'seleccion' | 'confirmacion' = 'seleccion';

  constructor(
    private galponService: GalponService,
    private nucleoService: NucleoService
  ) {}

  ngOnInit(): void {
    this.resetForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      this.resetForm();
      this.filtrarGranjasDisponibles();
    }
    if (changes['lote'] && this.lote) {
      this.filtrarGranjasDisponibles();
    }
  }

  resetForm(): void {
    this.granjaDestinoId = null;
    this.nucleoDestinoId = null;
    this.galponDestinoId = null;
    this.observaciones = '';
    this.nucleosFiltrados = [];
    this.galponesFiltrados = [];
    this.showConfirmacion = false;
    this.pasoActual = 'seleccion';
  }

  filtrarGranjasDisponibles(): void {
    if (!this.lote) {
      this.granjasDisponibles = this.farms;
      return;
    }
    // Excluir la granja actual del lote
    this.granjasDisponibles = this.farms.filter(f => f.id !== this.lote!.granjaId);
  }

  onGranjaDestinoChange(): void {
    this.nucleoDestinoId = null;
    this.galponDestinoId = null;
    
    if (this.granjaDestinoId) {
      // Filtrar núcleos de la granja destino
      this.nucleosFiltrados = this.nucleos.filter(n => n.granjaId === Number(this.granjaDestinoId));
      
      // Cargar galpones de la granja destino
      this.galponService.getByGranja(Number(this.granjaDestinoId)).subscribe({
        next: (galpones: GalponDetailDto[]) => {
          this.galponesFiltrados = galpones;
        },
        error: (error) => {
          console.error('Error al cargar galpones:', error);
          this.galponesFiltrados = [];
        }
      });
    } else {
      this.nucleosFiltrados = [];
      this.galponesFiltrados = [];
    }
  }

  onNucleoDestinoChange(): void {
    this.galponDestinoId = null;
    
    if (this.granjaDestinoId && this.nucleoDestinoId) {
      // Filtrar galpones por granja y núcleo
      this.galponService.getByGranjaAndNucleo(
        Number(this.granjaDestinoId), 
        this.nucleoDestinoId
      ).subscribe({
        next: (galpones: GalponDetailDto[]) => {
          this.galponesFiltrados = galpones;
        },
        error: (error) => {
          console.error('Error al cargar galpones:', error);
          this.galponesFiltrados = [];
        }
      });
    }
  }

  getGranjaDestinoNombre(): string {
    if (!this.granjaDestinoId) return '';
    const granja = this.granjasDisponibles.find(f => f.id === this.granjaDestinoId);
    return granja?.name || '';
  }

  getNucleoDestinoNombre(): string {
    if (!this.nucleoDestinoId) return 'Sin núcleo específico';
    const nucleo = this.nucleosFiltrados.find(n => n.nucleoId === this.nucleoDestinoId);
    return nucleo?.nucleoNombre || this.nucleoDestinoId;
  }

  getGalponDestinoNombre(): string {
    if (!this.galponDestinoId) return 'Sin galpón específico';
    const galpon = this.galponesFiltrados.find(g => g.galponId === this.galponDestinoId);
    return galpon?.galponNombre || this.galponDestinoId;
  }

  validarFormulario(): boolean {
    // Validar que se haya seleccionado una granja destino
    if (!this.granjaDestinoId || this.granjaDestinoId <= 0) {
      return false;
    }

    // Validar que la granja destino existe en la lista
    const granjaExiste = this.granjasDisponibles.some(f => f.id === this.granjaDestinoId);
    if (!granjaExiste) {
      return false;
    }

    // Validar que no sea la misma granja que la actual
    if (this.lote && this.lote.granjaId === this.granjaDestinoId) {
      return false;
    }

    // Si se seleccionó núcleo, validar que pertenece a la granja
    if (this.nucleoDestinoId) {
      const nucleoValido = this.nucleosFiltrados.some(n => n.nucleoId === this.nucleoDestinoId);
      if (!nucleoValido) {
        return false;
      }
    }

    // Si se seleccionó galpón, validar que pertenece a la granja/núcleo
    if (this.galponDestinoId) {
      const galponValido = this.galponesFiltrados.some(g => g.galponId === this.galponDestinoId);
      if (!galponValido) {
        return false;
      }
    }

    return true;
  }

  onSiguiente(): void {
    if (!this.validarFormulario()) {
      return;
    }
    this.pasoActual = 'confirmacion';
  }

  onVolver(): void {
    this.pasoActual = 'seleccion';
  }

  onConfirmar(): void {
    if (!this.lote || !this.granjaDestinoId) {
      return;
    }

    this.confirm.emit({
      loteId: this.lote.loteId!,
      granjaDestinoId: this.granjaDestinoId,
      nucleoDestinoId: this.nucleoDestinoId || null,
      galponDestinoId: this.galponDestinoId || null,
      observaciones: this.observaciones.trim() || null
    });
  }

  onCancelar(): void {
    this.resetForm();
    this.closed.emit();
  }

  onBackdropClick(event: Event): void {
    if (event.target === event.currentTarget) {
      this.onCancelar();
    }
  }
}


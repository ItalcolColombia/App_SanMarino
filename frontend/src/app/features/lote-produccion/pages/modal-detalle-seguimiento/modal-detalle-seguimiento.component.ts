import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProduccionService, SeguimientoItemDto } from '../../services/produccion.service';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';

@Component({
  selector: 'app-modal-detalle-seguimiento',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './modal-detalle-seguimiento.component.html',
  styleUrls: ['./modal-detalle-seguimiento.component.scss']
})
export class ModalDetalleSeguimientoComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() seguimientoId: number | null = null;
  @Output() close = new EventEmitter<void>();

  loading: boolean = false;
  seguimiento: SeguimientoItemDto | null = null;
  isEcuadorOrPanama: boolean = false;

  constructor(
    private produccionService: ProduccionService,
    private countryFilter: CountryFilterService
  ) {}

  ngOnInit(): void {
    this.isEcuadorOrPanama = this.countryFilter.isEcuadorOrPanama();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen']?.currentValue && this.seguimientoId) {
      this.cargarDetalle();
    }
  }

  cargarDetalle(): void {
    if (!this.seguimientoId) return;

    this.loading = true;
    this.produccionService.obtenerSeguimientoPorId(this.seguimientoId).subscribe({
      next: (data) => {
        this.seguimiento = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error al cargar detalle:', err);
        this.loading = false;
      }
    });
  }

  onClose(): void {
    this.close.emit();
  }

  getEtapaLabel(etapa: number): string {
    switch (etapa) {
      case 1: return 'Etapa 1 (Semana 25-33)';
      case 2: return 'Etapa 2 (Semana 34-50)';
      case 3: return 'Etapa 3 (Semana >50)';
      default: return `Etapa ${etapa}`;
    }
  }

  tieneDatosClasificadora(): boolean {
    if (!this.seguimiento) return false;
    return !!(
      this.seguimiento.huevoLimpio || this.seguimiento.huevoTratado ||
      this.seguimiento.huevoSucio || this.seguimiento.huevoDeforme ||
      this.seguimiento.huevoBlanco || this.seguimiento.huevoDobleYema ||
      this.seguimiento.huevoPiso || this.seguimiento.huevoPequeno ||
      this.seguimiento.huevoRoto || this.seguimiento.huevoDesecho ||
      this.seguimiento.huevoOtro
    );
  }

  tieneDatosPesaje(): boolean {
    if (!this.seguimiento) return false;
    return !!(
      this.seguimiento.pesoH || this.seguimiento.pesoM ||
      this.seguimiento.uniformidad || this.seguimiento.coeficienteVariacion ||
      this.seguimiento.observacionesPesaje
    );
  }
}





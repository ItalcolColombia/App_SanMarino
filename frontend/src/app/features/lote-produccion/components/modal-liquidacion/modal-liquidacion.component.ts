import { Component, Input, Output, EventEmitter, OnInit, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-modal-liquidacion',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './modal-liquidacion.component.html',
  styleUrls: ['./modal-liquidacion.component.scss']
})
export class ModalLiquidacionComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() loteId: number | null = null;
  @Input() loteNombre: string = '';
  
  @Output() close = new EventEmitter<void>();

  // Variables para liquidación
  liquidacionLoading = false;
  liquidacionData: any = null;

  constructor() { }

  ngOnInit(): void {
    if (this.isOpen && this.loteId) {
      this.cargarLiquidacion();
    }
  }

  ngOnChanges(): void {
    if (this.isOpen && this.loteId) {
      this.cargarLiquidacion();
    }
  }

  onClose(): void {
    this.close.emit();
  }

  cargarLiquidacion(): void {
    if (!this.loteId) return;
    this.liquidacionLoading = true;

    // Simular carga de datos (aquí deberías llamar al servicio real)
    setTimeout(() => {
      this.liquidacionData = {
        loteId: this.loteId!.toString(),
        loteNombre: this.loteNombre,
        fechaInicio: '2024-01-01',
        fechaFin: '2024-12-31',
        totalDias: 365,
        resumen: {
          poblacionInicial: 1000,
          poblacionFinal: 850,
          mortalidadTotal: 150,
          mortalidadPorcentaje: 15.0,
          huevosTotales: 25000,
          huevosIncubables: 22000,
          eficienciaProduccion: 88.0,
          consumoTotalHembras: 1500.5,
          consumoTotalMachos: 800.3,
          pesoPromedioHuevo: 65.5
        },
        metricas: {
          mortalidadDiariaPromedio: 0.41,
          produccionDiariaPromedio: 68.5,
          eficienciaDiariaPromedio: 88.0,
          consumoDiarioPromedio: 6.3,
          uniformidadProduccion: 85.2
        },
        etapas: [
          {
            etapa: 1,
            nombre: 'Etapa 1 (Semanas 25-33)',
            dias: 63,
            mortalidad: 45,
            mortalidadPorcentaje: 4.5,
            huevosTotales: 8000,
            huevosIncubables: 7200,
            eficiencia: 90.0,
            consumoHembras: 450.2,
            consumoMachos: 200.1
          },
          {
            etapa: 2,
            nombre: 'Etapa 2 (Semanas 34-50)',
            dias: 119,
            mortalidad: 75,
            mortalidadPorcentaje: 7.5,
            huevosTotales: 12000,
            huevosIncubables: 10500,
            eficiencia: 87.5,
            consumoHembras: 750.3,
            consumoMachos: 400.2
          },
          {
            etapa: 3,
            nombre: 'Etapa 3 (>50 semanas)',
            dias: 183,
            mortalidad: 30,
            mortalidadPorcentaje: 3.0,
            huevosTotales: 5000,
            huevosIncubables: 4300,
            eficiencia: 86.0,
            consumoHembras: 300.0,
            consumoMachos: 200.0
          }
        ]
      };
      this.liquidacionLoading = false;
    }, 1500);
  }

  getEficienciaColor(eficiencia: number): string {
    if (eficiencia >= 90) return 'success';
    if (eficiencia >= 80) return 'warning';
    return 'danger';
  }

  getMortalidadColor(porcentaje: number): string {
    if (porcentaje <= 5) return 'success';
    if (porcentaje <= 10) return 'warning';
    return 'danger';
  }

  formatDMY = (input: string | Date | null | undefined): string => {
    if (!input) return '';
    
    if (input instanceof Date && !isNaN(input.getTime())) {
      const y = input.getFullYear();
      const m = String(input.getMonth() + 1).padStart(2, '0');
      const d = String(input.getDate()).padStart(2, '0');
      return `${d}/${m}/${y}`;
    }

    const s = String(input).trim();
    const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
    const m1 = s.match(ymd);
    if (m1) return `${m1[3]}/${m1[2]}/${m1[1]}`;

    return s;
  };
}

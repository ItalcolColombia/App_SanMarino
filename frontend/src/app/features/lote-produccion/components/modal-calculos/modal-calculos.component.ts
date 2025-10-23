import { Component, Input, Output, EventEmitter, OnInit, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-modal-calculos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './modal-calculos.component.html',
  styleUrls: ['./modal-calculos.component.scss']
})
export class ModalCalculosComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() loteId: number | null = null;
  @Input() loteNombre: string = '';
  
  @Output() close = new EventEmitter<void>();

  // Variables para cálculos
  calcsLoading = false;
  calcsDesde: string | null = null;
  calcsHasta: string | null = null;

  calcsResp: {
    loteId: string;
    total: number;
    desde?: string | null;
    hasta?: string | null;
    items: Array<{
      fecha: string;
      edadDias?: number | null;
      mortalidadH: number;
      mortalidadM: number;
      selH: number;
      consKgH: number;
      consKgM: number;
      huevoTot: number;
      huevoInc: number;
      pesoHuevo: number;
      etapa: number;
      eficiencia: number;
      mortalidadTotal: number;
      mortalidadPorcentaje: number;
    }>;
  } | null = null;

  constructor() { }

  ngOnInit(): void {
    if (this.isOpen && this.loteId) {
      this.reloadCalculos();
    }
  }

  ngOnChanges(): void {
    if (this.isOpen && this.loteId) {
      this.calcsDesde = null;
      this.calcsHasta = null;
      this.reloadCalculos();
    }
  }

  onClose(): void {
    this.close.emit();
  }

  reloadCalculos(): void {
    if (!this.loteId) return;
    this.calcsLoading = true;

    // Simular carga de datos (aquí deberías llamar al servicio real)
    setTimeout(() => {
      this.calcsResp = {
        loteId: this.loteId!.toString(),
        total: 0,
        desde: this.calcsDesde,
        hasta: this.calcsHasta,
        items: []
      };
      this.calcsLoading = false;
    }, 1000);
  }

  onLimpiarFiltros(): void {
    this.calcsDesde = null;
    this.calcsHasta = null;
    this.reloadCalculos();
  }

  // Helper para formatear fechas
  formatDMY = (input: string | Date | null | undefined): string => {
    if (!input) return '';
    
    if (input instanceof Date && !isNaN(input.getTime())) {
      const y = input.getFullYear();
      const m = String(input.getMonth() + 1).padStart(2, '0');
      const d = String(input.getDate()).padStart(2, '0');
      return `${d}/${m}/${y}`;
    }

    const s = String(input).trim();

    // YYYY-MM-DD
    const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
    const m1 = s.match(ymd);
    if (m1) return `${m1[3]}/${m1[2]}/${m1[1]}`;

    // mm/dd/aaaa o dd/mm/aaaa
    const sl = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;
    const m2 = s.match(sl);
    if (m2) {
      let a = parseInt(m2[1], 10);
      let b = parseInt(m2[2], 10);
      const yyyy = parseInt(m2[3], 10);
      let mm = a, dd = b;
      if (a > 12 && b <= 12) { mm = b; dd = a; }
      const mmS = String(mm).padStart(2, '0');
      const ddS = String(dd).padStart(2, '0');
      return `${ddS}/${mmS}/${yyyy}`;
    }

    // ISO (con T). Extrae la fecha en LOCAL sin cambiar el día
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${day}/${m}/${y}`;
    }

    return '';
  };

  getEficienciaColor(eficiencia: number): string {
    if (eficiencia >= 90) return 'success';
    if (eficiencia >= 70) return 'warning';
    return 'danger';
  }

  getMortalidadColor(porcentaje: number): string {
    if (porcentaje <= 5) return 'success';
    if (porcentaje <= 10) return 'warning';
    return 'danger';
  }
}

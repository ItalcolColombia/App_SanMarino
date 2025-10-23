import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoteDto } from '../../../lote/services/lote.service';

@Component({
  selector: 'app-ficha-lote-select',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './ficha-lote-select.component.html',
  styleUrls: ['./ficha-lote-select.component.scss']
})
export class FichaLoteSelectComponent implements OnInit {
  @Input() selectedLote: LoteDto | null = null;
  @Input() selectedGranjaName: string = '';
  @Input() selectedNucleoNombre: string = '';

  constructor() { }

  ngOnInit(): void {
  }

  // ================== HELPERS ==================
  /** Edad (en días) a HOY desde fecha de encasetamiento (mínimo 1). */
  calcularEdadDias(fechaEncaset?: string | Date | null): number {
    const encYmd = this.toYMD(fechaEncaset);
    const enc = this.ymdToLocalNoonDate(encYmd);
    if (!enc) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const now = this.ymdToLocalNoonDate(this.todayYMD())!; // hoy al mediodía local
    return Math.max(1, Math.floor((now.getTime() - enc.getTime()) / MS_DAY) + 1);
  }

  /** Hoy en formato YYYY-MM-DD (local, sin zona) para <input type="date"> */
  private todayYMD(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  /** Normaliza cadenas mm/dd/aaaa, dd/mm/aaaa, ISO o Date a YYYY-MM-DD (local) */
  private toYMD(input: string | Date | null | undefined): string | null {
    if (!input) return null;

    if (input instanceof Date && !isNaN(input.getTime())) {
      const y = input.getFullYear();
      const m = String(input.getMonth() + 1).padStart(2, '0');
      const d = String(input.getDate()).padStart(2, '0');
      return `${y}-${m}-${d}`;
    }

    const s = String(input).trim();

    // YYYY-MM-DD
    const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
    const m1 = s.match(ymd);
    if (m1) return `${m1[1]}-${m1[2]}-${m1[3]}`;

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
      return `${yyyy}-${mmS}-${ddS}`;
    }

    // ISO (con T). Extrae la fecha en LOCAL sin cambiar el día
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${y}-${m}-${day}`;
    }

    return null;
  }

  /** Date (local) a partir de YMD en el mediodía local (para cálculos de semanas sin corrimientos) */
  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) return null;
    const d = new Date(`${ymd}T12:00:00`);
    return isNaN(d.getTime()) ? null : d;
  }

  /** Muestra dd/MM/aaaa SIN timezone shift a partir de cualquier entrada */
  formatDMY = (input: string | Date | null | undefined): string => {
    const ymd = this.toYMD(input);
    if (!ymd) return '';
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  };
}

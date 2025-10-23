import { Component, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto } from '../../services/produccion.service';

@Component({
  selector: 'app-tabla-lista-registro',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-lista-registro.component.html',
  styleUrls: ['./tabla-lista-registro.component.scss']
})
export class TablaListaRegistroComponent implements OnInit {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() loading: boolean = false;
  @Input() selectedLoteId: number | null = null;
  @Input() selectedLote: any = null;
  
  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoItemDto>();
  @Output() delete = new EventEmitter<number>();

  constructor() { }

  ngOnInit(): void {
    // No specific initialization needed for Producción
  }

  // ================== HELPERS ==================
  trackById = (_: number, r: SeguimientoItemDto) => r.id;

  /** Edad (en días) del pollito AL MOMENTO DEL REGISTRO. */
  calcularEdadDiasDesde(fechaEncaset?: string | Date | null, fechaReferencia?: string | Date | null): number {
    const enc = this.ymdToLocalNoonDate(this.toYMD(fechaEncaset));
    const ref = this.ymdToLocalNoonDate(this.toYMD(fechaReferencia));
    if (!enc || !ref) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const diff = ref.getTime() - enc.getTime();
    if (diff < 0) return 0;
    return Math.max(1, Math.floor(diff / MS_DAY) + 1);
  }

  /** Atajo para la tabla: edad en días del registro s usando la fecha de inicio del lote de producción. */
  edadDiasDe(s: SeguimientoItemDto): number {
    // Para Producción, calculamos la edad desde la fecha de inicio del lote de producción
    // Por ahora retornamos 0 ya que necesitamos acceso al ProduccionLote
    return 0;
  }

  // ================== EVENTOS ==================
  onCreate(): void {
    this.create.emit();
  }

  onEdit(seguimiento: SeguimientoItemDto): void {
    this.edit.emit(seguimiento);
  }

  onDelete(id: number): void {
    this.delete.emit(id);
  }

  // ================== HELPERS DE FECHA ==================
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
      let a = parseInt(m2[1], 10); // mm o dd
      let b = parseInt(m2[2], 10); // dd o mm
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
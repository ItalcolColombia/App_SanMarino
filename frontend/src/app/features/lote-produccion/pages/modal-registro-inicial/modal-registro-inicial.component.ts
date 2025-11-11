import { Component, Input, Output, EventEmitter, OnInit, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CrearProduccionLoteRequest } from '../../services/produccion.service';

@Component({
  selector: 'app-modal-registro-inicial',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './modal-registro-inicial.component.html',
  styleUrls: ['./modal-registro-inicial.component.scss']
})
export class ModalRegistroInicialComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() loteId: number | null = null;
  @Input() loteNombre: string = '';
  @Input() nucleoAsignado: string = '';
  @Input() nucleosDisponibles: Array<{ nucleoId: string, nucleoNombre: string }> = [];
  @Input() loading: boolean = false;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<CrearProduccionLoteRequest>();

  // Formulario
  form!: FormGroup;

  constructor(private fb: FormBuilder) { }

  ngOnInit(): void {
    this.initializeForm();
  }

  ngOnChanges(): void {
    if (this.isOpen && this.loteId) {
      this.resetForm();
    }
  }

  // ================== FORMULARIO ==================
  private initializeForm(): void {
    this.form = this.fb.group({
      fechaInicio: [this.todayYMD(), Validators.required],
      loteId: [null, Validators.required],
      avesInicialesH: [0, [Validators.required, Validators.min(0)]],
      avesInicialesM: [0, [Validators.required, Validators.min(0)]],
      huevosIniciales: [0, [Validators.required, Validators.min(0)]],
      tipoNido: ['Manual', Validators.required],
      ciclo: ['normal', Validators.required],
      nucleoP: ['']
    });
  }

  private resetForm(): void {
    this.form.reset({
      fechaInicio: this.todayYMD(),
      loteId: this.loteId,
      avesInicialesH: 0,
      avesInicialesM: 0,
      huevosIniciales: 0,
      tipoNido: 'Manual',
      ciclo: 'normal',
      nucleoP: this.nucleoAsignado || ''
    });
  }

  // ================== EVENTOS ==================
  onClose(): void {
    this.close.emit();
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.value;
    const ymd = this.toYMD(raw.fechaInicio)!;

    const request: CrearProduccionLoteRequest = {
      loteId: raw.loteId,
      fechaInicio: this.ymdToIsoAtNoon(ymd),
      avesInicialesH: Number(raw.avesInicialesH) || 0,
      avesInicialesM: Number(raw.avesInicialesM) || 0,
      huevosIniciales: Number(raw.huevosIniciales) || 0,
      tipoNido: raw.tipoNido,
      ciclo: raw.ciclo,
      nucleoP: raw.nucleoP?.trim() || undefined
    };

    this.save.emit(request);
  }

  // ================== HELPERS ==================
  getTotalAves(): number {
    const hembras = Number(this.form.get('avesInicialesH')?.value) || 0;
    const machos = Number(this.form.get('avesInicialesM')?.value) || 0;
    return hembras + machos;
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

  /** Convierte YYYY-MM-DD a ISO asegurando MEDIODÍA local → evita cruzar de día por zona horaria */
  private ymdToIsoAtNoon(ymd: string): string {
    const iso = new Date(`${ymd}T12:00:00`);
    return iso.toISOString();
  }
}

import { Component, Input, Output, EventEmitter, OnInit, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CrearSeguimientoRequest, SeguimientoItemDto } from '../../services/produccion.service';

@Component({
  selector: 'app-modal-seguimiento-diario',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './modal-seguimiento-diario.component.html',
  styleUrls: ['./modal-seguimiento-diario.component.scss']
})
export class ModalSeguimientoDiarioComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() produccionLoteId: number | null = null;
  @Input() editingSeguimiento: SeguimientoItemDto | null = null;
  @Input() loading: boolean = false;
  
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<CrearSeguimientoRequest>();

  // Formulario
  form!: FormGroup;

  constructor(private fb: FormBuilder) { }

  ngOnInit(): void {
    this.initializeForm();
  }

  ngOnChanges(): void {
    if (this.isOpen && this.produccionLoteId) {
      if (this.editingSeguimiento) {
        this.populateForm();
      } else {
        this.resetForm();
      }
    }
  }

  // ================== FORMULARIO ==================
  private initializeForm(): void {
    this.form = this.fb.group({
      fechaRegistro: [this.todayYMD(), Validators.required],
      produccionLoteId: [null, Validators.required],
      mortalidadH: [0, [Validators.required, Validators.min(0)]],
      mortalidadM: [0, [Validators.required, Validators.min(0)]],
      consumoKg: [0, [Validators.required, Validators.min(0)]],
      huevosTotales: [0, [Validators.required, Validators.min(0)]],
      huevosIncubables: [0, [Validators.required, Validators.min(0)]],
      pesoHuevo: [0, [Validators.required, Validators.min(0)]],
      observaciones: ['']
    });
  }

  private resetForm(): void {
    this.form.reset({
      fechaRegistro: this.todayYMD(),
      produccionLoteId: this.produccionLoteId,
      mortalidadH: 0,
      mortalidadM: 0,
      consumoKg: 0,
      huevosTotales: 0,
      huevosIncubables: 0,
      pesoHuevo: 0,
      observaciones: ''
    });
  }

  private populateForm(): void {
    if (!this.editingSeguimiento) return;
    
    this.form.patchValue({
      fechaRegistro: this.toYMD(this.editingSeguimiento.fechaRegistro),
      produccionLoteId: this.editingSeguimiento.produccionLoteId,
      mortalidadH: this.editingSeguimiento.mortalidadH,
      mortalidadM: this.editingSeguimiento.mortalidadM,
      consumoKg: this.editingSeguimiento.consumoKg,
      huevosTotales: this.editingSeguimiento.huevosTotales,
      huevosIncubables: this.editingSeguimiento.huevosIncubables,
      pesoHuevo: this.editingSeguimiento.pesoHuevo,
      observaciones: this.editingSeguimiento.observaciones || ''
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
    const ymd = this.toYMD(raw.fechaRegistro)!;

    const request: CrearSeguimientoRequest = {
      produccionLoteId: raw.produccionLoteId,
      fechaRegistro: this.ymdToIsoAtNoon(ymd),
      mortalidadH: Number(raw.mortalidadH) || 0,
      mortalidadM: Number(raw.mortalidadM) || 0,
      consumoKg: Number(raw.consumoKg) || 0,
      huevosTotales: Number(raw.huevosTotales) || 0,
      huevosIncubables: Number(raw.huevosIncubables) || 0,
      pesoHuevo: Number(raw.pesoHuevo) || 0,
      observaciones: raw.observaciones || undefined
    };

    this.save.emit(request);
  }

  // ================== HELPERS ==================
  getTotalMortalidad(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const machos = Number(this.form.get('mortalidadM')?.value) || 0;
    return hembras + machos;
  }

  getEficienciaProduccion(): number {
    const total = Number(this.form.get('huevosTotales')?.value) || 0;
    const incubables = Number(this.form.get('huevosIncubables')?.value) || 0;
    
    if (total === 0) return 0;
    return Math.round((incubables / total) * 100);
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

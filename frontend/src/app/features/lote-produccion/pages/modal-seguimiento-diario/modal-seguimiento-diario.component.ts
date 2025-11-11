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
  @Input() fechaEncaset: string | Date | null = null; // Fecha de encaset para calcular etapa

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<CrearSeguimientoRequest>();

  // Formulario
  form!: FormGroup;

  // Tipos de alimento disponibles
  tiposAlimento: string[] = ['Standard', 'Premium', 'Inicio', 'Postura', 'Final'];

  constructor(private fb: FormBuilder) { }

  ngOnInit(): void {
    this.initializeForm();
  }

  ngOnChanges(): void {
    if (this.isOpen) {
      // Actualizar produccionLoteId en el formulario si está disponible
      if (this.produccionLoteId && this.form) {
        this.form.patchValue({ produccionLoteId: this.produccionLoteId });
      }

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
      selH: [0, [Validators.required, Validators.min(0)]],
      consKgH: [0, [Validators.required, Validators.min(0)]],
      consKgM: [0, [Validators.required, Validators.min(0)]],
      huevosTotales: [0, [Validators.required, Validators.min(0)]],
      huevosIncubables: [0, [Validators.required, Validators.min(0)]],
      // Campos de Clasificadora de Huevos - (Limpio, Tratado) = HuevoInc +
      huevoLimpio: [0, [Validators.min(0)]],
      huevoTratado: [0, [Validators.min(0)]],
      // Campos de Clasificadora de Huevos - (Sucio, Deforme, Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, Otro) = Huevo Total
      huevoSucio: [0, [Validators.min(0)]],
      huevoDeforme: [0, [Validators.min(0)]],
      huevoBlanco: [0, [Validators.min(0)]],
      huevoDobleYema: [0, [Validators.min(0)]],
      huevoPiso: [0, [Validators.min(0)]],
      huevoPequeno: [0, [Validators.min(0)]],
      huevoRoto: [0, [Validators.min(0)]],
      huevoDesecho: [0, [Validators.min(0)]],
      huevoOtro: [0, [Validators.min(0)]],
      tipoAlimento: ['Standard', Validators.required],
      pesoHuevo: [0, [Validators.required, Validators.min(0)]],
      etapa: [1, [Validators.required, Validators.min(1), Validators.max(3)]],
      observaciones: [''],
      // Campos de Pesaje Semanal (registro una vez por semana)
      pesoH: [null, [Validators.min(0)]],
      pesoM: [null, [Validators.min(0)]],
      uniformidad: [null, [Validators.min(0), Validators.max(100)]],
      coeficienteVariacion: [null, [Validators.min(0), Validators.max(100)]],
      observacionesPesaje: ['']
    });

    // Calcular etapa automáticamente cuando cambia la fecha
    this.form.get('fechaRegistro')?.valueChanges.subscribe(() => {
      this.calcularYActualizarEtapa();
    });
  }

  private resetForm(): void {
    const fechaHoy = this.todayYMD();
    this.form.reset({
      fechaRegistro: fechaHoy,
      produccionLoteId: this.produccionLoteId,
      mortalidadH: 0,
      mortalidadM: 0,
      selH: 0,
      consKgH: 0,
      consKgM: 0,
      huevosTotales: 0,
      huevosIncubables: 0,
      huevoLimpio: 0,
      huevoTratado: 0,
      huevoSucio: 0,
      huevoDeforme: 0,
      huevoBlanco: 0,
      huevoDobleYema: 0,
      huevoPiso: 0,
      huevoPequeno: 0,
      huevoRoto: 0,
      huevoDesecho: 0,
      huevoOtro: 0,
      tipoAlimento: 'Standard',
      pesoHuevo: 0,
      etapa: this.calcularEtapa(fechaHoy),
      observaciones: '',
      // Campos de Pesaje Semanal
      pesoH: null,
      pesoM: null,
      uniformidad: null,
      coeficienteVariacion: null,
      observacionesPesaje: ''
    });
  }

  private populateForm(): void {
    if (!this.editingSeguimiento) return;

    const fechaRegistro = this.toYMD(this.editingSeguimiento.fechaRegistro);
    this.form.patchValue({
      fechaRegistro: fechaRegistro,
      produccionLoteId: this.editingSeguimiento.produccionLoteId,
      mortalidadH: this.editingSeguimiento.mortalidadH,
      mortalidadM: this.editingSeguimiento.mortalidadM,
      selH: this.editingSeguimiento.selH || 0,
      consKgH: this.editingSeguimiento.consKgH || 0,
      consKgM: this.editingSeguimiento.consKgM || 0,
      huevosTotales: this.editingSeguimiento.huevosTotales,
      huevosIncubables: this.editingSeguimiento.huevosIncubables,
      huevoLimpio: (this.editingSeguimiento as any).huevoLimpio || 0,
      huevoTratado: (this.editingSeguimiento as any).huevoTratado || 0,
      huevoSucio: (this.editingSeguimiento as any).huevoSucio || 0,
      huevoDeforme: (this.editingSeguimiento as any).huevoDeforme || 0,
      huevoBlanco: (this.editingSeguimiento as any).huevoBlanco || 0,
      huevoDobleYema: (this.editingSeguimiento as any).huevoDobleYema || 0,
      huevoPiso: (this.editingSeguimiento as any).huevoPiso || 0,
      huevoPequeno: (this.editingSeguimiento as any).huevoPequeno || 0,
      huevoRoto: (this.editingSeguimiento as any).huevoRoto || 0,
      huevoDesecho: (this.editingSeguimiento as any).huevoDesecho || 0,
      huevoOtro: (this.editingSeguimiento as any).huevoOtro || 0,
      tipoAlimento: this.editingSeguimiento.tipoAlimento || 'Standard',
      pesoHuevo: this.editingSeguimiento.pesoHuevo,
      etapa: this.editingSeguimiento.etapa || this.calcularEtapa(fechaRegistro || this.todayYMD()),
      observaciones: this.editingSeguimiento.observaciones || '',
      // Campos de Pesaje Semanal
      pesoH: (this.editingSeguimiento as any).pesoH || null,
      pesoM: (this.editingSeguimiento as any).pesoM || null,
      uniformidad: (this.editingSeguimiento as any).uniformidad || null,
      coeficienteVariacion: (this.editingSeguimiento as any).coeficienteVariacion || null,
      observacionesPesaje: (this.editingSeguimiento as any).observacionesPesaje || ''
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

    // Validación adicional: produccionLoteId es requerido
    if (!this.produccionLoteId) {
      console.error('ProduccionLoteId no está definido');
      return;
    }

    const raw = this.form.value;
    const ymd = this.toYMD(raw.fechaRegistro);

    if (!ymd) {
      console.error('Fecha de registro inválida');
      return;
    }

    const request: CrearSeguimientoRequest = {
      produccionLoteId: this.produccionLoteId, // Usar el Input directamente
      fechaRegistro: this.ymdToIsoAtNoon(ymd),
      mortalidadH: Number(raw.mortalidadH) || 0,
      mortalidadM: Number(raw.mortalidadM) || 0,
      selH: Number(raw.selH) || 0,
      consKgH: Number(raw.consKgH) || 0,
      consKgM: Number(raw.consKgM) || 0,
      huevosTotales: Number(raw.huevosTotales) || 0,
      huevosIncubables: Number(raw.huevosIncubables) || 0,
      huevoLimpio: Number(raw.huevoLimpio) || 0,
      huevoTratado: Number(raw.huevoTratado) || 0,
      huevoSucio: Number(raw.huevoSucio) || 0,
      huevoDeforme: Number(raw.huevoDeforme) || 0,
      huevoBlanco: Number(raw.huevoBlanco) || 0,
      huevoDobleYema: Number(raw.huevoDobleYema) || 0,
      huevoPiso: Number(raw.huevoPiso) || 0,
      huevoPequeno: Number(raw.huevoPequeno) || 0,
      huevoRoto: Number(raw.huevoRoto) || 0,
      huevoDesecho: Number(raw.huevoDesecho) || 0,
      huevoOtro: Number(raw.huevoOtro) || 0,
      tipoAlimento: raw.tipoAlimento || 'Standard',
      pesoHuevo: Number(raw.pesoHuevo) || 0,
      etapa: Number(raw.etapa) || this.calcularEtapa(ymd),
      observaciones: raw.observaciones?.trim() || undefined,
      // Campos de Pesaje Semanal
      pesoH: raw.pesoH ? Number(raw.pesoH) : undefined,
      pesoM: raw.pesoM ? Number(raw.pesoM) : undefined,
      uniformidad: raw.uniformidad ? Number(raw.uniformidad) : undefined,
      coeficienteVariacion: raw.coeficienteVariacion ? Number(raw.coeficienteVariacion) : undefined,
      observacionesPesaje: raw.observacionesPesaje?.trim() || undefined
    };

    this.save.emit(request);
  }

  // ================== HELPERS ==================
  getTotalMortalidad(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const machos = Number(this.form.get('mortalidadM')?.value) || 0;
    return hembras + machos;
  }

  getTotalRetiradas(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const machos = Number(this.form.get('mortalidadM')?.value) || 0;
    const selH = Number(this.form.get('selH')?.value) || 0;
    return hembras + machos + selH;
  }

  getTotalRetiradasHembras(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const selH = Number(this.form.get('selH')?.value) || 0;
    return hembras + selH;
  }

  getTotalConsumo(): number {
    const consKgH = Number(this.form.get('consKgH')?.value) || 0;
    const consKgM = Number(this.form.get('consKgM')?.value) || 0;
    return consKgH + consKgM;
  }

  getEficienciaProduccion(): number {
    const total = Number(this.form.get('huevosTotales')?.value) || 0;
    const incubables = Number(this.form.get('huevosIncubables')?.value) || 0;

    if (total === 0) return 0;
    return Math.round((incubables / total) * 100);
  }

  calcularYActualizarEtapa(): void {
    const fechaRegistro = this.form.get('fechaRegistro')?.value;
    if (fechaRegistro) {
      const etapa = this.calcularEtapa(fechaRegistro);
      this.form.patchValue({ etapa }, { emitEvent: false });
    }
  }

  calcularEtapa(fechaRegistro: string | Date | null): number {
    if (!fechaRegistro || !this.fechaEncaset) return 1;

    const fechaEncaset = new Date(this.fechaEncaset);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaEncaset.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    const semana = Math.max(25, Math.ceil(diffDays / 7));

    // Etapa 1: semana 25-33
    if (semana >= 25 && semana <= 33) return 1;
    // Etapa 2: semana 34-50
    if (semana >= 34 && semana <= 50) return 2;
    // Etapa 3: semana >50
    return 3;
  }

  getEtapaLabel(etapa: number): string {
    const labels: { [key: number]: string } = {
      1: 'Etapa 1 (Semana 25-33)',
      2: 'Etapa 2 (Semana 34-50)',
      3: 'Etapa 3 (Semana >50)'
    };
    return labels[etapa] || `Etapa ${etapa}`;
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

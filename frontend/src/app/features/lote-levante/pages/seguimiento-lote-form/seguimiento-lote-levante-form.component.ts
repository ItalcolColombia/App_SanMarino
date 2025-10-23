// src/app/features/lote-levante/pages/seguimiento-lote-levante-form/seguimiento-lote-levante-form.component.ts
import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs/operators';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';

import {
  SeguimientoLoteLevanteService,
  CreateSeguimientoLoteLevanteDto,
  UpdateSeguimientoLoteLevanteDto,
  SeguimientoLoteLevanteDto
} from '../../services/seguimiento-lote-levante.service';

import { LoteService, LoteDto } from '../../../lote/services/lote.service';

@Component({
  selector: 'app-seguimiento-lote-levante-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, SidebarComponent],
  templateUrl: './seguimiento-lote-levante-form.component.html',
  styleUrls: ['./seguimiento-lote-levante-form.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeguimientoLoteLevanteFormComponent implements OnInit {
  form!: FormGroup;

  lotes: LoteDto[] = [];
  lotesById: Record<string, LoteDto> = {};

  /** Edición sin getById: recibimos el registro por Router State (history.state.seguimiento) */
  editingRecord: SeguimientoLoteLevanteDto | null = null;

  loading = false;

  /** Sólo permitido por el backend */
  readonly cicloOptions = ['Normal'] as const;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private segSvc: SeguimientoLoteLevanteService,
    private loteSvc: LoteService
  ) {}

  get isEdit(): boolean {
    return !!this.editingRecord;
  }

  ngOnInit(): void {
    // 1) Cargar lotes y filtrar < 25 semanas
    this.loteSvc.getAll().subscribe((data) => {
      this.lotes = data.filter((l) => this.calcularEdadDias(l.fechaEncaset) < 175); // 25 semanas * 7 días = 175 días
      this.lotesById = this.lotes.reduce((acc, l) => {
        acc[l.loteId] = l;
        return acc;
      }, {} as Record<string, LoteDto>);

      // Si vino preseleccionado por query (ej: ?loteId=XYZ) y existe, lo aplicamos
      const preLote = this.route.snapshot.queryParamMap.get('loteId');
      if (preLote && this.lotesById[preLote]) {
        this.form?.get('loteId')?.setValue(preLote);
      }
    });

    // 2) Construir formulario
    this.form = this.fb.group({
      fechaRegistro: [this.todayISO(), Validators.required],
      loteId: ['', Validators.required],
      mortalidadHembras: [0, [Validators.required, Validators.min(0)]],
      mortalidadMachos:  [0, [Validators.required, Validators.min(0)]],
      selH:              [0, [Validators.required, Validators.min(0)]],
      selM:              [0, [Validators.required, Validators.min(0)]],
      errorSexajeHembras:[0, [Validators.required, Validators.min(0)]],
      errorSexajeMachos: [0, [Validators.required, Validators.min(0)]],
      tipoAlimento:      ['', Validators.required],
      consumoKgHembras:  [0, [Validators.required, Validators.min(0)]],
      observaciones:     [''],
      ciclo:             ['Normal', Validators.required],
    });

    // 3) Detectar modo edición por Router State (sin getById)
    const navState = (this.router.getCurrentNavigation()?.extras?.state || {}) as any;
    const seg: SeguimientoLoteLevanteDto | undefined = navState?.seguimiento;

    if (seg) {
      this.editingRecord = seg;
      this.form.patchValue({
        fechaRegistro: seg.fechaRegistro.substring(0, 10),
        loteId: seg.loteId,
        mortalidadHembras: seg.mortalidadHembras,
        mortalidadMachos: seg.mortalidadMachos,
        selH: seg.selH,
        selM: seg.selM,
        errorSexajeHembras: seg.errorSexajeHembras,
        errorSexajeMachos: seg.errorSexajeMachos,
        tipoAlimento: seg.tipoAlimento,
        consumoKgHembras: seg.consumoKgHembras,
        observaciones: seg.observaciones,
        ciclo: seg.ciclo || 'Normal'
      });
    } else {
      // Si solo hay un lote disponible, autoselecciona
      if (this.lotes.length === 1) {
        this.form.get('loteId')?.setValue(this.lotes[0].loteId);
      }
    }

    // 4) Sanea inputs numéricos para evitar negativos por teclado
    this.attachNonNegativeGuard([
      'mortalidadHembras','mortalidadMachos','selH','selM','errorSexajeHembras','errorSexajeMachos','consumoKgHembras'
    ]);
  }

  /** Guardar */
  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.value;

    const dto: CreateSeguimientoLoteLevanteDto = {
      fechaRegistro: new Date(raw.fechaRegistro).toISOString(),
      loteId: raw.loteId,
      mortalidadHembras: raw.mortalidadHembras,
      mortalidadMachos: raw.mortalidadMachos,
      selH: raw.selH,
      selM: raw.selM,
      errorSexajeHembras: raw.errorSexajeHembras,
      errorSexajeMachos: raw.errorSexajeMachos,
      tipoAlimento: raw.tipoAlimento,
      consumoKgHembras: raw.consumoKgHembras,
      observaciones: raw.observaciones,
      kcalAlH: null,
      protAlH: null,
      kcalAveH: null,
      protAveH: null,
      ciclo: raw.ciclo // "Normal"
    };

    this.loading = true;

    const op$ = this.isEdit
      ? this.segSvc.update({ ...dto, id: this.editingRecord!.id } as UpdateSeguimientoLoteLevanteDto)
      : this.segSvc.create(dto);

    op$
      .pipe(finalize(() => (this.loading = false)))
      .subscribe(() => this.router.navigate(['/lote-levante']));
  }

  cancel(): void {
    this.router.navigate(['/lote-levante']);
  }

  // Helpers ───────────────────────────────────────────────────────────────
  private attachNonNegativeGuard(keys: string[]): void {
    for (const k of keys) {
      const c = this.form.get(k);
      if (!c) continue;
      c.valueChanges.subscribe((val) => {
        if (val == null) return;
        const num = Number(val);
        if (!Number.isFinite(num) || num < 0) {
          c.setValue(0, { emitEvent: false });
        }
      });
    }
  }

  get f(): { [key: string]: AbstractControl } {
    return this.form.controls;
  }

  calcularEdadDias(fechaEncaset: string | Date | null | undefined): number {
    if (!fechaEncaset) return 0;
    const d = typeof fechaEncaset === 'string' ? new Date(fechaEncaset) : fechaEncaset;
    if (isNaN(d.getTime())) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    return Math.floor((Date.now() - d.getTime()) / MS_DAY) + 1; // base 1
  }

  /** @deprecated Usar calcularEdadDias() en su lugar */
  calcularEdadSemanas(fechaEncaset: string | Date | null | undefined): number {
    if (!fechaEncaset) return 0;
    const d = typeof fechaEncaset === 'string' ? new Date(fechaEncaset) : fechaEncaset;
    if (isNaN(d.getTime())) return 0;
    const MS_WEEK = 7 * 24 * 60 * 60 * 1000;
    return Math.floor((Date.now() - d.getTime()) / MS_WEEK) + 1; // base 1
  }

  loteNombre(id: string | null | undefined): string {
    return id ? (this.lotesById[id]?.loteNombre ?? id) : '—';
  }

  todayISO(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  }

  maxDateForInput(): string {
    // Para evitar fechas futuras (el back admite <= UtcNow+1; aquí lo dejamos en hoy)
    return this.todayISO();
  }
}

import { Component, Inject, OnInit, ChangeDetectionStrategy } from '@angular/core';

import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs/operators';

import type {
  CreateSeguimientoLoteLevanteDto,
  UpdateSeguimientoLoteLevanteDto,
  SeguimientoLoteLevanteDto
} from '../../../lote-levante/services/seguimiento-lote-levante.service';
import {
  SeguimientoEngordeCrudApi,
  ENGORDE_FORM_OPCIONES,
  EngordeFormOpciones
} from '../../services/seguimiento-engorde-crud.api';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { toYMD } from '../../funciones/fecha.funcion';

@Component({
  selector: 'app-seguimiento-engorde-form',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule],
  templateUrl: './seguimiento-aves-engorde-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./seguimiento-aves-engorde-form.component.scss']
})
export class SeguimientoEngordeFormComponent implements OnInit {
  form!: FormGroup;
  lotes: LoteDto[] = [];
  lotesById: Record<string, LoteDto> = {};
  editingRecord: SeguimientoLoteLevanteDto | null = null;
  loading = false;
  readonly cicloOptions = ['Normal'] as const;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private segSvc: SeguimientoEngordeCrudApi,
    private loteSvc: LoteService,
    @Inject(ENGORDE_FORM_OPCIONES) public opciones: EngordeFormOpciones
  ) {}

  get isEdit(): boolean {
    return !!this.editingRecord;
  }

  ngOnInit(): void {
    this.loteSvc.getAll().subscribe((data) => {
      /** Mismo corte que antes (edad 1-based < 175 equivale a edad 0-based < 174). */
      this.lotes = data.filter((l) => this.calcularEdadDias(l.fechaEncaset) < 174);
      this.lotesById = this.lotes.reduce((acc, l) => {
        acc[l.loteId] = l;
        return acc;
      }, {} as Record<string, LoteDto>);
      const preLote = this.route.snapshot.queryParamMap.get('loteId');
      if (preLote && this.lotesById[preLote]) {
        this.form?.get('loteId')?.setValue(preLote);
      }
    });

    this.form = this.fb.group({
      fechaRegistro: [this.todayISO(), Validators.required],
      loteId: ['', Validators.required],
      mortalidadHembras: [0, [Validators.required, Validators.min(0)]],
      mortalidadMachos: [0, [Validators.required, Validators.min(0)]],
      selH: [0, [Validators.required, Validators.min(0)]],
      selM: [0, [Validators.required, Validators.min(0)]],
      errorSexajeHembras: [0, [Validators.required, Validators.min(0)]],
      errorSexajeMachos: [0, [Validators.required, Validators.min(0)]],
      tipoAlimento: ['', Validators.required],
      consumoKgHembras: [0, [Validators.required, Validators.min(0)]],
      observaciones: [''],
      ciclo: ['Normal', Validators.required]
    });
    if (this.opciones.mostrarQq) {
      // Campos específicos Panamá: cantidad de alimento en quintales (QQ) por categoría
      this.form.addControl('qqMixtas', this.fb.control(null));
      this.form.addControl('qqHembras', this.fb.control(null));
      this.form.addControl('qqMachos', this.fb.control(null));
    }

    const navState = (this.router.getCurrentNavigation()?.extras?.state || {}) as { seguimiento?: SeguimientoLoteLevanteDto };
    const seg = navState?.seguimiento;
    if (seg) {
      this.editingRecord = seg;
      this.form.patchValue({
        fechaRegistro: toYMD(seg.fechaRegistro) ?? seg.fechaRegistro.substring(0, 10),
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
        ciclo: seg.ciclo || 'Normal',
        ...(this.opciones.mostrarQq
          ? { qqMixtas: seg.qqMixtas ?? null, qqHembras: seg.qqHembras ?? null, qqMachos: seg.qqMachos ?? null }
          : {})
      });
    } else if (this.lotes.length === 1) {
      this.form.get('loteId')?.setValue(this.lotes[0].loteId);
    }

    this.attachNonNegativeGuard([
      'mortalidadHembras', 'mortalidadMachos', 'selH', 'selM', 'errorSexajeHembras', 'errorSexajeMachos', 'consumoKgHembras'
    ]);
  }

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
      ciclo: raw.ciclo,
      // QQ (quintales de alimento) — solo cuando el país los usa (Panamá);
      // en Colombia las claves no viajan en el payload (contrato intacto).
      ...(this.opciones.mostrarQq
        ? {
            qqMixtas: this.toNumOrNull(raw.qqMixtas),
            qqHembras: this.toNumOrNull(raw.qqHembras),
            qqMachos: this.toNumOrNull(raw.qqMachos)
          }
        : {})
    };
    this.loading = true;
    const op$ = this.isEdit
      ? this.segSvc.update({ ...dto, id: this.editingRecord!.id } as UpdateSeguimientoLoteLevanteDto)
      : this.segSvc.create(dto);
    op$.pipe(finalize(() => (this.loading = false))).subscribe({
      next: () => this.router.navigate(['/daily-log', 'aves-engorde']),
      error: () => {}
    });
  }

  cancel(): void {
    this.router.navigate(['/daily-log', 'aves-engorde']);
  }

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

  /** Días desde encasetamiento hasta hoy: el primer día cuenta como 0. */
  calcularEdadDias(fechaEncaset: string | Date | null | undefined): number {
    if (!fechaEncaset) return 0;
    const d = typeof fechaEncaset === 'string' ? new Date(fechaEncaset) : fechaEncaset;
    if (isNaN(d.getTime())) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    return Math.max(0, Math.floor((Date.now() - d.getTime()) / MS_DAY));
  }

  loteNombre(id: string | null | undefined): string {
    return id ? (this.lotesById[id]?.loteNombre ?? id) : '—';
  }

  loteLabel(id: string | null | undefined): string {
    if (!id) return '—';
    const lote = this.lotesById[id];
    if (!lote) return String(id);
    const erp = (lote.loteErp ?? '').trim();
    return erp ? `${lote.loteNombre} - ERP: ${erp}` : lote.loteNombre;
  }

  loteErp(id: string | null | undefined): string {
    if (!id) return '';
    return (this.lotesById[id]?.loteErp ?? '').trim();
  }

  todayISO(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  maxDateForInput(): string {
    return this.todayISO();
  }

  /** Convierte un valor opcional del formulario a número o null (vacío => null). */
  private toNumOrNull(v: unknown): number | null {
    if (v === null || v === undefined || v === '') return null;
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }
}

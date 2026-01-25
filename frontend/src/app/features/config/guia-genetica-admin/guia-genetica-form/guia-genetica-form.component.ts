import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, finalize, map, of, Subject, switchMap, takeUntil, tap, catchError } from 'rxjs';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { FaIconLibrary } from '@fortawesome/angular-fontawesome';
import { faSave, faArrowLeft, faSpinner, faFloppyDisk } from '@fortawesome/free-solid-svg-icons';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { CreateProduccionAvicolaRawDto, GuiaGeneticaAdminService, ProduccionAvicolaRawDto, UpdateProduccionAvicolaRawDto } from '../guia-genetica-admin.service';

type FieldKey = keyof CreateProduccionAvicolaRawDto;

interface FieldDef {
  key: FieldKey;
  label: string;
  placeholder?: string;
}

@Component({
  selector: 'app-guia-genetica-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, FontAwesomeModule, SidebarComponent],
  templateUrl: './guia-genetica-form.component.html',
  styleUrls: ['./guia-genetica-form.component.scss']
})
export class GuiaGeneticaFormComponent implements OnInit, OnDestroy {
  faSave = faSave;
  faBack = faArrowLeft;
  faSpinner = faSpinner;
  faFloppy = faFloppyDisk;

  loading = false;
  error: string | null = null;
  editing = false;
  id: number | null = null;

  fields: FieldDef[] = [
    { key: 'codigoGuiaGenetica', label: 'Código guía genética (auto)' },
    { key: 'anioGuia', label: 'Año guía', placeholder: '2026' },
    { key: 'raza', label: 'Raza', placeholder: 'COBB 500' },
    { key: 'edad', label: 'Edad', placeholder: '42' },
    { key: 'mortSemH', label: '% MortSemH' },
    { key: 'retiroAcH', label: 'RetiroAcH' },
    { key: 'mortSemM', label: '% MortSemM' },
    { key: 'retiroAcM', label: 'RetiroAcM' },
    { key: 'hembras', label: 'Hembras (auto)' },
    { key: 'machos', label: 'Machos (auto)' },
    { key: 'consAcH', label: 'ConsAcH' },
    { key: 'consAcM', label: 'ConsAcM' },
    { key: 'grAveDiaH', label: 'GrAveDiaH' },
    { key: 'grAveDiaM', label: 'GrAveDiaM' },
    { key: 'pesoH', label: 'PesoH' },
    { key: 'pesoM', label: 'PesoM' },
    { key: 'uniformidad', label: '% Uniformidad' },
    { key: 'hTotalAa', label: 'HTotalAA' },
    { key: 'prodPorcentaje', label: '% Prod' },
    { key: 'hIncAa', label: 'HIncAA' },
    { key: 'aprovSem', label: '% AprovSem' },
    { key: 'pesoHuevo', label: 'PesoHuevo' },
    { key: 'masaHuevo', label: 'MasaHuevo' },
    { key: 'grasaPorcentaje', label: '% Grasa' },
    { key: 'nacimPorcentaje', label: '% Nacimiento' },
    { key: 'pollitoAa', label: 'PollitoAA' },
    { key: 'alimH', label: 'AlimH' },
    { key: 'kcalAveDiaH', label: 'KcalAveDiaH' },
    { key: 'kcalAveDiaM', label: 'KcalAveDiaM' },
    { key: 'kcalH', label: 'KcalH' },
    { key: 'protH', label: 'ProtH (%)' },
    { key: 'alimM', label: 'AlimM' },
    { key: 'kcalM', label: 'KcalM' },
    { key: 'protM', label: 'ProtM (%)' },
    { key: 'kcalSemH', label: 'KcalSemH (auto)' },
    { key: 'protHSem', label: 'ProtHSem (auto)' },
    { key: 'kcalSemM', label: 'KcalSemM (auto)' },
    { key: 'protSemM', label: 'ProtSemM (auto)' },
    { key: 'aprovAc', label: '% AprovAc' },
    { key: 'grHuevoT', label: 'GR/HuevoT' },
    { key: 'grHuevoInc', label: 'GR/HuevoInc' },
    { key: 'grPollito', label: 'GR/Pollito' },
    { key: 'valor1000', label: 'Valor 1000' },
    { key: 'valor150', label: 'Valor 150' },
    { key: 'apareo', label: '% Apareo' },
    { key: 'pesoMh', label: 'Peso M/H' }
  ];

  form = this.fb.group<Record<string, any>>({
    codigoGuiaGenetica: [''],
    anioGuia: [''],
    raza: [''],
    edad: [''],
    mortSemH: [''],
    retiroAcH: [''],
    mortSemM: [''],
    retiroAcM: [''],
    hembras: [''],
    machos: [''],
    consAcH: [''],
    consAcM: [''],
    grAveDiaH: [''],
    grAveDiaM: [''],
    pesoH: [''],
    pesoM: [''],
    uniformidad: [''],
    hTotalAa: [''],
    prodPorcentaje: [''],
    hIncAa: [''],
    aprovSem: [''],
    pesoHuevo: [''],
    masaHuevo: [''],
    grasaPorcentaje: [''],
    nacimPorcentaje: [''],
    pollitoAa: [''],
    alimH: [''],
    kcalAveDiaH: [''],
    kcalAveDiaM: [''],
    kcalH: [''],
    protH: [''],
    alimM: [''],
    kcalM: [''],
    protM: [''],
    kcalSemH: [''],
    protHSem: [''],
    kcalSemM: [''],
    protSemM: [''],
    aprovAc: [''],
    grHuevoT: [''],
    grHuevoInc: [''],
    grPollito: [''],
    valor1000: [''],
    valor150: [''],
    apareo: [''],
    pesoMh: ['']
  });

  private destroy$ = new Subject<void>();
  private prevHembras: number | null = null;
  private prevMachos: number | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: GuiaGeneticaAdminService,
    private route: ActivatedRoute,
    private router: Router,
    library: FaIconLibrary
  ) {
    library.addIcons(faSave, faArrowLeft, faSpinner, faFloppyDisk);
  }

  isAutoField(key: FieldKey): boolean {
    // Campos calculados por fórmula (se bloquean)
    return [
      'codigoGuiaGenetica',
      'masaHuevo',
      'aprovAc',
      'grHuevoT',
      'grHuevoInc',
      'grPollito',
      'hembras',
      'machos',
      'apareo',
      'kcalSemH',
      'protHSem',
      'kcalSemM',
      'protSemM'
    ].includes(key);
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.id = idParam ? Number(idParam) : null;
    this.editing = !!this.id;

    // Deshabilitar campos automáticos en el form (pero se envían con getRawValue)
    this.disableAutoFields();

    // Recalcular fórmulas cada vez que cambia algo relevante (inputs editables)
    this.form.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.recalculateDerivedFields());

    // Para hembras/machos (Edad > 1) necesitamos el registro anterior por (anio_guia + raza)
    this.watchPrevCounts();

    if (this.id) {
      this.loading = true;
      this.svc.getById(this.id)
        .pipe(finalize(() => (this.loading = false)), takeUntil(this.destroy$))
        .subscribe({
          next: (dto) => {
            this.form.patchValue(dto as any);
            // Después de cargar, recalcular para asegurar consistencia
            this.recalculateDerivedFields();
          },
          error: (err) => {
            console.error(err);
            this.error = 'No se pudo cargar el registro.';
          }
        });
    } else {
      // Nuevo: calcular con valores iniciales
      this.recalculateDerivedFields();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  back(): void {
    this.router.navigate(['/config/guia-genetica']);
  }

  save(): void {
    this.error = null;
    this.loading = true;

    const raw = this.form.getRawValue() as CreateProduccionAvicolaRawDto;
    const payload: CreateProduccionAvicolaRawDto = Object.fromEntries(
      Object.entries(raw).map(([k, v]) => [k, (v ?? '').toString().trim() || undefined])
    ) as CreateProduccionAvicolaRawDto;

    const obs = this.editing && this.id
      ? this.svc.update({ ...(payload as any), id: this.id } as UpdateProduccionAvicolaRawDto)
      : this.svc.create(payload);

    obs
      .pipe(finalize(() => (this.loading = false)), takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.router.navigate(['/config/guia-genetica', res.id]);
        },
        error: (err) => {
          console.error(err);
          this.error = 'No se pudo guardar. Verifique los datos e intente nuevamente.';
        }
      });
  }

  private disableAutoFields(): void {
    for (const f of this.fields) {
      if (this.isAutoField(f.key)) {
        const ctrl = this.form.get(f.key as string);
        // si ya viene disabled por carga, no forzar
        if (ctrl && ctrl.enabled) ctrl.disable({ emitEvent: false });
      }
    }
  }

  private watchPrevCounts(): void {
    const anioCtrl = this.form.get('anioGuia');
    const razaCtrl = this.form.get('raza');
    const edadCtrl = this.form.get('edad');
    if (!anioCtrl || !razaCtrl || !edadCtrl) return;

    // Con cualquier cambio en anio/raza/edad, buscar registro previo
    this.form.get('anioGuia')!.valueChanges
      .pipe(
        takeUntil(this.destroy$),
        debounceTime(250),
        switchMap(() => of(null))
      )
      .subscribe(() => this.loadPrevCountsAndRecalc());

    this.form.get('raza')!.valueChanges
      .pipe(
        takeUntil(this.destroy$),
        debounceTime(250),
        switchMap(() => of(null))
      )
      .subscribe(() => this.loadPrevCountsAndRecalc());

    this.form.get('edad')!.valueChanges
      .pipe(
        takeUntil(this.destroy$),
        debounceTime(250),
        switchMap(() => of(null))
      )
      .subscribe(() => this.loadPrevCountsAndRecalc());
  }

  private loadPrevCountsAndRecalc(): void {
    const anioGuia = (this.form.get('anioGuia')?.value ?? '').toString().trim();
    const raza = (this.form.get('raza')?.value ?? '').toString().trim();
    const edad = this.parseInt((this.form.get('edad')?.value ?? '').toString());

    // Si no hay datos suficientes, limpiar prev y recalcular
    if (!anioGuia || !raza || !edad || edad <= 1) {
      this.prevHembras = null;
      this.prevMachos = null;
      this.recalculateDerivedFields();
      return;
    }

    this.svc.search({
      anioGuia,
      raza,
      page: 1,
      pageSize: 200,
      sortBy: 'id',
      sortDesc: true
    })
      .pipe(
        takeUntil(this.destroy$),
        map((res) => (res.items ?? []) as ProduccionAvicolaRawDto[]),
        map((items) => {
          // buscar el mayor edad < edad actual
          const prev = items
            .map((it) => ({ it, e: this.parseInt(it.edad ?? '') }))
            .filter((x) => x.e !== null && x.e < edad)
            .sort((a, b) => (b.e ?? 0) - (a.e ?? 0))[0]?.it ?? null;
          return prev;
        }),
        tap((prev) => {
          this.prevHembras = prev ? this.parseNumber(prev.hembras ?? '') : null;
          this.prevMachos = prev ? this.parseNumber(prev.machos ?? '') : null;
        }),
        catchError((err) => {
          console.error(err);
          this.prevHembras = null;
          this.prevMachos = null;
          return of(null);
        })
      )
      .subscribe(() => this.recalculateDerivedFields());
  }

  private recalculateDerivedFields(): void {
    // leer inputs (editable)
    const raza = (this.form.get('raza')?.value ?? '').toString();
    const anio = (this.form.get('anioGuia')?.value ?? '').toString();
    const edad = this.parseInt((this.form.get('edad')?.value ?? '').toString());

    const mortH = this.parsePercent((this.form.get('mortSemH')?.value ?? '').toString()) ?? 0;
    const mortM = this.parsePercent((this.form.get('mortSemM')?.value ?? '').toString()) ?? 0;

    const pesoHuevo = this.parseNumber((this.form.get('pesoHuevo')?.value ?? '').toString());
    const prodPct = this.parsePercent((this.form.get('prodPorcentaje')?.value ?? '').toString());

    const hTotal = this.parseNumber((this.form.get('hTotalAa')?.value ?? '').toString());
    const hInc = this.parseNumber((this.form.get('hIncAa')?.value ?? '').toString());

    const consAcH = this.parseNumber((this.form.get('consAcH')?.value ?? '').toString());
    const consAcM = this.parseNumber((this.form.get('consAcM')?.value ?? '').toString());
    const pollito = this.parseNumber((this.form.get('pollitoAa')?.value ?? '').toString());

    const grAveDiaH = this.parseNumber((this.form.get('grAveDiaH')?.value ?? '').toString());
    const grAveDiaM = this.parseNumber((this.form.get('grAveDiaM')?.value ?? '').toString());
    const kcalH = this.parseNumber((this.form.get('kcalH')?.value ?? '').toString());
    const kcalM = this.parseNumber((this.form.get('kcalM')?.value ?? '').toString());
    const protH = this.parsePercent((this.form.get('protH')?.value ?? '').toString());
    const protM = this.parsePercent((this.form.get('protM')?.value ?? '').toString());

    // codigo guía genética (auto)
    const codigo = raza?.trim() && anio?.trim() && (this.form.get('edad')?.value ?? '').toString().trim()
      ? `${raza.trim()}${anio.trim()}${(this.form.get('edad')?.value ?? '').toString().trim()}`
      : '';

    // masa huevo
    const masaHuevo = (pesoHuevo !== null && prodPct !== null)
      ? (pesoHuevo * prodPct / 100)
      : null;

    // aprov ac
    const aprovAc = (hInc !== null && hTotal !== null && hTotal !== 0)
      ? (hInc / hTotal) * 100
      : null;

    // hembras/machos + apareo%
    let hembras: number | null = null;
    let machos: number | null = null;
    let apareoPct: number | null = null;

    if (edad !== null) {
      const baseH = edad === 1 ? 10000 : this.prevHembras;
      const baseM = edad === 1 ? 1400 : this.prevMachos;
      if (baseH !== null) hembras = baseH - (baseH * (mortH / 100));
      if (baseM !== null) machos = baseM - (baseM * (mortM / 100));
      if (hembras !== null && machos !== null && hembras !== 0) apareoPct = (machos / hembras) * 100;
    }

    // kcal/proteína semanal
    const kcalSemH = (kcalH !== null && grAveDiaH !== null) ? (kcalH * grAveDiaH * 7 / 1000) : null;
    const protHSem = (protH !== null && grAveDiaH !== null) ? ((protH / 100) * grAveDiaH * 7) : null;
    const kcalSemM = (kcalM !== null && grAveDiaM !== null) ? (kcalM * grAveDiaM * 7 / 1000) : null;
    const protSemM = (protM !== null && grAveDiaM !== null) ? ((protM / 100) * grAveDiaM * 7) : null;

    // GR/HuevoT, GR/HuevoInc, GR/Pollito
    let grHuevoT: number | null = null;
    let grHuevoInc: number | null = null;
    let grPollito: number | null = null;
    if (edad !== null) {
      if (edad > 24) {
        if (consAcH !== null && consAcM !== null && apareoPct !== null) {
          const totalCons = consAcH + (consAcM * (apareoPct / 100));
          if (hTotal !== null && hTotal !== 0) grHuevoT = totalCons / hTotal;
          if (hInc !== null && hInc !== 0) grHuevoInc = totalCons / hInc;
          if (pollito !== null && pollito !== 0) grPollito = totalCons / pollito;
        }
      } else {
        grHuevoT = 0;
        grHuevoInc = 0;
        grPollito = 0;
      }
    }

    // escribir en controles (disabled) sin disparar loops
    this.form.patchValue({
      codigoGuiaGenetica: codigo || undefined,
      masaHuevo: masaHuevo !== null ? this.formatNumber(masaHuevo) : undefined,
      aprovAc: aprovAc !== null ? this.formatNumber(aprovAc) : undefined,
      hembras: hembras !== null ? this.formatNumber(hembras) : undefined,
      machos: machos !== null ? this.formatNumber(machos) : undefined,
      apareo: apareoPct !== null ? this.formatNumber(apareoPct) : undefined,
      kcalSemH: kcalSemH !== null ? this.formatNumber(kcalSemH) : undefined,
      protHSem: protHSem !== null ? this.formatNumber(protHSem) : undefined,
      kcalSemM: kcalSemM !== null ? this.formatNumber(kcalSemM) : undefined,
      protSemM: protSemM !== null ? this.formatNumber(protSemM) : undefined,
      grHuevoT: grHuevoT !== null ? this.formatNumber(grHuevoT) : undefined,
      grHuevoInc: grHuevoInc !== null ? this.formatNumber(grHuevoInc) : undefined,
      grPollito: grPollito !== null ? this.formatNumber(grPollito) : undefined
    }, { emitEvent: false });
  }

  private parseInt(value: string): number | null {
    const clean = (value ?? '').toString().trim().replace(',', '.');
    if (!clean) return null;
    const n = Number(clean);
    if (Number.isNaN(n)) return null;
    return Math.trunc(n);
  }

  private parseNumber(value: string): number | null {
    const raw = (value ?? '').toString().trim();
    if (!raw) return null;
    let clean = raw.replace(/\s+/g, '').replace('%', '');
    clean = this.normalizeDecimalSeparators(clean);
    const n = Number(clean);
    return Number.isFinite(n) ? n : null;
  }

  private parsePercent(value: string): number | null {
    // devuelve 0-100 (acepta "12", "12%", "0.12" se toma como 0.12 si lo escriben así)
    return this.parseNumber(value);
  }

  private normalizeDecimalSeparators(input: string): string {
    const hasDot = input.includes('.');
    const hasComma = input.includes(',');
    if (hasDot && hasComma) {
      const lastDot = input.lastIndexOf('.');
      const lastComma = input.lastIndexOf(',');
      if (lastComma > lastDot) {
        // coma decimal, punto miles
        return input.replace(/\./g, '').replace(',', '.');
      }
      // punto decimal, coma miles
      return input.replace(/,/g, '');
    }
    if (hasComma && !hasDot) return input.replace(',', '.');
    return input;
  }

  private formatNumber(value: number): string {
    // 0.## similar al backend
    const rounded = Math.round(value * 100) / 100;
    return Number.isFinite(rounded) ? rounded.toString() : '';
  }
}


// file: src/app/features/lote-levante/components/seguimiento-calculos/seguimiento-calculos.component.ts
import { Component, Input, OnChanges, OnDestroy, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { Subscription, startWith, switchMap } from 'rxjs';
import { NgChartsModule } from 'ng2-charts';
import {
  SeguimientoLoteLevanteService,
  ResultadoLevanteResponse,
} from '../services/seguimiento-lote-levante.service';

// Asegúrate de que en tu service estén estos tipos exactos:
export interface ResultadoLevanteItemDto {
  fecha: string;
  edadDias: number | null;  // Cambiado de edadSemana a edadDias
  edadSemana?: number | null; // @deprecated - mantener para compatibilidad

  hembraViva: number | null;
  mortH: number; selH: number; errH: number;
  consKgH: number | null; pesoH: number | null; unifH: number | null; cvH: number | null;
  mortHPct: number | null; selHPct: number | null; errHPct: number | null;
  msEhH: number | null; acMortH: number | null; acSelH: number | null; acErrH: number | null;
  acConsKgH: number | null; consAcGrH: number | null; grAveDiaH: number | null;
  difConsHPct: number | null; difPesoHPct: number | null; retiroHPct: number | null; retiroHAcPct: number | null;

  machoVivo: number | null;
  mortM: number; selM: number; errM: number;
  consKgM: number | null; pesoM: number | null; unifM: number | null; cvM: number | null;
  mortMPct: number | null; selMPct: number | null; errMPct: number | null;
  msEmM: number | null; acMortM: number | null; acSelM: number | null; acErrM: number | null;
  acConsKgM: number | null; consAcGrM: number | null; grAveDiaM: number | null;
  difConsMPct: number | null; difPesoMPct: number | null; retiroMPct: number | null; retiroMAcPct: number | null;

  relMHPct: number | null;

  pesoHGuia: number | null; unifHGuia: number | null; consAcGrHGuia: number | null; grAveDiaHGuia: number | null; mortHPctGuia: number | null;
  pesoMGuia: number | null; unifMGuia: number | null; consAcGrMGuia: number | null; grAveDiaMGuia: number | null; mortMPctGuia: number | null;
  alimentoHGuia: string | null; alimentoMGuia: string | null;
}

@Component({
  selector: 'app-seguimiento-calculos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, NgChartsModule],
  templateUrl: './seguimiento-calculos.component.html',
  styleUrls: ['./seguimiento-calculos.component.scss']
})
export class SeguimientoCalculosComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) loteId: number | null = null;  // Changed from string | null to number | null

  // Formulario de filtros
  form = this.fb.group({
    desde: [null as string | null],
    hasta: [null as string | null],
    mostrar: ['consumo' as 'consumo' | 'mortalidad'] // toggle barras
  });

  // Estado
  loading = signal(false);
  data = signal<ResultadoLevanteResponse | null>(null);
  private sub?: Subscription;

  // Series / labels
  barLabels = signal<string[]>([]);
  barConsH = signal<number[]>([]);
  barConsM = signal<number[]>([]);
  barMortHPct = signal<number[]>([]);
  barMortMPct = signal<number[]>([]);

  pieLabels = ['Mort H', 'Sel H', 'Err H'];
  pieValues = signal<number[]>([0, 0, 0]);

  // Texto auxiliar de rango
  rangoTexto = computed(() => {
    const d = this.data();
    if (!d) return '';
    const p1 = d.desde ? `desde ${new Date(d.desde).toLocaleDateString()}` : '';
    const p2 = d.hasta ? `hasta ${new Date(d.hasta).toLocaleDateString()}` : '';
    const p3 = `(${d.total ?? 0} días)`;
    return [p1, p2, p3].filter(Boolean).join(' ');
  });

  // Opciones básicas de Chart.js (puedes ajustar escalas/formatos)
  barOptions = { responsive: true, maintainAspectRatio: false };
  pieOptions = { responsive: true, maintainAspectRatio: false };

  constructor(
    private fb: FormBuilder,
    private api: SeguimientoLoteLevanteService
  ) {}

  ngOnChanges(): void {
    if (!this.loteId) return;
    this.reload();
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  reload(): void {
    this.sub?.unsubscribe();
    this.loading.set(true);

    this.sub = this.form.valueChanges.pipe(
      startWith(this.form.value),
      switchMap(v => this.api.getResultado({
        loteId: this.loteId!,
        desde: v.desde ?? undefined,
        hasta: v.hasta ?? undefined,
        recalcular: true
      }))
    ).subscribe({
      next: (res) => {
        this.data.set(res);
        this.computeCharts((res.items ?? []) as ResultadoLevanteItemDto[]);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  // Normalizador para evitar NaN con null/undefined
  private n(x: number | null | undefined): number { return x ?? 0; }

  private computeCharts(items: ResultadoLevanteItemDto[]): void {
    const sorted = [...items].sort(
      (a,b) => new Date(a.fecha).getTime() - new Date(b.fecha).getTime()
    );

    this.barLabels.set(sorted.map(x => new Date(x.fecha).toLocaleDateString()));

    // Barras: consumo diario
    this.barConsH.set(sorted.map(x => this.n(x.consKgH)));
    this.barConsM.set(sorted.map(x => this.n(x.consKgM)));

    // Barras: % mortalidad
    this.barMortHPct.set(sorted.map(x => this.n(x.mortHPct)));
    this.barMortMPct.set(sorted.map(x => this.n(x.mortMPct)));

    // Torta: último día (retiros Hembras M+S+E)
    const last = sorted.at(-1);
    const mortH = this.n(last?.mortH);
    const selH  = this.n(last?.selH);
    const errH  = this.n(last?.errH);
    const total = mortH + selH + errH;
    this.pieValues.set(total > 0 ? [mortH, selH, errH] : [0, 0, 0]);
  }
}

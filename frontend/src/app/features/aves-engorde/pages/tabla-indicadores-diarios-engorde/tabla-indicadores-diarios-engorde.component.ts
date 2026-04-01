import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoLoteLevanteDto } from '../../../lote-levante/services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import {
  IndicadorDiarioFila,
  ComparativoTag
} from '../../../lote-levante/services/indicadores-diarios.models';
import { IndicadoresDiariosEngordeComputeService } from '../../services/indicadores-diarios-engorde-compute.service';

@Component({
  selector: 'app-tabla-indicadores-diarios-engorde',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-indicadores-diarios-engorde.component.html',
  styleUrls: ['./tabla-indicadores-diarios-engorde.component.scss']
})
export class TablaIndicadoresDiariosEngordeComponent implements OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() loading = false;

  filas: IndicadorDiarioFila[] = [];
  cargandoGuia = false;
  errorGuia: string | null = null;
  guiaOk = false;
  etiquetaGuiaCargada = '';

  constructor(private compute: IndicadoresDiariosEngordeComputeService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      void this.rebuild();
    }
  }

  private async rebuild(): Promise<void> {
    this.errorGuia = null;
    this.guiaOk = false;
    this.filas = [];

    if (!this.seguimientos?.length || !this.selectedLote) {
      return;
    }

    this.cargandoGuia = true;
    try {
      const res = await this.compute.compute(this.seguimientos, this.selectedLote);
      this.errorGuia = res.errorGuia;
      this.guiaOk = res.guiaOk;
      this.etiquetaGuiaCargada = res.etiquetaGuiaCargada;
      this.filas = res.filas;
    } finally {
      this.cargandoGuia = false;
    }
  }

  formatDMY(ymd: string): string {
    const p = ymd.split('-');
    if (p.length !== 3) {
      return ymd;
    }
    return `${p[2]}/${p[1]}/${p[0]}`;
  }

  formatNum(v: number | null | undefined, decimals = 1): string {
    if (v == null || Number.isNaN(v)) {
      return '—';
    }
    return v.toFixed(decimals);
  }

  formatPct(v: number | null | undefined, decimals = 2): string {
    if (v == null || Number.isNaN(v)) {
      return '—';
    }
    return `${v.toFixed(decimals)}%`;
  }

  tagPeso(row: IndicadorDiarioFila): ComparativoTag {
    if (row.pesoRealG <= 0 || row.pesoTablaG <= 0) {
      return 'na';
    }
    const a = Math.abs(row.difPesoVsTablaPct);
    if (a <= 5) {
      return 'ok';
    }
    if (a <= 12) {
      return 'warn';
    }
    return 'bad';
  }

  tagGanancia(row: IndicadorDiarioFila): ComparativoTag {
    if (row.gananciaDiariaRealG == null || row.gananciaDiariaTablaG <= 0) {
      return 'na';
    }
    const d = Math.abs(row.gananciaDiariaRealG - row.gananciaDiariaTablaG);
    const tol = Math.max(3, row.gananciaDiariaTablaG * 0.12);
    if (d <= tol) {
      return 'ok';
    }
    if (d <= tol * 2) {
      return 'warn';
    }
    return 'bad';
  }

  tagConsumoDiario(row: IndicadorDiarioFila): ComparativoTag {
    if (row.consumoDiarioTablaG <= 0) {
      return 'na';
    }
    const rel = Math.abs(row.consumoDiarioRealG - row.consumoDiarioTablaG) / row.consumoDiarioTablaG;
    if (rel <= 0.1) {
      return 'ok';
    }
    if (rel <= 0.2) {
      return 'warn';
    }
    return 'bad';
  }

  tagAlimentoAcum(row: IndicadorDiarioFila): ComparativoTag {
    if (row.alimentoAcumTablaG <= 0) {
      return 'na';
    }
    const rel = Math.abs(row.alimentoAcumRealG - row.alimentoAcumTablaG) / row.alimentoAcumTablaG;
    if (rel <= 0.1) {
      return 'ok';
    }
    if (rel <= 0.18) {
      return 'warn';
    }
    return 'bad';
  }

  tagCa(row: IndicadorDiarioFila): ComparativoTag {
    if (row.caReal == null || row.caTabla <= 0) {
      return 'na';
    }
    const rel = Math.abs(row.caReal - row.caTabla) / row.caTabla;
    if (rel <= 0.08) {
      return 'ok';
    }
    if (rel <= 0.15) {
      return 'warn';
    }
    return 'bad';
  }

  tagMortSel(row: IndicadorDiarioFila): ComparativoTag {
    if (row.mortSelTablaPct <= 0 && row.mortSelRealPct <= 0) {
      return 'na';
    }
    if (row.mortSelTablaPct <= 0) {
      return row.mortSelRealPct <= 0.15 ? 'ok' : 'warn';
    }
    if (row.mortSelRealPct <= row.mortSelTablaPct * 1.1) {
      return 'ok';
    }
    if (row.mortSelRealPct <= row.mortSelTablaPct * 1.35) {
      return 'warn';
    }
    return 'bad';
  }

  tagClass(t: ComparativoTag): string {
    return `cmp-tag cmp-tag--${t}`;
  }

  tagLabel(t: ComparativoTag): string {
    switch (t) {
      case 'ok':
        return 'OK';
      case 'warn':
        return 'Rev.';
      case 'bad':
        return 'Alerta';
      default:
        return '—';
    }
  }

  faseDiaClass(dia: number): Record<string, boolean> {
    if (dia <= 7) {
      return { 'indicadores-diarios__row--fase1': true };
    }
    if (dia <= 14) {
      return { 'indicadores-diarios__row--fase2': true };
    }
    return { 'indicadores-diarios__row--fase3': true };
  }

  descargarIndicadoresCsv(): void {
    if (!this.filas.length || !this.selectedLote) {
      return;
    }
    const sep = '\t';
    const esc = (v: string | number) => `"${String(v).replace(/"/g, '""')}"`;
    const head = [
      'Fecha',
      'Día',
      'Aves inicio',
      'Peso reg (g)',
      'Peso guía (g)',
      'Ganancia reg (g)',
      'Ganancia guía (g)',
      'Alim. diario reg (g/ave)',
      'Alim. diario guía (g/ave)',
      'Alim. acum. reg (g/ave)',
      'Alim. acum. guía (g/ave)',
      'CA reg',
      'CA guía',
      'Mort+sel reg %',
      'Mort+sel guía %'
    ];
    const lines = [head.join(sep)];
    for (const row of this.filas) {
      lines.push(
        [
          this.formatDMY(row.fechaYmd),
          row.dia,
          row.avesInicioDia,
          row.pesoRealG.toFixed(0),
          row.pesoTablaG.toFixed(0),
          row.gananciaDiariaRealG != null ? row.gananciaDiariaRealG.toFixed(1) : '',
          row.gananciaDiariaTablaG.toFixed(1),
          row.consumoDiarioRealG.toFixed(1),
          row.consumoDiarioTablaG.toFixed(1),
          row.alimentoAcumRealG.toFixed(0),
          row.alimentoAcumTablaG.toFixed(0),
          row.caReal != null ? row.caReal.toFixed(3) : '',
          row.caTabla.toFixed(3),
          row.mortSelRealPct.toFixed(2),
          row.mortSelTablaPct.toFixed(2)
        ]
          .map(v => esc(v))
          .join(sep)
      );
    }
    const bom = '\ufeff';
    const blob = new Blob([bom + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    const safe = (this.selectedLote.loteNombre ?? 'lote').replace(/[^\w\-]+/g, '_');
    a.href = URL.createObjectURL(blob);
    a.download = `indicadores_diarios_engorde_${safe}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  }
}

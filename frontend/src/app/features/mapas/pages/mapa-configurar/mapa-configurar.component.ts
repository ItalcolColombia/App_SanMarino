import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MapasService, MapaDetailDto, MapaPasoDto } from '../../services/mapas.service';
import { ENCABEZADO_ENTRADA_CIESA } from '../../data/plantilla-entrada-ciesa-encabezado';
import { MOVIMIENTO_ENTRADA_CIESA } from '../../data/plantilla-entrada-ciesa-movimiento';

const TIPOS = [
  { value: 'head', label: 'Encabezado (Head)' },
  { value: 'extraction', label: 'Extracción' },
  { value: 'transformation', label: 'Transformación' },
  { value: 'execute', label: 'Ejecución' },
  { value: 'export', label: 'Exportación' }
];

@Component({
  selector: 'app-mapa-configurar',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './mapa-configurar.component.html',
  styleUrls: ['./mapa-configurar.component.scss']
})
export class MapaConfigurarComponent implements OnInit {
  mapaId: number | null = null;
  mapa: MapaDetailDto | null = null;
  pasos: MapaPasoDto[] = [];
  /** One boolean per step: true = expanded (body visible), false = collapsed */
  stepExpanded: boolean[] = [];
  summaryExpanded = false;
  loading = false;
  saving = false;
  error: string | null = null;
  tipos = TIPOS;
  /** Campos del encabezado plantilla Entrada CIESA */
  readonly encabezadoEntradaCiesa = ENCABEZADO_ENTRADA_CIESA;
  /** Campos de la página de movimiento (afiliada al mapa Entrada CIESA) */
  readonly movimientoEntradaCiesa = MOVIMIENTO_ENTRADA_CIESA;
  plantillaEncabezadoExpanded = false;
  plantillaMovimientoExpanded = false;

  constructor(
    private route: ActivatedRoute,
    private mapasService: MapasService
  ) {}

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      this.mapaId = id ? +id : null;
      if (this.mapaId != null) this.load();
    });
  }

  load(): void {
    if (this.mapaId == null) return;
    this.loading = true;
    this.error = null;
    this.mapasService.getById(this.mapaId).subscribe({
      next: (data) => {
        this.mapa = data;
        this.pasos = (data.pasos ?? []).map(p => ({
          id: p.id,
          mapaId: p.mapaId ?? this.mapaId!,
          orden: p.orden,
          tipo: p.tipo || 'head',
          nombreEtiqueta: p.nombreEtiqueta ?? null,
          scriptSql: p.scriptSql ?? null,
          opciones: p.opciones ?? null
        }));
        if (this.pasos.length === 0) this.addPaso();
        this.syncStepExpanded();
        if (data.codigoPlantilla === 'entrada_ciesa') {
          this.plantillaEncabezadoExpanded = true;
          this.plantillaMovimientoExpanded = true;
        }
        this.loading = false;
      },
      error: (err) => {
        this.error = err?.error?.message || err?.message || 'Error al cargar el mapa';
        this.loading = false;
      }
    });
  }

  addPaso(): void {
    const nextOrden = this.pasos.length > 0
      ? Math.max(...this.pasos.map(p => p.orden)) + 1
      : 1;
    const defaultTipo = this.pasos.length === 0 ? 'head' : 'extraction';
    this.pasos.push({
      orden: nextOrden,
      tipo: defaultTipo,
      nombreEtiqueta: null,
      scriptSql: null,
      opciones: null
    });
    this.stepExpanded.push(false);
  }

  removePaso(index: number): void {
    this.pasos.splice(index, 1);
    this.stepExpanded.splice(index, 1);
    this.renumberOrden();
  }

  moveUp(index: number): void {
    if (index <= 0) return;
    [this.pasos[index - 1], this.pasos[index]] = [this.pasos[index], this.pasos[index - 1]];
    [this.stepExpanded[index - 1], this.stepExpanded[index]] = [this.stepExpanded[index], this.stepExpanded[index - 1]];
    this.renumberOrden();
  }

  moveDown(index: number): void {
    if (index >= this.pasos.length - 1) return;
    [this.pasos[index], this.pasos[index + 1]] = [this.pasos[index + 1], this.pasos[index]];
    [this.stepExpanded[index], this.stepExpanded[index + 1]] = [this.stepExpanded[index + 1], this.stepExpanded[index]];
    this.renumberOrden();
  }

  private renumberOrden(): void {
    this.pasos.forEach((p, i) => { p.orden = i + 1; });
  }

  private syncStepExpanded(): void {
    this.stepExpanded = this.pasos.map((_, i) => i === 0);
  }

  toggleStep(index: number): void {
    if (index >= 0 && index < this.stepExpanded.length) {
      this.stepExpanded[index] = !this.stepExpanded[index];
    }
  }

  toggleSummary(): void {
    this.summaryExpanded = !this.summaryExpanded;
  }

  /** Short summary for a step (for collapsed header and summary panel) */
  getStepSummary(p: MapaPasoDto): string {
    const tipo = this.getTipoLabel(p.tipo);
    const sql = (p.scriptSql ?? '').trim();
    const sqlInfo = sql.length > 0 ? `${sql.length} caracteres` : 'Sin SQL';
    const etiqueta = (p.nombreEtiqueta ?? '').trim();
    const parts = [tipo, etiqueta ? `"${etiqueta}"` : null, sqlInfo].filter(Boolean);
    return parts.join(' · ');
  }

  /** First line of SQL or placeholder for summary */
  getSqlPreview(p: MapaPasoDto, maxLen: number = 60): string {
    const sql = (p.scriptSql ?? '').trim();
    if (!sql) return '—';
    const firstLine = sql.split('\n')[0].trim();
    return firstLine.length > maxLen ? firstLine.slice(0, maxLen) + '…' : firstLine;
  }

  save(): void {
    if (this.mapaId == null) return;
    this.saving = true;
    this.error = null;
    const toSend: MapaPasoDto[] = this.pasos.map(p => ({
      id: p.id,
      mapaId: this.mapaId ?? undefined,
      orden: p.orden,
      tipo: (p.tipo || 'head').trim() || 'head',
      nombreEtiqueta: (p.nombreEtiqueta || '').trim() || null,
      scriptSql: (p.scriptSql || '').trim() || null,
      opciones: (p.opciones || '').trim() || null
    }));
    this.mapasService.savePasos(this.mapaId, toSend).subscribe({
      next: () => {
        this.saving = false;
        this.load();
      },
      error: (err) => {
        this.error = err?.error?.message || err?.message || 'Error al guardar pasos';
        this.saving = false;
      }
    });
  }

  getTipoLabel(value: string): string {
    return this.tipos.find(t => t.value === value)?.label ?? value;
  }
}

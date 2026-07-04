// frontend/src/app/features/informe-semanal-engorde/pages/informe-semanal-engorde-list/informe-semanal-engorde-list.component.ts
import { Component, OnInit, inject } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import * as XLSX from 'xlsx';
import { environment } from '../../../../../environments/environment';
import {
  InformeSemanalEngordeService,
  InformeSemanalReporte,
  InformeSemanalRequest,
  InformeSemanalFila
} from '../../services/informe-semanal-engorde.service';

interface PeFarm { id: number; name: string; }
interface PeNucleo { nucleoId: string; nucleoNombre?: string; granjaId: number; }
interface PeGalpon { galponId: string; galponNombre?: string; nucleoId: string; granjaId: number; }
interface PeLote {
  loteAveEngordeId: number; loteNombre: string; granjaId: number;
  nucleoId?: string | null; galponId?: string | null; fechaEncaset?: string | null;
}

interface FilterDataPolloEngordeResponse {
  farms?: Array<{ id?: number; Id?: number; name?: string; Name?: string }>;
  nucleos?: Array<Record<string, unknown>>;
  galpones?: Array<Record<string, unknown>>;
  lotesAveEngorde?: Array<Record<string, unknown>>;
}

@Component({
  selector: 'app-informe-semanal-engorde-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './informe-semanal-engorde-list.component.html',
  styleUrls: ['./informe-semanal-engorde-list.component.scss']
})
export class InformeSemanalEngordeListComponent implements OnInit {
  private http = inject(HttpClient);
  private service = inject(InformeSemanalEngordeService);
  private readonly filterDataUrl = `${environment.apiUrl}/LoteReproductoraAveEngorde/filter-data`;

  // Catálogos
  farms: PeFarm[] = [];
  private allNucleos: PeNucleo[] = [];
  private allGalpones: PeGalpon[] = [];
  private allLotes: PeLote[] = [];
  nucleos: PeNucleo[] = [];
  galpones: PeGalpon[] = [];
  lotes: PeLote[] = [];

  // Filtros
  todasLasGranjas = true;
  granjasSeleccionadas = new Set<number>();
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;
  fechaDesde = '';
  fechaHasta = '';

  // Estado
  loadingFilterData = true;
  loading = false;
  error: string | null = null;
  reporte: InformeSemanalReporte | null = null;

  ngOnInit(): void {
    this.cargarFilterData();
    this.establecerSemanaPorDefecto();
  }

  /** Semana calendario actual (lunes a domingo) por defecto. */
  establecerSemanaPorDefecto(): void {
    const hoy = new Date();
    const dow = (hoy.getDay() + 6) % 7; // lunes = 0
    const lunes = new Date(hoy); lunes.setDate(hoy.getDate() - dow);
    const domingo = new Date(lunes); domingo.setDate(lunes.getDate() + 6);
    this.fechaDesde = lunes.toISOString().slice(0, 10);
    this.fechaHasta = domingo.toISOString().slice(0, 10);
  }

  cargarFilterData(): void {
    this.loadingFilterData = true;
    this.http.get<FilterDataPolloEngordeResponse>(this.filterDataUrl).subscribe({
      next: (raw) => {
        const data = raw as Record<string, unknown>;
        const farms = (data['farms'] ?? data['Farms'] ?? []) as any[];
        const nucleos = (data['nucleos'] ?? data['Nucleos'] ?? []) as any[];
        const galpones = (data['galpones'] ?? data['Galpones'] ?? []) as any[];
        const lotes = (data['lotesAveEngorde'] ?? data['LotesAveEngorde'] ?? []) as any[];

        this.farms = farms.map(f => ({ id: Number(f.id ?? f.Id ?? 0), name: String(f.name ?? f.Name ?? '') }));
        this.allNucleos = nucleos.map(n => ({
          nucleoId: String(n.nucleoId ?? n.NucleoId ?? '').trim(),
          nucleoNombre: n.nucleoNombre ?? n.NucleoNombre,
          granjaId: Number(n.granjaId ?? n.GranjaId ?? 0)
        }));
        this.allGalpones = galpones.map(g => ({
          galponId: String(g.galponId ?? g.GalponId ?? '').trim(),
          galponNombre: g.galponNombre ?? g.GalponNombre,
          nucleoId: String(g.nucleoId ?? g.NucleoId ?? '').trim(),
          granjaId: Number(g.granjaId ?? g.GranjaId ?? 0)
        }));
        this.allLotes = lotes.map(l => ({
          loteAveEngordeId: Number(l['loteAveEngordeId'] ?? l['LoteAveEngordeId'] ?? 0),
          loteNombre: String(l['loteNombre'] ?? l['LoteNombre'] ?? ''),
          granjaId: Number(l['granjaId'] ?? l['GranjaId'] ?? 0),
          nucleoId: (l['nucleoId'] ?? l['NucleoId'] ?? null) as string | null,
          galponId: (l['galponId'] ?? l['GalponId'] ?? null) as string | null,
          fechaEncaset: (l['fechaEncaset'] ?? l['FechaEncaset'] ?? null) as string | null
        }));
        this.loadingFilterData = false;
      },
      error: () => {
        this.farms = []; this.allNucleos = []; this.allGalpones = []; this.allLotes = [];
        this.loadingFilterData = false;
      }
    });
  }

  /** true cuando hay exactamente una granja seleccionada → habilita cascada núcleo/galpón/lote. */
  get granjaUnica(): number | null {
    if (this.todasLasGranjas) return null;
    return this.granjasSeleccionadas.size === 1 ? [...this.granjasSeleccionadas][0] : null;
  }

  onTodasGranjasChange(): void {
    if (this.todasLasGranjas) this.granjasSeleccionadas.clear();
    this.resetCascada();
  }

  toggleGranja(id: number): void {
    if (this.granjasSeleccionadas.has(id)) this.granjasSeleccionadas.delete(id);
    else this.granjasSeleccionadas.add(id);
    if (this.granjasSeleccionadas.size > 0) this.todasLasGranjas = false;
    this.resetCascada();
  }

  granjaChecked(id: number): boolean {
    return this.granjasSeleccionadas.has(id);
  }

  private resetCascada(): void {
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.applyCascade();
  }

  private applyCascade(): void {
    const gid = this.granjaUnica;
    if (gid == null) { this.nucleos = []; this.galpones = []; this.lotes = []; return; }
    this.nucleos = this.allNucleos.filter(n => n.granjaId === gid);
    if (!this.selectedNucleoId) {
      this.galpones = this.allGalpones.filter(g => g.granjaId === gid);
      this.lotes = this.allLotes.filter(l => l.granjaId === gid);
      return;
    }
    const nid = String(this.selectedNucleoId).trim();
    this.galpones = this.allGalpones.filter(g => g.granjaId === gid && String(g.nucleoId).trim() === nid);
    this.lotes = this.allLotes.filter(l => l.granjaId === gid && String(l.nucleoId || '').trim() === nid);
    if (!this.selectedGalponId) return;
    const gpid = String(this.selectedGalponId).trim();
    this.lotes = this.lotes.filter(l => String(l.galponId || '').trim() === gpid);
  }

  onNucleoChange(): void { this.selectedGalponId = null; this.selectedLoteId = null; this.applyCascade(); }
  onGalponChange(): void { this.selectedLoteId = null; this.applyCascade(); }

  async generar(): Promise<void> {
    this.loading = true;
    this.error = null;
    this.reporte = null;
    try {
      const granjaIds = this.todasLasGranjas || this.granjasSeleccionadas.size === 0
        ? null
        : [...this.granjasSeleccionadas];
      const gUnica = this.granjaUnica;
      const request: InformeSemanalRequest = {
        granjaIds,
        nucleoId: gUnica != null ? (this.selectedNucleoId || null) : null,
        galponId: gUnica != null ? (this.selectedGalponId || null) : null,
        loteId: gUnica != null && this.selectedLoteId ? Number(this.selectedLoteId) : null,
        fechaDesde: this.fechaDesde || null,
        fechaHasta: this.fechaHasta || null
      };
      this.reporte = await firstValueFrom(this.service.generar(request));
    } catch (err: any) {
      this.error = err?.error?.message || err?.error?.error || err?.message || 'Error al generar el informe semanal';
      this.reporte = null;
    } finally {
      this.loading = false;
    }
  }

  limpiar(): void {
    this.todasLasGranjas = true;
    this.granjasSeleccionadas.clear();
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.establecerSemanaPorDefecto();
    this.applyCascade();
    this.reporte = null;
    this.error = null;
  }

  // ── Formato ────────────────────────────────────────────────────────────
  num(v: number | null | undefined, d = 1): string {
    if (v == null) return '—';
    return v.toLocaleString('es-PA', { minimumFractionDigits: d, maximumFractionDigits: d });
  }
  ent(v: number | null | undefined): string {
    if (v == null) return '—';
    return Math.round(v).toLocaleString('es-PA');
  }
  pct(v: number | null | undefined): string {
    if (v == null) return '—';
    return `${v.toFixed(2)}%`;
  }

  trackSemana = (_: number, g: { semana: number }) => g.semana;
  trackFila = (_: number, f: InformeSemanalFila) => f.loteAveEngordeId;

  // ── Exportar Excel (una hoja por semana de vida) ───────────────────────
  exportarExcel(): void {
    if (!this.reporte || this.reporte.semanas.length === 0) return;
    const wb = XLSX.utils.book_new();
    for (const g of this.reporte.semanas) {
      const aoa: (string | number)[][] = [];
      aoa.push([`INFORME SEMANAL POLLO DE ENGORDE - PANAMÁ — SEMANA N. ${g.semana}`]);
      aoa.push([
        'GRANJA', 'GALPÓN', 'AVES', 'SEMANA',
        'CONSUMO Tabla', 'CONSUMO Real', 'PESO Tabla', 'PESO Real',
        'GANANCIA Tabla', 'GANANCIA Real', 'CONVERSIÓN Tabla', 'CONVERSIÓN Real',
        'MORT. TABLA', 'MORT. NATURAL', 'SELECCIÓN', 'MORT. TOTAL',
        'VENTAS (un)', 'VENTAS (kg)', 'AGUA (ml)', 'RELACIÓN'
      ]);
      for (const f of g.filas) {
        aoa.push([
          f.loteNombre, f.galponNombre ?? f.galponId ?? '—', f.avesEncasetadas, f.semana,
          f.consumoTablaG ?? '—', round(f.consumoRealGAve, 1),
          f.pesoTablaG ?? '—', f.pesoRealG == null ? '—' : round(f.pesoRealG, 1),
          f.gananciaTablaG ?? '—', f.gananciaRealG == null ? '—' : round(f.gananciaRealG, 1),
          f.conversionTabla ?? '—', f.conversionReal == null ? '—' : round(f.conversionReal, 3),
          f.mortalidadTablaPct ?? '—', round(f.mortNaturalPct, 2), round(f.seleccionPct, 2), round(f.mortalidadTotalPct, 2),
          f.ventasUnid, round(f.ventasKg, 1), round(f.aguaMl, 1), f.relacionAgua == null ? '—' : round(f.relacionAgua, 3)
        ]);
      }
      const c = g.consolidado;
      aoa.push([
        'CONSOLIDADO', '', c.avesTotales, g.semana,
        '—', round(c.consumoRealGAveProm, 1),
        '—', c.pesoRealGProm == null ? '—' : round(c.pesoRealGProm, 1),
        '—', c.gananciaRealGProm == null ? '—' : round(c.gananciaRealGProm, 1),
        '—', c.conversionRealProm == null ? '—' : round(c.conversionRealProm, 3),
        '—', round(c.mortNaturalPctProm, 2), round(c.seleccionPctProm, 2), round(c.mortalidadTotalPctProm, 2),
        c.ventasUnidTotal, round(c.ventasKgTotal, 1), '', ''
      ]);
      const ws = XLSX.utils.aoa_to_sheet(aoa);
      XLSX.utils.book_append_sheet(wb, ws, `Semana ${g.semana}`);
    }
    const yyyymmdd = new Date().toISOString().slice(0, 10).replace(/-/g, '');
    XLSX.writeFile(wb, `Informe_Semanal_Engorde_${yyyymmdd}.xlsx`);
  }
}

function round(v: number | null | undefined, d: number): number | string {
  if (v == null) return '—';
  const f = Math.pow(10, d);
  return Math.round(v * f) / f;
}

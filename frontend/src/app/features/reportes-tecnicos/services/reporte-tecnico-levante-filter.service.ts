// src/app/features/reportes-tecnicos/services/reporte-tecnico-levante-filter.service.ts
import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { finalize } from 'rxjs';
import { environment } from '../../../../environments/environment';

// ─── Interfaces públicas ───────────────────────────────────────────────────────

export interface LevanteFilterFarm {
  id: number;
  name: string;
}

export interface LevanteFilterNucleo {
  nucleoId: string;
  nucleoNombre: string;
  granjaId: number;
}

export interface LevanteFilterGalpon {
  galponId: string;
  galponNombre: string;
  nucleoId: string;
  granjaId: number;
}

/** Registro de lote_postura_levante o lote_postura_produccion (normalizado) */
export interface LevanteFilterLote {
  loteId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId: string | null;
  galponId: string | null;
  loteErp?: string | null;
  estadoOperativoLote?: string | null;
  lotePosturaLevantePadreId?: number | null;
  lotePosturaBaseId?: number | null;
}

/** Registro de lote_postura_base */
export interface LevanteFilterLoteBase {
  lotePosturaBaseId: number;
  loteNombre: string;
  codigoErp?: string | null;
}

/** Item unificado para el dropdown Paso 4 (Lote Base en LEVANTE / Lote Base en PRODUCCIÓN) */
export interface FiltroLoteItem {
  id: number;
  nombre: string;
}

/** Estructura normalizada usada internamente (misma forma para ambas etapas) */
export interface LevanteFilterData {
  farms: LevanteFilterFarm[];
  nucleos: LevanteFilterNucleo[];
  galpones: LevanteFilterGalpon[];
  lotes: LevanteFilterLote[];
  lotesBase: LevanteFilterLoteBase[];
}

// ─── Servicio ─────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class ReporteTecnicoLevanteFilterService {
  private readonly http = inject(HttpClient);

  private readonly URLS = {
    LEVANTE:    `${environment.apiUrl}/ReporteTecnico/levante/filter-data`,
    PRODUCCION: `${environment.apiUrl}/ReporteTecnicoProduccion/filter-data`
  };

  private _data = signal<LevanteFilterData | null>(null);

  readonly loading    = signal(false);
  readonly errorCarga = signal<string | null>(null);

  // ── PASO 0: Etapa/Fase ───────────────────────────────────────────────────────
  readonly selectedEtapa = signal<'LEVANTE' | 'PRODUCCION'>('LEVANTE');

  // ── PASOS 1-3: Ubicación ─────────────────────────────────────────────────────
  readonly selectedGranjaId = signal<number | null>(null);
  readonly selectedNucleoId = signal<string | null>(null);
  readonly selectedGalponId = signal<string | null>(null);

  // ── PASO 4: Lote Base (levante y producción) ─────────────────────────────────
  readonly selectedLoteBaseId = signal<number | null>(null);

  // ── PASO 6: Sublote (LEVANTE) / Lote de Producción (PRODUCCIÓN) ─────────────
  readonly selectedSubloteId = signal<number | null>(null);

  // ── PASO 7: Periodicidad ─────────────────────────────────────────────────────
  readonly selectedPeriodicidad = signal<'Semanal' | 'Diario'>('Semanal');

  // ── Listas derivadas ─────────────────────────────────────────────────────────

  readonly granjas = computed<LevanteFilterFarm[]>(() => this._data()?.farms ?? []);

  readonly nucleosFiltrados = computed<LevanteFilterNucleo[]>(() => {
    const d   = this._data();
    const gid = this.selectedGranjaId();
    if (!d || !gid) return [];
    return d.nucleos.filter(n => n.granjaId === gid);
  });

  readonly galponesFiltrados = computed<LevanteFilterGalpon[]>(() => {
    const d   = this._data();
    const gid = this.selectedGranjaId();
    const nid = this.selectedNucleoId();
    if (!d || !gid) return [];
    return d.galpones
      .filter(g => g.granjaId === gid && (!nid || g.nucleoId === nid))
      .sort((a, b) =>
        a.galponNombre.localeCompare(b.galponNombre, 'es', { numeric: true, sensitivity: 'base' })
      );
  });

  /** PASO 4 — Lote Base unificado: para LEVANTE y PRODUCCIÓN muestra lote_postura_base */
  readonly lotesPaso4 = computed<FiltroLoteItem[]>(() => {
    const d     = this._data();
    const gid   = this.selectedGranjaId();
    const nid   = this.selectedNucleoId();
    const galId = this.selectedGalponId();
    if (!d || !gid) return [];

    // Para ambas etapas: buscar lote_postura_base a través de los lotes en esta ubicación
    let lotesUbicacion = d.lotes.filter(l => l.granjaId === gid);
    if (nid)   lotesUbicacion = lotesUbicacion.filter(l => l.nucleoId === nid);
    if (galId) lotesUbicacion = lotesUbicacion.filter(l => l.galponId === galId);

    const baseIds = new Set(
      lotesUbicacion.map(l => l.lotePosturaBaseId).filter((id): id is number => id != null)
    );

    return d.lotesBase
      .filter(lb => baseIds.has(lb.lotePosturaBaseId))
      .map(lb => ({ id: lb.lotePosturaBaseId, nombre: lb.loteNombre }))
      .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es', { numeric: true, sensitivity: 'base' }));
  });

  /** PASO 6 — Sublotes (LEVANTE) o Lotes de Producción (PRODUCCIÓN) bajo el lote base seleccionado */
  readonly sublotesFiltrados = computed<LevanteFilterLote[]>(() => {
    const d      = this._data();
    const baseId = this.selectedLoteBaseId();
    const gid    = this.selectedGranjaId();
    const nid    = this.selectedNucleoId();
    const galId  = this.selectedGalponId();
    if (!d || !baseId || !gid) return [];

    let lotes = d.lotes.filter(l => l.lotePosturaBaseId === baseId && l.granjaId === gid);
    if (nid)   lotes = lotes.filter(l => l.nucleoId === nid);
    if (galId) lotes = lotes.filter(l => l.galponId === galId);

    return lotes.sort((a, b) =>
      a.loteNombre.localeCompare(b.loteNombre, 'es', { numeric: true, sensitivity: 'base' })
    );
  });

  // ── Computeds de nombres seleccionados ───────────────────────────────────────

  readonly selectedLoteNombre = computed<string>(() => {
    const id    = this.selectedLoteBaseId();
    const items = this.lotesPaso4();
    return items.find(i => i.id === id)?.nombre ?? '';
  });

  readonly selectedSublote = computed<LevanteFilterLote | null>(() => {
    const sid = this.selectedSubloteId();
    const d   = this._data();
    if (!sid || !d) return null;
    return d.lotes.find(l => l.loteId === sid) ?? null;
  });

  readonly selectedSubloteNombre = computed<string>(
    () => this.selectedSublote()?.loteNombre ?? ''
  );

  readonly selectedGranjaNombre = computed<string>(() => {
    const gid = this.selectedGranjaId();
    return this.granjas().find(g => g.id === gid)?.name ?? '';
  });

  readonly selectedNucleoNombre = computed<string>(() => {
    const nid = this.selectedNucleoId();
    return this.nucleosFiltrados().find(n => n.nucleoId === nid)?.nucleoNombre ?? '';
  });

  readonly selectedGalponNombre = computed<string>(() => {
    const galId = this.selectedGalponId();
    return this.galponesFiltrados().find(g => g.galponId === galId)?.galponNombre ?? '';
  });

  /** selectedLoteBaseId ES el lotePosturaBaseId para ambas etapas */
  readonly selectedLotePosturaBaseId = computed<number | null>(
    () => this.selectedLoteBaseId()
  );

  readonly dataCargada = computed(() => this._data() !== null);

  // ── Carga de datos ────────────────────────────────────────────────────────────

  loadFilterData(): void {
    if (this._data() !== null) return;
    this._cargarDatos();
  }

  reloadFilterData(): void {
    this._cargarDatos();
  }

  private _cargarDatos(): void {
    this.loading.set(true);
    this.errorCarga.set(null);

    const url = this.selectedEtapa() === 'LEVANTE' ? this.URLS.LEVANTE : this.URLS.PRODUCCION;

    // Ambas etapas retornan LevanteFilterData (LoteReproductoraFilterDataDto shape)
    this.http.get<LevanteFilterData>(url)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => this._data.set(data),
        error: () => this.errorCarga.set(
          `No se pudieron cargar los filtros de ${this.selectedEtapa() === 'LEVANTE' ? 'Levante' : 'Producción'}.`
        )
      });
  }

  // ── Setters con reset en cascada ─────────────────────────────────────────────

  setEtapa(etapa: 'LEVANTE' | 'PRODUCCION'): void {
    if (this.selectedEtapa() === etapa) return;
    this.selectedEtapa.set(etapa);
    this._resetToGranja();
    this._data.set(null);
    this._cargarDatos();
  }

  setGranja(id: number | null): void {
    this.selectedGranjaId.set(id);
    this.selectedNucleoId.set(null);
    this.selectedGalponId.set(null);
    this.selectedLoteBaseId.set(null);
    this.selectedSubloteId.set(null);
  }

  setNucleo(id: string | null): void {
    this.selectedNucleoId.set(id);
    this.selectedGalponId.set(null);
    this.selectedLoteBaseId.set(null);
    this.selectedSubloteId.set(null);
  }

  setGalpon(id: string | null): void {
    this.selectedGalponId.set(id);
    this.selectedLoteBaseId.set(null);
    this.selectedSubloteId.set(null);
  }

  setLoteBase(id: number | null): void {
    this.selectedLoteBaseId.set(id);
    this.selectedSubloteId.set(null);
  }

  setSublote(id: number | null): void {
    this.selectedSubloteId.set(id);
  }

  setPeriodicidad(p: 'Semanal' | 'Diario'): void {
    this.selectedPeriodicidad.set(p);
  }

  private _resetToGranja(): void {
    this.selectedGranjaId.set(null);
    this.selectedNucleoId.set(null);
    this.selectedGalponId.set(null);
    this.selectedLoteBaseId.set(null);
    this.selectedSubloteId.set(null);
  }

  resetSeleccion(): void {
    this._resetToGranja();
  }
}

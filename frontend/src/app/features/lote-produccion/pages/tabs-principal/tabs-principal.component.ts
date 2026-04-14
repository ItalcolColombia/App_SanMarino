import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto, ProduccionLoteDetalleDto, InformacionLoteDto } from '../../services/produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { LotePosturaProduccionFilterItem } from '../filtro-select/filtro-select.component';
// Usar versión "components" que trae indicadores desde backend en 1 sola petición
import { TablaListaIndicadoresComponent } from '../../components/tabla-lista-indicadores/tabla-lista-indicadores.component';
import { GraficasPrincipalComponent } from '../graficas-principal/graficas-principal.component';
import { CatalogoAlimentosService, CatalogItemDto } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { catchError, of } from 'rxjs';
import * as XLSX from 'xlsx';

@Component({
  selector: 'app-tabs-principal',
  standalone: true,
  imports: [CommonModule, TablaListaIndicadoresComponent, GraficasPrincipalComponent],
  templateUrl: './tabs-principal.component.html',
  styleUrls: ['./tabs-principal.component.scss']
})
export class TabsPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() produccionLote: ProduccionLoteDetalleDto | null = null;
  /** ID del lote en fase Producción (hijo o mismo). Mismo que usa listado y modal de seguimiento diario. */
  @Input() produccionLoteId: number | null = null;
  /** Lote postura producción seleccionado (flujo LPP). Incluye aves, estado. */
  @Input() selectedLoteLPP: LotePosturaProduccionFilterItem | null = null;
  /** Información general del lote (nuevo endpoint). */
  @Input() informacionLote: InformacionLoteDto | null = null;
  /** ID del lote postura producción (flujo LPP). Se pasa a indicadores/gráfica. */
  get lotePosturaProduccionId(): number | null {
    return this.selectedLoteLPP?.lotePosturaProduccionId ?? null;
  }
  @Input() loading: boolean = false;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoItemDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoItemDto>();

  activeTab: 'general' | 'indicadores' | 'grafica' = 'general';

  private readonly catalogSvc = inject(CatalogoAlimentosService);
  private readonly catalogNameById = new Map<number, string>();
  private readonly catalogFetchInFlight = new Set<number>();

  constructor() { }

  ngOnInit(): void {
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos']) {
      this.preloadCatalogNamesFromSeguimientos();
    }
  }

  // ================== EVENTOS ==================
  onTabChange(tab: 'general' | 'indicadores' | 'grafica'): void {
    this.activeTab = tab;
  }

  onCreate(): void {
    this.create.emit();
  }

  onEdit(seg: SeguimientoItemDto): void {
    this.edit.emit(seg);
  }

  onDelete(id: number): void {
    this.delete.emit(id);
  }

  onViewDetail(seg: SeguimientoItemDto): void {
    this.viewDetail.emit(seg);
  }

  /** Fecha base para calcular edad (encaset desde Lote Levante). */
  getFechaBaseEdad(): string | Date | null {
    if (this.selectedLoteLPP?.fechaEncaset) return this.selectedLoteLPP.fechaEncaset;
    if (this.produccionLote?.fechaInicio) return this.produccionLote.fechaInicio;
    return this.selectedLote?.fechaEncaset ?? null;
  }

  /** Edad del lote en semanas (desde fecha base hasta hoy). Producción: semana >= 26. */
  getEdadSemanas(): number | string {
    if (this.informacionLote) return this.informacionLote.edadSemanasProduccion;
    const base = this.getFechaBaseEdad();
    if (!base) return '—';
    const dias = this.calcularEdadDiasDesdeFecha(base, new Date().toISOString());
    return Math.floor(dias / 7) + 1;
  }

  /** Edad en días desde fecha base hasta fechaRegistro. */
  calcularEdadDias(fechaRegistro: string | Date): number {
    const base = this.getFechaBaseEdad();
    if (!base) return 0;
    return this.calcularEdadDiasDesdeFecha(base, fechaRegistro);
  }

  private calcularEdadDiasDesdeFecha(fechaBase: string | Date, fechaReg: string | Date): number {
    const inicio = new Date(fechaBase);
    const reg = new Date(fechaReg);
    const diffTime = reg.getTime() - inicio.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    return Math.max(0, diffDays);
  }

  /** Edad en semanas para un registro (días desde encaset → semanas: floor(días/7)+1, prod ≥ 26). */
  calcularEdadSemanas(fechaRegistro: string | Date): number {
    const dias = this.calcularEdadDias(fechaRegistro);
    return Math.max(26, Math.floor(dias / 7) + 1);
  }

  // ================== CALCULOS ==================
  getTotalHuevos(): number {
    return this.seguimientos.reduce((total, seg) => total + (seg.huevosTotales || 0), 0);
  }

  // ================== EXPORT EXCEL (SEGUIMIENTO) ==================
  private sanitizeFilePart(s: string): string {
    return (s || '')
      .trim()
      .replace(/[\\/:*?"<>|]+/g, '-')
      .replace(/\s+/g, ' ')
      .slice(0, 120);
  }

  private getLoteNombreForExport(): string {
    return this.sanitizeFilePart(this.selectedLote?.loteNombre || `Lote_${this.selectedLote?.loteId ?? ''}`) || 'lote';
  }

  private getMaxSemanaEdadFromSeguimientos(): number | null {
    if (!this.seguimientos?.length) return null;
    const base = this.selectedLoteLPP?.fechaEncaset || this.selectedLote?.fechaEncaset || null;
    if (!base) return null;
    const enc = new Date(base as any);
    if (isNaN(enc.getTime())) return null;
    let max = 0;
    for (const s of this.seguimientos) {
      const d = new Date(s.fechaRegistro as any);
      if (isNaN(d.getTime())) continue;
      const diffDays = Math.floor((d.getTime() - enc.getTime()) / (1000 * 60 * 60 * 24));
      const semanaVida = Math.floor(diffDays / 7) + 1;
      if (semanaVida > max) max = semanaVida;
    }
    return max > 0 ? max : null;
  }

  descargarSeguimientoExcel(): void {
    const loteNombre = this.getLoteNombreForExport();
    const semana = this.getMaxSemanaEdadFromSeguimientos() ?? 0;
    const stamp = new Date().toISOString().slice(0, 10);
    const filename = `produccion-lote-${loteNombre}-tap-seguimiento-semana-${semana || 'NA'}-${stamp}.xlsx`;

    const rows = (this.seguimientos || []).map(s => ({
      Id: s.id,
      Fecha: new Date(s.fechaRegistro as any).toISOString().slice(0, 10),
      SemanaEdad: this.calcularEdadSemanas(s.fechaRegistro),
      Etapa: s.etapa,
      MortalidadH: s.mortalidadH,
      MortalidadM: s.mortalidadM,
      SeleccionH: s.selH,
      SeleccionM: s.selM,
      ConsKgH: s.consKgH,
      ConsKgM: s.consKgM,
      TipoItemH: this.getTipoItemH(s),
      TipoItemM: this.getTipoItemM(s),
      AlimentoH: this.getTipoAlimentoH(s),
      AlimentoM: this.getTipoAlimentoM(s),
      ConsumoOriginalH: this.getConsumoOriginalH(s),
      UnidadH: this.getUnidadConsumoOriginalH(s),
      ConsumoOriginalM: this.getConsumoOriginalM(s),
      UnidadM: this.getUnidadConsumoOriginalM(s),
      HuevosTotales: s.huevosTotales,
      HuevosIncubables: s.huevosIncubables,
      HuevoLimpio: (s as any).huevoLimpio ?? 0,
      HuevoTratado: (s as any).huevoTratado ?? 0,
      HuevoSucio: (s as any).huevoSucio ?? 0,
      HuevoDeforme: (s as any).huevoDeforme ?? 0,
      HuevoBlanco: (s as any).huevoBlanco ?? 0,
      HuevoDobleYema: (s as any).huevoDobleYema ?? 0,
      HuevoPiso: (s as any).huevoPiso ?? 0,
      HuevoPequeno: (s as any).huevoPequeno ?? 0,
      HuevoRoto: (s as any).huevoRoto ?? 0,
      HuevoDesecho: (s as any).huevoDesecho ?? 0,
      HuevoOtro: (s as any).huevoOtro ?? 0,
      PesoHuevo: s.pesoHuevo,
      PesoH: (s as any).pesoH ?? null,
      PesoM: (s as any).pesoM ?? null,
      Uniformidad: (s as any).uniformidad ?? null,
      CoeficienteVariacion: (s as any).coeficienteVariacion ?? null,
      ObservacionesPesaje: (s as any).observacionesPesaje ?? null
    }));

    const ws = XLSX.utils.json_to_sheet(rows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Seguimiento');
    XLSX.writeFile(wb, filename);
  }

  // ================== METADATA (compat legacy + nuevo) ==================
  private meta(s: SeguimientoItemDto): any | null {
    return (s as any)?.metadata ?? null;
  }

  private normalizeItems(arr: any): Array<{ tipoItem?: string; catalogItemId?: number; cantidad?: number; unidad?: string }> {
    if (!Array.isArray(arr)) return [];
    return arr
      .map(x => ({
        tipoItem: x?.tipoItem ?? x?.tipo_item,
        catalogItemId: Number(x?.catalogItemId ?? x?.catalog_item_id) || undefined,
        cantidad: typeof x?.cantidad === 'number' ? x.cantidad : Number(x?.cantidad) || undefined,
        unidad: x?.unidad ?? x?.unit ?? undefined
      }))
      .filter(x => (x.catalogItemId && x.catalogItemId > 0) || (x.cantidad != null && x.cantidad > 0));
  }

  private getNombreCatalogItem(id: number | null | undefined): string | null {
    if (!id || id <= 0) return null;
    return this.catalogNameById.get(id) ?? null;
  }

  private ensureCatalogItemFetched(id: number | null | undefined): void {
    if (!id || id <= 0) return;
    if (this.catalogNameById.has(id) || this.catalogFetchInFlight.has(id)) return;
    this.catalogFetchInFlight.add(id);
    this.catalogSvc.getById(id).pipe(
      catchError(() => of(null as unknown as CatalogItemDto))
    ).subscribe({
      next: (dto) => {
        const name = (dto as any)?.nombre ? String((dto as any).nombre).trim() : '';
        if (name) this.catalogNameById.set(id, name);
      },
      complete: () => this.catalogFetchInFlight.delete(id)
    });
  }

  private preloadCatalogNamesFromSeguimientos(): void {
    const ids = new Set<number>();
    for (const s of this.seguimientos || []) {
      const m = this.meta(s);
      // Nuevo: items por sexo
      const itemsH = this.normalizeItems(m?.itemsHembras ?? m?.items_hembras);
      const itemsM = this.normalizeItems(m?.itemsMachos ?? m?.items_machos);
      for (const it of [...itemsH, ...itemsM]) {
        if (it.catalogItemId && it.catalogItemId > 0) ids.add(it.catalogItemId);
      }
      // Viejo: ids de tipoAlimento por sexo
      const idH = Number(m?.tipoAlimentoHembras ?? m?.tipo_alimento_hembras) || 0;
      const idM = Number(m?.tipoAlimentoMachos ?? m?.tipo_alimento_machos) || 0;
      if (idH > 0) ids.add(idH);
      if (idM > 0) ids.add(idM);
    }
    ids.forEach(id => this.ensureCatalogItemFetched(id));
  }

  getTipoItemH(s: SeguimientoItemDto): string {
    const m = this.meta(s);
    // Nuevo: si hay items por sexo, el tipo se infiere como "alimento" (o el primero)
    const itemsH = this.normalizeItems(m?.itemsHembras ?? m?.items_hembras);
    if (itemsH.length) return String(itemsH[0].tipoItem || 'alimento');
    return (m?.tipoItemHembras ?? m?.tipo_item_hembras ?? '—') || '—';
  }

  getTipoItemM(s: SeguimientoItemDto): string {
    const m = this.meta(s);
    const itemsM = this.normalizeItems(m?.itemsMachos ?? m?.items_machos);
    if (itemsM.length) return String(itemsM[0].tipoItem || 'alimento');
    return (m?.tipoItemMachos ?? m?.tipo_item_machos ?? '—') || '—';
  }

  getTipoAlimentoH(s: SeguimientoItemDto): string {
    const m = this.meta(s);
    // Nuevo: itemsHembras -> mostrar nombres separados
    const itemsH = this.normalizeItems(m?.itemsHembras ?? m?.items_hembras);
    if (itemsH.length) {
      return itemsH.map(it => {
        const id = it.catalogItemId;
        this.ensureCatalogItemFetched(id);
        const name = this.getNombreCatalogItem(id) ?? (id ? `ID ${id}` : '—');
        const qty = it.cantidad != null ? it.cantidad : null;
        const u = (it.unidad || 'kg').toString();
        return qty != null ? `${name} (${qty} ${u})` : name;
      }).join(' / ');
    }

    // Viejo: tipoAlimentoHembras -> resolver nombre por catálogo si se puede
    const id = Number(m?.tipoAlimentoHembras ?? m?.tipo_alimento_hembras) || 0;
    if (id > 0) {
      this.ensureCatalogItemFetched(id);
      return this.getNombreCatalogItem(id) ?? `${(s?.tipoAlimento || '').trim() || '—'} (ID ${id})`;
    }
    return (s?.tipoAlimento || '').trim() || '—';
  }

  getTipoAlimentoM(s: SeguimientoItemDto): string {
    const m = this.meta(s);
    const itemsM = this.normalizeItems(m?.itemsMachos ?? m?.items_machos);
    if (itemsM.length) {
      return itemsM.map(it => {
        const id = it.catalogItemId;
        this.ensureCatalogItemFetched(id);
        const name = this.getNombreCatalogItem(id) ?? (id ? `ID ${id}` : '—');
        const qty = it.cantidad != null ? it.cantidad : null;
        const u = (it.unidad || 'kg').toString();
        return qty != null ? `${name} (${qty} ${u})` : name;
      }).join(' / ');
    }

    const id = Number(m?.tipoAlimentoMachos ?? m?.tipo_alimento_machos) || 0;
    if (id > 0) {
      this.ensureCatalogItemFetched(id);
      return this.getNombreCatalogItem(id) ?? `${(s?.tipoAlimento || '').trim() || '—'} (ID ${id})`;
    }
    return (s?.tipoAlimento || '').trim() || '—';
  }

  getConsumoOriginalH(s: SeguimientoItemDto): number {
    const m = this.meta(s);
    const v = m?.consumoOriginalHembras ?? m?.consumo_original_hembras ?? null;
    return typeof v === 'number' ? v : (s?.consKgH ?? 0);
  }

  getConsumoOriginalM(s: SeguimientoItemDto): number {
    const m = this.meta(s);
    const v = m?.consumoOriginalMachos ?? m?.consumo_original_machos ?? null;
    return typeof v === 'number' ? v : (s?.consKgM ?? 0);
  }

  getUnidadConsumoOriginalH(s: SeguimientoItemDto): string {
    const m = this.meta(s);
    return (m?.unidadConsumoOriginalHembras ?? m?.unidad_consumo_original_hembras ?? 'kg') || 'kg';
  }

  getUnidadConsumoOriginalM(s: SeguimientoItemDto): string {
    const m = this.meta(s);
    return (m?.unidadConsumoOriginalMachos ?? m?.unidad_consumo_original_machos ?? 'kg') || 'kg';
  }
}




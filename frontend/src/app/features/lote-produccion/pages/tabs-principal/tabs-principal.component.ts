import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto, ProduccionLoteDetalleDto, InformacionLoteDto, leerHuevoItemsDeMetadata } from '../../services/produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { LotePosturaProduccionFilterItem } from '../filtro-select/filtro-select.component';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { ResumenHuevoPorTipo } from '../../models/huevo-clasificacion.model';
import { resumirHuevoItemsPorTipo } from '../../funciones/resumir-huevo-items-por-tipo.funcion';
// Usar versión "components" que trae indicadores desde backend en 1 sola petición
import { TablaListaIndicadoresComponent } from '../../components/tabla-lista-indicadores/tabla-lista-indicadores.component';
import { GraficasPrincipalComponent } from '../graficas-principal/graficas-principal.component';
import { CatalogoAlimentosService, CatalogItemDto } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { catchError, of } from 'rxjs';
import { exportarObjetosExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { EdadesLoteComponent } from '../../../traslados-aves/components/edades-lote/edades-lote.component';

@Component({
  selector: 'app-tabs-principal',
  standalone: true,
  imports: [CommonModule, TablaListaIndicadoresComponent, GraficasPrincipalComponent, EdadesLoteComponent],
  templateUrl: './tabs-principal.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabs-principal.component.scss']
})
export class TabsPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() produccionLote: ProduccionLoteDetalleDto | null = null;
  /** ID del lote en fase Producción (hijo o mismo). Mismo que usa listado y modal de seguimiento diario. */
  @Input() produccionLoteId: number | null = null;
  /** ID del lote postura producción (flujo LPP). Necesario para los indicadores/gráfica por semana de vida. */
  @Input() lotePosturaProduccionId: number | null = null;
  /** Lote postura producción seleccionado (flujo LPP). Incluye aves, estado. */
  @Input() selectedLoteLPP: LotePosturaProduccionFilterItem | null = null;
  /** Información general del lote (nuevo endpoint). */
  @Input() informacionLote: InformacionLoteDto | null = null;
  @Input() loading: boolean = false;
  /**
   * ID del lote BASE (tabla `lotes`) para el bloque "Edades en el lote" (cohortes).
   * Null (default) ⇒ el bloque no se muestra ni consulta.
   */
  @Input() loteIdCohortes: number | null = null;
  /** Incrementar para refrescar las cohortes sin cambiar de lote (p. ej. tras un traslado). */
  @Input() cohortesRefreshTrigger = 0;
  /**
   * Lote de producción cerrado ⇒ se ocultan crear, editar y eliminar. El backend bloquea igual
   * (`ProduccionService.EnsureLoteProduccionAbiertoAsync`); esto evita ofrecer acciones que fallarían.
   */
  @Input() loteCerrado = false;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoItemDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoItemDto>();

  activeTab: 'general' | 'indicadores' | 'grafica' = 'general';

  private readonly catalogSvc = inject(CatalogoAlimentosService);
  private readonly catalogNameById = new Map<number, string>();
  private readonly catalogFetchInFlight = new Set<number>();

  // ===== Clasificación de huevos por ÍTEMS (flag de empresa · Santa Reyes) =====
  private readonly companyConfig = inject(ActiveCompanyConfigService);
  /**
   * Flag `companies.clasificacion_huevo_por_items`: las 11 columnas fijas (H. Limpio…H. Otro) y
   * "Huevos Inc." están siempre en 0 para estas empresas → se reemplazan por Primera / Pnc,
   * calculadas desde `metadata.huevoItems` de cada registro. FAIL-CLOSED: sin flag, grilla clásica.
   */
  clasificacionHuevoPorItems = false;
  /** seguimiento.id → totales Primera/Pnc. Se calcula UNA vez por carga de registros (no por ciclo de CD). */
  private readonly huevoPorTipoPorRegistro = new Map<number, ResumenHuevoPorTipo>();

  constructor() { }

  ngOnInit(): void {
    this.cargarFlagsEmpresa();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos']) {
      this.preloadCatalogNamesFromSeguimientos();
      this.recalcularHuevoPorTipo();
    }
  }

  /** Flags de la empresa activa (emite una vez y completa; caché por empresa). */
  private cargarFlagsEmpresa(): void {
    this.companyConfig.getFlags().subscribe(flags => {
      if (this.clasificacionHuevoPorItems === flags.clasificacionHuevoPorItems) return;
      this.clasificacionHuevoPorItems = flags.clasificacionHuevoPorItems;
      // El flag llega async: si los registros ya estaban cargados, calcular ahora.
      this.recalcularHuevoPorTipo();
    });
  }

  /**
   * Recalcula el mapa id → {primera, pnc} leyendo `metadata.huevoItems` de cada registro.
   * Solo con el flag activo (con el flag apagado el mapa queda vacío y las columnas no se pintan).
   */
  private recalcularHuevoPorTipo(): void {
    this.huevoPorTipoPorRegistro.clear();
    if (!this.clasificacionHuevoPorItems) return;
    for (const s of this.seguimientos || []) {
      const items = leerHuevoItemsDeMetadata(s?.metadata);
      if (!items.length) continue;
      this.huevoPorTipoPorRegistro.set(s.id, resumirHuevoItemsPorTipo(items));
    }
  }

  /** Huevos "Primera" del registro (0 si el registro no trae clasificación por ítems). */
  getHuevoPrimera(s: SeguimientoItemDto): number {
    return this.huevoPorTipoPorRegistro.get(s.id)?.primera ?? 0;
  }

  /** Huevos "Pnc" del registro (0 si el registro no trae clasificación por ítems). */
  getHuevoPnc(s: SeguimientoItemDto): number {
    return this.huevoPorTipoPorRegistro.get(s.id)?.pnc ?? 0;
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

  /**
   * Fecha base para calcular edad (encaset del lote).
   * REQ-012d: informacionLote.fechaEncaset (InformacionLoteDto, flujo LPP) es la fuente confiable —
   * viaja siempre en el header del módulo. Se prioriza sobre selectedLoteLPP (puede venir null desde
   * filter-data). Se ELIMINÓ el fallback a produccionLote.fechaInicio: es la fecha de INICIO DE
   * PRODUCCIÓN, no el encaset — usarla como base de edad daba semanas incorrectas en lotes legacy.
   */
  getFechaBaseEdad(): string | Date | null {
    if (this.informacionLote?.fechaEncaset) return this.informacionLote.fechaEncaset;
    if (this.selectedLoteLPP?.fechaEncaset) return this.selectedLoteLPP.fechaEncaset;
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

  /**
   * REQ-012c: etapa calculada EN VIVO desde la semana de vida real (fuente = getFechaBaseEdad(),
   * ya corregida por REQ-012d), en vez de usar el valor `etapa` almacenado (que se congeló con
   * fechaEncaset=null en flujo LPP). Rangos alineados a la hoja de fórmulas: 26-33→1, 34-50→2, >50→3.
   */
  getEtapaEnVivo(fechaRegistro: string | Date): number {
    const semana = this.calcularEdadSemanas(fechaRegistro);
    if (semana <= 33) return 1;
    if (semana <= 50) return 2;
    return 3;
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
    // REQ-012d: misma prioridad de fuente que getFechaBaseEdad() (informacionLote.fechaEncaset primero).
    const base = this.informacionLote?.fechaEncaset || this.selectedLoteLPP?.fechaEncaset || this.selectedLote?.fechaEncaset || null;
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
      Etapa: this.getEtapaEnVivo(s.fechaRegistro),
      MortalidadH: s.mortalidadH,
      MortalidadM: s.mortalidadM,
      SeleccionH: s.selH,
      SeleccionM: s.selM,
      ConsKgH: s.consKgH,
      ConsKgM: s.consKgM,
      AlimentoH: this.getTipoAlimentoH(s),
      AlimentoM: this.getTipoAlimentoM(s),
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
      ObservacionesPesaje: (s as any).observacionesPesaje ?? null
    }));

    exportarObjetosExcel(rows, { sheetName: 'Seguimiento', filenameFull: filename });
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

  // TK-2026-000023 — se eliminaron getConsumoOriginalH/M y getUnidadConsumoOriginalH/M.
  // Mostraban el consumo «tal como lo tecleó el usuario» desde `metadata`, pero esa clave no
  // existe en NINGUNA de las 604 filas de producción, así que el fallback devolvía consKgH/M:
  // la tabla repetía el mismo kg dos veces. Si algún día se captura en otra unidad, la columna
  // se vuelve a agregar leyendo la metadata (y solo entonces aporta algo).

  // ─── Doble validación ───────────────────────────────────────────────────────
  // La tabla no decide nada: recibe el mapa `seguimientoId → estado` que arma el contenedor desde el
  // backend, que es el único que conoce el flag de la empresa y el plazo.

  /** Flag de la empresa. En false la columna Estado no se muestra y nada cambia. */
  @Input() requiereValidacion = false;
  /** Permiso de validar. Sin él la columna se ve, pero sin el botón. */
  @Input() puedeValidar = false;
  /** seguimientoId → estado. Solo trae los NO validados: lo ausente ya se descontó. */
  @Input() estadoValidacionPorId = new Map<number, string>();

  @Output() validar = new EventEmitter<number>();

  estadoValidacionFila(id: number | null | undefined): string {
    if (id == null) return 'VALIDADO';
    return this.estadoValidacionPorId.get(id) ?? 'VALIDADO';
  }

  etiquetaValidacionFila(id: number | null | undefined): string {
    switch (this.estadoValidacionFila(id)) {
      case 'EN_RETRASO': return 'En retraso';
      case 'PENDIENTE':  return 'Pendiente';
      default:           return 'Validado';
    }
  }

  claseBadgeValidacion(id: number | null | undefined): string {
    switch (this.estadoValidacionFila(id)) {
      case 'EN_RETRASO': return 'badge-validacion badge-validacion--retraso';
      case 'PENDIENTE':  return 'badge-validacion badge-validacion--pendiente';
      default:           return 'badge-validacion badge-validacion--validado';
    }
  }

  /** Clase de la FILA: solo se pinta la vencida, para que el rojo siga significando algo. */
  claseFilaValidacion(id: number | null | undefined): string {
    return this.estadoValidacionFila(id) === 'EN_RETRASO' ? 'fila-validacion--retraso' : '';
  }

  tooltipValidacionFila(id: number | null | undefined): string {
    switch (this.estadoValidacionFila(id)) {
      case 'EN_RETRASO':
        return 'En retraso — superó el plazo de validación. Mientras no se valide, el lote no acepta días nuevos.';
      case 'PENDIENTE':
        return 'Pendiente de validar — el alimento y las aves están separados, todavía no descontados. Se puede editar y eliminar.';
      default:
        return 'Validado — el alimento y las aves ya se descontaron. El registro es de solo lectura.';
    }
  }

  puedeValidarFila(id: number | null | undefined): boolean {
    return this.puedeValidar && this.estadoValidacionFila(id) !== 'VALIDADO';
  }

  /** Un registro validado es de solo lectura: hay que quitarle la validación para corregirlo. */
  esSoloLecturaPorValidacion(id: number | null | undefined): boolean {
    return this.requiereValidacion && this.estadoValidacionFila(id) === 'VALIDADO';
  }

  onValidar(id: number | null | undefined): void {
    if (id == null) return;
    this.validar.emit(id);
  }
}




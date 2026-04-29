import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { finalize } from 'rxjs/operators';
import * as XLSX from 'xlsx';
import {
  MovimientoPolloEngordeService,
  MovimientoPolloEngordeDto,
  ResumenAvesLoteDto,
  AuditoriaVentasEngordeResponse,
  CorregirVentasCompletadasResponse,
  OrganizarPesoResponse
} from '../../services/movimiento-pollo-engorde.service';
import { FarmDto } from '../../../farm/services/farm.service';
import { NucleoDto } from '../../../lote-produccion/services/nucleo.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { LoteEngordeService, LoteAveEngordeDto } from '../../../lote-engorde/services/lote-engorde.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import { AuditoriaVentasModalComponent } from '../../components/auditoria-ventas-modal/auditoria-ventas-modal.component';
import {
  ModalMovimientoPolloEngordeComponent,
  MovimientoPolloEngordeSaveDetail
} from '../../components/modal-movimiento-pollo-engorde/modal-movimiento-pollo-engorde.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';

/** Opción del dropdown Lote (solo Ave Engorde). */
export interface LoteOption {
  value: string; // "ae-123"
  tipo: 'ae';
  id: number;
  label: string;
}

/** Fila agrupada: varios movimientos de venta con el mismo número de despacho (mismo viaje). */
export interface FilaDespachoGrupo {
  kind: 'despacho-grupo';
  clave: string;
  numeroDespacho: string;
  fechaMovimiento: string;
  movimientos: MovimientoPolloEngordeDto[];
}

export interface FilaMovimientoSimple {
  kind: 'simple';
  movimiento: MovimientoPolloEngordeDto;
}

export type FilaTablaMovimiento = FilaDespachoGrupo | FilaMovimientoSimple;

@Component({
  selector: 'app-movimientos-pollo-engorde-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ConfirmationModalComponent,
    AuditoriaVentasModalComponent,
    ModalMovimientoPolloEngordeComponent,
    HasPermissionDirective
  ],
  templateUrl: './movimientos-pollo-engorde-list.component.html',
  styleUrls: ['./movimientos-pollo-engorde-list.component.scss']
})
export class MovimientosPolloEngordeListComponent implements OnInit {
  readonly SIN_GALPON = '__SIN_GALPON__';

  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: Array<{ id: string; label: string }> = [];
  lotesOpciones: LoteOption[] = [];

  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteValue: string | null = null; // "ae-123"

  allLoteAveEngorde: LoteAveEngordeDto[] = [];
  /**
   * Lotes para venta por granja (misma lógica que antes en getter).
   * Referencia estable: si se recalcula con getter nuevo en cada CD, el modal hijo recibe @Input distinto
   * en cada ciclo y Angular puede re-ejecutar ngOnChanges sin parar (congelamiento).
   */
  lotesParaVentaGranjaList: LoteAveEngordeDto[] = [];
  /** Catálogo completo desde filter-data (se filtra en cliente al elegir granja/núcleo). */
  private allNucleosFull: NucleoDto[] = [];
  private allGalponesFull: GalponDetailDto[] = [];

  movimientos: MovimientoPolloEngordeDto[] = [];
  filteredMovimientos: MovimientoPolloEngordeDto[] = [];

  /** Detalle del lote Ave Engorde seleccionado para la tabla informativa. */
  loteDetalleAveEngorde: LoteAveEngordeDto | null = null;
  loadingLoteDetalle = false;
  /** Resumen de aves del lote (inicio, salidas, vendidas, actuales) para reporte. */
  resumenAvesLote: ResumenAvesLoteDto | null = null;
  loadingResumen = false;

  filtroBusqueda = '';
  filtroTipoMovimiento = '';
  filtroEstado = '';

  loading = false;
  error: string | null = null;
  modalOpen = false;
  /** Sin lote seleccionado: venta desde granja (varios galpones/lotes en un despacho). */
  ventaPorGranjaMode = false;
  editingMovimiento: MovimientoPolloEngordeDto | null = null;

  showConfirmationModal = false;
  confirmationModalData: ConfirmationModalData = {
    title: 'Confirmar',
    message: '¿Estás seguro?',
    type: 'info',
    confirmText: 'Confirmar',
    cancelText: 'Cancelar',
    showCancel: true
  };
  movimientoToDelete: MovimientoPolloEngordeDto | null = null;
  movimientoToComplete: MovimientoPolloEngordeDto | null = null;
  /** Completar varios movimientos del mismo despacho (sin cambiar backend). */
  movimientoToCompleteGroup: MovimientoPolloEngordeDto[] | null = null;
  movimientoToDeleteGroup: MovimientoPolloEngordeDto[] | null = null;

  /** Despachos multi-lote: filas expandidas para ver lotes. */
  expandedDespacho: Record<string, boolean> = {};

  private galponNameById = new Map<string, string>();
  /** IDs de lotes Ave Engorde presentes en ventas registradas (resultado actual). */
  private ventaLoteAveEngordeIdSet = new Set<number>();

  loadingAuditoria = false;
  auditoriaToRun: { dryRun: boolean; aplicarCorreccion: boolean } | null = null;
  auditoriaModalOpen = false;
  auditoriaResult: AuditoriaVentasEngordeResponse | null = null;
  auditoriaLoteMetaById: Record<number, { galponLabel: string; loteLabel: string }> = {};

  loadingOrganizarPeso = false;
  organizarPesoPending = false;

  get selectedGranjaName(): string {
    const g = this.granjas.find((x) => x.id === this.selectedGranjaId);
    return g?.name ?? '';
  }

  get selectedNucleoNombre(): string {
    const n = this.nucleos.find((x) => x.nucleoId === this.selectedNucleoId);
    return n?.nucleoNombre ?? '';
  }

  get selectedGalponNombre(): string {
    if (this.selectedGalponId === this.SIN_GALPON) return '— Sin galpón —';
    const id = (this.selectedGalponId ?? '').trim();
    return (this.galponNameById.get(id) || this.selectedGalponId) ?? '';
  }

  get selectedLoteNombre(): string {
    const opt = this.lotesOpciones.find((x) => x.value === this.selectedLoteValue);
    return opt?.label ?? '—';
  }

  get hasLoteSelected(): boolean {
    // En este módulo solo se filtra por Granja; no se selecciona lote desde filtros.
    return false;
  }

  /** Nuevo registro: con lote (cualquier tipo) o sin lote solo si hay lotes en la granja (venta por granja). */
  get canOpenNuevoRegistro(): boolean {
    if (!this.selectedGranjaId) return false;
    return this.lotesParaVentaGranjaList.length > 0;
  }

  get loteAveEngordeOrigenId(): number | null {
    if (!this.selectedLoteValue || !this.selectedLoteValue.startsWith('ae-')) return null;
    const id = parseInt(this.selectedLoteValue.replace('ae-', ''), 10);
    return isNaN(id) ? null : id;
  }

  /** Filas de tabla: agrupa ventas con el mismo número de despacho (mismo viaje, varios lotes). */
  get filasTabla(): FilaTablaMovimiento[] {
    return this.buildFilasTabla(this.filteredMovimientos);
  }

  constructor(
    private loteEngordeSvc: LoteEngordeService,
    private movimientoSvc: MovimientoPolloEngordeService,
    private toastService: ToastService
  ) {}

  private resumenAuditoriaTexto(res: AuditoriaVentasEngordeResponse): string {
    const lotes = res.lotes ?? [];
    const conExceso = lotes
      .filter((l) => (l.excesoH + l.excesoM + l.excesoX) > 0)
      .sort((a, b) => (b.excesoH + b.excesoM + b.excesoX) - (a.excesoH + a.excesoM + a.excesoX));

    const fmt = (n: number) => new Intl.NumberFormat('es-CO').format(n ?? 0);
    const pad = (s: string, w: number, dir: 'l' | 'r' = 'l') => {
      const txt = s ?? '';
      if (txt.length >= w) return txt.slice(0, w);
      const spaces = ' '.repeat(w - txt.length);
      return dir === 'r' ? spaces + txt : txt + spaces;
    };
    const row = (cols: Array<{ v: string; w: number; dir?: 'l' | 'r' }>) =>
      cols.map((c) => pad(c.v, c.w, c.dir ?? 'l')).join(' | ').trimEnd();
    const sep = (cols: Array<{ w: number }>) => cols.map((c) => '-'.repeat(c.w)).join('-+-');

    const lines: string[] = [];
    lines.push(res.mensaje || (res.ok ? 'OK' : 'Se encontraron inconsistencias.'));
    lines.push('');
    lines.push('RESUMEN');
    const modo = res.aplicarCorreccion
      ? 'CORRIGIÓ (solo Pendiente)'
      : res.dryRun
        ? 'VALIDACIÓN (sin cambios)'
        : 'VALIDACIÓN';
    const resumenCols = [
      { v: 'Lotes auditados', w: 16 },
      { v: String(lotes.length), w: 6, dir: 'r' as const },
      { v: 'Con exceso', w: 10 },
      { v: String(conExceso.length), w: 6, dir: 'r' as const },
      { v: 'Modo', w: 6 },
      { v: modo, w: 28 }
    ];
    lines.push(row(resumenCols));
    lines.push(sep(resumenCols));

    if (conExceso.length) {
      lines.push('');
      lines.push('DETALLE DE INCONSISTENCIAS (por lote)');
      const tableCols = [
        { v: 'Lote (ID · Nombre)', w: 26 },
        { v: 'Límite H', w: 9, dir: 'r' as const },
        { v: 'Límite M', w: 9, dir: 'r' as const },
        { v: 'Comp H', w: 8, dir: 'r' as const },
        { v: 'Comp M', w: 8, dir: 'r' as const },
        { v: 'Pend H', w: 8, dir: 'r' as const },
        { v: 'Pend M', w: 8, dir: 'r' as const },
        { v: 'Exceso H', w: 9, dir: 'r' as const },
        { v: 'Exceso M', w: 9, dir: 'r' as const },
        { v: 'Corregible', w: 10 }
      ];
      lines.push(row(tableCols));
      lines.push(sep(tableCols));

      for (const l of conExceso.slice(0, 50)) {
        const nombre = (l.loteNombre || '').trim();
        const lotLabel = nombre ? `${l.loteAveEngordeId} · ${nombre}` : String(l.loteAveEngordeId);
        lines.push(
          row([
            { v: lotLabel, w: 26 },
            { v: fmt(l.maxVendibleH), w: 9, dir: 'r' },
            { v: fmt(l.maxVendibleM), w: 9, dir: 'r' },
            { v: fmt(l.vendidasCompletadoH), w: 8, dir: 'r' },
            { v: fmt(l.vendidasCompletadoM), w: 8, dir: 'r' },
            { v: fmt(l.vendidasPendienteH), w: 8, dir: 'r' },
            { v: fmt(l.vendidasPendienteM), w: 8, dir: 'r' },
            { v: fmt(l.excesoH), w: 9, dir: 'r' },
            { v: fmt(l.excesoM), w: 9, dir: 'r' },
            { v: l.autoCorregible ? 'Sí' : 'No', w: 10 }
          ])
        );

        // Tabla de cálculo (explica de dónde sale el límite vendible)
        lines.push(
          row([
            { v: '  Cálculo', w: 26 },
            { v: `EncH ${fmt(l.encasetadasH)}`, w: 9 },
            { v: `EncM ${fmt(l.encasetadasM)}`, w: 9 },
            { v: `MortH ${fmt(l.mortCajaH + l.mortSegH)}`, w: 8 },
            { v: `MortM ${fmt(l.mortCajaM + l.mortSegM)}`, w: 8 },
            { v: `SelH ${fmt(l.selH)}`, w: 8 },
            { v: `SelM ${fmt(l.selM)}`, w: 8 },
            { v: `ErrH ${fmt(l.errSexH)}`, w: 9 },
            { v: `ErrM ${fmt(l.errSexM)}`, w: 9 },
            { v: `AsigH/M ${fmt(l.asignadasH)}/${fmt(l.asignadasM)}`, w: 10 }
          ])
        );

        if (!l.autoCorregible) {
          lines.push(`  Motivo: ${l.estado}. No hay ventas Pendiente para ajustar (el exceso viene de Completados u otra inconsistencia).`);
        }
      }
      if (conExceso.length > 50) lines.push(`... y ${conExceso.length - 50} lote(s) más con exceso.`);
    }

    if ((res.acciones?.length || 0) > 0) {
      lines.push('');
      lines.push(`Acciones aplicadas (${res.acciones.length}):`);
      for (const a of res.acciones.slice(0, 25)) {
        lines.push(
          `- ${a.numeroMovimiento} (ID ${a.movimientoId}) lote ${a.loteAveEngordeOrigenId}: ` +
            `H/M/X ${a.antesH}/${a.antesM}/${a.antesX} → ${a.despuesH}/${a.despuesM}/${a.despuesX}`
        );
      }
      if (res.acciones.length > 25) lines.push(`... y ${res.acciones.length - 25} acción(es) más.`);
    }
    return lines.join('\n');
  }

  auditarVentas(dryRun: boolean, aplicarCorreccion: boolean): void {
    if (!this.selectedGranjaId) return;
    if (this.loadingAuditoria) return;
    this.loadingAuditoria = true;
    this.movimientoSvc
      .postAuditarVentas({
        granjaId: this.selectedGranjaId,
        aplicarCorreccion,
        dryRun
      })
      .pipe(finalize(() => (this.loadingAuditoria = false)))
      .subscribe({
        next: (res) => {
          this.auditoriaResult = res;
          this.rebuildAuditoriaMeta();
          this.auditoriaModalOpen = true;
          if (res.aplicarCorreccion) this.loadMovimientos();
        },
        error: (err) => {
          this.toastService.error(err?.message ?? 'Error al auditar ventas.');
        }
      });
  }

  private rebuildAuditoriaMeta(): void {
    const map: Record<number, { galponLabel: string; loteLabel: string }> = {};
    for (const l of this.allLoteAveEngorde || []) {
      const id = l.loteAveEngordeId;
      if (id == null) continue;
      const galponNombre = (l.galpon?.galponNombre || '').trim();
      const galponId = (l.galponId || '').trim();
      const galponLabel = galponNombre || galponId || '— Sin galpón —';
      const loteLabel = (l.loteNombre || '').trim() || String(id);
      map[id] = { galponLabel, loteLabel };
    }
    this.auditoriaLoteMetaById = map;
  }

  closeAuditoriaModal(): void {
    this.auditoriaModalOpen = false;
    this.auditoriaResult = null;
  }

  solicitarAuditoriaFix(): void {
    if (!this.selectedGranjaId) return;
    if (this.loadingAuditoria) return;
    this.auditoriaToRun = { dryRun: false, aplicarCorreccion: true };
    this.confirmationModalData = {
      title: 'Validar y corregir ventas (Pendiente)',
      message:
        'Se validarán los lotes de la granja seleccionada. Si hay sobreventa, se corregirá automáticamente SOLO lo que esté en estado Pendiente (se reducen cantidades o se cancela si queda en 0). Movimientos Completados no se tocan. ¿Desea continuar?',
      type: 'warning',
      confirmText: 'Sí, corregir',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  solicitarCorregirCompletados(): void {
    if (!this.selectedGranjaId) return;
    this.confirmationModalData = {
      title: 'Corregir ventas completadas',
      message:
        'Esto ajusta cantidades en movimientos Completados para eliminar el exceso (por sexo) y devuelve al lote SOLO la diferencia corregida. No elimina registros; si algún movimiento queda en 0 se anula. ¿Desea continuar?',
      type: 'warning',
      confirmText: 'Sí, corregir',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
    this.auditoriaToRun = { dryRun: false, aplicarCorreccion: false };
    // Reusamos onConfirmAuditoria para otro flujo: si auditoriaToRun existe pero aplicarCorreccion=false,
    // onConfirmAuditoria llamará auditarVentas; aquí no queremos eso. Por eso manejamos con una bandera distinta.
    // La bandera real se maneja por 'correccionCompletadosPending' abajo.
    this.correccionCompletadosPending = true;
  }

  correccionCompletadosPending = false;

  private ejecutarCorregirCompletados(): void {
    if (!this.selectedGranjaId) return;
    this.loadingAuditoria = true;
    this.movimientoSvc
      .postCorregirVentasCompletadas({ granjaId: this.selectedGranjaId, dryRun: false })
      .pipe(finalize(() => (this.loadingAuditoria = false)))
      .subscribe({
        next: (res: CorregirVentasCompletadasResponse) => {
          this.toastService.success(res.mensaje || 'Corrección aplicada.');
          this.loadMovimientos();
          // Re-validar después de corregir.
          this.auditarVentas(true, false);
        },
        error: (err) => this.toastService.error(err?.message ?? 'Error al corregir completados.')
      });
  }

  onConfirmAuditoria(): void {
    if (this.organizarPesoPending) {
      this.organizarPesoPending = false;
      this.showConfirmationModal = false;
      this.auditoriaToRun = null;
      this.ejecutarOrganizarPeso();
      return;
    }
    if (this.correccionCompletadosPending) {
      this.correccionCompletadosPending = false;
      this.showConfirmationModal = false;
      this.auditoriaToRun = null;
      this.ejecutarCorregirCompletados();
      return;
    }
    const a = this.auditoriaToRun;
    this.auditoriaToRun = null;
    this.showConfirmationModal = false;
    if (!a) return;
    this.auditarVentas(a.dryRun, a.aplicarCorreccion);
  }

  solicitarOrganizarPeso(): void {
    if (!this.selectedGranjaId) return;
    if (this.loadingOrganizarPeso) return;
    this.organizarPesoPending = true;
    this.confirmationModalData = {
      title: 'Organizar Peso',
      message:
        'Esta acción recalcula el peso individual prorrateado de TODAS las ventas de la granja seleccionada que tengan peso registrado. ' +
        'Agrupa los movimientos por número de despacho y distribuye el PesoNeto proporcionalmente a las aves de cada lote. ' +
        'Los datos de peso bruto y tara originales no se modifican. ¿Desea continuar?',
      type: 'warning',
      confirmText: 'Sí, organizar peso',
      cancelText: 'Cancelar',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  private ejecutarOrganizarPeso(): void {
    if (!this.selectedGranjaId) return;
    this.loadingOrganizarPeso = true;
    this.movimientoSvc
      .postOrganizarPeso({ granjaId: this.selectedGranjaId, dryRun: false, reprocesarTodo: true })
      .pipe(finalize(() => (this.loadingOrganizarPeso = false)))
      .subscribe({
        next: (res: OrganizarPesoResponse) => {
          if (res.movimientosActualizados === 0) {
            this.toastService.info(res.mensaje || 'No había peso pendiente de organizar.');
          } else {
            this.toastService.success(
              `Peso organizado: ${res.despachosProcesados} despacho(s) · ${res.movimientosActualizados} movimiento(s) actualizados.`
            );
            this.loadMovimientos();
          }
        },
        error: (err) => this.toastService.error(err?.message ?? 'Error al organizar el peso.')
      });
  }

  ngOnInit(): void {
    this.filteredMovimientos = [];
    this.loading = true;
    this.movimientoSvc
      .getFilterData()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => {
          this.granjas = data.farms ?? [];
          this.allNucleosFull = data.nucleos ?? [];
          this.allGalponesFull = data.galpones ?? [];
          this.allLoteAveEngorde = data.lotesAveEngorde ?? [];
          this.refreshLotesParaVentaGranja();
        },
        error: () => {
          this.granjas = [];
          this.allNucleosFull = [];
          this.allGalponesFull = [];
          this.allLoteAveEngorde = [];
          this.refreshLotesParaVentaGranja();
          this.toastService.error('No se pudieron cargar los filtros. Revise la sesión o intente de nuevo.');
        }
      });
  }

  onGranjaChange(granjaId: number | null): void {
    this.selectedGranjaId = granjaId;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteValue = null;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.galpones = [];
    this.lotesOpciones = [];
    this.nucleos = [];
    this.ventaLoteAveEngordeIdSet = new Set<number>();

    if (!this.selectedGranjaId) {
      this.refreshLotesParaVentaGranja();
      return;
    }

    this.nucleos = this.allNucleosFull.filter((n) => n.granjaId === this.selectedGranjaId);
    this.fillGalponMapFromCache();
    this.refreshLotesParaVentaGranja();
    this.loadMovimientos();
  }

  onNucleoChange(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId;
    this.selectedGalponId = null;
    this.selectedLoteValue = null;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.selectedLoteValue = null;
    this.buildLotesOpciones();
    this.fillGalponMapFromCache();
    this.refreshLotesParaVentaGranja();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteValue = null;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.buildLotesOpciones();
    this.loadMovimientos();
  }

  onLoteChange(value: string | null): void {
    this.selectedLoteValue = value;
    this.movimientos = [];
    this.filteredMovimientos = [];
    this.loteDetalleAveEngorde = null;
    this.resumenAvesLote = null;
    if (this.selectedLoteValue) {
      this.loadLoteDetalle();
    }
    this.loadMovimientos();
  }

  /** Nombres de galpón desde el catálogo precargado (misma lógica que por API, sin peticiones extra). */
  private fillGalponMapFromCache(): void {
    this.galponNameById.clear();
    if (!this.selectedGranjaId) return;
    let rows = this.allGalponesFull.filter((g) => g.granjaId === this.selectedGranjaId);
    if (this.selectedNucleoId) {
      const nid = String(this.selectedNucleoId);
      rows = rows.filter((g) => String(g.nucleoId) === nid);
    }
    this.fillGalponMap(rows);
  }

  private fillGalponMap(rows: GalponDetailDto[] | null | undefined): void {
    for (const g of rows || []) {
      const id = String(g.galponId).trim();
      if (id) this.galponNameById.set(id, (g.galponNombre || id).trim());
    }
    this.buildGalponesFromLotes();
  }

  private buildGalponesFromLotes(): void {
    if (!this.selectedGranjaId) {
      this.galpones = [];
      return;
    }
    // Regla solicitada: mostrar solo galpones asociados a ventas registradas (resultado actual).
    let base = this.getVentaLotesAveEngorde();
    if (this.selectedNucleoId) {
      const nid = String(this.selectedNucleoId);
      base = base.filter((l) => (l.nucleoId ?? '') === nid);
    }
    const seen = new Set<string>();
    const result: Array<{ id: string; label: string }> = [];
    for (const l of base) {
      const id = (l.galponId ?? '').trim();
      if (!id || seen.has(id)) continue;
      seen.add(id);
      result.push({ id, label: this.galponNameById.get(id) || id });
    }
    if (base.some((l) => !this.hasValue(l.galponId))) {
      result.unshift({ id: this.SIN_GALPON, label: '— Sin galpón —' });
    }
    this.galpones = result.sort((a, b) => a.label.localeCompare(b.label, 'es', { numeric: true }));
  }

  private getVentaLotesAveEngorde(): LoteAveEngordeDto[] {
    if (!this.selectedGranjaId) return [];
    if (!this.ventaLoteAveEngordeIdSet.size) return [];
    const gid = String(this.selectedGranjaId);
    const base = this.allLoteAveEngorde.filter((l) => String(l.granjaId) === gid);
    return base.filter((l) => l.loteAveEngordeId != null && this.ventaLoteAveEngordeIdSet.has(l.loteAveEngordeId));
  }

  /**
   * Lotes Ave Engorde de la granja (y núcleo si aplica) para venta por despacho;
   * incluye todos los galpones; no filtra por el desplegable de galpón.
   */
  private refreshLotesParaVentaGranja(): void {
    if (!this.selectedGranjaId) {
      this.lotesParaVentaGranjaList = [];
      return;
    }
    const gid = String(this.selectedGranjaId);
    let rows = this.allLoteAveEngorde.filter((l) => String(l.granjaId) === gid);
    if (this.selectedNucleoId) {
      const nid = String(this.selectedNucleoId);
      rows = rows.filter((l) => (l.nucleoId ?? '') === nid);
    }
    this.lotesParaVentaGranjaList = rows.sort((a, b) => {
      const ga = (a.galponId ?? '').localeCompare(b.galponId ?? '', 'es');
      if (ga !== 0) return ga;
      return (a.loteNombre || '').localeCompare(b.loteNombre || '', 'es', { numeric: true });
    });
  }

  private buildLotesOpciones(): void {
    if (!this.selectedGranjaId) {
      this.lotesOpciones = [];
      return;
    }
    const nid = this.selectedNucleoId ? String(this.selectedNucleoId) : null;
    const gpid = this.selectedGalponId && this.selectedGalponId !== this.SIN_GALPON ? String(this.selectedGalponId).trim() : null;

    // Regla solicitada: mostrar solo lotes asociados a ventas registradas (resultado actual).
    let filteredAE = this.getVentaLotesAveEngorde();
    if (nid) filteredAE = filteredAE.filter((l) => (l.nucleoId ?? '') === nid);
    if (gpid) {
      filteredAE = filteredAE.filter((l) => (l.galponId ?? '').trim() === gpid);
    } else if (this.selectedGalponId === this.SIN_GALPON) {
      filteredAE = filteredAE.filter((l) => !this.hasValue(l.galponId));
    }

    const options: LoteOption[] = [];
    for (const l of filteredAE) {
      const id = l.loteAveEngordeId;
      if (id == null) continue;
      options.push({
        value: `ae-${id}`,
        tipo: 'ae',
        id,
        label: `Ave Engorde: ${l.loteNombre || id}`
      });
    }
    this.lotesOpciones = options.sort((a, b) => a.label.localeCompare(b.label, 'es', { numeric: true }));
  }

  private rebuildVentaBasedFilterOptions(): void {
    this.ventaLoteAveEngordeIdSet = this.buildVentaLoteAveEngordeIdSetFromMovimientos(this.movimientos);
    this.buildGalponesFromLotes();
    this.buildLotesOpciones();
    this.ensureSelectedFiltersStillValid();
  }

  private buildVentaLoteAveEngordeIdSetFromMovimientos(items: MovimientoPolloEngordeDto[]): Set<number> {
    const set = new Set<number>();
    for (const m of items ?? []) {
      if (m.tipoMovimiento !== 'Venta') continue;
      if (m.tipoLoteOrigen !== 'AveEngorde') continue;
      const id = m.loteOrigenId ?? null;
      if (id != null && !isNaN(Number(id))) set.add(Number(id));
    }
    return set;
  }

  private ensureSelectedFiltersStillValid(): void {
    if (this.selectedGalponId && !this.galpones.some((g) => g.id === this.selectedGalponId)) {
      this.selectedGalponId = null;
    }
    if (this.selectedLoteValue && !this.lotesOpciones.some((l) => l.value === this.selectedLoteValue)) {
      this.selectedLoteValue = null;
      this.loteDetalleAveEngorde = null;
      this.resumenAvesLote = null;
    }
  }

  /** Carga el detalle del lote Ave Engorde seleccionado para la tabla informativa. */
  private loadLoteDetalle(): void {
    const aeId = this.loteAveEngordeOrigenId;
    this.loteDetalleAveEngorde = null;
    if (aeId == null) return;

    this.loadingLoteDetalle = true;
    this.loteEngordeSvc
      .getById(aeId)
      .pipe(finalize(() => (this.loadingLoteDetalle = false)))
      .subscribe({
        next: (dto) => {
          this.loteDetalleAveEngorde = dto;
          this.loadResumenAvesLote('LoteAveEngorde', aeId);
        },
        error: () => (this.loadingLoteDetalle = false)
      });
  }

  private loadResumenAvesLote(tipo: 'LoteAveEngorde', id: number): void {
    this.loadingResumen = true;
    this.movimientoSvc
      .getResumenAvesLote(tipo, id)
      .pipe(finalize(() => (this.loadingResumen = false)))
      .subscribe({
        next: (r) => (this.resumenAvesLote = r),
        error: () => (this.resumenAvesLote = null)
      });
  }

  /** Carga movimientos según granja (global) y filtros en cascada (núcleo, galpón, lote). */
  private loadMovimientos(): void {
    if (!this.selectedGranjaId) return;

    this.loading = true;
    this.error = null;
    const params = this.buildMovimientoSearchParams();
    this.movimientoSvc
      .search(params)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (res) => {
          this.movimientos = res.items ?? [];
          this.rebuildVentaBasedFilterOptions();
          this.rebuildAuditoriaMeta();
          this.aplicarFiltros();
        },
        error: (err) => {
          this.error = err?.message ?? 'Error al cargar movimientos';
          this.movimientos = [];
          this.filteredMovimientos = [];
          this.ventaLoteAveEngordeIdSet = new Set<number>();
          this.galpones = [];
          this.lotesOpciones = [];
        }
      });
  }

  private buildMovimientoSearchParams(): {
    page: number;
    pageSize: number;
    sortBy: string;
    sortDesc: boolean;
    granjaOrigenId: number;
    nucleoOrigenId?: string;
    galponOrigenId?: string;
    galponOrigenSinAsignar?: boolean;
    loteAveEngordeOrigenId?: number;
  } {
    const p: {
      page: number;
      pageSize: number;
      sortBy: string;
      sortDesc: boolean;
      granjaOrigenId: number;
      nucleoOrigenId?: string;
      galponOrigenId?: string;
      galponOrigenSinAsignar?: boolean;
      loteAveEngordeOrigenId?: number;
    } = {
      page: 1,
      pageSize: 3000,
      sortBy: 'FechaMovimiento',
      sortDesc: true,
      granjaOrigenId: this.selectedGranjaId!
    };
    if (this.selectedNucleoId) {
      p.nucleoOrigenId = String(this.selectedNucleoId).trim();
    }
    if (this.selectedGalponId === this.SIN_GALPON) {
      p.galponOrigenSinAsignar = true;
    } else if (this.selectedGalponId) {
      p.galponOrigenId = String(this.selectedGalponId).trim();
    }
    if (this.selectedLoteValue?.startsWith('ae-')) {
      const id = parseInt(this.selectedLoteValue.replace('ae-', ''), 10);
      if (!isNaN(id)) p.loteAveEngordeOrigenId = id;
    }
    return p;
  }

  aplicarFiltros(): void {
    let filtered = [...this.movimientos];
    const term = (this.filtroBusqueda || '').trim().toLowerCase();
    if (term) {
      filtered = filtered.filter((m) => {
        const searchText = [
          m.numeroMovimiento ?? '',
          m.tipoMovimiento ?? '',
          m.numeroDespacho ?? '',
          m.loteOrigenNombre ?? '',
          m.loteDestinoNombre ?? '',
          m.granjaOrigenNombre ?? '',
          m.granjaDestinoNombre ?? '',
          m.motivoMovimiento ?? '',
          m.estado ?? ''
        ]
          .join(' ')
          .toLowerCase();
        return searchText.includes(term);
      });
    }
    if (this.filtroTipoMovimiento) filtered = filtered.filter((m) => m.tipoMovimiento === this.filtroTipoMovimiento);
    if (this.filtroEstado) filtered = filtered.filter((m) => m.estado === this.filtroEstado);
    this.filteredMovimientos = filtered;
  }

  onFiltroChange(): void {
    this.aplicarFiltros();
  }

  limpiarFiltros(): void {
    this.filtroBusqueda = '';
    this.filtroTipoMovimiento = '';
    this.filtroEstado = '';
    this.aplicarFiltros();
  }

  create(): void {
    if (!this.selectedGranjaId) return;
    if (this.lotesParaVentaGranjaList.length === 0) return;
    // Siempre crear venta por granja (despacho). No se filtra/selecciona por galpón/lote.
    this.ventaPorGranjaMode = true;
    this.editingMovimiento = null;
    this.modalOpen = true;
  }

  viewDetail(m: MovimientoPolloEngordeDto): void {
    this.movimientoSvc.getById(m.id).subscribe({
      next: (full) => {
        this.editingMovimiento = full;
        this.modalOpen = true;
      },
      error: () => {
        this.editingMovimiento = m;
        this.modalOpen = true;
      }
    });
  }

  editMovimiento(m: MovimientoPolloEngordeDto): void {
    if (m.estado === 'Completado' || m.estado === 'Cancelado') return;
    this.movimientoSvc.getById(m.id).subscribe({
      next: (full) => {
        this.editingMovimiento = full;
        this.modalOpen = true;
      },
      error: () => {
        this.editingMovimiento = m;
        this.modalOpen = true;
      }
    });
  }

  closeModal(): void {
    this.modalOpen = false;
    this.ventaPorGranjaMode = false;
    this.editingMovimiento = null;
  }

  onMovimientoSaved(detail?: MovimientoPolloEngordeSaveDetail): void {
    const n = detail?.ventaGranjaBatchCount;
    if (n != null && n > 0) {
      this.toastService.success(
        `Se registraron ${this.formatearNumero(n)} movimiento(s) de venta en una sola operación. Quedan pendientes de completar.`
      );
    } else {
      this.toastService.success('Movimiento guardado correctamente.');
    }
    this.closeModal();
    if (this.selectedGranjaId) {
      this.loadMovimientos();
      this.refreshResumenIfLoteSelected();
    }
  }

  private refreshResumenIfLoteSelected(): void {
    if (!this.selectedLoteValue) return;
    const aeId = this.loteAveEngordeOrigenId;
    if (aeId != null) this.loadResumenAvesLote('LoteAveEngorde', aeId);
  }

  completarMovimiento(m: MovimientoPolloEngordeDto): void {
    if (m.estado !== 'Pendiente') return;
    this.movimientoToCompleteGroup = null;
    this.movimientoToComplete = m;
    this.confirmationModalData = {
      title: 'Completar movimiento',
      message: `¿Completar el movimiento ${m.numeroMovimiento}? Se descontarán ${this.formatearNumero(m.totalAves)} aves del lote origen y se actualizará el inventario.`,
      type: 'info',
      confirmText: 'Sí, completar',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  onConfirmCompletar(): void {
    const grupo = this.movimientoToCompleteGroup;
    if (grupo?.length) {
      void this.ejecutarCompletarGrupo(grupo);
      return;
    }
    if (!this.movimientoToComplete) return;
    const m = this.movimientoToComplete;
    this.showConfirmationModal = false;
    this.loading = true;
    this.movimientoSvc
      .complete(m.id)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          this.toastService.success(`Movimiento ${m.numeroMovimiento} completado. Se actualizó el inventario del lote.`);
          this.loadMovimientos();
          this.refreshResumenIfLoteSelected();
          this.movimientoToComplete = null;
        },
        error: (err) => {
          this.showErrorMessage('Error al completar: ' + (err?.message ?? ''));
          this.movimientoToComplete = null;
        }
      });
  }

  private async ejecutarCompletarGrupo(pendientes: MovimientoPolloEngordeDto[]): Promise<void> {
    this.showConfirmationModal = false;
    this.movimientoToCompleteGroup = null;
    this.loading = true;
    try {
      const ids = pendientes.filter((m) => m.estado === 'Pendiente').map((m) => m.id);
      if (ids.length === 0) return;
      const ok = await firstValueFrom(this.movimientoSvc.completarBatch(ids));
      this.toastService.success(
        `Despacho completado: ${ok.length} movimiento(s). Se descontaron las aves en cada lote.`
      );
      this.loadMovimientos();
      this.refreshResumenIfLoteSelected();
    } catch (err: unknown) {
      this.showErrorMessage('Error al completar el despacho: ' + (err instanceof Error ? err.message : String(err)));
      this.loadMovimientos();
    } finally {
      this.loading = false;
    }
  }

  deleteMovimiento(m: MovimientoPolloEngordeDto): void {
    this.movimientoToDeleteGroup = null;
    this.movimientoToDelete = m;
    const extra =
      m.estado === 'Completado'
        ? ' Las aves registradas en la venta volverán al inventario del lote de origen (y se ajustará el destino si hubo traslado).'
        : m.estado === 'Pendiente'
          ? ' Aún no se había descontado inventario al completar.'
          : '';
    this.confirmationModalData = {
      title: 'Eliminar movimiento',
      message: `¿Eliminar el movimiento ${m.numeroMovimiento}? Desaparecerá del listado.${extra}`,
      type: 'warning',
      confirmText: 'Sí, eliminar',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  onConfirmDelete(): void {
    const grupo = this.movimientoToDeleteGroup;
    if (grupo?.length) {
      void this.ejecutarCancelarGrupo(grupo);
      return;
    }
    if (!this.movimientoToDelete) return;
    const m = this.movimientoToDelete;
    this.showConfirmationModal = false;
    this.loading = true;
    this.movimientoSvc
      .eliminar(m.id, 'Eliminado por usuario')
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          const msg =
            m.estado === 'Completado'
              ? `Movimiento ${m.numeroMovimiento} eliminado. Las aves volvieron al inventario del lote.`
              : `Movimiento ${m.numeroMovimiento} eliminado.`;
          this.showSuccessMessage(msg);
          this.loadMovimientos();
          this.refreshResumenIfLoteSelected();
          this.movimientoToDelete = null;
        },
        error: (err) => {
          this.showErrorMessage('Error al eliminar: ' + (err?.message ?? ''));
          this.movimientoToDelete = null;
        }
      });
  }

  private async ejecutarCancelarGrupo(items: MovimientoPolloEngordeDto[]): Promise<void> {
    this.showConfirmationModal = false;
    this.movimientoToDeleteGroup = null;
    this.loading = true;
    const afectados = items.filter((m) => m.estado !== 'Cancelado');
    try {
      for (const m of afectados) {
        await firstValueFrom(this.movimientoSvc.eliminar(m.id, 'Eliminado por usuario (despacho)'));
      }
      this.showSuccessMessage(`Se eliminaron ${afectados.length} movimiento(s) del despacho. Si estaban completados, el inventario se revirtió en cada lote.`);
      this.loadMovimientos();
      this.refreshResumenIfLoteSelected();
    } catch (err: unknown) {
      this.showErrorMessage('Error al eliminar el despacho: ' + (err instanceof Error ? err.message : String(err)));
      this.loadMovimientos();
    } finally {
      this.loading = false;
    }
  }

  onCancelDelete(): void {
    this.showConfirmationModal = false;
    this.movimientoToDelete = null;
    this.movimientoToComplete = null;
    this.movimientoToCompleteGroup = null;
    this.movimientoToDeleteGroup = null;
  }

  showSuccessMessage(message: string): void {
    this.confirmationModalData = {
      title: 'Operación exitosa',
      message,
      type: 'success',
      confirmText: 'Aceptar',
      showCancel: false
    };
    this.showConfirmationModal = true;
  }

  showErrorMessage(message: string): void {
    this.confirmationModalData = {
      title: 'Error',
      message,
      type: 'error',
      confirmText: 'Cerrar',
      showCancel: false
    };
    this.showConfirmationModal = true;
  }

  onConfirmationModalClose(): void {
    this.showConfirmationModal = false;
    this.movimientoToDelete = null;
    this.movimientoToComplete = null;
    this.movimientoToCompleteGroup = null;
    this.movimientoToDeleteGroup = null;
  }

  private hasValue(v: unknown): boolean {
    if (v == null) return false;
    const s = String(v).trim().toLowerCase();
    return s !== '' && s !== '0' && s !== 'null' && s !== 'undefined';
  }

  formatearNumero(num: number): string {
    return new Intl.NumberFormat('es-CO').format(num);
  }

  descargarExcel(): void {
    const rows = this.filteredMovimientos ?? [];
    if (!rows.length) {
      this.toastService.info('No hay datos para exportar con los filtros actuales.');
      return;
    }

    const headers = [
      'Número movimiento',
      'Despacho',
      'Fecha',
      'Tipo',
      'Estado',
      'Granja origen',
      'Lote origen',
      'Granja destino',
      'Lote destino',
      'Total aves',
      'Hembras',
      'Machos',
      'Mixtas',
      'Placa',
      'Hora salida',
      'Guía Agrocalidad',
      'Conductor',
      'Peso bruto',
      'Peso tara',
      'Peso neto',
      'Prom. peso/ave',
      'Observaciones'
    ];

    const data = rows.map((m) => {
      const pesoBruto = m.pesoBruto ?? null;
      const pesoTara = m.pesoTara ?? null;
      const pesoNeto = pesoBruto != null && pesoTara != null ? pesoBruto - pesoTara : null;
      const promPesoAve = pesoNeto != null && (m.totalAves ?? 0) > 0 ? pesoNeto / m.totalAves : null;
      return [
        m.numeroMovimiento ?? '',
        (m.numeroDespacho ?? '').trim(),
        this.fechaCorta(m.fechaMovimiento),
        m.tipoMovimiento ?? '',
        m.estado ?? '',
        m.granjaOrigenNombre ?? '',
        m.loteOrigenNombre ?? '',
        m.granjaDestinoNombre ?? '',
        m.loteDestinoNombre ?? '',
        m.totalAves ?? 0,
        m.cantidadHembras ?? 0,
        m.cantidadMachos ?? 0,
        m.cantidadMixtas ?? 0,
        m.placa ?? '',
        m.horaSalida ? String(m.horaSalida).slice(0, 5) : '',
        m.guiaAgrocalidad ?? '',
        m.conductor ?? '',
        pesoBruto ?? '',
        pesoTara ?? '',
        pesoNeto ?? '',
        promPesoAve != null ? Math.round(promPesoAve * 1000) / 1000 : '',
        (m.observaciones ?? '').trim()
      ];
    });

    const titleBase = 'Venta de Pollo Engorde';
    const granja = (this.selectedGranjaName || '').trim();
    const title = granja ? `${titleBase} — Granja: ${granja}` : titleBase;

    const filtros: string[] = [];
    if (this.selectedGalponId) filtros.push(`Galpón: ${this.selectedGalponNombre}`);
    if (this.selectedLoteValue) filtros.push(`Lote: ${this.selectedLoteNombre}`);
    if ((this.filtroTipoMovimiento || '').trim()) filtros.push(`Tipo: ${this.filtroTipoMovimiento}`);
    if ((this.filtroEstado || '').trim()) filtros.push(`Estado: ${this.filtroEstado}`);
    if ((this.filtroBusqueda || '').trim()) filtros.push(`Búsqueda: ${this.filtroBusqueda.trim()}`);

    const aoa: (string | number)[][] = [
      [title],
      ...(filtros.length ? [[`Filtros: ${filtros.join(' · ')}`]] : []),
      [],
      headers,
      ...data
    ];

    const ws = XLSX.utils.aoa_to_sheet(aoa);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Ventas');

    const safeGranja = (granja || 'granja').replace(/[\\/:*?"<>|]/g, '_');
    const d = new Date();
    const stamp = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
    XLSX.writeFile(wb, `Venta_pollo_engorde_${safeGranja}_${stamp}.xlsx`);
  }

  /** Total aves en lote Ave Engorde (hembras + machos + mixtas o avesEncasetadas). */
  totalAvesAveEngorde(l: LoteAveEngordeDto | null): number {
    if (!l) return 0;
    const h = l.hembrasL ?? 0;
    const m = l.machosL ?? 0;
    const x = l.mixtas ?? 0;
    if (h + m + x > 0) return h + m + x;
    return l.avesEncasetadas ?? 0;
  }

  /** Disponibilidad en lote para el modal (límite al crear movimiento). */
  get availableBirdsForModal(): { total: number; hembras?: number; machos?: number; mixtas?: number } | null {
    if (this.loteDetalleAveEngorde) {
      const l = this.loteDetalleAveEngorde;
      const h = l.hembrasL ?? 0;
      const m = l.machosL ?? 0;
      const x = l.mixtas ?? 0;
      const total = h + m + x > 0 ? h + m + x : (l.avesEncasetadas ?? 0);
      return { total, hembras: h, machos: m, mixtas: x };
    }
    return null;
  }

  /** Datos del lote seleccionado (raza, año tabla, fecha encasetamiento) para prellenar y calcular edad. */
  get lotInfoForMovement(): { raza?: string | null; anoTablaGenetica?: number | null; fechaEncasetamiento?: string | null } | null {
    if (this.loteDetalleAveEngorde) {
      return {
        raza: this.loteDetalleAveEngorde.raza ?? null,
        anoTablaGenetica: this.loteDetalleAveEngorde.anoTablaGenetica ?? null,
        fechaEncasetamiento: this.loteDetalleAveEngorde.fechaEncaset ?? null
      };
    }
    return null;
  }

  fechaCorta(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    return isNaN(d.getTime()) ? iso : d.toLocaleDateString('es');
  }

  trackById(_: number, m: MovimientoPolloEngordeDto): number {
    return m.id;
  }

  trackByFila(_: number, fila: FilaTablaMovimiento): string {
    return fila.kind === 'despacho-grupo' ? fila.clave : `s-${fila.movimiento.id}`;
  }

  toggleExpandDespacho(clave: string): void {
    this.expandedDespacho = { ...this.expandedDespacho, [clave]: !this.expandedDespacho[clave] };
  }

  isDespachoExpanded(clave: string): boolean {
    return !!this.expandedDespacho[clave];
  }

  estadoGrupoDespacho(movs: MovimientoPolloEngordeDto[]): string {
    const est = movs.map((m) => m.estado);
    const allEq = (s: string) => est.every((e) => e === s);
    if (allEq('Pendiente')) return 'Pendiente';
    if (allEq('Completado')) return 'Completado';
    if (allEq('Cancelado')) return 'Cancelado';
    return 'Parcial';
  }

  totalAvesGrupo(movs: MovimientoPolloEngordeDto[]): number {
    return movs.reduce((s, m) => s + (m.totalAves ?? 0), 0);
  }

  sumCantidadGrupo(movs: MovimientoPolloEngordeDto[], campo: 'cantidadHembras' | 'cantidadMachos' | 'cantidadMixtas'): number {
    return movs.reduce((s, m) => s + (m[campo] ?? 0), 0);
  }

  pendientesEnGrupo(movs: MovimientoPolloEngordeDto[]): MovimientoPolloEngordeDto[] {
    return movs.filter((m) => m.estado === 'Pendiente');
  }

  puedeCompletarGrupo(movs: MovimientoPolloEngordeDto[]): boolean {
    return movs.some((m) => m.estado === 'Pendiente');
  }

  puedeCancelarGrupo(movs: MovimientoPolloEngordeDto[]): boolean {
    return movs.some((m) => m.estado !== 'Cancelado');
  }

  completarGrupoDespacho(fila: FilaDespachoGrupo): void {
    const pend = this.pendientesEnGrupo(fila.movimientos);
    if (pend.length === 0) return;
    this.movimientoToComplete = null;
    this.movimientoToCompleteGroup = pend;
    const total = pend.reduce((s, m) => s + m.totalAves, 0);
    const lineas = pend
      .map((m) => `${m.loteOrigenNombre ?? 'Lote'}: ${this.formatearNumero(m.totalAves)} aves`)
      .join(' · ');
    this.confirmationModalData = {
      title: 'Completar despacho (varios lotes)',
      message: `Despacho ${fila.numeroDespacho}: se completarán ${pend.length} movimiento(s) pendientes (${this.formatearNumero(total)} aves en total). Se descontará de cada lote su cantidad. Detalle: ${lineas}.`,
      type: 'info',
      confirmText: 'Sí, completar todo',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  cancelarGrupoDespacho(fila: FilaDespachoGrupo): void {
    const afect = fila.movimientos.filter((m) => m.estado !== 'Cancelado');
    if (afect.length === 0) return;
    this.movimientoToDelete = null;
    this.movimientoToDeleteGroup = afect;
    const completados = afect.filter((m) => m.estado === 'Completado').length;
    const extra =
      completados > 0
        ? ` En ${completados} línea(s) ya completada(s), las aves volverán al inventario de cada lote de origen.`
        : '';
    this.confirmationModalData = {
      title: 'Eliminar despacho completo',
      message: `¿Eliminar ${afect.length} movimiento(s) del despacho ${fila.numeroDespacho}? Desaparecerán del listado.${extra}`,
      type: 'warning',
      confirmText: 'Sí, eliminar todo',
      cancelText: 'No',
      showCancel: true
    };
    this.showConfirmationModal = true;
  }

  private buildFilasTabla(list: MovimientoPolloEngordeDto[]): FilaTablaMovimiento[] {
    const puedeAgrupar = (m: MovimientoPolloEngordeDto) =>
      m.tipoMovimiento === 'Venta' && !!(m.numeroDespacho ?? '').trim();

    const grupoKey = (m: MovimientoPolloEngordeDto) =>
      `${(m.numeroDespacho ?? '').trim().toLowerCase()}|${this.fechaDiaISO(m.fechaMovimiento)}|${m.granjaOrigenId ?? 0}`;

    const groups = new Map<string, MovimientoPolloEngordeDto[]>();
    const sueltos: MovimientoPolloEngordeDto[] = [];

    for (const m of list) {
      if (!puedeAgrupar(m)) {
        sueltos.push(m);
        continue;
      }
      const k = grupoKey(m);
      if (!groups.has(k)) groups.set(k, []);
      groups.get(k)!.push(m);
    }

    const filas: FilaTablaMovimiento[] = [];

    for (const [, movs] of groups) {
      if (movs.length >= 2) {
        movs.sort((a, b) => (a.numeroMovimiento ?? '').localeCompare(b.numeroMovimiento ?? ''));
        const clave = grupoKey(movs[0]);
        filas.push({
          kind: 'despacho-grupo',
          clave,
          numeroDespacho: (movs[0].numeroDespacho ?? '').trim(),
          fechaMovimiento: movs[0].fechaMovimiento,
          movimientos: movs
        });
      } else {
        sueltos.push(movs[0]);
      }
    }

    sueltos.sort((a, b) => this.compareMovimientoFechaDesc(a, b));
    for (const m of sueltos) {
      filas.push({ kind: 'simple', movimiento: m });
    }

    filas.sort((a, b) => this.compareFilaTablaDesc(a, b));
    return filas;
  }

  private fechaDiaISO(iso: string): string {
    if (!iso) return '';
    return iso.slice(0, 10);
  }

  private compareMovimientoFechaDesc(a: MovimientoPolloEngordeDto, b: MovimientoPolloEngordeDto): number {
    const da = new Date(a.fechaMovimiento).getTime();
    const db = new Date(b.fechaMovimiento).getTime();
    if (db !== da) return db - da;
    return (b.numeroMovimiento ?? '').localeCompare(a.numeroMovimiento ?? '');
  }

  private compareFilaTablaDesc(a: FilaTablaMovimiento, b: FilaTablaMovimiento): number {
    const fa = a.kind === 'despacho-grupo' ? a.fechaMovimiento : a.movimiento.fechaMovimiento;
    const fb = b.kind === 'despacho-grupo' ? b.fechaMovimiento : b.movimiento.fechaMovimiento;
    const ta = new Date(fa).getTime();
    const tb = new Date(fb).getTime();
    if (tb !== ta) return tb - ta;
    const na = a.kind === 'despacho-grupo' ? a.numeroDespacho : a.movimiento.numeroMovimiento;
    const nb = b.kind === 'despacho-grupo' ? b.numeroDespacho : b.movimiento.numeroMovimiento;
    return (nb ?? '').localeCompare(na ?? '');
  }
}

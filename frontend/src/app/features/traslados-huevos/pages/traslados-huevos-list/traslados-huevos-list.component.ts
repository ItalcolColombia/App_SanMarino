// frontend/src/app/features/traslados-huevos/pages/traslados-huevos-list/traslados-huevos-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import * as XLSX from 'xlsx';
import { FiltroSelectComponent, FilterDataResponse } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';
import { ModalTrasladoHuevosComponent } from '../../components/modal-traslado-huevos/modal-traslado-huevos.component';
import { TrasladosHuevosService, TrasladoHuevosDto, DisponibilidadLoteDto } from '../../services/traslados-huevos.service';
import { FarmDto } from '../../../farm/services/farm.service';
import { NucleoDto } from '../../../lote-produccion/services/nucleo.service';
import {
  ProduccionService,
  SeguimientoItemDto,
  ListaSeguimientoResponse
} from '../../../lote-produccion/services/produccion.service';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-traslados-huevos-list',
  standalone: true,
  imports: [CommonModule, FormsModule, FiltroSelectComponent, ModalTrasladoHuevosComponent],
  templateUrl: './traslados-huevos-list.component.html',
  styleUrls: ['./traslados-huevos-list.component.scss']
})
export class TrasladosHuevosListComponent implements OnInit {
  // ================== filter-data (igual que SeguimientoProduccion) ==================
  /** URL para filter-data: Granja → Núcleo → Galpón → Lote LPP en una sola petición */
  readonly filterDataUrl = `${environment.apiUrl}/traslados/filter-data`;

  // ================== catálogos (desde filter-data) ==================
  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: Array<{ galponId: string; galponNombre: string; nucleoId: string; granjaId: number }> = [];
  lotes: Array<{ loteId: number; loteNombre: string; fechaEncaset?: string | null }> = [];

  // ================== selección / filtro ==================
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;

  // ================== datos ==================
  traslados: TrasladoHuevosDto[] = [];
  filteredTraslados: TrasladoHuevosDto[] = [];
  /** Resumen espejo LPP + ubicación (desde API disponibilidad). */
  disponibilidad: DisponibilidadLoteDto | null = null;
  /** Columnas de datos + columna acciones (para colspan en estados vacíos). */
  readonly tablaColumnasCount = 36;
  /** Lote LPP seleccionado (para fechaEncaset, etc.) */
  selectedLoteInfo: { loteNombre: string; fechaEncaset?: string | null } | null = null;

  // ================== Filtros de tabla ==================
  filtroBusqueda: string = '';
  filtroTipoOperacion: string = '';
  filtroEstado: string = '';

  // ================== UI ==================
  loading = false;
  exportandoSeguimientoExcel = false;
  error: string | null = null;
  modalOpen = false;
  editingTraslado: TrasladoHuevosDto | null = null;

  // ================== GETTERS ==================
  get selectedGranjaName(): string {
    const g = this.granjas.find(x => x.id === this.selectedGranjaId);
    return g?.name ?? '';
  }

  get selectedNucleoNombre(): string {
    const n = this.nucleos.find(x => x.nucleoId === this.selectedNucleoId);
    return n?.nucleoNombre ?? '';
  }

  get selectedGalponNombre(): string {
    if (this.selectedGalponId === '__SIN_GALPON__') return '— Sin galpón —';
    const g = this.galpones.find(x => String(x.galponId).trim() === String(this.selectedGalponId).trim());
    return g?.galponNombre ?? this.selectedGalponId?.toString() ?? '—';
  }

  get selectedLoteNombre(): string {
    const l = this.lotes.find(x => x.loteId === this.selectedLoteId);
    return l?.loteNombre ?? this.selectedLoteInfo?.loteNombre ?? (this.selectedLoteId?.toString() || '—');
  }

  constructor(
    private trasladosService: TrasladosHuevosService,
    private produccionService: ProduccionService
  ) {}

  // ================== INIT ==================
  ngOnInit(): void {
    this.filteredTraslados = [];
  }

  onFilterDataLoaded(data: FilterDataResponse): void {
    this.granjas = data.farms ?? [];
    this.nucleos = data.nucleos ?? [];
    this.galpones = data.galpones ?? [];
    const raw = data.lotes ?? [];
    this.lotes = raw.map((l: any) => ({
      loteId: l.lotePosturaProduccionId ?? l.loteId,
      loteNombre: l.loteNombre ?? '',
      fechaEncaset: l.fechaEncaset ?? null
    }));
  }

  // ================== CASCADA DE FILTROS (manejados por FiltroSelectComponent) ==================
  onGranjaChange(granjaId: number | null): void {
    this.selectedGranjaId = granjaId;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.traslados = [];
    this.filteredTraslados = [];
    this.disponibilidad = null;
    this.selectedLoteInfo = null;
  }

  onNucleoChange(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.traslados = [];
    this.filteredTraslados = [];
    this.disponibilidad = null;
    this.selectedLoteInfo = null;
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteId = null;
    this.traslados = [];
    this.filteredTraslados = [];
    this.disponibilidad = null;
    this.selectedLoteInfo = null;
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLoteId = loteId;
    this.traslados = [];
    this.filteredTraslados = [];
    this.disponibilidad = null;
    const l = this.lotes.find(x => x.loteId === loteId);
    this.selectedLoteInfo = l ? { loteNombre: l.loteNombre, fechaEncaset: l.fechaEncaset } : null;

    if (!this.selectedLoteId) return;

    this.loadDatosLote();
  }

  // ================== CARGA TRASLADOS + ESPEJO ==================
  private loadDatosLote(): void {
    if (!this.selectedLoteId) return;

    this.loading = true;
    this.error = null;

    const loteKey = `LPP-${this.selectedLoteId}`;
    forkJoin({
      traslados: this.trasladosService.getTrasladosHuevosPorLote(loteKey),
      disponibilidad: this.trasladosService.getDisponibilidadLoteLPP(this.selectedLoteId).pipe(
        catchError(() => of(null))
      )
    })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: ({ traslados, disponibilidad }) => {
          this.traslados = traslados || [];
          this.disponibilidad = disponibilidad;
          this.aplicarFiltros();
        },
        error: (err) => {
          console.error('Error cargando traslados:', err);
          this.error = 'Error al cargar los traslados de huevos';
          this.traslados = [];
          this.filteredTraslados = [];
          this.disponibilidad = null;
        }
      });
  }


  // ================== FILTROS DE TABLA ==================
  aplicarFiltros(): void {
    let filtered = [...this.traslados];

    // Filtro de búsqueda
    if (this.filtroBusqueda.trim()) {
      const term = this.filtroBusqueda.toLowerCase().trim();
      filtered = filtered.filter(t => {
        const searchText = [
          t.numeroTraslado || '',
          t.tipoOperacion || '',
          t.granjaDestinoNombre || '',
          t.motivo || '',
          t.descripcion || '',
          t.observaciones || '',
          t.usuarioNombre || '',
          t.loteId || '',
          String(t.usuarioTrasladoId ?? ''),
          t.estado || ''
        ].join(' ').toLowerCase();
        return searchText.includes(term);
      });
    }

    // Filtro por tipo de operación
    if (this.filtroTipoOperacion) {
      filtered = filtered.filter(t => t.tipoOperacion === this.filtroTipoOperacion);
    }

    // Filtro por estado
    if (this.filtroEstado) {
      filtered = filtered.filter(t => t.estado === this.filtroEstado);
    }

    this.filteredTraslados = filtered;
  }

  onFiltroChange(): void {
    this.aplicarFiltros();
  }

  limpiarFiltros(): void {
    this.filtroBusqueda = '';
    this.filtroTipoOperacion = '';
    this.filtroEstado = '';
    this.aplicarFiltros();
  }

  // ================== CRUD modal ==================
  create(): void {
    if (!this.selectedLoteId) return;
    this.editingTraslado = null;
    this.modalOpen = true;
  }

  viewDetail(traslado: TrasladoHuevosDto): void {
    // Cargar el traslado completo desde el backend para tener toda la información
    this.loading = true;
    this.trasladosService.getTrasladoHuevos(traslado.id).subscribe({
      next: (trasladoCompleto) => {
        this.editingTraslado = trasladoCompleto;
        this.modalOpen = true;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error cargando detalle del traslado:', err);
        // Si falla, usar el traslado que ya tenemos
        this.editingTraslado = traslado;
        this.modalOpen = true;
        this.loading = false;
      }
    });
  }

  editTraslado(traslado: TrasladoHuevosDto): void {
    // Cargar el traslado completo desde el backend para tener toda la información
    this.loading = true;
    this.trasladosService.getTrasladoHuevos(traslado.id).subscribe({
      next: (trasladoCompleto) => {
        this.editingTraslado = trasladoCompleto;
        this.modalOpen = true;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error cargando traslado para editar:', err);
        // Si falla, usar el traslado que ya tenemos
        this.editingTraslado = traslado;
        this.modalOpen = true;
        this.loading = false;
      }
    });
  }

  closeModal(): void {
    this.modalOpen = false;
    this.editingTraslado = null;
  }

  onTrasladoSaved(): void {
    this.closeModal();
    if (this.selectedLoteId) {
      this.loadDatosLote();
    }
  }

  deleteTraslado(traslado: TrasladoHuevosDto): void {
    if (traslado.estado === 'Cancelado') {
      return;
    }

    const msg =
      traslado.estado === 'Completado'
        ? `¿Anular el registro ${traslado.numeroTraslado}? Los huevos volverán a estar disponibles en el inventario.`
        : `¿Cancelar el traslado ${traslado.numeroTraslado}?`;

    if (!confirm(msg)) {
      return;
    }

    this.loading = true;
    this.error = null;
    const motivo =
      traslado.estado === 'Completado' ? 'Anulación: devolución de inventario' : 'Cancelado por usuario';

    this.trasladosService.cancelarTrasladoHuevos(traslado.id, motivo).subscribe({
      next: () => {
        this.loading = false;
        this.closeModal();
        this.loadDatosLote();
      },
      error: (err) => {
        this.loading = false;
        const detail = err?.error?.message ?? err?.message ?? 'Error desconocido';
        this.error = 'Error al anular el registro: ' + detail;
        setTimeout(() => (this.error = null), 8000);
      }
    });
  }

  // ================== HELPERS ==================
  private hasValue(v: unknown): boolean {
    if (v === null || v === undefined) return false;
    const s = String(v).trim().toLowerCase();
    return !(s === '' || s === '0' || s === 'null' || s === 'undefined');
  }

  private normalizeId(v: unknown): string {
    if (v === null || v === undefined) return '';
    return String(v).trim();
  }

  formatearNumero(num: number): string {
    return new Intl.NumberFormat('es-CO').format(num);
  }

  getTotalHuevos(traslado: TrasladoHuevosDto): number {
    return traslado.cantidadLimpio + traslado.cantidadTratado + traslado.cantidadSucio +
           traslado.cantidadDeforme + traslado.cantidadBlanco + traslado.cantidadDobleYema +
           traslado.cantidadPiso + traslado.cantidadPequeno + traslado.cantidadRoto +
           traslado.cantidadDesecho + traslado.cantidadOtro;
  }

  /** Suma de huevos en ventas completadas (inventario retirado por venta). */
  totalHuevosVentasCompletadas(): number {
    return this.traslados
      .filter(t => t.tipoOperacion === 'Venta' && t.estado === 'Completado')
      .reduce((s, t) => s + this.getTotalHuevos(t), 0);
  }

  totalRegistrosVentasCompletadas(): number {
    return this.traslados.filter(t => t.tipoOperacion === 'Venta' && t.estado === 'Completado').length;
  }

  /** Suma de huevos en traslados completados (distinto de venta). */
  totalHuevosTrasladosCompletados(): number {
    return this.traslados
      .filter(t => t.tipoOperacion === 'Traslado' && t.estado === 'Completado')
      .reduce((s, t) => s + this.getTotalHuevos(t), 0);
  }

  descargarSeguimientoExcel(): void {
    if (!this.selectedLoteId) return;
    this.exportandoSeguimientoExcel = true;
    this.produccionService
      .listarSeguimiento({ lotePosturaProduccionId: this.selectedLoteId, page: 1, size: 0 })
      .pipe(finalize(() => (this.exportandoSeguimientoExcel = false)))
      .subscribe({
        next: (res: ListaSeguimientoResponse) => {
          const items = res.items ?? [];
          const rows = items.map((i: SeguimientoItemDto) => this.mapSeguimientoToExportRow(i));
          const ws = XLSX.utils.json_to_sheet(
            rows.length ? rows : [{ Mensaje: 'Sin registros de seguimiento para este lote' }]
          );
          const wb = XLSX.utils.book_new();
          XLSX.utils.book_append_sheet(wb, ws, 'Seguimiento');
          const fname = `seguimiento-produccion-LPP-${this.selectedLoteId}-${new Date().toISOString().slice(0, 10)}.xlsx`;
          XLSX.writeFile(wb, fname);
        },
        error: (err: { error?: { message?: string }; message?: string }) => {
          const d = err?.error?.message ?? err?.message ?? 'Error al exportar';
          this.error = 'Exportación Excel: ' + d;
          setTimeout(() => (this.error = null), 8000);
        }
      });
  }

  private mapSeguimientoToExportRow(i: SeguimientoItemDto): Record<string, string | number | null | undefined> {
    return {
      ID: i.id,
      ProduccionLoteId: i.produccionLoteId,
      LotePosturaProduccionId: i.lotePosturaProduccionId ?? '',
      FechaRegistro: i.fechaRegistro,
      MortalidadH: i.mortalidadH,
      MortalidadM: i.mortalidadM,
      SelH: i.selH,
      SelM: i.selM,
      ConsKgH: i.consKgH,
      ConsKgM: i.consKgM,
      ConsumoKg: i.consumoKg,
      HuevosTotales: i.huevosTotales,
      HuevosIncubables: i.huevosIncubables,
      HuevoLimpio: i.huevoLimpio ?? '',
      HuevoTratado: i.huevoTratado ?? '',
      HuevoSucio: i.huevoSucio ?? '',
      HuevoDeforme: i.huevoDeforme ?? '',
      HuevoBlanco: i.huevoBlanco ?? '',
      HuevoDobleYema: i.huevoDobleYema ?? '',
      HuevoPiso: i.huevoPiso ?? '',
      HuevoPequeno: i.huevoPequeno ?? '',
      HuevoRoto: i.huevoRoto ?? '',
      HuevoDesecho: i.huevoDesecho ?? '',
      HuevoOtro: i.huevoOtro ?? '',
      TipoAlimento: i.tipoAlimento,
      PesoHuevo: i.pesoHuevo,
      Etapa: i.etapa,
      Observaciones: i.observaciones ?? '',
      CreatedAt: i.createdAt,
      UpdatedAt: i.updatedAt ?? '',
      ConsumoAguaDiario: i.consumoAguaDiario ?? '',
      ConsumoAguaPh: i.consumoAguaPh ?? '',
      ConsumoAguaOrp: i.consumoAguaOrp ?? '',
      ConsumoAguaTemperatura: i.consumoAguaTemperatura ?? '',
      PesoH: i.pesoH ?? '',
      PesoM: i.pesoM ?? '',
      Uniformidad: i.uniformidad ?? '',
      CoeficienteVariacion: i.coeficienteVariacion ?? '',
      ObservacionesPesaje: i.observacionesPesaje ?? '',
      MetadataJSON: i.metadata != null ? JSON.stringify(i.metadata) : ''
    };
  }

  calcularEdadDiasDesdeEncasetamiento(): number {
    const fechaEncaset = this.selectedLoteInfo?.fechaEncaset;
    if (!fechaEncaset) return 0;
    const fecha = new Date(fechaEncaset);
    const hoy = new Date();
    const diffTime = hoy.getTime() - fecha.getTime();
    return Math.floor(diffTime / (1000 * 60 * 60 * 24));
  }
}

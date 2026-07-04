import {
  Component, Input, Output, EventEmitter,
  OnChanges, SimpleChanges, ChangeDetectorRef, HostListener,
  ChangeDetectionStrategy
} from '@angular/core';

import { forkJoin, of } from 'rxjs';
import { catchError, finalize, map } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faTimes, faXmark } from '@fortawesome/free-solid-svg-icons';

import {
  SeguimientoLoteLevanteDto
} from '../../../lote-levante/services/seguimiento-lote-levante.service';
import { CatalogoAlimentosService } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { GestionInventarioService } from '../../../gestion-inventario/services/gestion-inventario.service';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { ShowIfEcuadorPanamaDirective } from '../../../../core/directives';

export interface ItemDetalleEngorde {
  catalogItemId: number;
  tipoItem: string;
  cantidad: number;
  unidad: string;
}

@Component({
  selector: 'app-modal-detalle-seguimiento-engorde',
  standalone: true,
  imports: [FontAwesomeModule, ShowIfEcuadorPanamaDirective],
  templateUrl: './modal-detalle-seguimiento-engorde.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./modal-detalle-seguimiento-engorde.component.scss']
})
export class ModalDetalleSeguimientoEngordeComponent implements OnChanges {
  faTimes = faTimes;
  faXmark = faXmark;

  @Input() isOpen = false;
  @Input() seguimiento: SeguimientoLoteLevanteDto | null = null;
  @Output() close = new EventEmitter<void>();

  loading = false;
  /** Alimentos/ítems del registro (en engorde todos van en itemsHembras). */
  items: ItemDetalleEngorde[] = [];
  /** itemId → "código - nombre" para mostrar en tabla. */
  itemNames = new Map<number, string>();

  constructor(
    private catalogService: CatalogoAlimentosService,
    private gestionInventarioSvc: GestionInventarioService,
    private countryFilter: CountryFilterService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen']?.currentValue && this.seguimiento) {
      this.procesarItems();
    }
    if (!this.isOpen) {
      this.items = [];
      this.itemNames.clear();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.isOpen) this.onClose();
  }

  get isPanama(): boolean { return this.countryFilter.isPanama(); }

  onClose(): void {
    this.close.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('detalle-engorde__backdrop')) {
      this.onClose();
    }
  }

  private procesarItems(): void {
    if (!this.seguimiento) return;

    const metadata = this.normalizeJson(this.seguimiento.metadata) ?? {};
    const itemsAdicionales = this.normalizeJson(this.seguimiento.itemsAdicionales) ?? {};
    this.items = [];

    // Engorde: hembrasSoloAlimento → todos los ítems van en itemsHembras de metadata.
    if (metadata['itemsHembras'] && Array.isArray(metadata['itemsHembras'])) {
      this.items = (metadata['itemsHembras'] as any[]).map(item => ({
        tipoItem: item.tipoItem || 'alimento',
        catalogItemId: item.catalogItemId ?? item.itemInventarioEcuadorId,
        cantidad: Number(item.cantidad ?? item.cantidadKg ?? 0),
        unidad: item.unidad || 'kg'
      })).filter(i => i.catalogItemId);
    } else if (itemsAdicionales['itemsHembras'] && Array.isArray(itemsAdicionales['itemsHembras'])) {
      this.items = (itemsAdicionales['itemsHembras'] as any[]).map(item => ({
        tipoItem: item.tipoItem || 'alimento',
        catalogItemId: item.catalogItemId ?? item.itemInventarioEcuadorId,
        cantidad: Number(item.cantidad ?? item.cantidadKg ?? 0),
        unidad: item.unidad || 'kg'
      })).filter(i => i.catalogItemId);
    } else {
      // Compatibilidad con registros antiguos (un solo alimento sin FormArray).
      const hId = Number(
        metadata['tipoAlimentoHembras'] ?? (this.seguimiento as any).tipoAlimentoHembras ?? 0
      );
      const cons = Number(
        metadata['consumoOriginalHembras'] ?? this.seguimiento.consumoKgHembras ?? 0
      );
      const unidad = String(metadata['unidadConsumoOriginalHembras'] ?? 'kg');
      if (hId > 0) {
        this.items = [{ tipoItem: 'alimento', catalogItemId: hId, cantidad: cons, unidad }];
      }
    }

    this.loadItemNames();
  }

  private loadItemNames(): void {
    const ids = new Set<number>(this.items.map(i => i.catalogItemId).filter(Boolean));
    if (ids.size === 0) return;

    const isEcuador = this.countryFilter.isEcuadorOrPanama();
    const requests = Array.from(ids).map(id => {
      if (isEcuador) {
        return this.gestionInventarioSvc.getItemById(id).pipe(
          map(item => item ? { nombre: item.nombre, codigo: item.codigo } : null),
          catchError(() => of(null))
        );
      }
      return this.catalogService.getById(id).pipe(
        map(item => item ? { nombre: item.nombre, codigo: item.codigo } : null),
        catchError(() => of(null))
      );
    });

    forkJoin(requests).pipe(
      finalize(() => this.cdr.markForCheck())
    ).subscribe(results => {
      Array.from(ids).forEach((id, index) => {
        const item = results[index];
        const label = item?.nombre
          ? (item.codigo ? `${item.codigo} — ${item.nombre}` : item.nombre)
          : `ID ${id}`;
        this.itemNames.set(id, label);
      });
    });
  }

  getItemDisplay(id: number): string {
    return this.itemNames.get(id) ?? `ID ${id}`;
  }

  // ── Helpers de formato ──────────────────────────────────────────────────────

  private normalizeJson(raw: any): any {
    if (raw == null) return null;
    if (typeof raw === 'string') {
      try { return JSON.parse(raw); } catch { return null; }
    }
    return raw;
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '—';
    const ymd = String(date).slice(0, 10);
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  }

  formatNum(v: number | null | undefined, decimals = 2): string {
    if (v == null) return '—';
    return Number(v).toFixed(decimals);
  }

  /** Devuelve true si el campo tiene valor mayor a 0 (para ocultar celdas vacías). */
  hasValue(v: number | null | undefined): boolean {
    return v != null && Number(v) > 0;
  }

  /** True si TODOS los campos de mortalidad/selección son cero o nulos (sección vacía). */
  get sinMortalidad(): boolean {
    const s = this.seguimiento;
    if (!s) return true;
    return (
      !this.hasValue(s.mortalidadHembras) &&
      !this.hasValue(s.mortalidadMachos) &&
      !this.hasValue(s.selH) &&
      !this.hasValue(s.selM) &&
      !this.hasValue(s.errorSexajeHembras) &&
      !this.hasValue(s.errorSexajeMachos)
    );
  }

  get sinPeso(): boolean {
    const s = this.seguimiento;
    if (!s) return true;
    return (
      s.pesoPromH == null && s.pesoPromM == null &&
      s.uniformidadH == null && s.uniformidadM == null &&
      s.cvH == null && s.cvM == null
    );
  }
}

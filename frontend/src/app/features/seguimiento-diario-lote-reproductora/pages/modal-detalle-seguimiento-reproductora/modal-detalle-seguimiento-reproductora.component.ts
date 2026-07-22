import {
  Component, Input, Output, EventEmitter,
  OnInit, OnChanges, SimpleChanges,
  ChangeDetectorRef, HostListener,
  ChangeDetectionStrategy
} from '@angular/core';

import { forkJoin, of } from 'rxjs';
import { catchError, finalize, map } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faTimes, faXmark } from '@fortawesome/free-solid-svg-icons';

import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-diario-lote-reproductora.service';
import { SeguimientoDiarioLoteReproductoraService } from '../../services/seguimiento-diario-lote-reproductora.service';
import { CatalogoAlimentosService } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { GestionInventarioService } from '../../../gestion-inventario/services/gestion-inventario.service';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { ShowIfEcuadorPanamaDirective } from '../../../../core/directives';
import { ymdSinTz } from '../../../../shared/utils/format';

interface ItemSeguimientoLocal {
  tipoItem: string;
  catalogItemId: number;
  cantidad: number;
  unidad: string;
}

@Component({
  selector: 'app-modal-detalle-seguimiento-reproductora',
  standalone: true,
  imports: [FontAwesomeModule, ShowIfEcuadorPanamaDirective],
  templateUrl: './modal-detalle-seguimiento-reproductora.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./modal-detalle-seguimiento-reproductora.component.scss']
})
export class ModalDetalleSeguimientoReproductoraComponent implements OnInit, OnChanges {
  faTimes = faTimes;
  faXmark = faXmark;

  @Input() isOpen = false;
  @Input() seguimiento: SeguimientoLoteLevanteDto | null = null;
  /** Si true, usa los datos del Input sin llamar al backend (el padre ya tiene el objeto completo). */
  @Input() skipFetch = false;
  @Output() close = new EventEmitter<void>();

  loading = false;
  itemsHembras: ItemSeguimientoLocal[] = [];
  itemsMachos: ItemSeguimientoLocal[] = [];
  itemsGenerales: ItemSeguimientoLocal[] = [];
  /** catalogItemId → etiqueta para mostrar en tablas de ítems */
  itemNames = new Map<number, string>();

  constructor(
    private segSvc: SeguimientoDiarioLoteReproductoraService,
    private catalogSvc: CatalogoAlimentosService,
    private inventarioSvc: GestionInventarioService,
    private countryFilter: CountryFilterService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    if (this.isOpen && this.seguimiento) this.cargarDetalle();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen']?.currentValue && this.seguimiento) this.cargarDetalle();
  }

  cargarDetalle(): void {
    if (!this.seguimiento) return;
    this.loading = true;

    if (this.skipFetch) {
      this.procesarItems();
      this.loading = false;
      return;
    }

    if (this.seguimiento.id) {
      this.segSvc.getById(this.seguimiento.id).subscribe({
        next: data => {
          this.seguimiento = data;
          this.procesarItems();
          this.loading = false;
        },
        error: () => {
          this.procesarItems();
          this.loading = false;
        }
      });
    } else {
      this.procesarItems();
      this.loading = false;
    }
  }

  private procesarItems(): void {
    if (!this.seguimiento) return;
    const metadata = (this.seguimiento.metadata as any) || {};
    const adicionales = (this.seguimiento.itemsAdicionales as any) || {};
    const seg = this.seguimiento as any;

    // Hembras
    if (metadata.itemsHembras?.length) {
      this.itemsHembras = metadata.itemsHembras.map((i: any) => ({
        tipoItem: i.tipoItem || 'alimento',
        catalogItemId: i.catalogItemId ?? i.itemInventarioEcuadorId,
        cantidad: i.cantidad || i.cantidadKg || 0,
        unidad: i.unidad || 'kg'
      }));
    } else if (adicionales.itemsHembras?.length) {
      this.itemsHembras = adicionales.itemsHembras;
    } else {
      const hId = Number(metadata.tipoAlimentoHembras ?? seg.tipoAlimentoHembras);
      if (hId) {
        this.itemsHembras = [{
          tipoItem: 'alimento',
          catalogItemId: hId,
          cantidad: metadata.consumoOriginalHembras ?? seg.consumoKgHembras ?? 0,
          unidad: metadata.unidadConsumoOriginalHembras || 'kg'
        }];
      } else {
        this.itemsHembras = [];
      }
    }

    // Machos
    if (metadata.itemsMachos?.length) {
      this.itemsMachos = metadata.itemsMachos.map((i: any) => ({
        tipoItem: i.tipoItem || 'alimento',
        catalogItemId: i.catalogItemId ?? i.itemInventarioEcuadorId,
        cantidad: i.cantidad || i.cantidadKg || 0,
        unidad: i.unidad || 'kg'
      }));
    } else if (adicionales.itemsMachos?.length) {
      this.itemsMachos = adicionales.itemsMachos;
    } else {
      const mId = Number(metadata.tipoAlimentoMachos ?? seg.tipoAlimentoMachos);
      const consM = metadata.consumoOriginalMachos ?? seg.consumoKgMachos;
      if (mId && consM != null) {
        this.itemsMachos = [{
          tipoItem: 'alimento',
          catalogItemId: mId,
          cantidad: Number(consM),
          unidad: metadata.unidadConsumoOriginalMachos || 'kg'
        }];
      } else {
        this.itemsMachos = [];
      }
    }

    // Generales
    if (metadata.itemsGenerales?.length) {
      this.itemsGenerales = metadata.itemsGenerales.map((i: any) => ({
        tipoItem: i.tipoItem || 'consumible',
        catalogItemId: i.catalogItemId ?? i.itemInventarioEcuadorId,
        cantidad: i.cantidad || i.cantidadKg || 0,
        unidad: i.unidad || 'unidades'
      }));
    } else if (adicionales.itemsGenerales?.length) {
      this.itemsGenerales = adicionales.itemsGenerales;
    } else {
      this.itemsGenerales = [];
    }

    this.loadItemNames();
  }

  private loadItemNames(): void {
    const ids = new Set<number>();
    [...this.itemsHembras, ...this.itemsMachos, ...this.itemsGenerales]
      .forEach(i => { if (i.catalogItemId) ids.add(i.catalogItemId); });
    if (ids.size === 0) return;

    const isEcuadorPanama = this.countryFilter.isEcuadorOrPanama();
    const requests = Array.from(ids).map(id =>
      isEcuadorPanama
        ? this.inventarioSvc.getItemById(id).pipe(
            map(item => item ? { nombre: item.nombre, codigo: item.codigo } : null),
            catchError(() => of(null))
          )
        : this.catalogSvc.getById(id).pipe(
            map(item => item ? { nombre: item.nombre, codigo: item.codigo } : null),
            catchError(() => of(null))
          )
    );

    forkJoin(requests).pipe(finalize(() => this.cdr.markForCheck())).subscribe(results => {
      Array.from(ids).forEach((id, idx) => {
        const item = results[idx];
        this.itemNames.set(id, item?.nombre
          ? (item.codigo ? `${item.codigo} - ${item.nombre}` : item.nombre)
          : `ID ${id}`);
      });
    });
  }

  getItemDisplay(catalogItemId: number): string {
    return this.itemNames.get(catalogItemId) ?? `ID ${catalogItemId}`;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.isOpen) this.onClose();
  }

  onClose(): void { this.close.emit(); }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('detalle-backdrop')) this.onClose();
  }

  formatNum(v: number | null | undefined, decimals = 2): string {
    if (v == null) return '—';
    return v.toFixed(decimals);
  }

  /**
   * Formatea una fecha "pura" a dd/MM/yyyy SIN corrimiento de zona.
   * Usa ymdSinTz (misma regla que la tabla con `| date:'…':'UTC'`) para que el modal
   * muestre exactamente el día registrado. Antes usaba new Date().toLocaleDateString(),
   * que convertía a zona local y restaba un día a los datos guardados a medianoche UTC.
   */
  formatDate(d: string | Date | null | undefined): string {
    const ymd = ymdSinTz(d);
    if (!ymd) return '—';
    const [y, m, day] = ymd.split('-');
    return `${day}/${m}/${y}`;
  }
}

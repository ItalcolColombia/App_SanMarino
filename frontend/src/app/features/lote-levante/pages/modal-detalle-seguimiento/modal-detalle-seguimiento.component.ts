import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, ChangeDetectorRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faTimes, faXmark } from '@fortawesome/free-solid-svg-icons';
import { SeguimientoLoteLevanteDto, SeguimientoLoteLevanteService, ItemSeguimientoDto } from '../../services/seguimiento-lote-levante.service';
import { CatalogoAlimentosService } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { ShowIfEcuadorPanamaDirective } from '../../../../core/directives';

@Component({
  selector: 'app-modal-detalle-seguimiento-levante',
  standalone: true,
  imports: [CommonModule, FontAwesomeModule, ShowIfEcuadorPanamaDirective],
  templateUrl: './modal-detalle-seguimiento.component.html',
  styleUrls: ['./modal-detalle-seguimiento.component.scss']
})
export class ModalDetalleSeguimientoLevanteComponent implements OnInit, OnChanges {
  faTimes = faTimes;
  faXmark = faXmark;
  @Input() isOpen: boolean = false;
  @Input() seguimiento: SeguimientoLoteLevanteDto | null = null;
  /** Si true, no llama getById y usa solo los datos pasados en seguimiento (p. ej. desde Seguimiento Diario Lote Reproductora). */
  @Input() skipFetch: boolean = false;
  @Output() close = new EventEmitter<void>();

  loading: boolean = false;
  itemsHembras: ItemSeguimientoDto[] = [];
  itemsMachos: ItemSeguimientoDto[] = [];
  /** catalogItemId -> nombre (o codigo - nombre) para mostrar en la tabla de ítems */
  itemNames = new Map<number, string>();

  constructor(
    private seguimientoService: SeguimientoLoteLevanteService,
    private catalogService: CatalogoAlimentosService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    if (this.isOpen && this.seguimiento) {
      this.cargarDetalle();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen']?.currentValue && this.seguimiento) {
      this.cargarDetalle();
    }
  }

  cargarDetalle(): void {
    if (!this.seguimiento) return;

    this.loading = true;

    if (this.skipFetch) {
      this.procesarItems();
      this.loading = false;
      return;
    }

    // Si tenemos el ID, cargar desde el backend para obtener todos los datos actualizados
    if (this.seguimiento.id) {
      this.seguimientoService.getById(this.seguimiento.id).subscribe({
        next: (data) => {
          this.seguimiento = data;
          this.procesarItems();
          this.loading = false;
        },
        error: (err) => {
          console.error('Error al cargar detalle:', err);
          // Si falla, usar los datos que ya tenemos
          this.procesarItems();
          this.loading = false;
        }
      });
    } else {
      // Si no hay ID, usar los datos que ya tenemos
      this.procesarItems();
      this.loading = false;
    }
  }

  private procesarItems(): void {
    if (!this.seguimiento) return;

    // Cargar items desde metadata (nuevo formato)
    const metadata = this.seguimiento.metadata || {};
    const itemsAdicionales = this.seguimiento.itemsAdicionales || {};

    // Items de hembras
    if (metadata.itemsHembras && Array.isArray(metadata.itemsHembras)) {
      this.itemsHembras = metadata.itemsHembras.map((item: any) => ({
        tipoItem: item.tipoItem || 'alimento',
        catalogItemId: item.catalogItemId,
        cantidad: item.cantidad || item.cantidadKg || 0,
        unidad: item.unidad || 'kg'
      }));
    } else if (itemsAdicionales.itemsHembras && Array.isArray(itemsAdicionales.itemsHembras)) {
      this.itemsHembras = itemsAdicionales.itemsHembras;
    } else {
      // Compatibilidad hacia atrás: construir desde campos tradicionales
      if (metadata.tipoAlimentoHembras && metadata.tipoItemHembras === 'alimento') {
        this.itemsHembras = [{
          tipoItem: 'alimento',
          catalogItemId: metadata.tipoAlimentoHembras,
          cantidad: metadata.consumoOriginalHembras || this.seguimiento.consumoKgHembras || 0,
          unidad: metadata.unidadConsumoOriginalHembras || 'kg'
        }];
      }
    }

    // Items de machos
    if (metadata.itemsMachos && Array.isArray(metadata.itemsMachos)) {
      this.itemsMachos = metadata.itemsMachos.map((item: any) => ({
        tipoItem: item.tipoItem || 'alimento',
        catalogItemId: item.catalogItemId,
        cantidad: item.cantidad || item.cantidadKg || 0,
        unidad: item.unidad || 'kg'
      }));
    } else if (itemsAdicionales.itemsMachos && Array.isArray(itemsAdicionales.itemsMachos)) {
      this.itemsMachos = itemsAdicionales.itemsMachos;
    } else {
      // Compatibilidad hacia atrás: construir desde campos tradicionales
      if (metadata.tipoAlimentoMachos && metadata.tipoItemMachos === 'alimento' && this.seguimiento.consumoKgMachos) {
        this.itemsMachos = [{
          tipoItem: 'alimento',
          catalogItemId: metadata.tipoAlimentoMachos,
          cantidad: metadata.consumoOriginalMachos || this.seguimiento.consumoKgMachos || 0,
          unidad: metadata.unidadConsumoOriginalMachos || 'kg'
        }];
      }
    }

    this.loadItemNames();
  }

  private loadItemNames(): void {
    const ids = new Set<number>();
    this.itemsHembras.forEach(i => ids.add(i.catalogItemId));
    this.itemsMachos.forEach(i => ids.add(i.catalogItemId));
    if (ids.size === 0) return;

    const requests = Array.from(ids).map(id =>
      this.catalogService.getById(id).pipe(
        catchError(() => of(null))
      )
    );
    forkJoin(requests).pipe(
      finalize(() => this.cdr.markForCheck())
    ).subscribe(results => {
      Array.from(ids).forEach((id, index) => {
        const item = results[index];
        const label = item?.nombre
          ? (item.codigo ? `${item.codigo} - ${item.nombre}` : item.nombre)
          : `ID ${id}`;
        this.itemNames.set(id, label);
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

  onClose(): void {
    this.close.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('detalle-modal__backdrop')) {
      this.onClose();
    }
  }

  formatNumber(value: number | null | undefined): string {
    if (value == null) return '—';
    return value.toFixed(2);
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '—';
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }
}

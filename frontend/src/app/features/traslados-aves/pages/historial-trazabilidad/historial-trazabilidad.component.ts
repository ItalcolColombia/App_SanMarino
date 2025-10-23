import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { 
  TrasladosAvesService, 
  HistorialInventarioDto, 
  HistorialInventarioSearchRequest, 
  TrazabilidadLoteDto,
  PagedResult
} from '../../services/traslados-aves.service';

@Component({
  selector: 'app-historial-trazabilidad',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, SidebarComponent],
  templateUrl: './historial-trazabilidad.component.html',
  styleUrls: ['./historial-trazabilidad.component.scss']
})
export class HistorialTrazabilidadComponent implements OnInit {
  // Signals para manejo de estado reactivo
  historial = signal<HistorialInventarioDto[]>([]);
  trazabilidad = signal<TrazabilidadLoteDto | null>(null);
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  totalRecords = signal<number>(0);
  currentPage = signal<number>(1);
  vistaActual = signal<'historial' | 'trazabilidad'>('historial');
  loteSeleccionado = signal<string | null>(null);

  // Filtros
  filtros: HistorialInventarioSearchRequest = {
    sortBy: 'fecha_registro',
    sortDesc: true,
    page: 1,
    pageSize: 20
  };

  // Computed properties
  hasData = computed(() => this.historial().length > 0);
  hasError = computed(() => !!this.error());
  isLoading = computed(() => this.loading());
  totalPages = computed(() => Math.ceil(this.totalRecords() / this.filtros.pageSize));
  hasTrazabilidad = computed(() => !!this.trazabilidad());

  constructor(
    private trasladosService: TrasladosAvesService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    // Verificar si se pasó un loteId en la ruta
    const loteId = this.route.snapshot.paramMap.get('loteId');
    if (loteId) {
      this.loteSeleccionado.set(loteId);
      this.vistaActual.set('trazabilidad');
      this.cargarTrazabilidad(loteId);
    } else {
      this.cargarHistorial();
    }
  }

  async cargarHistorial(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const result = await this.trasladosService.searchHistorial(this.filtros).toPromise();
      if (result) {
        this.historial.set(result.items);
        this.totalRecords.set(result.total);
        this.currentPage.set(result.page);
      }
    } catch (error: any) {
      console.error('Error al cargar historial:', error);
      this.error.set(error.message || 'Error al cargar el historial');
      this.historial.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  async cargarTrazabilidad(loteId: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const trazabilidad = await this.trasladosService.getTrazabilidadLote(loteId).toPromise();
      this.trazabilidad.set(trazabilidad || null);
    } catch (error: any) {
      console.error('Error al cargar trazabilidad:', error);
      this.error.set(error.message || 'Error al cargar la trazabilidad del lote');
      this.trazabilidad.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  cambiarVista(vista: 'historial' | 'trazabilidad'): void {
    this.vistaActual.set(vista);
    this.error.set(null);
    
    if (vista === 'historial') {
      this.cargarHistorial();
    }
  }

  async buscarTrazabilidad(): Promise<void> {
    const loteId = this.loteSeleccionado();
    if (!loteId || loteId.trim() === '') {
      this.error.set('Ingrese un ID de lote válido');
      return;
    }

    await this.cargarTrazabilidad(loteId);
  }

  onFiltroChange(): void {
    this.filtros.page = 1;
    this.cargarHistorial();
  }

  onPageChange(page: number): void {
    this.filtros.page = page;
    this.cargarHistorial();
  }

  onSortChange(sortBy: string): void {
    if (this.filtros.sortBy === sortBy) {
      this.filtros.sortDesc = !this.filtros.sortDesc;
    } else {
      this.filtros.sortBy = sortBy;
      this.filtros.sortDesc = false;
    }
    this.onFiltroChange();
  }

  // Navegación
  navegarADashboard(): void {
    this.router.navigate(['../dashboard'], { relativeTo: this.route });
  }

  navegarATraslados(): void {
    this.router.navigate(['../traslados'], { relativeTo: this.route });
  }

  navegarAMovimientos(): void {
    this.router.navigate(['../movimientos'], { relativeTo: this.route });
  }

  // Utilidades
  formatearFecha(fecha: Date | string): string {
    if (!fecha) return '—';
    const date = typeof fecha === 'string' ? new Date(fecha) : fecha;
    return date.toLocaleDateString('es-CO', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatearNumero(numero: number): string {
    return numero.toLocaleString('es-CO');
  }

  calcularDiferencia(antes: number, despues: number): number {
    return despues - antes;
  }

  getDiferenciaClass(diferencia: number): string {
    if (diferencia > 0) return 'positive';
    if (diferencia < 0) return 'negative';
    return 'neutral';
  }

  getDiferenciaIcon(diferencia: number): string {
    if (diferencia > 0) return '📈';
    if (diferencia < 0) return '📉';
    return '➖';
  }

  getTipoEventoIcon(tipo: string): string {
    const iconos: Record<string, string> = {
      'Creación': '🆕',
      'Movimiento': '🚚',
      'Ajuste': '⚖️',
      'Venta': '💰',
      'Mortalidad': '💀',
      'Selección': '🎯'
    };
    return iconos[tipo] || '📝';
  }

  limpiarFiltros(): void {
    this.filtros = {
      sortBy: 'fecha_registro',
      sortDesc: true,
      page: 1,
      pageSize: 20
    };
    this.cargarHistorial();
  }

  // Trackby functions para optimización
  trackByHistorialId(index: number, item: HistorialInventarioDto): number {
    return item.id;
  }

  trackByEventoFecha(index: number, item: any): string {
    return item.fecha;
  }
}

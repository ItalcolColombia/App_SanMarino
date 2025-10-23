import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { 
  TrasladosAvesService, 
  MovimientoAvesDto, 
  MovimientoAvesSearchRequest, 
  PagedResult
} from '../../services/traslados-aves.service';

@Component({
  selector: 'app-movimientos-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, SidebarComponent],
  templateUrl: './movimientos-list.component.html',
  styleUrls: ['./movimientos-list.component.scss']
})
export class MovimientosListComponent implements OnInit {
  // Signals para manejo de estado reactivo
  movimientos = signal<MovimientoAvesDto[]>([]);
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  totalRecords = signal<number>(0);
  currentPage = signal<number>(1);

  // Filtros
  filtros: MovimientoAvesSearchRequest = {
    sortBy: 'fecha_movimiento',
    sortDesc: true,
    page: 1,
    pageSize: 20
  };

  // Computed properties
  hasData = computed(() => this.movimientos().length > 0);
  hasError = computed(() => !!this.error());
  isLoading = computed(() => this.loading());
  totalPages = computed(() => Math.ceil(this.totalRecords() / this.filtros.pageSize));

  // Opciones para filtros
  tiposMovimiento = [
    { value: '', label: 'Todos los tipos' },
    { value: 'Traslado', label: 'Traslado' },
    { value: 'Venta', label: 'Venta' },
    { value: 'Mortalidad', label: 'Mortalidad' },
    { value: 'Selección', label: 'Selección' },
    { value: 'Ajuste', label: 'Ajuste' }
  ];

  estados = [
    { value: '', label: 'Todos los estados' },
    { value: 'Pendiente', label: 'Pendiente' },
    { value: 'Procesado', label: 'Procesado' },
    { value: 'Completado', label: 'Completado' },
    { value: 'Cancelado', label: 'Cancelado' }
  ];

  constructor(
    private trasladosService: TrasladosAvesService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.cargarMovimientos();
  }

  async cargarMovimientos(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const result = await this.trasladosService.searchMovimientos(this.filtros).toPromise();
      if (result) {
        this.movimientos.set(result.items);
        this.totalRecords.set(result.total);
        this.currentPage.set(result.page);
      }
    } catch (error: any) {
      console.error('Error al cargar movimientos:', error);
      this.error.set(error.message || 'Error al cargar los movimientos');
      this.movimientos.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  onFiltroChange(): void {
    this.filtros.page = 1;
    this.cargarMovimientos();
  }

  onPageChange(page: number): void {
    this.filtros.page = page;
    this.cargarMovimientos();
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

  navegarAHistorial(): void {
    this.router.navigate(['../historial'], { relativeTo: this.route });
  }

  // Acciones de movimientos
  async verDetalleMovimiento(id: number): Promise<void> {
    try {
      const movimiento = await this.trasladosService.getMovimientoById(id).toPromise();
      // TODO: Implementar modal de detalle
      console.log('Detalle del movimiento:', movimiento);
    } catch (error: any) {
      console.error('Error al cargar detalle:', error);
      this.error.set(error.message || 'Error al cargar el detalle del movimiento');
    }
  }

  async procesarMovimiento(id: number): Promise<void> {
    if (!confirm('¿Está seguro de que desea procesar este movimiento?')) {
      return;
    }

    try {
      await this.trasladosService.procesarMovimiento(id).toPromise();
      this.cargarMovimientos(); // Recargar lista
    } catch (error: any) {
      console.error('Error al procesar movimiento:', error);
      this.error.set(error.message || 'Error al procesar el movimiento');
    }
  }

  async cancelarMovimiento(id: number): Promise<void> {
    const motivo = prompt('Ingrese el motivo de cancelación:');
    if (!motivo || motivo.trim() === '') {
      return;
    }

    try {
      await this.trasladosService.cancelarMovimiento(id, motivo).toPromise();
      this.cargarMovimientos(); // Recargar lista
    } catch (error: any) {
      console.error('Error al cancelar movimiento:', error);
      this.error.set(error.message || 'Error al cancelar el movimiento');
    }
  }

  // Utilidades
  calcularTotalAves(movimiento: MovimientoAvesDto): number {
    return movimiento.cantidadHembras + movimiento.cantidadMachos;
  }

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

  getEstadoClass(estado: string): string {
    const estados: Record<string, string> = {
      'Pendiente': 'warning',
      'Procesado': 'info',
      'Completado': 'success',
      'Cancelado': 'danger'
    };
    return estados[estado] || 'secondary';
  }

  getTipoMovimientoIcon(tipo: string): string {
    const iconos: Record<string, string> = {
      'Traslado': '🚚',
      'Venta': '💰',
      'Mortalidad': '💀',
      'Selección': '🎯',
      'Ajuste': '⚖️'
    };
    return iconos[tipo] || '📦';
  }

  limpiarFiltros(): void {
    this.filtros = {
      sortBy: 'fecha_movimiento',
      sortDesc: true,
      page: 1,
      pageSize: 20
    };
    this.cargarMovimientos();
  }

  // Trackby functions para optimización
  trackByMovimientoId(index: number, item: MovimientoAvesDto): number {
    return item.id;
  }

  // Validaciones para acciones
  puedeSerProcesado(movimiento: MovimientoAvesDto): boolean {
    // Lógica para determinar si un movimiento puede ser procesado
    // Por ahora, asumimos que solo los pendientes pueden ser procesados
    return true; // TODO: Implementar lógica real basada en estado
  }

  puedeSerCancelado(movimiento: MovimientoAvesDto): boolean {
    // Lógica para determinar si un movimiento puede ser cancelado
    // Por ahora, asumimos que solo los pendientes y procesados pueden ser cancelados
    return true; // TODO: Implementar lógica real basada en estado
  }
}

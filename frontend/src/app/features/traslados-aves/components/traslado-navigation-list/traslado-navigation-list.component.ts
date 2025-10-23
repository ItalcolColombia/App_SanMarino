// src/app/features/traslados-aves/components/traslado-navigation-list/traslado-navigation-list.component.ts
import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TrasladoNavigationService, MovimientoAvesCompleto, MovimientoAvesCompletoSearchRequest } from '../../../../core/services/traslado-navigation/traslado-navigation.service';
import { TrasladoNavigationCardComponent } from '../traslado-navigation-card/traslado-navigation-card.component';

@Component({
  selector: 'app-traslado-navigation-list',
  standalone: true,
  imports: [CommonModule, FormsModule, TrasladoNavigationCardComponent],
  templateUrl: './traslado-navigation-list.component.html',
  styleUrls: ['./traslado-navigation-list.component.scss']
})
export class TrasladoNavigationListComponent implements OnInit {
  // Signals para el estado del componente
  movimientos = signal<MovimientoAvesCompleto[]>([]);
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  
  // Filtros
  filtros = signal<MovimientoAvesCompletoSearchRequest>({
    page: 1,
    pageSize: 20,
    sortBy: 'fecha_movimiento',
    sortDesc: true
  });
  
  // Paginación
  totalItems = signal<number>(0);
  currentPage = signal<number>(1);
  pageSize = signal<number>(20);
  
  // Estados de filtros
  showFilters = signal<boolean>(false);
  selectedEstado = signal<string>('');
  selectedTipo = signal<string>('');
  fechaDesde = signal<string>('');
  fechaHasta = signal<string>('');
  
  // Computed properties
  totalPages = computed(() => Math.ceil(this.totalItems() / this.pageSize()));
  hasResults = computed(() => this.movimientos().length > 0);
  isLoadingData = computed(() => this.loading() && this.movimientos().length === 0);
  
  // Opciones para filtros
  estados = [
    { value: '', label: 'Todos los estados' },
    { value: 'Pendiente', label: 'Pendiente' },
    { value: 'Completado', label: 'Completado' },
    { value: 'Cancelado', label: 'Cancelado' }
  ];
  
  tipos = [
    { value: '', label: 'Todos los tipos' },
    { value: 'Traslado', label: 'Traslado' },
    { value: 'Ajuste', label: 'Ajuste' },
    { value: 'Liquidacion', label: 'Liquidación' }
  ];

  constructor(private trasladoService: TrasladoNavigationService) {}

  ngOnInit(): void {
    this.cargarMovimientos();
  }

  /**
   * Carga los movimientos con los filtros actuales
   */
  async cargarMovimientos(): Promise<void> {
    try {
      this.loading.set(true);
      this.error.set(null);
      
      const request: MovimientoAvesCompletoSearchRequest = {
        ...this.filtros(),
        estado: this.selectedEstado() || undefined,
        tipoMovimiento: this.selectedTipo() || undefined,
        fechaDesde: this.fechaDesde() || undefined,
        fechaHasta: this.fechaHasta() || undefined,
        page: this.currentPage(),
        pageSize: this.pageSize()
      };
      
      const response = await this.trasladoService.searchCompleto(request).toPromise();
      
      if (response) {
        this.movimientos.set(response.items || []);
        this.totalItems.set(response.total || 0);
      }
    } catch (err: any) {
      console.error('Error cargando movimientos:', err);
      this.error.set('Error al cargar los movimientos. Inténtalo de nuevo.');
      this.movimientos.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Aplica los filtros y recarga los datos
   */
  async aplicarFiltros(): Promise<void> {
    this.currentPage.set(1);
    await this.cargarMovimientos();
  }

  /**
   * Limpia todos los filtros
   */
  async limpiarFiltros(): Promise<void> {
    this.selectedEstado.set('');
    this.selectedTipo.set('');
    this.fechaDesde.set('');
    this.fechaHasta.set('');
    this.currentPage.set(1);
    await this.cargarMovimientos();
  }

  /**
   * Cambia la página
   */
  async cambiarPagina(pagina: number): Promise<void> {
    if (pagina >= 1 && pagina <= this.totalPages()) {
      this.currentPage.set(pagina);
      await this.cargarMovimientos();
    }
  }

  /**
   * Cambia el tamaño de página
   */
  async cambiarTamanoPagina(tamano: number): Promise<void> {
    this.pageSize.set(tamano);
    this.currentPage.set(1);
    await this.cargarMovimientos();
  }

  /**
   * Cambia el ordenamiento
   */
  async cambiarOrdenamiento(campo: string): Promise<void> {
    const filtrosActuales = this.filtros();
    const nuevoFiltros = {
      ...filtrosActuales,
      sortBy: campo,
      sortDesc: filtrosActuales.sortBy === campo ? !filtrosActuales.sortDesc : true
    };
    
    this.filtros.set(nuevoFiltros);
    await this.cargarMovimientos();
  }

  /**
   * Alterna la visibilidad de los filtros
   */
  toggleFiltros(): void {
    this.showFilters.update(show => !show);
  }

  /**
   * Obtiene las páginas para la paginación
   */
  getPaginas(): number[] {
    const total = this.totalPages();
    const actual = this.currentPage();
    const paginas: number[] = [];
    
    // Mostrar máximo 5 páginas
    const inicio = Math.max(1, actual - 2);
    const fin = Math.min(total, actual + 2);
    
    for (let i = inicio; i <= fin; i++) {
      paginas.push(i);
    }
    
    return paginas;
  }

  /**
   * Obtiene el texto del ordenamiento actual
   */
  getTextoOrdenamiento(): string {
    const filtros = this.filtros();
    const campo = filtros.sortBy || 'fecha_movimiento';
    const desc = filtros.sortDesc;
    
    const campos: { [key: string]: string } = {
      'fecha_movimiento': 'Fecha',
      'numero_movimiento': 'Número',
      'estado': 'Estado',
      'tipo_movimiento': 'Tipo'
    };
    
    const direccion = desc ? '↓' : '↑';
    return `${campos[campo] || campo} ${direccion}`;
  }

  /**
   * Obtiene el resumen de filtros activos
   */
  getResumenFiltros(): string {
    const filtros: string[] = [];
    
    if (this.selectedEstado()) filtros.push(`Estado: ${this.selectedEstado()}`);
    if (this.selectedTipo()) filtros.push(`Tipo: ${this.selectedTipo()}`);
    if (this.fechaDesde()) filtros.push(`Desde: ${this.fechaDesde()}`);
    if (this.fechaHasta()) filtros.push(`Hasta: ${this.fechaHasta()}`);
    
    return filtros.length > 0 ? filtros.join(', ') : 'Sin filtros';
  }

  /**
   * Refresca los datos
   */
  async refrescar(): Promise<void> {
    await this.cargarMovimientos();
  }
}

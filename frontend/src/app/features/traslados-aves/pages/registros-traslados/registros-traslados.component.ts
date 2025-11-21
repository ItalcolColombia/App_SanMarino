import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { HierarchicalFilterComponent } from '../../../../shared/components/hierarchical-filter/hierarchical-filter.component';
import { 
  TrasladosAvesService, 
  HistorialTrasladoLoteDto,
  TrasladoHuevosDto,
} from '../../services/traslados-aves.service';
import { TrasladoNavigationService, TrasladoUnificado, MovimientoAvesCompleto } from '../../../../core/services/traslado-navigation/traslado-navigation.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';

@Component({
  selector: 'app-registros-traslados',
  standalone: true,
  imports: [CommonModule, FormsModule, SidebarComponent, HierarchicalFilterComponent],
  templateUrl: './registros-traslados.component.html',
  styleUrls: ['./registros-traslados.component.scss']
})
export class RegistrosTrasladosComponent implements OnInit {
  // ====== Signals para estado reactivo ======
  selectedFarmId = signal<number | null>(null);
  tabActivo = signal<'lotes' | 'huevos' | 'aves'>('lotes');
  
  // ====== Datos de registros ======
  historialTrasladosLotes = signal<HistorialTrasladoLoteDto[]>([]);
  loadingTrasladosLotes = signal<boolean>(false);
  
  trasladosHuevos = signal<TrasladoHuevosDto[]>([]);
  loadingTrasladosHuevos = signal<boolean>(false);
  
  trasladosAves = signal<MovimientoAvesCompleto[]>([]);
  loadingTrasladosAves = signal<boolean>(false);
  
  error = signal<string | null>(null);

  // ====== Catálogos ======
  farms: FarmDto[] = [];
  farmMap: Record<number, string> = {};

  constructor(
    private trasladosService: TrasladosAvesService,
    private trasladoNavigationService: TrasladoNavigationService,
    private farmService: FarmService
  ) {}

  ngOnInit(): void {
    this.cargarFarms();
  }

  // ===================== Cargar Datos Maestros ====================
  private async cargarFarms(): Promise<void> {
    try {
      const farms = await firstValueFrom(this.farmService.getAll());
      this.farms = farms || [];
      this.farms.forEach(f => this.farmMap[f.id] = f.name);
    } catch (err: any) {
      console.error('Error al cargar granjas:', err);
    }
  }

  // ===================== Manejo de Selección de Granja ====================
  onFarmSelected(farmId: number | null): void {
    this.selectedFarmId.set(farmId);
    if (farmId) {
      this.cargarTodosLosRegistros(farmId);
    } else {
      this.limpiarRegistros();
    }
  }

  limpiarSeleccion(): void {
    this.selectedFarmId.set(null);
    this.limpiarRegistros();
  }

  // ===================== Cargar Registros ====================
  private async cargarTodosLosRegistros(granjaId: number): Promise<void> {
    await Promise.all([
      this.cargarTrasladosLotes(granjaId),
      this.cargarTrasladosHuevos(granjaId),
      this.cargarTrasladosAves(granjaId)
    ]);
  }

  private async cargarTrasladosLotes(granjaId: number): Promise<void> {
    this.loadingTrasladosLotes.set(true);
    try {
      const historial = await firstValueFrom(
        this.trasladosService.getHistorialTrasladosLotesPorGranja(granjaId)
      );
      this.historialTrasladosLotes.set(historial || []);
    } catch (err: any) {
      console.error('Error al cargar traslados de lotes:', err);
      this.error.set(err.message || 'Error al cargar traslados de lotes');
      this.historialTrasladosLotes.set([]);
    } finally {
      this.loadingTrasladosLotes.set(false);
    }
  }

  private async cargarTrasladosHuevos(granjaId: number): Promise<void> {
    this.loadingTrasladosHuevos.set(true);
    try {
      const traslados = await firstValueFrom(
        this.trasladosService.getTrasladosHuevosPorGranja(granjaId)
      );
      this.trasladosHuevos.set(traslados || []);
    } catch (err: any) {
      console.error('Error al cargar traslados de huevos:', err);
      this.error.set(err.message || 'Error al cargar traslados de huevos');
      this.trasladosHuevos.set([]);
    } finally {
      this.loadingTrasladosHuevos.set(false);
    }
  }

  private async cargarTrasladosAves(granjaId: number): Promise<void> {
    this.loadingTrasladosAves.set(true);
    try {
      const traslados = await firstValueFrom(
        this.trasladoNavigationService.getByGranja(granjaId, 100)
      );
      // getByGranja ya devuelve solo movimientos de aves
      this.trasladosAves.set(traslados || []);
    } catch (err: any) {
      console.error('Error al cargar traslados de aves:', err);
      this.error.set(err.message || 'Error al cargar traslados de aves');
      this.trasladosAves.set([]);
    } finally {
      this.loadingTrasladosAves.set(false);
    }
  }

  private limpiarRegistros(): void {
    this.historialTrasladosLotes.set([]);
    this.trasladosHuevos.set([]);
    this.trasladosAves.set([]);
    this.error.set(null);
  }

  // ===================== Utilidades ====================
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

  obtenerNombreGranja(granjaId: number): string {
    return this.farmMap[granjaId] || `Granja #${granjaId}`;
  }
}


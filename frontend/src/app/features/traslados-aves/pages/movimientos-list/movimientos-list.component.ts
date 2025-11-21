import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { HierarchicalFilterComponent } from '../../../../shared/components/hierarchical-filter/hierarchical-filter.component';
import { 
  TrasladosAvesService, 
  HistorialTrasladoLoteDto,
  TrasladoHuevosDto,
} from '../../services/traslados-aves.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { TrasladoNavigationService, TrasladoUnificado } from '../../../../core/services/traslado-navigation/traslado-navigation.service';

@Component({
  selector: 'app-movimientos-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, SidebarComponent, HierarchicalFilterComponent],
  templateUrl: './movimientos-list.component.html',
  styleUrls: ['./movimientos-list.component.scss']
})
export class MovimientosListComponent implements OnInit {
  // ====== Signals para estado reactivo ======
  loteSeleccionado = signal<LoteDto | null>(null);
  loteCompleto = signal<LoteDto | null>(null);
  
  // ====== Tabs de Registros ======
  tabRegistrosActivo = signal<'huevos' | 'aves' | 'lotes'>('huevos');
  
  // ====== Datos de registros ======
  historialTrasladosLote = signal<HistorialTrasladoLoteDto[]>([]);
  loadingHistorialLotes = signal<boolean>(false);
  movimientosAvesLote = signal<TrasladoUnificado[]>([]);
  loadingMovimientos = signal<boolean>(false);
  trasladosHuevosLote = signal<TrasladoHuevosDto[]>([]);
  loadingTrasladosHuevos = signal<boolean>(false);

  constructor(
    private trasladosService: TrasladosAvesService,
    private loteService: LoteService,
    private trasladoNavigationService: TrasladoNavigationService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    // No cargar nada al inicio, esperar selección de lote
  }

  // ===================== Manejo de Selección de Lote ====================
  onLoteSelected(lote: LoteDto | null): void {
    this.loteSeleccionado.set(lote);
    if (lote) {
      this.tabRegistrosActivo.set('huevos'); // Reset to default tab
      this.cargarRegistrosLote(lote.loteId);
    } else {
      this.limpiarRegistros();
    }
  }

  limpiarSeleccionLote(): void {
    this.loteSeleccionado.set(null);
    this.limpiarRegistros();
  }

  // ===================== Cargar Registros del Lote ====================
  private async cargarRegistrosLote(loteId: number): Promise<void> {
    const loteIdStr = String(loteId);
    await Promise.all([
      this.cargarMovimientosLote(loteId),
      this.cargarHistorialTrasladosLote(loteId),
      this.cargarTrasladosHuevosLote(loteIdStr)
    ]);
  }

  private async cargarMovimientosLote(loteId: number): Promise<void> {
    this.loadingMovimientos.set(true);
    try {
      console.log(`[DEBUG] Cargando movimientos para lote ${loteId}`);
      const movimientos = await firstValueFrom(
        this.trasladoNavigationService.getByLote(loteId, 100)
      );
      console.log(`[DEBUG] Movimientos recibidos:`, movimientos);
      // Filtrar solo movimientos de aves
      const movimientosAves = movimientos?.filter(m => m.tipoTraslado === 'Aves') || [];
      console.log(`[DEBUG] Movimientos de aves filtrados:`, movimientosAves);
      this.movimientosAvesLote.set(movimientosAves);
    } catch (err: any) {
      console.error('Error al cargar movimientos del lote:', err);
      this.movimientosAvesLote.set([]);
    } finally {
      this.loadingMovimientos.set(false);
    }
  }

  private async cargarHistorialTrasladosLote(loteId: number): Promise<void> {
    this.loadingHistorialLotes.set(true);
    try {
      console.log(`[DEBUG] Cargando historial de traslados para lote ${loteId}`);
      const historial = await firstValueFrom(
        this.trasladosService.getHistorialTrasladosLote(loteId)
      );
      console.log(`[DEBUG] Historial recibido:`, historial);
      this.historialTrasladosLote.set(historial || []);
    } catch (err: any) {
      console.error('Error al cargar historial de traslados de lotes:', err);
      this.historialTrasladosLote.set([]);
    } finally {
      this.loadingHistorialLotes.set(false);
    }
  }

  private async cargarTrasladosHuevosLote(loteId: string): Promise<void> {
    this.loadingTrasladosHuevos.set(true);
    try {
      console.log(`[DEBUG] Cargando traslados de huevos para lote ${loteId}`);
      const traslados = await firstValueFrom(
        this.trasladosService.getTrasladosHuevosPorLote(loteId)
      );
      console.log(`[DEBUG] Traslados de huevos recibidos:`, traslados);
      this.trasladosHuevosLote.set(traslados || []);
    } catch (err: any) {
      console.error('Error al cargar traslados de huevos:', err);
      this.trasladosHuevosLote.set([]);
    } finally {
      this.loadingTrasladosHuevos.set(false);
    }
  }

  private limpiarRegistros(): void {
    this.historialTrasladosLote.set([]);
    this.movimientosAvesLote.set([]);
    this.trasladosHuevosLote.set([]);
    this.loteCompleto.set(null);
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

  // ===================== Navegación ====================
  navegarADashboard(): void {
    this.router.navigate(['../dashboard'], { relativeTo: this.route });
  }

  navegarATraslados(): void {
    this.router.navigate(['../traslados'], { relativeTo: this.route });
  }
}

import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  MapasService,
  MapaDetailDto,
  MapaPasoDto,
  EjecutarMapaRequest,
  EjecutarMapaResponse,
  MapaEjecucionEstadoDto,
  MapaEjecucionHistorialDto
} from '../../services/mapas.service';

@Component({
  selector: 'app-mapa-ejecutar-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './mapa-ejecutar-modal.component.html',
  styleUrls: ['./mapa-ejecutar-modal.component.scss']
})
export class MapaEjecutarModalComponent implements OnInit, OnDestroy {
  @Input() mapaId!: number;
  @Output() close = new EventEmitter<void>();

  mapa: MapaDetailDto | null = null;
  loading = false;
  error: string | null = null;
  fechaDesde = '';
  fechaHasta = '';
  formatoExport = 'excel';
  running = false;
  ejecucionId: number | null = null;
  estadoEjecucion: MapaEjecucionEstadoDto | null = null;
  descargando = false;
  descargandoId: number | null = null;
  historial: MapaEjecucionHistorialDto[] = [];
  private pollInterval: ReturnType<typeof setInterval> | null = null;

  get pasos(): MapaPasoDto[] {
    const m = this.mapa as Record<string, unknown> | null;
    if (!m) return [];
    const arr = (m['pasos'] ?? m['Pasos']) as MapaPasoDto[] | undefined;
    return Array.isArray(arr) ? arr : [];
  }

  get canExecute(): boolean {
    const list = this.pasos;
    if (!list.length) return false;
    return list.some(p => {
      const row = p as unknown as Record<string, unknown>;
      const tipo = (p.tipo ?? row['Tipo'] ?? '').toString().toLowerCase();
      const sql = (p.scriptSql ?? row['ScriptSql'] ?? '').toString().trim();
      return (tipo === 'export' || tipo.includes('export')) && sql.length > 0;
    });
  }

  get progressPercent(): number {
    const e = this.estadoEjecucion;
    if (!e || e.estado !== 'en_proceso' || e.totalPasos == null || e.totalPasos < 1) return 0;
    const actual = e.pasoActual ?? 0;
    return Math.min(100, Math.round((actual / e.totalPasos) * 100));
  }

  constructor(private mapasService: MapasService) {}

  ngOnInit(): void {
    this.loadMapa();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  onClose(): void {
    this.close.emit();
  }

  loadMapa(): void {
    this.loading = true;
    this.error = null;
    this.mapasService.getById(this.mapaId).subscribe({
      next: (m) => {
        this.mapa = m;
        this.loading = false;
        this.loadHistorial();
      },
      error: (err) => {
        this.error = err?.error?.message || err?.message || 'Error al cargar el mapa';
        this.loading = false;
      }
    });
  }

  ejecutar(): void {
    const request: EjecutarMapaRequest = {
      fechaDesde: this.fechaDesde ? this.fechaDesde + 'T00:00:00.000Z' : null,
      fechaHasta: this.fechaHasta ? this.fechaHasta + 'T23:59:59.999Z' : null,
      granjaIds: null,
      formatoExport: this.formatoExport || 'excel'
    };
    this.running = true;
    this.error = null;
    this.mapasService.ejecutar(this.mapaId, request).subscribe({
      next: (res: EjecutarMapaResponse) => {
        this.running = false;
        this.ejecucionId = res.ejecucionId;
        this.estadoEjecucion = {
          id: res.ejecucionId,
          mapaId: this.mapaId,
          estado: res.estado,
          mensajeError: null,
          fechaEjecucion: new Date().toISOString(),
          puedeDescargar: false
        };
        this.startPolling();
      },
      error: (err) => {
        this.running = false;
        this.error = err?.error?.message || err?.message || 'Error al iniciar la ejecución';
      }
    });
  }

  private startPolling(): void {
    this.stopPolling();
    if (this.ejecucionId == null) return;
    const poll = () => {
      this.mapasService.getEjecucionEstado(this.ejecucionId!).subscribe({
        next: (estado) => {
          this.estadoEjecucion = estado;
          if (estado.estado === 'completado' || estado.estado === 'error') {
            this.stopPolling();
            this.loadHistorial();
          }
        },
        error: () => {}
      });
    };
    poll();
    this.pollInterval = setInterval(poll, 2000);
  }

  private stopPolling(): void {
    if (this.pollInterval != null) {
      clearInterval(this.pollInterval);
      this.pollInterval = null;
    }
  }

  descargar(): void {
    if (this.ejecucionId == null) return;
    this.descargando = true;
    this.mapasService.descargarEjecucion(this.ejecucionId).subscribe({
      next: ({ blob, fileName }) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.click();
        URL.revokeObjectURL(url);
        this.descargando = false;
      },
      error: (err) => {
        this.error = err?.error?.message || err?.message || 'Error al descargar';
        this.descargando = false;
      }
    });
  }

  loadHistorial(): void {
    this.mapasService.getEjecucionesByMapa(this.mapaId, 10).subscribe({
      next: (list) => this.historial = list,
      error: () => {}
    });
  }

  descargarPorId(ejecucionId: number): void {
    this.descargandoId = ejecucionId;
    this.mapasService.descargarEjecucion(ejecucionId).subscribe({
      next: ({ blob, fileName }) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.click();
        URL.revokeObjectURL(url);
        this.descargandoId = null;
      },
      error: (err) => {
        this.error = err?.error?.message || err?.message || 'Error al descargar';
        this.descargandoId = null;
      }
    });
  }

  formatDate(s: string): string {
    try {
      const d = new Date(s);
      return isNaN(d.getTime()) ? s : d.toLocaleString();
    } catch {
      return s;
    }
  }

  reiniciar(): void {
    this.ejecucionId = null;
    this.estadoEjecucion = null;
    this.stopPolling();
    this.loadHistorial();
  }
}

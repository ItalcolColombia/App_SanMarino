// features/migraciones-masivas/pages/migraciones-masivas-page/migraciones-masivas-page.component.ts
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  HierarchicalFilterComponent,
  HierarchicalFilterCriteria
} from '../../../../shared/components/hierarchical-filter/hierarchical-filter.component';
import { CompanySelectorComponent } from '../../../../shared/components/company-selector/company-selector.component';
import { SelectorTipoMigracionComponent } from '../../components/selector-tipo-migracion/selector-tipo-migracion.component';
import { PanelPlantillaUploadComponent } from '../../components/panel-plantilla-upload/panel-plantilla-upload.component';
import { HistorialMigracionesComponent } from '../../components/historial-migraciones/historial-migraciones.component';
import { MigracionService } from '../../services/migracion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { TipoMigracionInfo, MigracionContexto, LoteElegible, ReproductoraElegible } from '../../models/migracion.model';

/**
 * Página orquestadora del módulo de Migraciones Masivas (Postura).
 * Usa SIGNALS + OnPush: el interceptor cifra el SECRET_UP con Web Crypto (crypto.subtle), lo que
 * hace que las respuestas HTTP emitan FUERA de la zona de Angular; con signals el refresco de vista
 * no depende de Zone.js, evitando que la pantalla quede "cargando" con los datos ya recibidos.
 *
 * El LOTE se elige de los ELEGIBLES del módulo (`/api/Migracion/elegibles`), no del selector de
 * lotes genérico del filtro jerárquico: cada tipo tiene su propia fuente (p.ej. Engorde usa
 * lote_ave_engorde) y el selector genérico mostraba 0 lotes para esos tipos.
 */
@Component({
  selector: 'app-migraciones-masivas-page',
  standalone: true,
  imports: [
    CommonModule,
    HierarchicalFilterComponent,
    CompanySelectorComponent,
    SelectorTipoMigracionComponent,
    PanelPlantillaUploadComponent,
    HistorialMigracionesComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './migraciones-masivas-page.component.html',
  styleUrl: './migraciones-masivas-page.component.scss'
})
export class MigracionesMasivasPageComponent implements OnInit {
  private readonly svc = inject(MigracionService);
  private readonly toast = inject(ToastService);

  readonly tipos = signal<TipoMigracionInfo[]>([]);
  readonly cargandoTipos = signal(true);
  readonly seleccionado = signal<TipoMigracionInfo | null>(null);
  readonly contexto = signal<MigracionContexto>({});
  /** Bump para que <app-historial-migraciones> refresque tras una importación real. */
  readonly historialRefreshTick = signal(0);

  /** Lotes elegibles del tipo seleccionado (fuente: /api/Migracion/elegibles, filtrada por la cascada). */
  readonly lotesElegibles = signal<LoteElegible[]>([]);
  readonly cargandoLotes = signal(false);
  /** Reproductoras del lote elegido (solo tipo Seguimiento Reproductora Engorde; selector opcional). */
  readonly reproductoras = signal<ReproductoraElegible[]>([]);
  readonly cargandoReproductoras = signal(false);

  readonly esTipoReproductora = computed(() => this.seleccionado()?.codigo === 'SeguimientoReproductoraEngorde');

  ngOnInit(): void {
    this.svc.getTipos().subscribe({
      next: (t) => { this.tipos.set(t); this.cargandoTipos.set(false); },
      error: () => { this.cargandoTipos.set(false); this.toast.error('No se pudieron cargar los tipos de migración.'); }
    });
  }

  onSeleccionar(tipo: TipoMigracionInfo): void {
    this.seleccionado.set(tipo);
    this.contexto.set({});
    this.lotesElegibles.set([]);
    this.reproductoras.set([]);
    if (tipo.requiereLote && tipo.disponible) this.cargarElegibles(tipo, {});
  }

  onFilterChange(c: HierarchicalFilterCriteria): void {
    const ctx: MigracionContexto = {
      granjaId: c.farmId ?? null,
      nucleoId: c.nucleoId ?? null,
      galponId: c.galponId ?? null,
      loteId: null,
      reproductoraId: null
    };
    this.contexto.set(ctx);
    this.reproductoras.set([]);
    const tipo = this.seleccionado();
    if (tipo?.requiereLote && tipo.disponible) this.cargarElegibles(tipo, ctx);
  }

  onLoteChange(valor: string): void {
    const loteId = valor ? Number(valor) : null;
    this.contexto.update(ctx => ({ ...ctx, loteId, reproductoraId: null }));
    this.reproductoras.set([]);
    if (loteId != null && this.esTipoReproductora()) this.cargarReproductoras(loteId);
  }

  onReproductoraChange(valor: string): void {
    this.contexto.update(ctx => ({ ...ctx, reproductoraId: valor ? Number(valor) : null }));
  }

  /** Etiqueta del lote en el selector: nombre + galpón (los nombres pueden repetirse entre galpones). */
  etiquetaLote(l: LoteElegible): string {
    return l.galponId ? `${l.loteNombre} — Galpón ${l.galponId}` : l.loteNombre;
  }

  /** Etiqueta de la reproductora: nombre (id) + avance de la primera semana. */
  etiquetaReproductora(r: ReproductoraElegible): string {
    return `${r.nombre} (${r.reproductoraId}) — ${r.cargados}/7 días, ${r.confirmados} confirmados`;
  }

  private cargarElegibles(tipo: TipoMigracionInfo, ctx: MigracionContexto): void {
    this.cargandoLotes.set(true);
    this.svc.getElegibles(tipo.codigo, ctx).subscribe({
      next: (lotes) => { this.lotesElegibles.set(lotes); this.cargandoLotes.set(false); },
      error: () => {
        this.lotesElegibles.set([]);
        this.cargandoLotes.set(false);
        this.toast.error('No se pudieron cargar los lotes elegibles.');
      }
    });
  }

  private cargarReproductoras(loteId: number): void {
    this.cargandoReproductoras.set(true);
    this.svc.getReproductoras(loteId).subscribe({
      next: (repros) => { this.reproductoras.set(repros); this.cargandoReproductoras.set(false); },
      error: () => {
        this.reproductoras.set([]);
        this.cargandoReproductoras.set(false);
        this.toast.error('No se pudieron cargar las reproductoras del lote.');
      }
    });
  }

  /** Una importación real terminó: el historial (siempre visible al pie) se refresca solo. */
  onImportado(): void {
    this.historialRefreshTick.update(n => n + 1);
  }
}

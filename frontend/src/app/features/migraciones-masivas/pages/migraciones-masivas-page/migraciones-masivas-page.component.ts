// features/migraciones-masivas/pages/migraciones-masivas-page/migraciones-masivas-page.component.ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
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
import { TipoMigracionInfo, MigracionContexto } from '../../models/migracion.model';

/**
 * Página orquestadora del módulo de Migraciones Masivas (Postura).
 * Usa SIGNALS + OnPush: el interceptor cifra el SECRET_UP con Web Crypto (crypto.subtle), lo que
 * hace que las respuestas HTTP emitan FUERA de la zona de Angular; con signals el refresco de vista
 * no depende de Zone.js, evitando que la pantalla quede "cargando" con los datos ya recibidos.
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

  ngOnInit(): void {
    this.svc.getTipos().subscribe({
      next: (t) => { this.tipos.set(t); this.cargandoTipos.set(false); },
      error: () => { this.cargandoTipos.set(false); this.toast.error('No se pudieron cargar los tipos de migración.'); }
    });
  }

  onSeleccionar(tipo: TipoMigracionInfo): void {
    this.seleccionado.set(tipo);
    this.contexto.set({});
  }

  onFilterChange(c: HierarchicalFilterCriteria): void {
    this.contexto.update(ctx => ({
      ...ctx,
      granjaId: c.farmId ?? null,
      nucleoId: c.nucleoId ?? null,
      galponId: c.galponId ?? null
    }));
  }

  onLoteSelected(lote: { loteId: number } | null): void {
    this.contexto.update(ctx => ({ ...ctx, loteId: lote?.loteId ?? null }));
  }

  /** Una importación real terminó: el historial (siempre visible al pie) se refresca solo. */
  onImportado(): void {
    this.historialRefreshTick.update(n => n + 1);
  }
}

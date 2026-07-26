// src/app/features/config/user-management/components/asignar-usuario-granja/configurar-alcance-granja.component.ts
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faGlobe, faLayerGroup, faTimes, faSave, faWarehouse, faKaaba, faCubes
} from '@fortawesome/free-solid-svg-icons';
import { Subject, forkJoin, takeUntil } from 'rxjs';

import {
  UserFarmService,
  UserFarmScopeConfigDto,
  UserFarmScopeItemDto,
  FarmLocationsTreeDto,
  FarmLocationNucleoNodeDto,
  FarmLocationGalponNodeDto,
  FarmLocationLoteNodeDto
} from '../../../../../core/services/user-farm/user-farm.service';
import { ToastService } from '../../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../../shared/services/confirm-dialog.service';

/**
 * Sub-modal "Alcance dentro de la granja": global (toda la granja) o restringido a
 * núcleos/galpones/lotes seleccionados. Guarda EXACTAMENTE lo marcado (grants mínimos);
 * el backend expande el cierre (núcleo ⇒ sus galpones y lotes; galpón ⇒ sus lotes).
 * Nota: el nivel LOTE aplica a la tabla `lotes` (reproductora levante/producción);
 * los módulos engorde/postura se gobiernan por núcleo/galpón.
 */
@Component({
  selector: 'app-configurar-alcance-granja',
  standalone: true,
  imports: [FormsModule, FontAwesomeModule],
  templateUrl: './configurar-alcance-granja.component.html'
})
export class ConfigurarAlcanceGranjaComponent implements OnChanges, OnDestroy {
  @Input() userId = '';
  @Input() userName = '';
  @Input() farmId = 0;
  @Input() farmName = '';
  @Input() isOpen = false;

  @Output() close = new EventEmitter<void>();
  /** Emite la config guardada para que el padre refresque la fila sin recargar. */
  @Output() scopeUpdated = new EventEmitter<UserFarmScopeConfigDto>();

  faGlobe = faGlobe;
  faLayerGroup = faLayerGroup;
  faTimes = faTimes;
  faSave = faSave;
  faWarehouse = faWarehouse;
  faKaaba = faKaaba;
  faCubes = faCubes;

  loading = false;
  saving = false;
  modo: 'global' | 'restringido' = 'global';
  tree: FarmLocationsTreeDto | null = null;

  // Grants marcados (mínimos; el cierre lo expande el backend)
  private nucleosSel = new Set<string>();
  private galponesSel = new Set<string>();
  private lotesSel = new Set<number>();

  totalSeleccionado = 0;

  private lastLoadedKey = '';
  private destroy$ = new Subject<void>();

  constructor(
    private userFarmService: UserFarmService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService
  ) {}

  ngOnChanges(): void {
    const key = `${this.userId}|${this.farmId}`;
    if (this.isOpen && this.userId && this.farmId > 0 && key !== this.lastLoadedKey) {
      this.lastLoadedKey = key;
      this.loadData();
    }
    if (!this.isOpen) {
      this.lastLoadedKey = '';
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadData(): void {
    this.loading = true;
    this.tree = null;
    this.nucleosSel.clear();
    this.galponesSel.clear();
    this.lotesSel.clear();

    forkJoin({
      scope: this.userFarmService.getUserFarmScope(this.userId, this.farmId),
      tree: this.userFarmService.getFarmLocationsTree(this.farmId)
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ({ scope, tree }) => {
          this.tree = tree;
          this.modo = scope.restrictLocations ? 'restringido' : 'global';
          for (const item of scope.items) {
            if (item.level === 'nucleo' && item.nucleoId) this.nucleosSel.add(item.nucleoId);
            else if (item.level === 'galpon' && item.galponId) this.galponesSel.add(item.galponId);
            else if (item.level === 'lote' && item.loteId != null) this.lotesSel.add(item.loteId);
          }
          this.recalcularTotal();
          this.loading = false;
        },
        error: (err) => {
          console.error('Error cargando alcance de granja:', err);
          this.toast.error('No se pudo cargar el alcance de la granja.');
          this.loading = false;
          this.close.emit();
        }
      });
  }

  // ── Estado de checkboxes (booleans puros: sin alocar por ciclo de CD) ──

  isNucleoChecked(n: FarmLocationNucleoNodeDto): boolean {
    return this.nucleosSel.has(n.nucleoId);
  }

  isGalponChecked(g: FarmLocationGalponNodeDto): boolean {
    return this.galponesSel.has(g.galponId);
  }

  /** El galpón queda cubierto (checked+disabled) si su núcleo está marcado. */
  isGalponCovered(n: FarmLocationNucleoNodeDto): boolean {
    return this.nucleosSel.has(n.nucleoId);
  }

  isLoteChecked(l: FarmLocationLoteNodeDto): boolean {
    return this.lotesSel.has(l.loteId);
  }

  /** El lote queda cubierto si su núcleo o su galpón está marcado. */
  isLoteCovered(n: FarmLocationNucleoNodeDto | null, g: FarmLocationGalponNodeDto | null): boolean {
    if (n && this.nucleosSel.has(n.nucleoId)) return true;
    if (g && this.galponesSel.has(g.galponId)) return true;
    return false;
  }

  toggleNucleo(n: FarmLocationNucleoNodeDto): void {
    if (this.nucleosSel.has(n.nucleoId)) {
      this.nucleosSel.delete(n.nucleoId);
    } else {
      this.nucleosSel.add(n.nucleoId);
      // Grants redundantes fuera: el núcleo ya cubre sus galpones y lotes
      for (const g of n.galpones) {
        this.galponesSel.delete(g.galponId);
        for (const l of g.lotes) this.lotesSel.delete(l.loteId);
      }
      for (const l of n.lotesSinGalpon) this.lotesSel.delete(l.loteId);
    }
    this.recalcularTotal();
  }

  toggleGalpon(g: FarmLocationGalponNodeDto): void {
    if (this.galponesSel.has(g.galponId)) {
      this.galponesSel.delete(g.galponId);
    } else {
      this.galponesSel.add(g.galponId);
      for (const l of g.lotes) this.lotesSel.delete(l.loteId);
    }
    this.recalcularTotal();
  }

  toggleLote(l: FarmLocationLoteNodeDto): void {
    if (this.lotesSel.has(l.loteId)) this.lotesSel.delete(l.loteId);
    else this.lotesSel.add(l.loteId);
    this.recalcularTotal();
  }

  private recalcularTotal(): void {
    this.totalSeleccionado = this.nucleosSel.size + this.galponesSel.size + this.lotesSel.size;
  }

  async guardar(): Promise<void> {
    const restringido = this.modo === 'restringido';

    if (restringido && this.totalSeleccionado === 0) {
      const ok = await this.confirmDialog.ask({
        title: 'Alcance vacío',
        message: `El acceso quedará RESTRINGIDO sin ningún lugar seleccionado: ${this.userName || 'el usuario'} no verá nada de "${this.farmName}" hasta que se le asigne un núcleo, galpón o lote. ¿Guardar así?`,
        type: 'warning',
        confirmText: 'Guardar'
      });
      if (!ok) return;
    }

    const items: UserFarmScopeItemDto[] = [];
    if (restringido) {
      for (const nucleoId of this.nucleosSel) items.push({ level: 'nucleo', nucleoId, galponId: null, loteId: null });
      for (const galponId of this.galponesSel) items.push({ level: 'galpon', nucleoId: null, galponId, loteId: null });
      for (const loteId of this.lotesSel) items.push({ level: 'lote', nucleoId: null, galponId: null, loteId });
    }

    this.saving = true;
    this.userFarmService
      .updateUserFarmScope(this.userId, this.farmId, { restrictLocations: restringido, items })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (config) => {
          this.saving = false;
          this.toast.success(
            restringido
              ? `Alcance restringido guardado (${config.items.length} lugar${config.items.length === 1 ? '' : 'es'}).`
              : 'Acceso global a la granja restablecido.'
          );
          this.scopeUpdated.emit(config);
          this.close.emit();
        },
        error: (err) => {
          this.saving = false;
          const msg = err?.error?.message || 'No se pudo guardar el alcance.';
          this.toast.error(msg);
        }
      });
  }

  closeModal(): void {
    if (this.saving) return;
    this.close.emit();
  }
}

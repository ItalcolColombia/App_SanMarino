// src/app/features/lote/components/modal-asignar-huevo-items/modal-asignar-huevo-items.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faTimes } from '@fortawesome/free-solid-svg-icons';
import { finalize } from 'rxjs';

import { ToastService } from '../../../../shared/services/toast.service';
import { LoteHuevoItemDto, LoteHuevoItemsService } from '../../services/lote-huevo-items.service';

/** Un grupo (`Primera` / `Pnc` / …) con sus ítems, para pintar el selector agrupado. */
interface GrupoHuevoItems {
  tipoHuevo: string;
  items: LoteHuevoItemDto[];
}

/** Etiqueta del grupo cuando el ítem del catálogo no trae `metadata.tipoHuevo`. */
const SIN_CATEGORIA = 'Sin categoría';

/**
 * F7.3 — declara **qué tipos de huevo produce un lote**. Esa declaración es la que el seguimiento
 * diario de producción convierte en filas fijas.
 *
 * <p>
 * **Fail-closed, y es el punto de la pantalla:** un lote sin ítems tildados no puede clasificar
 * huevos en el diario. Por eso el modal avisa explícitamente cuando se va a guardar vacío, en vez
 * de dejar que el operario descubra el bloqueo al día siguiente cargando producción.
 * </p>
 *
 * `Eager` obligatorio: carga por `subscribe` y escribe estado desde el callback (con OnPush el
 * modal se quedaría en «Cargando…» aunque la red respondiera 200).
 */
@Component({
  selector: 'app-modal-asignar-huevo-items',
  standalone: true,
  imports: [FontAwesomeModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './modal-asignar-huevo-items.component.html',
  styleUrls: ['./modal-asignar-huevo-items.component.scss']
})
export class ModalAsignarHuevoItemsComponent implements OnInit {
  private readonly svc = inject(LoteHuevoItemsService);
  private readonly toast = inject(ToastService);

  @Input({ required: true }) loteId!: number;
  @Input() loteNombre = '';

  @Output() cerrar = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  readonly faTimes = faTimes;

  grupos: GrupoHuevoItems[] = [];
  seleccionados = new Set<number>();
  loading = false;
  saving = false;
  /** El catálogo de huevo de la empresa vino vacío: no hay nada que declarar. */
  sinCatalogo = false;

  ngOnInit(): void {
    this.cargar();
  }

  private cargar(): void {
    this.loading = true;
    this.svc.getDisponibles(this.loteId)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: items => {
          this.seleccionados = new Set((items ?? []).filter(i => i.activo).map(i => i.catalogItemId));
          this.grupos = this.agrupar(items ?? []);
          this.sinCatalogo = (items ?? []).length === 0;
        },
        error: err => {
          // Un error de red no se puede leer como «catálogo vacío»: el mensaje tiene que decir que
          // falló la carga, o el operario cree que la empresa no tiene ítems y va a crearlos.
          this.grupos = [];
          this.sinCatalogo = false;
          this.toast.error(err?.error?.message ?? 'No se pudieron cargar los tipos de huevo del catálogo.');
        }
      });
  }

  /**
   * Agrupa por `tipoHuevo` conservando el orden que ya trae el backend (Primera → Pnc → resto, y
   * por nombre dentro de cada grupo). NO reordena: el orden es una sola regla y vive en
   * `HuevoItemsCalculos.PesoTipoHuevo`.
   */
  private agrupar(items: LoteHuevoItemDto[]): GrupoHuevoItems[] {
    const grupos: GrupoHuevoItems[] = [];
    for (const item of items) {
      const clave = item.tipoHuevo?.trim() || SIN_CATEGORIA;
      const grupo = grupos.find(g => g.tipoHuevo === clave);
      if (grupo) grupo.items.push(item);
      else grupos.push({ tipoHuevo: clave, items: [item] });
    }
    return grupos;
  }

  estaSeleccionado(catalogItemId: number): boolean {
    return this.seleccionados.has(catalogItemId);
  }

  alternar(catalogItemId: number): void {
    if (this.seleccionados.has(catalogItemId)) this.seleccionados.delete(catalogItemId);
    else this.seleccionados.add(catalogItemId);
  }

  /** Tilda o destilda un grupo entero — con 21 ítems, hacerlo de a uno es tedioso. */
  alternarGrupo(grupo: GrupoHuevoItems): void {
    const todos = grupo.items.every(i => this.seleccionados.has(i.catalogItemId));
    for (const item of grupo.items) {
      if (todos) this.seleccionados.delete(item.catalogItemId);
      else this.seleccionados.add(item.catalogItemId);
    }
  }

  grupoCompleto(grupo: GrupoHuevoItems): boolean {
    return grupo.items.length > 0 && grupo.items.every(i => this.seleccionados.has(i.catalogItemId));
  }

  seleccionadosDelGrupo(grupo: GrupoHuevoItems): number {
    return grupo.items.filter(i => this.seleccionados.has(i.catalogItemId)).length;
  }

  get totalSeleccionados(): number {
    return this.seleccionados.size;
  }

  guardar(): void {
    this.saving = true;
    this.svc.asignar(this.loteId, { catalogItemIds: [...this.seleccionados] })
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.toast.success(
            this.totalSeleccionados > 0
              ? `Se guardaron ${this.totalSeleccionados} tipos de huevo para este lote.`
              : 'El lote quedó sin tipos de huevo: no podrá registrar clasificación en el seguimiento diario.'
          );
          this.guardado.emit();
        },
        error: err => this.toast.error(err?.error?.message ?? 'No se pudieron guardar los tipos de huevo.')
      });
  }

  onCerrar(): void {
    this.cerrar.emit();
  }
}

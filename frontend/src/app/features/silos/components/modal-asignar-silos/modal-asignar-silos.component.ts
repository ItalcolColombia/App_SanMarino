// src/app/features/silos/components/modal-asignar-silos/modal-asignar-silos.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faTimes } from '@fortawesome/free-solid-svg-icons';
import { Observable, finalize, forkJoin } from 'rxjs';

import { ToastService } from '../../../../shared/services/toast.service';
import { FarmSiloDto, SilosService } from '../../services/silos.service';
import { SelectorSilosComponent } from '../selector-silos/selector-silos.component';

/** Qué se está configurando: los silos que alimentan un GALPÓN, o de los que consume un LOTE. */
export type DestinoAsignacionSilos =
  | { tipo: 'galpon'; granjaId: number; nucleoId: string; galponId: string; titulo: string }
  | { tipo: 'lote'; loteId: number; titulo: string };

/**
 * Modal de asignación de silos. Sirve para el galpón («qué silos lo alimentan») y para el lote
 * («de qué silos consume»): las dos son el mismo gesto sobre distinta fuente de datos, así que
 * comparten pantalla en vez de duplicarla.
 *
 * `Eager` obligatorio: carga por `subscribe` y escribe estado desde el callback (con OnPush el
 * modal se quedaría en «Cargando…» aunque la red respondiera 200).
 */
@Component({
  selector: 'app-modal-asignar-silos',
  standalone: true,
  imports: [FontAwesomeModule, SelectorSilosComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './modal-asignar-silos.component.html',
  styleUrls: ['./modal-asignar-silos.component.scss']
})
export class ModalAsignarSilosComponent implements OnInit {
  private readonly svc = inject(SilosService);
  private readonly toast = inject(ToastService);

  @Input({ required: true }) destino!: DestinoAsignacionSilos;

  @Output() cerrar = new EventEmitter<void>();
  @Output() guardado = new EventEmitter<void>();

  readonly faTimes = faTimes;

  disponibles: FarmSiloDto[] = [];
  seleccionados: number[] = [];
  loading = false;
  saving = false;

  ngOnInit(): void {
    this.cargar();
  }

  get subtitulo(): string {
    return this.destino.tipo === 'galpon'
      ? 'Silos y bodegas que alimentan a este galpón. Definen qué ubicaciones se ofrecen al registrar movimientos filtrando por él.'
      : 'Silos y bodegas de los que consume este lote. El seguimiento diario solo ofrecerá estos.';
  }

  get mensajeVacio(): string {
    return this.destino.tipo === 'galpon'
      ? 'La granja todavía no tiene silos ni bodega. Configúrelos primero en la granja.'
      : 'El galpón del lote no tiene silos asignados y la granja tampoco tiene ninguno configurado.';
  }

  private cargar(): void {
    this.loading = true;

    if (this.destino.tipo === 'galpon') {
      const { granjaId, nucleoId, galponId } = this.destino;
      forkJoin({
        disponibles: this.svc.getSilosDeGranja(granjaId, true),
        actuales: this.svc.getSilosDeGalpon(granjaId, nucleoId, galponId)
      })
        .pipe(finalize(() => (this.loading = false)))
        .subscribe({
          next: ({ disponibles, actuales }) => {
            this.disponibles = disponibles;
            this.seleccionados = actuales.map(a => a.farmSiloId);
          },
          error: e => this.toast.error(this.mensajeError(e, 'No se pudieron cargar los silos del galpón.'))
        });
      return;
    }

    const { loteId } = this.destino;
    forkJoin({
      disponibles: this.svc.getSilosDisponiblesDeLote(loteId),
      actuales: this.svc.getSilosDeLote(loteId)
    })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: ({ disponibles, actuales }) => {
          this.disponibles = disponibles;
          this.seleccionados = actuales.map(a => a.farmSiloId);
        },
        error: e => this.toast.error(this.mensajeError(e, 'No se pudieron cargar los silos del lote.'))
      });
  }

  onSeleccionChange(ids: number[]): void {
    this.seleccionados = ids;
  }

  guardar(): void {
    if (this.saving) return;
    this.saving = true;

    // Se tipa como `Observable<unknown>`: el ternario devuelve dos observables de payload distinto
    // y la unión de firmas no es invocable. Acá no se usa el resultado, solo el éxito/error.
    const peticion: Observable<unknown> = this.destino.tipo === 'galpon'
      ? this.svc.asignarSilosAGalpon(
          this.destino.granjaId,
          this.destino.nucleoId,
          this.destino.galponId,
          { farmSiloIds: this.seleccionados }
        )
      : this.svc.asignarSilosALote(this.destino.loteId, { farmSiloIds: this.seleccionados });

    peticion
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.toast.success('Silos asignados.');
          this.guardado.emit();
          this.cerrar.emit();
        },
        error: e => this.toast.error(this.mensajeError(e, 'No se pudieron asignar los silos.'))
      });
  }

  onCerrar(): void {
    this.cerrar.emit();
  }

  private mensajeError(e: unknown, fallback: string): string {
    const msg = (e as { error?: { message?: string } } | null)?.error?.message;
    return typeof msg === 'string' && msg.trim() ? msg : fallback;
  }
}

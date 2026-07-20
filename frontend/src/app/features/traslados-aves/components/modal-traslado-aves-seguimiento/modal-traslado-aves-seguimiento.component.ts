import {
  Component, Input, Output, EventEmitter,
  OnChanges, SimpleChanges, inject,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { catchError, forkJoin, of } from 'rxjs';

import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../lote-levante/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { LoteService, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';
import { LotePosturaLevanteService, LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';
import { LotePosturaProduccionService, LotePosturaProduccionDto } from '../../../lote/services/lote-postura-produccion.service';
import {
  TrasladosAvesService,
  TrasladoAvesDesdeSegDiarioDto,
  TrasladoAvesResultSegDto
} from '../../services/traslados-aves.service';

export interface OrigenTrasladoInfo {
  loteId: number;          // ID lote_postura_levante o produccion
  loteIdBase?: number | null; // ID base (tabla lotes) — necesario para resumen-mortalidad
  tipoLote: string;        // "Levante" | "Produccion"
  loteNombre: string;
  avesHActual: number;     // fallback (encasetamiento) si no hay saldo real
  avesMActual: number;
  fechaSeguimiento: string;
}

@Component({
  selector: 'app-modal-traslado-aves-seguimiento',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './modal-traslado-aves-seguimiento.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./modal-traslado-aves-seguimiento.component.scss']
})
export class ModalTrasladoAvesSeguimientoComponent implements OnChanges {

  @Input() isOpen = false;
  @Input() origen: OrigenTrasladoInfo | null = null;

  @Output() closed = new EventEmitter<void>();
  @Output() trasladoCompletado = new EventEmitter<TrasladoAvesResultSegDto>();

  private readonly farmSvc    = inject(FarmService);
  private readonly nucleoSvc  = inject(NucleoService);
  private readonly galponSvc  = inject(GalponService);
  private readonly loteSvc    = inject(LoteService);
  private readonly lplSvc     = inject(LotePosturaLevanteService);
  private readonly lppSvc     = inject(LotePosturaProduccionService);
  private readonly trasladoSvc = inject(TrasladosAvesService);

  // ── Estado general ─────────────────────────────────────────────
  loading = false;
  enviando = false;
  errorMsg: string | null = null;

  /** Tipo destino: SIEMPRE igual a origen.tipoLote (Feature 13: no cross-phase). */
  get tipoDestino(): 'Levante' | 'Produccion' {
    return (this.origen?.tipoLote === 'Produccion' ? 'Produccion' : 'Levante');
  }

  // ── Cascade destino ─────────────────────────────────────────────
  granjas:  FarmDto[]         = [];
  nucleos:  NucleoDto[]       = [];
  galpones: GalponDetailDto[] = [];
  lotesDestino: (LotePosturaLevanteDto | LotePosturaProduccionDto)[] = [];

  granjaDestinoId:  number | null = null;
  nucleoDestinoId:  string | null = null;
  galponDestinoId:  string | null = null;
  loteDestinoId:    number | null = null;

  // ── Cantidades ──────────────────────────────────────────────────
  trasladoHembras = 0;
  trasladoMachos  = 0;
  observaciones   = '';

  /** Fecha REAL del evento de traslado (editable; default = fecha sugerida por el caller, o vacía). REQ-009a. */
  fechaEvento = '';
  /** Hoy (YYYY-MM-DD) en LOCAL (no UTC, evita el +1 día de toISOString() de noche en Colombia) — acota el máximo del date picker. */
  readonly hoyStr = ModalTrasladoAvesSeguimientoComponent.todayYMDLocal();

  /** Fecha de hoy en formato yyyy-MM-dd LOCAL. */
  private static todayYMDLocal(): string {
    const d = new Date();
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  }

  // ── Saldo REAL del origen (Feature 13) ─────────────────────────
  /** Resumen de mortalidad — saldoHembras/saldoMachos son las "aves vivas". */
  resumenOrigen: LoteMortalidadResumenDto | null = null;

  /** Aves disponibles (real) — fuente de verdad para validar inputs. */
  get hembrasDisponibles(): number {
    return this.resumenOrigen?.saldoHembras ?? this.origen?.avesHActual ?? 0;
  }
  get machosDisponibles(): number {
    return this.resumenOrigen?.saldoMachos ?? this.origen?.avesMActual ?? 0;
  }

  // ── Datos en caché ──────────────────────────────────────────────
  private todosLPL: LotePosturaLevanteDto[]     = [];
  private todosLPP: LotePosturaProduccionDto[]  = [];

  // ── Lifecycle ───────────────────────────────────────────────────
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      this.resetForm();
      // REQ-009a: si el caller no trae una fecha sugerida (último registro del lote origen),
      // queda VACÍA para forzar una elección consciente en vez de asumir "hoy".
      this.fechaEvento = this.origen?.fechaSeguimiento || '';
      this.cargarDatosIniciales();
    }
  }

  // ── Inicialización ──────────────────────────────────────────────
  private cargarDatosIniciales(): void {
    this.loading = true;
    const tipoOrigen = this.origen?.tipoLote ?? 'Levante';

    // Cargar resumen real del origen (en paralelo con granjas + lotes).
    // Sirve tanto para Levante como para Producción — el saldo del resumen
    // se calcula sobre el lote base (tabla lotes) y refleja todos los
    // descuentos (mortalidad/sel) + traslados (in/out) de AMBAS fases.
    const loteIdBase = this.origen?.loteIdBase;
    const resumen$ = loteIdBase
      ? this.loteSvc.getResumenMortalidad(loteIdBase).pipe(catchError(() => of<LoteMortalidadResumenDto | null>(null)))
      : of<LoteMortalidadResumenDto | null>(null);

    forkJoin({
      farms:   this.farmSvc.getForTrasladoSeguimiento().pipe(catchError(() => of<FarmDto[]>([]))),
      lpl:     tipoOrigen === 'Levante'    ? this.lplSvc.getAll().pipe(catchError(() => of<LotePosturaLevanteDto[]>([])))    : of<LotePosturaLevanteDto[]>([]),
      lpp:     tipoOrigen === 'Produccion' ? this.lppSvc.getAll().pipe(catchError(() => of<LotePosturaProduccionDto[]>([]))) : of<LotePosturaProduccionDto[]>([]),
      resumen: resumen$
    }).subscribe(({ farms, lpl, lpp, resumen }) => {
      this.granjas   = farms;
      this.todosLPL  = lpl;
      this.todosLPP  = lpp;
      this.resumenOrigen = resumen;
      this.loading   = false;
    });
  }

  // ── Cascade handlers ────────────────────────────────────────────
  onGranjaChange(): void {
    this.nucleoDestinoId = null;
    this.galponDestinoId = null;
    this.loteDestinoId   = null;
    this.nucleos    = [];
    this.galpones   = [];
    this.lotesDestino = [];
    this.filtrarLotesDestino();

    if (!this.granjaDestinoId) return;

    this.nucleoSvc.getByGranja(Number(this.granjaDestinoId))
      .pipe(catchError(() => of<NucleoDto[]>([])))
      .subscribe(ns => { this.nucleos = ns; });
  }

  onNucleoChange(): void {
    this.galponDestinoId = null;
    this.loteDestinoId   = null;
    this.galpones   = [];
    this.lotesDestino = [];

    if (this.granjaDestinoId && this.nucleoDestinoId) {
      this.galponSvc.getByGranjaAndNucleo(
          Number(this.granjaDestinoId), this.nucleoDestinoId
        )
        .pipe(catchError(() => of<GalponDetailDto[]>([])))
        .subscribe(gs => { this.galpones = gs; });
    }
    this.filtrarLotesDestino();
  }

  onGalponChange(): void {
    this.loteDestinoId = null;
    this.filtrarLotesDestino();
  }

  // ── Filtrar lotes del destino seleccionado ───────────────────────
  private filtrarLotesDestino(): void {
    if (!this.granjaDestinoId) {
      this.lotesDestino = [];
      return;
    }

    const origenId = this.origen?.loteId;
    const gId      = Number(this.granjaDestinoId);

    if (this.tipoDestino === 'Levante') {
      this.lotesDestino = this.todosLPL.filter(l => {
        if (l.granjaId !== gId) return false;
        if (origenId != null && l.lotePosturaLevanteId === origenId) return false;
        if (this.nucleoDestinoId &&
            String(l.nucleo?.nucleoId ?? l.nucleoId ?? '') !== this.nucleoDestinoId) return false;
        if (this.galponDestinoId &&
            String(l.galpon?.galponId ?? l.galponId ?? '') !== this.galponDestinoId) return false;
        return true;
      });
    } else {
      this.lotesDestino = this.todosLPP.filter(l => {
        if (l.granjaId !== gId) return false;
        if (origenId != null && l.lotePosturaProduccionId === origenId) return false;
        if (this.nucleoDestinoId &&
            String(l.nucleo?.nucleoId ?? l.nucleoId ?? '') !== this.nucleoDestinoId) return false;
        if (this.galponDestinoId &&
            String(l.galpon?.galponId ?? l.galponId ?? '') !== this.galponDestinoId) return false;
        return true;
      });
    }
  }

  // ── Helpers de ID ────────────────────────────────────────────────
  getLoteId(l: LotePosturaLevanteDto | LotePosturaProduccionDto): number {
    return this.tipoDestino === 'Levante'
      ? (l as LotePosturaLevanteDto).lotePosturaLevanteId
      : (l as LotePosturaProduccionDto).lotePosturaProduccionId;
  }

  getLoteNombre(l: LotePosturaLevanteDto | LotePosturaProduccionDto): string {
    return l.loteNombre;
  }

  getLoteGalpon(l: LotePosturaLevanteDto | LotePosturaProduccionDto): string {
    return l.galpon?.galponNombre ?? '';
  }

  // ── Validaciones ─────────────────────────────────────────────────
  get hembrasValidas(): boolean {
    return this.trasladoHembras >= 0 && this.trasladoHembras <= this.hembrasDisponibles;
  }
  get machosValidos(): boolean {
    return this.trasladoMachos >= 0 && this.trasladoMachos <= this.machosDisponibles;
  }

  get formularioValido(): boolean {
    return (
      !!this.fechaEvento && // REQ-009a: fecha del evento es obligatoria (elección consciente, no default silencioso)
      this.loteDestinoId != null &&
      (this.trasladoHembras > 0 || this.trasladoMachos > 0) &&
      this.hembrasValidas &&
      this.machosValidos
    );
  }

  // ── Confirmar ────────────────────────────────────────────────────
  onConfirmar(): void {
    if (!this.formularioValido || !this.origen) return;

    this.enviando = true;
    this.errorMsg = null;

    const dto: TrasladoAvesDesdeSegDiarioDto = {
      loteOrigenId:    this.origen.loteId,
      tipoOrigen:      this.origen.tipoLote,
      fechaSeguimiento: this.fechaEvento || this.origen.fechaSeguimiento,
      trasladoHembras: this.trasladoHembras,
      trasladoMachos:  this.trasladoMachos,
      loteDestinoId:   Number(this.loteDestinoId),
      tipoDestino:     this.tipoDestino,
      granjaDestinoId: this.granjaDestinoId ?? undefined,
      observaciones:   this.observaciones.trim() || null
    };

    this.trasladoSvc.ejecutarTrasladoDesdeSegDiario(dto).subscribe({
      next: (result) => {
        this.enviando = false;
        if (result.exitoso) {
          this.trasladoCompletado.emit(result);
          this.cerrar();
        } else {
          this.errorMsg = result.mensaje || 'Error al ejecutar el traslado.';
        }
      },
      error: (err) => {
        this.enviando = false;
        this.errorMsg = err?.error?.message ?? err?.message ?? 'Error al ejecutar el traslado.';
      }
    });
  }

  // ── Cierre ───────────────────────────────────────────────────────
  cerrar(): void {
    this.resetForm();
    this.closed.emit();
  }

  onBackdropClick(event: Event): void {
    if (event.target === event.currentTarget) this.cerrar();
  }

  // ── Reset ────────────────────────────────────────────────────────
  private resetForm(): void {
    this.errorMsg        = null;
    this.enviando        = false;
    this.granjaDestinoId = null;
    this.nucleoDestinoId = null;
    this.galponDestinoId = null;
    this.loteDestinoId   = null;
    this.trasladoHembras = 0;
    this.trasladoMachos  = 0;
    this.observaciones   = '';
    this.nucleos         = [];
    this.galpones        = [];
    this.lotesDestino    = [];
    this.resumenOrigen   = null;
  }
}

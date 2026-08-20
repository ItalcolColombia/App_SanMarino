import {
  Component, Input, Output, EventEmitter,
  OnInit, OnChanges, SimpleChanges, inject,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, forkJoin, of } from 'rxjs';

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
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';

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
export class ModalTrasladoAvesSeguimientoComponent implements OnInit, OnChanges {

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
  private readonly companyConfig = inject(ActiveCompanyConfigService);

  // ── Estado general ─────────────────────────────────────────────
  loading = false;
  enviando = false;
  errorMsg: string | null = null;

  // ── Etapa destino (Fase 3 — cross-etapa por flag de empresa) ────
  /**
   * Flag `companies.permite_traslado_aves_cross_etapa` (FAIL-CLOSED: false si el GET falla).
   * Con el flag apagado el modal se comporta EXACTAMENTE como antes: destino = etapa del origen.
   */
  permiteCrossEtapa = false;

  /** Etapa del lote destino elegida. Por defecto = la del origen. */
  etapaDestino: 'Levante' | 'Produccion' = 'Levante';

  /** True si LPP ya se cargó para elegir destino de Producción desde un origen de Levante. */
  private lppCargadosCrossEtapa = false;
  /** True mientras se cargan los lotes de la otra etapa. */
  cargandoLotesEtapa = false;

  /** Etapa del lote ORIGEN (normalizada). */
  get etapaOrigen(): 'Levante' | 'Produccion' {
    return (this.origen?.tipoLote === 'Produccion' ? 'Produccion' : 'Levante');
  }

  /** Tipo destino que viaja en el payload = etapa elegida (sin flag, siempre la del origen). */
  get tipoDestino(): 'Levante' | 'Produccion' {
    return this.etapaDestino;
  }

  /** El selector de etapa sólo aparece con el flag activo y origen en Levante (levante → producción). */
  get mostrarSelectorEtapaDestino(): boolean {
    return this.permiteCrossEtapa && this.etapaOrigen === 'Levante';
  }

  /** True cuando el traslado cruza de etapa (Levante → Producción): las aves entran como cohorte. */
  get esCrossEtapa(): boolean {
    return this.etapaOrigen !== this.etapaDestino;
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
  /** Transporte (postura, Santa Reyes). Opcionales — nunca se piden como requeridos. */
  placa      = '';
  conductor  = '';
  sellos     = '';
  /** Santa Reyes: oculta el campo Machos (no se manejan en postura). */
  ocultaMachosEnPostura = false;

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
  ngOnInit(): void {
    // Flags de la empresa activa (emite una vez y completa; usa caché por empresa).
    this.companyConfig.getFlags().subscribe(flags => {
      this.permiteCrossEtapa = flags.permiteTrasladoAvesCrossEtapa;
      // Si el flag llega después de abrir el modal, el destino sigue en la etapa del origen
      // (default) — el selector simplemente aparece.
      this.ocultaMachosEnPostura = flags.ocultaMachosEnPostura;
      if (this.ocultaMachosEnPostura) this.trasladoMachos = 0;
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['origen']) {
      this.etapaDestino = this.etapaOrigen;
    }
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
      // paraDestino=true: catálogo de lotes DESTINO — no se restringe por el alcance granular del usuario
      lpl:     tipoOrigen === 'Levante'    ? this.lplSvc.getAll(true).pipe(catchError(() => of<LotePosturaLevanteDto[]>([])))    : of<LotePosturaLevanteDto[]>([]),
      lpp:     tipoOrigen === 'Produccion' ? this.lppSvc.getAll(true).pipe(catchError(() => of<LotePosturaProduccionDto[]>([]))) : of<LotePosturaProduccionDto[]>([]),
      resumen: resumen$
    }).subscribe(({ farms, lpl, lpp, resumen }) => {
      this.granjas   = farms;
      this.todosLPL  = lpl;
      this.todosLPP  = lpp;
      this.resumenOrigen = resumen;
      this.loading   = false;
    });
  }

  // ── Etapa destino (cross-etapa) ─────────────────────────────────
  /**
   * Cambio de etapa destino (sólo visible con flag + origen Levante).
   * Los lotes de Producción se cargan con el MISMO mecanismo que ya usa el modal
   * cuando el origen es Producción (`LotePosturaProduccionService.getAll()`), en forma
   * perezosa: si el flag está apagado nunca se dispara esta petición.
   */
  onEtapaDestinoChange(): void {
    this.loteDestinoId = null;
    this.lotesDestino  = [];

    if (this.etapaDestino === 'Produccion' && !this.lppCargadosCrossEtapa && this.etapaOrigen === 'Levante') {
      this.cargandoLotesEtapa = true;
      this.lppSvc.getAll(true) // paraDestino: lotes DESTINO cross-etapa sin alcance granular
        .pipe(
          catchError(() => of<LotePosturaProduccionDto[]>([])),
          finalize(() => { this.cargandoLotesEtapa = false; })
        )
        .subscribe(lpp => {
          this.todosLPP = lpp;
          this.lppCargadosCrossEtapa = true;
          this.filtrarLotesDestino();
        });
      return;
    }

    this.filtrarLotesDestino();
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

    // paraDestino=true: cascada de DESTINO — no se restringe por el alcance granular del usuario
    this.nucleoSvc.getByGranja(Number(this.granjaDestinoId), true)
      .pipe(catchError(() => of<NucleoDto[]>([])))
      .subscribe(ns => { this.nucleos = ns; });
  }

  onNucleoChange(): void {
    this.galponDestinoId = null;
    this.loteDestinoId   = null;
    this.galpones   = [];
    this.lotesDestino = [];

    if (this.granjaDestinoId && this.nucleoDestinoId) {
      // paraDestino=true: cascada de DESTINO — no se restringe por el alcance granular del usuario
      this.galponSvc.getByGranjaAndNucleo(
          Number(this.granjaDestinoId), this.nucleoDestinoId, true
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

    // El lote origen sólo se auto-excluye si el destino es de SU MISMA etapa
    // (los ids de LPL y LPP son secuencias distintas: cruzados no significan lo mismo).
    const mismaEtapaQueOrigen = this.etapaOrigen === this.tipoDestino;

    if (this.tipoDestino === 'Levante') {
      this.lotesDestino = this.todosLPL.filter(l => {
        if (l.granjaId !== gId) return false;
        if (mismaEtapaQueOrigen && origenId != null && l.lotePosturaLevanteId === origenId) return false;
        if (this.nucleoDestinoId &&
            String(l.nucleo?.nucleoId ?? l.nucleoId ?? '') !== this.nucleoDestinoId) return false;
        if (this.galponDestinoId &&
            String(l.galpon?.galponId ?? l.galponId ?? '') !== this.galponDestinoId) return false;
        return true;
      });
    } else {
      this.lotesDestino = this.todosLPP.filter(l => {
        if (l.granjaId !== gId) return false;
        if (mismaEtapaQueOrigen && origenId != null && l.lotePosturaProduccionId === origenId) return false;
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
      observaciones:   this.observaciones.trim() || null,
      placa:           this.placa.trim() || null,
      conductor:       this.conductor.trim() || null,
      sellos:          this.sellos.trim() || null
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
    // Destino arranca SIEMPRE en la etapa del origen (comportamiento previo al flag).
    this.etapaDestino    = this.etapaOrigen;
    this.lppCargadosCrossEtapa = false;
    this.cargandoLotesEtapa    = false;
    this.granjaDestinoId = null;
    this.nucleoDestinoId = null;
    this.galponDestinoId = null;
    this.loteDestinoId   = null;
    this.trasladoHembras = 0;
    this.trasladoMachos  = 0;
    this.observaciones   = '';
    this.placa           = '';
    this.conductor       = '';
    this.sellos          = '';
    this.nucleos         = [];
    this.galpones        = [];
    this.lotesDestino    = [];
    this.resumenOrigen   = null;
  }
}

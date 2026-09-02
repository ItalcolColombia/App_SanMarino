import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { exportarTablaExcel, exportarAoaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';
import { TablaListaIndicadoresComponent } from '../tabla-lista-indicadores/tabla-lista-indicadores.component';
import { GraficasPrincipalComponent } from '../graficas-principal/graficas-principal.component';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { LoteRegistroHistoricoUnificadoDto } from '../../../aves-engorde/services/seguimiento-aves-engorde.service';
import { EdadesLoteComponent } from '../../../traslados-aves/components/edades-lote/edades-lote.component';
import { FilaCapturaPendienteComponent } from '../../../../shared/components/fila-captura-pendiente/fila-captura-pendiente.component';
import type { CapturaPendienteResumen } from '../../../../shared/offline/models/outbox.model';

/** Totales del historial unificado por una fecha (YYYY-MM-DD), alineados con el backend. */
interface AggregadoHistoricoDia {
  ingresoKg: number;
  trasladoEntradaKg: number;
  trasladoSalidaKg: number;
  consumoBodegaKg: number;
  refsDocumento: string[];
  ventaH: number;
  ventaM: number;
  ventaX: number;
}

/** Fila enriquecida para la tabla de registros diarios (libro de seguimiento / pestaña Seguimiento). */
export interface RegistroDiarioTablaFila {
  seg: SeguimientoLoteLevanteDto;
  /** Día de vida 1…n (encaset = día 1). null si la fecha del registro es anterior al encasetamiento (REQ-011d). */
  edadDia: number | null;
  /** null si la fecha del registro es anterior al encasetamiento (REQ-011d). */
  semana: number | null;
  diaCorto: string;
  /** Solo mortalidad + selección (como TOTAL MORT+ SEL / DÍA). */
  totalMortSelDia: number;
  /**
   * TK-2026-000021 — salidas del día POR SEXO (mortalidad + selección de cada uno). La columna
   * única sumaba los dos y no dejaba ver de qué lado se estaban yendo las aves.
   */
  totalMortSelDiaH: number;
  totalMortSelDiaM: number;
  saldoAves: number;
  /** Saldo de aves vivas (hembras) — REQ-008a: necesario para gr/ave/día por sexo en Reporte semana. */
  saldoAvesH: number;
  /** Saldo de aves vivas (machos) — REQ-008a. */
  saldoAvesM: number;
  consumoDiaKg: number;
  acumConsumoKg: number;
  /** Consumo acumulado de hembras (kg) — REQ-007c. */
  acumConsumoHKg: number;
  /** Consumo acumulado de machos (kg) — REQ-007c. */
  acumConsumoMKg: number;
  ingresoAlimento: string;
  traslado: string;
  documento: string;
  despachoH: number | null;
  despachoM: number | null;
  /** Ventas mixtas (historial unificado); solo pollo engorde. */
  despachoX: number | null;
  /** INV_CONSUMO sumado del inventario (kg); solo pollo engorde. */
  consumoBodegaKg: number | null;
  tipoAlimentoCorto: string;
  /** % Retiro (Mort+Sel) de la SEMANA sobre aves al inicio de la semana (REQ-007d). Mismo valor en
   *  todas las filas de la semana. null si el saldo al inicio de semana es <= 0 (nunca 100% sintético). */
  pctRetiroSemana: number | null;
}

/**
 * Columnas de machos que se retiran de la grilla de registro diario con
 * `ocultaMachosEnPostura`: mortalidad, selección, total mort+sel, saldo, consumo kg,
 * consumo acumulado, peso promedio, uniformidad, C.V., ingreso y salida por traslado.
 * Vive acá porque `colspanRegistroDiario` tiene que restarlas.
 */
const COLUMNAS_MACHOS_TABLA_DIARIA = 11;

@Component({
  selector: 'app-tabs-principal',
  standalone: true,
  imports: [CommonModule, TablaListaIndicadoresComponent, GraficasPrincipalComponent, EdadesLoteComponent, FilaCapturaPendienteComponent],
  templateUrl: './tabs-principal.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./tabs-principal.component.scss']
})
export class TabsPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];

  /**
   * Capturas de este lote guardadas sin red y todavía sin enviar. Entra como **input aparte** de
   * `seguimientos` a propósito: ese arreglo alimenta los indicadores, la gráfica y el Excel de esta
   * misma clase, y una fila que el servidor nunca vio no puede llegar a ninguno de los tres. Al no
   * mezclarse, la separación está garantizada por construcción y no por un filtro que haya que
   * recordar en cada exportación nueva.
   */
  @Input() capturasPendientes: CapturaPendienteResumen[] = [];
  /** LoteDto (aves-engorde) o LotePosturaLevanteDto (seguimiento levante). */
  @Input() selectedLote: LoteDto | LotePosturaLevanteDto | null = null;
  /** Resumen de descuentos (mortalidad, descarte, error sexaje) sobre el lote en Levante. */
  @Input() resumenLevante: LoteMortalidadResumenDto | null = null;
  @Input() loading: boolean = false;
  /** Si true, deshabilita botones Crear / Editar / Eliminar (ej. lote sin aves o cerrado). */
  @Input() disableCreateEditDelete: boolean = false;
  /** Si true, muestra aviso "Lote cerrado" en la información del lote. */
  @Input() isLoteCerrado: boolean = false;
  /** Solo módulo seguimiento pollo engorde: botón para exportar la tabla de registros diarios a Excel. */
  @Input() showExportSeguimientoExcel: boolean = false;
  /** Nombre del lote (nombre de archivo y fila de contexto en el Excel). */
  @Input() exportSeguimientoLoteNombre: string = '';
  /** Filas de lote_registro_historico_unificado (misma respuesta que por-lote). Solo pollo engorde. */
  @Input() historicoUnificado: LoteRegistroHistoricoUnificadoDto[] = [];
  /**
   * Si true, agrupa el historial por fecha de operación y rellena Ingreso, Traslado, Documento,
   * Despacho H/M/X y consumo bodega en la tabla principal (sin segunda tabla).
   */
  @Input() enriquecerTablaConHistoricoInventario = false;
  /**
   * ID del lote BASE (tabla `lotes`) para el bloque "Edades en el lote" (cohortes).
   * Null (default) ⇒ el bloque no se muestra ni consulta — módulos que no lo pasan quedan intactos.
   */
  @Input() loteIdCohortes: number | null = null;
  /** Incrementar para refrescar las cohortes sin cambiar de lote (p. ej. tras un traslado). */
  @Input() cohortesRefreshTrigger = 0;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoLoteLevanteDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoLoteLevanteDto>();

  activeTab: 'general' | 'indicadores' | 'grafica' = 'general';

  // Verificar si el usuario es admin
  isAdmin: boolean = false;

  /** Registros ordenados por fecha (asc) con acumulados y campos de metadata (traslado, ingreso, etc.). */
  diarioFilas: RegistroDiarioTablaFila[] = [];


  /**
   * Flag `companies.captura_huevos_en_levante`.
   *
   * TK-2026-000021: ya NO gobierna columnas — los huevos salieron de la tabla de registros
   * diarios y de su Excel porque son tema de PRODUCCIÓN. El campo se conserva porque el flag
   * sigue vivo y gobernando lo que importa: la captura de huevos en el registro diario, su
   * desglose en el detalle y el arrastre hacia el lote de producción al cerrar el levante.
   * **Fail-closed**: arranca apagado y sólo se prende si el backend lo confirma.
   */
  mostrarColumnasHuevos = false;

  /** Empresas sin machos en postura: sus columnas no se pintan ni se exportan (SR-DEF-1). */
  ocultaMachosEnPostura = false;

  /**
   * Celda del Excel que solo existe si la empresa maneja machos: devuelve `[]` o `[valor]` para
   * usarse con spread. La cabecera y el dato se filtran con la MISMA condición y en la misma
   * posición, así que **no se pueden desalinear** — que es el riesgo real de tocar dos arrays
   * paralelos, y un Excel corrido no avisa: se ve bien y los números quedan bajo otro título.
   */
  private soloConMachos<T>(valor: T): T[] {
    return this.ocultaMachosEnPostura ? [] : [valor];
  }

  constructor(
    private storageService: TokenStorageService,
    private companyConfig: ActiveCompanyConfigService
  ) { }

  ngOnInit(): void {
    this.checkAdminRole();
    // El flag llega async (HTTP con caché de 5 min). El componente es `Eager`, así que basta con
    // asignar el campo para que la tabla se repinte; `getFlags()` completa ⇒ no hay fuga de
    // suscripción.
    this.companyConfig.getFlags().subscribe(flags => {
      this.mostrarColumnasHuevos = flags.capturaHuevosEnLevante;
      this.ocultaMachosEnPostura = flags.ocultaMachosEnPostura;
    });
  }

  // Verificar si el usuario tiene rol de Admin
  private checkAdminRole(): void {
    const session = this.storageService.get();
    if (session?.user?.roles) {
      this.isAdmin = session.user.roles.includes('Admin');
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote'] || changes['historicoUnificado'] || changes['enriquecerTablaConHistoricoInventario']) {
      this.diarioFilas = this.buildDiarioFilas();
    }
  }

  /** Columnas de la tabla de registros diarios. Feature 13: ahora son 4 columnas
   *  por género (↘ Ing.H, ↘ Ing.M, ↗ Sal.H, ↗ Sal.M) en lugar de 2 totales,
   *  por lo que sumamos +2 columnas al cómputo base. */
  get colspanRegistroDiario(): number {
    // 26 columnas base (se quitó "Día (calendario)"; la fecha ya lo cubre — REQ-007e;
    // se sumaron "Consumo acum. hembras/machos (kg)" — REQ-007c).
    // TK-2026-000021: +1 por abrir «TOTAL MORT+SEL» en H/M, +1 por abrir «Saldo aves» en H/M,
    // +4 por uniformidad y C.V. de cada sexo; los huevos salieron de la grilla (siguen en el
    // Excel y en el detalle), así que ya no suman columnas acá.
    // SR-DEF-1: con machos ocultos se retiran 11 columnas de la grilla (mortalidad, selección,
    // total mort+sel, saldo, consumo kg, consumo acum., peso, uniformidad, C.V., ingreso y salida
    // por traslado). Sin descontarlas, la fila de "sin registros" se estira de más y deja la tabla
    // torcida — el colspan es un número fijo, no se recalcula solo.
    return 32
      + (this.enriquecerTablaConHistoricoInventario ? 3 : 0)
      - (this.ocultaMachosEnPostura ? COLUMNAS_MACHOS_TABLA_DIARIA : 0);
  }

  /** Cantidad de registros cuya fecha es anterior al encasetamiento del lote (REQ-011d). */
  get registrosAnterioresAEncaset(): number {
    return (this.diarioFilas ?? []).reduce((n, f) => n + (f.edadDia == null ? 1 : 0), 0);
  }

  trackByDiarioFila = (_: number, f: RegistroDiarioTablaFila) => f.seg.id;

  /** Registros de seguimiento diario cargados para el lote (debe coincidir con filas de la tabla). */
  get cantidadRegistrosSeguimiento(): number {
    return (this.seguimientos ?? []).length;
  }

  get fechaEncasetLote(): string | null {
    const l = this.selectedLote as LoteDto | LotePosturaLevanteDto | null;
    const raw = l && 'fechaEncaset' in l ? (l as { fechaEncaset?: string | null }).fechaEncaset : null;
    return raw && String(raw).trim() ? String(raw) : null;
  }

  /** Mortalidad acumulada (hembras + machos) según resumen API. */
  get totalMortalidadAcumulada(): number {
    const r = this.resumenLevante;
    if (!r) return 0;
    return (r.mortalidadAcumHembras ?? 0) + (r.mortalidadAcumMachos ?? 0);
  }

  /** Selección / descarte acumulado (hembras + machos). */
  get totalSeleccionAcumulada(): number {
    const r = this.resumenLevante;
    if (!r) return 0;
    return (r.selAcumHembras ?? 0) + (r.selAcumMachos ?? 0);
  }

  /** Error de sexaje acumulado (hembras + machos). */
  get totalErrorSexajeAcumulado(): number {
    const r = this.resumenLevante;
    if (!r) return 0;
    return (r.errorSexajeAcumHembras ?? 0) + (r.errorSexajeAcumMachos ?? 0);
  }

  private buildDiarioFilas(): RegistroDiarioTablaFila[] {
    const list = [...(this.seguimientos || [])];
    if (list.length === 0) return [];
    list.sort((a, b) => {
      const ya = this.toYMD(a.fechaRegistro) ?? '';
      const yb = this.toYMD(b.fechaRegistro) ?? '';
      if (ya !== yb) return ya.localeCompare(yb);
      return (a.id ?? 0) - (b.id ?? 0);
    });

    const histPorFecha = this.enriquecerTablaConHistoricoInventario ? this.aggregateHistoricoPorFecha() : null;

    const inicial = this.avesInicialesLote();
    const iniciales = this.avesInicialesPorGenero();
    /** Acumulado de todas las bajas (mort + sel + err. sexaje) para saldo de aves. */
    let acumTodasPerdidas = 0;
    /** Acumulado de traslados ingresos (+) y salidas (-) — Feature 13. */
    let acumTrasIn = 0;
    let acumTrasOut = 0;
    let acumCons = 0;
    /** Acumuladores por sexo — REQ-007c (consumo H/M) y REQ-008a (saldo de aves por sexo). */
    let acumConsH = 0;
    let acumConsM = 0;
    let acumPerdidasH = 0;
    let acumPerdidasM = 0;
    let acumTrasInH = 0;
    let acumTrasInM = 0;
    let acumTrasOutH = 0;
    let acumTrasOutM = 0;
    const out: RegistroDiarioTablaFila[] = [];

    for (const seg of list) {
      const mh = seg.mortalidadHembras ?? 0;
      const mm = seg.mortalidadMachos ?? 0;
      const selh = seg.selH ?? 0;
      const selm = seg.selM ?? 0;
      const erh = seg.errorSexajeHembras ?? 0;
      const erm = seg.errorSexajeMachos ?? 0;
      const totalMortSelDiaH = mh + selh;
      const totalMortSelDiaM = mm + selm;
      const totalMortSelDia = totalMortSelDiaH + totalMortSelDiaM;
      const perdidasTodasDia = totalMortSelDia + erh + erm;
      acumTodasPerdidas += perdidasTodasDia;
      acumPerdidasH += mh + selh + erh;
      acumPerdidasM += mm + selm + erm;

      // 🔀 Feature 13 — acumular traslados por fila (en orden cronológico)
      const tInH  = seg.trasladoIngresoHembras ?? 0;
      const tInM  = seg.trasladoIngresoMachos ?? 0;
      const tOutH = seg.trasladoSalidaHembras  ?? 0;
      const tOutM = seg.trasladoSalidaMachos   ?? 0;
      acumTrasIn  += tInH + tInM;
      acumTrasOut += tOutH + tOutM;
      acumTrasInH  += tInH;
      acumTrasInM  += tInM;
      acumTrasOutH += tOutH;
      acumTrasOutM += tOutM;

      const ch = Number(seg.consumoKgHembras ?? 0);
      const cm = Number(seg.consumoKgMachos ?? 0);
      const consDia = ch + cm;
      acumCons  += consDia;
      acumConsH += ch;
      acumConsM += cm;

      // saldo = inicial − bajas + ingresos_traslado − salidas_traslado
      const saldo = Math.max(0, inicial - acumTodasPerdidas + acumTrasIn - acumTrasOut);
      // REQ-008a: mismo criterio pero por sexo (necesario para gr/ave/día H/M en Reporte semana).
      const saldoH = Math.max(0, iniciales.h - acumPerdidasH + acumTrasInH - acumTrasOutH);
      const saldoM = Math.max(0, iniciales.m - acumPerdidasM + acumTrasInM - acumTrasOutM);

      const edad0 = this.calcularEdadDias(seg.fechaRegistro);
      /** Días de vida: el día del encasetamiento es 1. null si el registro es anterior al
       *  encasetamiento (REQ-011d): antes se clampeaba en silencio con Math.max(0, diff). */
      const edadDia = edad0 == null ? null : Math.max(1, edad0 + 1);
      /** Semana de cría: semana 1 = días 1..7, semana 2 = 8..14, etc. (sin tope). null si edadDia es null. */
      const semana = edadDia == null ? null : Math.max(1, Math.ceil(edadDia / 7));

      const ymd = this.toYMD(seg.fechaRegistro);
      const agg = ymd && histPorFecha ? histPorFecha.get(ymd) : undefined;

      const metaIng = this.metaStr(seg, 'ingresoAlimento', 'ingreso_alimento', 'ingresoAlimentoKg');
      const metaTras = this.metaStr(seg, 'traslado', 'notaTraslado', 'trasladoAlimento', 'textoTraslado', 'trasladoTexto');
      const metaDoc = this.metaStr(seg, 'documento', 'documentoAlimento', 'nroDocumento', 'numeroDocumento');
      const metaDh = this.metaNum(seg, 'despachoHembras', 'despachoH', 'despacho_hembra');
      const metaDm = this.metaNum(seg, 'despachoMachos', 'despachoM', 'despacho_macho');

      let ingresoAlimento = metaIng;
      let traslado = metaTras;
      let documento = metaDoc;
      let despachoH = metaDh;
      let despachoM = metaDm;
      let despachoX: number | null = null;
      let consumoBodegaKg: number | null = null;

      if (agg) {
        if (agg.ingresoKg > 0) {
          ingresoAlimento = `${this.formatKgNumber(agg.ingresoKg)} kg`;
        }
        const partesTr: string[] = [];
        if (agg.trasladoEntradaKg > 0) partesTr.push(`Entrada ${this.formatKgNumber(agg.trasladoEntradaKg)} kg`);
        if (agg.trasladoSalidaKg > 0) partesTr.push(`Salida ${this.formatKgNumber(agg.trasladoSalidaKg)} kg`);
        if (partesTr.length) {
          traslado = partesTr.join(' · ');
        }
        if (agg.refsDocumento.length) {
          documento = [...new Set(agg.refsDocumento)].join(', ');
        }
        if (agg.ventaH > 0) despachoH = agg.ventaH;
        if (agg.ventaM > 0) despachoM = agg.ventaM;
        despachoX = agg.ventaX > 0 ? agg.ventaX : null;
        consumoBodegaKg = agg.consumoBodegaKg > 0 ? agg.consumoBodegaKg : null;
      }

      out.push({
        seg,
        edadDia,
        semana,
        diaCorto: this.formatDiaSemanaCorto(seg.fechaRegistro),
        totalMortSelDia,
        totalMortSelDiaH,
        totalMortSelDiaM,
        saldoAves: saldo,
        saldoAvesH: saldoH,
        saldoAvesM: saldoM,
        consumoDiaKg: consDia,
        acumConsumoKg: acumCons,
        acumConsumoHKg: acumConsH,
        acumConsumoMKg: acumConsM,
        ingresoAlimento,
        traslado,
        documento,
        despachoH,
        despachoM,
        despachoX,
        consumoBodegaKg,
        tipoAlimentoCorto: this.tipoAlimentoCorto(seg.tipoAlimento),
        pctRetiroSemana: null // se completa abajo, agrupado por semana (REQ-007d)
      });
    }

    this.aplicarPctRetiroSemana(out);
    return out;
  }

  /** REQ-007d: %Retiro (Mort+Sel) de la SEMANA sobre el saldo de aves al inicio de esa semana.
   *  Reemplaza el % diario anterior (que caía a 100% sintético cuando el saldo del día era 0 —
   *  el síntoma visible de lotes con encaset corrupto). Se agrupa `out` por semana y se asigna el
   *  mismo valor a todas las filas de esa semana; null si el saldo al inicio de semana es <= 0. */
  private aplicarPctRetiroSemana(out: RegistroDiarioTablaFila[]): void {
    const porSemana = new Map<number, { mortSel: number; bajas: number; saldoFin: number }>();
    for (const f of out) {
      if (f.semana == null) continue;
      const acc = porSemana.get(f.semana) ?? { mortSel: 0, bajas: 0, saldoFin: 0 };
      const erh = f.seg.errorSexajeHembras ?? 0;
      const erm = f.seg.errorSexajeMachos ?? 0;
      acc.mortSel += f.totalMortSelDia;
      acc.bajas += f.totalMortSelDia + erh + erm;
      acc.saldoFin = f.saldoAves; // filas en orden cronológico → queda la de la última fecha de la semana
      porSemana.set(f.semana, acc);
    }
    for (const f of out) {
      if (f.semana == null) continue;
      const acc = porSemana.get(f.semana)!;
      const saldoInicioSemana = acc.saldoFin + acc.bajas;
      f.pctRetiroSemana = saldoInicioSemana > 0 ? (100 * acc.mortSel) / saldoInicioSemana : null;
    }
  }

  /** Agrupa historial unificado por fecha de operación (misma lógica que el backfill de metadata). */
  private aggregateHistoricoPorFecha(): Map<string, AggregadoHistoricoDia> {
    const map = new Map<string, AggregadoHistoricoDia>();
    const ensure = (ymd: string): AggregadoHistoricoDia => {
      let a = map.get(ymd);
      if (!a) {
        a = {
          ingresoKg: 0,
          trasladoEntradaKg: 0,
          trasladoSalidaKg: 0,
          consumoBodegaKg: 0,
          refsDocumento: [],
          ventaH: 0,
          ventaM: 0,
          ventaX: 0
        };
        map.set(ymd, a);
      }
      return a;
    };

    const pushRef = (a: AggregadoHistoricoDia, h: LoteRegistroHistoricoUnificadoDto) => {
      const r = (h.numeroDocumento?.trim() || h.referencia?.trim() || '').trim();
      if (r) a.refsDocumento.push(r);
    };

    for (const h of this.historicoUnificado ?? []) {
      const ymd = this.toYMD(h.fechaOperacion);
      if (!ymd) continue;
      const a = ensure(ymd);
      const kg = Number(h.cantidadKg ?? 0);

      switch (h.tipoEvento) {
        case 'INV_INGRESO':
          a.ingresoKg += kg;
          pushRef(a, h);
          break;
        case 'INV_TRASLADO_ENTRADA':
          a.trasladoEntradaKg += kg;
          break;
        case 'INV_TRASLADO_SALIDA':
          a.trasladoSalidaKg += kg;
          break;
        case 'INV_CONSUMO':
          a.consumoBodegaKg += kg;
          break;
        case 'VENTA_AVES':
          a.ventaH += h.cantidadHembras ?? 0;
          a.ventaM += h.cantidadMachos ?? 0;
          a.ventaX += h.cantidadMixtas ?? 0;
          pushRef(a, h);
          break;
        default:
          break;
      }
    }

    return map;
  }

  private formatKgNumber(n: number): string {
    return Number(n.toFixed(3)).toString();
  }

  /** Aves al inicio del ciclo (hembras + machos del lote, o aves encasetadas). */
  private avesInicialesLote(): number {
    const l = this.selectedLote as Record<string, unknown> | null;
    if (!l) return 0;
    const h = Number(l['hembrasL'] ?? 0);
    const m = Number(l['machosL'] ?? 0);
    if (h + m > 0) return Math.round(h + m);
    const av = l['avesEncasetadas'];
    if (av != null && av !== '') return Math.round(Number(av));
    return 0;
  }

  /** Aves iniciales por sexo (hembras/machos) — REQ-008a. Solo usa hembrasL/machosL: a diferencia de
   *  avesInicialesLote(), el fallback "avesEncasetadas" (combinado) no trae el split por sexo. */
  private avesInicialesPorGenero(): { h: number; m: number } {
    const l = this.selectedLote as Record<string, unknown> | null;
    if (!l) return { h: 0, m: 0 };
    const h = Number(l['hembrasL'] ?? 0);
    const m = Number(l['machosL'] ?? 0);
    return { h: Math.round(h) || 0, m: Math.round(m) || 0 };
  }

  private metaStr(seg: SeguimientoLoteLevanteDto, ...keys: string[]): string {
    const raw = seg.metadata;
    if (!raw || typeof raw !== 'object') return '';
    const m = raw as Record<string, unknown>;
    for (const k of keys) {
      const v = m[k];
      if (v != null && String(v).trim() !== '') return String(v).trim();
    }
    return '';
  }

  private metaNum(seg: SeguimientoLoteLevanteDto, ...keys: string[]): number | null {
    const raw = seg.metadata;
    if (!raw || typeof raw !== 'object') return null;
    const m = raw as Record<string, unknown>;
    for (const k of keys) {
      const v = m[k];
      if (v == null || v === '') continue;
      const n = Number(v);
      if (!Number.isNaN(n)) return n;
    }
    return null;
  }

  private tipoAlimentoCorto(tipo: string | null | undefined): string {
    const t = (tipo ?? '').toUpperCase();
    if (t.includes('PRE')) return 'PRE';
    if (t.includes('INI')) return 'INI';
    if (t.includes('ENG')) return 'ENG';
    if (t.includes('FIN')) return 'FIN-D';
    if (!tipo?.trim()) return '—';
    return tipo.length > 8 ? tipo.slice(0, 8) + '…' : tipo;
  }

  /** Ej. "vie 16 ene" (es-EC). */
  formatDiaSemanaCorto(iso: string | Date | null | undefined): string {
    const ymd = this.toYMD(iso);
    if (!ymd) return '';
    const d = new Date(`${ymd}T12:00:00`);
    if (isNaN(d.getTime())) return '';
    try {
      return new Intl.DateTimeFormat('es-EC', { weekday: 'short', day: 'numeric', month: 'short' }).format(d);
    } catch {
      return '';
    }
  }

  // ================== EVENTOS ==================
  onTabChange(tab: 'general' | 'indicadores' | 'grafica'): void {
    this.activeTab = tab;
  }


  onCreate(): void {
    this.create.emit();
  }

  onEdit(seg: SeguimientoLoteLevanteDto): void {
    this.edit.emit(seg);
  }

  onDelete(id: number): void {
    this.delete.emit(id);
  }

  onViewDetail(seg: SeguimientoLoteLevanteDto): void {
    this.viewDetail.emit(seg);
  }

  /** Exporta las mismas columnas que la tabla «Registros Diarios» + cabecera detallada del lote.
   *  Feature 13: incluye traslados por género, saldo de aves vivas por fila, y usuario que registró. */
  exportSeguimientoDiarioExcel(): void {
    if (!this.showExportSeguimientoExcel || !this.diarioFilas?.length) return;

    // ─── Cabecera detallada del lote ───────────────────────────────────
    const sel = this.selectedLote as any;
    const r = this.resumenLevante;
    const lpl = sel && 'lotePosturaLevanteId' in (sel ?? {}) ? sel as any : null;

    const granjaNombre = sel?.farm?.name ?? lpl?.farm?.name ?? '—';
    const nucleoNombre = sel?.nucleo?.nucleoNombre ?? lpl?.nucleo?.nucleoNombre ?? '—';
    const galponNombre = sel?.galpon?.galponNombre ?? lpl?.galpon?.galponNombre ?? '—';
    const fase = lpl ? 'Levante' : (sel?.fase ?? 'Levante');
    const raza = sel?.raza ?? lpl?.raza ?? '—';
    const fechaEncaset = sel?.fechaEncaset ?? lpl?.fechaEncaset ?? null;
    const hembrasIni = (r?.hembrasIniciales ?? sel?.hembrasL ?? 0) as number;
    const machosIni  = (r?.machosIniciales  ?? sel?.machosL  ?? 0) as number;
    const avesIni = hembrasIni + machosIni;
    const saldoH = r?.saldoHembras ?? 0;
    const saldoM = r?.saldoMachos  ?? 0;
    const totMortAcumH = r?.mortalidadAcumHembras ?? 0;
    const totMortAcumM = r?.mortalidadAcumMachos  ?? 0;
    const totSelAcumH  = r?.selAcumHembras ?? 0;
    const totSelAcumM  = r?.selAcumMachos  ?? 0;
    const trasInH  = (r as any)?.levanteTrasladoIngresoHembras ?? 0;
    const trasInM  = (r as any)?.levanteTrasladoIngresoMachos  ?? 0;
    const trasOutH = (r as any)?.levanteTrasladoSalidaHembras  ?? 0;
    const trasOutM = (r as any)?.levanteTrasladoSalidaMachos   ?? 0;

    const loteNombre = this.exportSeguimientoLoteNombre.trim();
    const fechaGen = new Date();
    const fechaGenStr = `${String(fechaGen.getDate()).padStart(2,'0')}/${String(fechaGen.getMonth()+1).padStart(2,'0')}/${fechaGen.getFullYear()} ${String(fechaGen.getHours()).padStart(2,'0')}:${String(fechaGen.getMinutes()).padStart(2,'0')}`;

    const cabecera: (string | number)[][] = [
      ['Seguimiento Diario de Levante'],
      [`Generado: ${fechaGenStr}`],
      [],
      ['INFORMACIÓN DEL LOTE'],
      ['Lote:', loteNombre || '—', '', 'Fase:', fase],
      ['Granja:', granjaNombre, '', 'Núcleo:', nucleoNombre, '', 'Galpón:', galponNombre],
      ['Raza:', raza, '', 'Fecha encasetamiento:', fechaEncaset ? this.formatDMY(fechaEncaset) : '—'],
      // Cabecera de contexto: con machos ocultos se muestra solo la columna de hembras. Los TOTALES
      // se conservan tal cual — son el total de aves del lote, no un dato de machos.
      this.ocultaMachosEnPostura
        ? ['Hembras encasetadas:', hembrasIni, '', 'Total encasetadas:', avesIni]
        : ['Hembras encasetadas:', hembrasIni, '', 'Machos encasetados:', machosIni, '', 'Total encasetadas:', avesIni],
      this.ocultaMachosEnPostura
        ? ['Aves vivas (H):', saldoH, '', 'Total vivas:', saldoH + saldoM]
        : ['Aves vivas (H):', saldoH, '', 'Aves vivas (M):', saldoM, '', 'Total vivas:', saldoH + saldoM],
      this.ocultaMachosEnPostura
        ? ['Mortalidad acum. (H):', totMortAcumH]
        : ['Mortalidad acum. (H):', totMortAcumH, '', 'Mortalidad acum. (M):', totMortAcumM],
      this.ocultaMachosEnPostura
        ? ['Selección acum. (H):', totSelAcumH]
        : ['Selección acum. (H):', totSelAcumH, '', 'Selección acum. (M):', totSelAcumM],
      this.ocultaMachosEnPostura
        ? ['Ingreso traslados (H):', trasInH, '', 'Total ingresos:', trasInH + trasInM]
        : ['Ingreso traslados (H):', trasInH, '', 'Ingreso traslados (M):', trasInM, '', 'Total ingresos:', trasInH + trasInM],
      this.ocultaMachosEnPostura
        ? ['Salida traslados (H):', trasOutH, '', 'Total salidas:', trasOutH + trasOutM]
        : ['Salida traslados (H):',  trasOutH, '', 'Salida traslados (M):',  trasOutM, '', 'Total salidas:',  trasOutH + trasOutM],
      [],
      ['REGISTROS DIARIOS'],
      []
    ];

    // ─── Encabezados de tabla ──────────────────────────────────────────
    const headers = [
      'Fecha',
      'Semana',
      'Edad (días vida)',
      'Mortalidad hembras',
      ...this.soloConMachos('Mortalidad machos'),
      'Selección hembras',
      ...this.soloConMachos('Selección machos'),
      // El error de sexaje desaparece como CONCEPTO (los dos géneros), igual que en los formularios.
      ...this.soloConMachos('Error sexaje hembras'),
      ...this.soloConMachos('Error sexaje machos'),
      'TOTAL MORT+ SEL hembras / día',
      ...this.soloConMachos('TOTAL MORT+ SEL machos / día'),
      // 🔀 Feature 13 — traslados dedicados por género
      'Ingreso traslado hembras',
      ...this.soloConMachos('Ingreso traslado machos'),
      'Salida traslado hembras',
      ...this.soloConMachos('Salida traslado machos'),
      ...(this.enriquecerTablaConHistoricoInventario
        ? ['Despacho mixtas', 'Consumo bodega (kg)', 'Saldo alimento (kg)']
        : []),
      'Saldo hembras',
      ...this.soloConMachos('Saldo machos'),
      'Tipo alimento',
      'Consumo kg hembras',
      ...this.soloConMachos('Consumo kg machos'),
      'Consumo real día (kg)',
      'Consumo acumulado (kg)',
      'Consumo acum. hembras (kg)',
      ...this.soloConMachos('Consumo acum. machos (kg)'),
      '% Retiro (Mort+Sel)/aves',
      'Peso prom. hembras (kg)',
      ...this.soloConMachos('Peso prom. machos (kg)'),
      'Uniformidad hembras (%)',
      ...this.soloConMachos('Uniformidad machos (%)'),
      'C.V. hembras (%)',
      ...this.soloConMachos('C.V. machos (%)'),
      'Observaciones',
      // 🥚 TK-2026-000021: los huevos son tema de PRODUCCIÓN y salieron de este seguimiento (tabla
      // y Excel). Lo que NO se toca: la captura en el registro diario, el desglose en el detalle
      // (👁️) y el arrastre de huevos hacia el lote de producción al cerrar el levante.
      // Auditoría
      'Registrado por',
      'Fecha registro',
      'Última actualización',
      'Actualizado por'
    ];

    const rows = this.diarioFilas.map(f => {
      const s: any = f.seg;
      return [
        this.formatDMY(s.fechaRegistro),
        f.semana ?? '—',
        f.edadDia ?? '—',
        s.mortalidadHembras ?? 0,
        ...this.soloConMachos(s.mortalidadMachos ?? 0),
        s.selH ?? 0,
        ...this.soloConMachos(s.selM ?? 0),
        ...this.soloConMachos(s.errorSexajeHembras ?? 0),
        ...this.soloConMachos(s.errorSexajeMachos ?? 0),
        f.totalMortSelDiaH,
        ...this.soloConMachos(f.totalMortSelDiaM),
        s.trasladoIngresoHembras ?? 0,
        ...this.soloConMachos(s.trasladoIngresoMachos ?? 0),
        s.trasladoSalidaHembras  ?? 0,
        ...this.soloConMachos(s.trasladoSalidaMachos ?? 0),
        ...(this.enriquecerTablaConHistoricoInventario
          ? [
              f.despachoX ?? '',
              f.consumoBodegaKg != null ? f.consumoBodegaKg : '',
              f.seg.saldoAlimentoKg != null ? f.seg.saldoAlimentoKg : ''
            ]
          : []),
        f.saldoAvesH,
        ...this.soloConMachos(f.saldoAvesM),
        f.tipoAlimentoCorto,
        s.consumoKgHembras ?? 0,
        ...this.soloConMachos(s.consumoKgMachos ?? 0),
        f.consumoDiaKg,
        f.acumConsumoKg,
        f.acumConsumoHKg,
        ...this.soloConMachos(f.acumConsumoMKg),
        f.pctRetiroSemana != null ? Math.round(f.pctRetiroSemana * 100) / 100 : '',
        s.pesoPromH != null ? s.pesoPromH : '',
        ...this.soloConMachos(s.pesoPromM != null ? s.pesoPromM : ''),
        s.uniformidadH != null ? s.uniformidadH : '',
        ...this.soloConMachos(s.uniformidadM != null ? s.uniformidadM : ''),
        s.cvH != null ? s.cvH : '',
        ...this.soloConMachos(s.cvM != null ? s.cvM : ''),
        (s.observaciones || '').trim() || '—',
        s.createdByUserId ?? '—',
        s.createdAt ? this.formatDMY(s.createdAt) : '—',
        s.updatedAt ? this.formatDMY(s.updatedAt) : '—',
        s.updatedByUserId ?? '—'
      ];
    });

    const aoa: (string | number)[][] = [...cabecera, headers, ...rows];

    // Anchos de columnas razonables para que el Excel se vea bien al abrir
    const colWidths = Array.from({ length: headers.length }, (_, i) => (i === 0 ? 14 : i === headers.length - 4 ? 28 : 18));

    const safe = (loteNombre || 'lote').replace(/[\\/:*?"<>|]/g, '_');
    const d = new Date();
    const stamp = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
    exportarAoaExcel(aoa, 'Seguimiento', {
      colWidths,
      filenameFull: `Seguimiento_Diario_de_Levante_${safe}_${stamp}.xlsx`,
    });
  }

  // ================== CALCULO DE EDAD ==================
  /**
   * Edad del lote en la fecha del registro (días de calendario desde encasetamiento).
   * Retorna 0 si la fecha es igual al encasetamiento; para UI se muestra como día 1 (edad0 + 1).
   */
  calcularEdadDias(fechaRegistro: string | Date): number | null {
    if (!this.selectedLote?.fechaEncaset) return 0;
    const encYmd = this.toYMD(this.selectedLote.fechaEncaset);
    const regYmd = this.toYMD(fechaRegistro);
    if (!encYmd || !regYmd) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const enc = this.ymdToLocalNoonDate(encYmd);
    const reg = this.ymdToLocalNoonDate(regYmd);
    if (!enc || !reg) return 0;
    const diff = Math.floor((reg.getTime() - enc.getTime()) / MS_DAY);
    // REQ-011d: registro anterior al encasetamiento -> edad indefinida. Antes Math.max(0, diff)
    // clampeaba en silencio a día 0, haciendo que semana/edad quedaran "congeladas" sin avisar.
    if (diff < 0) return null;
    // Math.max(0, …) se conserva solo para el borde del mismo día (diff === 0, ya no negativo).
    return Math.max(0, diff);
  }

  private toYMD(input: string | Date | null | undefined): string | null {
    if (input == null || input === '') return null;
    if (input instanceof Date && !isNaN(input.getTime())) {
      return `${input.getFullYear()}-${String(input.getMonth() + 1).padStart(2, '0')}-${String(input.getDate()).padStart(2, '0')}`;
    }
    const s = String(input).trim();
    const head = s.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (head) return `${head[1]}-${head[2]}-${head[3]}`;
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    return null;
  }

  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) return null;
    const d = new Date(`${ymd}T12:00:00`);
    return isNaN(d.getTime()) ? null : d;
  }

  formatDMY(input: string | Date | null | undefined): string {
    const ymd = this.toYMD(input);
    if (!ymd) return '';
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  }

  // ─── Doble validación ───────────────────────────────────────────────────────
  // La tabla no decide nada: recibe el mapa `seguimientoId → estado` que arma el contenedor desde el
  // backend, que es el único que conoce el flag de la empresa y el plazo.

  /** Flag de la empresa. En false la columna Estado no se muestra y nada cambia. */
  @Input() requiereValidacion = false;
  /** Permiso de validar. Sin él la columna se ve, pero sin el botón. */
  @Input() puedeValidar = false;
  /** seguimientoId → estado. Solo trae los NO validados: lo ausente ya se descontó. */
  @Input() estadoValidacionPorId = new Map<number, string>();

  @Output() validar = new EventEmitter<number>();

  estadoValidacionFila(id: number | null | undefined): string {
    if (id == null) return 'VALIDADO';
    return this.estadoValidacionPorId.get(id) ?? 'VALIDADO';
  }

  etiquetaValidacionFila(id: number | null | undefined): string {
    switch (this.estadoValidacionFila(id)) {
      case 'EN_RETRASO': return 'En retraso';
      case 'PENDIENTE':  return 'Pendiente';
      default:           return 'Validado';
    }
  }

  claseBadgeValidacion(id: number | null | undefined): string {
    switch (this.estadoValidacionFila(id)) {
      case 'EN_RETRASO': return 'badge-validacion badge-validacion--retraso';
      case 'PENDIENTE':  return 'badge-validacion badge-validacion--pendiente';
      default:           return 'badge-validacion badge-validacion--validado';
    }
  }

  /** Clase de la FILA: solo se pinta la vencida, para que el rojo siga significando algo. */
  claseFilaValidacion(id: number | null | undefined): string {
    return this.estadoValidacionFila(id) === 'EN_RETRASO' ? 'fila-validacion--retraso' : '';
  }

  tooltipValidacionFila(id: number | null | undefined): string {
    switch (this.estadoValidacionFila(id)) {
      case 'EN_RETRASO':
        return 'En retraso — superó el plazo de validación. Mientras no se valide, el lote no acepta días nuevos.';
      case 'PENDIENTE':
        return 'Pendiente de validar — el alimento y las aves están separados, todavía no descontados. Se puede editar y eliminar.';
      default:
        return 'Validado — el alimento y las aves ya se descontaron. El registro es de solo lectura.';
    }
  }

  puedeValidarFila(id: number | null | undefined): boolean {
    return this.puedeValidar && this.estadoValidacionFila(id) !== 'VALIDADO';
  }

  /** Un registro validado es de solo lectura: hay que quitarle la validación para corregirlo. */
  esSoloLecturaPorValidacion(id: number | null | undefined): boolean {
    return this.requiereValidacion && this.estadoValidacionFila(id) === 'VALIDADO';
  }

  onValidar(id: number | null | undefined): void {
    if (id == null) return;
    this.validar.emit(id);
  }
}

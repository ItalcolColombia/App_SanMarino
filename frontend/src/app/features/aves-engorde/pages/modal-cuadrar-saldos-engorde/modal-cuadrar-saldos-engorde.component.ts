import {
  Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, HostListener
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import { LoteDto } from '../../../../features/lote/services/lote.service';
import {
  SeguimientoAvesEngordeService,
  FilaExcelCuadrarSaldosDto,
  InconsistenciaCuadrarSaldosDto,
  AccionCorreccionCuadrarSaldosDto,
  CuadrarSaldosValidarResponseDto,
  CuadrarSaldosAplicarResponseDto
} from '../../services/seguimiento-aves-engorde.service';

type ModalStep = 'upload' | 'validating' | 'validated' | 'applying' | 'applied';

@Component({
  selector: 'app-modal-cuadrar-saldos-engorde',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './modal-cuadrar-saldos-engorde.component.html',
  styleUrls: ['./modal-cuadrar-saldos-engorde.component.scss']
})
export class ModalCuadrarSaldosEngordeComponent implements OnChanges {
  @Input() isOpen = false;
  @Input() selectedLote: LoteDto | null = null;

  /** Emitido al cerrar el modal. */
  @Output() cerrar = new EventEmitter<void>();
  /** Emitido cuando se aplicaron correcciones y hay que recargar datos. */
  @Output() saldosAplicados = new EventEmitter<void>();

  step: ModalStep = 'upload';
  errorMensaje = '';

  // Upload
  archivoNombre = '';
  filasExcel: FilaExcelCuadrarSaldosDto[] = [];
  filasPreview: FilaExcelCuadrarSaldosDto[] = [];

  // Validación
  validarResponse: CuadrarSaldosValidarResponseDto | null = null;
  accionesSugeridas: AccionCorreccionCuadrarSaldosDto[] = [];
  /** Acciones marcadas por el usuario para aplicar (por defecto todas). */
  accionesSeleccionadas = new Set<number>();

  // Aplicar
  aplicarResponse: CuadrarSaldosAplicarResponseDto | null = null;

  constructor(private segSvc: SeguimientoAvesEngordeService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) this.reset();
  }

  @HostListener('document:keydown.escape')
  onEsc(): void {
    if (this.isOpen && this.step !== 'validating' && this.step !== 'applying') this.onCerrar();
  }

  get loteId(): number | null {
    return this.selectedLote?.loteId ?? null;
  }

  get puedeValidar(): boolean {
    return this.filasExcel.length > 0 && this.step === 'upload';
  }

  get inconsistencias(): InconsistenciaCuadrarSaldosDto[] {
    return this.validarResponse?.inconsistencias ?? [];
  }

  get hayInconsistencias(): boolean {
    return this.inconsistencias.length > 0;
  }

  get accionesParaAplicar(): AccionCorreccionCuadrarSaldosDto[] {
    return this.accionesSugeridas.filter((_, i) => this.accionesSeleccionadas.has(i));
  }

  get etiquetaTipoInconsistencia(): (tipo: string) => string {
    return (tipo: string) => {
      const map: Record<string, string> = {
        INGRESO_FALTANTE: 'Ingreso faltante',
        INGRESO_SOBRANTE: 'Ingreso sobrante',
        INGRESO_MONTO_DIFERENTE: 'Ingreso: monto diferente',
        TRASLADO_ENTRADA_FALTANTE: 'Traslado entrada faltante',
        TRASLADO_ENTRADA_SOBRANTE: 'Traslado entrada sobrante',
        TRASLADO_ENTRADA_DIFERENTE: 'Traslado entrada: diferente',
        TRASLADO_SALIDA_FALTANTE: 'Traslado salida faltante',
        TRASLADO_SALIDA_SOBRANTE: 'Traslado salida sobrante',
        TRASLADO_SALIDA_DIFERENTE: 'Traslado salida: diferente',
        SALDO_DIFERENTE: 'Saldo diferente',
        DOCUMENTO_DIFERENTE: 'Documento diferente'
      };
      return map[tipo] ?? tipo;
    };
  }

  get etiquetaTipoAccion(): (tipo: string) => string {
    return (tipo: string) => {
      const map: Record<string, string> = {
        AJUSTAR_FECHA: 'Ajustar fecha',
        ANULAR: 'Anular registro',
        INSERTAR: 'Insertar registro'
      };
      return map[tipo] ?? tipo;
    };
  }

  get badgeAccion(): (tipo: string) => string {
    return (tipo: string) => {
      if (tipo === 'AJUSTAR_FECHA') return 'badge--info';
      if (tipo === 'ANULAR') return 'badge--danger';
      if (tipo === 'INSERTAR') return 'badge--success';
      return 'badge--neutral';
    };
  }

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.errorMensaje = '';
    this.archivoNombre = file.name;
    this.parsearExcel(file);
    input.value = '';
  }

  private parsearExcel(file: File): void {
    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const wb = XLSX.read(e.target?.result as string, { type: 'binary', cellDates: true });
        const ws = wb.Sheets[wb.SheetNames[0]];
        const rows: unknown[][] = XLSX.utils.sheet_to_json(ws, { header: 1, defval: null }) as unknown[][];

        // Encontrar fila de encabezado (primera que tenga "fecha")
        let headerIdx = -1;
        for (let i = 0; i < Math.min(15, rows.length); i++) {
          const r = rows[i];
          if (r?.some(c => typeof c === 'string' && /fecha/i.test(c as string))) {
            headerIdx = i;
            break;
          }
        }
        if (headerIdx === -1) {
          this.errorMensaje = 'No se encontró fila de encabezado con columna "Fecha" en el Excel.';
          return;
        }

        const headers = (rows[headerIdx] as (string | null)[]).map(h =>
          (h ?? '').toString().trim().toLowerCase()
        );

        const col = (...names: string[]): number =>
          names.map(n => headers.indexOf(n)).find(i => i >= 0) ?? -1;

        const colFecha    = col('fecha');
        const colSaldo    = col('saldo alimento', 'saldo de alimento', 'saldo alimento (kg)', 'saldo_alimento', 'saldo');
        const colIngreso  = col('ingreso alimento', 'ingreso de alimento', 'ingreso alimento (kg)', 'ingreso_alimento', 'ingreso');
        const colTrEnt    = col('traslado entrada', 'traslado_entrada', 'entrada alimento', 'traslado entrada (kg)');
        const colTrSal    = col('traslado salida', 'traslado_salida', 'salida alimento', 'traslado salida (kg)');
        const colTrNet    = colTrEnt === -1 ? col('traslado', 'traslado (kg)') : -1;
        const colDoc      = col('documento', 'doc', 'referencia', 'nro documento', 'numero documento');
        const colCons     = col('consumo en kilos', 'consumo (kg)', 'consumo kg', 'consumo_kg', 'consumo real día (kg)', 'consumo real dia (kg)', 'consumo');
        const colAcum     = col('consumo acumulado', 'consumo acumulado (kg)', 'acumulado');

        if (colFecha === -1) {
          this.errorMensaje = 'No se encontró la columna "Fecha" en el encabezado del Excel.';
          return;
        }

        const getNum = (row: unknown[], c: number): number | null => {
          if (c === -1 || row[c] == null || row[c] === '') return null;
          const n = Number(row[c]);
          return isNaN(n) ? null : n;
        };

        const getStr = (row: unknown[], c: number): string | null => {
          if (c === -1 || row[c] == null) return null;
          const s = String(row[c]).trim();
          return s || null;
        };

        const parseFecha = (raw: unknown): string => {
          if (raw instanceof Date) return this.formatDateYMD(raw);
          const s = String(raw ?? '').trim();
          const dmy = s.match(/^(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})/);
          if (dmy) return `${dmy[3]}-${dmy[2].padStart(2, '0')}-${dmy[1].padStart(2, '0')}`;
          const ymd = s.match(/^(\d{4})-(\d{2})-(\d{2})/);
          if (ymd) return `${ymd[1]}-${ymd[2]}-${ymd[3]}`;
          // Número serial de Excel (días desde 1900-01-01)
          if (typeof raw === 'number' && raw > 1000) {
            const d = XLSX.SSF.parse_date_code(raw);
            if (d) return `${d.y}-${String(d.m).padStart(2, '0')}-${String(d.d).padStart(2, '0')}`;
          }
          return '';
        };

        const result: FilaExcelCuadrarSaldosDto[] = [];
        for (let i = headerIdx + 1; i < rows.length; i++) {
          const row = rows[i] as unknown[];
          if (!row || row[colFecha] == null || row[colFecha] === '') continue;
          const fecha = parseFecha(row[colFecha]);
          if (!fecha) continue;

          let trasladoEntradaKg = getNum(row, colTrEnt);
          let trasladoSalidaKg  = getNum(row, colTrSal);
          if (colTrNet >= 0) {
            const t = getNum(row, colTrNet);
            if (t != null) {
              if (t >= 0) trasladoEntradaKg = t;
              else trasladoSalidaKg = Math.abs(t);
            }
          }

          result.push({
            fecha,
            saldoAlimentoKg:     getNum(row, colSaldo),
            ingresoAlimentoKg:   getNum(row, colIngreso),
            trasladoEntradaKg,
            trasladoSalidaKg,
            documento:           getStr(row, colDoc),
            consumoKg:           getNum(row, colCons),
            consumoAcumuladoKg:  getNum(row, colAcum)
          });
        }

        if (result.length === 0) {
          this.errorMensaje = 'El Excel no contiene filas de datos válidas (con fechas reconocibles).';
          return;
        }

        this.filasExcel = result;
        this.filasPreview = result.slice(0, 5);
        this.errorMensaje = '';
      } catch (err) {
        this.errorMensaje = `Error al leer el Excel: ${(err as Error)?.message ?? err}`;
      }
    };
    reader.onerror = () => { this.errorMensaje = 'No se pudo leer el archivo.'; };
    reader.readAsBinaryString(file);
  }

  onValidar(): void {
    if (!this.loteId || !this.puedeValidar) return;
    this.step = 'validating';
    this.errorMensaje = '';

    this.segSvc.cuadrarSaldosValidar(this.loteId, this.filasExcel).subscribe({
      next: res => {
        this.validarResponse = res;
        this.accionesSugeridas = res.accionesSugeridas ?? [];
        // Seleccionar todas las acciones por defecto
        this.accionesSeleccionadas = new Set(this.accionesSugeridas.map((_, i) => i));
        this.step = 'validated';
      },
      error: err => {
        this.errorMensaje = err?.error?.message ?? 'Error al validar. Intente de nuevo.';
        this.step = 'upload';
      }
    });
  }

  toggleAccion(idx: number): void {
    if (this.accionesSeleccionadas.has(idx)) this.accionesSeleccionadas.delete(idx);
    else this.accionesSeleccionadas.add(idx);
  }

  seleccionarTodasAcciones(): void {
    this.accionesSeleccionadas = new Set(this.accionesSugeridas.map((_, i) => i));
  }

  deseleccionarTodasAcciones(): void {
    this.accionesSeleccionadas.clear();
  }

  onAplicar(): void {
    const acciones = this.accionesParaAplicar;
    if (!this.loteId || acciones.length === 0) return;
    this.step = 'applying';
    this.errorMensaje = '';

    this.segSvc.cuadrarSaldosAplicar(this.loteId, acciones).subscribe({
      next: res => {
        this.aplicarResponse = res;
        this.step = 'applied';
        this.saldosAplicados.emit();
      },
      error: err => {
        this.errorMensaje = err?.error?.message ?? 'Error al aplicar correcciones.';
        this.step = 'validated';
      }
    });
  }

  onCerrar(): void {
    this.cerrar.emit();
  }

  volverAUpload(): void {
    this.reset();
  }

  private reset(): void {
    this.step = 'upload';
    this.errorMensaje = '';
    this.archivoNombre = '';
    this.filasExcel = [];
    this.filasPreview = [];
    this.validarResponse = null;
    this.accionesSugeridas = [];
    this.accionesSeleccionadas = new Set();
    this.aplicarResponse = null;
  }

  private formatDateYMD(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  formatDMY(ymd: string): string {
    if (!ymd) return '';
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  }

  formatNum(v: number | null | undefined, dec = 3): string {
    if (v == null) return '—';
    return v.toFixed(dec);
  }
}

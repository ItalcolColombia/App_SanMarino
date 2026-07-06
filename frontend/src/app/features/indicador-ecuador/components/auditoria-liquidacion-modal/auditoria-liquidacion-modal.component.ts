// frontend/src/app/features/indicador-ecuador/components/auditoria-liquidacion-modal/auditoria-liquidacion-modal.component.ts
import { Component, EventEmitter, Input, Output, ChangeDetectionStrategy } from '@angular/core';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { exportarAoaExcel } from '../../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { IndicadorEcuadorService } from '../../services/indicador-ecuador.service';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import {
  AuditoriaLiquidacionResultado,
  AuditoriaScopeInput,
  AuditReconRow,
  AuditHallazgo,
  PLANTILLA_INDICADORES
} from '../../models/auditoria-liquidacion.model';

/**
 * Modal "Verificar liquidación": el usuario sube el Excel correcto (formato vertical etiqueta|valor),
 * el back lo parsea y la BD devuelve el análisis (reconciliación + hallazgos + simulación).
 * Este componente SOLO organiza y pinta el resultado.
 */
@Component({
  selector: 'app-auditoria-liquidacion-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './auditoria-liquidacion-modal.component.html'
})
export class AuditoriaLiquidacionModalComponent {
  @Input() scope!: AuditoriaScopeInput;
  @Output() cerrar = new EventEmitter<void>();

  archivo: File | null = null;
  cargando = false;
  error: string | null = null;
  resultado: AuditoriaLiquidacionResultado | null = null;

  // Aplicar corrección
  kgAplicar: number | null = null;
  aplicando = false;
  mensajeOk: string | null = null;

  constructor(private confirmDialog: ConfirmDialogService, private svc: IndicadorEcuadorService) {}

  /** Hay corrección aplicable: el gap es atribuible a despachos sin peso. */
  get puedeAplicar(): boolean {
    return !!this.resultado?.simulacion?.atribuibleASinPeso;
  }

  onArchivo(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    this.archivo = input.files?.[0] ?? null;
    this.error = null;
  }

  analizar(): void {
    if (!this.archivo) { this.error = 'Seleccione el archivo Excel correcto.'; return; }
    if (!this.scope?.granjaId) { this.error = 'No hay alcance (granja) para auditar. Genere primero la liquidación.'; return; }
    this.cargando = true;
    this.error = null;
    this.resultado = null;
    this.svc.auditarLiquidacion(this.scope, this.archivo).subscribe({
      next: (r) => {
        this.cargando = false;
        if (r?.error) { this.error = r.error; return; }
        this.resultado = r;
        const gap = r.simulacion?.gapKg ?? 0;
        this.kgAplicar = gap > 0 ? Math.round(gap) : null;
      },
      error: (e) => {
        this.cargando = false;
        this.error = e?.error?.error ?? e?.error?.message ?? e?.message ?? 'Error al auditar la liquidación.';
      }
    });
  }

  reiniciar(): void {
    this.resultado = null; this.archivo = null; this.error = null;
    this.mensajeOk = null; this.kgAplicar = null;
  }
  close(): void { this.cerrar.emit(); }

  /**
   * Aplica la corrección sugerida (carga kgAplicar en los despachos sin peso). Pide confirmación,
   * llama el endpoint gateado y re-ejecuta la verificación con el mismo archivo para mostrar el cuadre.
   */
  async aplicarCorreccion(): Promise<void> {
    const kg = this.kgAplicar ?? 0;
    if (!kg || kg <= 0) { this.error = 'Indique los kg a aplicar (mayor a 0).'; return; }
    const ok = await this.confirmDialog.ask({
      title: 'Aplicar corrección',
      message:
        `Se cargarán ${kg.toLocaleString('es-EC')} kg en los despachos sin peso de la corrida ` +
        `(se distribuyen por aves). Esto MODIFICA los movimientos. ¿Continuar?`,
      type: 'warning',
      confirmText: 'Continuar',
    });
    if (!ok) return;
    this.aplicando = true; this.error = null; this.mensajeOk = null;
    this.svc.aplicarCorreccionLiquidacion(this.scope, kg).subscribe({
      next: (r) => {
        this.aplicando = false;
        if (!r?.ok) { this.error = r?.error ?? 'No se pudo aplicar la corrección.'; return; }
        this.mensajeOk = `Corrección aplicada: ${r.movimientos} despacho(s), ${kg.toLocaleString('es-EC')} kg. Re-verificado.`;
        if (this.archivo) this.analizar();
      },
      error: (e) => {
        this.aplicando = false;
        this.error = e?.status === 403
          ? 'No tienes permiso para aplicar la corrección (liquidacion.aplicar_correccion).'
          : (e?.error?.error ?? e?.error?.message ?? e?.message ?? 'Error al aplicar la corrección.');
      }
    });
  }

  /**
   * Descarga una plantilla .xlsx con el formato exacto que espera el verificador:
   * etiquetas en la columna A y la columna B (VALOR) en blanco para llenar con NÚMEROS (sin fórmulas).
   * Evita el caso de subir un archivo con #DIV/0! o valores en celdas equivocadas.
   */
  descargarPlantilla(): void {
    const rows: (string | number | null)[][] = [
      ['ECU - ITALCOL S.A.'],
      ['GRANJA', this.scope?.granjaId ?? ''],
      ['LOTE', this.scope?.loteCodigo ?? ''],
      [],
      ['ANÁLISIS TÉCNICO'],
      ['Indicador', 'VALOR'],
      ...PLANTILLA_INDICADORES.map((l) => [l, null] as (string | number | null)[]),
      [],
      ['Instrucciones:'],
      ['• Llene SOLO la columna VALOR con números (sin fórmulas, sin #DIV/0!).'],
      ['• Use punto (.) como separador decimal. Los miles pueden ir con coma o sin ella.'],
      ['• No cambie los nombres de la columna Indicador.']
    ];
    exportarAoaExcel(rows, 'Liquidacion', {
      colWidths: [36, 16],
      filenameFull: 'plantilla_verificacion_liquidacion.xlsx',
    });
  }

  // ── Helpers de presentación (triviales, específicos del modal) ──
  filaClase(r: AuditReconRow): string {
    if (!r.tieneExcel) return 'bg-gray-50 text-gray-400';
    if (r.cuadra) return 'bg-green-50';
    return r.clase === 'dato' ? 'bg-red-50' : 'bg-amber-50';
  }

  estadoTexto(r: AuditReconRow): string {
    if (!r.tieneExcel) return 'sin dato Excel';
    if (r.cuadra) return 'cuadra';
    return r.clase === 'dato' ? 'falla de dato' : 'dif. de definición';
  }

  sevBorde(sev: string): string {
    return sev === 'critico' ? 'border-l-red-500 bg-red-50'
         : sev === 'alerta'  ? 'border-l-amber-500 bg-amber-50'
         : 'border-l-sky-400 bg-sky-50';
  }

  sevBadge(sev: string): string {
    return sev === 'critico' ? 'bg-red-600 text-white'
         : sev === 'alerta'  ? 'bg-amber-500 text-white'
         : 'bg-sky-500 text-white';
  }

  num(v: number | null | undefined): string {
    if (v === null || v === undefined) return '—';
    return v.toLocaleString('es-EC', { maximumFractionDigits: 2 });
  }

  /** Columnas de la tabla de registros de un hallazgo (claves del primer registro). */
  columnasRegistro(h: AuditHallazgo): string[] {
    const first = h.registros?.[0];
    return first ? Object.keys(first) : [];
  }
}

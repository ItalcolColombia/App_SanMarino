// frontend/src/app/features/traslados-aves/components/comprobante-traslado-aves/comprobante-traslado-aves.component.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MovimientoAvesCompleto, UbicacionCompleta } from '../../../../core/services/traslado-navigation/traslado-navigation.service';

/** Una fila etiqueta/valor del comprobante. */
export interface FilaComprobante {
  label: string;
  value: string;
}

/**
 * Comprobante imprimible de un traslado de aves — cierra `TK-2026-000180` / `SR-DEF-5` (F9.2c).
 *
 * <p>Es el **primer comprobante del repo**. Sigue el patrón de
 * `indicador-ecuador/components/liquidacion-reporte-panama`: componente standalone y tonto
 * (recibe los datos ya armados por `@Input()`), con `print()` = `window.print()` y el documento
 * maquetado en CSS con `@media print`. **No se agregó ninguna librería de PDF**: no hay ninguna en
 * el repo (solo `xlsx` en el front y ClosedXML/EPPlus en el back) y el navegador ya imprime a PDF.</p>
 *
 * <p>La fuente es `GET api/TrasladoNavigation/{id}` (`MovimientoAvesCompletoDto`), que ya trae
 * origen y destino completos más placa/conductor/sellos.</p>
 */
@Component({
  selector: 'app-comprobante-traslado-aves',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './comprobante-traslado-aves.component.html',
  styleUrls: ['./comprobante-traslado-aves.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager
})
export class ComprobanteTrasladoAvesComponent implements OnChanges {
  @Input({ required: true }) data!: MovimientoAvesCompleto;

  /** Razón social del encabezado; sale de la empresa activa. */
  @Input() empresa = '';

  /**
   * Empresas con `ocultaMachosEnPostura` no manejan machos en ningún lado (decisión del usuario,
   * 24-ago-2026): el comprobante tampoco los imprime.
   */
  @Input() ocultaMachos = false;

  /** false cuando va embebido y el contenedor pone sus propios botones. */
  @Input() mostrarAcciones = true;

  @Output() cerrar = new EventEmitter<void>();

  /**
   * Filas ya construidas. Se calculan en `ngOnChanges` y NO en un getter: un getter que arma
   * arrays nuevos por ciclo de change detection rompe la estabilidad de referencias que pide
   * CLAUDE.md.
   */
  filasGenerales: FilaComprobante[] = [];
  filasOrigen: FilaComprobante[] = [];
  filasDestino: FilaComprobante[] = [];
  filasCantidades: FilaComprobante[] = [];
  filasTransporte: FilaComprobante[] = [];

  /** Sin ningún dato de transporte no se imprime la sección (evita un bloque de guiones). */
  hayTransporte = false;

  ngOnChanges(): void {
    if (!this.data) return;

    this.filasGenerales = [
      { label: 'N.º de movimiento', value: this.texto(this.data.numeroMovimiento) },
      { label: 'Fecha', value: this.fecha(this.data.fechaMovimiento) },
      { label: 'Tipo', value: this.texto(this.data.tipoMovimientoDescripcion || this.data.tipoMovimiento) },
      { label: 'Estado', value: this.texto(this.data.estado) },
      { label: 'Registrado por', value: this.texto(this.data.usuarioNombre) },
      { label: 'Procesado', value: this.fecha(this.data.fechaProcesamiento) }
    ];

    this.filasOrigen = this.filasUbicacion(this.data.origen);
    this.filasDestino = this.filasUbicacion(this.data.destino);

    const cantidades: FilaComprobante[] = [
      { label: 'Hembras', value: this.numero(this.data.cantidadHembras) }
    ];
    if (!this.ocultaMachos) {
      cantidades.push({ label: 'Machos', value: this.numero(this.data.cantidadMachos) });
      cantidades.push({ label: 'Mixtas', value: this.numero(this.data.cantidadMixtas) });
    }
    cantidades.push({ label: 'Total de aves', value: this.numero(this.data.totalAves) });
    this.filasCantidades = cantidades;

    const { placa, conductor, sellos } = this.data;
    this.hayTransporte = !!(placa || conductor || sellos);
    this.filasTransporte = [
      { label: 'Placa', value: this.texto(placa) },
      { label: 'Conductor', value: this.texto(conductor) },
      { label: 'Precinto / sellos', value: this.texto(sellos) }
    ];
  }

  print(): void {
    window.print();
  }

  private filasUbicacion(u: UbicacionCompleta | null | undefined): FilaComprobante[] {
    return [
      { label: 'Granja', value: this.texto(u?.granjaNombre) },
      { label: 'Núcleo', value: this.texto(u?.nucleoNombre ?? u?.nucleoId) },
      { label: 'Galpón', value: this.texto(u?.galponNombre ?? u?.galponId) },
      { label: 'Lote', value: this.texto(u?.loteNombre) },
      { label: 'Raza', value: this.texto(u?.raza) }
    ];
  }

  private texto(v: string | number | null | undefined): string {
    const s = v == null ? '' : String(v).trim();
    return s === '' ? '—' : s;
  }

  private numero(v: number | null | undefined): string {
    return v == null ? '—' : v.toLocaleString('es-CO');
  }

  private fecha(v: string | null | undefined): string {
    if (!v) return '—';
    const d = new Date(v);
    return isNaN(d.getTime()) ? '—' : d.toLocaleDateString('es-CO', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }
}

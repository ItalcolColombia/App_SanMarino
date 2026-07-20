// frontend/src/app/features/indicador-ecuador/components/liquidacion-reporte-panama/liquidacion-reporte-panama.component.ts
import { Component, EventEmitter, Input, Output, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { ReporteIndicadoresPanamaDto } from '../../services/indicador-ecuador.service';

/** Una fila del cuadro de resultados. */
interface FilaResultado {
  label: string;
  value: number | null | undefined;
  /** dígitos decimales a mostrar */
  dec?: number;
  /** sufijo (ej: "Día(s)", "%") */
  suffix?: string;
  /** resalta la fila (totales / claves) */
  highlight?: boolean;
}

@Component({
  selector: 'app-liquidacion-reporte-panama',
  standalone: true,
  imports: [CommonModule],
  providers: [DecimalPipe],
  templateUrl: './liquidacion-reporte-panama.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./liquidacion-reporte-panama.component.scss']
})
export class LiquidacionReportePanamaComponent {
  @Input({ required: true }) data!: ReporteIndicadoresPanamaDto;
  @Input() empresa = 'PAN - ITALCOL S.A.';
  /** Subtítulo del encabezado; null ⇒ el texto por defecto (empresa · Lote #id). */
  @Input() subtitulo: string | null = null;
  /** false cuando el reporte va embebido (ej. tabs de corrida): oculta Imprimir/Cerrar propios. */
  @Input() mostrarAcciones = true;

  @Output() cerrar = new EventEmitter<void>();

  print(): void {
    window.print();
  }

  /** Tarjetas hero (los 6 insumos digitados + aves encasetadas). */
  get heroCards(): Array<{ icon: string; label: string; value: number; dec: number; suffix?: string }> {
    const l = this.data.liquidacion;
    return [
      { icon: '📅', label: 'Días en Granja', value: l.diasEnGranja, dec: 0, suffix: ' día(s)' },
      { icon: '🐣', label: 'Días de Engorde', value: l.diasEngorde, dec: 0, suffix: ' día(s)' },
      { icon: '🐔', label: 'Aves Finales en Granja', value: l.avesFinalGranja, dec: 0 },
      { icon: '🏭', label: 'Aves Beneficiadas', value: l.avesBeneficiada, dec: 0 },
      { icon: '⚖️', label: 'Producción Kilo en Pie', value: l.produccionKiloPie, dec: 2, suffix: ' kg' },
      { icon: '📐', label: 'Metros Cuadrados', value: l.metrosCuadrados, dec: 0, suffix: ' m²' }
    ];
  }

  /** Filas del cuadro completo "RESULTADOS DE LIQUIDACIÓN". */
  get filas(): FilaResultado[] {
    const l = this.data.liquidacion;
    return [
      { label: 'Días de Engorde', value: l.diasEngorde, dec: 0, suffix: ' Día(s)' },
      { label: 'Días en Granja', value: l.diasEnGranja, dec: 0, suffix: ' Día(s)' },
      { label: 'Aves en Granja', value: l.avesFinalGranja, dec: 0 },
      { label: 'Aves Beneficiadas', value: l.avesBeneficiada, dec: 0 },
      { label: 'Producción Kilo en Pie', value: l.produccionKiloPie, dec: 2 },
      { label: 'Metros Cuadrados', value: l.metrosCuadrados, dec: 0 },
      { label: 'Aves Encasetadas', value: this.data.avesEncasetadas, dec: 0 },
      { label: 'Peso Promedio', value: l.pesoPromedio, dec: 2 },
      { label: '% Mortalidad', value: l.mortalidadPorc, dec: 2, suffix: '%' },
      { label: '% Seleccion', value: l.seleccionPorc, dec: 2, suffix: '%' },
      { label: '% Mortalidad Total', value: l.porcMortalidadTotal, dec: 2, suffix: '%', highlight: true },
      { label: 'Supervivencia', value: l.supervivencia, dec: 2, highlight: true },
      { label: 'Consumo Ave', value: l.consumoAve, dec: 2 },
      { label: 'Conversion', value: l.conversion, dec: 2, highlight: true },
      { label: 'Eficiencia Americana', value: l.eficienciaAmericana, dec: 2 },
      { label: 'E.E.F', value: l.eeF, dec: 2 },
      { label: 'E.E.F -2', value: l.eefDos, dec: 2 },
      { label: 'Aves/Mtr 2', value: l.avesMetrosCua, dec: 2 },
      { label: 'Kilos/Mtr 2', value: l.kilosMetrosCua, dec: 2 },
      { label: 'Productividad', value: l.productividad, dec: 2 },
      { label: 'Faltante Sobrante', value: l.faltanteSobra, dec: 0, highlight: true }
    ];
  }

  /** Información productiva (agregados del seguimiento). */
  get infoProductiva(): Array<{ icon: string; label: string; value: number; dec: number; suffix?: string }> {
    const i = this.data.infoProductiva;
    return [
      { icon: '🌾', label: 'Consumo Alimento Total', value: i.consumoAlimentoTotal, dec: 2, suffix: ' qq' },
      { icon: '✂️', label: 'Total Aves Selección', value: i.totalAvesSeleccion, dec: 0 },
      { icon: '💀', label: 'Total Aves Muertas', value: i.totalAvesMuertas, dec: 0 }
    ];
  }
}

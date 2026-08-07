// frontend/src/app/features/reporte-diario-costos-postura/funciones/expandir-filas-alimento.funcion.ts
import { fechaCortaSinTz } from '../../../shared/utils/format';
import {
  FilaAlimentoView,
  ReporteDiarioCostosPosturaFila
} from '../models/reporte-diario-costos-postura.model';

/**
 * Expande cada día a UNA FILA POR ÍTEM de alimento (decisión D4 del 07-ago-2026).
 *
 * El diseño tiene una sola celda «tipo alimento | cantidad» por sexo, pero un día puede traer
 * varios alimentos del mismo sexo (caso real de S-369B el 23-feb-2026: PREPICO 567,966 kg +
 * PREPOSTURA 546,134 kg para hembras). Cada ítem ocupa su propia fila para poder costear por
 * referencia; hembras y machos se aparean por posición dentro del día y el sobrante queda con
 * la otra columna vacía.
 *
 * Función PURA: sin `this`, sin DI, sin estado.
 */
export function expandirFilasAlimento(
  filas: readonly ReporteDiarioCostosPosturaFila[]
): FilaAlimentoView[] {
  const salida: FilaAlimentoView[] = [];

  for (const f of filas) {
    const hembras = f.alimentos.filter(a => a.sexo === 'H');
    const machos = f.alimentos.filter(a => a.sexo === 'M');
    const n = Math.max(hembras.length, machos.length);

    // Un día sin ningún ítem igual aparece: el reporte es diario y una fila ausente se lee
    // como «no hubo registro», que es distinto de «no consumió».
    if (n === 0) {
      salida.push(base(f, null, null, null, null));
      continue;
    }

    for (let i = 0; i < n; i++) {
      const h = hembras[i];
      const m = machos[i];
      salida.push(base(
        f,
        h ? h.nombre : null,
        h ? h.cantidadKg : null,
        m ? m.nombre : null,
        m ? m.cantidadKg : null
      ));
    }
  }

  return salida;
}

function base(
  f: ReporteDiarioCostosPosturaFila,
  hembraNombre: string | null,
  hembraKg: number | null,
  machoNombre: string | null,
  machoKg: number | null
): FilaAlimentoView {
  return {
    fecha: f.fecha,
    fechaFmt: fechaCortaSinTz(f.fecha),
    fase: f.fase,
    loteGalpon: f.loteGalpon,
    hembraNombre,
    hembraKg,
    machoNombre,
    machoKg
  };
}

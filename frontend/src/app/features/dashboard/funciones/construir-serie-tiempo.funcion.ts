// features/dashboard/funciones/construir-serie-tiempo.funcion.ts
//
// Función PURA: series temporales → `ChartData<'line'>` de Chart.js.

import { ChartConfiguration, ChartData } from 'chart.js';
import { etiquetaDiaMes } from '../../../shared/utils/format';
import { PuntoSerie, SerieTiempo } from '../models/dashboard-metricas.model';
import { COLOR_POR_ROL, COLORES_MARCA } from './paleta-graficas.funcion';

/**
 * Tope de días que el eje genera solo. Con más, se cae a las fechas presentes.
 *
 * Es una válvula, no una regla de negocio: una fecha mal cargada (`0202-03-01`) generaría dos
 * millones de puntos y colgaría la pestaña. Diez años cubre cualquier período real del dashboard.
 */
const MAX_DIAS_EJE = 3660;

/** `YYYY-MM-DD` → milisegundos UTC, o `null` si no tiene esa forma. */
function aMsUtc(ymd: string): number | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(ymd);
  if (!m) return null;
  const ms = Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  return Number.isFinite(ms) ? ms : null;
}

/** Milisegundos UTC → `YYYY-MM-DD`. */
function aYmd(ms: number): string {
  return new Date(ms).toISOString().slice(0, 10);
}

/**
 * Todos los días entre dos fechas, inclusive. Se cuenta en UTC a propósito: sumar 24 h sobre una
 * fecha local salta o repite un día en los cambios de horario de verano.
 */
export function rangoDiario(desde: string, hasta: string): string[] {
  const ini = aMsUtc(desde);
  const fin = aMsUtc(hasta);
  if (ini === null || fin === null || fin < ini) return [];

  const DIA = 86_400_000;
  const dias = Math.round((fin - ini) / DIA) + 1;
  if (dias > MAX_DIAS_EJE) return [];

  return Array.from({ length: dias }, (_, i) => aYmd(ini + i * DIA));
}

/**
 * Eje X del gráfico: **todos los días** entre la primera y la última fecha con dato.
 *
 * 🔴 No es la unión de las fechas presentes, y la diferencia es el punto de esta función. Si el
 * 02/03 no se cargó, con la unión el eje sería `[01/03, 03/03]`: el día faltante **desaparece** y la
 * línea une los dos extremos como si fueran contiguos — la tendencia inventada que este archivo
 * existe para impedir. Con el rango completo, el 02/03 está en el eje y su valor es `null` ⇒ hueco.
 */
export function ejeDeFechas(series: readonly SerieTiempo[]): string[] {
  const fechas = new Set<string>();
  for (const serie of series ?? []) {
    for (const punto of serie?.puntos ?? []) {
      const f = punto?.fecha?.trim();
      if (f) fechas.add(f);
    }
  }
  if (fechas.size === 0) return [];

  // Orden lexicográfico = orden cronológico para `YYYY-MM-DD`.
  const presentes = [...fechas].sort();
  const completo = rangoDiario(presentes[0], presentes[presentes.length - 1]);

  // Si el rango no se pudo generar (fecha con otro formato, o rango absurdo), al menos se dibujan
  // las fechas que sí hay: mejor un eje incompleto que una pestaña colgada.
  return completo.length ? completo : presentes;
}

/** Índice fecha → valor de una serie. La última repetición gana (no se suman duplicados). */
function indexarPuntos(puntos: readonly PuntoSerie[]): Map<string, number | null> {
  const mapa = new Map<string, number | null>();
  for (const punto of puntos ?? []) {
    const f = punto?.fecha?.trim();
    if (!f) continue;
    const v = punto.valor;
    mapa.set(f, typeof v === 'number' && Number.isFinite(v) ? v : null);
  }
  return mapa;
}

/**
 * Arma el `ChartData` de una gráfica de líneas.
 *
 * ## La regla que importa: los huecos quedan huecos
 *
 * Una fecha sin punto en una serie sale como **`null`**, no como `0`, y el dataset va con
 * `spanGaps: false` para que Chart.js dibuje el corte. Un día sin registro cargado y un día con
 * mortalidad cero son hechos distintos: rellenar con `0` inventa un dato que nadie midió, y unir
 * los extremos con una recta inventa la tendencia del medio.
 *
 * @param series  Las series a dibujar. Cada una trae su rol semántico, que decide el color.
 * @param fechasForzadas  Eje X explícito (p. ej. todos los días del período aunque falten datos).
 *                        Si se omite, el eje es la unión de las fechas presentes.
 */
export function construirSerieTiempo(
  series: readonly SerieTiempo[],
  fechasForzadas?: readonly string[]
): ChartData<'line', (number | null)[], string> {
  const limpias = (series ?? []).filter(s => !!s);
  const eje = fechasForzadas?.length ? [...fechasForzadas].sort() : ejeDeFechas(limpias);

  return {
    labels: eje.map(etiquetaDiaMes),
    datasets: limpias.map(serie => {
      const porFecha = indexarPuntos(serie.puntos);
      const color = COLOR_POR_ROL[serie.rol] ?? COLORES_MARCA.neutro;
      const esReferencia = serie.rol === 'referencia';

      return {
        label: serie.etiqueta,
        // El hueco es un `null`, no un cero. Ver el bloque de arriba.
        data: eje.map(f => (porFecha.has(f) ? porFecha.get(f)! : null)),
        borderColor: color,
        backgroundColor: color,
        pointBackgroundColor: color,
        pointBorderColor: '#FFFFFF',
        borderWidth: 2,
        pointRadius: 3,
        pointHoverRadius: 5,
        tension: 0.3,
        fill: false,
        // La guía/meta va punteada: se lee como referencia y no compite con el dato real.
        borderDash: esReferencia ? [6, 4] : undefined,
        spanGaps: false
      };
    })
  };
}

/**
 * Opciones base de una gráfica de líneas del dashboard.
 *
 * Se devuelve un objeto NUEVO en cada llamada a propósito: Chart.js muta el objeto de opciones
 * (guarda escalas y estado interno), así que compartir una constante entre dos gráficas las acopla.
 * El componente lo asigna UNA vez a un campo, nunca desde un getter del template.
 */
export function opcionesLinea(tituloEjeY?: string): ChartConfiguration<'line'>['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: {
        position: 'top',
        labels: { usePointStyle: true, padding: 16, color: COLORES_MARCA.texto }
      },
      tooltip: {
        backgroundColor: 'rgba(28, 25, 23, 0.92)',
        borderColor: COLORES_MARCA.naranja,
        borderWidth: 1,
        cornerRadius: 6
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        title: tituloEjeY ? { display: true, text: tituloEjeY, color: COLORES_MARCA.neutro } : undefined,
        grid: { color: 'rgba(107, 114, 128, 0.12)' },
        ticks: { color: COLORES_MARCA.neutro }
      },
      x: {
        grid: { display: false },
        ticks: { color: COLORES_MARCA.neutro, maxRotation: 0, autoSkip: true }
      }
    }
  };
}

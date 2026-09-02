// features/dashboard/funciones/construir-distribucion.funcion.ts
//
// Función PURA: categorías → `ChartData` de dona o de barras.

import { ChartConfiguration, ChartData } from 'chart.js';
import { ItemDistribucion } from '../models/dashboard-metricas.model';
import { COLORES_MARCA, coloresCategorias } from './paleta-graficas.funcion';

/** Cuántas categorías se dibujan antes de agrupar el resto en «Otros». */
const MAX_CATEGORIAS = 8;

/**
 * Ordena de mayor a menor y agrupa la cola en «Otros».
 *
 * Sin esto, una torta de 40 granjas es ilegible y sus etiquetas tapan el gráfico. Se agrupa —no se
 * descarta— porque el total tiene que seguir siendo el total: una torta a la que le faltan porciones
 * miente sobre las proporciones de las que quedan.
 */
export function agruparCola(
  items: readonly ItemDistribucion[],
  maximo: number = MAX_CATEGORIAS
): ItemDistribucion[] {
  const validos = (items ?? [])
    .filter(i => !!i && Number.isFinite(i.valor))
    .map(i => ({ etiqueta: i.etiqueta?.trim() || '(sin nombre)', valor: i.valor }));

  if (maximo <= 0 || validos.length <= maximo) {
    return validos.sort((a, b) => b.valor - a.valor);
  }

  const ordenados = [...validos].sort((a, b) => b.valor - a.valor);
  const cabeza = ordenados.slice(0, maximo - 1);
  const cola = ordenados.slice(maximo - 1);
  const suma = cola.reduce((acc, i) => acc + i.valor, 0);

  return [...cabeza, { etiqueta: `Otros (${cola.length})`, valor: suma }];
}

/** `ChartData` de una dona. */
export function construirDistribucion(
  items: readonly ItemDistribucion[],
  etiquetaSerie = ''
): ChartData<'doughnut', number[], string> {
  const datos = agruparCola(items);

  return {
    labels: datos.map(d => d.etiqueta),
    datasets: [
      {
        label: etiquetaSerie,
        data: datos.map(d => d.valor),
        backgroundColor: coloresCategorias(datos.length),
        borderColor: '#FFFFFF',
        borderWidth: 2
      }
    ]
  };
}

/** `ChartData` de barras categóricas (una barra por categoría). */
export function construirBarras(
  items: readonly ItemDistribucion[],
  etiquetaSerie = ''
): ChartData<'bar', number[], string> {
  const datos = agruparCola(items);

  return {
    labels: datos.map(d => d.etiqueta),
    datasets: [
      {
        label: etiquetaSerie,
        data: datos.map(d => d.valor),
        backgroundColor: coloresCategorias(datos.length),
        borderWidth: 0,
        borderRadius: 4
      }
    ]
  };
}

/** Opciones base de una dona. Objeto nuevo por llamada (Chart.js muta las opciones). */
export function opcionesDona(): ChartConfiguration<'doughnut'>['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '58%',
    plugins: {
      legend: {
        position: 'right',
        labels: { usePointStyle: true, padding: 14, color: COLORES_MARCA.texto, boxWidth: 10 }
      },
      tooltip: {
        backgroundColor: 'rgba(28, 25, 23, 0.92)',
        borderColor: COLORES_MARCA.naranja,
        borderWidth: 1,
        cornerRadius: 6
      }
    }
  };
}

/** Opciones base de barras categóricas. Objeto nuevo por llamada. */
export function opcionesBarras(tituloEjeY?: string): ChartConfiguration<'bar'>['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
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
      x: { grid: { display: false }, ticks: { color: COLORES_MARCA.neutro } }
    }
  };
}

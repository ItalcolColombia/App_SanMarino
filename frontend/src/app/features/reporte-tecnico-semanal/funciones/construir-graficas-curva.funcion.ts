// Función PURA: gráficas de la CURVA CONSOLIDADA del año (bloque «Resumen a
// semana N» de las hojas Gráf LEV Hembras / Gráf LEV Machos / Gráf Producción).
//
// Eje X = EDAD en semanas, no fechas: es lo que permite comparar lotes
// encasetados en meses distintos contra la misma guía.
//
// Misma convención de series que el resto del módulo: Real sólido, Guía
// punteada [6,4], tension 0.3, spanGaps, pointRadius 2; hembras en naranja
// Italfoods y machos en azul.
import { ChartConfiguration, ChartData } from 'chart.js';
import { CurvaConsolidadaPunto, EtapaResumen } from '../models/resumen-semanal-ra-pesadas.model';

export interface GraficaCurva {
  titulo: string;
  data: ChartData<'line'>;
  options: ChartConfiguration<'line'>['options'];
}

const H_REAL = '#F5821F';
const H_GUIA = '#FBB040';
const M_REAL = '#1976d2';
const M_GUIA = '#64a8dc';

function linea(label: string, data: (number | null)[], color: string, guia = false) {
  return {
    label,
    data,
    borderColor: color,
    backgroundColor: 'transparent',
    borderDash: guia ? [6, 4] : undefined,
    tension: 0.3,
    spanGaps: true,
    pointRadius: 2
  };
}

function opciones(tituloEjeY: string): ChartConfiguration<'line'>['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: { position: 'bottom', labels: { boxWidth: 18, font: { size: 10 } } }
    },
    scales: {
      x: { title: { display: true, text: 'Edad (semanas)', font: { size: 10 } } },
      y: { title: { display: true, text: tituloEjeY, font: { size: 10 } } }
    }
  };
}

const val = (p: CurvaConsolidadaPunto, k: string): number | null => p.indicadores?.[k] ?? null;

export function construirGraficasCurva(
  puntos: CurvaConsolidadaPunto[],
  etapa: EtapaResumen
): GraficaCurva[] {
  if (!puntos || puntos.length === 0) return [];
  const edades = puntos.map(p => String(p.edadSemana));

  if (etapa === 'levante') {
    return [
      {
        titulo: 'Desviación de peso vs guía (%)',
        data: { labels: edades, datasets: [
          linea('Hembras', puntos.map(p => val(p, 'difPesoHembrasPct')), H_REAL),
          linea('Machos', puntos.map(p => val(p, 'difPesoMachosPct')), M_REAL)
        ] },
        options: opciones('% desviación')
      },
      {
        titulo: 'Uniformidad (%)',
        data: { labels: edades, datasets: [
          linea('Hembras', puntos.map(p => val(p, 'uniformidadHembras')), H_REAL),
          linea('Machos', puntos.map(p => val(p, 'uniformidadMachos')), M_REAL)
        ] },
        options: opciones('% uniformidad')
      },
      {
        titulo: 'Retiro acumulado vs guía — Hembras (%)',
        data: { labels: edades, datasets: [
          linea('Real', puntos.map(p => val(p, 'retiroAcumHembrasPct')), H_REAL),
          linea('Guía', puntos.map(p => val(p, 'retiroAcumHembrasGuia')), H_GUIA, true)
        ] },
        options: opciones('% retiro acumulado')
      },
      {
        titulo: 'Retiro acumulado vs guía — Machos (%)',
        data: { labels: edades, datasets: [
          linea('Real', puntos.map(p => val(p, 'retiroAcumMachosPct')), M_REAL),
          linea('Guía', puntos.map(p => val(p, 'retiroAcumMachosGuia')), M_GUIA, true)
        ] },
        options: opciones('% retiro acumulado')
      },
      {
        titulo: 'Desviación de consumo vs guía (%)',
        data: { labels: edades, datasets: [
          linea('Hembras', puntos.map(p => val(p, 'difConsumoHembrasPct')), H_REAL),
          linea('Machos', puntos.map(p => val(p, 'difConsumoMachosPct')), M_REAL)
        ] },
        options: opciones('% desviación')
      },
      {
        titulo: 'Aves vivas por edad',
        data: { labels: edades, datasets: [
          linea('Hembras', puntos.map(p => p.saldoHembras), H_REAL),
          linea('Machos', puntos.map(p => p.saldoMachos), M_REAL)
        ] },
        options: opciones('aves')
      }
    ];
  }

  return [
    {
      titulo: '% Producción vs guía',
      data: { labels: edades, datasets: [
        linea('Real', puntos.map(p => val(p, 'produccionPct')), H_REAL),
        linea('Guía', puntos.map(p => val(p, 'produccionPctGuia')), H_GUIA, true)
      ] },
      options: opciones('% producción')
    },
    {
      titulo: 'Huevo total por ave alojada vs guía',
      data: { labels: edades, datasets: [
        linea('H.T.A.A', puntos.map(p => val(p, 'htaa')), H_REAL),
        linea('Guía', puntos.map(p => val(p, 'htaaGuia')), H_GUIA, true)
      ] },
      options: opciones('huevos/ave')
    },
    {
      titulo: 'Huevo incubable por ave alojada vs guía',
      data: { labels: edades, datasets: [
        linea('H.I.A.A', puntos.map(p => val(p, 'hiaa')), H_REAL),
        linea('Guía', puntos.map(p => val(p, 'hiaaGuia')), H_GUIA, true)
      ] },
      options: opciones('huevos/ave')
    },
    {
      titulo: '% Aprovechamiento vs guía',
      data: { labels: edades, datasets: [
        linea('Real', puntos.map(p => val(p, 'aprovSemPct')), H_REAL),
        linea('Guía', puntos.map(p => val(p, 'aprovSemPctGuia')), H_GUIA, true)
      ] },
      options: opciones('% aprovechamiento')
    },
    {
      titulo: 'Retiro acumulado vs guía',
      data: { labels: edades, datasets: [
        linea('Hembras', puntos.map(p => val(p, 'retiroAcumHembrasPct')), H_REAL),
        linea('Hembras guía', puntos.map(p => val(p, 'retiroAcumHembrasGuia')), H_GUIA, true),
        linea('Machos', puntos.map(p => val(p, 'retiroAcumMachosPct')), M_REAL),
        linea('Machos guía', puntos.map(p => val(p, 'retiroAcumMachosGuia')), M_GUIA, true)
      ] },
      options: opciones('% retiro acumulado')
    },
    {
      titulo: 'Alimento por huevo incubable (gr)',
      data: { labels: edades, datasets: [
        linea('gr/H.Inc', puntos.map(p => val(p, 'grHuevoInc')), H_REAL)
      ] },
      options: opciones('gramos')
    }
  ];
}

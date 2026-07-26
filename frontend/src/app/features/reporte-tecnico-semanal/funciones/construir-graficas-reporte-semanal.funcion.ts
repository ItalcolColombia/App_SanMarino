// Función PURA: arma las definiciones Chart.js de la vista "Gráficas" del
// Reporte Técnico Semanal, replicando las gráficas embebidas de los Excel
// oficiales (A382 levante: 8 por hoja · A346 producción: 6 por hoja).
// Convención de series del repo (graficas-principal): Real sólido, Guía
// punteada [6,4], tension 0.3, spanGaps, pointRadius 2.
import { ChartConfiguration, ChartData } from 'chart.js';
import {
  ReporteSemanalLevanteTab,
  ReporteSemanalProduccionTab
} from '../models/reporte-tecnico-semanal.model';

export interface GraficaReporteSemanal {
  titulo: string;
  tipo: 'line' | 'bar';
  data: ChartData<'line' | 'bar'>;
  options: ChartConfiguration['options'];
}

// Paleta: Hembras = naranja Italfoods (real) / dorado (guía); Machos = azul (real) / azul claro (guía).
const H_REAL = '#F5821F';
const H_GUIA = '#FBB040';
const M_REAL = '#1976d2';
const M_GUIA = '#64a8dc';

type Punto = number | null;

function linea(label: string, data: Punto[], color: string, guia = false) {
  return {
    type: 'line' as const,
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

function barra(label: string, data: Punto[], color: string, guia = false) {
  return {
    type: 'bar' as const,
    label,
    data,
    backgroundColor: guia ? `${color}66` : color,
    borderColor: color,
    borderWidth: guia ? 1 : 0
  };
}

function opciones(tituloEjeY: string): ChartConfiguration['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: { position: 'bottom', labels: { boxWidth: 18, font: { size: 10 } } },
      tooltip: { enabled: true }
    },
    scales: {
      x: { title: { display: true, text: 'Semana de edad' }, ticks: { font: { size: 10 } } },
      y: { title: { display: true, text: tituloEjeY }, ticks: { font: { size: 10 } } }
    }
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// LEVANTE — 8 gráficas por hoja (como el Excel A382).
// ─────────────────────────────────────────────────────────────────────────────
export function construirGraficasLevante(tab: ReporteSemanalLevanteTab): GraficaReporteSemanal[] {
  const semanas = tab.semanas;
  if (semanas.length === 0) return [];
  const labels = semanas.map(s => `${s.semana}`);
  const v = <K>(f: (s: (typeof semanas)[number]) => K): K[] => semanas.map(f);

  return [
    {
      titulo: 'Peso Levante',
      tipo: 'line',
      data: {
        labels,
        datasets: [
          linea('Peso H Guía', v(s => s.pesoHembrasGuia), H_GUIA, true),
          linea('Peso H Lote', v(s => s.pesoHembras), H_REAL),
          linea('Peso M Guía', v(s => s.pesoMachosGuia), M_GUIA, true),
          linea('Peso M Lote', v(s => s.pesoMachos), M_REAL)
        ]
      },
      options: opciones('Gramos')
    },
    {
      titulo: 'Desviación Peso %',
      tipo: 'bar',
      data: {
        labels,
        datasets: [
          barra('Desviación Hembras', v(s => s.desviacionPesoHembrasPct), H_REAL),
          barra('Desviación Machos', v(s => s.desviacionPesoMachosPct), M_REAL)
        ]
      },
      options: opciones('% desviación vs guía')
    },
    {
      titulo: 'Consumo Levante (gr/ave/día)',
      tipo: 'line',
      data: {
        labels,
        datasets: [
          linea('Consumo H Guía', v(s => s.grAveDiaHembrasGuia), H_GUIA, true),
          linea('Consumo H Lote', v(s => s.grAveDiaHembras), H_REAL),
          linea('Consumo M Guía', v(s => s.grAveDiaMachosGuia), M_GUIA, true),
          linea('Consumo M Lote', v(s => s.grAveDiaMachos), M_REAL)
        ]
      },
      options: opciones('gr/ave/día')
    },
    {
      titulo: 'Incremento consumo Hembras',
      tipo: 'line',
      data: {
        labels,
        datasets: [
          linea('Incremento H Guía', v(s => s.incrementoGrAveDiaHembrasGuia), H_GUIA, true),
          linea('Incremento H Lote', v(s => s.incrementoGrAveDiaHembras), H_REAL)
        ]
      },
      options: opciones('gr/ave/día')
    },
    {
      titulo: 'Incremento consumo Machos',
      tipo: 'line',
      data: {
        labels,
        datasets: [
          linea('Incremento M Guía', v(s => s.incrementoGrAveDiaMachosGuia), M_GUIA, true),
          linea('Incremento M Lote', v(s => s.incrementoGrAveDiaMachos), M_REAL)
        ]
      },
      options: opciones('gr/ave/día')
    },
    {
      titulo: 'Retiro Total y Mortalidad Hembras',
      tipo: 'bar',
      data: {
        labels,
        datasets: [
          barra('Mortalidad sem. Lote', v(s => s.mortalidadHembrasPct), H_REAL),
          barra('Mortal+Descart. sem. Guía', v(s => s.mortSelHembrasGuiaPct), H_GUIA, true),
          linea('Retiro Total Lote', v(s => s.retiroAcumHembrasPct), '#374151'),
          linea('Retiro Total Guía', v(s => s.retiroAcumHembrasGuiaPct), '#9ca3af', true)
        ]
      },
      options: opciones('%')
    },
    {
      titulo: 'Retiro Total y Mortalidad Machos',
      tipo: 'bar',
      data: {
        labels,
        datasets: [
          barra('Mortalidad sem. Lote', v(s => s.mortalidadMachosPct), M_REAL),
          barra('Mortal+Descart. sem. Guía', v(s => s.mortSelMachosGuiaPct), M_GUIA, true),
          linea('Retiro Total Lote', v(s => s.retiroAcumMachosPct), '#374151'),
          linea('Retiro Total Guía', v(s => s.retiroAcumMachosGuiaPct), '#9ca3af', true)
        ]
      },
      options: opciones('%')
    },
    {
      titulo: 'Uniformidad %',
      tipo: 'line',
      data: {
        labels,
        datasets: [
          linea('Uniform. Guía', v(s => s.uniformidadGuia), H_GUIA, true),
          linea('Uniform. H Lote', v(s => s.uniformidadHembras), H_REAL)
        ]
      },
      options: opciones('%')
    }
  ];
}

// ─────────────────────────────────────────────────────────────────────────────
// PRODUCCIÓN — 6 gráficas por hoja (como el Excel A346).
// ─────────────────────────────────────────────────────────────────────────────
export function construirGraficasProduccion(tab: ReporteSemanalProduccionTab): GraficaReporteSemanal[] {
  const semanas = tab.semanas;
  if (semanas.length === 0) return [];
  const labels = semanas.map(s => `${s.semana}`);
  const v = <K>(f: (s: (typeof semanas)[number]) => K): K[] => semanas.map(f);

  return [
    {
      titulo: 'Producción %',
      tipo: 'line',
      data: {
        labels,
        datasets: [
          linea('% Producción Guía', v(s => s.porcentajeProduccionGuia), H_GUIA, true),
          linea('% Producción Lote', v(s => s.porcentajeProduccion), H_REAL)
        ]
      },
      options: opciones('% aves/día')
    },
    {
      titulo: 'H.T.A.A — H.I.A.A — % H.I',
      tipo: 'line',
      data: {
        labels,
        datasets: [
          linea('H.T.A.A Guía', v(s => s.htaaGuia), H_GUIA, true),
          linea('H.T.A.A Lote', v(s => s.htaa), H_REAL),
          linea('H.I.A.A Guía', v(s => s.hiaaGuia), M_GUIA, true),
          linea('H.I.A.A Lote', v(s => s.hiaa), M_REAL),
          linea('% H.I Guía', v(s => s.porcentajeIncubablesGuia), '#9ca3af', true),
          linea('% H.I Lote', v(s => s.porcentajeIncubables), '#374151')
        ]
      },
      options: opciones('Huevos/ave alojada · %')
    },
    {
      titulo: 'Consumo ave (gr/ave/día)',
      tipo: 'line',
      data: {
        labels,
        datasets: [
          linea('Consumo H Guía', v(s => s.grAveDiaHembrasGuia), H_GUIA, true),
          linea('Consumo H Lote', v(s => s.grAveDiaHembras), H_REAL),
          linea('Consumo M Guía', v(s => s.grAveDiaMachosGuia), M_GUIA, true),
          linea('Consumo M Lote', v(s => s.grAveDiaMachos), M_REAL)
        ]
      },
      options: opciones('gr/ave/día')
    },
    {
      titulo: 'Desviación Peso %',
      tipo: 'bar',
      data: {
        labels,
        datasets: [
          barra('Desviación Hembras', v(s => s.desviacionPesoHembrasPct), H_REAL),
          barra('Desviación Machos', v(s => s.desviacionPesoMachosPct), M_REAL)
        ]
      },
      options: opciones('% desviación vs guía')
    },
    {
      titulo: 'Retiro Total y Mortalidad Hembras',
      tipo: 'bar',
      data: {
        labels,
        datasets: [
          barra('Mortalidad sem. Lote', v(s => s.mortalidadHembrasPct), H_REAL),
          barra('Mortalidad sem. Guía', v(s => s.mortalidadHembrasGuiaPct), H_GUIA, true),
          linea('% M+D Acum Lote', v(s => s.mortSelHembrasAcumPct), '#374151'),
          linea('Retiro Acum Guía', v(s => s.retiroAcumHembrasGuiaPct), '#9ca3af', true)
        ]
      },
      options: opciones('%')
    },
    {
      titulo: 'Retiro Total Machos',
      tipo: 'line',
      data: {
        labels,
        datasets: [
          linea('% M+D Acum Lote', v(s => s.mortSelMachosAcumPct), M_REAL),
          linea('Retiro Acum Guía', v(s => s.retiroAcumMachosGuiaPct), M_GUIA, true),
          linea('Mortalidad sem. Lote', v(s => s.mortalidadMachosPct), '#374151'),
          linea('Mortalidad sem. Guía', v(s => s.mortalidadMachosGuiaPct), '#9ca3af', true)
        ]
      },
      options: opciones('%')
    }
  ];
}

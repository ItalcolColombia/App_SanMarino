/**
 * Construcción pura de los `ChartData` de la liquidación técnica de levante.
 *
 * Extraído de `liquidacion-tecnica.component.ts` SIN cambiar comportamiento: mismos datasets,
 * colores, conversiones (/1000) y aritmética de distribución de retiros. Funciones puras: reciben
 * la liquidación y devuelven el objeto de datos del gráfico (sin `this`/DI/estado de Angular).
 */
import { ChartData } from 'chart.js';
import { LiquidacionTecnicaDto, LiquidacionTecnicaCompletaDto } from '../services/liquidacion-tecnica.service';

/** Gráfico de barras: Indicadores Real vs Guía. */
export function construirIndicadoresChartData(liquidacion: LiquidacionTecnicaDto | null): ChartData<'bar'> {
  if (!liquidacion) {
    return { labels: [], datasets: [] };
  }

  const indicadores = [
    { label: 'Mort. H (%)', real: liquidacion.porcentajeMortalidadHembras, guia: null },
    { label: 'Mort. M (%)', real: liquidacion.porcentajeMortalidadMachos, guia: null },
    { label: 'Retiro H (%)', real: liquidacion.porcentajeRetiroTotalHembras, guia: liquidacion.porcentajeRetiroGuia },
    { label: 'Retiro M (%)', real: liquidacion.porcentajeRetiroTotalMachos, guia: liquidacion.porcentajeRetiroGuia },
    { label: 'Consumo (g)', real: liquidacion.consumoAlimentoRealGramos / 1000, guia: liquidacion.consumoAlimentoGuiaGramos ? liquidacion.consumoAlimentoGuiaGramos / 1000 : null },
    { label: 'Peso H (g)', real: liquidacion.pesoSemana25RealHembras ? liquidacion.pesoSemana25RealHembras / 1000 : null, guia: liquidacion.pesoSemana25GuiaHembras ? liquidacion.pesoSemana25GuiaHembras / 1000 : null },
    { label: 'Unif. H (%)', real: liquidacion.uniformidadRealHembras, guia: liquidacion.uniformidadGuiaHembras }
  ].filter(ind => ind.real != null);

  return {
    labels: indicadores.map(ind => ind.label),
    datasets: [
      {
        label: 'Real',
        data: indicadores.map(ind => ind.real || 0),
        backgroundColor: 'rgba(211, 47, 47, 0.8)',
        borderColor: 'rgba(211, 47, 47, 1)',
        borderWidth: 1
      },
      {
        label: 'Guía',
        data: indicadores.map(ind => ind.guia || 0),
        backgroundColor: 'rgba(120, 113, 108, 0.8)',
        borderColor: 'rgba(120, 113, 108, 1)',
        borderWidth: 1
      }
    ]
  };
}

/** Gráfico de torta: Distribución de retiros. */
export function construirRetirosChartData(liquidacion: LiquidacionTecnicaDto | null): ChartData<'pie'> {
  if (!liquidacion) {
    return { labels: [], datasets: [] };
  }

  const totalHembras = liquidacion.hembrasEncasetadas;
  const totalMachos = liquidacion.machosEncasetados;
  const total = totalHembras + totalMachos;

  const mortHembras = (liquidacion.porcentajeMortalidadHembras / 100) * totalHembras;
  const mortMachos = (liquidacion.porcentajeMortalidadMachos / 100) * totalMachos;
  const selHembras = (liquidacion.porcentajeSeleccionHembras / 100) * totalHembras;
  const selMachos = (liquidacion.porcentajeSeleccionMachos / 100) * totalMachos;
  const errHembras = (liquidacion.porcentajeErrorSexajeHembras / 100) * totalHembras;
  const errMachos = (liquidacion.porcentajeErrorSexajeMachos / 100) * totalMachos;
  const vivas = total - (mortHembras + mortMachos + selHembras + selMachos + errHembras + errMachos);

  return {
    labels: ['Vivas', 'Mort. Hembras', 'Mort. Machos', 'Sel. Hembras', 'Sel. Machos', 'Error Sexaje'],
    datasets: [{
      data: [vivas, mortHembras, mortMachos, selHembras, selMachos, errHembras + errMachos],
      backgroundColor: [
        'rgba(16, 185, 129, 0.8)', // Verde - Vivas
        'rgba(239, 68, 68, 0.8)',  // Rojo - Mort Hembras
        'rgba(185, 28, 28, 0.8)',  // Rojo oscuro - Mort Machos
        'rgba(245, 158, 11, 0.8)', // Amarillo - Sel Hembras
        'rgba(217, 119, 6, 0.8)',  // Amarillo oscuro - Sel Machos
        'rgba(107, 114, 128, 0.8)' // Gris - Error Sexaje
      ],
      borderColor: [
        'rgba(16, 185, 129, 1)',
        'rgba(239, 68, 68, 1)',
        'rgba(185, 28, 28, 1)',
        'rgba(245, 158, 11, 1)',
        'rgba(217, 119, 6, 1)',
        'rgba(107, 114, 128, 1)'
      ],
      borderWidth: 2
    }]
  };
}

/** Gráfico de líneas: Evolución semanal (mortalidad y selección por sexo). */
export function construirEvolucionChartData(liquidacionCompleta: LiquidacionTecnicaCompletaDto | null): ChartData<'line'> {
  if (!liquidacionCompleta?.detallesSeguimiento?.length) {
    return { labels: [], datasets: [] };
  }

  const seguimientos = liquidacionCompleta.detallesSeguimiento
    .sort((a, b) => a.semana - b.semana);

  return {
    labels: seguimientos.map(s => `Sem ${s.semana}`),
    datasets: [
      {
        label: 'Mortalidad Hembras',
        data: seguimientos.map(s => s.mortalidadHembras || 0),
        borderColor: 'rgba(239, 68, 68, 1)',
        backgroundColor: 'rgba(239, 68, 68, 0.1)',
        tension: 0.4,
        fill: false
      },
      {
        label: 'Mortalidad Machos',
        data: seguimientos.map(s => s.mortalidadMachos || 0),
        borderColor: 'rgba(185, 28, 28, 1)',
        backgroundColor: 'rgba(185, 28, 28, 0.1)',
        tension: 0.4,
        fill: false
      },
      {
        label: 'Selección Hembras',
        data: seguimientos.map(s => s.seleccionHembras || 0),
        borderColor: 'rgba(245, 158, 11, 1)',
        backgroundColor: 'rgba(245, 158, 11, 0.1)',
        tension: 0.4,
        fill: false
      },
      {
        label: 'Selección Machos',
        data: seguimientos.map(s => s.seleccionMachos || 0),
        borderColor: 'rgba(217, 119, 6, 1)',
        backgroundColor: 'rgba(217, 119, 6, 0.1)',
        tension: 0.4,
        fill: false
      }
    ]
  };
}

/** Gráfico de líneas: Consumo y Peso (múltiples ejes Y). */
export function construirConsumoPesoChartData(liquidacionCompleta: LiquidacionTecnicaCompletaDto | null): ChartData<'line'> {
  if (!liquidacionCompleta?.detallesSeguimiento?.length) {
    return { labels: [], datasets: [] };
  }

  const seguimientos = liquidacionCompleta.detallesSeguimiento
    .sort((a, b) => a.semana - b.semana);

  return {
    labels: seguimientos.map(s => `Sem ${s.semana}`),
    datasets: [
      {
        label: 'Consumo Alimento (kg)',
        data: seguimientos.map(s => s.consumoAlimento || 0),
        borderColor: 'rgba(59, 130, 246, 1)',
        backgroundColor: 'rgba(59, 130, 246, 0.1)',
        tension: 0.4,
        fill: false,
        yAxisID: 'y'
      },
      {
        label: 'Peso Hembras (g)',
        data: seguimientos.map(s => (s.pesoPromedioHembras || 0) / 1000), // Convertir a kg para mejor escala
        borderColor: 'rgba(16, 185, 129, 1)',
        backgroundColor: 'rgba(16, 185, 129, 0.1)',
        tension: 0.4,
        fill: false,
        yAxisID: 'y1'
      },
      {
        label: 'Uniformidad Hembras (%)',
        data: seguimientos.map(s => s.uniformidadHembras || 0),
        borderColor: 'rgba(168, 162, 158, 1)',
        backgroundColor: 'rgba(168, 162, 158, 0.1)',
        tension: 0.4,
        fill: false,
        yAxisID: 'y2'
      }
    ]
  };
}

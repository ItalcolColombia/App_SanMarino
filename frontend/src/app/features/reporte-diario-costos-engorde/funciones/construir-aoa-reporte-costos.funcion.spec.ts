import { construirAoaReporteCostos } from './construir-aoa-reporte-costos.funcion';
import {
  ReporteDiarioCostosDesgloseSexo,
  ReporteDiarioCostosReporte
} from '../models/reporte-diario-costos.model';

/** Desglose por sexo en cero (lo que manda una empresa mixta o una fila sin datos). */
const SIN_SEXO: ReporteDiarioCostosDesgloseSexo = {
  mortalidadHembras: 0, mortalidadMachos: 0,
  seleccionHembras: 0, seleccionMachos: 0,
  mortSelHembras: 0, mortSelMachos: 0
};

/**
 * Kilometro 22 / lote base 2604, 28-ago (cifras reales de la fn v4):
 *   Galpon-1: mort H 23 · M 26                     → 49
 *   Galpon-2: mort H 4 · M 10, sel H 10 · M 15     → 39
 */
function reporte(mortalidadPorSexo: boolean | undefined): ReporteDiarioCostosReporte {
  return {
    filtrosAplicados: { granjaId: 38 },
    fechaInicioEfectiva: '2026-08-28',
    fechaFinEfectiva: '2026-08-28',
    granjaId: 38,
    granjaNombre: 'Kilometro 22',
    loteBaseEngordeId: 11,
    loteBaseNombre: '2604',
    lotes: [],
    galpones: [
      { galponId: 'G0035', galponNombre: 'Galpon-1', lotes: ['2604 - 2'] },
      { galponId: 'G0036', galponNombre: 'Galpon-2', lotes: ['2604'] }
    ],
    avesVivasActuales: [
      { galponId: 'G0035', galponNombre: 'Galpon-1', avesVivas: 47714 },
      { galponId: 'G0036', galponNombre: 'Galpon-2', avesVivas: 47299 }
    ],
    avesVivasActualesTotal: 95013,
    filas: [{
      fecha: '2026-08-28',
      consumoTotalKg: 3100,
      mortSelTotal: 88,
      avesVivasTotal: 95013,
      alimentos: [{ nombreAlimento: 'AV. POLLITO PREINICIADOR', stockKg: 12240, consumoKg: 3100 }],
      galpones: [
        {
          galponId: 'G0035', galponNombre: 'Galpon-1', mortalidad: 49, seleccion: 0, errSexaje: 0,
          mortSel: 49, consumoKg: 700, avesVivas: 47714,
          ...SIN_SEXO, mortalidadHembras: 23, mortalidadMachos: 26, mortSelHembras: 23, mortSelMachos: 26
        },
        {
          galponId: 'G0036', galponNombre: 'Galpon-2', mortalidad: 14, seleccion: 25, errSexaje: 0,
          mortSel: 39, consumoKg: 2400, avesVivas: 47299,
          mortalidadHembras: 4, mortalidadMachos: 10, seleccionHembras: 10, seleccionMachos: 15,
          mortSelHembras: 14, mortSelMachos: 25
        }
      ]
    }],
    totales: {
      consumoTotalKg: 3100,
      mortSelTotal: 88,
      alimentos: [{ nombreAlimento: 'AV. POLLITO PREINICIADOR', consumoKg: 3100 }],
      porGalpon: [
        {
          galponId: 'G0035', galponNombre: 'Galpon-1', mortalidad: 49, seleccion: 0, errSexaje: 0, mortSel: 49,
          ...SIN_SEXO, mortalidadHembras: 23, mortalidadMachos: 26, mortSelHembras: 23, mortSelMachos: 26
        },
        {
          galponId: 'G0036', galponNombre: 'Galpon-2', mortalidad: 14, seleccion: 25, errSexaje: 0, mortSel: 39,
          mortalidadHembras: 4, mortalidadMachos: 10, seleccionHembras: 10, seleccionMachos: 15,
          mortSelHembras: 14, mortSelMachos: 25
        }
      ]
    },
    mortalidadPorSexo
  };
}

/** Fila del AOA cuya primera celda es `primera`. */
function filaQueEmpiezaCon(aoa: (string | number)[][], primera: string): (string | number)[] {
  const fila = aoa.find(r => r[0] === primera);
  if (!fila) throw new Error(`No hay fila que empiece con "${primera}"`);
  return fila;
}

describe('construirAoaReporteCostos', () => {
  it('sin desglose por sexo (empresa mixta) el archivo es el de siempre: una columna por galpón', () => {
    for (const flag of [false, undefined]) {
      const { aoa, colWidths } = construirAoaReporteCostos(reporte(flag));

      // FECHA | 3 alimento | TOTAL DÍA | 2 mort | 2 aves | TOTAL AVES
      expect(filaQueEmpiezaCon(aoa, 'FECHA').length).toBe(10);
      expect(aoa.some(r => r.includes('H'))).toBeFalse();
      expect(colWidths.length).toBe(10);

      const dia = aoa.find(r => r[1] === 'AV. POLLITO PREINICIADOR' && r[0] !== '')!;
      expect(dia.slice(5, 7)).toEqual([49, 39]);
      expect(dia.slice(7)).toEqual([47714, 47299, 95013]);

      expect(filaQueEmpiezaCon(aoa, 'SUMA TOTAL').slice(5)).toEqual([49, 39, 47714, 47299, 95013]);
    }
  });

  it('con desglose por sexo cada galpón ocupa H | M | Total y H + M es el total de siempre', () => {
    const { aoa, colWidths } = construirAoaReporteCostos(reporte(true));

    const n1 = filaQueEmpiezaCon(aoa, 'FECHA');
    expect(n1.length).toBe(14);   // 2 galpones × 3 en mortalidad
    expect(n1.slice(5, 11)).toEqual(Array(6).fill('MORTALIDAD + SELECCIÓN'));
    expect(colWidths.length).toBe(14);

    const idxN1 = aoa.indexOf(n1);
    expect(aoa[idxN1 + 1].slice(5, 11)).toEqual(['Galpon-1', 'Galpon-1', 'Galpon-1', 'Galpon-2', 'Galpon-2', 'Galpon-2']);
    expect(aoa[idxN1 + 2].slice(5, 11)).toEqual(['H', 'M', 'Total', 'H', 'M', 'Total']);

    const dia = aoa.find(r => r[1] === 'AV. POLLITO PREINICIADOR' && r[0] !== '')!;
    expect(dia.length).toBe(14);
    expect(dia.slice(5, 11)).toEqual([23, 26, 49, 14, 25, 39]);
    expect(dia.slice(11)).toEqual([47714, 47299, 95013]);   // aves vivas no se tocan

    expect(filaQueEmpiezaCon(aoa, 'SUMA TOTAL').slice(5)).toEqual([23, 26, 49, 14, 25, 39, 47714, 47299, 95013]);
  });

  it('las filas de alimento que siguen a la primera del día dejan vacías las 3 celdas de cada galpón', () => {
    const rep = reporte(true);
    rep.filas[0].alimentos.push({ nombreAlimento: 'AV. SUPER POLLITO INICIACION', stockKg: 17370, consumoKg: 0 });

    const { aoa } = construirAoaReporteCostos(rep);

    const segunda = aoa.find(r => r[1] === 'AV. SUPER POLLITO INICIACION')!;
    expect(segunda.length).toBe(14);
    expect(segunda.slice(5, 11)).toEqual(['', '', '', '', '', '']);
  });
});

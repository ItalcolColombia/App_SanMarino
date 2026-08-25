// frontend/src/app/features/reporte-diario-costos-postura/funciones/construir-aoa-costos-postura.funcion.spec.ts
import { construirHojasCostosPostura } from './construir-aoa-costos-postura.funcion';
import {
  ReporteDiarioCostosPosturaFila,
  ReporteDiarioCostosPosturaReporte
} from '../models/reporte-diario-costos-postura.model';

function filaHuevo(overrides: Partial<ReporteDiarioCostosPosturaFila> = {}): ReporteDiarioCostosPosturaFila {
  return {
    fecha: '2026-08-22',
    fase: 'Produccion',
    loteId: 1,
    loteNombre: 'SMOKE-SR-001',
    galponId: 'G0497',
    galponNombre: 'Galpón 1',
    loteGalpon: 'SMOKE-SR-001 : Galpón 1',
    nucleoId: '910001',
    granjaId: 109,
    granjaNombre: 'La Esperanza',
    regional: '',
    lotePosturaBaseId: null,
    loteBaseNombre: '',
    edadDias: 229,
    semana: 33,
    mortalidadH: 0, mortalidadM: 0,
    seleccionH: 0, seleccionM: 0,
    errorSexajeH: 0, errorSexajeM: 0,
    ventaAvesH: 0, ventaAvesM: 0,
    consumoKgH: 0, consumoKgM: 0,
    alimentos: [],
    huevo: { fertil: 0, comercial: 0, inservible: 0, total: 1290, venta: 0, trasladoPlanta: 0, particionCuadra: false },
    diaEnAmbasEtapas: false,
    excluidoDelTotal: false,
    ...overrides
  };
}

function reporteConHuevo(filas: ReporteDiarioCostosPosturaFila[]): ReporteDiarioCostosPosturaReporte {
  return {
    filtrosAplicados: {},
    fechaDesdeEfectiva: null,
    fechaHastaEfectiva: null,
    fases: ['Produccion'],
    lotes: [],
    filas,
    totales: {
      aves: {
        mortalidadH: 0, mortalidadM: 0, seleccionH: 0, seleccionM: 0,
        errorSexajeH: 0, errorSexajeM: 0, ventaAvesH: 0, ventaAvesM: 0,
        totalH: 0, totalM: 0, total: 0
      },
      consumoKgH: 0, consumoKgM: 0, consumoKgTotal: 0,
      alimentos: [],
      huevo: { fertil: 0, comercial: 0, inservible: 0, total: 1290, venta: 0, trasladoPlanta: 0, particionCuadra: false }
    },
    ubicaciones: null,
    diasDuplicados: 0,
    totalesExcluidos: null,
    alcanceExpandidoPorLoteBase: false
  };
}

describe('construirHojasCostosPostura — hoja Huevos', () => {
  it('sin flag (comportamiento previo): incluye fértil/comercial/inservible en cabecera y datos', () => {
    const rep = reporteConHuevo([filaHuevo()]);
    const hojas = construirHojasCostosPostura(rep, false, false);
    const huevos = hojas.find(h => h.sheetName === 'Huevos')!;
    const cabecera = huevos.aoa.find(f => Array.isArray(f) && f.includes('Fecha')) as unknown[];
    expect(cabecera).toEqual(jasmine.arrayContaining(['Huevo fértil', 'Huevo comercial', 'Huevo inservible']));
  });

  it('con clasificacion_huevo_por_items: NO incluye fértil/comercial/inservible, pero sí Huevo Total', () => {
    const rep = reporteConHuevo([filaHuevo()]);
    const hojas = construirHojasCostosPostura(rep, false, true);
    const huevos = hojas.find(h => h.sheetName === 'Huevos')!;
    const filaCabecera = huevos.aoa.find(f => Array.isArray(f) && f.includes('Fecha')) as unknown[];
    expect(filaCabecera).not.toContain('Huevo fértil');
    expect(filaCabecera).not.toContain('Huevo comercial');
    expect(filaCabecera).not.toContain('Huevo inservible');
    expect(filaCabecera).toContain('Huevo Total');
  });

  it('con el flag ON, cabecera y cada fila de datos tienen la MISMA cantidad de columnas (no se desalinean)', () => {
    const rep = reporteConHuevo([filaHuevo(), filaHuevo({ fecha: '2026-08-23' })]);
    const hojas = construirHojasCostosPostura(rep, false, true);
    const huevos = hojas.find(h => h.sheetName === 'Huevos')!;
    const filaCabecera = huevos.aoa.find(f => Array.isArray(f) && f.includes('Fecha')) as unknown[];
    const filasDatos = huevos.aoa.filter(f => Array.isArray(f) && f.length > 0 && f[0] === 'SMOKE-SR-001') as unknown[][];
    for (const fila of filasDatos) {
      expect(fila.length).toBe(filaCabecera.length);
    }
  });

  it('sin filas de producción, no agrega la hoja Huevos (con o sin flag)', () => {
    const rep = reporteConHuevo([]);
    expect(construirHojasCostosPostura(rep, false, false).find(h => h.sheetName === 'Huevos')).toBeUndefined();
    expect(construirHojasCostosPostura(rep, false, true).find(h => h.sheetName === 'Huevos')).toBeUndefined();
  });
});

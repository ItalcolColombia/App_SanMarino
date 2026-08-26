// frontend/src/app/features/reporte-tecnico-semanal/funciones/construir-aoa-reporte-semanal.funcion.spec.ts
import { construirHojasProduccion } from './construir-aoa-reporte-semanal.funcion';
import {
  ReporteSemanalTabHeader,
  ReporteTecnicoSemanalProduccionResponse
} from '../models/reporte-tecnico-semanal.model';

function header(nombre: string): ReporteSemanalTabHeader {
  return {
    loteId: 1,
    lotePosturaProduccionId: 10,
    loteNombre: nombre,
    esConsolidado: nombre === 'Consolidado',
    granjaId: 109,
    granjaNombre: 'La Esperanza',
    municipio: null,
    nucleoId: '910001',
    nucleoNombre: 'Núcleo 1',
    galponId: 'G0497',
    galponNombre: 'Galpón 1',
    tecnico: null,
    raza: 'Babcock Brown',
    anioGuia: 2026,
    fechaEncaset: '2026-01-05',
    fechaInicioProduccion: '2026-05-01',
    baseHembras: 10000,
    baseMachos: 0,
    pesoInicialHembras: null,
    mortCajasHembras: null,
    mortCajasMachos: null
  };
}

function respuesta(): ReporteTecnicoSemanalProduccionResponse {
  return {
    lotePosturaBaseId: 1,
    loteBaseNombre: 'SMOKE-SR-001',
    raza: 'Babcock Brown',
    anioGuia: 2026,
    tieneGuia: true,
    consolidado: { header: header('Consolidado'), semanas: [] },
    tabs: [{ header: header('SMOKE-SR-001'), semanas: [] }]
  };
}

describe('construirHojasProduccion — hojas CLAS Huevo', () => {
  it('sin flag (comportamiento previo): agrega las hojas CLAS Gral y CLAS <lote>', () => {
    const hojas = construirHojasProduccion(respuesta(), false);
    expect(hojas.some(h => h.sheetName === 'CLAS Gral')).toBeTrue();
    expect(hojas.some(h => h.sheetName.startsWith('CLAS'))).toBeTrue();
  });

  it('con clasificacion_huevo_por_items: NO agrega ninguna hoja CLAS *', () => {
    const hojas = construirHojasProduccion(respuesta(), true);
    expect(hojas.some(h => h.sheetName.startsWith('CLAS'))).toBeFalse();
  });

  it('con el flag ON, las hojas de semanas (Gral + por lote) se siguen generando igual', () => {
    const sinFlag = construirHojasProduccion(respuesta(), false).filter(h => !h.sheetName.startsWith('CLAS'));
    const conFlag = construirHojasProduccion(respuesta(), true);
    expect(conFlag.map(h => h.sheetName)).toEqual(sinFlag.map(h => h.sheetName));
  });
});

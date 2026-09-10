// src/app/features/lote/funciones/lote-list-encasetamiento.funcion.spec.ts
import {
  filtrarLotesPorEstadoCierre,
  filtrarLoteBasePorEstadoCierre,
  mostrarLoteBasePorDefecto
} from './lote-list-encasetamiento.funcion';
import { LoteDto } from '../services/lote.service';
import { LotePosturaBaseDto } from '../services/lote-postura-base.service';

function lote(id: number, cerradoCompleto: boolean | undefined | null): LoteDto {
  return { loteId: id, loteNombre: `L${id}`, granjaId: 1, cerradoCompleto } as LoteDto;
}

function base(id: number, totalLotes: number, tieneLoteAbierto: boolean): LotePosturaBaseDto {
  return {
    lotePosturaBaseId: id,
    loteNombre: `B${id}`,
    codigoErp: null,
    descripcionErp: null,
    cantidadHembras: 0,
    cantidadMachos: 0,
    cantidadMixtas: 0,
    raza: null,
    tipoLinea: null,
    fechaEncaset: null,
    companyId: 1,
    companyNombre: null,
    createdByUserId: 1,
    paisId: null,
    paisNombre: null,
    farmId: null,
    farmNombre: null,
    erpCreate: null,
    createdAt: '2026-01-01',
    totalLotes,
    tieneLoteAbierto
  };
}

describe('filtrarLotesPorEstadoCierre', () => {
  const lotes = [lote(1, true), lote(2, false), lote(3, undefined)];

  it('"abiertos" (default) esconde solo los que cerraron por completo', () => {
    const res = filtrarLotesPorEstadoCierre(lotes, 'abiertos');
    expect(res.map(l => l.loteId)).toEqual([2, 3]);
  });

  it('"cerrados" trae solo los que cerraron por completo', () => {
    const res = filtrarLotesPorEstadoCierre(lotes, 'cerrados');
    expect(res.map(l => l.loteId)).toEqual([1]);
  });

  it('"todos" no filtra nada', () => {
    expect(filtrarLotesPorEstadoCierre(lotes, 'todos')).toEqual(lotes);
  });

  it('un lote sin la señal calculada (cerradoCompleto ausente) se trata como abierto', () => {
    // Más seguro mostrar de más que esconder un lote real por un dato faltante.
    const res = filtrarLotesPorEstadoCierre([lote(9, undefined)], 'abiertos');
    expect(res.length).toBe(1);
  });
});

describe('mostrarLoteBasePorDefecto', () => {
  it('una base sin lotes asignados se muestra ("sin asignar", no puede estar cerrada)', () => {
    expect(mostrarLoteBasePorDefecto(0, false)).toBe(true);
  });

  it('una base con al menos un lote abierto se muestra', () => {
    expect(mostrarLoteBasePorDefecto(3, true)).toBe(true);
  });

  it('una base con lotes pero TODOS cerrados no se muestra', () => {
    expect(mostrarLoteBasePorDefecto(3, false)).toBe(false);
  });
});

describe('filtrarLoteBasePorEstadoCierre', () => {
  const bases = [
    base(1, 0, false), // sin asignar -> abierta
    base(2, 2, true),  // tiene un lote abierto -> abierta
    base(3, 2, false)  // todos sus lotes cerrados -> cerrada
  ];

  it('"abiertos" (default) esconde solo las bases 100% cerradas', () => {
    const res = filtrarLoteBasePorEstadoCierre(bases, 'abiertos');
    expect(res.map(b => b.lotePosturaBaseId)).toEqual([1, 2]);
  });

  it('"cerrados" trae solo las bases 100% cerradas', () => {
    const res = filtrarLoteBasePorEstadoCierre(bases, 'cerrados');
    expect(res.map(b => b.lotePosturaBaseId)).toEqual([3]);
  });

  it('"todos" no filtra nada', () => {
    expect(filtrarLoteBasePorEstadoCierre(bases, 'todos')).toEqual(bases);
  });
});

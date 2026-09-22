import { estadoDesdeFilas, resumenAtiende, FilaResolutorVista } from './estado-resolutores.funcion';

describe('estadoDesdeFilas', () => {
  const tipos = ['SOPORTE', 'DESARROLLO', 'REQUERIMIENTO', 'DUDAS'];

  it('sin filas deja todo apagado y en EMPRESA', () => {
    const e = estadoDesdeFilas(tipos, []);
    expect(tipos.every(t => e.activo[t] === false)).toBeTrue();
    expect(tipos.every(t => e.alcance[t] === 'EMPRESA')).toBeTrue();
    expect(tipos.every(t => e.pais[t] === null)).toBeTrue();
  });

  it('null o undefined no revientan', () => {
    expect(estadoDesdeFilas(tipos, null).activo['SOPORTE']).toBeFalse();
    expect(estadoDesdeFilas(tipos, undefined).alcance['DUDAS']).toBe('EMPRESA');
  });

  it('una fila activa prende su tipo y conserva el pais', () => {
    const e = estadoDesdeFilas(tipos, [{ tipo: 'SOPORTE', paisId: 1, activo: true, alcance: 'EMPRESA' }]);
    expect(e.activo['SOPORTE']).toBeTrue();
    expect(e.pais['SOPORTE']).toBe(1);
    expect(e.activo['DUDAS']).toBeFalse();
  });

  it('una fila inactiva no prende nada', () => {
    const e = estadoDesdeFilas(tipos, [{ tipo: 'SOPORTE', paisId: null, activo: false, alcance: 'GLOBAL' }]);
    expect(e.activo['SOPORTE']).toBeFalse();
    expect(e.alcance['SOPORTE']).toBe('EMPRESA');
  });

  it('GLOBAL manda sobre la fila de empresa del mismo tipo, en cualquier orden', () => {
    const filas: FilaResolutorVista[] = [
      { tipo: 'DESARROLLO', paisId: null, activo: true, alcance: 'EMPRESA' },
      { tipo: 'DESARROLLO', paisId: null, activo: true, alcance: 'GLOBAL' },
    ];
    expect(estadoDesdeFilas(tipos, filas).alcance['DESARROLLO']).toBe('GLOBAL');
    expect(estadoDesdeFilas(tipos, [...filas].reverse()).alcance['DESARROLLO']).toBe('GLOBAL');
  });

  it('un alcance ausente o desconocido cuenta como EMPRESA', () => {
    const e = estadoDesdeFilas(tipos, [
      { tipo: 'DUDAS', paisId: null, activo: true },
      { tipo: 'SOPORTE', paisId: null, activo: true, alcance: 'OTRO' as never },
    ]);
    expect(e.alcance['DUDAS']).toBe('EMPRESA');
    expect(e.alcance['SOPORTE']).toBe('EMPRESA');
  });
});

describe('resumenAtiende', () => {
  const label = (t: string) => ({ SOPORTE: 'Soporte', DESARROLLO: 'Desarrollo' } as Record<string, string>)[t] ?? t;

  it('etiqueta cada tipo con la empresa o con todas las empresas', () => {
    const chips = resumenAtiende([
      { tipo: 'SOPORTE', paisId: null, activo: true, alcance: 'EMPRESA' },
      { tipo: 'DESARROLLO', paisId: null, activo: true, alcance: 'GLOBAL' },
    ], 'Agroavicola Sanmarino', label);

    expect(chips).toEqual([
      { tipo: 'SOPORTE', label: 'Soporte', etiqueta: 'Agroavicola Sanmarino' },
      { tipo: 'DESARROLLO', label: 'Desarrollo', etiqueta: 'todas las empresas' },
    ]);
  });

  it('sin nombre de empresa usa un texto neutro y omite lo inactivo y lo repetido', () => {
    const chips = resumenAtiende([
      { tipo: 'SOPORTE', paisId: null, activo: false, alcance: 'EMPRESA' },
      { tipo: 'DUDAS', paisId: null, activo: true },
      { tipo: 'DUDAS', paisId: 1, activo: true },
    ], null, label);

    expect(chips.length).toBe(1);
    expect(chips[0]).toEqual({ tipo: 'DUDAS', label: 'DUDAS', etiqueta: 'esta empresa' });
  });
});

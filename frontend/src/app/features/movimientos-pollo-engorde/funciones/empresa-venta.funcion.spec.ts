// src/app/features/movimientos-pollo-engorde/funciones/empresa-venta.funcion.spec.ts
import {
  SIN_EMPRESA_VENTA,
  VARIAS_EMPRESAS_VENTA,
  claveEmpresaVenta,
  coincideEmpresaVenta,
  resumenEmpresaVenta,
  unirOpcionesEmpresaVenta
} from './empresa-venta.funcion';

describe('claveEmpresaVenta', () => {
  it('recorta y pasa a minúsculas; null y undefined quedan vacíos', () => {
    expect(claveEmpresaVenta('  PlAnTa ')).toBe('planta');
    expect(claveEmpresaVenta(null)).toBe('');
    expect(claveEmpresaVenta(undefined)).toBe('');
  });
});

describe('unirOpcionesEmpresaVenta', () => {
  it('conserva el orden de la lista maestra y agrega después lo que traigan las otras fuentes', () => {
    const r = unirOpcionesEmpresaVenta(['Planta', 'Cliente A'], ['Cliente B'], ['Planta Vieja']);
    expect(r).toEqual(['Planta', 'Cliente A', 'Cliente B', 'Planta Vieja']);
  });

  it('no repite: ignora mayúsculas y espacios, y se queda con la primera aparición', () => {
    const r = unirOpcionesEmpresaVenta(['Planta'], ['  planta ', 'PLANTA', 'Cliente']);
    expect(r).toEqual(['Planta', 'Cliente']);
  });

  it('descarta vacíos, blancos, null y undefined', () => {
    const r = unirOpcionesEmpresaVenta(['', '   ', null, 'Planta', undefined]);
    expect(r).toEqual(['Planta']);
  });

  it('devuelve el texto recortado', () => {
    expect(unirOpcionesEmpresaVenta(['  Planta Norte  '])).toEqual(['Planta Norte']);
  });

  it('sin fuentes, o con fuentes vacías, devuelve una lista vacía (fail-closed: el campo no se dibuja)', () => {
    expect(unirOpcionesEmpresaVenta()).toEqual([]);
    expect(unirOpcionesEmpresaVenta([], [])).toEqual([]);
  });

  it('una empresa guardada en una venta vieja que ya no está en la lista se conserva como opción', () => {
    // Caso real: la lista se editó (el id de cada opción cambia, el texto no) y se quitó «Cliente Viejo».
    const r = unirOpcionesEmpresaVenta(['Planta'], ['Cliente Viejo']);
    expect(r).toContain('Cliente Viejo');
  });
});

describe('coincideEmpresaVenta', () => {
  it('sin filtro entran todas, con o sin empresa y de cualquier tipo', () => {
    expect(coincideEmpresaVenta('Venta', 'Planta', '')).toBeTrue();
    expect(coincideEmpresaVenta('Venta', null, '')).toBeTrue();
    expect(coincideEmpresaVenta('Traslado', null, null)).toBeTrue();
    expect(coincideEmpresaVenta('Venta', 'Planta', undefined)).toBeTrue();
  });

  it('con una empresa entran solo las ventas de esa empresa (exacta, sin distinguir mayúsculas ni espacios)', () => {
    expect(coincideEmpresaVenta('Venta', 'Planta', 'Planta')).toBeTrue();
    expect(coincideEmpresaVenta('Venta', ' planta ', 'PLANTA')).toBeTrue();
    expect(coincideEmpresaVenta('Venta', 'Planta Norte', 'Planta')).toBeFalse();
    expect(coincideEmpresaVenta('Venta', 'Cliente A', 'Planta')).toBeFalse();
  });

  it('las filas sin empresa no entran cuando se filtra por una empresa concreta', () => {
    expect(coincideEmpresaVenta('Venta', null, 'Planta')).toBeFalse();
    expect(coincideEmpresaVenta('Venta', '  ', 'Planta')).toBeFalse();
    expect(coincideEmpresaVenta('Traslado', null, 'Planta')).toBeFalse();
  });

  it('«Sin empresa» entran solo las VENTAS sin empresa (un traslado no es una venta sin empresa)', () => {
    expect(coincideEmpresaVenta('Venta', null, SIN_EMPRESA_VENTA)).toBeTrue();
    expect(coincideEmpresaVenta('Venta', '', SIN_EMPRESA_VENTA)).toBeTrue();
    expect(coincideEmpresaVenta('Venta', 'Planta', SIN_EMPRESA_VENTA)).toBeFalse();
    expect(coincideEmpresaVenta('Traslado', null, SIN_EMPRESA_VENTA)).toBeFalse();
    expect(coincideEmpresaVenta('Ajuste', null, SIN_EMPRESA_VENTA)).toBeFalse();
  });
});

describe('resumenEmpresaVenta', () => {
  it('todas las líneas con la misma empresa → ese texto', () => {
    expect(resumenEmpresaVenta([{ plantaDestino: 'Planta' }, { plantaDestino: ' planta ' }])).toBe('Planta');
    expect(resumenEmpresaVenta([{ plantaDestino: 'Planta' }])).toBe('Planta');
  });

  it('empresas distintas → «Varias»', () => {
    expect(resumenEmpresaVenta([{ plantaDestino: 'Planta' }, { plantaDestino: 'Cliente A' }])).toBe(VARIAS_EMPRESAS_VENTA);
  });

  it('unas líneas con empresa y otras sin ella → «Varias» (no se esconde la diferencia)', () => {
    expect(resumenEmpresaVenta([{ plantaDestino: 'Planta' }, { plantaDestino: null }])).toBe(VARIAS_EMPRESAS_VENTA);
    expect(resumenEmpresaVenta([{ plantaDestino: 'Planta' }, {}])).toBe(VARIAS_EMPRESAS_VENTA);
  });

  it('ninguna línea con empresa, o sin líneas → null', () => {
    expect(resumenEmpresaVenta([{ plantaDestino: null }, { plantaDestino: '' }, {}])).toBeNull();
    expect(resumenEmpresaVenta([])).toBeNull();
  });
});

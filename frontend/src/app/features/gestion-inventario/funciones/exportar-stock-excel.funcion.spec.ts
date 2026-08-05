import { InventarioGestionStockDto } from '../services/gestion-inventario.service';
import { cabecerasStockExcel, construirFilasStockExcel } from './exportar-stock-excel.funcion';

/** Índices de columna con ubicación (alimento). Sin ubicación se corren 2 posiciones. */
const COL = {
  granja: 0,
  nucleo: 1,
  galpon: 2,
  codigo: 3,
  producto: 4,
  tipo: 5,
  fecha: 6,
  cantidad: 7,
  unidad: 8
} as const;

function stock(over: Partial<InventarioGestionStockDto> = {}): InventarioGestionStockDto {
  return {
    id: 1,
    farmId: 10,
    nucleoId: null,
    galponId: null,
    itemInventarioEcuadorId: 100,
    itemCodigo: 'AV0374',
    itemNombre: 'AV. AMINAPOT 720 1LT 0%',
    itemType: 'Otros insumos',
    quantity: 20,
    unit: 'kg',
    granjaNombre: 'BODEGA PRINCIAL KM 86',
    nucleoNombre: null,
    galponNombre: null,
    fechaIngreso: '2026-06-30T12:00:00Z',
    ...over
  };
}

describe('construirFilasStockExcel', () => {
  it('trae núcleo y galpón cuando la fila es de alimento', () => {
    const fila = construirFilasStockExcel(
      [
        stock({
          itemType: 'Alimento',
          nucleoId: 'N1',
          galponId: 'G3',
          nucleoNombre: 'NÚCLEO 1',
          galponNombre: 'GALPÓN 3'
        })
      ],
      { incluirUbicacion: true }
    )[0];

    expect(fila[COL.granja]).toBe('BODEGA PRINCIAL KM 86');
    expect(fila[COL.nucleo]).toBe('NÚCLEO 1');
    expect(fila[COL.galpon]).toBe('GALPÓN 3');
    expect(fila[COL.tipo]).toBe('Alimento');
  });

  it('deja la ubicación en «—» cuando el ítem no es alimento (stock a nivel granja)', () => {
    const fila = construirFilasStockExcel([stock()], { incluirUbicacion: true })[0];

    expect(fila[COL.nucleo]).toBe('—');
    expect(fila[COL.galpon]).toBe('—');
    // El resto de la fila no cambia por no tener ubicación.
    expect(fila[COL.codigo]).toBe('AV0374');
    expect(fila[COL.producto]).toBe('AV. AMINAPOT 720 1LT 0%');
    expect(fila[COL.unidad]).toBe('kg');
  });

  it('cae al farmId cuando la granja no trae nombre', () => {
    const fila = construirFilasStockExcel([stock({ granjaNombre: undefined, farmId: 42 })], {
      incluirUbicacion: true
    })[0];

    expect(fila[COL.granja]).toBe('42');
  });

  it('muestra el id de núcleo/galpón cuando falta el nombre (mismo fallback que la grilla)', () => {
    const fila = construirFilasStockExcel(
      [stock({ nucleoId: 'N7', galponId: 'G9', nucleoNombre: null, galponNombre: null })],
      { incluirUbicacion: true }
    )[0];

    expect(fila[COL.nucleo]).toBe('N7');
    expect(fila[COL.galpon]).toBe('G9');
  });

  it('omite las columnas de ubicación cuando no aplican (Colombia: inventario a nivel granja)', () => {
    const fila = construirFilasStockExcel([stock({ nucleoId: 'N1', nucleoNombre: 'NÚCLEO 1' })], {
      incluirUbicacion: false
    })[0];

    expect(fila.length).toBe(7);
    expect(fila[0]).toBe('BODEGA PRINCIAL KM 86');
    expect(fila[1]).toBe('AV0374'); // Código pasa a ser la 2ª columna
    expect(fila).not.toContain('NÚCLEO 1');
  });

  it('no corre la fecha de ingreso al día anterior aunque venga anclada en UTC', () => {
    const fila = construirFilasStockExcel([stock({ fechaIngreso: '2026-06-30T00:00:00Z' })], {
      incluirUbicacion: true
    })[0];

    // Un `new Date(iso).toLocaleDateString()` en zonas negativas devolvería el 29.
    expect(String(fila[COL.fecha]).startsWith('30/')).toBeTrue();
  });

  it('escribe «—» cuando el registro no tiene fecha de ingreso', () => {
    const fila = construirFilasStockExcel([stock({ fechaIngreso: null })], { incluirUbicacion: true })[0];

    expect(fila[COL.fecha]).toBe('—');
  });

  it('exporta la cantidad como número para que el Excel pueda sumarla', () => {
    const fila = construirFilasStockExcel([stock({ quantity: 1234.5 })], { incluirUbicacion: true })[0];

    expect(fila[COL.cantidad]).toBe(1234.5);
    expect(typeof fila[COL.cantidad]).toBe('number');
  });

  it('devuelve lista vacía si no hay stock (el componente avisa y no descarga)', () => {
    expect(construirFilasStockExcel([], { incluirUbicacion: true })).toEqual([]);
  });

  it('conserva una fila por registro y el orden que devuelve el backend', () => {
    const filas = construirFilasStockExcel(
      [
        stock({ granjaNombre: 'GRANJA A' }),
        stock({ granjaNombre: 'GRANJA B' }),
        stock({ granjaNombre: 'GRANJA C' })
      ],
      { incluirUbicacion: true }
    );

    expect(filas.map((f) => f[COL.granja])).toEqual(['GRANJA A', 'GRANJA B', 'GRANJA C']);
  });
});

describe('cabecerasStockExcel', () => {
  it('coincide en largo con las filas, con y sin ubicación', () => {
    const filaCon = construirFilasStockExcel([stock()], { incluirUbicacion: true })[0];
    const filaSin = construirFilasStockExcel([stock()], { incluirUbicacion: false })[0];

    expect(cabecerasStockExcel(true).length).toBe(filaCon.length);
    expect(cabecerasStockExcel(false).length).toBe(filaSin.length);
  });

  it('incluye Núcleo y Galpón solo cuando la ubicación aplica', () => {
    expect(cabecerasStockExcel(true)).toEqual([
      'Granja',
      'Núcleo',
      'Galpón',
      'Código',
      'Producto',
      'Tipo',
      'Fecha de ingreso',
      'Cantidad',
      'Unidad'
    ]);
    expect(cabecerasStockExcel(false)).not.toContain('Galpón');
  });
});

import { InventarioGestionStockDto } from '../services/gestion-inventario.service';
import {
  HOJA_ALIMENTO,
  HOJA_OTROS,
  cabecerasStockExcel,
  construirFilasStockExcel,
  construirHojasStockExcel,
  esFilaAlimento,
  particionarStockPorConcepto
} from './exportar-stock-excel.funcion';

/** Índices de columna con ubicación (hoja Alimento). Sin ubicación se corren 2 posiciones. */
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

const META = { filtros: ['Granjas: todas las asignadas (2)'], incluirUbicacion: true };

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

/** Fila de alimento típica: concepto Alimento + ubicación resuelta por el backend. */
function alimento(over: Partial<InventarioGestionStockDto> = {}): InventarioGestionStockDto {
  return stock({
    itemType: 'Alimento',
    itemCodigo: 'SM0178',
    itemNombre: 'AV. SUPER POLLO ENGORDE',
    nucleoId: 'N1',
    galponId: 'G2',
    nucleoNombre: 'N1',
    galponNombre: 'Galpon-2',
    quantity: 13570,
    ...over
  });
}

describe('esFilaAlimento', () => {
  it('reconoce el concepto sin importar mayúsculas (el catálogo lo tiene de las dos formas)', () => {
    expect(esFilaAlimento(stock({ itemType: 'Alimento' }))).toBeTrue();
    expect(esFilaAlimento(stock({ itemType: 'alimento' }))).toBeTrue();
    expect(esFilaAlimento(stock({ itemType: ' ALIMENTO ' }))).toBeTrue();
  });

  it('no confunde otros conceptos con alimento', () => {
    expect(esFilaAlimento(stock({ itemType: 'Medicamento' }))).toBeFalse();
    expect(esFilaAlimento(stock({ itemType: 'Otros insumos' }))).toBeFalse();
    expect(esFilaAlimento(stock({ itemType: '' }))).toBeFalse();
  });

  it('clasifica por concepto, NO por tener galpón (alimento a nivel granja sigue siendo alimento)', () => {
    const sinUbicacion = stock({ itemType: 'Alimento', nucleoId: null, galponId: null });
    expect(esFilaAlimento(sinUbicacion)).toBeTrue();
  });
});

describe('particionarStockPorConcepto', () => {
  it('separa alimento de los demás conceptos conservando el orden del backend', () => {
    const { alimento: ali, otros } = particionarStockPorConcepto([
      alimento({ itemCodigo: 'A1' }),
      stock({ itemCodigo: 'O1', itemType: 'Desinfectante' }),
      alimento({ itemCodigo: 'A2' }),
      stock({ itemCodigo: 'O2', itemType: 'Vacuna' })
    ]);

    expect(ali.map(r => r.itemCodigo)).toEqual(['A1', 'A2']);
    expect(otros.map(r => r.itemCodigo)).toEqual(['O1', 'O2']);
  });

  it('devuelve los dos grupos vacíos si no hay stock', () => {
    expect(particionarStockPorConcepto([])).toEqual({ alimento: [], otros: [] });
  });
});

describe('construirHojasStockExcel', () => {
  it('siempre entrega las dos hojas, en orden Alimento → Otros conceptos', () => {
    const hojas = construirHojasStockExcel([alimento(), stock()], META);

    expect(hojas.length).toBe(2);
    expect(hojas[0].sheetName).toBe(HOJA_ALIMENTO);
    expect(hojas[1].sheetName).toBe(HOJA_OTROS);
  });

  it('la hoja de alimento lleva Núcleo y Galpón; la de otros conceptos no', () => {
    const [hojaAli, hojaOtros] = construirHojasStockExcel([alimento(), stock()], META);

    expect(hojaAli.headers).toContain('Galpón');
    expect(hojaAli.rows[0][COL.nucleo]).toBe('N1');
    expect(hojaAli.rows[0][COL.galpon]).toBe('Galpon-2');

    expect(hojaOtros.headers).not.toContain('Núcleo');
    expect(hojaOtros.headers).not.toContain('Galpón');
    expect(hojaOtros.headers.length).toBe(7);
    expect(hojaOtros.rows[0].length).toBe(7);
  });

  it('cada fila cae en una sola hoja (sin duplicar ni perder registros)', () => {
    const filas = [alimento(), stock(), alimento(), stock({ itemType: 'Gas' }), stock({ itemType: 'alimento' })];
    const hojas = construirHojasStockExcel(filas, META);

    expect(hojas[0].rows.length).toBe(3); // 2 Alimento + 1 alimento (minúscula)
    expect(hojas[1].rows.length).toBe(2);
  });

  it('incluye la ubicación en «Otros conceptos» si algún registro la trajera (no oculta el dato)', () => {
    const raro = stock({ itemType: 'Medicamento', nucleoId: 'N9', galponId: 'G9', galponNombre: 'Galpon-9' });
    const [, hojaOtros] = construirHojasStockExcel([raro], META);

    expect(hojaOtros.headers).toContain('Galpón');
    expect(hojaOtros.rows[0][COL.galpon]).toBe('Galpon-9');
  });

  it('marca «Sin registros» en la hoja que quede vacía, sin romper la estructura del archivo', () => {
    const [hojaAli, hojaOtros] = construirHojasStockExcel([stock()], META);

    expect(hojaAli.rows).toEqual([['Sin registros para este grupo.']]);
    expect(hojaAli.subtitles).toContain('Registros: 0 · Granjas con existencias: 0');
    expect(hojaOtros.rows.length).toBe(1);
  });

  it('resume por hoja cuántos registros y granjas trae', () => {
    const hojas = construirHojasStockExcel(
      [
        alimento({ granjaNombre: 'GRANJA A' }),
        alimento({ granjaNombre: 'GRANJA B' }),
        stock({ granjaNombre: 'GRANJA A' })
      ],
      META
    );

    expect(hojas[0].subtitles).toContain('Registros: 2 · Granjas con existencias: 2');
    expect(hojas[1].subtitles).toContain('Registros: 1 · Granjas con existencias: 1');
  });

  it('repite en las dos hojas las líneas de contexto del archivo', () => {
    const hojas = construirHojasStockExcel([alimento(), stock()], META);

    expect(hojas[0].subtitles?.[0]).toBe('Granjas: todas las asignadas (2)');
    expect(hojas[1].subtitles?.[0]).toBe('Granjas: todas las asignadas (2)');
  });

  it('omite la ubicación en las dos hojas cuando no aplica (Colombia: todo a nivel granja)', () => {
    const hojas = construirHojasStockExcel([alimento(), stock()], { ...META, incluirUbicacion: false });

    expect(hojas[0].headers.length).toBe(7);
    expect(hojas[0].headers).not.toContain('Galpón');
    expect(hojas[1].headers.length).toBe(7);
  });
});

describe('construirFilasStockExcel', () => {
  it('trae núcleo y galpón cuando la fila es de alimento', () => {
    const fila = construirFilasStockExcel([alimento({ nucleoNombre: 'NÚCLEO 1', galponNombre: 'GALPÓN 3' })], {
      incluirUbicacion: true
    })[0];

    expect(fila[COL.granja]).toBe('BODEGA PRINCIAL KM 86');
    expect(fila[COL.nucleo]).toBe('NÚCLEO 1');
    expect(fila[COL.galpon]).toBe('GALPÓN 3');
    expect(fila[COL.tipo]).toBe('Alimento');
  });

  it('deja la ubicación en «—» cuando el ítem no es alimento (stock a nivel granja)', () => {
    const fila = construirFilasStockExcel([stock()], { incluirUbicacion: true })[0];

    expect(fila[COL.nucleo]).toBe('—');
    expect(fila[COL.galpon]).toBe('—');
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

  it('omite las columnas de ubicación cuando no aplican', () => {
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

// ─── Inventario por SILO (Santa Reyes) ───────────────────────────────────────
// El silo/bodega ES la ubicación: núcleo y galpón llegan en null a propósito, así que la columna
// Silo los reemplaza. Con `incluirSilo` ausente el archivo tiene que quedar idéntico al de siempre.

/** Fila de una empresa que ubica por silo: sin núcleo/galpón y con el silo resuelto. */
function porSilo(over: Partial<InventarioGestionStockDto> = {}): InventarioGestionStockDto {
  return stock({
    itemType: 'Alimento',
    nucleoId: null,
    galponId: null,
    nucleoNombre: null,
    galponNombre: null,
    siloId: 4,
    siloNombre: 'Silo 4',
    ...over
  });
}

describe('exportación con inventario por silo', () => {
  it('agrega la columna Silo en la cabecera, después de la granja', () => {
    expect(cabecerasStockExcel(false, true)).toEqual([
      'Granja',
      'Silo',
      'Código',
      'Producto',
      'Tipo',
      'Fecha de ingreso',
      'Cantidad',
      'Unidad'
    ]);
  });

  it('la cabecera y la fila siguen midiendo lo mismo', () => {
    const fila = construirFilasStockExcel([porSilo()], { incluirUbicacion: false, incluirSilo: true })[0];
    expect(cabecerasStockExcel(false, true).length).toBe(fila.length);
  });

  it('escribe el nombre del silo y cae al id solo si el nombre no vino', () => {
    const [conNombre] = construirFilasStockExcel([porSilo()], { incluirUbicacion: false, incluirSilo: true });
    expect(conNombre[1]).toBe('Silo 4');

    const [sinNombre] = construirFilasStockExcel([porSilo({ siloNombre: null })], {
      incluirUbicacion: false,
      incluirSilo: true
    });
    expect(sinNombre[1]).toBe('4');

    const [sinSilo] = construirFilasStockExcel([porSilo({ siloId: null, siloNombre: null })], {
      incluirUbicacion: false,
      incluirSilo: true
    });
    expect(sinSilo[1]).toBe('—');
  });

  it('el silo va en las DOS hojas: la bodega de insumos también es una ubicación con saldo', () => {
    const hojas = construirHojasStockExcel(
      [porSilo(), porSilo({ itemType: 'Otros insumos', siloId: 99, siloNombre: 'Bodega' })],
      { filtros: [], incluirUbicacion: false, incluirSilo: true }
    );

    const alimento = hojas.find((h) => h.sheetName === HOJA_ALIMENTO)!;
    const otros = hojas.find((h) => h.sheetName === HOJA_OTROS)!;
    expect(alimento.headers).toContain('Silo');
    expect(otros.headers).toContain('Silo');
    expect(alimento.rows[0][1]).toBe('Silo 4');
    expect(otros.rows[0][1]).toBe('Bodega');
  });

  it('sin la bandera, el archivo queda idéntico al de las empresas sin silo', () => {
    const conFlagApagado = construirFilasStockExcel([porSilo()], { incluirUbicacion: false });
    const comoSiempre = construirFilasStockExcel([porSilo()], { incluirUbicacion: false, incluirSilo: false });

    expect(conFlagApagado).toEqual(comoSiempre);
    expect(cabecerasStockExcel(false)).not.toContain('Silo');
    expect(conFlagApagado[0].length).toBe(cabecerasStockExcel(false).length);
  });
});

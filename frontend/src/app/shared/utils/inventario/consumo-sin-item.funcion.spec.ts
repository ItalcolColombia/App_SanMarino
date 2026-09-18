import {
  BloqueConsumo,
  FilaConsumo,
  MENSAJE_ITEM_REQUERIDO_EN_FILA,
  MENSAJE_ITEM_REQUERIDO_EN_PIE,
  filasConCantidadSinItem,
  filaTieneCantidadSinItem,
  mensajeCantidadSinItem
} from './consumo-sin-item.funcion';

/**
 * Contrato de la regla «cantidad > 0 ⇒ ítem elegido» de los seguimientos diarios de levante y de
 * producción. Nace del ticket de sep-2026 (producción #676): el operario tecleó 1200 sin elegir el
 * ítem, el modal descartó la fila en silencio y el registro se guardó con consumo 0.
 *
 * Los casos que más importan son los de NO marcar: fila vacía (flag `parcial`), ítem elegido, y la
 * fila heredada sin tocar (consumo escalar de la app móvil / carga masiva).
 */
describe('filaTieneCantidadSinItem', () => {
  const fila = (cantidad: unknown, catalogItemId: unknown, editadaPorElOperario = true): FilaConsumo =>
    ({ cantidad, catalogItemId, editadaPorElOperario });

  it('cantidad con ítem null / 0 / vacío / ausente: marca (el caso del ticket)', () => {
    expect(filaTieneCantidadSinItem(fila(1200, null))).toBe(true);
    expect(filaTieneCantidadSinItem(fila(1200, 0))).toBe(true);
    expect(filaTieneCantidadSinItem(fila(1200, ''))).toBe(true);
    expect(filaTieneCantidadSinItem(fila(1200, undefined))).toBe(true);
  });

  it('cantidad con ítem elegido: no marca', () => {
    expect(filaTieneCantidadSinItem(fila(1200, 45))).toBe(false);
  });

  it('un id llegado como texto también cuenta como ítem elegido', () => {
    expect(filaTieneCantidadSinItem(fila('1200', '45'))).toBe(false);
    expect(filaTieneCantidadSinItem(fila('1200', ''))).toBe(true);
  });

  it('cantidad 0, vacía o ausente: fila sin usar, no marca (flag parcial)', () => {
    expect(filaTieneCantidadSinItem(fila(0, null))).toBe(false);
    expect(filaTieneCantidadSinItem(fila(null, null))).toBe(false);
    expect(filaTieneCantidadSinItem(fila('', null))).toBe(false);
    expect(filaTieneCantidadSinItem(fila(undefined, null))).toBe(false);
  });

  it('cantidad negativa o no numérica: no marca (no es una cantidad)', () => {
    expect(filaTieneCantidadSinItem(fila(-5, null))).toBe(false);
    expect(filaTieneCantidadSinItem(fila(NaN, null))).toBe(false);
    expect(filaTieneCantidadSinItem(fila('abc', null))).toBe(false);
  });

  it('una cantidad fraccionaria también cuenta: 0.001 ya es consumo', () => {
    expect(filaTieneCantidadSinItem(fila(0.001, null))).toBe(true);
  });

  it('fila que solo se hidrató desde el registro y el operario no tocó: no marca', () => {
    // Registro con consumo escalar heredado (app móvil / carga masiva): editar la mortalidad no puede
    // exigir un ítem, porque el ítem generaría una salida de inventario que nadie pidió.
    expect(filaTieneCantidadSinItem(fila(1200, null, false))).toBe(false);
  });

  it('null y undefined no lanzan', () => {
    expect(filaTieneCantidadSinItem(null)).toBe(false);
    expect(filaTieneCantidadSinItem(undefined)).toBe(false);
  });
});

describe('filasConCantidadSinItem', () => {
  const f = (cantidad: unknown, catalogItemId: unknown, editada = true): FilaConsumo =>
    ({ cantidad, catalogItemId, editadaPorElOperario: editada });

  it('sin bloques, bloque sin filas o filas nulas: lista vacía sin lanzar', () => {
    expect(filasConCantidadSinItem(null)).toEqual([]);
    expect(filasConCantidadSinItem(undefined)).toEqual([]);
    expect(filasConCantidadSinItem([])).toEqual([]);
    expect(filasConCantidadSinItem([{ nombre: 'Hembras', filas: [] }])).toEqual([]);
    expect(filasConCantidadSinItem([{ nombre: 'Hembras', filas: [null, undefined] }])).toEqual([]);
  });

  it('todo bien: lista vacía', () => {
    const bloques: BloqueConsumo[] = [
      { nombre: 'Hembras', filas: [f(1200, 45)] },
      { nombre: 'Machos', filas: [f(0, null)] }
    ];
    expect(filasConCantidadSinItem(bloques)).toEqual([]);
  });

  it('una sola fila en el bloque: no hace falta decir cuál', () => {
    const r = filasConCantidadSinItem([{ nombre: 'Hembras', filas: [f(1200, null)] }]);
    expect(r).toEqual([{ bloque: 'Hembras', fila: 1, enBloqueDeVarias: false }]);
  });

  it('varias filas: numera desde 1 y avisa que el bloque tiene varias', () => {
    const r = filasConCantidadSinItem([{ nombre: 'Hembras', filas: [f(500, 45), f(300, null)] }]);
    expect(r).toEqual([{ bloque: 'Hembras', fila: 2, enBloqueDeVarias: true }]);
  });

  it('respeta el orden: bloque y luego fila', () => {
    const r = filasConCantidadSinItem([
      { nombre: 'Hembras', filas: [f(1, null), f(2, null)] },
      { nombre: 'Machos', filas: [f(3, null)] }
    ]);
    expect(r.map(x => `${x.bloque}#${x.fila}`)).toEqual(['Hembras#1', 'Hembras#2', 'Machos#1']);
  });

  it('las filas heredadas sin tocar se ignoran aunque tengan cantidad y no ítem', () => {
    const r = filasConCantidadSinItem([{ nombre: 'Hembras', filas: [f(1200, null, false)] }]);
    expect(r).toEqual([]);
  });
});

describe('mensajeCantidadSinItem', () => {
  it('sin filas afectadas: cadena vacía', () => {
    expect(mensajeCantidadSinItem([])).toBe('');
  });

  it('una fila en un bloque de una: nombra el bloque, sin número de fila', () => {
    const m = mensajeCantidadSinItem([{ bloque: 'Hembras', fila: 1, enBloqueDeVarias: false }]);
    expect(m).toContain('Hembras');
    expect(m).not.toContain('fila');
    expect(m).toContain('deje la cantidad en 0');
  });

  it('una fila en un bloque de varias: dice cuál', () => {
    const m = mensajeCantidadSinItem([{ bloque: 'Machos', fila: 2, enBloqueDeVarias: true }]);
    expect(m).toContain('Machos (fila 2)');
  });

  it('varias filas: lista donde está cada una, en plural', () => {
    const m = mensajeCantidadSinItem([
      { bloque: 'Hembras', fila: 1, enBloqueDeVarias: false },
      { bloque: 'Machos', fila: 2, enBloqueDeVarias: true }
    ]);
    expect(m).toContain('Hembras, Machos (fila 2)');
    expect(m).toContain('cantidades');
  });

  it('un mismo lugar repetido no se lista dos veces', () => {
    const m = mensajeCantidadSinItem([
      { bloque: 'Hembras', fila: 1, enBloqueDeVarias: false },
      { bloque: 'Hembras', fila: 1, enBloqueDeVarias: false }
    ]);
    expect(m.match(/Hembras/g)?.length).toBe(1);
  });
});

describe('textos compartidos', () => {
  it('el aviso en línea y el del pie existen y dicen qué hacer', () => {
    expect(MENSAJE_ITEM_REQUERIDO_EN_FILA.length).toBeGreaterThan(0);
    expect(MENSAJE_ITEM_REQUERIDO_EN_PIE).toContain('cantidad');
  });
});

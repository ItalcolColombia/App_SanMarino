import { ItemDistribucion } from '../models/dashboard-metricas.model';
import {
  agruparCola,
  construirBarras,
  construirDistribucion,
  opcionesBarras,
  opcionesDona
} from './construir-distribucion.funcion';
import { PALETA_CATEGORICA, colorCategoria, coloresCategorias } from './paleta-graficas.funcion';

const items = (...pares: [string, number][]): ItemDistribucion[] =>
  pares.map(([etiqueta, valor]) => ({ etiqueta, valor }));

describe('agruparCola', () => {
  it('ordena de mayor a menor', () => {
    const salida = agruparCola(items(['A', 1], ['B', 9], ['C', 5]));
    expect(salida.map(i => i.etiqueta)).toEqual(['B', 'C', 'A']);
  });

  it('con pocas categorías no agrupa nada', () => {
    const salida = agruparCola(items(['A', 1], ['B', 2]));
    expect(salida.length).toBe(2);
    expect(salida.some(i => i.etiqueta.startsWith('Otros'))).toBe(false);
  });

  it('agrupa la cola en «Otros» conservando el TOTAL', () => {
    // Que el total se conserve es lo importante: una torta a la que le faltan porciones miente
    // sobre las proporciones de las que quedan.
    const entrada = items(
      ['A', 100], ['B', 90], ['C', 80], ['D', 70],
      ['E', 60], ['F', 50], ['G', 40], ['H', 30], ['I', 20], ['J', 10]
    );
    const totalEntrada = entrada.reduce((a, i) => a + i.valor, 0);

    const salida = agruparCola(entrada, 8);

    expect(salida.length).toBe(8);
    expect(salida[7].etiqueta).toBe('Otros (3)');
    expect(salida.reduce((a, i) => a + i.valor, 0)).toBe(totalEntrada);
  });

  it('descarta valores no numéricos en vez de propagarlos al gráfico', () => {
    const salida = agruparCola([
      { etiqueta: 'A', valor: 5 },
      { etiqueta: 'B', valor: NaN },
      { etiqueta: 'C', valor: Infinity },
      null as unknown as ItemDistribucion
    ]);

    expect(salida).toEqual([{ etiqueta: 'A', valor: 5 }]);
  });

  it('una etiqueta vacía se nombra, no queda en blanco', () => {
    const salida = agruparCola([{ etiqueta: '   ', valor: 3 }]);
    expect(salida[0].etiqueta).toBe('(sin nombre)');
  });

  it('lista vacía o nula devuelve lista vacía', () => {
    expect(agruparCola([])).toEqual([]);
    expect(agruparCola(null as unknown as ItemDistribucion[])).toEqual([]);
  });
});

describe('construirDistribucion', () => {
  it('mapea etiquetas y valores en el mismo orden', () => {
    const chart = construirDistribucion(items(['Granja A', 10], ['Granja B', 30]));

    expect(chart.labels).toEqual(['Granja B', 'Granja A']);
    expect(chart.datasets[0].data).toEqual([30, 10]);
  });

  it('pinta tantos colores como porciones', () => {
    const chart = construirDistribucion(items(['A', 1], ['B', 2], ['C', 3]));
    expect((chart.datasets[0].backgroundColor as string[]).length).toBe(3);
  });

  it('sin datos devuelve un gráfico vacío', () => {
    const chart = construirDistribucion([]);
    expect(chart.labels).toEqual([]);
    expect(chart.datasets[0].data).toEqual([]);
  });
});

describe('construirBarras', () => {
  it('usa el mismo agrupado y orden que la dona', () => {
    const barras = construirBarras(items(['A', 1], ['B', 5]));
    const dona = construirDistribucion(items(['A', 1], ['B', 5]));
    expect(barras.labels).toEqual(dona.labels);
    expect(barras.datasets[0].data).toEqual(dona.datasets[0].data);
  });
});

describe('paleta categórica', () => {
  it('cicla cuando hay más categorías que colores, en vez de inventar uno', () => {
    // Un color aleatorio cambiaría entre recargas para el mismo dato.
    const n = PALETA_CATEGORICA.length;
    expect(colorCategoria(n)).toBe(colorCategoria(0));
    expect(colorCategoria(n + 3)).toBe(colorCategoria(3));
  });

  it('tolera índices negativos', () => {
    expect(colorCategoria(-1)).toBe(PALETA_CATEGORICA[PALETA_CATEGORICA.length - 1]);
  });

  it('no usa el rojo ni el verde SEMÁNTICOS como color de categoría', () => {
    // En una torta de granjas una porción roja se lee como «esta granja está mal», y no significa
    // nada de eso. El rojo es peligro y el verde es éxito: reservados.
    expect(PALETA_CATEGORICA).not.toContain('#DC2626');
    expect(PALETA_CATEGORICA).not.toContain('#16A34A');
  });

  it('coloresCategorias respeta la cantidad pedida', () => {
    expect(coloresCategorias(3).length).toBe(3);
    expect(coloresCategorias(0)).toEqual([]);
    expect(coloresCategorias(-2)).toEqual([]);
  });
});

describe('opciones de gráficas categóricas', () => {
  it('devuelven un objeto nuevo cada vez (Chart.js muta las opciones)', () => {
    expect(opcionesDona()).not.toBe(opcionesDona());
    expect(opcionesBarras()).not.toBe(opcionesBarras());
  });
});

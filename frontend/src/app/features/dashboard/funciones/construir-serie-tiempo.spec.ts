import { SerieTiempo } from '../models/dashboard-metricas.model';
import {
  construirSerieTiempo,
  ejeDeFechas,
  opcionesLinea,
  rangoDiario
} from './construir-serie-tiempo.funcion';
import { COLORES_MARCA } from './paleta-graficas.funcion';

const serie = (
  etiqueta: string,
  rol: SerieTiempo['rol'],
  puntos: [string, number | null][]
): SerieTiempo => ({
  etiqueta,
  rol,
  puntos: puntos.map(([fecha, valor]) => ({ fecha, valor }))
});

describe('construirSerieTiempo', () => {
  it('arma el eje en orden cronológico, cruzando todas las series', () => {
    const chart = construirSerieTiempo([
      serie('A', 'principal', [['2026-03-03', 1], ['2026-03-01', 2]]),
      serie('B', 'secundaria', [['2026-03-02', 3]])
    ]);

    expect(chart.labels).toEqual(['01/03', '02/03', '03/03']);
  });

  // ------------------------------------------------------------------ LA regla del archivo

  it('12 · un día sin dato queda en null, NO en cero', () => {
    const chart = construirSerieTiempo([
      serie('Mortalidad', 'alerta', [
        ['2026-03-01', 5],
        // 02/03 no se cargó
        ['2026-03-03', 7]
      ])
    ]);

    expect(chart.datasets[0].data).toEqual([5, null, 7]);
    // Y explícito, porque es el error que se quiere impedir:
    expect(chart.datasets[0].data).not.toEqual([5, 0, 7]);
  });

  it('12b · un cero MEDIDO se conserva como cero (no se confunde con «sin dato»)', () => {
    // Mortalidad 0 es un hecho: ese día no murió ninguna ave. Si esto saliera `null`, la gráfica
    // borraría un dato real.
    const chart = construirSerieTiempo([
      serie('Mortalidad', 'alerta', [['2026-03-01', 0], ['2026-03-02', 4]])
    ]);

    expect(chart.datasets[0].data).toEqual([0, 4]);
  });

  it('12c · el dataset pide spanGaps:false para que el hueco se DIBUJE como corte', () => {
    // Sin esto, Chart.js une los extremos con una recta e inventa la tendencia del medio.
    const chart = construirSerieTiempo([serie('A', 'principal', [['2026-03-01', 1]])]);
    expect((chart.datasets[0] as { spanGaps?: boolean }).spanGaps).toBe(false);
  });

  it('12d · rellena con null las fechas que otra serie tiene y esta no', () => {
    const chart = construirSerieTiempo([
      serie('Completa', 'principal', [['2026-03-01', 1], ['2026-03-02', 2]]),
      serie('Parcial', 'secundaria', [['2026-03-02', 9]])
    ]);

    expect(chart.datasets[0].data).toEqual([1, 2]);
    expect(chart.datasets[1].data).toEqual([null, 9]);
  });

  it('valores no numéricos (NaN, Infinity, undefined) se tratan como sin dato', () => {
    const chart = construirSerieTiempo([
      serie('A', 'principal', [
        ['2026-03-01', NaN],
        ['2026-03-02', Infinity],
        ['2026-03-03', undefined as unknown as number],
        ['2026-03-04', 5]
      ])
    ]);

    expect(chart.datasets[0].data).toEqual([null, null, null, 5]);
  });

  // ------------------------------------------------------------------ eje forzado

  it('con eje forzado dibuja TODOS los días del período, aunque no haya dato', () => {
    const chart = construirSerieTiempo(
      [serie('A', 'principal', [['2026-03-02', 7]])],
      ['2026-03-01', '2026-03-02', '2026-03-03']
    );

    expect(chart.labels).toEqual(['01/03', '02/03', '03/03']);
    expect(chart.datasets[0].data).toEqual([null, 7, null]);
  });

  // ------------------------------------------------------------------ color por rol

  it('el color sale del ROL, no lo elige el llamador', () => {
    const chart = construirSerieTiempo([
      serie('Producción', 'principal', [['2026-03-01', 1]]),
      serie('Mortalidad', 'alerta', [['2026-03-01', 1]]),
      serie('Guía', 'referencia', [['2026-03-01', 1]]),
      serie('Cumplido', 'exito', [['2026-03-01', 1]])
    ]);

    expect(chart.datasets[0].borderColor).toBe(COLORES_MARCA.naranja);
    expect(chart.datasets[1].borderColor).toBe(COLORES_MARCA.peligro);
    expect(chart.datasets[2].borderColor).toBe(COLORES_MARCA.neutro);
    expect(chart.datasets[3].borderColor).toBe(COLORES_MARCA.exito);
  });

  it('la serie de referencia va punteada y las demás enteras', () => {
    const chart = construirSerieTiempo([
      serie('Real', 'principal', [['2026-03-01', 1]]),
      serie('Guía', 'referencia', [['2026-03-01', 1]])
    ]);

    expect(chart.datasets[0].borderDash).toBeUndefined();
    expect(chart.datasets[1].borderDash).toEqual([6, 4]);
  });

  // ------------------------------------------------------------------ bordes

  it('sin series devuelve un gráfico vacío, no revienta', () => {
    const chart = construirSerieTiempo([]);
    expect(chart.labels).toEqual([]);
    expect(chart.datasets).toEqual([]);
  });

  it('tolera entradas nulas y series sin puntos', () => {
    const chart = construirSerieTiempo([
      null as unknown as SerieTiempo,
      { etiqueta: 'Vacía', rol: 'principal', puntos: [] }
    ]);

    expect(chart.datasets.length).toBe(1);
    expect(chart.datasets[0].data).toEqual([]);
  });

  it('una fecha repetida no duplica la columna del eje', () => {
    const chart = construirSerieTiempo([
      serie('A', 'principal', [['2026-03-01', 1], ['2026-03-01', 9]])
    ]);

    expect(chart.labels).toEqual(['01/03']);
    expect(chart.datasets[0].data).toEqual([9]); // gana la última, no se suman
  });
});

describe('ejeDeFechas', () => {
  it('🔴 devuelve el RANGO DIARIO COMPLETO, no la unión de las fechas presentes', () => {
    // Este es el corazón del archivo, y salió de un test que falló: con la unión, el eje de
    // [01, 05] era `['2026-03-01','2026-03-05']` — los tres días del medio DESAPARECÍAN y la línea
    // unía los extremos como si fueran contiguos. El hueco sólo puede dibujarse si el día está
    // en el eje con valor null.
    const eje = ejeDeFechas([
      serie('A', 'principal', [['2026-03-05', 1], ['', 2], ['2026-03-01', 3]]),
      serie('B', 'principal', [['2026-03-05', 4]])
    ]);

    expect(eje).toEqual([
      '2026-03-01', '2026-03-02', '2026-03-03', '2026-03-04', '2026-03-05'
    ]);
  });

  it('una sola fecha da un eje de un día', () => {
    expect(ejeDeFechas([serie('A', 'principal', [['2026-03-01', 1]])])).toEqual(['2026-03-01']);
  });

  it('sin fechas devuelve vacío', () => {
    expect(ejeDeFechas([])).toEqual([]);
    expect(ejeDeFechas([serie('A', 'principal', [])])).toEqual([]);
  });

  it('con un rango absurdo cae a las fechas presentes en vez de colgar la pestaña', () => {
    // Una fecha mal cargada (año 202) generaría ~660.000 puntos.
    const eje = ejeDeFechas([serie('A', 'principal', [['0202-03-01', 1], ['2026-03-01', 2]])]);
    expect(eje).toEqual(['0202-03-01', '2026-03-01']);
  });
});

describe('rangoDiario', () => {
  it('incluye los dos extremos', () => {
    expect(rangoDiario('2026-03-01', '2026-03-03'))
      .toEqual(['2026-03-01', '2026-03-02', '2026-03-03']);
  });

  it('cruza fin de mes y año bisiesto sin saltear ni repetir días', () => {
    expect(rangoDiario('2028-02-27', '2028-03-01'))
      .toEqual(['2028-02-27', '2028-02-28', '2028-02-29', '2028-03-01']);
  });

  it('cuenta en UTC: un cambio de horario de verano no salta ni repite un día', () => {
    // Sumar 24 h sobre una fecha LOCAL en la madrugada del cambio devuelve el mismo día otra vez.
    const marzo = rangoDiario('2026-03-28', '2026-03-31');
    expect(marzo).toEqual(['2026-03-28', '2026-03-29', '2026-03-30', '2026-03-31']);
    expect(new Set(marzo).size).toBe(4);

    const octubre = rangoDiario('2026-10-31', '2026-11-02');
    expect(octubre).toEqual(['2026-10-31', '2026-11-01', '2026-11-02']);
  });

  it('rango invertido o fechas inválidas devuelven vacío', () => {
    expect(rangoDiario('2026-03-05', '2026-03-01')).toEqual([]);
    expect(rangoDiario('ayer', '2026-03-01')).toEqual([]);
    expect(rangoDiario('2026-3-1', '2026-03-05')).toEqual([]);
  });

  it('mismo día devuelve ese día', () => {
    expect(rangoDiario('2026-03-01', '2026-03-01')).toEqual(['2026-03-01']);
  });
});

describe('opcionesLinea', () => {
  it('devuelve un objeto NUEVO cada vez', () => {
    // Chart.js muta las opciones (guarda estado de las escalas): compartir una constante entre dos
    // gráficas las acopla y la segunda hereda el eje de la primera.
    expect(opcionesLinea()).not.toBe(opcionesLinea());
  });

  it('sin título de eje no inventa uno', () => {
    const opciones = opcionesLinea();
    expect(opciones?.scales?.['y']?.title).toBeUndefined();

    const conTitulo = opcionesLinea('Aves');
    expect(conTitulo?.scales?.['y']?.title).toEqual(
      jasmine.objectContaining({ display: true, text: 'Aves' })
    );
  });
});

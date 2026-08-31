import { of } from 'rxjs';
import { IndicadoresDiariosEngordeComputeService } from './indicadores-diarios-engorde-compute.service';
import { GuiaGeneticaEcuadorDetalleDto, GuiaGeneticaEcuadorService } from '../../config/guia-genetica-ecuador/guia-genetica-ecuador.service';
import { SeguimientoLoteLevanteDto } from '../../lote-levante/services/seguimiento-lote-levante.service';
import { LoteDto } from '../../lote/services/lote.service';
import { IndicadorDiarioFilaEngorde } from '../models/indicadores-diarios-engorde.models';

/**
 * TK Lady Malave (21-ago-2026): ganancia diaria de la tabla de indicadores diarios de pollo
 * engorde no dividia el delta de peso entre los dias transcurridos cuando el pesaje deja de ser
 * diario (1ra semana a diario, luego cada 4 dias). Casos cubren: pesaje diario (sin cambio),
 * pesaje cada 4 dias, un intervalo distinto de 4 (generalizacion pedida por Moises) y un dia sin
 * peso registrado en medio de un tramo.
 *
 * Ticket Panama (31-ago-2026): `row.dia` paso a ser el DIA DE NEGOCIO 1-based (el primer dia con
 * registro es el dia 1; no existe el dia 0, y en un lote que llego a las 13:00 o despues ese
 * primer dia es el siguiente al encaset). La EDAD (0-based) se conserva internamente para el
 * cruce con la guia genetica y para la aritmetica de ganancia, que no cambian.
 */
describe('IndicadoresDiariosEngordeComputeService — ganancia diaria', () => {
  const ENCASET = '2026-01-01';
  const PESO_INI = 40;

  function guiaFake(): GuiaGeneticaEcuadorService {
    const detalle: GuiaGeneticaEcuadorDetalleDto[] = [
      {
        sexo: 'mixto',
        dia: 0,
        pesoCorporalG: PESO_INI,
        gananciaDiariaG: 5,
        promedioGananciaDiariaG: 5,
        cantidadAlimentoDiarioG: 10,
        alimentoAcumuladoG: 10,
        ca: 0.2,
        mortalidadSeleccionDiaria: 0
      }
    ];
    return { getDatos: () => of(detalle) } as unknown as GuiaGeneticaEcuadorService;
  }

  /** Guia con dos dias distintos para verificar que el cruce sigue siendo por EDAD. */
  function guiaFakeDosDias(): GuiaGeneticaEcuadorService {
    const base = {
      sexo: 'mixto',
      gananciaDiariaG: 5,
      promedioGananciaDiariaG: 5,
      cantidadAlimentoDiarioG: 10,
      alimentoAcumuladoG: 10,
      ca: 0.2,
      mortalidadSeleccionDiaria: 0
    };
    const detalle: GuiaGeneticaEcuadorDetalleDto[] = [
      { ...base, dia: 0, pesoCorporalG: PESO_INI } as GuiaGeneticaEcuadorDetalleDto,
      { ...base, dia: 1, pesoCorporalG: 62 } as GuiaGeneticaEcuadorDetalleDto
    ];
    return { getDatos: () => of(detalle) } as unknown as GuiaGeneticaEcuadorService;
  }

  function lote(): LoteDto {
    return {
      loteId: 1,
      loteNombre: 'Lote test',
      granjaId: 1,
      fechaEncaset: ENCASET,
      raza: 'Ross',
      anoTablaGenetica: 2022,
      pesoMixto: PESO_INI,
      avesEncasetadas: 1000
    } as LoteDto;
  }

  /** Fila minima de seguimiento en la EDAD `edad` (dias desde ENCASET; 0 = dia del encaset). */
  function reg(edad: number, pesoH: number, pesoM: number): SeguimientoLoteLevanteDto {
    const fecha = new Date(2026, 0, 1 + edad, 12, 0, 0, 0);
    const ymd = `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(fecha.getDate()).padStart(2, '0')}`;
    return {
      id: edad,
      fechaRegistro: ymd,
      loteId: '1',
      mortalidadHembras: 0,
      mortalidadMachos: 0,
      selH: 0,
      selM: 0,
      errorSexajeHembras: 0,
      errorSexajeMachos: 0,
      tipoAlimento: '',
      consumoKgHembras: 0,
      pesoPromH: pesoH,
      pesoPromM: pesoM
    } as SeguimientoLoteLevanteDto;
  }

  function filaDia(filas: IndicadorDiarioFilaEngorde[], dia: number): IndicadorDiarioFilaEngorde {
    const f = filas.find(x => x.dia === dia);
    if (!f) throw new Error(`No se encontro fila del dia ${dia}`);
    return f;
  }

  it('pesaje diario (1ra semana): ganancia = delta sin dividir (divisor 1, sin regresion)', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    const seguimientos = [
      reg(1, 45, 45),
      reg(2, 50, 50),
      reg(3, 55, 55),
      reg(4, 60, 60),
      reg(5, 65, 65),
      reg(6, 70, 70),
      reg(7, 75, 75)
    ];

    const { filas, guiaOk } = await service.compute(seguimientos, lote());

    expect(guiaOk).toBe(true);
    // Sin hora: dia mostrado = edad + 1 (edades 1..7 → dias 2..8).
    for (const dia of [2, 3, 4, 5, 6, 7, 8]) {
      expect(filaDia(filas, dia).gananciaDiariaRealG).toBe(5);
    }
  });

  it('pesaje cada 4 dias tras la 1ra semana: ganancia = delta / 4 (caso reportado)', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    const seguimientos = [
      reg(7, 75, 75),
      reg(11, 95, 95) // +20 g en 4 dias reales
    ];

    const { filas } = await service.compute(seguimientos, lote());

    expect(filaDia(filas, 12).gananciaDiariaRealG).toBe(5);
  });

  it('intervalo distinto de 4 dias: ganancia = delta / dias reales transcurridos', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    const seguimientos = [
      reg(11, 95, 95),
      reg(16, 120, 120) // +25 g en 5 dias reales
    ];

    const { filas } = await service.compute(seguimientos, lote());

    expect(filaDia(filas, 17).gananciaDiariaRealG).toBe(5);
  });

  it('dia sin peso registrado en medio de un tramo: null y no mueve el ultimo pesaje', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    const seguimientos = [
      reg(11, 95, 95),
      reg(13, 0, 0), // sin pesaje ese dia
      reg(16, 120, 120) // debe seguir comparando contra la edad 11, no la 13
    ];

    const { filas } = await service.compute(seguimientos, lote());

    expect(filaDia(filas, 14).gananciaDiariaRealG).toBeNull();
    expect(filaDia(filas, 17).gananciaDiariaRealG).toBe(5);
  });

  it('primer pesaje del lote: compara contra el peso inicial del dia del encaset', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    const seguimientos = [reg(1, 45, 45)];

    const { filas } = await service.compute(seguimientos, lote());

    // (45 - PESO_INI) / (edad 1 - edad 0) = 5 — la aritmetica sigue en EDADES.
    expect(filaDia(filas, 2).gananciaDiariaRealG).toBe(45 - PESO_INI);
  });

  // ─── Numeracion de negocio (ticket Panama 31-ago-2026: no existe el dia 0) ───

  it('sin hora: el dia del encaset es el dia 1 y ninguna fila queda en dia 0', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    const seguimientos = [reg(0, 42, 42), reg(1, 45, 45), reg(2, 50, 50)];

    const { filas } = await service.compute(seguimientos, lote());

    expect(filas.map(f => f.dia)).toEqual([1, 2, 3]);
    expect(filas.every(f => f.dia >= 1)).toBe(true);
  });

  it('lote tardio (hora >= 13:00): el primer dia con registro (edad 1) se numera dia 1', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    // Con llegada 21:33 el dia del encaset no admite registro: el lote arranca en la edad 1.
    const seguimientos = [reg(1, 45, 45), reg(2, 50, 50), reg(3, 55, 55)];

    const { filas } = await service.compute(seguimientos, lote(), '21:33');

    expect(filas.map(f => f.dia)).toEqual([1, 2, 3]);
    // La ganancia no cambia: el divisor sigue siendo en edades (45 − 40 en 1 dia).
    expect(filaDia(filas, 1).gananciaDiariaRealG).toBe(5);
  });

  it('la guia genetica sigue cruzando por EDAD aunque el dia mostrado sea 1-based', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFakeDosDias());
    const seguimientos = [reg(0, 42, 42), reg(1, 60, 60)];

    const { filas } = await service.compute(seguimientos, lote());

    // Fila mostrada como dia 1 = edad 0 → guia del dia 0 (40 g); dia 2 = edad 1 → guia del dia 1 (62 g).
    expect(filaDia(filas, 1).pesoTablaG).toBe(PESO_INI);
    expect(filaDia(filas, 2).pesoTablaG).toBe(62);
  });
});

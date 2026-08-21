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

  /** Fila minima de seguimiento: dia de vida `diaVida` (respecto a ENCASET) con un peso mixto dado. */
  function reg(diaVida: number, pesoH: number, pesoM: number): SeguimientoLoteLevanteDto {
    const fecha = new Date(2026, 0, 1 + diaVida, 12, 0, 0, 0);
    const ymd = `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(fecha.getDate()).padStart(2, '0')}`;
    return {
      id: diaVida,
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
    for (const dia of [1, 2, 3, 4, 5, 6, 7]) {
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

    expect(filaDia(filas, 11).gananciaDiariaRealG).toBe(5);
  });

  it('intervalo distinto de 4 dias: ganancia = delta / dias reales transcurridos', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    const seguimientos = [
      reg(11, 95, 95),
      reg(16, 120, 120) // +25 g en 5 dias reales
    ];

    const { filas } = await service.compute(seguimientos, lote());

    expect(filaDia(filas, 16).gananciaDiariaRealG).toBe(5);
  });

  it('dia sin peso registrado en medio de un tramo: null y no mueve el ultimo pesaje', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    const seguimientos = [
      reg(11, 95, 95),
      reg(13, 0, 0), // sin pesaje ese dia
      reg(16, 120, 120) // debe seguir comparando contra el dia 11, no el 13
    ];

    const { filas } = await service.compute(seguimientos, lote());

    expect(filaDia(filas, 13).gananciaDiariaRealG).toBeNull();
    expect(filaDia(filas, 16).gananciaDiariaRealG).toBe(5);
  });

  it('primer pesaje del lote: compara contra el peso inicial en el dia 0', async () => {
    const service = new IndicadoresDiariosEngordeComputeService(guiaFake());
    const seguimientos = [reg(1, 45, 45)];

    const { filas } = await service.compute(seguimientos, lote());

    // (45 - PESO_INI) / (1 - 0) = 5
    expect(filaDia(filas, 1).gananciaDiariaRealG).toBe(45 - PESO_INI);
  });
});

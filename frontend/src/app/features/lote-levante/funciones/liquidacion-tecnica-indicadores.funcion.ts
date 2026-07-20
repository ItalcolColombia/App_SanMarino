/**
 * Construcción pura de la tabla de indicadores y del cálculo de cumplimiento vs. guía genética.
 *
 * Extraído de `liquidacion-tecnica.component.ts` SIN cambiar comportamiento: mismas fórmulas,
 * mismos umbrales (0.2, 0.1, 2, 5/10/20 %), mismo manejo de nulos y mismo orden de operaciones.
 * Módulo financiero/técnico frágil: preservar CADA número. Funciones puras (sin `this`/DI/estado).
 *
 * Nota: `construirIndicadores` NO lleva anotación de tipo de retorno a propósito, para que el tipo
 * inferido (incluida la unión de `cumple: number | boolean`) sea idéntico al del getter original y
 * el chequeo de tipos de la plantilla no cambie.
 */
import { LiquidacionTecnicaDto } from '../services/liquidacion-tecnica.service';
import { LiquidacionTecnicaComparacionDto } from '../services/liquidacion-comparacion.service';
import { CumplimientoGeneralInput, CumplimientoGeneralResult, GuiaValoresEsperados } from '../models/liquidacion-tecnica.model';

/** Tabla comparativa de indicadores (Real vs. Guía). Devuelve `[]` si no hay liquidación. */
export function construirIndicadores(
  liquidacion: LiquidacionTecnicaDto | null,
  comparacion: LiquidacionTecnicaComparacionDto | null,
  guia: GuiaValoresEsperados
) {
  if (!liquidacion) return [];

  return [
    {
      concepto: 'Mortalidad Hembras',
      real: liquidacion.porcentajeMortalidadHembras,
      guia: guia.mortalidadEsperadaHembrasGuia > 0 ? guia.mortalidadEsperadaHembrasGuia : null,
      diferencia: guia.mortalidadEsperadaHembrasGuia > 0
                  ? (liquidacion.porcentajeMortalidadHembras - guia.mortalidadEsperadaHembrasGuia)
                  : null,
      unidad: '%',
      tipo: 'porcentaje',
      cumple: guia.mortalidadEsperadaHembrasGuia > 0
              ? Math.abs(liquidacion.porcentajeMortalidadHembras - guia.mortalidadEsperadaHembrasGuia) <= (guia.mortalidadEsperadaHembrasGuia * 0.2)
              : false
    },
    {
      concepto: 'Mortalidad Machos',
      real: liquidacion.porcentajeMortalidadMachos,
      guia: guia.mortalidadEsperadaMachosGuia > 0 ? guia.mortalidadEsperadaMachosGuia : null,
      diferencia: guia.mortalidadEsperadaMachosGuia > 0
                  ? (liquidacion.porcentajeMortalidadMachos - guia.mortalidadEsperadaMachosGuia)
                  : null,
      unidad: '%',
      tipo: 'porcentaje',
      cumple: guia.mortalidadEsperadaMachosGuia > 0
              ? Math.abs(liquidacion.porcentajeMortalidadMachos - guia.mortalidadEsperadaMachosGuia) <= (guia.mortalidadEsperadaMachosGuia * 0.2)
              : false
    },
    {
      concepto: 'Selección Hembras',
      real: liquidacion.porcentajeSeleccionHembras,
      guia: null,
      diferencia: null,
      unidad: '%',
      tipo: 'porcentaje',
      cumple: true // No hay guía para selección
    },
    {
      concepto: 'Selección Machos',
      real: liquidacion.porcentajeSeleccionMachos,
      guia: null,
      diferencia: null,
      unidad: '%',
      tipo: 'porcentaje',
      cumple: true // No hay guía para selección
    },
    {
      concepto: 'Retiro Total Hembras',
      real: liquidacion.porcentajeRetiroTotalHembras,
      guia: liquidacion.porcentajeRetiroGuia,
      diferencia: liquidacion.porcentajeRetiroGuia ?
        liquidacion.porcentajeRetiroTotalHembras - liquidacion.porcentajeRetiroGuia : null,
      unidad: '%',
      tipo: 'porcentaje',
      cumple: true // Usar lógica existente
    },
    {
      concepto: 'Retiro Total Machos',
      real: liquidacion.porcentajeRetiroTotalMachos,
      guia: liquidacion.porcentajeRetiroGuia,
      diferencia: liquidacion.porcentajeRetiroGuia ?
        liquidacion.porcentajeRetiroTotalMachos - liquidacion.porcentajeRetiroGuia : null,
      unidad: '%',
      tipo: 'porcentaje',
      cumple: true // Usar lógica existente
    },
    {
      concepto: 'Consumo Alimento',
      real: liquidacion.consumoAlimentoRealGramos,
      guia: comparacion?.consumoAcumuladoEsperadoHembras || liquidacion.consumoAlimentoGuiaGramos,
      diferencia: comparacion?.diferenciaConsumoHembras || liquidacion.porcentajeDiferenciaConsumo,
      unidad: 'gr',
      tipo: 'peso',
      cumple: comparacion?.cumpleConsumoHembras || false
    },
    {
      concepto: 'Peso Semana 25 (Hembras)',
      real: liquidacion.pesoSemana25RealHembras || 0,
      guia: guia.pesoEsperadoHembrasGuia > 0 ? guia.pesoEsperadoHembrasGuia : null,
      diferencia: liquidacion.pesoSemana25RealHembras && guia.pesoEsperadoHembrasGuia > 0
                  ? (liquidacion.pesoSemana25RealHembras - guia.pesoEsperadoHembrasGuia)
                  : null,
      unidad: 'gr',
      tipo: 'peso',
      cumple: liquidacion.pesoSemana25RealHembras && guia.pesoEsperadoHembrasGuia > 0
              ? Math.abs(liquidacion.pesoSemana25RealHembras - guia.pesoEsperadoHembrasGuia) / guia.pesoEsperadoHembrasGuia <= 0.1
              : false
    },
    {
      concepto: 'Peso Semana 25 (Machos)',
      real: liquidacion.pesoSemana25RealMachos || 0,
      guia: guia.pesoEsperadoMachosGuia > 0 ? guia.pesoEsperadoMachosGuia : null,
      diferencia: liquidacion.pesoSemana25RealMachos && guia.pesoEsperadoMachosGuia > 0
                  ? (liquidacion.pesoSemana25RealMachos - guia.pesoEsperadoMachosGuia)
                  : null,
      unidad: 'gr',
      tipo: 'peso',
      cumple: liquidacion.pesoSemana25RealMachos && guia.pesoEsperadoMachosGuia > 0
              ? Math.abs(liquidacion.pesoSemana25RealMachos - guia.pesoEsperadoMachosGuia) / guia.pesoEsperadoMachosGuia <= 0.1
              : false
    },
    {
      concepto: 'Uniformidad (Hembras)',
      real: liquidacion.uniformidadRealHembras || 0,
      guia: guia.uniformidadEsperadaHembrasGuia > 0 ? guia.uniformidadEsperadaHembrasGuia : null,
      diferencia: liquidacion.uniformidadRealHembras && guia.uniformidadEsperadaHembrasGuia > 0
        ? (liquidacion.uniformidadRealHembras - guia.uniformidadEsperadaHembrasGuia)
        : null,
      unidad: '%',
      tipo: 'porcentaje',
      cumple: liquidacion.uniformidadRealHembras && guia.uniformidadEsperadaHembrasGuia > 0
        ? Math.abs(liquidacion.uniformidadRealHembras - guia.uniformidadEsperadaHembrasGuia) <= 2
        : false
    },
    {
      concepto: 'Uniformidad (Machos)',
      real: liquidacion.uniformidadRealMachos || 0,
      guia: guia.uniformidadEsperadaMachosGuia > 0 ? guia.uniformidadEsperadaMachosGuia : null,
      diferencia: liquidacion.uniformidadRealMachos && guia.uniformidadEsperadaMachosGuia > 0
        ? (liquidacion.uniformidadRealMachos - guia.uniformidadEsperadaMachosGuia)
        : null,
      unidad: '%',
      tipo: 'porcentaje',
      cumple: liquidacion.uniformidadRealMachos && guia.uniformidadEsperadaMachosGuia > 0
        ? Math.abs(liquidacion.uniformidadRealMachos - guia.uniformidadEsperadaMachosGuia) <= 2
        : false
    }
  ].filter(ind => ind.real != null);
}

/**
 * Cumplimiento general vs. guía (peso, consumo, mortalidad). Devuelve el conteo de
 * parámetros óptimos y el % de cumplimiento promedio. Aritmética idéntica al método original.
 *
 * REQ-010f/REQ-002h: se retiró el parámetro "Conversión Alimenticia" (KPI de pollo de engorde,
 * no aplica a reproductoras) — el promedio pasa de 4 a 3 parámetros. Cambio de comportamiento
 * INTENCIONAL pedido por el REQ (antes promediaba peso+consumo+mortalidad+conversión).
 */
export function calcularCumplimientoGeneral(input: CumplimientoGeneralInput): CumplimientoGeneralResult {
  let cumplimientos: number[] = [];
  let parametrosOptimos = 0;

  // Peso promedio
  const diferenciaPeso = Math.abs(input.pesoEsperadoGuia - input.pesoReal);
  const porcentajePeso = input.pesoEsperadoGuia > 0 ? (diferenciaPeso / input.pesoEsperadoGuia) * 100 : 100;
  const cumplimientoPeso = Math.max(0, 100 - porcentajePeso);
  cumplimientos.push(cumplimientoPeso);
  if (porcentajePeso <= 5) parametrosOptimos++;

  // Consumo
  const diferenciaConsumo = Math.abs(input.consumoEsperadoGuia - input.consumoReal);
  const porcentajeConsumo = input.consumoEsperadoGuia > 0 ? (diferenciaConsumo / input.consumoEsperadoGuia) * 100 : 100;
  const cumplimientoConsumo = Math.max(0, 100 - porcentajeConsumo);
  cumplimientos.push(cumplimientoConsumo);
  if (porcentajeConsumo <= 10) parametrosOptimos++;

  // Mortalidad
  const diferenciaMortalidad = Math.abs(input.mortalidadEsperadaGuia - input.mortalidadReal);
  const porcentajeMortalidad = input.mortalidadEsperadaGuia > 0 ? (diferenciaMortalidad / input.mortalidadEsperadaGuia) * 100 : 100;
  const cumplimientoMortalidad = Math.max(0, 100 - porcentajeMortalidad);
  cumplimientos.push(cumplimientoMortalidad);
  if (porcentajeMortalidad <= 20) parametrosOptimos++;

  // Promedio general
  const porcentajeCumplimientoGeneral = cumplimientos.reduce((sum, val) => sum + val, 0) / cumplimientos.length;
  return { parametrosOptimos, porcentajeCumplimientoGeneral };
}

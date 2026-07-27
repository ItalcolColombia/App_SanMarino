import { ValoresClasificadoraHuevo, TotalesHuevo } from '../models/huevo-levante.model';

/**
 * Totales de la clasificadora fija de huevos.
 *
 * Aritmética IDÉNTICA a la de producción
 * (`modal-seguimiento-diario.component.ts → totalesClasificadoraFija()`) y al cálculo puro del
 * backend (`HuevosLevanteCalculos`), para que los tres den siempre el mismo número:
 *
 * ```
 * incubables = limpio + tratado
 * totales    = incubables + sucio + deforme + blanco + dobleYema + piso + pequeno + roto + desecho + otro
 * ```
 *
 * Función PURA: sin `this`, sin DI, sin estado. Tolera null/undefined/strings (los inputs `number`
 * de Angular devuelven string cuando el usuario escribe).
 */
export function totalesHuevosLevante(valores: ValoresClasificadoraHuevo): TotalesHuevo {
  const n = (v: unknown): number => {
    const num = Number(v);
    return Number.isFinite(num) ? num : 0;
  };

  const incubables = n(valores.huevoLimpio) + n(valores.huevoTratado);

  const totales =
    incubables +
    n(valores.huevoSucio) +
    n(valores.huevoDeforme) +
    n(valores.huevoBlanco) +
    n(valores.huevoDobleYema) +
    n(valores.huevoPiso) +
    n(valores.huevoPequeno) +
    n(valores.huevoRoto) +
    n(valores.huevoDesecho) +
    n(valores.huevoOtro);

  return { incubables, totales };
}

/**
 * Eficiencia de producción = % de huevos incubables sobre el total, redondeado al entero.
 * Devuelve 0 cuando no hay huevos (sin división por cero). Mismo cálculo que
 * `getEficienciaProduccion()` del modal de producción.
 */
export function eficienciaHuevosLevante(totales: TotalesHuevo): number {
  if (totales.totales <= 0) return 0;
  return Math.round((totales.incubables / totales.totales) * 100);
}

/**
 * Aves con que ARRANCÓ un lote de engorde, y su total.
 *
 * Existe para separar dos números que el formulario trataba como uno solo y que significan cosas
 * opuestas:
 *
 * - `hembrasL` / `machosL` / `mixtas` del DTO son el **saldo vivo**: el seguimiento diario les
 *   descuenta las bajas y las ventas lo despachado.
 * - `inicialHembras` / `inicialMachos` / `inicialMixtas` son el **encasetamiento** (registro
 *   `Inicio` del historial), la base contra la que la serie diaria calcula el saldo de cada día.
 *
 * El formulario edita el encasetamiento. Cargarlo del saldo —lo que hacía antes— reescribía
 * `avesEncasetadas` con un número ya descontado y hacía que toda la serie volviera a restar las
 * mismas bajas.
 */

/** Lo mínimo que estas funciones necesitan de un lote; compatible con `LoteAveEngordeDto`. */
export interface AvesDelLote {
  hembrasL?: number | null;
  machosL?: number | null;
  mixtas?: number | null;
  avesEncasetadas?: number | null;
  inicialHembras?: number | null;
  inicialMachos?: number | null;
  inicialMixtas?: number | null;
}

/** Desglose de aves por bucket. */
export interface AvesPorSexo {
  hembras: number;
  machos: number;
  mixtas: number;
}

const num = (v: number | null | undefined): number => (v == null ? 0 : Number(v) || 0);

/**
 * Encasetamiento vigente del lote, para precargar el formulario de edición.
 *
 * Réplica exacta de `LoteAveEngordeService.InicialVigente` en el backend: **el fallback tiene que
 * coincidir** o un lote sin registro `Inicio` generaría un delta espurio con solo abrir y guardar
 * el formulario sin tocar nada. Sin `Inicio`, el total del encasetamiento vive en `mixtas` porque
 * el sistema no conoce su desglose por sexo (hoy los lotes de la base tienen todos su `Inicio`).
 */
export function avesInicialesDelLote(lote: AvesDelLote | null | undefined): AvesPorSexo {
  if (!lote) return { hembras: 0, machos: 0, mixtas: 0 };

  const tieneInicial =
    lote.inicialHembras != null || lote.inicialMachos != null || lote.inicialMixtas != null;

  if (tieneInicial) {
    return {
      hembras: num(lote.inicialHembras),
      machos: num(lote.inicialMachos),
      mixtas: num(lote.inicialMixtas)
    };
  }

  return { hembras: 0, machos: 0, mixtas: num(lote.avesEncasetadas) };
}

/** Saldo vivo del lote: lo que queda hoy después de bajas y ventas. Solo se muestra, no se edita. */
export function avesSaldoDelLote(lote: AvesDelLote | null | undefined): AvesPorSexo {
  if (!lote) return { hembras: 0, machos: 0, mixtas: 0 };
  return { hembras: num(lote.hembrasL), machos: num(lote.machosL), mixtas: num(lote.mixtas) };
}

/**
 * Total encasetado = hembras + machos + mixtas.
 *
 * Las **mixtas cuentan**: omitirlas dejaba en cero el total de los lotes mixtos (Panamá), que
 * llevan toda su población en ese bucket.
 */
export function totalAvesEncasetadas(aves: AvesPorSexo): number {
  return aves.hembras + aves.machos + aves.mixtas;
}

/** Aves que el ajuste agrega (positivo) o quita (negativo) respecto del encasetamiento vigente. */
export function deltaAvesEncasetadas(vigente: AvesPorSexo, propuesto: AvesPorSexo): number {
  return totalAvesEncasetadas(propuesto) - totalAvesEncasetadas(vigente);
}

/**
 * Total encasetado de un lote, para las tablas y los paneles de solo lectura.
 *
 * Prefiere `avesEncasetadas`, que es la columna que el backend mantiene alineada con el registro
 * `Inicio` (invariante de `fn_cuadre_aves_engorde`); si faltara, reconstruye desde el desglose
 * inicial. **El fallback nunca usa el saldo vivo**: sumar `hembrasL + machosL` daba un total ya
 * descontado, más chico que las aves encasetadas de la columna de al lado, y era justo la
 * incoherencia que se ve en pantalla.
 */
export function totalEncasetadoDelLote(lote: AvesDelLote | null | undefined): number {
  if (!lote) return 0;
  const declarado = num(lote.avesEncasetadas);
  return declarado > 0 ? declarado : totalAvesEncasetadas(avesInicialesDelLote(lote));
}

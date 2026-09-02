import type { CapturaPendienteResumen, OperacionPendiente } from '../models/outbox.model';

/**
 * Qué capturas sin enviar hay **de este lote, en esta pantalla y de esta sesión**.
 *
 * ## El problema que resuelve
 *
 * Después de guardar sin red, la fila capturada es **invisible**: la pantalla cierra el modal y
 * recarga la lista desde la caché de lectura, que no la tiene. El único rastro era un toast que
 * desaparece y un contador global en la barra de estado — que dice «3 sin enviar» pero no dice de
 * qué lote ni de qué día. En el galpón eso se lee como «se perdió», y el galponero vuelve a
 * capturarlo.
 *
 * ## Lo que esta función NO hace, y es lo importante
 *
 * **No devuelve el payload ni sus números.** Sólo la fecha del registro y si quedó rechazada. Eso es
 * deliberado: en F3 se descartó meter la fila capturada en el arreglo `seguimientos` porque viaja
 * tres niveles abajo a componentes compartidos que **no pueden distinguirla** de una guardada, y de
 * ahí entra al Excel, a los indicadores y a la gráfica **como dato real**. El servidor nunca vio esa
 * fila: un indicador calculado con ella es un número inventado.
 *
 * Devolviendo sólo «hay una captura del día X sin enviar», no hay ningún número que se pueda
 * confundir con un dato confirmado — la separación queda garantizada **por construcción**, no por un
 * filtro que alguien tiene que acordarse de poner en cada exportación.
 *
 * ## Fail-closed
 *
 * Sin partición o sin lote no devuelve **nada**. Mismo criterio que `claveParticion`: mostrar
 * capturas sin saber de quién ni de qué lote son es exactamente cómo se filtra el trabajo de un
 * operario en la pantalla de otro.
 */
export function resumirCapturasPendientes(
  operaciones: readonly OperacionPendiente[] | null | undefined,
  criterio: {
    /** Tipo de operación de ESTA pantalla (`seguimiento_levante_crear`, …). */
    readonly tipo: string;
    /** `{userId}|{companyId}|{paisId}` de la sesión activa. */
    readonly particion: string | null | undefined;
    /**
     * Campo(s) del payload que identifican al lote, con el valor que la pantalla está mostrando.
     * Coincide si **alguno** casa.
     *
     * Es un mapa y no un par suelto por producción: su payload lleva `lotePosturaProduccionId` en el
     * flujo nuevo y `produccionLoteId` en el legacy —uno de los dos siempre en `null`— y **los dos
     * valores son distintos**, así que un solo campo dejaría a la mitad de las capturas sin
     * encontrar. No truena, no avisa: la fila simplemente no aparecería.
     */
    readonly lote: Readonly<Record<string, string | number | null | undefined>>;
  }
): CapturaPendienteResumen[] {
  const { tipo, particion, lote } = criterio;

  if (!operaciones?.length || !tipo || !particion || !lote) {
    return [];
  }

  // El `0` y la cadena vacía cuentan como ausencia: un `!= null` los dejaría pasar y compararía
  // contra un lote que no existe. Si NINGÚN campo trae valor, no hay lote que mostrar.
  const buscados = Object.entries(lote)
    .map(([campo, valor]) => [campo, normalizarClave(valor)] as const)
    .filter((par): par is readonly [string, string] => par[1] !== null);

  if (buscados.length === 0) {
    return [];
  }

  const resumenes = operaciones
    .filter(op =>
      op.tipo === tipo
      && op.particion === particion
      && buscados.some(([campo, valor]) => normalizarClave(leerCampo(op.payload, campo)) === valor)
    )
    .map(op => ({
      clientOpId: op.clientOpId,
      fecha: fechaDelPayload(op.payload),
      capturadoAt: op.capturadoAtDispositivo,
      rechazada: op.estado === 'rechazada'
    }));

  // Por fecha del registro; a igual fecha (o sin fecha), por orden de captura. Estable y
  // predecible: el galponero busca el día, no el orden en que tocó Guardar.
  return resumenes.sort((a, b) =>
    (a.fecha ?? '').localeCompare(b.fecha ?? '') || a.capturadoAt.localeCompare(b.capturadoAt)
  );
}

/** Lee un campo del payload sin asumir su forma: viene del outbox como `unknown`. */
function leerCampo(payload: unknown, campo: string): unknown {
  if (!payload || typeof payload !== 'object') {
    return null;
  }
  return (payload as Record<string, unknown>)[campo];
}

/**
 * Normaliza un id a texto para comparar. El id de lote viaja como **cadena** en levante/engorde y
 * como **número** en producción; comparar con `===` entre tipos distintos no encuentra nada y no
 * rompe nada — el síntoma sería una tabla que nunca muestra la captura.
 */
function normalizarClave(valor: unknown): string | null {
  if (valor === null || valor === undefined) return null;
  const texto = String(valor).trim();
  return texto === '' || texto === '0' ? null : texto;
}

/**
 * Fecha del registro, recortada a `YYYY-MM-DD`.
 *
 * ⚠️ **No hay un único nombre de campo.** Levante, producción y pollo engorde mandan
 * `fechaRegistro`; la **reproductora de engorde manda `fecha`** —medido en su modal, que arma el
 * payload a mano—. Leer sólo uno dejaría a esa pantalla mostrando la captura sin día, que es
 * justamente el dato que el galponero necesita para reconocerla.
 */
function fechaDelPayload(payload: unknown): string | null {
  for (const campo of ['fechaRegistro', 'fecha']) {
    const cruda = leerCampo(payload, campo);
    if (typeof cruda !== 'string' || cruda.length < 10) {
      continue;
    }
    const ymd = cruda.slice(0, 10);
    if (/^\d{4}-\d{2}-\d{2}$/.test(ymd)) {
      return ymd;
    }
  }
  return null;
}

import type { IdentidadParticion } from '../models/offline.model';

/**
 * ¿Esta mutación se puede capturar sin red, y con qué tipo de operación viaja al servidor?
 *
 * ## Una sola fuente de verdad, y por qué
 *
 * En F2 la lista blanca de endpoints cacheables se escribió "a ojo" y cubría 23 de 78 rutas, con 7
 * entradas que no existían (una era un typo). No rompía el build ni ningún test: el único síntoma
 * era que esa pantalla no andaba sin red, y eso se descubre en la granja.
 *
 * Acá el riesgo es peor —una operación encolada que el servidor no sabe aplicar se rechaza como
 * `contrato_obsoleto` **después** de que el galponero creyó haberla guardado— así que la lista no es
 * una lista de rutas: es un mapa `ruta → tipo de operación`. Si el tipo no existe del lado del
 * servidor, la entrada no puede escribirse sin que se note. Agregar una ruta obliga a nombrar su
 * tipo, y el tipo obliga a que exista su rama en `SyncPushService.DespacharAsync`.
 */

/** Ruta (sin host ni query) + método → tipo de operación del contrato de sync. */
const OPERACIONES_SOPORTADAS: ReadonlyArray<{
  readonly metodo: string;
  readonly ruta: RegExp;
  readonly tipo: string;
}> = [
  {
    metodo: 'POST',
    // Alta de seguimiento diario de levante. Sin sufijo de id: el alta va al recurso, no a un item.
    ruta: /\/api\/SeguimientoLoteLevante\/?$/i,
    tipo: 'seguimiento_levante_crear'
  },
  {
    metodo: 'POST',
    // Alta de seguimiento diario de PRODUCCIÓN — la segunda etapa de postura. `$` al final para no
    // capturar `/seguimiento/{id}` ni los sub-recursos de indicadores.
    ruta: /\/api\/Produccion\/seguimiento\/?$/i,
    tipo: 'seguimiento_produccion_crear'
  },
  {
    metodo: 'POST',
    // Alta de seguimiento diario de POLLO ENGORDE.
    ruta: /\/api\/SeguimientoAvesEngordeEcuador\/?$/i,
    tipo: 'seguimiento_engorde_crear'
  },
  {
    metodo: 'POST',
    // Alta de seguimiento diario de la REPRODUCTORA de pollo engorde. Comparte pantalla con la
    // anterior pero es otro service y otra tabla, asi que es otro tipo de operacion.
    ruta: /\/api\/SeguimientoDiarioLoteReproductora\/?$/i,
    tipo: 'seguimiento_reproductora_engorde_crear'
  }
];

/**
 * Devuelve el tipo de operación si la mutación es encolable, o `null` si no.
 *
 * `null` significa "que falle como siempre": el error de red se propaga a la pantalla, que es el
 * comportamiento correcto para todo lo que el servidor no puede aplicar diferido.
 */
export function resolverTipoOperacion(metodo: string | null | undefined, url: string | null | undefined): string | null {
  if (!metodo || !url) {
    return null;
  }

  const metodoNormalizado = metodo.trim().toUpperCase();
  // Se compara solo el camino: la query no participa (y en un alta no debería haberla).
  const ruta = url.split('?')[0] ?? '';

  const encontrada = OPERACIONES_SOPORTADAS.find(
    op => op.metodo === metodoNormalizado && op.ruta.test(ruta)
  );

  return encontrada?.tipo ?? null;
}

/**
 * ¿Se puede encolar esta petición **con esta identidad**?
 *
 * Fail-closed, con el mismo criterio que `claveParticion`: sin los tres ids no se encola. Una
 * operación sin empresa no se puede aplicar después —el servidor la rechaza como
 * `empresa_no_autorizada`, y con razón— así que dejarla entrar solo serviría para que el galponero
 * crea que guardó algo que nunca va a poder salir.
 *
 * El `0` cuenta como ausencia: es el hueco que deja un campo sin llenar, no una empresa.
 */
export function decidirEncolable(
  metodo: string | null | undefined,
  url: string | null | undefined,
  identidad: IdentidadParticion | null | undefined
): boolean {
  if (resolverTipoOperacion(metodo, url) === null) {
    return false;
  }

  return identidadCompleta(identidad);
}

function identidadCompleta(identidad: IdentidadParticion | null | undefined): boolean {
  if (!identidad) {
    return false;
  }

  return presente(identidad.userId) && presente(identidad.companyId) && presente(identidad.paisId);
}

function presente(valor: string | number | null | undefined): boolean {
  if (valor === null || valor === undefined) {
    return false;
  }

  const texto = String(valor).trim();
  return texto !== '' && texto !== '0';
}

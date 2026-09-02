import { decidirEncolable, resolverTipoOperacion } from './decidir-encolable.funcion';
import type { IdentidadParticion } from '../models/offline.model';

/**
 * La lista blanca de escritura.
 *
 * Lo que se prueba acá es que **nada entre a la cola por accidente**. Una operación encolada que el
 * servidor no puede aplicar se rechaza recién al sincronizar — o sea, después de que el galponero
 * creyó haberla guardado y cerró la app.
 */
describe('decidirEncolable', () => {
  const identidadValida: IdentidadParticion = { userId: 'u-1', companyId: 4, paisId: 1 };
  const RUTA = 'http://localhost:5002/api/SeguimientoLoteLevante';

  describe('resolverTipoOperacion', () => {
    it('reconoce el alta de seguimiento de levante', () => {
      expect(resolverTipoOperacion('POST', RUTA)).toBe('seguimiento_levante_crear');
    });

    it('no distingue mayúsculas en el método', () => {
      expect(resolverTipoOperacion('post', RUTA)).toBe('seguimiento_levante_crear');
    });

    it('ignora la barra final', () => {
      expect(resolverTipoOperacion('POST', `${RUTA}/`)).toBe('seguimiento_levante_crear');
    });

    it('reconoce el alta de seguimiento de PRODUCCIÓN (la otra etapa de postura)', () => {
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/Produccion/seguimiento'))
        .toBe('seguimiento_produccion_crear');
    });

    it('🔑 NO confunde la EDICIÓN de producción con el alta', () => {
      // `/seguimiento/123` es un PUT de edición; encolarlo dejaría que un PUT sincronizado tarde
      // pise columnas del sistema.
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/Produccion/seguimiento/123')).toBeNull();
    });

    it('NO encola los sub-recursos de Producción', () => {
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/Produccion/lotes')).toBeNull();
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/Produccion/indicadores-semanales')).toBeNull();
    });

    it('reconoce el alta de POLLO ENGORDE', () => {
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/SeguimientoAvesEngordeEcuador'))
        .toBe('seguimiento_engorde_crear');
    });

    it('reconoce el alta de la REPRODUCTORA de pollo engorde', () => {
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/SeguimientoDiarioLoteReproductora'))
        .toBe('seguimiento_reproductora_engorde_crear');
    });

    it('reconoce el alta de GASTO DE INVENTARIO (H4)', () => {
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/inventario-gastos'))
        .toBe('gasto_inventario_crear');
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/inventario-gastos/'))
        .toBe('gasto_inventario_crear');
    });

    it('🔑 NO encola los sub-recursos de gastos de inventario', () => {
      // Son LECTURAS que alimentan el formulario. Encolar una devolveria un 202 sintetico en vez de
      // la lista de items, y el formulario se quedaria vacio creyendo que guardo algo.
      for (const sub of ['items', 'existencias', 'filter-data', 'conceptos', 'export']) {
        expect(resolverTipoOperacion('POST', `http://localhost:5002/api/inventario-gastos/${sub}`)).toBeNull();
      }
    });

    it('🔑 engorde y levante NO se confunden aunque compartan el cuerpo', () => {
      // Mismo payload, services distintos: si el tipo se equivocara, el seguimiento entraría en la
      // etapa que no es.
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/SeguimientoAvesEngordeEcuador'))
        .not.toBe(resolverTipoOperacion('POST', RUTA));
    });

    it('NO encola la carga masiva ni los sub-recursos de engorde', () => {
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/SeguimientoAvesEngordeEcuador/bulk')).toBeNull();
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/SeguimientoAvesEngordeEcuador/cuadrar-saldos')).toBeNull();
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/SeguimientoDiarioLoteReproductora/bulk')).toBeNull();
    });

    it('NO encola las lecturas', () => {
      expect(resolverTipoOperacion('GET', RUTA)).toBeNull();
    });

    it('NO encola la edición: un PUT sincronizado tarde pisa columnas del sistema', () => {
      expect(resolverTipoOperacion('PUT', `${RUTA}/123`)).toBeNull();
    });

    it('NO encola el borrado', () => {
      expect(resolverTipoOperacion('DELETE', `${RUTA}/123`)).toBeNull();
    });

    it('NO encola un POST a un sub-recurso que el servidor no sabe aplicar diferido', () => {
      expect(resolverTipoOperacion('POST', `${RUTA}/filter-data`)).toBeNull();
    });

    it('NO encola otra ruta parecida', () => {
      expect(resolverTipoOperacion('POST', 'http://localhost:5002/api/SeguimientoProduccion')).toBeNull();
    });

    it('la query no participa de la decisión', () => {
      expect(resolverTipoOperacion('POST', `${RUTA}?x=1`)).toBe('seguimiento_levante_crear');
    });

    it('tolera método o url ausentes', () => {
      expect(resolverTipoOperacion(null, RUTA)).toBeNull();
      expect(resolverTipoOperacion('POST', null)).toBeNull();
    });
  });

  describe('identidad', () => {
    it('encola con identidad completa', () => {
      expect(decidirEncolable('POST', RUTA, identidadValida)).toBe(true);
    });

    it('sin sesión no encola', () => {
      expect(decidirEncolable('POST', RUTA, null)).toBe(false);
    });

    it('sin empresa no encola: el servidor la rechazaría y la captura moriría en la bandeja', () => {
      expect(decidirEncolable('POST', RUTA, { ...identidadValida, companyId: null })).toBe(false);
    });

    it('el 0 cuenta como ausencia, no como empresa', () => {
      expect(decidirEncolable('POST', RUTA, { ...identidadValida, companyId: 0 })).toBe(false);
      expect(decidirEncolable('POST', RUTA, { ...identidadValida, paisId: 0 })).toBe(false);
      expect(decidirEncolable('POST', RUTA, { ...identidadValida, userId: '0' })).toBe(false);
    });

    it('la cadena vacía también es ausencia', () => {
      expect(decidirEncolable('POST', RUTA, { ...identidadValida, userId: '   ' })).toBe(false);
    });

    it('una ruta fuera de lista no encola ni con identidad completa', () => {
      expect(decidirEncolable('GET', RUTA, identidadValida)).toBe(false);
    });
  });
});

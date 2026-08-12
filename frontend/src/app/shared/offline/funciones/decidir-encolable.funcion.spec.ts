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

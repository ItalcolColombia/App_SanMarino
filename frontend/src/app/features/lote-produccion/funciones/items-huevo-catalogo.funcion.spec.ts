import { CatalogItemDto } from '../../catalogo-alimentos/services/catalogo-alimentos.service';
import { HuevoCatalogOption, HuevoFilaFija, TIPO_HUEVO_SIN_CATEGORIA } from '../models/huevo-clasificacion.model';
import { HuevoItemSeguimiento } from '../services/produccion.service';
import {
  agruparItemsHuevoPorTipo,
  construirFilasFijasHuevo,
  esItemEnKilos,
  esVigentePrimeraPostura,
  fusionarItemsHuevoGuardados,
  mapearItemsHuevoACatalogo,
  sumarCantidadesHuevo
} from './items-huevo-catalogo.funcion';

/**
 * V52/F7 y F10 — clasificación de huevos por ítems (Santa Reyes, flag `clasificacionHuevoPorItems`).
 *
 * `esVigentePrimeraPostura` es el ESPEJO de `HuevoPrimeraPosturaCalculos.EsVigente` (backend, con
 * sus propios tests xUnit). Los dos son fail-open a propósito: sin límite configurado —o sea, en
 * TODA empresa que no sea Santa Reyes— no se oculta absolutamente nada. Ese es el caso «flag OFF ⇒
 * comportamiento previo idéntico» que exige el patrón de features por empresa.
 */
describe('items-huevo-catalogo', () => {
  const item = (id: number, codigo: string, nombre: string, metadata?: unknown): CatalogItemDto => ({
    id,
    codigo,
    nombre,
    itemType: 'huevo',
    metadata,
    activo: true
  });

  describe('esVigentePrimeraPostura (espejo de HuevoPrimeraPosturaCalculos.EsVigente)', () => {
    it('vigente hasta el ULTIMO dia de la semana limite', () => {
      // Texto del cliente: "mostrar primera postura hasta el ultimo dia de la semana 22".
      expect(esVigentePrimeraPostura(22, 1)).toBe(true);
      expect(esVigentePrimeraPostura(22, 21)).toBe(true);
      expect(esVigentePrimeraPostura(22, 22)).toBe(true);
    });

    it('deja de estar vigente desde el PRIMER dia de la semana siguiente', () => {
      // "...desde el primer dia de la semana 23 no usa mas el item de primera postura".
      expect(esVigentePrimeraPostura(22, 23)).toBe(false);
      expect(esVigentePrimeraPostura(22, 60)).toBe(false);
    });

    it('sin limite configurado no oculta nada (flag OFF = comportamiento de siempre)', () => {
      // Es el caso de TODA empresa que no sea Santa Reyes: `huevoPrimeraPosturaHastaSemana` null.
      expect(esVigentePrimeraPostura(null, 1)).toBe(true);
      expect(esVigentePrimeraPostura(null, 500)).toBe(true);
    });

    it('sin semana de vida calculable tampoco oculta nada', () => {
      // Lote sin fecha de encaset todavia: no hay regla que aplicar, no se esconde una opcion.
      expect(esVigentePrimeraPostura(22, null)).toBe(true);
      expect(esVigentePrimeraPostura(null, null)).toBe(true);
    });
  });

  describe('mapearItemsHuevoACatalogo', () => {
    it('lee tipoHuevo, um y primeraPostura de la metadata en camelCase y en snake_case', () => {
      // El catalogo se cargo por vias distintas a lo largo del tiempo; las dos formas conviven.
      const opciones = mapearItemsHuevoACatalogo([
        item(1, 'H001', 'Huevo sin clasificar rojo', { tipoHuevo: 'Primera', um: 'UND', primeraPostura: true }),
        item(2, 'H002', 'Manchado rojo', { tipo_huevo: 'Pnc', UM: 'UND', primera_postura: true })
      ]);

      expect(opciones.length).toBe(2);
      expect(opciones[0]).toEqual(jasmine.objectContaining({
        id: 1, tipoHuevo: 'Primera', um: 'UND', primeraPostura: true, label: 'H001 — Huevo sin clasificar rojo'
      }));
      expect(opciones[1]).toEqual(jasmine.objectContaining({
        id: 2, tipoHuevo: 'Pnc', um: 'UND', primeraPostura: true
      }));
    });

    it('descarta los items sin id: no se puede guardar un desglose contra un item sin clave', () => {
      const opciones = mapearItemsHuevoACatalogo([
        item(0, 'X', 'Sin id'),
        { codigo: 'Y', nombre: 'Sin campo id', activo: true },
        item(7, 'Z', 'Con id')
      ]);

      expect(opciones.map(o => o.id)).toEqual([7]);
    });

    it('sin metadata deja los campos en null/false, no inventa un tipo', () => {
      const [op] = mapearItemsHuevoACatalogo([item(3, 'H003', 'Suelto')]);
      expect(op.tipoHuevo).toBeNull();
      expect(op.um).toBeNull();
      expect(op.primeraPostura).toBe(false);
    });

    it('arma el label con lo que haya: codigo y nombre, solo uno, o el id', () => {
      const opciones = mapearItemsHuevoACatalogo([
        item(4, '', 'Solo nombre'),
        item(5, 'SOLOCOD', ''),
        item(6, '', '')
      ]);

      expect(opciones.map(o => o.label)).toEqual(['Solo nombre', 'SOLOCOD', 'Ítem 6']);
    });
  });

  describe('fusionarItemsHuevoGuardados', () => {
    const guardado = (catalogItemId: number, codigo: string, nombre: string): HuevoItemSeguimiento =>
      ({ catalogItemId, codigo, nombre, cantidad: 10 } as HuevoItemSeguimiento);

    it('suma los items guardados que ya no estan en el catalogo (item desactivado)', () => {
      // Sin esto, editar un registro viejo perderia en pantalla lo que se habia guardado.
      const opciones = mapearItemsHuevoACatalogo([item(1, 'H001', 'Vigente')]);
      const fusion = fusionarItemsHuevoGuardados(opciones, [guardado(99, 'H099', 'Descatalogado')]);

      expect(fusion.map(o => o.id)).toEqual([1, 99]);
      expect(fusion[1].label).toBe('H099 — Descatalogado');
    });

    it('no duplica un item que ya esta en el catalogo', () => {
      const opciones = mapearItemsHuevoACatalogo([item(1, 'H001', 'Vigente')]);
      const fusion = fusionarItemsHuevoGuardados(opciones, [guardado(1, 'H001', 'Vigente')]);

      expect(fusion.length).toBe(1);
    });

    it('el item guardado nunca se marca como primeraPostura', () => {
      // La vigencia decide que se OFRECE como opcion nueva; lo ya elegido y guardado se mantiene
      // editable siempre, aunque el lote ya haya pasado la semana limite.
      const fusion = fusionarItemsHuevoGuardados([], [guardado(99, 'H099', 'Primera postura roja')]);
      expect(fusion[0].primeraPostura).toBe(false);
    });
  });

  describe('agruparItemsHuevoPorTipo', () => {
    const op = (id: number, label: string, tipoHuevo: string | null): HuevoCatalogOption =>
      ({ id, codigo: '', nombre: label, tipoHuevo, um: null, primeraPostura: false, label });

    it('pone Primera antes que Pnc y el resto al final, sin importar el orden de entrada', () => {
      const grupos = agruparItemsHuevoPorTipo([
        op(1, 'C', 'Pnc'),
        op(2, 'A', null),
        op(3, 'B', 'Primera')
      ]);

      expect(grupos.map(g => g.tipoHuevo)).toEqual(['Primera', 'Pnc', TIPO_HUEVO_SIN_CATEGORIA]);
    });

    it('ordena los items de cada grupo por label', () => {
      const grupos = agruparItemsHuevoPorTipo([
        op(1, 'Zeta', 'Primera'),
        op(2, 'Alfa', 'Primera')
      ]);

      expect(grupos[0].items.map(i => i.label)).toEqual(['Alfa', 'Zeta']);
    });

    it('lista vacia devuelve cero grupos, no un grupo vacio', () => {
      expect(agruparItemsHuevoPorTipo([])).toEqual([]);
    });
  });

  describe('sumarCantidadesHuevo', () => {
    it('suma lo que es numero y valido', () => {
      expect(sumarCantidadesHuevo([1, '2', 3.5])).toBe(6.5);
    });

    it('ignora null, vacio, texto no numerico, negativos y cero', () => {
      // Suma defensiva: el input viene de un formulario, no de la base.
      expect(sumarCantidadesHuevo([null, undefined, '', 'abc', -5, 0, 4])).toBe(4);
    });

    it('lista vacia da 0', () => {
      expect(sumarCantidadesHuevo([])).toBe(0);
    });
  });

  // ===================== F7.3 · FILAS FIJAS =====================

  describe('construirFilasFijasHuevo', () => {
    const declarado = (catalogItemId: number, nombre: string, tipoHuevo: string | null, extra: Partial<HuevoFilaFija> = {}): HuevoFilaFija => ({
      catalogItemId, codigo: String(catalogItemId), nombre, tipoHuevo, um: 'UND',
      primeraPostura: false, huerfano: false, fueraDeVigencia: false, ...extra
    });

    it('agrupa Primera antes que Pnc y ordena por nombre dentro del grupo', () => {
      const grupos = construirFilasFijasHuevo(
        [
          declarado(3, 'Zeta pnc', 'Pnc'),
          declarado(1, 'Beta primera', 'Primera'),
          declarado(2, 'Alfa primera', 'Primera')
        ],
        [], null, null);

      expect(grupos.map(g => g.tipoHuevo)).toEqual(['Primera', 'Pnc']);
      expect(grupos[0].filas.map(f => f.nombre)).toEqual(['Alfa primera', 'Beta primera']);
    });

    it('lote sin tipos declarados da CERO grupos: fail-closed, no cae al catalogo', () => {
      // El punto de F7.3. Si esto empieza a devolver filas, el control que pidio el cliente se fue.
      expect(construirFilasFijasHuevo([], [], null, null)).toEqual([]);
    });

    it('conserva un item GUARDADO que el lote ya no declara, marcado como huerfano', () => {
      // El lote pudo cambiar su declaracion despues de que el registro se guardo. Perder ese dato
      // en silencio al editar seria peor que mostrarlo con una marca.
      const grupos = construirFilasFijasHuevo(
        [declarado(1, 'Vigente', 'Primera')],
        [{ catalogItemId: 99, codigo: 'H099', nombre: 'Ya no declarado', tipoHuevo: 'Pnc', um: 'UND' }],
        null, null);

      const todas = grupos.flatMap(g => g.filas);
      expect(todas.length).toBe(2);
      expect(todas.find(f => f.catalogItemId === 99)!.huerfano).toBeTrue();
      expect(todas.find(f => f.catalogItemId === 1)!.huerfano).toBeFalse();
    });

    it('no duplica un item que esta declarado Y guardado', () => {
      const grupos = construirFilasFijasHuevo(
        [declarado(1, 'Rojo', 'Primera')],
        [{ catalogItemId: 1, codigo: '1', nombre: 'Rojo', tipoHuevo: 'Primera', um: 'UND' }],
        null, null);

      expect(grupos.flatMap(g => g.filas).length).toBe(1);
    });

    it('marca fueraDeVigencia solo a los de primera postura y solo pasada la semana limite', () => {
      const declarados = [
        declarado(1, 'Primera postura', 'Primera', { primeraPostura: true }),
        declarado(2, 'Primera comun', 'Primera')
      ];

      const vigente = construirFilasFijasHuevo(declarados, [], 22, 22).flatMap(g => g.filas);
      expect(vigente.find(f => f.catalogItemId === 1)!.fueraDeVigencia).toBeFalse();

      const vencido = construirFilasFijasHuevo(declarados, [], 23, 22).flatMap(g => g.filas);
      expect(vencido.find(f => f.catalogItemId === 1)!.fueraDeVigencia).toBeTrue();
      // El que NO es de primera postura nunca vence.
      expect(vencido.find(f => f.catalogItemId === 2)!.fueraDeVigencia).toBeFalse();
    });

    it('sin limite configurado o sin semana calculable no vence nada (fail-open)', () => {
      const declarados = [declarado(1, 'Primera postura', 'Primera', { primeraPostura: true })];

      expect(construirFilasFijasHuevo(declarados, [], 99, null).flatMap(g => g.filas)[0].fueraDeVigencia).toBeFalse();
      expect(construirFilasFijasHuevo(declarados, [], null, 22).flatMap(g => g.filas)[0].fueraDeVigencia).toBeFalse();
    });

    it('el item huerfano NUNCA se marca de primera postura ni fuera de vigencia', () => {
      // No se conoce su metadata actual, y la vigencia solo decide que se OFRECE: nunca bloquea
      // lo que ya estaba guardado.
      const grupos = construirFilasFijasHuevo(
        [],
        [{ catalogItemId: 99, codigo: null, nombre: null, tipoHuevo: null, um: null }],
        30, 22);

      const fila = grupos.flatMap(g => g.filas)[0];
      expect(fila.primeraPostura).toBeFalse();
      expect(fila.fueraDeVigencia).toBeFalse();
      expect(fila.nombre).toBe('Ítem 99');
    });
  });

  describe('esItemEnKilos', () => {
    it('reconoce KIL tolerando capitalizacion y espacios', () => {
      expect(esItemEnKilos('KIL')).toBeTrue();
      expect(esItemEnKilos(' kil ')).toBeTrue();
    });

    it('todo lo demas se cuenta en unidades', () => {
      // D2: solo los que se PESAN admiten decimales. Un falso positivo aca dejaria pasar
      // decimales a un contrato entero y volveria el redondeo silencioso.
      expect(esItemEnKilos('UND')).toBeFalse();
      expect(esItemEnKilos(null)).toBeFalse();
      expect(esItemEnKilos(undefined)).toBeFalse();
      expect(esItemEnKilos('')).toBeFalse();
    });
  });
});

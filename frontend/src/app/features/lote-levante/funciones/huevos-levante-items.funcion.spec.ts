import {
  cantidadesDesdeGuardados,
  construirHuevoItemsPayloadLevante,
  ContextoTabHuevosLevante,
  mostrarTabHuevosLevante,
  resolverModoHuevosLevante,
  subtotalCantidades,
  tiposDelLoteAFilas,
  validarHuevoItemsLevante
} from './huevos-levante-items.funcion';
import { permiteHuevosEnLevante } from './semana-vida-levante.funcion';
import { HuevoFilaFija } from '../../lote-produccion/models/huevo-clasificacion.model';

describe('huevos-levante-items.funcion (Santa Reyes: tipos del lote en levante)', () => {
  const ENCASET = '2026-01-01';
  const SEMANA_17 = '2026-04-29'; // día 118 ⇒ último día de la semana 17
  const SEMANA_18 = '2026-04-30'; // día 119 ⇒ primer día de la semana 18

  const fila = (catalogItemId: number, extra: Partial<HuevoFilaFija> = {}): HuevoFilaFija => ({
    catalogItemId,
    codigo: String(catalogItemId),
    nombre: `HUEVO ${catalogItemId}`,
    tipoHuevo: 'Primera',
    um: 'UND',
    primeraPostura: false,
    huerfano: false,
    fueraDeVigencia: false,
    ...extra
  });

  const ctx = (extra: Partial<ContextoTabHuevosLevante> = {}): ContextoTabHuevosLevante => ({
    modo: 'porItems',
    fechaEncaset: ENCASET,
    fechaRegistro: SEMANA_18,
    desdeSemana: 18,
    tiposDelLote: { cargando: false, error: false, cantidad: 3 },
    ...extra
  });

  describe('permiteHuevosEnLevante con semana mínima', () => {
    it('sin semana mínima es idéntico a la versión de siempre', () => {
      for (const fecha of ['2025-12-31', ENCASET, '2026-02-15', SEMANA_17, SEMANA_18, '2027-01-01']) {
        expect(permiteHuevosEnLevante(ENCASET, fecha, null)).toBe(permiteHuevosEnLevante(ENCASET, fecha));
      }
    });

    it('con semana 18 habilita desde el primer día de la semana 18', () => {
      expect(permiteHuevosEnLevante(ENCASET, SEMANA_17, 18)).toBeFalse();
      expect(permiteHuevosEnLevante(ENCASET, SEMANA_18, 18)).toBeTrue();
    });

    it('sin fecha de encaset no hay semana evaluable y se permite', () => {
      expect(permiteHuevosEnLevante(null, SEMANA_17, 18)).toBeTrue();
    });
  });

  describe('resolverModoHuevosLevante', () => {
    it('combina captura en levante y clasificación por ítems', () => {
      expect(resolverModoHuevosLevante(false, false)).toBe('ninguno');
      expect(resolverModoHuevosLevante(false, true)).toBe('ninguno');
      expect(resolverModoHuevosLevante(true, false)).toBe('clasificadora');
      expect(resolverModoHuevosLevante(true, true)).toBe('porItems');
    });
  });

  describe('mostrarTabHuevosLevante', () => {
    it('sin captura en levante nunca muestra el tab', () => {
      expect(mostrarTabHuevosLevante(ctx({ modo: 'ninguno' }))).toBeFalse();
    });

    it('clasificadora fija (Sanmarino) ignora los tipos del lote', () => {
      expect(mostrarTabHuevosLevante(ctx({
        modo: 'clasificadora', desdeSemana: null, fechaRegistro: '2026-01-10',
        tiposDelLote: { cargando: true, error: true, cantidad: 0 }
      }))).toBeTrue();
    });

    it('por ítems con tipos declarados y semana alcanzada muestra el tab', () => {
      expect(mostrarTabHuevosLevante(ctx())).toBeTrue();
    });

    it('por ítems: lote sin tipos declarados no muestra el tab', () => {
      expect(mostrarTabHuevosLevante(ctx({ tiposDelLote: { cargando: false, error: false, cantidad: 0 } }))).toBeFalse();
    });

    it('por ítems: mientras carga o si falló la consulta no muestra el tab', () => {
      expect(mostrarTabHuevosLevante(ctx({ tiposDelLote: { cargando: true, error: false, cantidad: 3 } }))).toBeFalse();
      expect(mostrarTabHuevosLevante(ctx({ tiposDelLote: { cargando: false, error: true, cantidad: 3 } }))).toBeFalse();
    });

    it('antes de la semana mínima no muestra el tab', () => {
      expect(mostrarTabHuevosLevante(ctx({ fechaRegistro: SEMANA_17 }))).toBeFalse();
    });
  });

  describe('filas, cantidades y payload', () => {
    it('tiposDelLoteAFilas conserva los datos del tipo y arranca sin marcas', () => {
      const [f] = tiposDelLoteAFilas([{
        id: 1, loteId: 156, catalogItemId: 678, codigo: null, nombre: 'HUEVO BLANCO',
        tipoHuevo: 'Primera', um: 'UND', primeraPostura: true, itemActivo: true, activo: true
      }]);
      expect(f).toEqual(jasmine.objectContaining({
        catalogItemId: 678, codigo: '', nombre: 'HUEVO BLANCO', primeraPostura: true, huerfano: false, fueraDeVigencia: false
      }));
    });

    it('cantidadesDesdeGuardados rehidrata por catalogItemId', () => {
      expect(cantidadesDesdeGuardados([{ catalogItemId: 678, cantidad: 120 }, { catalogItemId: 0, cantidad: 5 }]))
        .toEqual({ 678: 120 });
    });

    it('subtotalCantidades suma solo lo positivo', () => {
      expect(subtotalCantidades([fila(1), fila(2), fila(3)], { 1: 10, 2: null, 3: -4 })).toBe(10);
    });

    it('el payload descarta vacíos y ceros y completa etiquetas de la fila o del guardado', () => {
      const payload = construirHuevoItemsPayloadLevante(
        [fila(678), fila(679, { codigo: '', nombre: '', tipoHuevo: null, um: null }), fila(680)],
        { 678: 100, 679: 7, 680: 0 },
        [{ catalogItemId: 679, codigo: 'P9', nombre: 'HUEVO PNC', tipoHuevo: 'Pnc', cantidad: 3, um: 'UND' }]
      );
      expect(payload).toEqual([
        { catalogItemId: 678, codigo: '678', nombre: 'HUEVO 678', tipoHuevo: 'Primera', cantidad: 100, um: 'UND' },
        { catalogItemId: 679, codigo: 'P9', nombre: 'HUEVO PNC', tipoHuevo: 'Pnc', cantidad: 7, um: 'UND' }
      ]);
    });
  });

  describe('validarHuevoItemsLevante', () => {
    it('sin problemas devuelve null', () => {
      expect(validarHuevoItemsLevante([fila(1), fila(2)], { 1: 50, 2: null })).toBeNull();
    });

    it('rechaza negativos, decimales y primera postura fuera de vigencia con cantidad', () => {
      expect(validarHuevoItemsLevante([fila(1)], { 1: -1 })).toContain('negativa');
      expect(validarHuevoItemsLevante([fila(1)], { 1: 2.5 })).toContain('entero');
      expect(validarHuevoItemsLevante([fila(1, { um: 'KIL' })], { 1: 2.5 })).toContain('kilos');
      expect(validarHuevoItemsLevante([fila(1, { fueraDeVigencia: true })], { 1: 5 })).toContain('fuera de vigencia');
    });

    it('una fila fuera de vigencia sin cantidad no bloquea', () => {
      expect(validarHuevoItemsLevante([fila(1, { fueraDeVigencia: true })], { 1: 0 })).toBeNull();
    });
  });
});

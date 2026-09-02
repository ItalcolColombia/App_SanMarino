import { resumirCapturasPendientes } from './resumir-capturas-pendientes.funcion';
import type { OperacionPendiente } from '../models/outbox.model';

const PARTICION = 'u1|1|1';

function op(over: Partial<OperacionPendiente> = {}): OperacionPendiente {
  return {
    clientOpId: over.clientOpId ?? 'op-1',
    particion: over.particion ?? PARTICION,
    tipo: over.tipo ?? 'seguimiento_levante_crear',
    companyId: 1,
    paisId: 1,
    userId: 'u1',
    deviceId: 'dev-1',
    capturadoAtDispositivo: over.capturadoAtDispositivo ?? '2026-08-12T10:00:00.000Z',
    metodo: 'POST',
    url: '/api/SeguimientoLoteLevante',
    payload: over.payload ?? { loteId: 'A374A', fechaRegistro: '2026-08-12T00:00:00Z' },
    estado: over.estado ?? 'pendiente',
    intentos: 0,
    proximoIntentoEn: null,
    creadoEn: 1,
    ...over
  } as OperacionPendiente;
}

const CRITERIO = {
  tipo: 'seguimiento_levante_crear',
  particion: PARTICION,
  lote: { loteId: 'A374A' }
} as const;

describe('resumirCapturasPendientes', () => {
  it('devuelve la captura de este lote, con su fecha recortada a YYYY-MM-DD', () => {
    const r = resumirCapturasPendientes([op()], CRITERIO);

    expect(r.length).toBe(1);
    expect(r[0].clientOpId).toBe('op-1');
    expect(r[0].fecha).toBe('2026-08-12');
    expect(r[0].rechazada).toBeFalse();
  });

  it('NO devuelve el payload ni ningún número capturado', () => {
    // Es la garantía estructural de que una captura no puede entrar al Excel ni a los indicadores:
    // no hay número que copiar. Si alguien agrega el payload al resumen, este test falla.
    const r = resumirCapturasPendientes(
      [op({ payload: { loteId: 'A374A', fechaRegistro: '2026-08-12', mortalidadHembras: 999 } })],
      CRITERIO
    );

    expect(Object.keys(r[0]).sort()).toEqual(['capturadoAt', 'clientOpId', 'fecha', 'rechazada']);
    expect(JSON.stringify(r)).not.toContain('999');
  });

  it('ignora las capturas de OTRO lote', () => {
    const otro = op({ clientOpId: 'op-2', payload: { loteId: 'B999', fechaRegistro: '2026-08-12' } });

    const r = resumirCapturasPendientes([op(), otro], CRITERIO);

    expect(r.map(x => x.clientOpId)).toEqual(['op-1']);
  });

  it('ignora las capturas de OTRA partición — son de otro operario o de otra empresa', () => {
    const ajena = op({ clientOpId: 'op-ajena', particion: 'u2|1|1' });

    const r = resumirCapturasPendientes([op(), ajena], CRITERIO);

    expect(r.map(x => x.clientOpId)).toEqual(['op-1']);
  });

  it('ignora las capturas de OTRO tipo — pollo y reproductora comparten el cuerpo', () => {
    // El payload es idéntico; lo único que las separa es el tipo. Confundirlas mostraría en la
    // pantalla de engorde una captura de reproductora, que va a otra tabla.
    const reproductora = op({ clientOpId: 'op-repro', tipo: 'seguimiento_reproductora_engorde_crear' });

    const r = resumirCapturasPendientes([op(), reproductora], CRITERIO);

    expect(r.map(x => x.clientOpId)).toEqual(['op-1']);
  });

  it('compara el id de lote entre número y cadena — producción lo manda numérico', () => {
    const produccion = op({
      clientOpId: 'op-prod',
      tipo: 'seguimiento_produccion_crear',
      payload: { lotePosturaProduccionId: 1234, fechaRegistro: '2026-08-12' }
    });

    const r = resumirCapturasPendientes([produccion], {
      tipo: 'seguimiento_produccion_crear',
      particion: PARTICION,
      lote: { lotePosturaProduccionId: '1234' }   // la pantalla lo tiene como texto
    });

    expect(r.length).toBe(1);
  });

  it('produccion: encuentra la captura por CUALQUIERA de sus dos ids de lote', () => {
    // El payload lleva lotePosturaProduccionId en el flujo nuevo y produccionLoteId en el legacy;
    // uno de los dos siempre viene null, y los valores son DISTINTOS entre si. Con un solo campo,
    // la mitad de las capturas no se encontraria -- sin error y sin aviso.
    const nuevo = op({
      clientOpId: 'lpp',
      tipo: 'seguimiento_produccion_crear',
      payload: { lotePosturaProduccionId: 1234, produccionLoteId: null, fechaRegistro: '2026-08-12' }
    });
    const legacy = op({
      clientOpId: 'legacy',
      tipo: 'seguimiento_produccion_crear',
      payload: { lotePosturaProduccionId: null, produccionLoteId: 77, fechaRegistro: '2026-08-13' }
    });

    const r = resumirCapturasPendientes([nuevo, legacy], {
      tipo: 'seguimiento_produccion_crear',
      particion: PARTICION,
      lote: { lotePosturaProduccionId: 1234, produccionLoteId: 77 }
    });

    expect(r.map(x => x.clientOpId)).toEqual(['lpp', 'legacy']);
  });

  it('produccion: un id nulo NO hace coincidir a las capturas que traen ese campo en null', () => {
    // Sin la guarda, `null === null` casaria con TODA captura del flujo contrario.
    const legacy = op({
      clientOpId: 'legacy',
      tipo: 'seguimiento_produccion_crear',
      payload: { lotePosturaProduccionId: null, produccionLoteId: 77, fechaRegistro: '2026-08-13' }
    });

    const r = resumirCapturasPendientes([legacy], {
      tipo: 'seguimiento_produccion_crear',
      particion: PARTICION,
      lote: { lotePosturaProduccionId: 1234, produccionLoteId: null }
    });

    expect(r).toEqual([]);
  });

  it('reproductora: la fecha viene en `fecha`, no en `fechaRegistro`', () => {
    // Su modal arma el payload a mano y usa otro nombre. Leyendo solo `fechaRegistro`, esa pantalla
    // mostraria la captura SIN dia -- justo el dato que sirve para reconocerla.
    const repro = op({
      tipo: 'seguimiento_reproductora_engorde_crear',
      payload: { reproductoraId: 55, loteId: 7, fecha: '2026-08-12T12:00:00Z' }
    });

    const r = resumirCapturasPendientes([repro], {
      tipo: 'seguimiento_reproductora_engorde_crear',
      particion: PARTICION,
      lote: { reproductoraId: 55 }
    });

    expect(r.length).toBe(1);
    expect(r[0].fecha).toBe('2026-08-12');
  });

  it('marca las rechazadas', () => {
    const r = resumirCapturasPendientes([op({ estado: 'rechazada' })], CRITERIO);

    expect(r[0].rechazada).toBeTrue();
  });

  it('ordena por fecha del registro y desempata por momento de captura', () => {
    const dia13 = op({ clientOpId: 'b', payload: { loteId: 'A374A', fechaRegistro: '2026-08-13' } });
    const dia12tarde = op({
      clientOpId: 'c',
      capturadoAtDispositivo: '2026-08-12T18:00:00.000Z',
      payload: { loteId: 'A374A', fechaRegistro: '2026-08-12' }
    });

    const r = resumirCapturasPendientes([dia13, dia12tarde, op()], CRITERIO);

    expect(r.map(x => x.clientOpId)).toEqual(['op-1', 'c', 'b']);
  });

  describe('fail-closed', () => {
    it('sin partición no devuelve nada', () => {
      expect(resumirCapturasPendientes([op()], { ...CRITERIO, particion: null })).toEqual([]);
    });

    it('sin lote seleccionado no devuelve nada', () => {
      expect(resumirCapturasPendientes([op()], { ...CRITERIO, lote: { loteId: null } })).toEqual([]);
      expect(resumirCapturasPendientes([op()], { ...CRITERIO, lote: { loteId: '' } })).toEqual([]);
      expect(resumirCapturasPendientes([op()], { ...CRITERIO, lote: {} })).toEqual([]);
    });

    it('el 0 cuenta como ausencia, no como id de lote', () => {
      // Un `!= null` dejaría pasar el 0 y compararía contra un lote que no existe.
      const cero = op({ payload: { loteId: 0, fechaRegistro: '2026-08-12' } });

      expect(resumirCapturasPendientes([cero], { ...CRITERIO, lote: { loteId: 0 } })).toEqual([]);
    });

    it('sin operaciones, o con la cola nula, devuelve un arreglo vacío', () => {
      expect(resumirCapturasPendientes([], CRITERIO)).toEqual([]);
      expect(resumirCapturasPendientes(null, CRITERIO)).toEqual([]);
      expect(resumirCapturasPendientes(undefined, CRITERIO)).toEqual([]);
    });
  });

  it('tolera un payload sin fecha o con una fecha ilegible', () => {
    const sinFecha = op({ clientOpId: 'sf', payload: { loteId: 'A374A' } });
    const basura = op({ clientOpId: 'bs', payload: { loteId: 'A374A', fechaRegistro: 'ayer nomas' } });

    const r = resumirCapturasPendientes([sinFecha, basura], CRITERIO);

    expect(r.length).toBe(2);
    expect(r.every(x => x.fecha === null)).toBeTrue();
  });

  it('tolera un payload que no es un objeto', () => {
    expect(resumirCapturasPendientes([op({ payload: 'texto suelto' })], CRITERIO)).toEqual([]);
    expect(resumirCapturasPendientes([op({ payload: null })], CRITERIO)).toEqual([]);
  });
});

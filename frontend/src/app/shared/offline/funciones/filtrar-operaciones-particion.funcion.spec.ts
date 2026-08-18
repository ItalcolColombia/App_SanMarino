import { filtrarOperacionesParticion } from './filtrar-operaciones-particion.funcion';
import type { IdentidadParticion } from '../models/offline.model';
import type { OperacionPendiente } from '../models/outbox.model';

/**
 * Quién puede empujar qué.
 *
 * Es la guarda que impide que la captura de un operario salga firmada por el que agarró la tablet
 * después. Los dos errores se pagan distinto: dejar pasar una operación ajena falsifica la autoría
 * de un dato de campo (irreversible); dejar una propia sin enviar la conserva en la cola
 * (reversible). Por eso ante la duda no se envía.
 */
describe('filtrarOperacionesParticion', () => {
  const AHORA = 1_700_000_000_000;

  const alex: IdentidadParticion = { userId: 'guid-alex', companyId: 1, paisId: 1 };
  const lady: IdentidadParticion = { userId: 'guid-lady', companyId: 3, paisId: 2 };

  /** Una operación encolada, con la partición ya resuelta como la arma `OutboxService.encolar`. */
  function op(parcial: Partial<OperacionPendiente> & { clientOpId: string; particion: string }): OperacionPendiente {
    return {
      tipo: 'seguimiento_levante',
      companyId: 1,
      paisId: 1,
      userId: 'guid-alex',
      deviceId: 'tablet-1',
      capturadoAtDispositivo: '2026-08-18T10:00:00.000Z',
      metodo: 'POST',
      url: '/api/SeguimientoLoteLevante',
      payload: { loteId: 116 },
      estado: 'pendiente',
      intentos: 0,
      proximoIntentoEn: null,
      creadoEn: AHORA - 60_000,
      ...parcial
    };
  }

  const deAlex = op({ clientOpId: 'op-alex', particion: 'guid-alex|1|1' });
  const deLady = op({ clientOpId: 'op-lady', particion: 'guid-lady|3|2', companyId: 3, paisId: 2, userId: 'guid-lady' });

  it('🔑 con dos particiones en la cola devuelve SOLO la activa', () => {
    expect(filtrarOperacionesParticion([deAlex, deLady], alex, AHORA)).toEqual([deAlex]);
    expect(filtrarOperacionesParticion([deAlex, deLady], lady, AHORA)).toEqual([deLady]);
  });

  it('🔑 las ajenas quedan en la cola: no se borran, no se marcan, no se tocan', () => {
    const cola = [deAlex, deLady];
    const copia = JSON.parse(JSON.stringify(cola)) as OperacionPendiente[];

    filtrarOperacionesParticion(cola, alex, AHORA);

    // La función es pura: el arreglo de entrada y cada fila siguen exactamente como estaban.
    expect(cola).toEqual(copia);
    expect(deLady.estado).toBe('pendiente');
  });

  it('la partición se compara completa: misma empresa y país, otro usuario, NO entra', () => {
    // El caso de la tablet compartida en una sola granja. Coinciden 2 de los 3 identificadores y
    // eso no alcanza: el autor lo estampa el servidor desde el token.
    const otroDeLaMismaEmpresa = op({ clientOpId: 'op-otro', particion: 'guid-otro|1|1', userId: 'guid-otro' });

    expect(filtrarOperacionesParticion([otroDeLaMismaEmpresa], alex, AHORA)).toEqual([]);
  });

  describe('estado', () => {
    it('lo rechazado no sale: espera a una persona en la bandeja', () => {
      const rechazada = op({
        clientOpId: 'op-rech',
        particion: 'guid-alex|1|1',
        estado: 'rechazada',
        errorCodigo: 'validacion'
      });

      expect(filtrarOperacionesParticion([rechazada], alex, AHORA)).toEqual([]);
    });
  });

  describe('backoff', () => {
    it('sin próximo intento (null) sale ya', () => {
      expect(filtrarOperacionesParticion([deAlex], alex, AHORA)).toEqual([deAlex]);
    });

    it('con el próximo intento vencido sale', () => {
      const vencido = op({ clientOpId: 'op-v', particion: 'guid-alex|1|1', proximoIntentoEn: AHORA - 1 });
      expect(filtrarOperacionesParticion([vencido], alex, AHORA)).toEqual([vencido]);
    });

    it('justo en el instante del próximo intento sale (el límite es inclusivo, igual que hoy)', () => {
      const justo = op({ clientOpId: 'op-j', particion: 'guid-alex|1|1', proximoIntentoEn: AHORA });
      expect(filtrarOperacionesParticion([justo], alex, AHORA)).toEqual([justo]);
    });

    it('con el próximo intento en el futuro NO sale', () => {
      const futuro = op({ clientOpId: 'op-f', particion: 'guid-alex|1|1', proximoIntentoEn: AHORA + 1 });
      expect(filtrarOperacionesParticion([futuro], alex, AHORA)).toEqual([]);
    });
  });

  describe('fail-closed: sin identidad completa no se envía NADA', () => {
    const casos: Array<[string, IdentidadParticion | null]> = [
      ['sin sesión (null)', null],
      ['sin usuario', { userId: null, companyId: 1, paisId: 1 }],
      ['sin empresa', { userId: 'guid-alex', companyId: null, paisId: 1 }],
      ['sin país', { userId: 'guid-alex', companyId: 1, paisId: null }],
      ['empresa 0 (cuenta como ausencia)', { userId: 'guid-alex', companyId: 0, paisId: 1 }],
      ['usuario vacío', { userId: '', companyId: 1, paisId: 1 }]
    ];

    for (const [nombre, identidad] of casos) {
      it(`${nombre} ⇒ []`, () => {
        // Es el escenario del logout: la cola sobrevive (R9) y no hay a nombre de quién empujarla.
        expect(filtrarOperacionesParticion([deAlex, deLady], identidad, AHORA)).toEqual([]);
      });
    }
  });

  it('🔑 una fila con la partición corrupta NO se cuela cuando tampoco hay sesión', () => {
    // IndexedDB no valida tipos: una fila vieja o a medio escribir puede tener `particion` nula. Sin
    // el corte temprano, ese `null === null` la haría pasar justo en el escenario más delicado —sin
    // sesión— y saldría con el token del próximo que entre.
    const corrupta = { ...deAlex, clientOpId: 'op-corrupta', particion: null as unknown as string };

    expect(filtrarOperacionesParticion([corrupta], null, AHORA)).toEqual([]);
    expect(filtrarOperacionesParticion([corrupta], { userId: null, companyId: null, paisId: null }, AHORA)).toEqual([]);
  });

  it('cola vacía, nula o indefinida ⇒ []', () => {
    expect(filtrarOperacionesParticion([], alex, AHORA)).toEqual([]);
    expect(filtrarOperacionesParticion(null, alex, AHORA)).toEqual([]);
    expect(filtrarOperacionesParticion(undefined, alex, AHORA)).toEqual([]);
  });

  it('conserva el orden en que vino la cola: se envían como se capturaron', () => {
    // `leerTodasLasOperaciones` las entrega por `creadoEn`, y el orden importa: dos capturas del
    // mismo lote aplicadas al revés dejan el saldo distinto.
    const vieja = op({ clientOpId: 'op-1', particion: 'guid-alex|1|1', creadoEn: 1 });
    const media = op({ clientOpId: 'op-2', particion: 'guid-alex|1|1', creadoEn: 2 });
    const nueva = op({ clientOpId: 'op-3', particion: 'guid-alex|1|1', creadoEn: 3 });

    const salida = filtrarOperacionesParticion([vieja, deLady, media, nueva], alex, AHORA);

    expect(salida.map(o => o.clientOpId)).toEqual(['op-1', 'op-2', 'op-3']);
  });
});

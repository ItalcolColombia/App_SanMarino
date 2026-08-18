import { clasificarCapturasDiagnostico } from './clasificar-capturas-diagnostico.funcion';
import type { IdentidadParticion } from '../../../shared/offline/models/offline.model';
import type { OperacionPendiente } from '../../../shared/offline/models/outbox.model';

/**
 * Qué ve quien abre la pantalla de rescate.
 *
 * `/diagnostico` se abre **sin sesión** a propósito, así que el caso por defecto —nadie logueado— es
 * el que tiene que salir bien: ahí no hay nada propio y todo va enmascarado.
 */
describe('clasificarCapturasDiagnostico', () => {
  const alex: IdentidadParticion = { userId: 'guid-alex', companyId: 1, paisId: 1 };

  function op(clientOpId: string, particion: string): OperacionPendiente {
    return {
      clientOpId,
      particion,
      tipo: 'seguimiento_levante',
      companyId: 1,
      paisId: 1,
      userId: 'guid-alex',
      deviceId: 'tablet-1',
      capturadoAtDispositivo: '2026-08-18T10:00:00.000Z',
      metodo: 'POST',
      url: '/api/SeguimientoLoteLevante',
      payload: { loteId: 116, mortalidadHembras: 3 },
      estado: 'pendiente',
      intentos: 0,
      proximoIntentoEn: null,
      creadoEn: 1
    };
  }

  const deAlex = op('op-alex', 'guid-alex|1|1');
  const deLady = op('op-lady', 'guid-lady|3|2');

  it('🔑 la de la sesión activa es propia; la de otro, no', () => {
    const filas = clasificarCapturasDiagnostico([deAlex, deLady], alex);

    expect(filas.map(f => f.propia)).toEqual([true, false]);
  });

  it('🔑 sin sesión NADA es propio: es como se abre la pantalla en un rescate', () => {
    expect(clasificarCapturasDiagnostico([deAlex, deLady], null).every(f => !f.propia)).toBe(true);
  });

  it('identidad incompleta ⇒ nada es propio (fail-closed, igual que la caché)', () => {
    const casos: IdentidadParticion[] = [
      { userId: null, companyId: 1, paisId: 1 },
      { userId: 'guid-alex', companyId: null, paisId: 1 },
      { userId: 'guid-alex', companyId: 1, paisId: null },
      { userId: 'guid-alex', companyId: 0, paisId: 1 }
    ];

    for (const identidad of casos) {
      expect(clasificarCapturasDiagnostico([deAlex], identidad)[0].propia).toBe(false);
    }
  });

  it('mismo usuario en OTRA empresa: no es propia', () => {
    // El operario multiempresa que cambió de empresa. Su captura sigue siendo suya, pero esta
    // sesión no puede empujarla (el servidor exige que la empresa de la sesión coincida), así que
    // tampoco se le ofrece descartarla acá.
    expect(clasificarCapturasDiagnostico([deAlex], { userId: 'guid-alex', companyId: 3, paisId: 1 })[0].propia)
      .toBe(false);
  });

  it('una fila con la partición corrupta nunca es propia', () => {
    const corrupta = { ...deAlex, particion: null as unknown as string };

    expect(clasificarCapturasDiagnostico([corrupta], alex)[0].propia).toBe(false);
    expect(clasificarCapturasDiagnostico([corrupta], null)[0].propia).toBe(false);
  });

  it('devuelve la MISMA cola, en el mismo orden: listar todo es parte del fix', () => {
    // Esconder las ajenas sería la peor variante de "se perdió": el operario tiene que poder ver
    // que su captura sigue ahí aunque esta sesión no la pueda tocar.
    const filas = clasificarCapturasDiagnostico([deLady, deAlex], alex);

    expect(filas.length).toBe(2);
    expect(filas.map(f => f.operacion.clientOpId)).toEqual(['op-lady', 'op-alex']);
  });

  it('no muta las operaciones: la fila apunta a la original', () => {
    const filas = clasificarCapturasDiagnostico([deAlex], alex);

    expect(filas[0].operacion).toBe(deAlex);
  });

  it('cola vacía, nula o indefinida ⇒ []', () => {
    expect(clasificarCapturasDiagnostico([], alex)).toEqual([]);
    expect(clasificarCapturasDiagnostico(null, alex)).toEqual([]);
    expect(clasificarCapturasDiagnostico(undefined, alex)).toEqual([]);
  });
});

import { clasificarResultadoPush, debeFrenar } from './clasificar-resultado-push.funcion';
import type { ResultadoPush } from '../models/outbox.model';

/**
 * Qué se hace con cada respuesta del servidor.
 *
 * Es la decisión más delicada del módulo: **borrar de más pierde trabajo de campo** y **borrar de
 * menos duplica**. Por eso solo se borra ante una confirmación explícita.
 */
describe('clasificarResultadoPush', () => {
  const base: ResultadoPush = { clientOpId: 'op-1', estado: 'aplicada' };

  it('aplicada ⇒ se saca de la cola', () => {
    expect(clasificarResultadoPush(base)).toBe('borrar');
  });

  it('🔑 aplicada con replay TAMBIÉN se saca: es la prueba de que la idempotencia actuó', () => {
    expect(clasificarResultadoPush({ ...base, replay: true })).toBe('borrar');
  });

  it('requiere_cuadre se saca: el dato entró, el descuadre lo resuelve el supervisor', () => {
    expect(clasificarResultadoPush({ ...base, estado: 'requiere_cuadre' })).toBe('borrar');
  });

  it('sin respuesta para esa operación ⇒ se reintenta, no se borra', () => {
    // No se puede afirmar que se aplicó. Borrarla sería perder la captura.
    expect(clasificarResultadoPush(undefined)).toBe('reintentar');
    expect(clasificarResultadoPush(null)).toBe('reintentar');
  });

  describe('rechazos que necesitan a una persona', () => {
    for (const codigo of ['validacion', 'contrato_obsoleto', 'regla_de_negocio', 'duplicado_en_lote']) {
      it(`${codigo} ⇒ bandeja (reintentar daría exactamente lo mismo)`, () => {
        expect(clasificarResultadoPush({ ...base, estado: 'rechazada', errorCodigo: codigo })).toBe('bandeja');
      });
    }
  });

  describe('rechazos que pueden mejorar solos', () => {
    it('error_interno ⇒ reintentar', () => {
      expect(clasificarResultadoPush({ ...base, estado: 'rechazada', errorCodigo: 'error_interno' })).toBe('reintentar');
    });

    it('🔑 empresa_no_autorizada ⇒ reintentar, NO bandeja', () => {
      // Suele ser transitorio: le están corrigiendo la asignación al usuario. Mandarlo a la bandeja
      // haría que el galponero descarte una captura real por un permiso que se arregla solo.
      expect(clasificarResultadoPush({ ...base, estado: 'rechazada', errorCodigo: 'empresa_no_autorizada' }))
        .toBe('reintentar');
    });

    it('un rechazo sin código ⇒ reintentar (no se descarta nada por las dudas)', () => {
      expect(clasificarResultadoPush({ ...base, estado: 'rechazada' })).toBe('reintentar');
    });
  });

  it('un estado que este cliente no conoce no borra nada', () => {
    const futuro = { ...base, estado: 'en_revision' } as unknown as ResultadoPush;
    expect(clasificarResultadoPush(futuro)).toBe('reintentar');
  });
});

describe('debeFrenar', () => {
  it('429 frena el resto del lote', () => {
    expect(debeFrenar(429)).toBe(true);
  });

  it('503 frena', () => {
    expect(debeFrenar(503)).toBe(true);
  });

  it('un 500 puntual no frena: puede ser una operación mala, no el servidor caído', () => {
    expect(debeFrenar(500)).toBe(false);
  });

  it('sin status no frena', () => {
    expect(debeFrenar(null)).toBe(false);
  });
});

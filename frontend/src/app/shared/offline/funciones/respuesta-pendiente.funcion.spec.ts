import { esRespuestaPendiente } from './respuesta-pendiente.funcion';

/**
 * La pregunta que separa "lo guardó el servidor" de "lo guardó la tablet".
 *
 * Un falso positivo hace que la pantalla diga "pendiente de enviar" sobre algo que YA está en el
 * servidor. Un falso negativo hace que diga "guardado" sobre algo que todavía está en el equipo, que
 * es el peor resultado posible del módulo.
 */
describe('esRespuestaPendiente', () => {
  it('reconoce la respuesta sintética y devuelve el id de la operación', () => {
    expect(esRespuestaPendiente({ __offlinePendiente: true, clientOpId: 'abc-123' })).toBe('abc-123');
  });

  it('una respuesta real del servidor devuelve null', () => {
    expect(esRespuestaPendiente({ id: 1108, loteId: 116 })).toBeNull();
  });

  it('no se activa con la marca en falso', () => {
    expect(esRespuestaPendiente({ __offlinePendiente: false, clientOpId: 'abc' })).toBeNull();
  });

  it('🔑 exige el booleano exacto: un valor "parecido a true" no cuenta', () => {
    // Si el servidor algún día devolviera un campo con ese nombre y otro tipo, la pantalla no puede
    // empezar a decir "pendiente" sobre datos ya guardados.
    expect(esRespuestaPendiente({ __offlinePendiente: 'true', clientOpId: 'abc' })).toBeNull();
    expect(esRespuestaPendiente({ __offlinePendiente: 1, clientOpId: 'abc' })).toBeNull();
  });

  it('marcada pero sin id utilizable devuelve null: sin id no hay nada que mostrar ni rastrear', () => {
    expect(esRespuestaPendiente({ __offlinePendiente: true })).toBeNull();
    expect(esRespuestaPendiente({ __offlinePendiente: true, clientOpId: '   ' })).toBeNull();
    expect(esRespuestaPendiente({ __offlinePendiente: true, clientOpId: 42 })).toBeNull();
  });

  it('tolera nulos y tipos que no son objeto', () => {
    expect(esRespuestaPendiente(null)).toBeNull();
    expect(esRespuestaPendiente(undefined)).toBeNull();
    expect(esRespuestaPendiente('ok')).toBeNull();
    expect(esRespuestaPendiente(0)).toBeNull();
  });
});

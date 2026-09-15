import { resolverSemanaOpcionalParaGuardar } from './parametro-semana-opcional.funcion';

describe('resolverSemanaOpcionalParaGuardar (sentinel de borrado para semanas opcionales)', () => {
  it('un valor positivo se manda tal cual, editando o creando', () => {
    expect(resolverSemanaOpcionalParaGuardar(18, true)).toBe(18);
    expect(resolverSemanaOpcionalParaGuardar(18, false)).toBe(18);
    expect(resolverSemanaOpcionalParaGuardar('22', true)).toBe(22);
  });

  it('al EDITAR, vacío manda el sentinel 0 (pide borrar el límite)', () => {
    expect(resolverSemanaOpcionalParaGuardar(null, true)).toBe(0);
    expect(resolverSemanaOpcionalParaGuardar(undefined, true)).toBe(0);
    expect(resolverSemanaOpcionalParaGuardar('', true)).toBe(0);
  });

  it('al CREAR, vacío manda null (el alta no tiene "conservar" que pisar)', () => {
    expect(resolverSemanaOpcionalParaGuardar(null, false)).toBeNull();
    expect(resolverSemanaOpcionalParaGuardar('', false)).toBeNull();
  });

  it('0 o negativos se tratan como vacío (el rango real arranca en 1)', () => {
    expect(resolverSemanaOpcionalParaGuardar(0, true)).toBe(0);
    expect(resolverSemanaOpcionalParaGuardar(-3, true)).toBe(0);
    expect(resolverSemanaOpcionalParaGuardar(-3, false)).toBeNull();
  });
});

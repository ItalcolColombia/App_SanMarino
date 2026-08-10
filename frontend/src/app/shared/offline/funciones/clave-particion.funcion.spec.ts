import { claveEntrada, claveParticion } from './clave-particion.funcion';

/**
 * Esta función es la que impide que un operario vea los datos de otra empresa. Los casos de
 * `null` no son defensivos: cada uno es una forma concreta de colapsar dos sesiones en la misma
 * clave de caché.
 */
describe('claveParticion', () => {
  it('arma la clave con los tres identificadores', () => {
    expect(claveParticion({ userId: 'abc-123', companyId: 5, paisId: 1 })).toBe('abc-123|5|1');
  });

  it('🔴 dos empresas distintas dan claves DISTINTAS para el mismo usuario', () => {
    // Es el caso de la fuga: la misma URL (`GET /api/Lote`) con respuestas distintas según
    // la empresa activa, que viaja en un header que la caché del Service Worker ignoraría.
    const a = claveParticion({ userId: 'abc', companyId: 5, paisId: 1 });
    const b = claveParticion({ userId: 'abc', companyId: 6, paisId: 1 });

    expect(a).not.toBe(b);
  });

  it('el país también particiona (una empresa puede operar en varios)', () => {
    const ec = claveParticion({ userId: 'abc', companyId: 5, paisId: 2 });
    const pa = claveParticion({ userId: 'abc', companyId: 5, paisId: 3 });

    expect(ec).not.toBe(pa);
  });

  it('es FAIL-CLOSED: sin userId no hay clave', () => {
    expect(claveParticion({ userId: null, companyId: 5, paisId: 1 })).toBeNull();
    expect(claveParticion({ userId: undefined, companyId: 5, paisId: 1 })).toBeNull();
    expect(claveParticion({ userId: '', companyId: 5, paisId: 1 })).toBeNull();
  });

  it('es FAIL-CLOSED: sin companyId no hay clave', () => {
    expect(claveParticion({ userId: 'abc', companyId: null, paisId: 1 })).toBeNull();
    expect(claveParticion({ userId: 'abc', companyId: undefined, paisId: 1 })).toBeNull();
  });

  it('es FAIL-CLOSED: sin paisId no hay clave', () => {
    expect(claveParticion({ userId: 'abc', companyId: 5, paisId: null })).toBeNull();
  });

  it('🔴 el 0 cuenta como AUSENCIA, no como id válido', () => {
    // Un chequeo con `!= null` dejaría pasar el 0 y todas las sesiones sin empresa resuelta
    // colapsarían en la clave `abc|0|1`, leyendo lo que guardó la anterior.
    expect(claveParticion({ userId: 'abc', companyId: 0, paisId: 1 })).toBeNull();
    expect(claveParticion({ userId: 0, companyId: 5, paisId: 1 })).toBeNull();
  });

  it('tolera null/undefined como identidad completa', () => {
    expect(claveParticion(null)).toBeNull();
    expect(claveParticion(undefined)).toBeNull();
  });
});

describe('claveEntrada', () => {
  it('incluye partición, método y URL', () => {
    expect(claveEntrada('abc|5|1', 'get', '/api/Lote?farmId=3')).toBe('abc|5|1|GET /api/Lote?farmId=3');
  });

  it('normaliza el método a mayúsculas para que no haya claves gemelas', () => {
    expect(claveEntrada('p', 'get', '/x')).toBe(claveEntrada('p', 'GET', '/x'));
  });

  it('URLs distintas son entradas distintas (los parámetros importan)', () => {
    expect(claveEntrada('p', 'GET', '/api/Lote?farmId=3'))
      .not.toBe(claveEntrada('p', 'GET', '/api/Lote?farmId=4'));
  });
});
